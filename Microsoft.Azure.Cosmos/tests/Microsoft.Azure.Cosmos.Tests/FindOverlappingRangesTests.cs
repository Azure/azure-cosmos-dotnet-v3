//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class FindOverlappingRangesTests
    {
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("When the feed range has x overlaps for y feed ranges.")]
        [DataRow(1, 0)]
        [DataRow(2, 0)]
        [DataRow(2, 1)]
        [DataRow(3, 0)]
        [DataRow(3, 1)]
        [DataRow(3, 2)]
        [DataRow(10, 0)]
        [DataRow(10, 1)]
        [DataRow(10, 2)]
        [DataRow(10, 3)]
        [DataRow(10, 4)]
        [DataRow(10, 5)]
        [DataRow(10, 6)]
        [DataRow(10, 7)]
        [DataRow(10, 8)]
        [DataRow(10, 9)]
        public void GivenXFeedRangeAndYFeedRangesWhenTheFeedRangeOverlapsThenReturnAllOverlappingRangesTest(
            int numberOfRanges,
            int expectedOverlap)
        {
            ContainerInternal container = (ContainerInternal)MockCosmosUtil.CreateMockCosmosClient()
                .GetContainer(
                    databaseId: "TestDb",
                    containerId: "Test");

            IEnumerable<Cosmos.FeedRange> feedRanges = FindOverlappingRangesTests.CreateFeedRanges(
                minHexValue: "",
                maxHexValue: "FFFFFFFFFFFFFFF",
                numberOfRanges: numberOfRanges);

            Cosmos.FeedRange feedRange = FindOverlappingRangesTests.CreateFeedRangeThatOverlap(
                overlap: expectedOverlap,
                feedRanges: feedRanges.ToList());            

            Logger.LogLine($"{feedRange} -> {feedRange.ToJsonString()}");

            IReadOnlyList<Cosmos.FeedRange> overlappingRanges = container.FindOverlappingRanges(
                feedRange: feedRange,
                feedRanges: feedRanges.ToList());

            Assert.IsNotNull(overlappingRanges);
            Logger.LogLine($"{nameof(overlappingRanges)} -> {JsonConvert.SerializeObject(overlappingRanges)}");

            int actualOverlap = overlappingRanges.Count;

            Assert.AreEqual(
                expected: expectedOverlap + 1, 
                actual: actualOverlap,
                message: $"The given feedRange should have {expectedOverlap + 1} overlaps for {numberOfRanges}  feedRanges.");

            Assert.IsTrue(overlappingRanges.Contains(feedRanges.ElementAt(0)));
            Assert.IsTrue(overlappingRanges.Contains(feedRanges.ElementAt(0 + expectedOverlap)));
        }

        private static Cosmos.FeedRange CreateFeedRangeThatOverlap(int overlap, IReadOnlyList<Cosmos.FeedRange> feedRanges)
        {
            string min = ((FeedRangeEpk)feedRanges[0]).Range.Min;
            string max = ((FeedRangeEpk)feedRanges[0 + overlap]).Range.Max;

            Cosmos.FeedRange feedRange = FindOverlappingRangesTests.CreateFeedRange(
                min: min,
                max: max);

            return feedRange;
        }

        private static Cosmos.FeedRange CreateFeedRange(string min, string max)
        {
            if (min == "0")
            {
                min = "";
            }

            Documents.Routing.Range<string> range = new (
                min: min,
                max: max,
                isMinInclusive: true,
                isMaxInclusive: false);

            FeedRangeEpk feedRangeEpk = new (range);

            return Cosmos.FeedRange.FromJsonString(feedRangeEpk.ToJsonString());
        }

        private static IEnumerable<Cosmos.FeedRange> CreateFeedRanges(
            string minHexValue,
            string maxHexValue,
            int numberOfRanges = 10)
        {
            if (minHexValue == string.Empty)
            {
                minHexValue = "0";
            }

            // Convert hex strings to ulong
            ulong minValue = ulong.Parse(minHexValue, System.Globalization.NumberStyles.HexNumber);
            ulong maxValue = ulong.Parse(maxHexValue, System.Globalization.NumberStyles.HexNumber);

            ulong range = maxValue - minValue + 1; // Include the upper boundary
            ulong stepSize = range / (ulong)numberOfRanges;

            // Generate the sub-ranges
            List<(string, string)> subRanges = new ();

            for (int i = 0; i < numberOfRanges; i++)
            {
                ulong splitMinValue = minValue + (stepSize * (ulong)i);
                ulong splitMaxValue = (i == numberOfRanges - 1) ? maxValue : splitMinValue + stepSize - 1;
                subRanges.Add((splitMinValue.ToString("X"), splitMaxValue.ToString("X")));
            }
                                    
            foreach ((string min, string max) in subRanges)
            {
                Logger.LogLine($"{min} - {max}");

                yield return FindOverlappingRangesTests.CreateFeedRange(
                    min: min,
                    max: max);
            }
        }
    }
}
