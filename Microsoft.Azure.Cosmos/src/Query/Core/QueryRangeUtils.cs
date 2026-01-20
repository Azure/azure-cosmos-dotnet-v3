// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents.Routing;

    internal static class QueryRangeUtils
    {
        private static readonly bool IsLengthAwareComparisonEnabled = ConfigurationManager.IsLengthAwareRangeComparatorEnabled();

        /// <summary>
        /// Updates the FeedRange to limit the scope of incoming feedRange to logical partition within a single physical partition.
        /// Generally speaking, a subpartitioned container can experience split partition at any level of hierarchical partition key.
        /// This could cause a situation where more than one physical partition contains the data for a partial partition key.
        /// Currently, enumerator instantiation does not honor physical partition boundary and allocates entire epk range which could spans across multiple physical partitions to the enumerator.
        /// Since such an epk range does not exist at the container level, Service generates a GoneException.
        /// This method restrics the range of each enumerator by intersecting it with physical partition range.
        /// </summary>
        public static FeedRangeInternal LimitHpkFeedRangeToPartition(PartitionKey? partitionKey, FeedRangeInternal feedRange, ContainerQueryProperties containerQueryProperties)
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

                                (IComparer<Range<string>> minComparer, IComparer<Range<string>> maxComparer) = RangeComparerProvider.GetComparers(containerQueryProperties.UseLengthAwareRangeComparer);

                                if (minComparer.Compare(
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

                                if (maxComparer.Compare(
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

        /// <summary>
        /// Limits the partition key ranges to fit within the provided EPK ranges.
        /// Computes the overall min and max from the provided ranges, then trims each partition key range to fit within those boundaries.
        /// </summary>
        /// <param name="partitionKeyRanges">The list of partition key ranges to trim</param>
        /// <param name="providedRanges">The EPK ranges to use as boundaries</param>
        /// <param name="useLengthAwareComparer">Whether to use length-aware range comparers</param>
        /// <returns>A list of trimmed partition key ranges that fit within the provided ranges</returns>
        public static List<Documents.PartitionKeyRange> LimitPartitionKeyRangesToProvidedRanges(
            List<Documents.PartitionKeyRange> partitionKeyRanges,
            IReadOnlyList<Documents.Routing.Range<string>> providedRanges,
            bool useLengthAwareComparer = true)
        {
            (IComparer<Range<string>> minComparer, IComparer<Range<string>> maxComparer) = RangeComparerProvider.GetComparers(useLengthAwareComparer);

            // Compute the overall min and max from providedRanges
            string overallMin = providedRanges[0].Min;
            string overallMax = providedRanges[0].Max;

            foreach (Range<string> providedRange in providedRanges)
            {
                // ProvidedRanges are user input, which can be generally deserialized from a json representation of FeedRangeInternal.
                // FeedRangeInternal allows min/max to be included or excluded.
                // However PartitionKeyRange assumes min is inclusive and max is exclusive.
                // This is also similar to backend behavior where EPK ranges are always min-inclusive and max-exclusive.
                // Therefore, despite the possible customization at FeedRangeInternal level, we only support min-inclusive and max-exclusive ranges.
                // Ideally this validation should be done at the public API. Since that is not present, we only assert below.
                Debug.Assert(providedRange.IsMinInclusive, "QueryRangeUtils Assert!", "Only min-inclusive ranges are supported!");
                Debug.Assert(!providedRange.IsMaxInclusive, "QueryRangeUtils Assert!", "Only max-exclusive ranges are supported!");

                if (minComparer.Compare(providedRange, CreateSingleValueRange(overallMin)) < 0)
                {
                    overallMin = providedRange.Min;
                }

                if (maxComparer.Compare(providedRange, CreateSingleValueRange(overallMax)) > 0)
                {
                    overallMax = providedRange.Max;
                }
            }

            // Trim each range to fit within the overall boundaries
            List<Documents.PartitionKeyRange> trimmedRanges = new List<Documents.PartitionKeyRange>(partitionKeyRanges.Count);
            foreach (Documents.PartitionKeyRange range in partitionKeyRanges)
            {
                string trimmedMin = range.MinInclusive;
                string trimmedMax = range.MaxExclusive;

                // Trim min: use the greater of range.Min and overallMin
                if (minComparer.Compare(CreateSingleValueRange(range.MinInclusive), CreateSingleValueRange(overallMin)) < 0)
                {
                    trimmedMin = overallMin;
                }

                // Trim max: use the lesser of range.Max and overallMax
                if (maxComparer.Compare(CreateSingleValueRange(range.MaxExclusive), CreateSingleValueRange(overallMax)) > 0)
                {
                    trimmedMax = overallMax;
                }

                trimmedRanges.Add(
                    new Documents.PartitionKeyRange
                    {
                        Id = range.Id,
                        MinInclusive = trimmedMin,
                        MaxExclusive = trimmedMax,
                        Parents = range.Parents
                    });
            }

            return trimmedRanges;
        }

        private static Range<string> CreateSingleValueRange(string singleValue) => new Range<string>(
            singleValue,
            singleValue,
            isMinInclusive: true,
            isMaxInclusive: true);
    }
}
