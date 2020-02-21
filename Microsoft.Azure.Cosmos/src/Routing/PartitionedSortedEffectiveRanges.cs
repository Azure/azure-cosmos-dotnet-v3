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
            CreateOutcome createStatus = PartitionedSortedEffectiveRanges.TryCreate(
                effectivePartitionKeyRanges,
                out PartitionedSortedEffectiveRanges partitionedSortedEffectiveRanges);

            switch (createStatus)
            {
                case CreateOutcome.DuplicatePartitionKeyRange:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not have duplicate values.");

                case CreateOutcome.EmptyPartitionKeyRange:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not have an empty range.");

                case CreateOutcome.NoPartitionKeyRanges:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not be empty.");

                case CreateOutcome.NullPartitionKeyRanges:
                    throw new ArgumentNullException(nameof(effectivePartitionKeyRanges));

                case CreateOutcome.RangesAreNotContiguous:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must have contiguous ranges.");

                case CreateOutcome.RangesOverlap:
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not overlapping ranges.");

                case CreateOutcome.Success:
                    return partitionedSortedEffectiveRanges;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(CreateOutcome)}: {createStatus}.");
            }
        }

        public static CreateOutcome TryCreate(
            IEnumerable<EffectivePartitionKeyRange> effectivePartitionKeyRanges,
            out PartitionedSortedEffectiveRanges partitionedSortedEffectiveRanges)
        {
            if (effectivePartitionKeyRanges == null)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateOutcome.NullPartitionKeyRanges;
            }

            if (effectivePartitionKeyRanges.Count() == 0)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateOutcome.NoPartitionKeyRanges;
            }

            SortedSet<EffectivePartitionKeyRange> sortedSet = new SortedSet<EffectivePartitionKeyRange>();
            foreach (EffectivePartitionKeyRange effectivePartitionKeyRange in effectivePartitionKeyRanges)
            {
                if (effectivePartitionKeyRange.StartInclusive.Equals(effectivePartitionKeyRange.EndExclusive))
                {
                    partitionedSortedEffectiveRanges = default;
                    return CreateOutcome.EmptyPartitionKeyRange;
                }

                if (!sortedSet.Add(effectivePartitionKeyRange))
                {
                    partitionedSortedEffectiveRanges = default;
                    return CreateOutcome.DuplicatePartitionKeyRange;
                }
            }

            // Need to check if the ranges overlap
            // This can be done by checking the overall range that the ranges cover
            // and comparing it to the width of all the ranges.
            // https://stackoverflow.com/questions/3269434/whats-the-most-efficient-way-to-test-two-integer-ranges-for-overlap
            UInt128 minStart = UInt128.MaxValue;
            UInt128 maxEnd = UInt128.MinValue;
            UInt128 sumOfWidth = 0;

            foreach (EffectivePartitionKeyRange effectivePartitionKeyRange in sortedSet)
            {
                if (effectivePartitionKeyRange.StartInclusive.Value < minStart)
                {
                    minStart = effectivePartitionKeyRange.StartInclusive.Value;
                }

                if (effectivePartitionKeyRange.EndExclusive.Value > maxEnd)
                {
                    maxEnd = effectivePartitionKeyRange.EndExclusive.Value;
                }

                UInt128 width = effectivePartitionKeyRange.EndExclusive.Value - effectivePartitionKeyRange.StartInclusive.Value;
                sumOfWidth += width;
            }

            UInt128 rangeCoverage = maxEnd - minStart;
            if (rangeCoverage < sumOfWidth)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateOutcome.RangesOverlap;
            }
            else if (rangeCoverage > sumOfWidth)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateOutcome.RangesAreNotContiguous;
            }
            else
            {
                partitionedSortedEffectiveRanges = new PartitionedSortedEffectiveRanges(sortedSet);
                return CreateOutcome.Success;
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
                    .OrderByDescending((range) => range.StartInclusive.Value)
                    .ThenByDescending(keySelector, comparer);
            }
            else
            {
                orderedEnumerable = this.effectivePartitionKeyRanges
                    .OrderBy((range) => range.StartInclusive.Value)
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

        public enum CreateOutcome
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
