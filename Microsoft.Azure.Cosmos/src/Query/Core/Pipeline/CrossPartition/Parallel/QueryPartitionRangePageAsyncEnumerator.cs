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
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class QueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<QueryPage, QueryState>
    {
        private readonly IQueryDataSource queryDataSource;
        private readonly SqlQuerySpec sqlQuerySpec;
        private readonly QueryPaginationOptions queryPaginationOptions;
        private readonly Cosmos.PartitionKey? partitionKey;

        public QueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            Cosmos.PartitionKey? partitionKey,
            QueryPaginationOptions queryPaginationOptions,
            CancellationToken cancellationToken)
            : base(feedRangeState, cancellationToken)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.queryPaginationOptions = queryPaginationOptions;
            this.partitionKey = partitionKey;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<QueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            FeedRangeInternal feedRange = this.partitionKey.HasValue ? new FeedRangePartitionKey(this.partitionKey.Value) : this.FeedRangeState.FeedRange;
            return this.queryDataSource.MonadicQueryAsync(
              sqlQuerySpec: this.sqlQuerySpec,
              feedRangeState: new FeedRangeState<QueryState>(feedRange, this.FeedRangeState.State),
              queryPaginationOptions: this.queryPaginationOptions,
              trace: trace,
              cancellationToken);
        }
    }
}