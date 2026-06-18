//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using static Microsoft.Azure.Cosmos.Routing.MetadataHedgingStrategy;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class PartitionKeyRangeCacheTests
    {
        private static readonly Uri PrimaryEndpoint = new Uri("https://acct-eastus.documents.azure.com/");
        private static readonly Uri HedgeEndpoint = new Uri("https://acct-westus.documents.azure.com/");
        private const string CollectionRid = "JsAtAA==";

        [TestMethod]
        [Owner("dkunda")]
        public async Task NullStrategy_UsesDirectStoreModelPath_SingleSendPerPage()
        {
            ConcurrentQueue<Uri> sentToEndpoints = new ConcurrentQueue<Uri>();
            Mock<IStoreModel> storeModel = BuildStoreModelMock(sentToEndpoints, primaryDelayMs: 0);

            PartitionKeyRangeCache cache = new PartitionKeyRangeCache(
                authorizationTokenProvider: BuildTokenProviderMock().Object,
                storeModel: storeModel.Object,
                collectionCache: null,
                endpointManager: BuildEndpointManagerMock().Object,
                useLengthAwareRangeComparer: false,
                enableAsyncCacheExceptionNoSharing: true,
                metadataHedgingStrategy: null);

            CollectionRoutingMap map = await cache.TryLookupAsync(
                collectionRid: CollectionRid,
                previousValue: null,
                request: null,
                trace: NoOpTrace.Singleton);

            Assert.IsNotNull(map);
            // 2 sends: page 1 (OK, body) + page 2 (NotModified). No hedge.
            Assert.AreEqual(2, sentToEndpoints.Count);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task WithStrategy_ColdStart_PrimarySlow_PageOneHedges()
        {
            ConcurrentQueue<Uri> sentToEndpoints = new ConcurrentQueue<Uri>();
            Mock<IStoreModel> storeModel = BuildStoreModelMock(sentToEndpoints, primaryDelayMs: 500);

            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            PartitionKeyRangeCache cache = new PartitionKeyRangeCache(
                authorizationTokenProvider: BuildTokenProviderMock().Object,
                storeModel: storeModel.Object,
                collectionCache: null,
                endpointManager: BuildEndpointManagerMock().Object,
                useLengthAwareRangeComparer: false,
                enableAsyncCacheExceptionNoSharing: true,
                metadataHedgingStrategy: strategy);

            CollectionRoutingMap map = await cache.TryLookupAsync(
                collectionRid: CollectionRid,
                previousValue: null,
                request: null,
                trace: NoOpTrace.Singleton);

            Assert.IsNotNull(map);

            // Page 1 fires hedge (2 sends). Page 2 (NotModified) pins to winner (1 send).
            // Total = 3.
            Assert.AreEqual(3, sentToEndpoints.Count);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task WithStrategy_ColdStart_PagesAfterFirst_PinToWinningEndpoint()
        {
            ConcurrentQueue<Uri> sentToEndpoints = new ConcurrentQueue<Uri>();
            Mock<IStoreModel> storeModel = BuildStoreModelMock(sentToEndpoints, primaryDelayMs: 500);

            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            PartitionKeyRangeCache cache = new PartitionKeyRangeCache(
                authorizationTokenProvider: BuildTokenProviderMock().Object,
                storeModel: storeModel.Object,
                collectionCache: null,
                endpointManager: BuildEndpointManagerMock().Object,
                useLengthAwareRangeComparer: false,
                enableAsyncCacheExceptionNoSharing: true,
                metadataHedgingStrategy: strategy);

            CollectionRoutingMap map = await cache.TryLookupAsync(
                collectionRid: CollectionRid,
                previousValue: null,
                request: null,
                trace: NoOpTrace.Singleton);

            Assert.IsNotNull(map);

            // The 3rd call (page 2, NotModified) MUST hit the hedge winner (West US),
            // not the primary (East US).
            Uri[] sequence = sentToEndpoints.ToArray();
            Assert.AreEqual(3, sequence.Length);
            Assert.AreEqual(HedgeEndpoint, sequence[2], "Page 2 did not pin to winning endpoint.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task WithStrategy_WarmRefresh_FastPrimary_SingleSendPerPage()
        {
            ConcurrentQueue<Uri> sentToEndpoints = new ConcurrentQueue<Uri>();
            Mock<IStoreModel> storeModel = BuildStoreModelMock(sentToEndpoints, primaryDelayMs: 500);

            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            PartitionKeyRangeCache cache = new PartitionKeyRangeCache(
                authorizationTokenProvider: BuildTokenProviderMock().Object,
                storeModel: storeModel.Object,
                collectionCache: null,
                endpointManager: BuildEndpointManagerMock().Object,
                useLengthAwareRangeComparer: false,
                enableAsyncCacheExceptionNoSharing: true,
                metadataHedgingStrategy: strategy);

            // Seed the cache (cold start). May hedge but we don't care here.
            CollectionRoutingMap initialMap = await cache.TryLookupAsync(
                collectionRid: CollectionRid,
                previousValue: null,
                request: null,
                trace: NoOpTrace.Singleton);

            int sendCountAfterSeed = sentToEndpoints.Count;

            // Force-refresh: previousValue != null → isColdStart=false. The refresh is
            // now hedge-ELIGIBLE (hedging is no longer gated on cold start), but the
            // refresh's first page carries the seeded continuation token, so the mock
            // returns NotModified immediately. The primary wins before the threshold,
            // so no hedge is dispatched → a single primary send.
            CollectionRoutingMap refreshed = await cache.TryLookupAsync(
                collectionRid: CollectionRid,
                previousValue: initialMap,
                request: null,
                trace: NoOpTrace.Singleton);

            Assert.IsNotNull(refreshed);
            Assert.AreEqual(sendCountAfterSeed + 1, sentToEndpoints.Count);

            // The refresh send went to the primary endpoint (primary won fast; no hedge).
            Uri[] sequence = sentToEndpoints.ToArray();
            Assert.AreEqual(PrimaryEndpoint, sequence[sequence.Length - 1]);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task WithStrategy_WarmRefresh_PrimarySlow_PageOneHedges()
        {
            // Core regression for the broadened scope on the PKRange path: a
            // steady-state refresh (previousValue != null → isColdStart=false) whose
            // first page is slow on the primary MUST dispatch a cross-region hedge.
            ConcurrentQueue<Uri> sentToEndpoints = new ConcurrentQueue<Uri>();
            Mock<IStoreModel> storeModel = BuildWarmRefreshHedgingStoreModelMock(sentToEndpoints, refreshPrimaryDelayMs: 500);

            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            PartitionKeyRangeCache cache = new PartitionKeyRangeCache(
                authorizationTokenProvider: BuildTokenProviderMock().Object,
                storeModel: storeModel.Object,
                collectionCache: null,
                endpointManager: BuildEndpointManagerMock().Object,
                useLengthAwareRangeComparer: false,
                enableAsyncCacheExceptionNoSharing: true,
                metadataHedgingStrategy: strategy);

            // Seed (cold start) — fast, single send to primary (continuation seeds "etag-1").
            CollectionRoutingMap initialMap = await cache.TryLookupAsync(
                collectionRid: CollectionRid,
                previousValue: null,
                request: null,
                trace: NoOpTrace.Singleton);

            int sendCountAfterSeed = sentToEndpoints.Count;
            Assert.AreEqual(2, sendCountAfterSeed, "cold-start seed is page 1 (OK) + page 2 (NotModified), both fast primary sends");

            // Warm refresh — first page (continuation "etag-1") is slow on primary, so the
            // hedge fires and wins; page 2 (continuation "etag-2") pins to the winner.
            CollectionRoutingMap refreshed = await cache.TryLookupAsync(
                collectionRid: CollectionRid,
                previousValue: initialMap,
                request: null,
                trace: NoOpTrace.Singleton);

            Assert.IsNotNull(refreshed);

            Uri[] sequence = sentToEndpoints.ToArray();
            int refreshSends = sequence.Length - sendCountAfterSeed;
            Assert.AreEqual(3, refreshSends, "warm refresh page 1 must hedge (2 sends) and page 2 pins to the winner (1 send)");
            Assert.AreEqual(HedgeEndpoint, sequence[sequence.Length - 1], "page 2 of the warm refresh must pin to the hedge winner");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static Mock<IStoreModel> BuildStoreModelMock(
            ConcurrentQueue<Uri> sentToEndpoints,
            int primaryDelayMs)
        {
            // Each TryLookupAsync issues a do/while change-feed loop that ends
            // on NotModified. The mock returns OK for any request without an
            // IfNoneMatch header (page 1), and NotModified for any request
            // with one (page 2+). Endpoint capture honors RouteToLocation set
            // by the cache before the inner call.
            ConcurrentDictionary<int, byte> sendIndexToDelayed = new ConcurrentDictionary<int, byte>();
            int sendIndex = 0;

            Mock<IStoreModel> mock = new Mock<IStoreModel>(MockBehavior.Strict);
            mock
                .Setup(s => s.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>(async (req, ct) =>
                {
                    int idx = Interlocked.Increment(ref sendIndex);
                    Uri target = req.RequestContext?.LocationEndpointToRoute ?? PrimaryEndpoint;
                    sentToEndpoints.Enqueue(target);

                    // The first request to the primary endpoint (no continuation token)
                    // stalls so the hedge has time to fire and win.
                    bool hasContinuation = !string.IsNullOrEmpty(req.Headers?[HttpConstants.HttpHeaders.IfNoneMatch]);
                    bool isPrimaryFirstPage = !hasContinuation && target.Equals(PrimaryEndpoint);
                    if (isPrimaryFirstPage && primaryDelayMs > 0)
                    {
                        await Task.Delay(primaryDelayMs, ct);
                    }

                    return hasContinuation
                        ? BuildNotModifiedResponse()
                        : BuildPartitionKeyRangeFeedResponse();
                });
            return mock;
        }

        private static MetadataHedgingStrategy BuildStrategy(TimeSpan? threshold = null)
        {
            return new MetadataHedgingStrategy(
                globalEndpointManager: BuildEndpointManagerMock().Object,
                isHedgingDisabledByGateway: () => false,
                isPpafEnabled: () => true,
                customerOptIn: true,
                threshold: threshold ?? TimeSpan.FromMilliseconds(50));
        }

        private static Mock<IGlobalEndpointManager> BuildEndpointManagerMock()
        {
            Mock<IGlobalEndpointManager> mock = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            ReadOnlyCollection<Uri> reads = new ReadOnlyCollection<Uri>(new[] { PrimaryEndpoint, HedgeEndpoint });
            mock.Setup(g => g.ReadEndpoints).Returns(reads);
            mock.Setup(g => g.PreferredLocationCount).Returns(2);
            mock.Setup(g => g.GetApplicableEndpoints(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>())).Returns(reads);
            mock.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(PrimaryEndpoint);
            mock.Setup(g => g.GetLocation(PrimaryEndpoint)).Returns("East US");
            mock.Setup(g => g.GetLocation(HedgeEndpoint)).Returns("West US");
            return mock;
        }

        private static Mock<ICosmosAuthorizationTokenProvider> BuildTokenProviderMock()
        {
            Mock<ICosmosAuthorizationTokenProvider> mock = new Mock<ICosmosAuthorizationTokenProvider>(MockBehavior.Loose);
            mock.Setup(t => t.GetUserAuthorizationTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<INameValueCollection>(),
                    It.IsAny<AuthorizationTokenType>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync("dummy-token");
            return mock;
        }

        private static DocumentServiceResponse BuildPartitionKeyRangeFeedResponse()
        {
            List<PartitionKeyRange> ranges = new List<PartitionKeyRange>
            {
                new PartitionKeyRange
                {
                    Id = "0",
                    MinInclusive = string.Empty,
                    MaxExclusive = "FF",
                    ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==",
                },
            };

            JObject body = new JObject
            {
                { "_rid", CollectionRid },
                { "_count", ranges.Count },
                { "PartitionKeyRanges", JArray.FromObject(ranges) },
            };

            byte[] bytes = Encoding.UTF8.GetBytes(body.ToString());
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            headers.ETag = "etag-1";
            return new DocumentServiceResponse(new MemoryStream(bytes), headers, HttpStatusCode.OK);
        }

        private static DocumentServiceResponse BuildNotModifiedResponse()
        {
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            headers.ETag = "etag-1";
            return new DocumentServiceResponse(Stream.Null, headers, HttpStatusCode.NotModified);
        }

        // Stateful mock that drives a warm (refresh) PKRange read through a hedge.
        // Tokens advance so the cold-start seed and the warm refresh use distinct
        // continuation values:
        //   (no continuation)  -> OK feed, ETag "etagA"  (seed page 1, fast)
        //   "etagA"            -> NotModified, ETag "etagB" (seed page 2 ends seed)
        //   "etagB"            -> OK feed, ETag "etagC"  (refresh page 1; SLOW on primary)
        //   "etagC"            -> NotModified, ETag "etagC" (refresh page 2 ends refresh)
        private static Mock<IStoreModel> BuildWarmRefreshHedgingStoreModelMock(
            ConcurrentQueue<Uri> sentToEndpoints,
            int refreshPrimaryDelayMs)
        {
            Mock<IStoreModel> mock = new Mock<IStoreModel>(MockBehavior.Strict);
            mock
                .Setup(s => s.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>(async (req, ct) =>
                {
                    Uri target = req.RequestContext?.LocationEndpointToRoute ?? PrimaryEndpoint;
                    sentToEndpoints.Enqueue(target);

                    string continuation = req.Headers?[HttpConstants.HttpHeaders.IfNoneMatch];

                    if (string.IsNullOrEmpty(continuation))
                    {
                        return BuildPartitionKeyRangeFeedResponse("etagA");
                    }

                    if (continuation == "etagA")
                    {
                        return BuildNotModifiedResponse("etagB");
                    }

                    if (continuation == "etagB")
                    {
                        // Refresh first page: stall the primary so the hedge fires and wins.
                        if (target.Equals(PrimaryEndpoint) && refreshPrimaryDelayMs > 0)
                        {
                            await Task.Delay(refreshPrimaryDelayMs, ct);
                        }

                        return BuildPartitionKeyRangeFeedResponse("etagC");
                    }

                    return BuildNotModifiedResponse("etagC");
                });
            return mock;
        }

        private static DocumentServiceResponse BuildPartitionKeyRangeFeedResponse(string etag)
        {
            List<PartitionKeyRange> ranges = new List<PartitionKeyRange>
            {
                new PartitionKeyRange
                {
                    Id = "0",
                    MinInclusive = string.Empty,
                    MaxExclusive = "FF",
                    ResourceId = "ccZ1ANCszwkDAAAAAAAAUA==",
                },
            };

            JObject body = new JObject
            {
                { "_rid", CollectionRid },
                { "_count", ranges.Count },
                { "PartitionKeyRanges", JArray.FromObject(ranges) },
            };

            byte[] bytes = Encoding.UTF8.GetBytes(body.ToString());
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            headers.ETag = etag;
            return new DocumentServiceResponse(new MemoryStream(bytes), headers, HttpStatusCode.OK);
        }

        private static DocumentServiceResponse BuildNotModifiedResponse(string etag)
        {
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            headers.ETag = etag;
            return new DocumentServiceResponse(Stream.Null, headers, HttpStatusCode.NotModified);
        }
    }
}
