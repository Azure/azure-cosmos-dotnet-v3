//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal static class IRoutingMapProviderExtensions
    {
        private static string Max(string left, string right)
        {
            return StringComparer.Ordinal.Compare(left, right) < 0 ? right : left;
        }

        private static bool IsSortedAndNonOverlapping<T>(IList<Range<T>> list)
            where T : IComparable<T>
        {
            IComparer<T> comparer = typeof(T) == typeof(string) ? (IComparer<T>)StringComparer.Ordinal : Comparer<T>.Default;

            for (int i = 1; i < list.Count; i++)
            {
                Range<T> previousRange = list[i - 1];
                Range<T> currentRange = list[i];

                int compareResult = comparer.Compare(previousRange.Max, currentRange.Min);
                if (compareResult > 0)
                {
                    return false;
                }
                else if (compareResult == 0 && previousRange.IsMaxInclusive && currentRange.IsMinInclusive)
                {
                    return false;
                }
            }

            return true;
        }

        public static async Task<PartitionKeyRange> TryGetRangeByEffectivePartitionKeyAsync(
            this IRoutingMapProvider routingMapProvider,
            string collectionResourceId,
            string effectivePartitionKey)
        {
            IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                collectionResourceId,
                Range<string>.GetPointRange(effectivePartitionKey));

            if (ranges == null)
            {
                return null;
            }

            return ranges.Single();
        }

        public static async Task<List<PartitionKeyRange>> TryGetOverlappingRangesAsync(
            this IRoutingMapProvider routingMapProvider,
            string collectionResourceId,
            IList<Range<string>> sortedRanges,
            bool forceRefresh = false)
        {
            if (!IsSortedAndNonOverlapping(sortedRanges))
            {
                throw new ArgumentException("sortedRanges");
            }

            List<PartitionKeyRange> targetRanges = new List<PartitionKeyRange>();
            int currentProvidedRange = 0;
            while (currentProvidedRange < sortedRanges.Count)
            {
                if (sortedRanges[currentProvidedRange].IsEmpty)
                {
                    currentProvidedRange++;
                    continue;
                }

                Range<string> queryRange;
                if (targetRanges.Count > 0)
                {
                    string left = Max(
                        targetRanges[targetRanges.Count - 1].MaxExclusive,
                        sortedRanges[currentProvidedRange].Min);

                    bool leftInclusive = string.CompareOrdinal(left, sortedRanges[currentProvidedRange].Min) == 0
                                             ? sortedRanges[currentProvidedRange].IsMinInclusive
                                             : false;

                    queryRange = new Range<string>(
                        left,
                        sortedRanges[currentProvidedRange].Max,
                        leftInclusive,
                        sortedRanges[currentProvidedRange].IsMaxInclusive);
                }
                else
                {
                    queryRange = sortedRanges[currentProvidedRange];
                }

                IReadOnlyList<PartitionKeyRange> overlappingRanges =
                    await routingMapProvider.TryGetOverlappingRangesAsync(collectionResourceId, queryRange, forceRefresh);

                if (overlappingRanges == null)
                {
                    return null;
                }

                targetRanges.AddRange(overlappingRanges);

                Range<string> lastKnownTargetRange = targetRanges[targetRanges.Count - 1].ToRange();
                while (currentProvidedRange < sortedRanges.Count &&
                    Range<string>.MaxComparer.Instance.Compare(sortedRanges[currentProvidedRange], lastKnownTargetRange) <= 0)
                {
                    currentProvidedRange++;
                }
            }

            return targetRanges;
        }
    }
}
