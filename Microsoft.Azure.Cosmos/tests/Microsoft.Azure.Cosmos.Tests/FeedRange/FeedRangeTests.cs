//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Routing;
    using Moq;
    using Microsoft.Azure.Cosmos.Tracing;

    [TestClass]
    public class FeedRangeTests
    {
        [TestMethod]
        public void FeedRangeEPK_Range()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEpk feedRangeEPK = new FeedRangeEpk(range);
            Assert.AreEqual(range, feedRangeEPK.Range);
        }

        [TestMethod]
        public void FeedRangePK_PK()
        {
            PartitionKey partitionKey = new PartitionKey("test");
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            Assert.AreEqual(partitionKey, feedRangePartitionKey.PartitionKey);
        }

        [TestMethod]
        public void FeedRangePKRangeId_PKRange()
        {
            string pkRangeId = Guid.NewGuid().ToString();
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(pkRangeId);
            Assert.AreEqual(pkRangeId, feedRangePartitionKeyRange.PartitionKeyRangeId);
        }

        [TestMethod]
        public async Task FeedRangeEPK_GetEffectiveRangesAsync()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEpk FeedRangeEpk = new FeedRangeEpk(range);
            List<Documents.Routing.Range<string>> ranges = await FeedRangeEpk.GetEffectiveRangesAsync(Mock.Of<IRoutingMapProvider>(), null, null, NoOpTrace.Singleton);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(range, ranges[0]);
        }

        [TestMethod]
        public async Task FeedRangePK_GetEffectiveRangesAsync()
        {
            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/id");
            PartitionKey partitionKey = new PartitionKey("test");
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            Documents.Routing.Range<string> range = Documents.Routing.Range<string>.GetPointRange(partitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition));
            List<Documents.Routing.Range<string>> ranges = await feedRangePartitionKey.GetEffectiveRangesAsync(Mock.Of<IRoutingMapProvider>(), null, partitionKeyDefinition, NoOpTrace.Singleton);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(range, ranges[0]);
        }

        [TestMethod]
        public async Task FeedRangePKRangeId_GetEffectiveRangesAsync()
        {
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = "AA", MaxExclusive = "BB" };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            IRoutingMapProvider routingProvider = Mock.Of<IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .Setup(f => f.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.Is<string>(s => s == partitionKeyRange.Id), It.IsAny<ITrace>(), It.IsAny<bool>()))
                .ReturnsAsync(partitionKeyRange);
            List<Documents.Routing.Range<string>> ranges = await feedRangePartitionKeyRange.GetEffectiveRangesAsync(routingProvider, null, null, NoOpTrace.Singleton);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(partitionKeyRange.ToRange().Min, ranges[0].Min);
            Assert.AreEqual(partitionKeyRange.ToRange().Max, ranges[0].Max);
            Mock.Get(routingProvider)
                .Verify(f => f.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.Is<string>(s => s == partitionKeyRange.Id), It.IsAny<ITrace>(), It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public async Task FeedRangePKRangeId_GetEffectiveRangesAsync_Refresh()
        {
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = "AA", MaxExclusive = "BB" };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            IRoutingMapProvider routingProvider = Mock.Of<IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .SetupSequence(f => f.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.Is<string>(s => s == partitionKeyRange.Id), It.IsAny<ITrace>(), It.IsAny<bool>()))
                .ReturnsAsync(null)
                .ReturnsAsync(partitionKeyRange);
            List<Documents.Routing.Range<string>> ranges = await feedRangePartitionKeyRange.GetEffectiveRangesAsync(routingProvider, null, null, NoOpTrace.Singleton);
            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(partitionKeyRange.ToRange().Min, ranges[0].Min);
            Assert.AreEqual(partitionKeyRange.ToRange().Max, ranges[0].Max);
            Mock.Get(routingProvider)
                .Verify(f => f.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.Is<string>(s => s == partitionKeyRange.Id), It.IsAny<ITrace>(), It.IsAny<bool>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task FeedRangePKRangeId_GetEffectiveRangesAsync_Null()
        {
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = "AA", MaxExclusive = "BB" };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            IRoutingMapProvider routingProvider = Mock.Of<IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .SetupSequence(f => f.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.Is<bool>(b => true)))
                .ReturnsAsync((Documents.PartitionKeyRange)null)
                .ReturnsAsync((Documents.PartitionKeyRange)null);
            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => feedRangePartitionKeyRange.GetEffectiveRangesAsync(routingProvider, null, null, NoOpTrace.Singleton));
            Assert.AreEqual(HttpStatusCode.Gone, exception.StatusCode);
            Assert.AreEqual((int)Documents.SubStatusCodes.PartitionKeyRangeGone, exception.SubStatusCode);
        }

        [TestMethod]
        public async Task FeedRangeEPK_GetPartitionKeyRangesAsync()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = range.Min, MaxExclusive = range.Max };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            IRoutingMapProvider routingProvider = Mock.Of<IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .Setup(f => f.TryGetOverlappingRangesAsync(It.IsAny<string>(), It.Is<Documents.Routing.Range<string>>(s => s == range), It.IsAny<ITrace>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<Documents.PartitionKeyRange>() { partitionKeyRange });

            FeedRangeEpk FeedRangeEpk = new FeedRangeEpk(range);
            IEnumerable<string> pkRanges = await FeedRangeEpk.GetPartitionKeyRangesAsync(routingProvider, null, null, default, NoOpTrace.Singleton);
            Assert.AreEqual(1, pkRanges.Count());
            Assert.AreEqual(partitionKeyRange.Id, pkRanges.First());
        }

        [TestMethod]
        public async Task FeedRangePK_GetPartitionKeyRangesAsync()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = range.Min, MaxExclusive = range.Max };
            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/id");
            PartitionKey partitionKey = new PartitionKey("test");
            IRoutingMapProvider routingProvider = Mock.Of<IRoutingMapProvider>();
            Mock.Get(routingProvider)
                .Setup(f => f.TryGetOverlappingRangesAsync(It.IsAny<string>(), It.IsAny<Documents.Routing.Range<string>>(), It.IsAny<ITrace>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<Documents.PartitionKeyRange>() { partitionKeyRange });

            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            IEnumerable<string> pkRanges = await feedRangePartitionKey.GetPartitionKeyRangesAsync(routingProvider, null, partitionKeyDefinition, default, NoOpTrace.Singleton);
            Assert.AreEqual(1, pkRanges.Count());
            Assert.AreEqual(partitionKeyRange.Id, pkRanges.First());
        }

        [TestMethod]
        public async Task FeedRangePKRangeId_GetPartitionKeyRangesAsync()
        {
            Documents.PartitionKeyRange partitionKeyRange = new Documents.PartitionKeyRange() { Id = Guid.NewGuid().ToString(), MinInclusive = "AA", MaxExclusive = "BB" };
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(partitionKeyRange.Id);
            IEnumerable<string> pkRanges = await feedRangePartitionKeyRange.GetPartitionKeyRangesAsync(Mock.Of<IRoutingMapProvider>(), null, null, default, NoOpTrace.Singleton);
            Assert.AreEqual(1, pkRanges.Count());
            Assert.AreEqual(partitionKeyRange.Id, pkRanges.First());
        }

        [TestMethod]
        public void FeedRangeEPK_ToJsonFromJson()
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEpk feedRangeEpk = new FeedRangeEpk(range);
            string representation = feedRangeEpk.ToJsonString();
            FeedRangeEpk feedRangeEPKDeserialized = Cosmos.FeedRange.FromJsonString(representation) as FeedRangeEpk;
            Assert.IsNotNull(feedRangeEPKDeserialized);
            Assert.AreEqual(feedRangeEpk.Range.Min, feedRangeEPKDeserialized.Range.Min);
            Assert.AreEqual(feedRangeEpk.Range.Max, feedRangeEPKDeserialized.Range.Max);
        }

        [TestMethod]
        public void FeedRangePK_ToJsonFromJson()
        {
            PartitionKey partitionKey = new PartitionKey("test");
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            string representation = feedRangePartitionKey.ToJsonString();
            FeedRangePartitionKey feedRangePartitionKeyDeserialized = Cosmos.FeedRange.FromJsonString(representation) as FeedRangePartitionKey;
            Assert.IsNotNull(feedRangePartitionKeyDeserialized);
            Assert.AreEqual(feedRangePartitionKey.PartitionKey.ToJsonString(), feedRangePartitionKeyDeserialized.PartitionKey.ToJsonString());
        }

        [TestMethod]
        public void FeedRangePKRangeId_ToJsonFromJson()
        {
            string pkRangeId = Guid.NewGuid().ToString();
            FeedRangePartitionKeyRange feedRangePartitionKeyRange = new FeedRangePartitionKeyRange(pkRangeId);
            string representation = feedRangePartitionKeyRange.ToJsonString();
            FeedRangePartitionKeyRange feedRangePartitionKeyRangeDeserialized = Cosmos.FeedRange.FromJsonString(representation) as FeedRangePartitionKeyRange;
            Assert.IsNotNull(feedRangePartitionKeyRangeDeserialized);
            Assert.AreEqual(feedRangePartitionKeyRange.PartitionKeyRangeId, feedRangePartitionKeyRangeDeserialized.PartitionKeyRangeId);
        }
    }
}
