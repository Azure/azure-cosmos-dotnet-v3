// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.OrderBy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal sealed class OrderByQueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>, IBufferable
    {
        private readonly InnerEnumerator innerEnumerator;
        private readonly BufferedPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState> bufferedEnumerator;

        public OrderByQueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyRange feedRange,
            int pageSize,
            string filter,
            QueryState state = default)
            : base(feedRange, state)
        {
            this.StartOfPageState = state;
            this.innerEnumerator = new InnerEnumerator(
                queryDataSource,
                sqlQuerySpec,
                feedRange,
                pageSize,
                filter,
                state);
            this.bufferedEnumerator = new BufferedPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>(
                this.innerEnumerator);
        }

        public SqlQuerySpec SqlQuerySpec => this.innerEnumerator.SqlQuerySpec;

        public int PageSize => this.innerEnumerator.PageSize;

        public string Filter => this.innerEnumerator.Filter;

        public QueryState StartOfPageState { get; set; }

        public override ValueTask DisposeAsync() => default;

        protected override async Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
        {
            this.StartOfPageState = this.State;
            await this.bufferedEnumerator.MoveNextAsync();
            return this.bufferedEnumerator.Current;
        }

        public ValueTask BufferAsync() => this.bufferedEnumerator.BufferAsync();

        private sealed class InnerEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>
        {
            private readonly IQueryDataSource queryDataSource;

            public InnerEnumerator(
                IQueryDataSource queryDataSource,
                SqlQuerySpec sqlQuerySpec,
                PartitionKeyRange feedRange,
                int pageSize,
                string filter,
                QueryState state = default)
                : base(feedRange, state)
            {
                this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
                this.PageSize = pageSize;
                this.Filter = filter;
            }

            public SqlQuerySpec SqlQuerySpec { get; }

            public int PageSize { get; }

            public string Filter { get; }

            public override ValueTask DisposeAsync() => default;

            protected override Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
            {
                return this.queryDataSource
                    .MonadicQueryAsync(
                        sqlQuerySpec: this.SqlQuerySpec,
                        continuationToken: this.State == null ? null : ((CosmosString)this.State.Value).Value,
                        feedRange: new FeedRangePartitionKeyRange(this.Range.Id),
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
