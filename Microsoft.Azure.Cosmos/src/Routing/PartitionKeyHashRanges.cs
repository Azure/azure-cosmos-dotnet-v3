// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Represent a list of ranges with the following properties
    /// * no duplicates
    /// * non empty members
    /// * non empty itself
    /// * contiguous
    /// * non overlapping
    /// </summary>
    internal sealed class PartitionKeyHashRanges : IOrderedEnumerable<PartitionKeyHashRange>
    {
        private readonly SortedSet<PartitionKeyHashRange> partitionKeyHashRanges;

        private PartitionKeyHashRanges(SortedSet<PartitionKeyHashRange> partitionKeyHashRanges)
        {
            // All invariants are checked by the static constructor
            this.partitionKeyHashRanges = partitionKeyHashRanges;
        }

        public static PartitionKeyHashRanges Create(IEnumerable<PartitionKeyHashRange> partitionKeyHashRanges)
        {
            CreateOutcome createStatus = PartitionKeyHashRanges.TryCreate(
                partitionKeyHashRanges,
                out PartitionKeyHashRanges partitionedSortedEffectiveRanges);

            switch (createStatus)
            {
                case CreateOutcome.DuplicatePartitionKeyRange:
                    throw new ArgumentException($"{nameof(partitionKeyHashRanges)} must not have duplicate values.");

                case CreateOutcome.EmptyPartitionKeyRange:
                    throw new ArgumentException($"{nameof(partitionKeyHashRanges)} must not have an empty range.");

                case CreateOutcome.NoPartitionKeyRanges:
                    throw new ArgumentException($"{nameof(partitionKeyHashRanges)} must not be empty.");

                case CreateOutcome.NullPartitionKeyRanges:
                    throw new ArgumentNullException(nameof(partitionKeyHashRanges));

                case CreateOutcome.RangesAreNotContiguous:
                    throw new ArgumentException($"{nameof(partitionKeyHashRanges)} must have contiguous ranges.");

                case CreateOutcome.RangesOverlap:
                    throw new ArgumentException($"{nameof(partitionKeyHashRanges)} must not overlapping ranges.");

                case CreateOutcome.Success:
                    return partitionedSortedEffectiveRanges;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(CreateOutcome)}: {createStatus}.");
            }
        }

        public static CreateOutcome TryCreate(
            IEnumerable<PartitionKeyHashRange> partitionKeyHashRanges,
            out PartitionKeyHashRanges partitionedSortedEffectiveRanges)
        {
            if (partitionKeyHashRanges == null)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateOutcome.NullPartitionKeyRanges;
            }

            if (partitionKeyHashRanges.Count() == 0)
            {
                partitionedSortedEffectiveRanges = default;
                return CreateOutcome.NoPartitionKeyRanges;
            }

            SortedSet<PartitionKeyHashRange> sortedSet = new SortedSet<PartitionKeyHashRange>();
            foreach (PartitionKeyHashRange partitionKeyHashRange in partitionKeyHashRanges)
            {
                if (partitionKeyHashRange.StartInclusive.Equals(partitionKeyHashRange.EndExclusive))
                {
                    if (partitionKeyHashRange.StartInclusive.HasValue && partitionKeyHashRange.EndExclusive.HasValue)
                    {
                        partitionedSortedEffectiveRanges = default;
                        return CreateOutcome.EmptyPartitionKeyRange;
                    }
                }

                if (!sortedSet.Add(partitionKeyHashRange))
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
            (UInt128 sumOfWidth, bool overflowed) = (0, false);

            foreach (PartitionKeyHashRange partitionKeyHashRange in sortedSet)
            {
                if (partitionKeyHashRange.StartInclusive.HasValue)
                {
                    if (partitionKeyHashRange.StartInclusive.Value.Value < minStart)
                    {
                        minStart = partitionKeyHashRange.StartInclusive.Value.Value;
                    }
                }
                else
                {
                    minStart = UInt128.MinValue;
                }

                if (partitionKeyHashRange.EndExclusive.HasValue)
                {
                    if (partitionKeyHashRange.EndExclusive.Value.Value > maxEnd)
                    {
                        maxEnd = partitionKeyHashRange.EndExclusive.Value.Value;
                    }
                }
                else
                {
                    maxEnd = UInt128.MaxValue;
                }

                UInt128 width = partitionKeyHashRange.EndExclusive.GetValueOrDefault(new PartitionKeyHash(UInt128.MaxValue)).Value
                    - partitionKeyHashRange.StartInclusive.GetValueOrDefault(new PartitionKeyHash(UInt128.MinValue)).Value;
                sumOfWidth += width;
                if (sumOfWidth < width)
                {
                    overflowed = true;
                }
            }

            UInt128 rangeCoverage = maxEnd - minStart;
            if ((rangeCoverage < sumOfWidth) || overflowed)
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
                partitionedSortedEffectiveRanges = new PartitionKeyHashRanges(sortedSet);
                return CreateOutcome.Success;
            }
        }

        public IOrderedEnumerable<PartitionKeyHashRange> CreateOrderedEnumerable<TKey>(
            Func<PartitionKeyHashRange, TKey> keySelector,
            IComparer<TKey> comparer,
            bool descending)
        {
            IOrderedEnumerable<PartitionKeyHashRange> orderedEnumerable;
            if (descending)
            {
                orderedEnumerable = this.partitionKeyHashRanges
                    .OrderByDescending((range) => range.StartInclusive.Value)
                    .ThenByDescending(keySelector, comparer);
            }
            else
            {
                orderedEnumerable = this.partitionKeyHashRanges
                    .OrderBy((range) => range.StartInclusive.Value)
                    .ThenBy(keySelector, comparer);
            }

            return orderedEnumerable;
        }

        public IEnumerator<PartitionKeyHashRange> GetEnumerator()
        {
            return this.partitionKeyHashRanges.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.partitionKeyHashRanges.GetEnumerator();
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[");

            foreach (PartitionKeyHashRange partitionKeyHashRange in this.partitionKeyHashRanges)
            {
                stringBuilder.Append(partitionKeyHashRange.ToString());
                stringBuilder.Append(",");
            }

            stringBuilder.Append("]");

            return stringBuilder.ToString();
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
