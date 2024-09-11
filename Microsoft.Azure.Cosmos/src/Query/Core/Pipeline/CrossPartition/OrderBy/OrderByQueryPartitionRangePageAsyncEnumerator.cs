// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class OrderByQueryPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>, IPrefetcher
    {
        private readonly InnerEnumerator innerEnumerator;
        private readonly BufferedPartitionRangePageAsyncEnumeratorBase<OrderByQueryPage, QueryState> bufferedEnumerator;

        public static OrderByQueryPartitionRangePageAsyncEnumerator Create(
            IQueryDataSource queryDataSource,
            ContainerQueryProperties containerQueryProperties,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            PartitionKey? partitionKey,
            QueryExecutionOptions queryPaginationOptions,
            string filter,
            PrefetchPolicy prefetchPolicy)
        {
            InnerEnumerator enumerator = new InnerEnumerator(
                queryDataSource,
                containerQueryProperties,
                sqlQuerySpec,
                feedRangeState,
                partitionKey,
                queryPaginationOptions,
                filter);

            BufferedPartitionRangePageAsyncEnumeratorBase<OrderByQueryPage, QueryState> bufferedEnumerator = prefetchPolicy switch
            {
                PrefetchPolicy.PrefetchSinglePage => new BufferedPartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>(enumerator),
                PrefetchPolicy.PrefetchAll => new FullyBufferedPartitionRangeAsyncEnumerator<OrderByQueryPage, QueryState>(enumerator),
                _ => throw new ArgumentOutOfRangeException(nameof(prefetchPolicy)),
            };

            return new OrderByQueryPartitionRangePageAsyncEnumerator(enumerator, bufferedEnumerator, feedRangeState);
        }

        private OrderByQueryPartitionRangePageAsyncEnumerator(
            InnerEnumerator innerEnumerator,
            BufferedPartitionRangePageAsyncEnumeratorBase<OrderByQueryPage, QueryState> bufferedEnumerator,
            FeedRangeState<QueryState> feedRangeState)
            : base(feedRangeState)
        {
            this.innerEnumerator = innerEnumerator ?? throw new ArgumentNullException(nameof(innerEnumerator));
            this.bufferedEnumerator = bufferedEnumerator ?? throw new ArgumentNullException(nameof(bufferedEnumerator));
            this.StartOfPageState = feedRangeState.State;
        }

        public SqlQuerySpec SqlQuerySpec => this.innerEnumerator.SqlQuerySpec;

        public QueryExecutionOptions QueryPaginationOptions => this.innerEnumerator.QueryPaginationOptions;

        public string Filter => this.innerEnumerator.Filter;

        public QueryState StartOfPageState { get; private set; }

        public int BufferedResultCount => this.bufferedEnumerator.BufferedItemCount;

        public override ValueTask DisposeAsync()
        {
            // the innerEnumerator is passed to the bufferedEnumerator
            return this.bufferedEnumerator.DisposeAsync();
        }

        protected override async Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            this.StartOfPageState = this.FeedRangeState.State;
            await this.bufferedEnumerator.MoveNextAsync(trace, cancellationToken);
            return this.bufferedEnumerator.Current;
        }

        public ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
        {
            return this.bufferedEnumerator.PrefetchAsync(trace, cancellationToken);
        }

        public OrderByQueryPartitionRangePageAsyncEnumerator CloneAsFullyBufferedEnumerator()
        {
            if (this.Current.Failed)
            {
                throw new InvalidOperationException($"{nameof(CloneAsFullyBufferedEnumerator)} is valid only if the enumerator has not failed");
            }

            InnerEnumerator innerEnumerator = this.innerEnumerator.CloneWithMaxPageSize();

            FullyBufferedPartitionRangeAsyncEnumerator<OrderByQueryPage, QueryState> bufferedEnumerator = new FullyBufferedPartitionRangeAsyncEnumerator<OrderByQueryPage, QueryState>(
                innerEnumerator,
                new List<OrderByQueryPage> { this.Current.Result });

            return new OrderByQueryPartitionRangePageAsyncEnumerator(
                innerEnumerator,
                bufferedEnumerator,
                this.FeedRangeState);
        }

        private sealed class InnerEnumerator : PartitionRangePageAsyncEnumerator<OrderByQueryPage, QueryState>
        {
            private readonly IQueryDataSource queryDataSource;
            private readonly ContainerQueryProperties containerQueryProperties;

            public InnerEnumerator(
                IQueryDataSource queryDataSource,
                ContainerQueryProperties containerQueryProperties,
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                PartitionKey? partitionKey,
                QueryExecutionOptions queryPaginationOptions,
                string filter)
                : base(feedRangeState)
            {
                this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
                this.containerQueryProperties = containerQueryProperties;
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
                this.PartitionKey = partitionKey;
                this.QueryPaginationOptions = queryPaginationOptions ?? QueryExecutionOptions.Default;
                this.Filter = filter;
            }

            public SqlQuerySpec SqlQuerySpec { get; }

            public PartitionKey? PartitionKey { get; }

            public QueryExecutionOptions QueryPaginationOptions { get; }

            public string Filter { get; }

            public InnerEnumerator CloneWithMaxPageSize()
            {
                QueryExecutionOptions options = new QueryExecutionOptions(
                    pageSizeHint: int.MaxValue,
                    optimisticDirectExecute: this.QueryPaginationOptions.OptimisticDirectExecute,
                    additionalHeaders: this.QueryPaginationOptions.AdditionalHeaders);

                return new InnerEnumerator(
                    this.queryDataSource,
                    this.containerQueryProperties,
                    this.SqlQuerySpec,
                    this.FeedRangeState,
                    this.PartitionKey,
                    options,
                    this.Filter);
            }

            public override ValueTask DisposeAsync() => default;

            protected override async Task<TryCatch<OrderByQueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
            {
                FeedRangeInternal feedRange = HierarchicalPartitionUtils.LimitFeedRangeToSinglePartition(this.PartitionKey, this.FeedRangeState.FeedRange, this.containerQueryProperties);

                TryCatch<QueryPage> monadicQueryPage = await this.queryDataSource
                    .MonadicQueryAsync(
                        sqlQuerySpec: this.SqlQuerySpec,
                        feedRangeState: new FeedRangeState<QueryState>(feedRange, this.FeedRangeState.State),
                        queryPaginationOptions: this.QueryPaginationOptions,
                        trace: trace,
                        cancellationToken);
                if (monadicQueryPage.Failed)
                {
                    return TryCatch<OrderByQueryPage>.FromException(monadicQueryPage.Exception);
                }
                QueryPage queryPage = monadicQueryPage.Result;
                return TryCatch<OrderByQueryPage>.FromResult(new OrderByQueryPage(queryPage));
            }
        }
    }
}
