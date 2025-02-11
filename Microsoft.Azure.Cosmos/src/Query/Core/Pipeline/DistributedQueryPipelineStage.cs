// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class DistributedQueryPipelineStage : IQueryPipelineStage
    {
        private readonly IDocumentContainer documentContainer;

        private readonly SqlQuerySpec sqlQuerySpec;

        private readonly FeedRangeInternal feedRangeInternal;

        private readonly PartitionKey? partitionKey;

        private readonly QueryExecutionOptions queryPaginationOptions;

        private ContinuationToken continuationToken;

        private bool started;

        public TryCatch<QueryPage> Current { get; private set; }

        private DistributedQueryPipelineStage(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRangeInternal,
            PartitionKey? partitionKey,
            QueryExecutionOptions queryPaginationOptions,
            ContinuationToken continuationToken)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.feedRangeInternal = feedRangeInternal ?? FeedRangeEpk.FullRange;
            this.partitionKey = partitionKey;
            this.queryPaginationOptions = queryPaginationOptions ?? throw new ArgumentNullException(nameof(queryPaginationOptions));
            this.continuationToken = continuationToken;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRangeInternal,
            PartitionKey? partitionKey,
            QueryExecutionOptions queryPaginationOptions,
            CosmosElement continuation)
        {
            TryCatch<ContinuationToken> continuationToken = ContinuationToken.MonadicCreate(continuation);
            if (continuationToken.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(continuationToken.Exception);
            }

            return TryCatch<IQueryPipelineStage>.FromResult(new DistributedQueryPipelineStage(
                documentContainer,
                sqlQuerySpec,
                feedRangeInternal,
                partitionKey,
                queryPaginationOptions,
                continuationToken.Result));
        }

        public async ValueTask<bool> MoveNextAsync(Tracing.ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (this.started && this.continuationToken.BackendToken == null)
            {
                return false;
            }

            QueryState queryState = this.continuationToken.BackendToken == null ? null : new QueryState(this.continuationToken.BackendToken);
            FeedRangeState<QueryState> feedRangeState = new FeedRangeState<QueryState>(
                this.feedRangeInternal,
                queryState);
            TryCatch<QueryPage> tryCatchQueryPage = await this.documentContainer.MonadicQueryAsync(
                this.sqlQuerySpec,
                feedRangeState,
                this.queryPaginationOptions,
                trace,
                cancellationToken);

            this.started = true;
            if (tryCatchQueryPage.Failed)
            {
                this.Current = tryCatchQueryPage;
                return true;
            }

            QueryPage page = tryCatchQueryPage.Result;

            TryCatch<ContinuationToken> tryCatchContinuation = ContinuationToken.MonadicCreatefromBackendToken(page.State?.Value);
            if (tryCatchContinuation.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(tryCatchContinuation.Exception);
                return true;
            }

            this.continuationToken = tryCatchContinuation.Result;
            CosmosElement innerState = this.continuationToken.ToCosmosElement();
            QueryState state = innerState == null ? null : new QueryState(innerState);
            this.Current = TryCatch<QueryPage>.FromResult(new QueryPage(
                page.Documents,
                page.RequestCharge,
                page.ActivityId,
                page.CosmosQueryExecutionInfo,
                page.DistributionPlanSpec,
                disallowContinuationTokenMessage: null,
                page.AdditionalHeaders,
                state,
                page.Streaming));
            return true;
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }

        private readonly struct ContinuationToken
        {
            private const string TokenPropertyName = "DQC";

            public CosmosString BackendToken { get; }

            private ContinuationToken(CosmosString backendToken)
            {
                this.BackendToken = backendToken;
            }

            public CosmosElement ToCosmosElement()
            {
                if (this.BackendToken == null)
                {
                    return null;
                }

                return CosmosObject.Create(new Dictionary<string, CosmosElement>()
                {
                    { TokenPropertyName, this.BackendToken }
                });
            }

            public static TryCatch<ContinuationToken> MonadicCreate(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    return default;
                }

                if (!(cosmosElement is CosmosObject cosmosObject) ||
                    !cosmosObject.TryGetValue(TokenPropertyName, out CosmosElement backendTokenElement) ||
                    !(backendTokenElement is CosmosString backendTokenString))
                {
                    return TryCatch<ContinuationToken>.FromException(
                        new ArgumentException($"Invalid {nameof(DistributedQueryPipelineStage)} continuation token: {cosmosElement}"));
                }

                return TryCatch<ContinuationToken>.FromResult(new ContinuationToken(backendTokenString));
            }

            public static TryCatch<ContinuationToken> MonadicCreatefromBackendToken(CosmosElement continuation)
            {
                if (continuation == null)
                {
                    return TryCatch<ContinuationToken>.FromResult(default);
                }

                if (continuation is CosmosString continuationUtfAny)
                {
                    return TryCatch<ContinuationToken>.FromResult(new ContinuationToken(continuationUtfAny));
                }

                return TryCatch<ContinuationToken>.FromException(new ArgumentException(
                        $"Invalid {nameof(DistributedQueryPipelineStage)} continuation token: {continuation}"));
            }
        }
    }
}