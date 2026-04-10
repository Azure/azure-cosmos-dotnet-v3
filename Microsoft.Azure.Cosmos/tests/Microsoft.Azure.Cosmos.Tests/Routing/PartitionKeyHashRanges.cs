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
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

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

        public IOrderedEnumerable<PartitionKeyHashRange> CreateOrderedEnumerable<TKey>(
            Func<PartitionKeyHashRange, TKey> keySelector,
            IComparer<TKey> comparer,
            bool descending)
        {
            IOrderedEnumerable<PartitionKeyHashRange> orderedEnumerable = descending
                ? this.partitionKeyHashRanges
                    .OrderByDescending((range) => range.StartInclusive.Value)
                    .ThenByDescending(keySelector, comparer)
                : this.partitionKeyHashRanges
                    .OrderBy((range) => range.StartInclusive.Value)
                    .ThenBy(keySelector, comparer);
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

        public static PartitionKeyHashRanges Create(IEnumerable<PartitionKeyHashRange> partitionKeyHashRanges)
        {
            TryCatch<PartitionKeyHashRanges> tryCreateMonad = Monadic.Create(partitionKeyHashRanges);
            tryCreateMonad.ThrowIfFailed();

            return tryCreateMonad.Result;
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
                    if (partitionKeyHashRange.StartInclusive.Value.HashValues[0] < minStart)
                    {
                        minStart = partitionKeyHashRange.StartInclusive.Value.HashValues[0];
                    }
                }
                else
                {
                    minStart = UInt128.MinValue;
                }

                if (partitionKeyHashRange.EndExclusive.HasValue)
                {
                    if (partitionKeyHashRange.EndExclusive.Value.HashValues[0] > maxEnd)
                    {
                        maxEnd = partitionKeyHashRange.EndExclusive.Value.HashValues[0];
                    }
                }
                else
                {
                    maxEnd = UInt128.MaxValue;
                }

                UInt128 width = partitionKeyHashRange.EndExclusive.GetValueOrDefault(new PartitionKeyHash(UInt128.MaxValue)).HashValues[0]
                    - partitionKeyHashRange.StartInclusive.GetValueOrDefault(new PartitionKeyHash(UInt128.MinValue)).HashValues[0];
                sumOfWidth += width;
                if (sumOfWidth < width)
                {
                    overflowed = true;
                }
            }

            UInt128 rangeCoverage = maxEnd - minStart;
            CreateOutcome createOutcome;
            if ((rangeCoverage < sumOfWidth) || overflowed)
            {
                partitionedSortedEffectiveRanges = default;
                createOutcome = CreateOutcome.RangesOverlap;
            }
            else if (rangeCoverage > sumOfWidth)
            {
                partitionedSortedEffectiveRanges = default;
                createOutcome = CreateOutcome.RangesAreNotContiguous;
            }
            else
            {
                partitionedSortedEffectiveRanges = new PartitionKeyHashRanges(sortedSet);
                createOutcome = CreateOutcome.Success;
            }

            return createOutcome;
        }

        public static class Monadic
        {
            public static TryCatch<PartitionKeyHashRanges> Create(IEnumerable<PartitionKeyHashRange> partitionKeyHashRanges)
            {
                CreateOutcome createStatus = PartitionKeyHashRanges.TryCreate(
                    partitionKeyHashRanges,
                    out PartitionKeyHashRanges partitionedSortedEffectiveRanges);

                return createStatus switch
                {
                    CreateOutcome.DuplicatePartitionKeyRange => TryCatch<PartitionKeyHashRanges>.FromException(
                        new ArgumentException($"{nameof(partitionKeyHashRanges)} must not have duplicate values.")),
                    CreateOutcome.EmptyPartitionKeyRange => TryCatch<PartitionKeyHashRanges>.FromException(
                        new ArgumentException($"{nameof(partitionKeyHashRanges)} must not have an empty range.")),
                    CreateOutcome.NoPartitionKeyRanges => TryCatch<PartitionKeyHashRanges>.FromException(
                        new ArgumentException($"{nameof(partitionKeyHashRanges)} must not be empty.")),
                    CreateOutcome.NullPartitionKeyRanges => TryCatch<PartitionKeyHashRanges>.FromException(
                        new ArgumentNullException(nameof(partitionKeyHashRanges))),
                    CreateOutcome.RangesAreNotContiguous => TryCatch<PartitionKeyHashRanges>.FromException(
                        new ArgumentException($"{nameof(partitionKeyHashRanges)} must have contiguous ranges.")),
                    CreateOutcome.RangesOverlap => TryCatch<PartitionKeyHashRanges>.FromException(
                        new ArgumentException($"{nameof(partitionKeyHashRanges)} must not overlapping ranges.")),
                    CreateOutcome.Success => TryCatch<PartitionKeyHashRanges>.FromResult(partitionedSortedEffectiveRanges),
                    _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(CreateOutcome)}: {createStatus}."),
                };
            }
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