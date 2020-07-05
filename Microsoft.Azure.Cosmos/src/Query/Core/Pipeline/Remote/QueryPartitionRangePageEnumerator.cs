// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class QueryPartitionRangePageEnumerator : PartitionRangePageEnumerator<QueryPage, QueryState>
    {
        private readonly IQueryDataSource queryDataSource;
        private readonly SqlQuerySpec sqlQuerySpec;
        private readonly int pageSize;

        public QueryPartitionRangePageEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRange feedRange,
            int pageSize,
            QueryState state = default)
            : base(feedRange, state)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.pageSize = pageSize;

            if (!(feedRange is FeedRangePartitionKeyRange))
            {
                throw new ArgumentOutOfRangeException(nameof(feedRange));
            }
        }

        public override Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken) => this.queryDataSource.ExecuteQueryAsync(
            sqlQuerySpec: this.sqlQuerySpec,
            continuationToken: this.State == null ? null : ((CosmosString)this.State.Value).Value,
            partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)this.Range).PartitionKeyRangeId),
            pageSize: this.pageSize,
            cancellationToken);

        public override ValueTask DisposeAsync() => default;
    }
}