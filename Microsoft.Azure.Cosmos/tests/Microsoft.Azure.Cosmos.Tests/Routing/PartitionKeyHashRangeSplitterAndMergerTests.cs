//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class PartitionKeyHashRangeSplitterAndMergerTests
    {
        [TestMethod]
        public void TestSplit()
        {
            {
                // Empty partition
                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.RangeNotWideEnough,
                    range: CreateRange(0, 0));
            }

            {
                // Range Not Wide Enough
                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.RangeNotWideEnough,
                    range: CreateRange(0, 1),
                    numRanges: 2);
            }

            {
                // Ranges Need to Be Positive
                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.NumRangesNeedsToBeGreaterThanZero,
                    range: CreateRange(0, 2),
                    numRanges: 0);

                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.NumRangesNeedsToBeGreaterThanZero,
                    range: CreateRange(0, 2),
                    numRanges: -1);
            }

            {
                // Success
                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.Success,
                    range: CreateRange(0, 2),
                    numRanges: 2,
                    CreateRange(0, 1), CreateRange(1, 2));

                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.Success,
                    range: CreateRange(0, 3),
                    numRanges: 2,
                    CreateRange(0, 1), CreateRange(1, 3));

                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.Success,
                    range: CreateRange(0, 3),
                    numRanges: 1,
                    CreateRange(0, 3));
            }

            {
                // Split with open ranges
                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.Success,
                    range: CreateRange(0, null),
                    numRanges: 2,
                    CreateRange(0, UInt128.MaxValue / 2), CreateRange(UInt128.MaxValue / 2, null));

                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.Success,
                    range: CreateRange(null, 4),
                    numRanges: 2,
                    CreateRange(null, 2), CreateRange(2, 4));

                VerifySplit(
                    splitOutcome: PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.Success,
                    range: CreateRange(null, null),
                    numRanges: 2,
                    CreateRange(null, UInt128.MaxValue / 2), CreateRange(UInt128.MaxValue / 2, null));
            }

            static void VerifySplit(
                PartitionKeyHashRangeSplitterAndMerger.SplitOutcome splitOutcome,
                PartitionKeyHashRange range,
                int numRanges = 1,
                params PartitionKeyHashRange[] splitRanges)
            {
                PartitionKeyHashRangeSplitterAndMerger.SplitOutcome actualSplitOutcome = PartitionKeyHashRangeSplitterAndMerger.TrySplitRange(
                    partitionKeyHashRange: range,
                    rangeCount: numRanges,
                    splitRanges: out PartitionKeyHashRanges splitRangesActual);

                Assert.AreEqual(splitOutcome, actualSplitOutcome);
                if (splitOutcome == PartitionKeyHashRangeSplitterAndMerger.SplitOutcome.Success)
                {
                    Assert.AreEqual(numRanges, splitRangesActual.Count());
                    Assert.AreEqual(splitRanges.Count(), splitRangesActual.Count());

                    PartitionKeyHashRanges expectedSplitRanges = PartitionKeyHashRanges.Create(splitRanges);

                    IEnumerable<(PartitionKeyHashRange, PartitionKeyHashRange)> expectedAndActual = expectedSplitRanges.Zip(splitRangesActual, (first, second) => (first, second));
                    foreach ((PartitionKeyHashRange expected, PartitionKeyHashRange actual) in expectedAndActual)
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
                    CreateRange(1, 3), CreateRange(3, 9), CreateRange(9, 15));
            }

            {
                // Merges with open ranges
                VerifyMerge(
                    expectedMergedRange: CreateRange(1, null),
                    CreateRange(1, 3), CreateRange(3, 9), CreateRange(9, null));

                VerifyMerge(
                    expectedMergedRange: CreateRange(null, 15),
                    CreateRange(null, 3), CreateRange(3, 9), CreateRange(9, 15));

                VerifyMerge(
                    expectedMergedRange: CreateRange(null, null),
                    CreateRange(null, 3), CreateRange(3, 9), CreateRange(9, null));
            }

            static void VerifyMerge(
                PartitionKeyHashRange expectedMergedRange,
                params PartitionKeyHashRange[] rangesToMerge)
            {
                PartitionKeyHashRange actualMergedRange = PartitionKeyHashRangeSplitterAndMerger.MergeRanges(
                    PartitionKeyHashRanges.Create(rangesToMerge));
                Assert.AreEqual(expectedMergedRange, actualMergedRange);
            }
        }

        private static PartitionKeyHashRange CreateRange(UInt128? start, UInt128? end)
        {
            return new PartitionKeyHashRange(
                startInclusive: start.HasValue ? (PartitionKeyHash?)new PartitionKeyHash(start.Value) : null,
                endExclusive: end.HasValue ? (PartitionKeyHash?)new PartitionKeyHash(end.Value) : null);
        }
    }
}