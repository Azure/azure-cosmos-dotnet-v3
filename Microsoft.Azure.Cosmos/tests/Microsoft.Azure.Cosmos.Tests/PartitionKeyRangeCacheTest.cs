//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using TraceLevel = Cosmos.Tracing.TraceLevel;

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
                Mock<CollectionCache> mockCollectioNCache = new(false);
                Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider = new();
                NameValueCollectionWrapper headers = new()
                {
                    [HttpConstants.HttpHeaders.ETag] = eTag,
                };

                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

                using GlobalEndpointManager endpointManager = new(mockDocumentClient.Object, new ConnectionPolicy());

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
                PartitionKeyRangeCache partitionKeyRangeCache = new(mockTokenProvider.Object, mockStoreModel.Object, mockCollectioNCache.Object, endpointManager, useLengthAwareRangeComparer: false, enableAsyncCacheExceptionNoSharing: false);
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
        /// Test to validate that when the gateway service is unavailable, the partition key range cache is able to retry
        /// the Get PK Range calls to different regions, based on the preferred locations count. Therefore, the cache keep
        /// retrying until the max attempt is exausted.
        /// </summary>
        [TestMethod]
        [DataRow(1, false, DisplayName = "Validates when the preferred location count is just one, the cache retries only once and fails.")]
        [DataRow(2, false, DisplayName = "Validates when the preferred location count is two, the cache retries twice and fails.")]
        [DataRow(3, true, DisplayName = "Validates when the preferred location count is three, the cache retries thrice and succeeds on the last attempt.")]
        public async Task TryGetOverlappingRangesAsync_WhenGatewayThrowsServiceUnavailable_ShouldMarkReadEndpointAsUnavailable2(int preferredLocationsCount, bool shouldSucceed)
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
                Mock<CollectionCache> mockCollectioNCache = new(false);
                Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider = new();
                NameValueCollectionWrapper headers = new()
                {
                    [HttpConstants.HttpHeaders.ETag] = eTag,
                };

                Uri serviceUri = new("https://foo");
                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(serviceUri);

                Mock<IGlobalEndpointManager> mockedEndpointManager = new Mock<IGlobalEndpointManager>();
                mockedEndpointManager
                    .Setup(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                    .Returns(serviceUri);

                mockedEndpointManager
                    .Setup(gem => gem.PreferredLocationCount)
                    .Returns(preferredLocationsCount);

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
                     .ThrowsAsync(CosmosExceptionFactory.CreateServiceUnavailableException(
                                            message: "Service is Unavailable.",
                                            headers: new Headers()
                                            {
                                                ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                SubStatusCode = SubStatusCodes.TransportGenerated503
                                            },
                                            trace: trace,
                                            innerException: null))
                     .ThrowsAsync(CosmosExceptionFactory.CreateServiceUnavailableException(
                                            message: "Service is Unavailable.",
                                            headers: new Headers()
                                            {
                                                ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                SubStatusCode = SubStatusCodes.TransportGenerated503
                                            },
                                            trace: trace,
                                            innerException: null))
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

                PartitionKeyRangeCache partitionKeyRangeCache = new(mockTokenProvider.Object, mockStoreModel.Object, mockCollectioNCache.Object, mockedEndpointManager.Object, useLengthAwareRangeComparer: false, enableAsyncCacheExceptionNoSharing: false);

                if (shouldSucceed)
                {
                    // Act.
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
                else
                {
                    // Act.
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
                }
            }
        }

        /// <summary>
        /// Regression bar for PR #5866 (cold-start callsite).
        ///
        /// <see cref="PartitionKeyRangeCache.GetRoutingMapForCollectionAsync"/> has two
        /// sister callsites that materialize a <see cref="CollectionRoutingMap"/>:
        ///   * <c>previousRoutingMap == null</c> (cold-start) calls <c>TryCreateCompleteRoutingMap(...)</c>.
        ///   * <c>previousRoutingMap != null</c> (refresh) calls <c>previousRoutingMap.TryCombine(...)</c>.
        ///
        /// PR #5551 wired the refresh callsite to <c>this.useLengthAwareRangeComparer</c>
        /// but left the cold-start callsite with a literal <c>false</c>, so the first
        /// routing map for every container in every <see cref="CosmosClient"/> was always
        /// built with the regular <c>Range&lt;string&gt;.MinComparer/MaxComparer</c>
        /// regardless of the env var or <see cref="CosmosClientOptions.UseLengthAwareRangeComparer"/>.
        /// This test pins the cold-start callsite to consume the configured value.
        /// </summary>
        [DataTestMethod]
        [DataRow(true, "LengthAwareMinComparer", "LengthAwareMaxComparer", DisplayName = "useLengthAwareRangeComparer=true bakes LengthAware comparers into cold-start CollectionRoutingMap")]
        [DataRow(false, "MinComparer", "MaxComparer", DisplayName = "useLengthAwareRangeComparer=false bakes regular comparers into cold-start CollectionRoutingMap")]
        [Owner("amudumba")]
        public async Task GetRoutingMapForCollectionAsync_ColdStart_PropagatesUseLengthAwareRangeComparerToCollectionRoutingMap(
            bool useLengthAwareRangeComparer,
            string expectedMinComparerTypeName,
            string expectedMaxComparerTypeName)
        {
            string eTag = "483";
            string authToken = "token!";
            string containerRId = "kjhsAA==";
            string singlePkCollectionCache = "{\"_rid\":\"3FIlAOzjvyg=\",\"PartitionKeyRanges\":[{\"_rid\":\"3FIlAOzjvygCAAAAAAAAUA==\",\"id\":\"0\",\"_etag\":\"\\\"00005565-0000-0800-0000-621fd98a0000\\\"\",\"minInclusive\":\"\",\"maxExclusive\":\"FF\",\"ridPrefix\":0,\"_self\":\"dbs/3FIlAA==/colls/3FIlAOzjvyg=/pkranges/3FIlAOzjvygCAAAAAAAAUA==/\",\"throughputFraction\":1,\"status\":\"splitting\",\"parents\":[],\"_ts\":1646254474,\"_lsn\":44}],\"_count\":1}";
            byte[] singlePkCollectionCacheByte = Encoding.UTF8.GetBytes(singlePkCollectionCache);

            using (ITrace trace = Trace.GetRootTrace(this.TestContext.TestName, TraceComponent.Unknown, TraceLevel.Info))
            {
                Mock<IStoreModel> mockStoreModel = new();
                Mock<CollectionCache> mockCollectionCache = new(false);
                Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider = new();
                NameValueCollectionWrapper headers = new()
                {
                    [HttpConstants.HttpHeaders.ETag] = eTag,
                };

                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

                using GlobalEndpointManager endpointManager = new(mockDocumentClient.Object, new ConnectionPolicy());

                mockStoreModel.SetupSequence(x => x.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new DocumentServiceResponse(new MemoryStream(singlePkCollectionCacheByte),
                            new StoreResponseNameValueCollection()
                            {
                                ETag = eTag,
                            },
                            HttpStatusCode.OK))
                     .ReturnsAsync(new DocumentServiceResponse(null, headers, HttpStatusCode.NotModified, null));

                mockTokenProvider.Setup(x => x.GetUserAuthorizationTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INameValueCollection>(), It.IsAny<AuthorizationTokenType>(), It.IsAny<ITrace>()))
                    .Returns(new ValueTask<string>(authToken));

                // Act 1: drive a cold-start lookup (previousRoutingMap == null branch in GetRoutingMapForCollectionAsync).
                PartitionKeyRangeCache partitionKeyRangeCache = new(
                    mockTokenProvider.Object,
                    mockStoreModel.Object,
                    mockCollectionCache.Object,
                    endpointManager,
                    useLengthAwareRangeComparer: useLengthAwareRangeComparer,
                    enableAsyncCacheExceptionNoSharing: false);

                IReadOnlyList<PartitionKeyRange> ranges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                    containerRId,
                    FeedRangeEpk.FullRange.Range,
                    trace,
                    forceRefresh: false);
                Assert.IsNotNull(ranges, "Cold-start lookup should return overlapping ranges.");

                // Act 2: re-read the cached map (cache hit, previousValue: null) so we can inspect the baked-in comparers.
                CollectionRoutingMap cachedMap = await partitionKeyRangeCache.TryLookupAsync(
                    collectionRid: containerRId,
                    previousValue: null,
                    request: null,
                    trace: trace);
                Assert.IsNotNull(cachedMap, "Cached CollectionRoutingMap should exist after cold-start lookup.");

                // Assert: the (MinComparer, MaxComparer) tuple baked into the CollectionRoutingMap
                // ctor reflects the useLengthAwareRangeComparer wired into PartitionKeyRangeCache.
                FieldInfo comparersField = typeof(CollectionRoutingMap).GetField(
                    "comparers",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(comparersField, "CollectionRoutingMap.comparers private field must exist.");

                object comparers = comparersField.GetValue(cachedMap);
                Type tupleType = comparers.GetType();

                object minComparer = tupleType.GetField("Item1").GetValue(comparers);
                object maxComparer = tupleType.GetField("Item2").GetValue(comparers);

                Assert.AreEqual(
                    expectedMinComparerTypeName,
                    minComparer.GetType().Name,
                    $"Cold-start routing map should bake {expectedMinComparerTypeName} when PartitionKeyRangeCache.useLengthAwareRangeComparer={useLengthAwareRangeComparer}.");
                Assert.AreEqual(
                    expectedMaxComparerTypeName,
                    maxComparer.GetType().Name,
                    $"Cold-start routing map should bake {expectedMaxComparerTypeName} when PartitionKeyRangeCache.useLengthAwareRangeComparer={useLengthAwareRangeComparer}.");
            }
        }

        /// <summary>
        /// Server served a different range than the client resolved => the partition moved, so the routing
        /// cache is force-refreshed for the served range exactly once.
        /// </summary>
        [TestMethod]
        public async Task RefreshRoutingCacheIfPartitionMoved_ServerServedDifferentRange_ForceRefreshesOnce()
        {
            Mock<PartitionKeyRangeCache> cache = CreateRefreshableCacheMock();

            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(
                cache.Object,
                collectionResourceId: "dbs/db/colls/coll",
                clientResolvedPartitionKeyRangeId: "0",
                serverServedPartitionKeyRangeId: "5",
                trace: NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync("dbs/db/colls/coll", "5", It.IsAny<ITrace>(), true),
                Times.Once);
        }

        /// <summary>
        /// Client and server ranges match (including case-insensitively) => the partition did not move, so no
        /// refresh is issued.
        /// </summary>
        [TestMethod]
        public async Task RefreshRoutingCacheIfPartitionMoved_SameRange_IsNoOp()
        {
            Mock<PartitionKeyRangeCache> cache = CreateRefreshableCacheMock();

            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(cache.Object, "coll", "A", "A", NoOpTrace.Singleton);
            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(cache.Object, "coll", "A", "a", NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()),
                Times.Never);
        }

        /// <summary>
        /// A null cache, an empty collection id, an empty client range id, or a whitespace-only server range id
        /// are all treated as absent inputs => no refresh and, importantly, no null-reference exception.
        /// </summary>
        [TestMethod]
        public async Task RefreshRoutingCacheIfPartitionMoved_MissingInputs_IsNoOp()
        {
            Mock<PartitionKeyRangeCache> cache = CreateRefreshableCacheMock();

            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(null, "coll", "0", "5", NoOpTrace.Singleton);
            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(cache.Object, string.Empty, "0", "5", NoOpTrace.Singleton);
            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(cache.Object, "coll", string.Empty, "5", NoOpTrace.Singleton);
            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(cache.Object, "coll", "0", "   ", NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()),
                Times.Never);
        }

        /// <summary>
        /// A refresh failure faults the returned task rather than being swallowed: the primitive returns the
        /// un-awaited refresh task, which is the contract the point-op path relies on to retry an idempotent read.
        /// </summary>
        [TestMethod]
        public async Task RefreshRoutingCacheIfPartitionMoved_RefreshFailure_FaultsReturnedTask()
        {
            Mock<PartitionKeyRangeCache> cache = new Mock<PartitionKeyRangeCache>(null, null, null, null, false, false, null);
            cache
                .Setup(c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("refresh boom"));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(
                    cache.Object, "coll", "0", "5", NoOpTrace.Singleton));

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync("coll", "5", It.IsAny<ITrace>(), true),
                Times.Once);
        }

        /// <summary>
        /// The primitive is stateless, so two moves to the same range each force-refresh — nothing collapses them.
        /// </summary>
        [TestMethod]
        public async Task RefreshRoutingCacheIfPartitionMoved_RepeatedSameRangeMovesRefreshEachTime()
        {
            Mock<PartitionKeyRangeCache> cache = CreateRefreshableCacheMock();

            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(cache.Object, "coll", "0", "5", NoOpTrace.Singleton);
            await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(cache.Object, "coll", "1", "5", NoOpTrace.Singleton);

            cache.Verify(
                c => c.TryGetPartitionKeyRangeByIdAsync("coll", "5", It.IsAny<ITrace>(), true),
                Times.Exactly(2));
        }

        private static Mock<PartitionKeyRangeCache> CreateRefreshableCacheMock()
        {
            Mock<PartitionKeyRangeCache> cache = new Mock<PartitionKeyRangeCache>(null, null, null, null, false, false, null);
            cache
                .Setup(c => c.TryGetPartitionKeyRangeByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<bool>()))
                .ReturnsAsync((PartitionKeyRange)null);
            return cache;
        }
    }
}