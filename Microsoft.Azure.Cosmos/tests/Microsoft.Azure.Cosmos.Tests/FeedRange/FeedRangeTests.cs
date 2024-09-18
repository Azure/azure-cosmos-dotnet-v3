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
    using Newtonsoft.Json;
    using System.Text;
    using System.IO;
    using System.Net.Http;
    using Newtonsoft.Json.Linq;

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
        public void FeedRangePK_None_ToJsonFromJson()
        {
            FeedRangePartitionKey feedRange = new FeedRangePartitionKey(PartitionKey.None);
            string range = feedRange.ToJsonString();
            FeedRangePartitionKey other = Cosmos.FeedRange.FromJsonString(range) as FeedRangePartitionKey;
            Assert.AreEqual(feedRange.PartitionKey, other.PartitionKey);

            PartitionKey partitionKeyStringNone = new PartitionKey("None");
            FeedRangePartitionKey withNoneAsValue = new FeedRangePartitionKey(partitionKeyStringNone);
            string withNoneAsValueAsString = withNoneAsValue.ToJsonString();
            Assert.AreNotEqual(range, withNoneAsValueAsString);
            FeedRangePartitionKey withNoneAsValueAsStringDeserialized = Cosmos.FeedRange.FromJsonString(withNoneAsValueAsString) as FeedRangePartitionKey;
            Assert.AreNotEqual(feedRange.PartitionKey, withNoneAsValueAsStringDeserialized.PartitionKey);
            Assert.AreEqual(partitionKeyStringNone, withNoneAsValueAsStringDeserialized.PartitionKey);
        }

        [TestMethod]
        public void FeedRangePK_Null_ToJsonFromJson()
        {
            FeedRangePartitionKey feedRange = new FeedRangePartitionKey(PartitionKey.Null);
            string range = feedRange.ToJsonString();
            FeedRangePartitionKey other = Cosmos.FeedRange.FromJsonString(range) as FeedRangePartitionKey;
            Assert.AreEqual(feedRange.PartitionKey, other.PartitionKey);
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

        /// <summary>
        /// Upon failures in PartitionKeyRanges calls, the failure should be a CosmosException
        /// </summary>
        [TestMethod]
        public async Task GetFeedRangesThrowsCosmosException()
        {
            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            Uri endpoint = MockSetupsHelper.SetupSingleRegionAccount(
                "mockAccountInfo",
                consistencyLevel: ConsistencyLevel.Session,
                mockHttpHandler,
                out string primaryRegionEndpoint);

            string databaseName = "mockDbName";
            string containerName = "mockContainerName";
            string containerRid = "ccZ1ANCszwk=";
            Documents.ResourceId cRid = Documents.ResourceId.Parse(containerRid);
            MockSetupsHelper.SetupContainerProperties(
                mockHttpHandler: mockHttpHandler,
                regionEndpoint: primaryRegionEndpoint,
                databaseName: databaseName,
                containerName: containerName,
                containerRid: containerRid);

            // Return a 503 on PKRange call
            bool invokedPkRanges = false;
            Uri partitionKeyUri = new Uri($"{primaryRegionEndpoint}/dbs/{cRid.DatabaseId}/colls/{cRid.DocumentCollectionId}/pkranges");
            mockHttpHandler.Setup(x => x.SendAsync(It.Is<HttpRequestMessage>(x => x.RequestUri == partitionKeyUri), It.IsAny<CancellationToken>()))
              .Returns(() => Task.FromResult(new HttpResponseMessage()
              {
                  StatusCode = HttpStatusCode.ServiceUnavailable,
                  Content = new StringContent("ServiceUnavailable")
              }))
              .Callback(() => invokedPkRanges = true);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
            };

            using (CosmosClient customClient = new CosmosClient(
                   endpoint.ToString(),
                   Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                   cosmosClientOptions))
            {
                Container container = customClient.GetContainer(databaseName, containerName);
                CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(() => container.GetFeedRangesAsync(CancellationToken.None));
                Assert.AreEqual(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
                Assert.IsTrue(invokedPkRanges);
            }
        }

        /// <summary>
        /// RangeJsonConverter accepts only (minInclusive=True, maxInclusive=False) combination
        ///     In its serialization its not including minInclusive, maxInclusive combination 
        ///     but on deserialization setting them to (true, false
        ///     
        /// All other combinations should throw an exception
        /// </summary>
        [TestMethod]
        [DataRow(false, true)]
        [DataRow(false, false)]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [Owner("kirankk")]
        public void FeedRangeEpk_SerializationValidation(bool minInclusive, bool maxInclusive)
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("", "FF", minInclusive, maxInclusive);
            RangeJsonConverter rangeConverter = new RangeJsonConverter();

            using StringWriter sw = new StringWriter();
            using JsonWriter writer = new JsonTextWriter(sw);
            {
                JsonSerializer jsonSerializer = new JsonSerializer();
                rangeConverter.WriteJson(writer, range, jsonSerializer);
                writer.Flush();
                sw.Flush();

                JObject parsedJson = JObject.Parse(sw.ToString());
                Assert.AreEqual(true, parsedJson.ContainsKey("min"));
                Assert.AreEqual(string.Empty, parsedJson["min"]);
                Assert.AreEqual(true, parsedJson.ContainsKey("max"));
                Assert.AreEqual("FF", parsedJson["max"]);
                Assert.AreEqual(!minInclusive, parsedJson.ContainsKey("isMinInclusive"));
                Assert.AreEqual(maxInclusive, parsedJson.ContainsKey("isMaxInclusive"));
                if (!minInclusive)
                {
                    Assert.AreEqual(false, parsedJson["isMinInclusive"]);
                }

                if (maxInclusive)
                {
                    Assert.AreEqual(true, parsedJson["isMaxInclusive"]);
                }
            }
        }

        [TestMethod]
        [DataRow(false, true)]
        [DataRow(false, false)]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [Owner("kirankk")]
        public void FeedRangeEpk_SerdeValdation(bool minInclusive, bool maxInclusive)
        {
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("", "FF", minInclusive, maxInclusive);
            RangeJsonConverter rangeConverter = new RangeJsonConverter();

            using StringWriter sw = new StringWriter();
            using JsonWriter writer = new JsonTextWriter(sw);
            {
                JsonSerializer jsonSerializer = new JsonSerializer();

                rangeConverter.WriteJson(writer, range, jsonSerializer);

                string serializedJson = sw.ToString();
                System.Diagnostics.Trace.TraceInformation(serializedJson);

                using TextReader reader = new StringReader(serializedJson);
                using JsonReader jsonReader = new JsonTextReader(reader);
                Documents.Routing.Range<string> rangeDeserialized = (Documents.Routing.Range<string>)rangeConverter.ReadJson(jsonReader, typeof(Documents.Routing.Range<string>), null, jsonSerializer);
                Assert.IsTrue(range.Equals(rangeDeserialized), serializedJson);
            }
        }

        [TestMethod]
        [Owner("kirankk")]
        public void FeedRangeEpk_BackwardComptibility()
        {
            string testJson = @"{""min"":"""",""max"":""FF""}";
            System.Diagnostics.Trace.TraceInformation(testJson);
            RangeJsonConverter rangeConverter = new RangeJsonConverter();

            using TextReader reader = new StringReader(testJson);
            using JsonReader jsonReader = new JsonTextReader(reader);
            Documents.Routing.Range<string> rangeDeserialized = (Documents.Routing.Range<string>)rangeConverter.ReadJson(jsonReader, typeof(Documents.Routing.Range<string>), null, new JsonSerializer());

            Assert.IsTrue(rangeDeserialized.IsMinInclusive);
            Assert.IsFalse(rangeDeserialized.IsMaxInclusive);
        }
    }
}
