// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal abstract class EffectivePartitionKeyRangeFactory
    {
        public abstract EffectivePartitionKeyRange FullRange { get; }

        public PartitionedSortedEffectiveRanges SplitRange(EffectivePartitionKeyRange effectivePartitionKeyRange, int numRanges)
        {
            if (numRanges < 1)
            {
                throw new ArgumentOutOfRangeException($"{nameof(numRanges)} must be a positive integer.");
            }

            if (effectivePartitionKeyRange.Width < numRanges)
            {
                throw new ArgumentOutOfRangeException($"Can not split {effectivePartitionKeyRange} into {numRanges}, since {effectivePartitionKeyRange} is not wide enough.");
            }

            List<EffectivePartitionKeyRange> childrenRanges = new List<EffectivePartitionKeyRange>();
            UInt128 subrangeWidth = effectivePartitionKeyRange.Width / numRanges;
            for (int i = 0; i < numRanges - 1; i++)
            {
                EffectivePartitionKey offset = new EffectivePartitionKey(effectivePartitionKeyRange.Start.Value + (subrangeWidth * i));
                EffectivePartitionKey count = new EffectivePartitionKey(subrangeWidth);
                childrenRanges.Add(new EffectivePartitionKeyRange(offset, count));
            }

            // Last range will have remaining EPKs, since the range might not be divisible.
            {
                EffectivePartitionKey offset = new EffectivePartitionKey(effectivePartitionKeyRange.Start.Value + (subrangeWidth * (numRanges - 1)));
                EffectivePartitionKey count = new EffectivePartitionKey(effectivePartitionKeyRange.Width - effectivePartitionKeyRange.Start.Value);
                childrenRanges.Add(new EffectivePartitionKeyRange(offset, count));
            }

            return PartitionedSortedEffectiveRanges.Create(childrenRanges);
        }

        public EffectivePartitionKeyRange MergeRanges(PartitionedSortedEffectiveRanges partitionedSortedEffectiveRanges)
        {
            if (partitionedSortedEffectiveRanges == null)
            {
                throw new ArgumentNullException(nameof(partitionedSortedEffectiveRanges));
            }

            return new EffectivePartitionKeyRange(
                start: partitionedSortedEffectiveRanges.First().Start,
                end: partitionedSortedEffectiveRanges.Last().End);
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
