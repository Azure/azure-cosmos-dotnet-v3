// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class PartitionedSortedEffectiveRanges : IOrderedEnumerable<EffectivePartitionKeyRange>
    {
        private readonly SortedSet<EffectivePartitionKeyRange> effectivePartitionKeyRanges;

        private PartitionedSortedEffectiveRanges(SortedSet<EffectivePartitionKeyRange> effectivePartitionKeyRanges)
        {
            // All invariants are checked by the static constructor
            this.effectivePartitionKeyRanges = effectivePartitionKeyRanges;
        }

        public static PartitionedSortedEffectiveRanges Create(IEnumerable<EffectivePartitionKeyRange> effectivePartitionKeyRanges)
        {
            CreateStatus createStatus = PartitionedSortedEffectiveRanges.TryCreate(
                effectivePartitionKeyRanges,
                out PartitionedSortedEffectiveRanges partitionedSortedEffectiveRanges);

            switch (createStatus)
            {
                case CreateStatus.DuplicatePartitionKeyRange:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not have duplicate values.");

                case CreateStatus.EmptyPartitionKeyRange:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not have an empty range.");

                case CreateStatus.NoPartitionKeyRanges:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not be empty.");

                case CreateStatus.NullPartitionKeyRanges:
                    throw new ArgumentNullException(nameof(effectivePartitionKeyRanges));

                case CreateStatus.RangesAreNotContiguous:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must have contiguous ranges.");

                case CreateStatus.RangesOverlap:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not overlapping ranges.");

                case CreateStatus.Success:
                    return partitionedSortedEffectiveRanges;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(CreateStatus)}: {createStatus}.");
            }
        }

        public static CreateStatus TryCreate(
            IEnumerable<EffectivePartitionKeyRange> effectivePartitionKeyRanges,
            out PartitionedSortedEffectiveRanges partitionedSortedEffectiveRanges)
        {
            if (effectivePartitionKeyRanges == null)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateStatus.NullPartitionKeyRanges;
            }

            if (effectivePartitionKeyRanges.Count() == 0)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateStatus.NoPartitionKeyRanges;
            }

            SortedSet<EffectivePartitionKeyRange> sortedSet = new SortedSet<EffectivePartitionKeyRange>();
            foreach (EffectivePartitionKeyRange effectivePartitionKeyRange in effectivePartitionKeyRanges)
            {
                if (effectivePartitionKeyRange.Width == 0)
                {
                    partitionedSortedEffectiveRanges = default;
                    return CreateStatus.EmptyPartitionKeyRange;
                }

                if (!sortedSet.Add(effectivePartitionKeyRange))
                {
                    partitionedSortedEffectiveRanges = default;
                    return CreateStatus.DuplicatePartitionKeyRange;
                }
            }

            // Need to check if the ranges overlap
            // This can be done by checking the overall range that the ranges cover
            // and comparing it to the width of al the ranges.
            // https://stackoverflow.com/questions/3269434/whats-the-most-efficient-way-to-test-two-integer-ranges-for-overlap
            UInt128 minStart = UInt128.MaxValue;
            UInt128 maxEnd = UInt128.MinValue;
            UInt128 sumOfWidth = 0;

            foreach (EffectivePartitionKeyRange effectivePartitionKeyRange in sortedSet)
            {
                if (effectivePartitionKeyRange.Start.Value < minStart)
                {
                    minStart = effectivePartitionKeyRange.Start.Value;
                }

                if (effectivePartitionKeyRange.End.Value > maxEnd)
                {
                    maxEnd = effectivePartitionKeyRange.End.Value;
                }

                sumOfWidth += effectivePartitionKeyRange.Width;
            }

            UInt128 rangeCoverage = maxEnd - minStart;
            if (rangeCoverage < sumOfWidth)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateStatus.RangesOverlap;
            }
            else if (rangeCoverage > sumOfWidth)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateStatus.RangesAreNotContiguous;
            }
            else
            {
                partitionedSortedEffectiveRanges = new PartitionedSortedEffectiveRanges(sortedSet);
                return CreateStatus.Success;
            }
        }

        public IOrderedEnumerable<EffectivePartitionKeyRange> CreateOrderedEnumerable<TKey>(
            Func<EffectivePartitionKeyRange, TKey> keySelector,
            IComparer<TKey> comparer,
            bool descending)
        {
            IOrderedEnumerable<EffectivePartitionKeyRange> orderedEnumerable;
            if (descending)
            {
                orderedEnumerable = this.effectivePartitionKeyRanges
                    .OrderByDescending((range) => range.Start.Value)
                    .ThenByDescending(keySelector, comparer);
            }
            else
            {
                orderedEnumerable = this.effectivePartitionKeyRanges
                    .OrderBy((range) => range.Start.Value)
                    .ThenBy(keySelector, comparer);
            }

            return orderedEnumerable;
        }

        public IEnumerator<EffectivePartitionKeyRange> GetEnumerator()
        {
            return this.effectivePartitionKeyRanges.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.effectivePartitionKeyRanges.GetEnumerator();
        }

        public enum CreateStatus
        {
            DuplicatePartitionKeyRange,
            EmptyPartitionKeyRange,
            NoPartitionKeyRanges,
            NullPartitionKeyRanges,
            RangesAreNotContiguous,
            RangesOverlap,
            Success,
        }
    }
}
