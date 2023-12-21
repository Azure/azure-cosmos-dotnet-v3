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
    using System;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;

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
        /// Test to validate that when a fresh container is created, TryGetOverlappingRangesAsync() should not
        /// add same key to the trace dictionary. The same key could be regenerated, if the CPU computation is
        /// fast enough to invoke TryLookupAsync within .5 to 15 ms timeframe, which may lead to get the exact
        /// same UTC DateTime, which is nothing but the trace key. An example trace JSON with the PKRange Info
        /// nodes can be found below:
        ///
        ///    "name": "Try Get Overlapping Ranges",
        ///    "id": "9d28a8cb-9669-422b-9619-b91cbbf61dff",
        ///    "start time": "06:16:03:990",
        ///    "duration in milliseconds": 50.8808,
        ///    "data": {
        ///        "PKRangeCache Info(#2022-08-30T18:16:04.0364155Z)": {
        ///            "Previous Continuation Token": null,
        ///            "Continuation Token": "483"
        ///        },
        ///        "PKRangeCache Info(483#2022-08-30T18:16:04.0394322Z)": {
        ///            "Previous Continuation Token": "483",
        ///            "Continuation Token": "483"
        ///         }
        ///     },
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
            using (ITrace trace = Trace.GetRootTrace(this.TestContext.TestName, TraceComponent.Unknown, TraceLevel.Info))
            {
                Mock<IStoreModel> mockStoreModel = new();
                Mock<CollectionCache> mockCollectioNCache = new();
                Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider = new();
                NameValueCollectionWrapper headers = new()
                {
                    [HttpConstants.HttpHeaders.ETag] = eTag,
                };

                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

                using GlobalEndpointManager endpointManager = new (mockDocumentClient.Object, new ConnectionPolicy());

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
                PartitionKeyRangeCache partitionKeyRangeCache = new(mockTokenProvider.Object, mockStoreModel.Object, mockCollectioNCache.Object, endpointManager);
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

                Assert.IsTrue(!string.IsNullOrWhiteSpace(firstPkRangeCacheKeyName));
                Assert.IsTrue(!string.IsNullOrWhiteSpace(secondPkRangeCacheKeyName));
                Assert.AreNotEqual(firstPkRangeCacheKeyName, secondPkRangeCacheKeyName);

                string firstPkRangeValue = ((JProperty)traceData.SelectToken(dataPath).First)?.Value?.SelectToken(previousContinuationTokenKey)?.ToString();
                string secondPkRangeValue = ((JProperty)traceData.SelectToken(dataPath).Last)?.Value?.SelectToken(previousContinuationTokenKey)?.ToString();

                Assert.IsTrue(!string.IsNullOrWhiteSpace(secondPkRangeValue));
                Assert.IsTrue(string.IsNullOrEmpty(firstPkRangeValue));
                Assert.AreEqual(eTag, secondPkRangeValue);
            }
        }

        /// <summary>
        /// Test to validate that when the gateway service is unavailable, the partition key range cache is able to mark
        /// the service endpoint as unavailable for read, so that the retry policy could pick the next region for the Pk
        /// range calls.
        /// </summary>
        [TestMethod]
        public async Task TryGetOverlappingRangesAsync_WhenGatewayThrowsServiceUnavailable_ShouldMarkReadEndpointAsUnavailable()
        {
            // Arrange.
            string eTag = "483";
            string authToken = "token!";
            string containerRId = "kjhsAA==";
            string singlePkCollectionCache = "{\"_rid\":\"3FIlAOzjvyg=\",\"PartitionKeyRanges\":[{\"_rid\":\"3FIlAOzjvygCAAAAAAAAUA==\",\"id\":\"0\",\"_etag\":\"\\\"00005565-0000-0800-0000-621fd98a0000\\\"\",\"minInclusive\":\"\",\"maxExclusive\":\"FF\",\"ridPrefix\":0,\"_self\":\"dbs/3FIlAA==/colls/3FIlAOzjvyg=/pkranges/3FIlAOzjvygCAAAAAAAAUA==/\",\"throughputFraction\":1,\"status\":\"splitting\",\"parents\":[],\"_ts\":1646254474,\"_lsn\":44}],\"_count\":1}";
            byte[] singlePkCollectionCacheByte = Encoding.UTF8.GetBytes(singlePkCollectionCache);
            using (ITrace trace = Trace.GetRootTrace(this.TestContext.TestName, TraceComponent.Unknown, TraceLevel.Info))
            {
                Mock<IStoreModel> mockStoreModel = new();
                Mock<CollectionCache> mockCollectioNCache = new();
                Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider = new();
                NameValueCollectionWrapper headers = new()
                {
                    [HttpConstants.HttpHeaders.ETag] = eTag,
                };

                Uri serviceUri = new ("https://foo");
                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(serviceUri);

                Mock<IGlobalEndpointManager> mockedEndpointManager = new Mock<IGlobalEndpointManager>();
                mockedEndpointManager
                    .Setup(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                    .Returns(serviceUri);
                mockedEndpointManager
                    .Setup(gem => gem.MarkEndpointUnavailableForRead(serviceUri));

                mockStoreModel.SetupSequence(x => x.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(CosmosExceptionFactory.CreateServiceUnavailableException(
                                            message: "Service is Unavailable.",
                                            headers: new Headers()
                                            {
                                                ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                SubStatusCode = SubStatusCodes.TransportGenerated503
                                            },
                                            trace: trace,
                                            innerException: null))
                     .ReturnsAsync(new DocumentServiceResponse(null, headers, HttpStatusCode.NotModified, null))
                     .ReturnsAsync(new DocumentServiceResponse(null, headers, HttpStatusCode.NotModified, null));

                mockTokenProvider.Setup(x => x.GetUserAuthorizationTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>(), It.IsAny<AuthorizationTokenType>(), It.IsAny<ITrace>()))
                    .Returns(new ValueTask<string>(authToken));

                // Act.
                PartitionKeyRangeCache partitionKeyRangeCache = new(mockTokenProvider.Object, mockStoreModel.Object, mockCollectioNCache.Object, mockedEndpointManager.Object);
                CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                    containerRId,
                    FeedRangeEpk.FullRange.Range,
                    trace,
                    forceRefresh: true));

                // Assert.
                string diagnostics = new CosmosTraceDiagnostics(trace).ToString();
                JObject traceData = JsonConvert.DeserializeObject<JObject>(diagnostics);

                Assert.IsNotNull(cosmosException);
                Assert.IsNotNull(traceData);
                Assert.AreEqual(HttpStatusCode.ServiceUnavailable, cosmosException.StatusCode);
                Assert.AreEqual(SubStatusCodes.TransportGenerated503, cosmosException.Headers.SubStatusCode);

                mockedEndpointManager.Verify(em => em.MarkEndpointUnavailableForRead(It.IsAny<Uri>()), Times.Once);
            }
        }
    }
}
