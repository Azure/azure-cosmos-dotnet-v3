//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using TraceLevel = Cosmos.Tracing.TraceLevel;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Unit Tests for <see cref="PartitionKeyRangeCache"/>.
    /// </summary>
    [TestClass]
    public class PartitionKeyRangeCacheTest
    {
        /// <summary>
        /// Gets and Sets the current <see cref="TestContext"/>.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Initializes the test class with the current test context.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
        }

        /// <summary>
        /// Test to validate that when a fresh container is created, TryGetOverlappingRangesAsync() should not
        /// add same key to the trace dictionary. The same key could be regenerated, if the CPU computation is
        /// fast enough to invoke TryLookupAsync within .5 to 15 ms timeframe, which may lead to get the exact
        /// same UTC DateTime, which is nothing but the trace key.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> object indicating the test is finished.
        /// </returns>
        [TestMethod]
        public async Task TryGetOverlappingRangesAsync_WithFreshContainer_ShouldNotAddSameTraceKey()
        {
            // Arrange.
            string eTag = "483";
            string authToken = "token!";
            string containerRId = "kjhsAA==";
            string singlePkCollectionCache = "{\"_rid\":\"3FIlAOzjvyg=\",\"PartitionKeyRanges\":[{\"_rid\":\"3FIlAOzjvygCAAAAAAAAUA==\",\"id\":\"0\",\"_etag\":\"\\\"00005565-0000-0800-0000-621fd98a0000\\\"\",\"minInclusive\":\"\",\"maxExclusive\":\"FF\",\"ridPrefix\":0,\"_self\":\"dbs/3FIlAA==/colls/3FIlAOzjvyg=/pkranges/3FIlAOzjvygCAAAAAAAAUA==/\",\"throughputFraction\":1,\"status\":\"splitting\",\"parents\":[],\"_ts\":1646254474,\"_lsn\":44}],\"_count\":1}";
            byte[] singlePkCollectionCacheByte = Encoding.UTF8.GetBytes(singlePkCollectionCache);
            ITrace trace = Trace.GetRootTrace(this.TestContext.TestName, TraceComponent.Unknown, TraceLevel.Info);

            Mock<IStoreModel> mockStoreModel = new();
            Mock<CollectionCache> mockCollectioNCache = new();
            Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider = new();
            NameValueCollectionWrapper headers = new()
            {
                [HttpConstants.HttpHeaders.ETag] = eTag,
            };

            mockStoreModel.SetupSequence(x => x.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new DocumentServiceResponse(new MemoryStream(singlePkCollectionCacheByte),
                        new StoreResponseNameValueCollection()
                        {
                            ETag = eTag,
                        },
                        HttpStatusCode.OK))
                 .ReturnsAsync(new DocumentServiceResponse(null, headers, HttpStatusCode.NotModified, null))
                 .ReturnsAsync(new DocumentServiceResponse(null, headers, HttpStatusCode.NotModified, null));

            mockTokenProvider.Setup(x => x.GetUserAuthorizationTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>(), It.IsAny<AuthorizationTokenType>(), It.IsAny<ITrace>()))
                .Returns(new ValueTask<string>(authToken));

            // Act.
            PartitionKeyRangeCache partitionKeyRangeCache = new (mockTokenProvider.Object, mockStoreModel.Object, mockCollectioNCache.Object);
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                containerRId,
                FeedRangeEpk.FullRange.Range,
                trace,
                forceRefresh: true);

            // Assert.
            string dataPath = "children[0].data", previousContinuationTokenKey = "['Previous Continuation Token']", diagnostics = new CosmosTraceDiagnostics(trace).ToString();
            JObject traceData = JsonConvert.DeserializeObject<JObject>(diagnostics);

            Assert.IsNotNull(traceData);

            string firstPkRangeCacheKeyName = ((JProperty)traceData.SelectToken(dataPath)?.First).Name;
            string secondPkRangeCacheKeyName = ((JProperty)traceData.SelectToken(dataPath)?.Last).Name;

            Assert.IsNotNull(firstPkRangeCacheKeyName);
            Assert.IsNotNull(secondPkRangeCacheKeyName);
            Assert.IsTrue(firstPkRangeCacheKeyName.Length > 0);
            Assert.IsTrue(secondPkRangeCacheKeyName.Length > 0);
            Assert.AreNotEqual(firstPkRangeCacheKeyName, secondPkRangeCacheKeyName);

            string firstPkRangeValue = ((JProperty)traceData.SelectToken(dataPath).First)?.Value?.SelectToken(previousContinuationTokenKey)?.ToString();
            string secondPkRangeValue = ((JProperty)traceData.SelectToken(dataPath).Last)?.Value?.SelectToken(previousContinuationTokenKey)?.ToString();

            Assert.IsNotNull(firstPkRangeValue);
            Assert.IsNotNull(secondPkRangeValue);
            Assert.IsTrue(firstPkRangeValue.Length == 0);
            Assert.AreEqual(eTag, secondPkRangeValue);

            trace.Dispose();
        }
    }
}
