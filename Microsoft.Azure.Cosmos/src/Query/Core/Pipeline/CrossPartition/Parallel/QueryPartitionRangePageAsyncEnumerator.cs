// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class QueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<QueryPage, QueryState>
    {
        private readonly IQueryDataSource queryDataSource;
        private readonly SqlQuerySpec sqlQuerySpec;
        private readonly QueryExecutionOptions queryPaginationOptions;
        private readonly ContainerQueryProperties containerQueryProperties;
        private readonly Cosmos.PartitionKey? partitionKey;

        public QueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            Cosmos.PartitionKey? partitionKey,
            QueryExecutionOptions queryPaginationOptions,
            ContainerQueryProperties containerQueryProperties)
            : base(feedRangeState)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.queryPaginationOptions = queryPaginationOptions;
            this.partitionKey = partitionKey;
            this.containerQueryProperties = containerQueryProperties;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<QueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            FeedRangeInternal feedRange = HierarchicalPartitionUtils.LimitFeedRangeToSinglePartition(this.partitionKey, this.FeedRangeState.FeedRange, this.containerQueryProperties);
            return this.queryDataSource.MonadicQueryAsync(
              sqlQuerySpec: this.sqlQuerySpec,
              feedRangeState: new FeedRangeState<QueryState>(feedRange, this.FeedRangeState.State),
              queryPaginationOptions: this.queryPaginationOptions,
              trace: trace,
              cancellationToken);
        }
    }
}