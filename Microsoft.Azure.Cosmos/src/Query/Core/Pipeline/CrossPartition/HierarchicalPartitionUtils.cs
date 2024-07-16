// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal static class HierarchicalPartitionUtils
    {
        /// <summary>
        /// Updates the FeedRange to limit the scope of incoming feedRange to logical partition within a single physical partition.
        /// Generally speaking, a subpartitioned container can experience split partition at any level of hierarchical partition key.
        /// This could cause a situation where more than one physical partition contains the data for a partial partition key.
        /// Currently, enumerator instantiation does not honor physical partition boundary and allocates entire epk range which could spans across multiple physical partitions to the enumerator.
        /// Since such an epk range does not exist at the container level, Service generates a GoneException.
        /// This method restrics the range of each enumerator by intersecting it with physical partition range.
        /// </summary>
        public static FeedRangeInternal LimitFeedRangeToSinglePartition(PartitionKey? partitionKey, FeedRangeInternal feedRange, ContainerQueryProperties containerQueryProperties)
        {
            // We sadly need to check the partition key, since a user can set a partition key in the request options with a different continuation token.
            // In the future the partition filtering and continuation information needs to be a tightly bounded contract (like cross feed range state).
            if (partitionKey.HasValue)
            {
                // ISSUE-HACK-adityasa-3/25/2024 - We should not update the original feed range inside this class.
                // Instead we should guarantee that when enumerator is instantiated it is limited to a single physical partition.
                // Ultimately we should remove enumerator's dependency on PartitionKey.
                if ((containerQueryProperties.PartitionKeyDefinition.Paths.Count > 1) &&
                    (partitionKey.Value.InternalKey.Components.Count != containerQueryProperties.PartitionKeyDefinition.Paths.Count) &&
                    (feedRange is FeedRangeEpk feedRangeEpk))
                {
                    if (containerQueryProperties.EffectiveRangesForPartitionKey == null ||
                        containerQueryProperties.EffectiveRangesForPartitionKey.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "EffectiveRangesForPartitionKey should be populated when PK is specified in request options.");
                    }

                    foreach (Documents.Routing.Range<String> epkForPartitionKey in containerQueryProperties.EffectiveRangesForPartitionKey)
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
                    feedRange = new FeedRangePartitionKey(partitionKey.Value);
                }
            }

            return feedRange;
        }
    }
}
