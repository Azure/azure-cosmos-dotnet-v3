//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Stored partition key ranges in an efficient way with some additional information and provides
    /// convenience methods for working with set of ranges.
    /// </summary>
    internal sealed class CollectionRoutingMap
    {
        private static readonly int InvalidPkRangeId = -1;

        /// <summary>
        /// Partition key range id to partition address and range.
        /// </summary>
        private readonly Dictionary<string, Tuple<PartitionKeyRange, ServiceIdentity>> rangeById;

        private readonly List<PartitionKeyRange> orderedPartitionKeyRanges;
        private readonly List<Range<string>> orderedRanges;
        private readonly HashSet<string> goneRanges;

        public int HighestNonOfflinePkRangeId { get; private set; }

        private CollectionRoutingMap(
            Dictionary<string, Tuple<PartitionKeyRange, ServiceIdentity>> rangeById,
            List<PartitionKeyRange> orderedPartitionKeyRanges,
            string collectionUniqueId,
            string changeFeedNextIfNoneMatch)
        {
            this.rangeById = rangeById ?? throw new ArgumentNullException(nameof(rangeById));
            this.orderedPartitionKeyRanges = orderedPartitionKeyRanges ?? throw new ArgumentNullException(nameof(orderedPartitionKeyRanges));
            this.orderedRanges = orderedPartitionKeyRanges.Select(
                range =>
                new Range<string>(
                    range.MinInclusive,
                    range.MaxExclusive,
                    true,
                    false)).ToList();

            this.CollectionUniqueId = collectionUniqueId ?? throw new ArgumentNullException(nameof(collectionUniqueId));
            this.ChangeFeedNextIfNoneMatch = changeFeedNextIfNoneMatch;
            this.goneRanges = new HashSet<string>(orderedPartitionKeyRanges.SelectMany(r => r.Parents ?? Enumerable.Empty<string>()));

            this.HighestNonOfflinePkRangeId = orderedPartitionKeyRanges.Max(range =>
            {
                int pkId = CollectionRoutingMap.InvalidPkRangeId;
                if (!int.TryParse(range.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out pkId))
                {
                    DefaultTrace.TraceCritical(
                        "Could not parse partition key range Id as int {0} for collectionRid {1}",
                        range.Id,
                        this.CollectionUniqueId);

                    throw new ArgumentException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Could not parse partition key range Id as int {0} for collectionRid {1}",
                        range.Id,
                        this.CollectionUniqueId));
                }

                return range.Status == PartitionKeyRangeStatus.Offline ? CollectionRoutingMap.InvalidPkRangeId : pkId;
            });
        }

        public static bool TryCreateCompleteRoutingMap(
            IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> ranges,
            string collectionUniqueId,
            string changeFeedNextIfNoneMatch,
            out CollectionRoutingMap collectionRoutingMap)
        {
            Dictionary<string, Tuple<PartitionKeyRange, ServiceIdentity>> rangeById =
                new Dictionary<string, Tuple<PartitionKeyRange, ServiceIdentity>>(StringComparer.Ordinal);

            foreach (Tuple<PartitionKeyRange, ServiceIdentity> range in ranges)
            {
                rangeById[range.Item1.Id] = range;
            }

            List<Tuple<PartitionKeyRange, ServiceIdentity>> sortedRanges = rangeById.Values.ToList();
            sortedRanges.Sort(new MinPartitionKeyTupleComparer());
            List<PartitionKeyRange> orderedRanges = sortedRanges.Select(range => range.Item1).ToList();

            if (!IsCompleteSetOfRanges(orderedRanges))
            {
                collectionRoutingMap = default;
                return false;
            }

            collectionRoutingMap = new CollectionRoutingMap(rangeById, orderedRanges, collectionUniqueId, changeFeedNextIfNoneMatch);
            return true;
        }

        public string CollectionUniqueId { get; private set; }

        public string ChangeFeedNextIfNoneMatch { get; private set; }

        /// <summary>
        /// Ranges in increasing order.
        /// </summary>
        public IReadOnlyList<PartitionKeyRange> OrderedPartitionKeyRanges => this.orderedPartitionKeyRanges;

        public IReadOnlyList<PartitionKeyRange> GetOverlappingRanges(params Range<string>[] providedPartitionKeyRanges)
        {
            if (providedPartitionKeyRanges == null)
            {
                throw new ArgumentNullException(nameof(providedPartitionKeyRanges));
            }

            SortedList<string, PartitionKeyRange> partitionRanges = new SortedList<string, PartitionKeyRange>();

            // Algorithm: Use binary search to find the positions of the min key and max key in the routing map
            // Then within that two positions, check for overlapping partition key ranges
            foreach (Range<string> providedRange in providedPartitionKeyRanges)
            {
                int minIndex = this.orderedRanges.BinarySearch(providedRange, Range<string>.MinComparer.Instance);
                if (minIndex < 0)
                {
                    minIndex = Math.Max(0, (~minIndex) - 1);
                }

                int maxIndex = this.orderedRanges.BinarySearch(providedRange, Range<string>.MaxComparer.Instance);
                if (maxIndex < 0)
                {
                    maxIndex = Math.Min(this.OrderedPartitionKeyRanges.Count - 1, ~maxIndex);
                }

                for (int i = minIndex; i <= maxIndex; ++i)
                {
                    if (Range<string>.CheckOverlapping(this.orderedRanges[i], providedRange))
                    {
                        partitionRanges[this.OrderedPartitionKeyRanges[i].MinInclusive] = this.OrderedPartitionKeyRanges[i];
                    }
                }
            }

            return new ReadOnlyCollection<PartitionKeyRange>(partitionRanges.Values);
        }

        public PartitionKeyRange GetRangeByEffectivePartitionKey(string effectivePartitionKeyValue)
        {
            if (string.CompareOrdinal(effectivePartitionKeyValue, PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey) >= 0)
            {
                throw new ArgumentException("effectivePartitionKeyValue");
            }

            if (string.CompareOrdinal(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, effectivePartitionKeyValue) == 0)
            {
                return this.orderedPartitionKeyRanges[0];
            }

            int index = this.orderedRanges.BinarySearch(
                new Range<string>(effectivePartitionKeyValue, effectivePartitionKeyValue, true, true),
                Range<string>.MinComparer.Instance);

            if (index < 0)
            {
                index = ~index - 1;
                Debug.Assert(index >= 0);
                Debug.Assert(this.orderedRanges[index].Contains(effectivePartitionKeyValue));
            }

            return this.orderedPartitionKeyRanges[index];
        }

        public bool TryGetRangeByPartitionKeyRangeId(string partitionKeyRangeId, out PartitionKeyRange partitionKeyRange)
        {
            if (!this.rangeById.TryGetValue(partitionKeyRangeId, out Tuple<PartitionKeyRange, ServiceIdentity> addresses))
            {
                partitionKeyRange = default;
                return false;
            }

            partitionKeyRange = addresses.Item1;
            return true;
        }

        public bool TryGetInfoByPartitionKeyRangeId(string partitionKeyRangeId, out ServiceIdentity serviceIdentity)
        {
            if (!this.rangeById.TryGetValue(partitionKeyRangeId, out Tuple<PartitionKeyRange, ServiceIdentity> addresses))
            {
                serviceIdentity = default;
                return false;
            }

            serviceIdentity = addresses.Item2;
            return true;
        }

        public bool TryCombine(
            IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> ranges,
            string changeFeedNextIfNoneMatch,
            out CollectionRoutingMap collectionRoutingMap)
        {
            HashSet<string> newGoneRanges = new HashSet<string>(ranges.SelectMany(tuple => tuple.Item1.Parents ?? Enumerable.Empty<string>()));
            newGoneRanges.UnionWith(this.goneRanges);

            Dictionary<string, Tuple<PartitionKeyRange, ServiceIdentity>> newRangeById =
                this.rangeById.Values.Where(tuple => !newGoneRanges.Contains(tuple.Item1.Id)).ToDictionary(tuple => tuple.Item1.Id, StringComparer.Ordinal);

            foreach (Tuple<PartitionKeyRange, ServiceIdentity> tuple in ranges.Where(tuple => !newGoneRanges.Contains(tuple.Item1.Id)))
            {
                newRangeById[tuple.Item1.Id] = tuple;

                DefaultTrace.TraceInformation(
                    "CollectionRoutingMap.TryCombine newRangeById[{0}] = {1}",
                    tuple.Item1.Id, tuple);
            }

            List<Tuple<PartitionKeyRange, ServiceIdentity>> sortedRanges = newRangeById.Values.ToList();

            sortedRanges.Sort(new MinPartitionKeyTupleComparer());
            List<PartitionKeyRange> newOrderedRanges = sortedRanges.Select(range => range.Item1).ToList();

            if (!IsCompleteSetOfRanges(newOrderedRanges))
            {
                collectionRoutingMap = default;
                return false;
            }

            collectionRoutingMap = new CollectionRoutingMap(newRangeById, newOrderedRanges, this.CollectionUniqueId, changeFeedNextIfNoneMatch);
            return true;
        }

        private class MinPartitionKeyTupleComparer : IComparer<Tuple<PartitionKeyRange, ServiceIdentity>>
        {
            public int Compare(Tuple<PartitionKeyRange, ServiceIdentity> left, Tuple<PartitionKeyRange, ServiceIdentity> right)
            {
                return string.CompareOrdinal(left.Item1.MinInclusive, right.Item1.MinInclusive);
            }
        }

        private static bool IsCompleteSetOfRanges(IList<PartitionKeyRange> orderedRanges)
        {
            bool isComplete = false;
            if (orderedRanges.Count > 0)
            {
                PartitionKeyRange firstRange = orderedRanges[0];
                PartitionKeyRange lastRange = orderedRanges[orderedRanges.Count - 1];
                isComplete = string.CompareOrdinal(firstRange.MinInclusive, PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey) == 0;
                isComplete &= string.CompareOrdinal(lastRange.MaxExclusive, PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey) == 0;

                for (int i = 1; i < orderedRanges.Count; i++)
                {
                    PartitionKeyRange previousRange = orderedRanges[i - 1];
                    PartitionKeyRange currentRange = orderedRanges[i];
                    isComplete &= previousRange.MaxExclusive.Equals(currentRange.MinInclusive);

                    if (!isComplete)
                    {
                        if (string.CompareOrdinal(previousRange.MaxExclusive, currentRange.MinInclusive) > 0)
                        {
                            throw new InvalidOperationException("Ranges overlap");
                        }

                        break;
                    }
                }
            }

            return isComplete;
        }

        public bool IsGone(string partitionKeyRangeId)
        {
            return this.goneRanges.Contains(partitionKeyRangeId);
        }

    }
}
