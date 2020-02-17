// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;

    internal abstract class EffectivePartitionKeyRangeFactory
    {
        public abstract EffectivePartitionKeyRange FullRange { get; }

        public IEnumerable<EffectivePartitionKeyRange> SplitRange(EffectivePartitionKeyRange effectivePartitionKeyRange, int numRanges)
        {
            if (numRanges < 1)
            {
                throw new ArgumentOutOfRangeException($"{nameof(numRanges)} must be a positive integer.");
            }

            if (effectivePartitionKeyRange.Width < numRanges)
            {
                throw new ArgumentOutOfRangeException($"Can not split {effectivePartitionKeyRange} into {numRanges}, since {effectivePartitionKeyRange} is not wide enough.");
            }

            UInt128 subrangeWidth = effectivePartitionKeyRange.Width / numRanges;
            for (int i = 0; i < numRanges - 1; i++)
            {
                EffectivePartitionKey offset = new EffectivePartitionKey(effectivePartitionKeyRange.Start.Value + (subrangeWidth * i));
                EffectivePartitionKey count = new EffectivePartitionKey(subrangeWidth);
                yield return new EffectivePartitionKeyRange(offset, count);
            }

            // Last range will have remaining EPKs, since the range might not be divisible.
            {
                EffectivePartitionKey offset = new EffectivePartitionKey(effectivePartitionKeyRange.Start.Value + (subrangeWidth * (numRanges - 1)));
                EffectivePartitionKey count = new EffectivePartitionKey(effectivePartitionKeyRange.Width - effectivePartitionKeyRange.Start.Value);
                yield return new EffectivePartitionKeyRange(offset, count);
            }
        }

        public EffectivePartitionKeyRange MergeRanges(IEnumerable<EffectivePartitionKeyRange> effectivePartitionKeyRanges)
        {
            if (effectivePartitionKeyRanges == null)
            {
                throw new ArgumentNullException(nameof(effectivePartitionKeyRanges));
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
                return new EffectivePartitionKeyRange(start: new EffectivePartitionKey(minStart), end: new EffectivePartitionKey(maxEnd));
            }
        }

        private sealed class EffectivePartitionKeyRangeFactoryV1 : EffectivePartitionKeyRangeFactory
        {
            private static readonly EffectivePartitionKeyRange fullRange = new EffectivePartitionKeyRange(
                start: new EffectivePartitionKey(0),
                end: new EffectivePartitionKey(uint.MaxValue));

            public override EffectivePartitionKeyRange FullRange => EffectivePartitionKeyRangeFactoryV1.fullRange;
        }

        private sealed class EffectivePartitionKeyRangeFactoryV2 : EffectivePartitionKeyRangeFactory
        {
            private static readonly EffectivePartitionKeyRange fullRange = new EffectivePartitionKeyRange(
                start: new EffectivePartitionKey(0),
                end: new EffectivePartitionKey(UInt128.FromByteArray(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x3F })));

            public override EffectivePartitionKeyRange FullRange => EffectivePartitionKeyRangeFactoryV2.fullRange;
        }
    }
}
