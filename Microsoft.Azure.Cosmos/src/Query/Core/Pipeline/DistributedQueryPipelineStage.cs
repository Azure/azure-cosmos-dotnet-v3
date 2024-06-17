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
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class DistributedQueryPipelineStage : IQueryPipelineStage
    {
        private readonly ICosmosDistributedQueryClient cosmosDistributedQueryClient;

        private readonly SqlQuerySpec sqlQuerySpec;

        private readonly FeedRangeInternal feedRangeInternal;

        private readonly PartitionKey? partitionKey;

        private readonly QueryPaginationOptions queryPaginationOptions;

        private ContinuationToken continuationToken;

        private bool started;

        public TryCatch<QueryPage> Current { get; private set; }

        private DistributedQueryPipelineStage(
            ICosmosDistributedQueryClient cosmosDistributedQueryClient,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRangeInternal,
            PartitionKey? partitionKey,
            QueryPaginationOptions queryPaginationOptions,
            ContinuationToken continuationToken)
        {
            this.cosmosDistributedQueryClient = cosmosDistributedQueryClient ?? throw new ArgumentNullException(nameof(cosmosDistributedQueryClient));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.feedRangeInternal = feedRangeInternal ?? FeedRangeEpk.FullRange;
            this.partitionKey = partitionKey;
            this.queryPaginationOptions = queryPaginationOptions ?? throw new ArgumentNullException(nameof(queryPaginationOptions));
            this.continuationToken = continuationToken;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            ICosmosDistributedQueryClient cosmosDistributedQueryClient,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRangeInternal,
            PartitionKey? partitionKey,
            QueryPaginationOptions queryPaginationOptions,
            CosmosElement continuation)
        {
            TryCatch<ContinuationToken> continuationToken = ContinuationToken.MonadicCreate(continuation);
            if (continuationToken.Failed)
            {
                return TryCatch<IQueryPipelineStage>.FromException(continuationToken.Exception);
            }

            return TryCatch<IQueryPipelineStage>.FromResult(new DistributedQueryPipelineStage(
                cosmosDistributedQueryClient,
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

            TryCatch<QueryPage> tryCatchQueryPage = await this.cosmosDistributedQueryClient.MonadicQueryAsync(
                this.partitionKey,
                this.feedRangeInternal,
                this.sqlQuerySpec,
                this.continuationToken.BackendToken,
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

            this.continuationToken = new ContinuationToken(GetBackendContinuationToken(page.State?.Value));

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

        private static string GetBackendContinuationToken(CosmosElement continuation)
        {
            return (continuation != null && continuation is CosmosString continuationUtfAny) ?
                continuationUtfAny.Value.ToString() :
                null;
        }

        private readonly struct ContinuationToken
        {
            private const string TokenPropertyName = "DQC";

            public string BackendToken { get; }

            public ContinuationToken(string backendToken)
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
                    { TokenPropertyName, CosmosString.Create(this.BackendToken) }
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

                return TryCatch<ContinuationToken>.FromResult(new ContinuationToken(backendTokenString.Value));
            }
        }
    }
}