//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class FindOverlappingRangesTests
    {
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("When a given feed range has x overlaps for y feed ranges.")]
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
        public void GivenAFeedRangeAndYFeedRangesWhenTheFeedRangeOverlapsThenReturnXOverlappingRangesTest(
            int numberOfRanges,
            int expectedOverlap)
        {
            ContainerInternal container = (ContainerInternal)MockCosmosUtil.CreateMockCosmosClient()
                .GetContainer(
                    databaseId: Guid.NewGuid().ToString(),
                    containerId: Guid.NewGuid().ToString());

            IEnumerable<Cosmos.FeedRange> feedRanges = FindOverlappingRangesTests.CreateFeedRanges(
                minHexValue: "",
                maxHexValue: "FF",
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

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Given a partition key and a X number of ranges, When checking to find which range the partition key belongs to, Then the" +
            " range is found at id Y.")]
        [DataRow(1, 1)]
        [DataRow(2, 1)]
        [DataRow(10, 1)]
        public async Task GivenAPartitionKeyAndYFeedRangesWhenTheFeedRangeOverlapsThenReturnXOverlappingRangesTestAsync(
            int numberOfRanges,
            int expectedRangeId)
        {
            ITrace trace = Trace.GetRootTrace("TestTrace");

            IEnumerable<Cosmos.FeedRange> feedRanges = FindOverlappingRangesTests.CreateFeedRanges(
                minHexValue: "",
                maxHexValue: "FF",
                numberOfRanges: numberOfRanges);

            List<Tuple<PartitionKeyRange, ServiceIdentity>> partitionKeyRanges = new (feedRanges.Count());

            for (int counter = 0; counter < feedRanges.Count(); counter++)
            {
                Documents.Routing.Range<string> range = ((FeedRangeEpk)feedRanges.ElementAt(counter)).Range;

                partitionKeyRanges.Add(
                    new Tuple<PartitionKeyRange, ServiceIdentity>(
                        new PartitionKeyRange
                        { 
                            Id = counter.ToString(),
                            MinInclusive = range.Min,
                            MaxExclusive = range.Max
                        },
                        (ServiceIdentity)null));
            }

            string containerId = Guid.NewGuid().ToString();
            ContainerInternal container = (ContainerInternal)MockCosmosUtil
                .CreateMockCosmosClient()
                .GetContainer(
                    databaseId: Guid.NewGuid().ToString(),
                    containerId: containerId);

            PartitionKeyDefinition partitionKeyDefinition = new();
            partitionKeyDefinition.Paths.Add("/pk");
            partitionKeyDefinition.Version = PartitionKeyDefinitionVersion.V1;
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("test");

            const string collectionRid = "DvZRAOvLgDM=";
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(collectionRid);
            containerProperties.Id = containerId;
            containerProperties.PartitionKey = partitionKeyDefinition;

            IReadOnlyList <Cosmos.FeedRange> overlappingRanges = await container.FindOverlappingRangesAsync(
                partitionKey: partitionKey,
                feedRanges: feedRanges.ToList());

            Assert.IsNotNull(overlappingRanges);
            Logger.LogLine($"{nameof(overlappingRanges)} -> {JsonConvert.SerializeObject(overlappingRanges)}");

            int actualOverlap = overlappingRanges.Count;

            Assert.AreEqual(
                expected: expectedRangeId,
                actual: actualOverlap,
                message: $"The given partition key should be at range {expectedRangeId}.");

            Assert.IsTrue(overlappingRanges.Contains(feedRanges.ElementAt(0)));
        }

        private Mock<CosmosClientContext> MockClientContext()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.OperationHelperAsync<object>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<object>>>(),
                It.IsAny<Func<object, OpenTelemetryAttributes>>(),
                It.IsAny<TraceComponent>(),
                It.IsAny<Microsoft.Azure.Cosmos.Tracing.TraceLevel>()))
               .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<object>>, Func<object, OpenTelemetryAttributes>, TraceComponent, Microsoft.Azure.Cosmos.Tracing.TraceLevel>(
                (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, comp, level) => func(NoOpTrace.Singleton));

            mockContext.Setup(x => x.Client).Returns(MockCosmosUtil.CreateMockCosmosClient());

            return mockContext;
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
            ulong splitMaxValue = default;

            for (int i = 0; i < numberOfRanges; i++)
            {
                ulong splitMinValue = splitMaxValue;
                splitMaxValue = (i == numberOfRanges - 1) ? maxValue : splitMinValue + stepSize - 1;
                subRanges.Add((splitMinValue.ToString("X"), splitMaxValue.ToString("X")));
            }
                
            List<Cosmos.FeedRange> rs = new List<Cosmos.FeedRange>();

            foreach ((string min, string max) in subRanges)
            {
                Logger.LogLine($"{min} - {max}");

                rs.Add(FindOverlappingRangesTests.CreateFeedRange(
                    min: min,
                    max: max));
            }

            return rs;
        }
    }
}
