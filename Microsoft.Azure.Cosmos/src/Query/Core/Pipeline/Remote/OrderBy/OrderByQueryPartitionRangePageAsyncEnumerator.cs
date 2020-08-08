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

    internal sealed class OrderByQueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>
    {
        private readonly IQueryDataSource queryDataSource;
        private readonly SqlQuerySpec sqlQuerySpec;
        private readonly int pageSize;

        public OrderByQueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyRange feedRange,
            int pageSize,
            string filter,
            QueryState state = default)
            : base(feedRange, state)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.pageSize = pageSize;
            this.Filter = filter;
            this.StartOfPageState = state;
        }

        public string Filter { get; }

        public QueryState StartOfPageState { get; private set; }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
        {
            this.StartOfPageState = this.State;
            return this.queryDataSource
                .MonadicQueryAsync(
                    sqlQuerySpec: this.sqlQuerySpec,
                    continuationToken: this.State == null ? null : ((CosmosString)this.State.Value).Value,
                    feedRange: new FeedRangeEpk(this.Range.ToRange()),
                    pageSize: this.pageSize,
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
