// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class QueryPartitionRangePageEnumerator : PartitionRangePageEnumerator
    {
        private readonly IQueryDataSource queryDataSource;
        private readonly SqlQuerySpec sqlQuerySpec;
        private readonly int pageSize;

        public QueryPartitionRangePageEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRange feedRange,
            int pageSize,
            State state = default)
            : base(feedRange, state)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.pageSize = pageSize;

            if (state != default)
            {
                if (!(state is QueryPage.ContinuationTokenState))
                {
                    throw new ArgumentOutOfRangeException(nameof(state));
                }
            }

            if (!(feedRange is FeedRangePartitionKeyRange))
            {
                throw new ArgumentOutOfRangeException(nameof(feedRange));
            }
        }

        public override async Task<TryCatch<Page>> GetNextPageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<QueryPage> queryResponse = await this.queryDataSource.ExecuteQueryAsync(
                sqlQuerySpec: this.sqlQuerySpec,
                continuationToken: ((QueryPage.ContinuationTokenState)this.State).ContinuationToken,
                partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)this.Range).PartitionKeyRangeId),
                pageSize: this.pageSize,
                cancellationToken);
            if (queryResponse.Failed)
            {
                return TryCatch<Page>.FromException(queryResponse.Exception);
            }

            return TryCatch<Page>.FromResult(queryResponse.Result);
        }

        public override ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
