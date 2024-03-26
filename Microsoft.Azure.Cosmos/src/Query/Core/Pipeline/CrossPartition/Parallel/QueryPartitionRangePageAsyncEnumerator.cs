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
        private readonly ContainerQueryProperties containerQueryProperties;
        private readonly Cosmos.PartitionKey? partitionKey;

        public QueryPartitionRangePageAsyncEnumerator(
            IQueryDataSource queryDataSource,
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            Cosmos.PartitionKey? partitionKey,
            QueryPaginationOptions queryPaginationOptions,
            ContainerQueryProperties containerQueryProperties,
            CancellationToken cancellationToken)
            : base(feedRangeState, cancellationToken)
        {
            this.queryDataSource = queryDataSource ?? throw new ArgumentNullException(nameof(queryDataSource));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.queryPaginationOptions = queryPaginationOptions;
            this.partitionKey = partitionKey;
            this.containerQueryProperties = containerQueryProperties;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<QueryPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            FeedRangeInternal feedRange = this.LimitFeedRangeToSinglePartition();
            return this.queryDataSource.MonadicQueryAsync(
              sqlQuerySpec: this.sqlQuerySpec,
              feedRangeState: new FeedRangeState<QueryState>(feedRange, this.FeedRangeState.State),
              queryPaginationOptions: this.queryPaginationOptions,
              trace: trace,
              cancellationToken);
        }

        /// <summary>
        /// Updates the FeedRange to limit the scope of this enumerator to single physical partition.
        /// Generally speaking, a subpartitioned container can experience split partition at any level of hierarchical partition key.
        /// This could cause a situation where more than one physical partition contains the data for a partial partition key.
        /// Currently, enumerator instantiation does not honor physical partition boundary and allocates entire epk range which could spans across multiple physical partitions to the enumerator.
        /// Since such an epk range does not exist at the container level, Service generates a GoneException.
        /// This method restrics the range of each container by shrinking the ends of the range so that they do not span across physical partition.
        /// </summary>
        private FeedRangeInternal LimitFeedRangeToSinglePartition()
        {
            // We sadly need to check the partition key, since a user can set a partition key in the request options with a different continuation token.
            // In the future the partition filtering and continuation information needs to be a tightly bounded contract (like cross feed range state).
            FeedRangeInternal feedRange = this.FeedRangeState.FeedRange;
            if (this.partitionKey.HasValue)
            {
                // ISSUE-HACK-adityasa-3/25/2024 - We should not update the original feed range inside this class.
                // Instead we should guarantee that when enumerator is instantiated it is limited to a single physical partition.
                // Ultimately we should remove enumerator's dependency on PartitionKey.
                if ((this.containerQueryProperties.PartitionKeyDefinition.Paths.Count > 1) &&
                    (this.partitionKey.Value.InternalKey.Components.Count != this.containerQueryProperties.PartitionKeyDefinition.Paths.Count) &&
                    (feedRange is FeedRangeEpk feedRangeEpk))
                {
                    if (this.containerQueryProperties.EffectiveRangesForPartitionKey == null ||
                        this.containerQueryProperties.EffectiveRangesForPartitionKey.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "EffectiveRangesForPartitionKey should be populated when PK is specified in request options.");
                    }

                    foreach (Documents.Routing.Range<String> epkForPartitionKey in
                        this.containerQueryProperties.EffectiveRangesForPartitionKey)
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
                else
                {
                    feedRange = new FeedRangePartitionKey(this.partitionKey.Value);
                }
            }

            return feedRange;
        }
    }
}