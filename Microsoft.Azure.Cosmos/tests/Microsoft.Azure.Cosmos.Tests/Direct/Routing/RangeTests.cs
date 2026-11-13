//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="Range"/> class.
    /// </summary>
    [TestClass]
    public class RangeTests
    {

        [TestMethod]
        public void LengthAwareMinComparer_OrdersRanges_Correctly()
        {
            Range<string> range1 = new Range<string>("06AB34CFE4E482236BCACBBF50E234AD", "06AB34CFE4E482236BCACBBF50E234ABFF", true, false);
            Range<string> range2 = new Range<string>("06AB34CFE4E482236BCACBBF50E234ADFF", "06AB34CFE4E482236BCACBBF50E234ABFFFF", true, false);
            Range<string> range3 = new Range<string>("06AB34CFE4E482236BCACBBF50E234AB", "06AB34CFE4E482236BCACBBF50E234ABFFFF", true, false);

            List<Range<string>> ranges = new List<Range<string>> { range2, range3, range1 };
            ranges.Sort(Range<string>.LengthAwareMinComparer.Instance);

            Assert.IsTrue(ranges[0].Equals(range3));
            Assert.IsTrue(ranges[1].Equals(range1));
            Assert.IsTrue(ranges[2].Equals(range2));


            range1 = new Range<string>("06AB34CFE4E482236BCACBBF50E234AB0000000", "06AB34CFE4E482236BCACBBF50E234ABFF", true, false);
            range2 = new Range<string>("06AB34CFE4E482236BCACBBF50E234ABFF", "06AB34CFE4E482236BCACBBF50E234ABFFFF", true, false);

            ranges = new List<Range<string>> { range2, range1 };
            Assert.IsTrue(ranges[0].Equals(range2));
            ranges.Sort(Range<string>.LengthAwareMinComparer.Instance);

            Assert.IsTrue(ranges[0].Equals(range1));
            Assert.IsTrue(ranges[1].Equals(range2));

            range1 = new Range<string>(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, "06AB34CFE4E482236BCACBBF50E234ABFF", true, false);
            range2 = new Range<string>("06AB34CFE4E482236BCACBBF50E234ABFF", "06AB34CFE4E482236BCACBBF50E234ABFFFF", true, false);
            ranges = new List<Range<string>> { range2, range1 };
            ranges.Sort(Range<string>.LengthAwareMinComparer.Instance);

            Assert.IsTrue(ranges[0].Equals(range1), "Range with minimum inclusive effective partition key should come first in the sorted list.");
            Assert.IsTrue(ranges[1].Equals(range2), "Range with maximum exclusive effective partition key should come second in the sorted list.");


            range1 = new Range<string>("06AB34CFE4E482236BCACBBF50E234ABFF", "06AB34CFE4E482236BCACBBF50E234ABFF", true, false);
            range2 = new Range<string>(PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, "06AB34CFE4E482236BCACBBF50E234ABFFFF", true, false);
            ranges = new List<Range<string>> { range2, range1 };
            ranges.Sort(Range<string>.LengthAwareMinComparer.Instance);

            Assert.IsTrue(ranges[0].Equals(range1), "Range with minimum inclusive effective partition key should come first in the sorted list.");
            Assert.IsTrue(ranges[1].Equals(range2), "Range with maximum exclusive effective partition key should come second in the sorted list.");
        }

        [TestMethod]
        public void LengthAwareComparer_BinarySearch_WithTrailingZeros()
        {
            // Arrange: two ranges, one with MaxExclusive having trailing zeros
            //RangeB with id 0.
            Range<string> rangeA = new Range<string>(
                "0BD3FBE846AF75790CE63F78B1A81620",
                "0BD3FBE846AF75790CE63F78B1A8163100000000000000000000000000000000",
                true, false);

            //RangeB with id 1.
            Range<string> rangeB = new Range<string>(
                "0BD3FBE846AF75790CE63F78B1A8163100000000000000000000000000000000",
                "0BD3FBE846AF75790CE63F78B1A81631FF",
                true, false);

            List<Range<string>> testRanges = new List<Range<string>> { rangeA, rangeB };

            Range<string> rangeToBeSearched = new Range<string>(
                "0BD3FBE846AF75790CE63F78B1A81631", "0BD3FBE846AF75790CE63F78B1A81631FF", true, false);

            //Correct range overlapping function should yield rangeB from above list.
            int minIndex = testRanges.BinarySearch(rangeToBeSearched, Range<string>.LengthAwareMinComparer.Instance);
            Assert.AreEqual(1, minIndex, "The range with trailing zeros should be considered equal to the range without and minComparer should return the right range.");

            int maxIndex = testRanges.BinarySearch(rangeToBeSearched, Range<string>.LengthAwareMaxComparer.Instance);
            Assert.AreEqual(1, maxIndex, "The range with trailing zeros should be considered equal to the range without and maxComparer should return the right range.");

            //Verify that the pure string based Min and Max comparers fail to find the correct range i.e. they perform the non normalized string based comparison.
            minIndex = testRanges.BinarySearch(rangeToBeSearched, Range<string>.MinComparer.Instance);
            Assert.IsTrue(minIndex < 0);

            minIndex = Math.Max(0, (~minIndex) - 1);
            Assert.AreEqual(0, minIndex);

            maxIndex = testRanges.BinarySearch(rangeToBeSearched, Range<string>.MaxComparer.Instance);
            Assert.AreEqual(1, maxIndex);
        }


        [TestMethod]
        public void LengthAwareMaxComparer_OrdersRanges_Correctly()
        {
            Range<string> range1 = new Range<string>("0BD3FBE846AF75790CE63F78B1A81631", "0BD3FBE846AF75790CE63F78B1A81631FF", true, false);
            Range<string> range2 = new Range<string>("0BD3FBE846AF75790CE63F78B1A81631", "0BD3FBE846AF75790CE63F78B1A81631FFFF", true, false);
            Range<string> range3 = new Range<string>("0BD3FBE846AF75790CE63F78B1A81631", "0BD3FBE846AF75790CE63F78B1A816310000", true, false);

            List<Range<string>> ranges = new List<Range<string>> { range2, range3, range1 };
            ranges.Sort(Range<string>.LengthAwareMaxComparer.Instance);

            Assert.AreEqual(range3, ranges[0]);
            Assert.AreEqual(range1, ranges[1]);
            Assert.AreEqual(range2, ranges[2]);
        }

        [TestMethod]
        public void LengthAwareMinComparer_DistinguishesSignificantBytes()
        {
            Range<string> rangeA = new Range<string>("ABC", "ABCFF", true, false);
            Range<string> rangeB = new Range<string>("ABD", "ABDFF", true, false);

            int cmp = Range<string>.LengthAwareMinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp < 0, "ABC should be less than ABD in length aware string comparison.");

            cmp = Range<string>.MinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp < 0, "ABC should be less than ABD in ordinal string comparison.");

            rangeA = new Range<string>("ABC00", "ABCFF", true, false);
            rangeB = new Range<string>("ABC00000", "ABDFF", true, false);

            cmp = Range<string>.LengthAwareMinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp == 0, "ABC00 should be equal to ABC00000 in length aware string comparison.");

            cmp = Range<string>.MinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp < 0, "ABC00 should be unequal to ABC00000 in ordinal string comparison.");

            rangeA = new Range<string>("00ABC00", "ABCFF", true, false);
            rangeB = new Range<string>("0000ABC", "ABDFF", true, false);

            cmp = Range<string>.LengthAwareMinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp > 0, "00ABC should be equal to 0000ABC in length aware string comparison.");

            cmp = Range<string>.MinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp > 0, "00ABC should be unequal to 0000ABC in ordinal string comparison.");

            rangeA = new Range<string>("", "ABCFF", true, false);
            rangeB = new Range<string>("000030", "ABDFF", true, false);
            cmp = Range<string>.LengthAwareMinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp < 0, "Empty string should be less than 000030 in length aware string comparison.");


            rangeA = new Range<string>("00000FE846AF75790CE63F78B1A81621", "ABCFF", true, false);
            rangeB = new Range<string>("0BD3FBE846AF75790CE63F78B1A81621", "ABDFF", true, false);

            cmp = Range<string>.LengthAwareMinComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp < 0, "Trailing zeroes should not affect comparison outcome.");

        }

        [TestMethod]
        public void LengthAwareMaxComparer_DistinguishesSignificantBytes()
        {
            Range<string> rangeA = new Range<string>("DEF", "DEFFE", true, false);
            Range<string> rangeB = new Range<string>("DEF", "DEFFF", true, false);

            int cmp = Range<string>.LengthAwareMaxComparer.Instance.Compare(rangeA, rangeB);
            Assert.IsTrue(cmp < 0, "DEFFE should be less than DEFFF in length aware string comparison.");
        }

        [TestMethod]
        public void GetOverlappingRanges_UsesLengthAwareComparers_ForBinarySearch()
        {
            // Arrange: PartitionKeyRanges with leading zeros and empty string
            List<Range<string>> partitionKeyRanges = new List<Range<string>>
            {
                new Range<string>("", "0000000030", true, false), // Id 0
                new Range<string>("0000000030", "0000000050", true, false), // Id 1
                new Range<string>("0000000050", "0000000070", true, false), // Id 2
                new Range<string>("0000000070", "FF", true, false), // Id 3
            };

            // Provided ranges to search for overlaps
            List<Range<string>> providedRanges = new List<Range<string>>
            {
                new Range<string>("0000000040", "0000000040", true, true), // Should overlap with Id 1
                new Range<string>("0000000050", "0000000060", true, true), // Should overlap with Id 2
                new Range<string>("", "0000000030", true, false), // Should overlap with Id 0
                new Range<string>("0000000046", "0000000050", true, true), // Should overlap with Id 1 and Id 2 since maxInclusive is true
                new Range<string>("0000000071", "FF", true, false), // Should overlap with Id 3
            };

            // Act & Assert: Use LengthAwareMinComparer and LengthAwareMaxComparer for binary search
            foreach (Range<string> providedRange in providedRanges)
            {
                int minIndex = partitionKeyRanges.BinarySearch(providedRange, Range<string>.LengthAwareMinComparer.Instance);
                if (minIndex < 0)
                {
                    minIndex = Math.Max(0, (~minIndex) - 1);
                }

                int maxIndex = partitionKeyRanges.BinarySearch(providedRange, Range<string>.LengthAwareMaxComparer.Instance);
                if (maxIndex < 0)
                {
                    maxIndex = Math.Min(partitionKeyRanges.Count - 1, ~maxIndex);
                }

                bool foundOverlap = false;

                //Special case for multiple overlaps.
                if (providedRange.Min == "0000000046")
                {
                    Assert.AreEqual(1, maxIndex - minIndex, "Expected to find overlap starting at Id 1 and 2 for range 0000000046-0000000050");
                    Assert.AreEqual(1, minIndex, "Expected to find overlap starting at Id 1 for range 0000000046-0000000050");
                    Assert.AreEqual(2, maxIndex, "Expected to find overlap ending at Id 2 for range 0000000046-0000000050");
                }

                for (int i = minIndex; i <= maxIndex; ++i)
                {
                    if (Range<string>.CheckOverlapping(partitionKeyRanges[i], providedRange))
                    {
                        foundOverlap = true;
                        // Validate expected overlaps
                        if (providedRange.Min == "0000000040")
                            Assert.AreEqual("0000000030", partitionKeyRanges[i].Min, "Should overlap with Id 1");
                        if (providedRange.Min == "0000000050")
                            Assert.AreEqual("0000000050", partitionKeyRanges[i].Min, "Should overlap with Id 2");
                        if (providedRange.Min == "")
                            Assert.AreEqual("", partitionKeyRanges[i].Min, "Should overlap with Id 0");
                        if (providedRange.Min == "0000000071")
                            Assert.AreEqual("0000000070", partitionKeyRanges[i].Min, "Should overlap with Id 3");
                    }
                }
                Assert.IsTrue(foundOverlap, $"No overlap found for range {providedRange.Min}-{providedRange.Max}");
            }
        }
    }
}