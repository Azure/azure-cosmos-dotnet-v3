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
            if (effectivePartitionKeyRanges == null)
            {
                throw new ArgumentNullException(nameof(effectivePartitionKeyRanges));
            }

            if (effectivePartitionKeyRanges.Count == 0)
            {
                throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not be empty.");
            }

            // Need to check if the ranges overlap
            // This can be done by checking the overall range that the ranges cover
            // and comparing it to the width of al the ranges.
            // https://stackoverflow.com/questions/3269434/whats-the-most-efficient-way-to-test-two-integer-ranges-for-overlap
            UInt128 minStart = UInt128.MaxValue;
            UInt128 maxEnd = UInt128.MinValue;
            UInt128 sumOfWidth = 0;

            foreach (EffectivePartitionKeyRange effectivePartitionKeyRange in effectivePartitionKeyRanges)
            {
                if (effectivePartitionKeyRange.Width == 0)
                {
                    throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} must not have empty ranges");
                }

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
                throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} has overlap.");
            }
            else if (rangeCoverage > sumOfWidth)
            {
                throw new ArgumentException($"{nameof(effectivePartitionKeyRanges)} are not contigious.");
            }
            else
            {
                this.effectivePartitionKeyRanges = effectivePartitionKeyRanges;
            }
        }

        public static PartitionedSortedEffectiveRanges Create(IEnumerable<EffectivePartitionKeyRange> effectivePartitionKeyRanges)
        {
            if (effectivePartitionKeyRanges == null)
            {
                throw new ArgumentNullException(nameof(effectivePartitionKeyRanges));
            }

            SortedSet<EffectivePartitionKeyRange> sortedSet = new SortedSet<EffectivePartitionKeyRange>();
            foreach (EffectivePartitionKeyRange effectivePartitionKeyRange in effectivePartitionKeyRanges)
            {
                if (!sortedSet.Add(effectivePartitionKeyRange))
                {
                    throw new ArgumentException($"Duplicate effective partition key added: {effectivePartitionKeyRange}.");
                }
            }

            return new PartitionedSortedEffectiveRanges(sortedSet);
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
    }
}
