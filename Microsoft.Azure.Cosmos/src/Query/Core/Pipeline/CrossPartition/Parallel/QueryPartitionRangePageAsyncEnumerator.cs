// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal sealed class QueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<QueryPage, QueryState>
    {
        private readonly IQueryDataSource queryDataSource;
        private readonly SqlQuerySpec sqlQuerySpec;
        private readonly int pageSize;
        private readonly Cosmos.PartitionKey? partitionKey;

        public QueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRange,
            Cosmos.PartitionKey? partitionKey,
            int pageSize,
            CancellationToken cancellationToken,
            QueryState state = default)
            : base(feedRange, cancellationToken, state)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.pageSize = pageSize;
            this.partitionKey = partitionKey;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Unfortunately we need to keep both the epk range and partition key for queries
            // Since the continuation token format uses epk range even though we only need the partition key to route the request.
            FeedRangeInternal feedRange = this.partitionKey.HasValue ? new FeedRangePartitionKey(this.partitionKey.Value) : this.Range;
            return this.queryDataSource.MonadicQueryAsync(
              sqlQuerySpec: this.sqlQuerySpec,
              continuationToken: this.State == null ? null : ((CosmosString)this.State.Value).Value,
              feedRange: feedRange,
              pageSize: this.pageSize,
              cancellationToken);
        }
    }
}