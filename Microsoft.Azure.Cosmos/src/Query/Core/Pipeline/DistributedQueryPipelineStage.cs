// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class DistributedQueryPipelineStage : IQueryPipelineStage
    {
        private readonly ICosmosDistributedQueryClient cosmosDistributedQueryClient;

        private readonly SqlQuerySpec sqlQuerySpec;

        private readonly FeedRangeInternal feedRangeInternal;

        private readonly PartitionKey? partitionKey;

        private CosmosElement continuationToken;

        private bool started;

        public TryCatch<QueryPage> Current { get; private set; }

        private DistributedQueryPipelineStage(
            ICosmosDistributedQueryClient cosmosDistributedQueryClient,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRangeInternal,
            PartitionKey? partitionKey,
            CosmosElement continuationToken)
        {
            this.cosmosDistributedQueryClient = cosmosDistributedQueryClient ?? throw new ArgumentNullException(nameof(cosmosDistributedQueryClient));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.feedRangeInternal = feedRangeInternal ?? FeedRangeEpk.FullRange;
            this.partitionKey = partitionKey;
            this.continuationToken = continuationToken;
        }

        public static IQueryPipelineStage Create(
            ICosmosDistributedQueryClient cosmosDistributedQueryClient,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRangeInternal,
            PartitionKey? partitionKey,
            CosmosElement continuationToken)
        {
            return new DistributedQueryPipelineStage(cosmosDistributedQueryClient, sqlQuerySpec, feedRangeInternal, partitionKey, continuationToken);
        }

        public async ValueTask<bool> MoveNextAsync(Tracing.ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (!this.started || this.continuationToken != null)
            {
                this.Current = await this.cosmosDistributedQueryClient.MonadicQueryAsync(
                    this.partitionKey,
                    this.feedRangeInternal,
                    this.sqlQuerySpec,
                    this.continuationToken,
                    QueryPaginationOptions.Default,
                    trace,
                    cancellationToken);

                this.continuationToken = this.Current.Result?.State?.Value;
                this.started = true;
                return true;
            }

            return false;
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}