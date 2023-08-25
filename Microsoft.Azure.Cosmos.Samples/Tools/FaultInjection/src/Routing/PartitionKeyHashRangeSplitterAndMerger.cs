// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Splits or merges <see cref="PartitionKeyHashRange"/>
    /// </summary>
    internal abstract class PartitionKeyHashRangeSplitterAndMerger
    {
        public abstract PartitionKeyHashRange FullRange { get; }

        public static PartitionKeyHashRanges SplitRange(PartitionKeyHashRange partitionKeyHashRange, int rangeCount)
        {
            SplitOutcome splitOutcome = PartitionKeyHashRangeSplitterAndMerger.TrySplitRange(
                partitionKeyHashRange,
                rangeCount,
                out PartitionKeyHashRanges splitRanges);

            return splitOutcome switch
            {
                SplitOutcome.Success => splitRanges,
                SplitOutcome.NumRangesNeedsToBeGreaterThanZero => throw new ArgumentOutOfRangeException($"{nameof(rangeCount)} must be a positive integer"),
                SplitOutcome.RangeNotWideEnough => throw new ArgumentOutOfRangeException($"{nameof(partitionKeyHashRange)} is not wide enough to split into {rangeCount} ranges."),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(SplitOutcome)}: {splitOutcome}."),
            };
        }

        public static SplitOutcome TrySplitRange(PartitionKeyHashRange partitionKeyHashRange, int rangeCount, out PartitionKeyHashRanges splitRanges)
        {
            if (rangeCount < 1)
            {
                splitRanges = default;
                return SplitOutcome.NumRangesNeedsToBeGreaterThanZero;
            }

            UInt128 actualEnd = partitionKeyHashRange.EndExclusive.HasValue ? partitionKeyHashRange.EndExclusive.Value.Value : UInt128.MaxValue;
            UInt128 actualStart = partitionKeyHashRange.StartInclusive.HasValue ? partitionKeyHashRange.StartInclusive.Value.Value : UInt128.MinValue;
            UInt128 rangeLength = actualEnd - actualStart;
            if (rangeLength < rangeCount)
            {
                splitRanges = default;
                return SplitOutcome.RangeNotWideEnough;
            }

            if (rangeCount == 1)
            {
                // Just return the range as is:
                splitRanges = PartitionKeyHashRanges.Create(new PartitionKeyHashRange[] { partitionKeyHashRange });
                return SplitOutcome.Success;
            }

            List<PartitionKeyHashRange> childRanges = new List<PartitionKeyHashRange>();
            UInt128 childRangeLength = rangeLength / rangeCount;
            // First range should start at the user supplied range (since the input might have an open range and we don't want to return 0)
            {
                PartitionKeyHash? start = partitionKeyHashRange.StartInclusive;
                PartitionKeyHash end = new PartitionKeyHash(actualStart + childRangeLength);
                childRanges.Add(new PartitionKeyHashRange(start, end));
            }

            for (int i = 1; i < rangeCount - 1; i++)
            {
                PartitionKeyHash start = new PartitionKeyHash(actualStart + (childRangeLength * i));
                PartitionKeyHash end = new PartitionKeyHash(start.Value + childRangeLength);
                childRanges.Add(new PartitionKeyHashRange(start, end));
            }

            // Last range will have remaining EPKs, since the range might not be divisible.
            {
                PartitionKeyHash start = new PartitionKeyHash(actualStart + (childRangeLength * (rangeCount - 1)));
                PartitionKeyHash? end = partitionKeyHashRange.EndExclusive;
                childRanges.Add(new PartitionKeyHashRange(start, end));
            }

            splitRanges = PartitionKeyHashRanges.Create(childRanges);
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

        private sealed class V1 : PartitionKeyHashRangeSplitterAndMerger
        {
            private static readonly PartitionKeyHashRange fullRange = new PartitionKeyHashRange(
                startInclusive: new PartitionKeyHash(0),
                endExclusive: new PartitionKeyHash(uint.MaxValue));

            public override PartitionKeyHashRange FullRange => PartitionKeyHashRangeSplitterAndMerger.V1.fullRange;
        }

        private sealed class V2 : PartitionKeyHashRangeSplitterAndMerger
        {
            private static readonly PartitionKeyHashRange fullRange = new PartitionKeyHashRange(
                startInclusive: new PartitionKeyHash(0),
                endExclusive: new PartitionKeyHash(UInt128.FromByteArray(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F })));

            public override PartitionKeyHashRange FullRange => PartitionKeyHashRangeSplitterAndMerger.V2.fullRange;
        }

        public enum SplitOutcome
        {
            Success,
            NumRangesNeedsToBeGreaterThanZero,
            RangeNotWideEnough,
        }
    }
}
