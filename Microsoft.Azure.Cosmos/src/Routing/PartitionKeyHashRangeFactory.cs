// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal abstract class PartitionKeyHashRangeFactory
    {
        public abstract PartitionKeyHashRange FullRange { get; }

        public static PartitionKeyHashRanges SplitRange(PartitionKeyHashRange partitionKeyHashRange, int numRanges)
        {
            SplitOutcome splitOutcome = PartitionKeyHashRangeFactory.TrySplitRange(
                partitionKeyHashRange,
                numRanges,
                out PartitionKeyHashRanges splitRanges);

            switch (splitOutcome)
            {
                case SplitOutcome.Success:
                    return splitRanges;

                case SplitOutcome.NumRangesNeedsToBePositive:
                    throw new ArgumentOutOfRangeException($"{nameof(numRanges)} must be a positive integer");

                case SplitOutcome.RangeNotWideEnough:
                    throw new ArgumentOutOfRangeException($"{nameof(partitionKeyHashRange)} is not wide enough to split into {numRanges} ranges.");

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(SplitOutcome)}: {splitOutcome}.");
            }
        }

        public static SplitOutcome TrySplitRange(PartitionKeyHashRange partitionKeyHashRange, int numRanges, out PartitionKeyHashRanges splitRanges)
        {
            if (numRanges < 1)
            {
                splitRanges = default;
                return SplitOutcome.NumRangesNeedsToBePositive;
            }

            UInt128 actualEnd = partitionKeyHashRange.EndExclusive.HasValue ? partitionKeyHashRange.EndExclusive.Value.Value : UInt128.MaxValue;
            UInt128 actualStart = partitionKeyHashRange.StartInclusive.HasValue ? partitionKeyHashRange.StartInclusive.Value.Value : UInt128.MinValue;
            UInt128 rangeLength = actualEnd - actualStart;
            if (rangeLength < numRanges)
            {
                splitRanges = default;
                return SplitOutcome.RangeNotWideEnough;
            }

            List<PartitionKeyHashRange> childrenRanges = new List<PartitionKeyHashRange>();
            UInt128 subrangeLength = rangeLength / numRanges;
            for (int i = 0; i < numRanges - 1; i++)
            {
                PartitionKeyHash start = new PartitionKeyHash(actualStart + (subrangeLength * i));
                PartitionKeyHash end = new PartitionKeyHash(start.Value + subrangeLength);
                childrenRanges.Add(new PartitionKeyHashRange(start, end));
            }

            // Last range will have remaining EPKs, since the range might not be divisible.
            {
                PartitionKeyHash start = new PartitionKeyHash(actualStart + (subrangeLength * (numRanges - 1)));
                PartitionKeyHash? end = partitionKeyHashRange.EndExclusive;
                childrenRanges.Add(new PartitionKeyHashRange(start, end));
            }

            splitRanges = PartitionKeyHashRanges.Create(childrenRanges);
            return SplitOutcome.Success;
        }

        public static PartitionKeyHashRange MergeRanges(PartitionKeyHashRanges partitionedSortedEffectiveRanges)
        {
            if (partitionedSortedEffectiveRanges == null)
            {
                throw new ArgumentNullException(nameof(partitionedSortedEffectiveRanges));
            }

            return new PartitionKeyHashRange(
                startInclusive: partitionedSortedEffectiveRanges.First().StartInclusive,
                endExclusive: partitionedSortedEffectiveRanges.Last().EndExclusive);
        }

        private sealed class V1 : PartitionKeyHashRangeFactory
        {
            private static readonly PartitionKeyHashRange fullRange = new PartitionKeyHashRange(
                startInclusive: new PartitionKeyHash(0),
                endExclusive: new PartitionKeyHash(uint.MaxValue));

            public override PartitionKeyHashRange FullRange => PartitionKeyHashRangeFactory.V1.fullRange;
        }

        private sealed class V2 : PartitionKeyHashRangeFactory
        {
            private static readonly PartitionKeyHashRange fullRange = new PartitionKeyHashRange(
                startInclusive: new PartitionKeyHash(0),
                endExclusive: new PartitionKeyHash(UInt128.FromByteArray(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F })));

            public override PartitionKeyHashRange FullRange => PartitionKeyHashRangeFactory.V2.fullRange;
        }

        public enum SplitOutcome
        {
            Success,
            NumRangesNeedsToBePositive,
            RangeNotWideEnough,
        }
    }
}
