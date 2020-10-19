// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class OrderByQueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>, IPrefetcher
    {
        private readonly InnerEnumerator innerEnumerator;
        private readonly BufferedPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState> bufferedEnumerator;

        public OrderByQueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeInternal feedRange,
            PartitionKey? partitionKey,
            int pageSize,
            string filter,
            CancellationToken cancellationToken,
            QueryState state = default)
            : base(feedRange, cancellationToken, state)
        {
            this.StartOfPageState = state;
            this.innerEnumerator = new InnerEnumerator(
                queryDataSource,
                sqlQuerySpec,
                feedRange,
                partitionKey,
                pageSize,
                filter,
                cancellationToken,
                state);
            this.bufferedEnumerator = new BufferedPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>(
                this.innerEnumerator,
                cancellationToken);
        }

        public SqlQuerySpec SqlQuerySpec => this.innerEnumerator.SqlQuerySpec;

        public int PageSize => this.innerEnumerator.PageSize;

        public string Filter => this.innerEnumerator.Filter;

        public QueryState StartOfPageState { get; private set; }

        public override ValueTask DisposeAsync() => default;

        protected override async Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
        {
            this.StartOfPageState = this.State;
            await this.bufferedEnumerator.MoveNextAsync();
            return this.bufferedEnumerator.Current;
        }

        public ValueTask PrefetchAsync(CancellationToken cancellationToken) => this.bufferedEnumerator.PrefetchAsync(cancellationToken);

        private sealed class InnerEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>
        {
            private readonly IQueryDataSource queryDataSource;

            public InnerEnumerator(
                IQueryDataSource queryDataSource,
                SqlQuerySpec sqlQuerySpec,
                FeedRangeInternal feedRange,
                PartitionKey? partitionKey,
                int pageSize,
                string filter,
                CancellationToken cancellationToken,
                QueryState state = default)
                : base(feedRange, cancellationToken, state)
            {
                this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
                this.PartitionKey = partitionKey;
                this.PageSize = pageSize;
                this.Filter = filter;
            }

            public SqlQuerySpec SqlQuerySpec { get; }

            public PartitionKey? PartitionKey { get; }

            public int PageSize { get; }

            public string Filter { get; }

            public override ValueTask DisposeAsync() => default;

            protected override Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                // Unfortunately we need to keep both the epk range and partition key for queries
                // Since the continuation token format uses epk range even though we only need the partition key to route the request.
                FeedRangeInternal feedRange = this.PartitionKey.HasValue ? new FeedRangePartitionKey(this.PartitionKey.Value) : this.Range;

                return this.queryDataSource
                    .MonadicQueryAsync(
                        sqlQuerySpec: this.SqlQuerySpec,
                        continuationToken: this.State == null ? null : ((CosmosString)this.State.Value).Value,
                        feedRange: feedRange,
                        pageSize: this.PageSize,
                        cancellationToken)
                    .ContinueWith<TryCatch<OrderByQueryPage>>(antecedent =>
                    {
                        TryCatch<QueryPage> monadicQueryPage = antecedent.Result;
                        if (monadicQueryPage.Failed)
                        {
                            return TryCatch<OrderByQueryPage>.FromException(monadicQueryPage.Exception);
                        }

                        QueryPage queryPage = monadicQueryPage.Result;
                        return TryCatch<OrderByQueryPage>.FromResult(new OrderByQueryPage(queryPage));
                    });
            }
        }
    }
}
