//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal static class IRoutingMapProviderExtensions
    {
        private static string Max(string left, string right)
        {
            return StringComparer.Ordinal.Compare(left, right) < 0 ? right : left;
        }

        public static async Task<TryCatch<PartitionKeyRange>> TryGetRangeByEffectivePartitionKeyAsync(
            this IRoutingMapProvider routingMapProvider,
            string collectionResourceId,
            string effectivePartitionKey)
        {
            TryCatch<IReadOnlyList<PartitionKeyRange>> tryGetOverlappingRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                collectionResourceId,
                Range<string>.GetPointRange(effectivePartitionKey));

            if (tryGetOverlappingRanges.Failed)
            {
                return TryCatch<PartitionKeyRange>.FromException(tryGetOverlappingRanges.Exception);
            }

            return TryCatch<PartitionKeyRange>.FromResult(tryGetOverlappingRanges.Result.Single());
        }

        public static async Task<PartitionKeyRange> GetRangeByEffectivePartitionKeyAsync(
            this IRoutingMapProvider routingMapProvider,
            string collectionResourceId,
            string effectivePartitionKey)
        {
            TryCatch<PartitionKeyRange> tryGetPartitionKeyRange = await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(
                collectionResourceId,
                effectivePartitionKey);
            tryGetPartitionKeyRange.ThrowIfFailed();

            return tryGetPartitionKeyRange.Result;
        }

        private static bool IsNonOverlapping<T>(SortedSet<Range<T>> sortedSet)
            where T : IComparable<T>
        {
            IComparer<T> comparer = typeof(T) == typeof(string) ? (IComparer<T>)StringComparer.Ordinal : Comparer<T>.Default;

            IEnumerable<Range<T>> withoutLast = sortedSet.Take(sortedSet.Count - 1);
            IEnumerable<Range<T>> withoutFirst = sortedSet.Skip(1);

            IEnumerable<Tuple<Range<T>, Range<T>>> currentAndNexts = withoutLast.Zip(withoutFirst, (current, next) => new Tuple<Range<T>, Range<T>>(current, next));
            foreach (Tuple<Range<T>, Range<T>> currentAndNext in currentAndNexts)
            {
                Range<T> current = currentAndNext.Item1;
                Range<T> next = currentAndNext.Item2;

                int compareResult = comparer.Compare(current.Max, next.Min);
                if (compareResult > 0)
                {
                    return false;
                }
                else if (compareResult == 0 && current.IsMaxInclusive && next.IsMinInclusive)
                {
                    return false;
                }
            }

            return true;
        }

        public static async Task<List<PartitionKeyRange>> GetOverlappingRangesAsync(
            this IRoutingMapProvider routingMapProvider,
            string collectionResourceId,
            IEnumerable<Range<string>> sortedRanges)
        {
            TryCatch<List<PartitionKeyRange>> tryGetOverlappingRangesAsync = await routingMapProvider.TryGetOverlappingRangesAsync(
                collectionResourceId,
                sortedRanges,
                forceRefresh: true);
            tryGetOverlappingRangesAsync.ThrowIfFailed();

            return tryGetOverlappingRangesAsync.Result;
        }

        public static async Task<TryCatch<List<PartitionKeyRange>>> TryGetOverlappingRangesAsync(
            this IRoutingMapProvider routingMapProvider,
            string collectionResourceId,
            IEnumerable<Range<string>> sortedRanges,
            bool forceRefresh = false)
        {
            if (sortedRanges == null)
            {
                throw new ArgumentNullException(nameof(sortedRanges));
            }

            // Remove the duplicates
            SortedSet<Range<string>> distinctSortedRanges = new SortedSet<Range<string>>(sortedRanges, Range<string>.MinComparer.Instance);

            // Make sure there is no overlap
            if (!IRoutingMapProviderExtensions.IsNonOverlapping(distinctSortedRanges))
            {
                throw new ArgumentException($"{nameof(sortedRanges)} had overlaps.");
            }

            // For each range try to figure out what PartitionKeyRanges it spans.
            List<PartitionKeyRange> targetRanges = new List<PartitionKeyRange>();
            foreach (Range<string> range in sortedRanges)
            {
                // if the range is empty then it by definition does not span any ranges.
                if (range.IsEmpty)
                {
                    continue;
                }

                // If current range already is covered by the most recently added PartitionKeyRange, then we can skip it
                // (to avoid duplicates).
                if ((targetRanges.Count != 0) && (Range<string>.MaxComparer.Instance.Compare(range, targetRanges.Last().ToRange()) <= 0))
                {
                    continue;
                }

                // Calculate what range to look up.
                Range<string> queryRange;
                if (targetRanges.Count == 0)
                {
                    // If there are no existing partition key ranges,
                    // then we take the first to get the ball rolling.
                    queryRange = range;
                }
                else
                {
                    // We don't want to double count a partition key range
                    // So we form a new range where 
                    // * left of the range is Max(lastPartitionKeyRange.Right(), currentRange.Left())
                    // * right is just the right of the currentRange.
                    // That way if the current range overlaps with the partition key range it won't double count it when doing:
                    //  routingMapProvider.TryGetOverlappingRangesAsync
                    string left = IRoutingMapProviderExtensions.Max(targetRanges.Last().MaxExclusive, range.Min);
                    bool leftInclusive = string.CompareOrdinal(left, range.Min) == 0 ? range.IsMinInclusive : false;
                    queryRange = new Range<string>(
                        left,
                        range.Max,
                        leftInclusive,
                        range.IsMaxInclusive);
                }

                TryCatch<IReadOnlyList<PartitionKeyRange>> overlappingRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                    collectionResourceId,
                    queryRange,
                    forceRefresh);

                if (overlappingRanges.Failed)
                {
                    // null means we weren't able to find the overlapping ranges.
                    // This is due to a stale cache.
                    // It is the caller's responsiblity to recall this method with forceRefresh = true
                    return TryCatch<List<PartitionKeyRange>>.FromException(new NotFoundException());

                    // Design note: It would be better if this method just returned a bool and followed the standard TryGet Pattern.
                    // It would be even better to remove the forceRefresh flag just replace it with a non TryGet method call.
                }

                targetRanges.AddRange(overlappingRanges.Result);
            }

            return TryCatch<List<PartitionKeyRange>>.FromResult(targetRanges);
        }
    }
}
