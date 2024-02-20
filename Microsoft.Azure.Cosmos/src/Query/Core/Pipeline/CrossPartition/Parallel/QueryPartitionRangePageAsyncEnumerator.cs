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
        private readonly QueryPaginationOptions queryPaginationOptions;
        private readonly Cosmos.PartitionKey? partitionKey;
        private readonly ContainerQueryProperties containerQueryOptions;

        public QueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            Cosmos.PartitionKey? partitionKey,
            QueryPaginationOptions queryPaginationOptions,
            ContainerQueryProperties containerQueryOptions,
            CancellationToken cancellationToken)
            : base(feedRangeState, cancellationToken)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.queryPaginationOptions = queryPaginationOptions;
            this.partitionKey = partitionKey;
            this.containerQueryOptions = containerQueryOptions;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<QueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            // We sadly need to check the partition key, since a user can set a partition key in the request options with a different continuation token.
            // In the future the partition filtering and continuation information needs to be a tightly bounded contract (like cross feed range state).
            FeedRangeInternal feedRange = this.FeedRangeState.FeedRange;

            if (feedRange is FeedRangeEpk feedRangeEpk && this.partitionKey.HasValue)
            {
                if (this.containerQueryOptions.EffectiveRangesForPartitionKey == null ||
                    this.containerQueryOptions.EffectiveRangesForPartitionKey.Count == 0)
                {
                    throw new InvalidOperationException(
                        "EffectiveRangesForPartitionKey should be populated " +
                        "when PK is specified in request options.");
                }

                foreach (Documents.Routing.Range<String> epkForPartitionKey in
                    this.containerQueryOptions.EffectiveRangesForPartitionKey)
                {
                    if (Documents.Routing.Range<String>.CheckOverlapping(
                            feedRangeEpk.Range,
                            epkForPartitionKey))
                    {
                        if (!feedRangeEpk.Range.Equals(epkForPartitionKey))
                        {
                            String overlappingMin;
                            bool minInclusive;
                            String overlappingMax;
                            bool maxInclusive;

                            if (Documents.Routing.Range<String>.MinComparer.Instance.Compare(
                                    epkForPartitionKey,
                                    feedRangeEpk.Range) < 0)
                            {
                                overlappingMin = feedRangeEpk.Range.Min;
                                minInclusive = feedRangeEpk.Range.IsMinInclusive;
                            }
                            else
                            {
                                overlappingMin = epkForPartitionKey.Min;
                                minInclusive = epkForPartitionKey.IsMinInclusive;
                            }

                            if (Documents.Routing.Range<String>.MaxComparer.Instance.Compare(
                                    epkForPartitionKey,
                                    feedRangeEpk.Range) > 0)
                            {
                                overlappingMax = feedRangeEpk.Range.Max;
                                maxInclusive = feedRangeEpk.Range.IsMaxInclusive;
                            }
                            else
                            {
                                overlappingMax = epkForPartitionKey.Max;
                                maxInclusive = epkForPartitionKey.IsMaxInclusive;
                            }

                            feedRange = new FeedRangeEpk(
                                new Documents.Routing.Range<String>(
                                    overlappingMin,
                                    overlappingMax,
                                    minInclusive,
                                    maxInclusive));
                        }

                        break;
                    }
                }
            }

            return this.queryDataSource.MonadicQueryAsync(
              sqlQuerySpec: this.sqlQuerySpec,
              feedRangeState: new FeedRangeState<QueryState>(feedRange, this.FeedRangeState.State),
              queryPaginationOptions: this.queryPaginationOptions,
              trace: trace,
              cancellationToken);
        }
    }
}