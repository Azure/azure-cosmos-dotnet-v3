//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class EffectivePartitionKeyRangeFactoryTests
    {
        [TestMethod]
        public void TestSplit()
        {
            {
                // Empty partition
                VerifySplit(
                    splitOutcome: EffectivePartitionKeyRangeFactory.SplitOutcome.RangeNotWideEnough,
                    range: CreateRange(0, 0));
            }

            {
                // Range Not Wide Enough
                VerifySplit(
                    splitOutcome: EffectivePartitionKeyRangeFactory.SplitOutcome.RangeNotWideEnough,
                    range: CreateRange(0, 1),
                    numRanges: 2);
            }

            {
                // Ranges Need to Be Positive
                VerifySplit(
                    splitOutcome: EffectivePartitionKeyRangeFactory.SplitOutcome.NumRangesNeedsToBePositive,
                    range: CreateRange(0, 2),
                    numRanges: 0);

                VerifySplit(
                    splitOutcome: EffectivePartitionKeyRangeFactory.SplitOutcome.NumRangesNeedsToBePositive,
                    range: CreateRange(0, 2),
                    numRanges: -1);
            }

            {
                // Success
                VerifySplit(
                    splitOutcome: EffectivePartitionKeyRangeFactory.SplitOutcome.Success,
                    range: CreateRange(0, 2),
                    numRanges: 2,
                    CreateRange(0, 1), CreateRange(1, 2));

                VerifySplit(
                    splitOutcome: EffectivePartitionKeyRangeFactory.SplitOutcome.Success,
                    range: CreateRange(0, 3),
                    numRanges: 2,
                    CreateRange(0, 1), CreateRange(1, 3));
            }

            void VerifySplit(
                EffectivePartitionKeyRangeFactory.SplitOutcome splitOutcome,
                EffectivePartitionKeyRange range,
                int numRanges = 1,
                params EffectivePartitionKeyRange[] splitRanges)
            {
                EffectivePartitionKeyRangeFactory.SplitOutcome actualSplitOutcome = EffectivePartitionKeyRangeFactory.TrySplitRange(
                    effectivePartitionKeyRange: range,
                    numRanges: numRanges,
                    splitRanges: out PartitionedSortedEffectiveRanges splitRangesActual);

                Assert.AreEqual(splitOutcome, actualSplitOutcome);
                if (splitOutcome == EffectivePartitionKeyRangeFactory.SplitOutcome.Success)
                {
                    Assert.AreEqual(numRanges, splitRangesActual.Count());
                    Assert.AreEqual(splitRanges.Count(), splitRangesActual.Count());

                    PartitionedSortedEffectiveRanges expectedSplitRanges = PartitionedSortedEffectiveRanges.Create(splitRanges);

                    IEnumerable<(EffectivePartitionKeyRange, EffectivePartitionKeyRange)> expectedAndActual = expectedSplitRanges.Zip(splitRangesActual, (first, second) => (first, second));
                    foreach ((EffectivePartitionKeyRange expected, EffectivePartitionKeyRange actual) in expectedAndActual)
                    {
                        Assert.AreEqual(expected, actual);
                    }
                }
            }
        }

        [TestMethod]
        public void TestMerge()
        {
            {
                // Single Range
                VerifyMerge(
                    expectedMergedRange: CreateRange(0, 1),
                    rangesToMerge: CreateRange(0, 1));
            }

            {
                // Basic Merge
                VerifyMerge(
                    expectedMergedRange: CreateRange(0, 2),
                    CreateRange(0, 1), CreateRange(1, 2));
            }

            {
                // Complex Merge
                VerifyMerge(
                    expectedMergedRange: CreateRange(1, 15),
                    CreateRange(1, 3), CreateRange(3, 9), CreateRange(9, 15) );
            }

            void VerifyMerge(
                EffectivePartitionKeyRange expectedMergedRange,
                params EffectivePartitionKeyRange[] rangesToMerge)
            {
                EffectivePartitionKeyRange actualMergedRange = EffectivePartitionKeyRangeFactory.MergeRanges(
                    PartitionedSortedEffectiveRanges.Create(rangesToMerge));
                Assert.AreEqual(expectedMergedRange, actualMergedRange);
            }
        }

        private static EffectivePartitionKeyRange CreateRange(UInt128 start, UInt128 end)
        {
            return new EffectivePartitionKeyRange(start: new EffectivePartitionKey(start), end: new EffectivePartitionKey(end));
        }
    }
}
