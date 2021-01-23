// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class OrderByQueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>, IPrefetcher
    {
        private readonly InnerEnumerator innerEnumerator;
        private readonly BufferedPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState> bufferedEnumerator;

        public OrderByQueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            PartitionKey? partitionKey,
            QueryPaginationOptions queryPagingationOptions,
            string filter,
            CancellationToken cancellationToken)
            : base(feedRangeState, cancellationToken)
        {
            this.StartOfPageState = feedRangeState.State;
            this.innerEnumerator = new InnerEnumerator(
                queryDataSource,
                sqlQuerySpec,
                feedRangeState,
                partitionKey,
                queryPagingationOptions,
                filter,
                cancellationToken);
            this.bufferedEnumerator = new BufferedPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>(
                this.innerEnumerator,
                cancellationToken);
        }

        public SqlQuerySpec SqlQuerySpec => this.innerEnumerator.SqlQuerySpec;

        public QueryPaginationOptions QueryPaginationOptions => this.innerEnumerator.QueryPaginationOptions;

        public string Filter => this.innerEnumerator.Filter;

        public QueryState StartOfPageState { get; private set; }

        public override ValueTask DisposeAsync() => default;

        protected override async Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            this.StartOfPageState = this.FeedRangeState.State;
            await this.bufferedEnumerator.MoveNextAsync(trace);
            return this.bufferedEnumerator.Current;
        }

        public ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken) => this.bufferedEnumerator.PrefetchAsync(trace, cancellationToken);

        private sealed class InnerEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>
        {
            private readonly IQueryDataSource queryDataSource;
            private readonly QueryPaginationOptions queryPaginationOptions;

            public InnerEnumerator(
                IQueryDataSource queryDataSource,
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                PartitionKey? partitionKey,
                QueryPaginationOptions queryPaginationOptions,
                string filter,
                CancellationToken cancellationToken)
                : base(feedRangeState, cancellationToken)
            {
                this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
                this.PartitionKey = partitionKey;
                this.queryPaginationOptions = queryPaginationOptions ?? QueryPaginationOptions.Default;
                this.Filter = filter;
            }

            public SqlQuerySpec SqlQuerySpec { get; }

            public PartitionKey? PartitionKey { get; }

            public QueryPaginationOptions QueryPaginationOptions { get; }

            public string Filter { get; }

            public override ValueTask DisposeAsync() => default;

            protected override Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
            {
                // Unfortunately we need to keep both the epk range and partition key for queries
                // Since the continuation token format uses epk range even though we only need the partition key to route the request.
                FeedRangeInternal feedRange = this.PartitionKey.HasValue ? new FeedRangePartitionKey(this.PartitionKey.Value) : this.FeedRangeState.FeedRange;

                return this.queryDataSource
                    .MonadicQueryAsync(
                        sqlQuerySpec: this.SqlQuerySpec,
                        feedRangeState: this.FeedRangeState,
                        queryPaginationOptions: this.queryPaginationOptions,
                        trace: trace,
                        cancellationToken)
                    .ContinueWith<TryCatch<OrderByQueryPage>>(antecedent =>
                    {
                        TryCatch<QueryPage> monadicQueryPage = antecedent.Result;
                        if (monadicQueryPage.Failed)
                        {
                            Console.WriteLine(this.SqlQuerySpec);
                            return TryCatch<OrderByQueryPage>.FromException(monadicQueryPage.Exception);
                        }

                        QueryPage queryPage = monadicQueryPage.Result;
                        return TryCatch<OrderByQueryPage>.FromResult(new OrderByQueryPage(queryPage));
                    });
            }
        }
    }
}
