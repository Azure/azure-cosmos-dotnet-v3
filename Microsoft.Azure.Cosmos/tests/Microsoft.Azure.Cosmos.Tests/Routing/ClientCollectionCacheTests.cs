//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using static Microsoft.Azure.Cosmos.Routing.MetadataHedgingStrategy;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class ClientCollectionCacheTests
    {
        private static readonly Uri PrimaryEndpoint = new Uri("https://acct-eastus.documents.azure.com/");
        private static readonly Uri HedgeEndpoint = new Uri("https://acct-westus.documents.azure.com/");

        // ---------------------------------------------------------------
        // CollectionCache factory invocation behavior
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public async Task ResolveByNameAsync_CacheHit_DoesNotInvokeFactory()
        {
            CapturingCollectionCache cache = new CapturingCollectionCache();

            // First read populates the cache.
            await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            // Second call (forceRefresh=false) should hit the cache.
            await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            Assert.AreEqual(1, cache.GetByNameCallCount);
        }

        // ---------------------------------------------------------------
        // ClientCollectionCache wiring to MetadataHedgingStrategy
        // ---------------------------------------------------------------

        [TestMethod]
        [Owner("dkunda")]
        public async Task ClientCollectionCache_NullStrategy_UsesDirectStoreModelPath()
        {
            int sendCount = 0;
            Mock<IStoreModel> storeModel = BuildStoreModelMock(_ => { sendCount++; return BuildOkContainerResponse(); });

            ClientCollectionCache cache = new ClientCollectionCache(
                sessionContainer: null,
                storeModel: storeModel.Object,
                tokenProvider: BuildTokenProviderMock().Object,
                retryPolicy: BuildRetryPolicyFactoryMock().Object,
                telemetryToServiceHelper: BuildDisabledTelemetryHelper(),
                enableAsyncCacheExceptionNoSharing: true,
                metadataHedgingStrategy: null,
                globalEndpointManager: null);

            ContainerProperties result = await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, sendCount);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ClientCollectionCache_WithStrategy_WarmRead_FastPrimary_SingleSend()
        {
            int sendCount = 0;
            Mock<IStoreModel> storeModel = BuildStoreModelMock(_ => { Interlocked.Increment(ref sendCount); return BuildOkContainerResponse(); });

            MetadataHedgingStrategy strategy = BuildStrategy();
            ClientCollectionCache cache = BuildClientCollectionCache(storeModel.Object, strategy);

            // forceRefresh=true is a warm (refresh) read. The warm read is eligible to
            // hedge, but the primary responds immediately and wins before the
            // threshold, so no hedge is dispatched → exactly one send.
            ContainerProperties result = await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: true,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, sendCount);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ClientCollectionCache_WithStrategy_WarmRead_PrimarySlow_HedgeFires_TwoSends()
        {
            // Core regression for the broadened scope: a forceRefresh (warm,
            // non-cold-start) collection read whose primary is slow MUST dispatch a
            // cross-region hedge — proving hedging is no longer limited to cold start.
            int sendCount = 0;
            Mock<IStoreModel> storeModel = new Mock<IStoreModel>(MockBehavior.Strict);
            storeModel
                .Setup(s => s.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>(async (req, ct) =>
                {
                    int callIndex = Interlocked.Increment(ref sendCount);
                    if (callIndex == 1)
                    {
                        // Primary stalls long enough for the hedge to fire.
                        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    }
                    return BuildOkContainerResponse();
                });

            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            ClientCollectionCache cache = BuildClientCollectionCache(storeModel.Object, strategy);

            ContainerProperties result = await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: true,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, sendCount, "a slow-primary warm refresh read must fire exactly one cross-region hedge");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ClientCollectionCache_WithStrategy_WarmCacheHit_DoesNotHedgeOrResend()
        {
            int sendCount = 0;
            Mock<IStoreModel> storeModel = BuildStoreModelMock(_ => { Interlocked.Increment(ref sendCount); return BuildOkContainerResponse(); });

            // Low threshold: a cold-start read would be eligible to hedge if the
            // populate delegate ever ran on the warm path. The primary store model
            // responds immediately so the cold-start populate itself stays a single send.
            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            ClientCollectionCache cache = BuildClientCollectionCache(storeModel.Object, strategy);

            // Cold start: first population of the cache entry.
            ContainerProperties cold = await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            int sendsAfterColdStart = sendCount;

            // Warmed-up client: a subsequent read of the same entry must hit the
            // cache, never re-enter the cold-start populate delegate, and therefore
            // never hedge — producing zero additional metadata sends.
            ContainerProperties warm = await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            Assert.IsNotNull(cold);
            Assert.IsNotNull(warm);
            Assert.AreEqual(1, sendsAfterColdStart, "Cold-start populate should issue exactly one send when the primary wins before the threshold.");
            Assert.AreEqual(sendsAfterColdStart, sendCount, "Warm cache hit must not issue any additional metadata sends or hedges.");
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ClientCollectionCache_WithStrategy_ColdStart_PrimaryWinsBeforeThreshold_SingleSend()
        {
            int sendCount = 0;
            Mock<IStoreModel> storeModel = BuildStoreModelMock(_ => { Interlocked.Increment(ref sendCount); return BuildOkContainerResponse(); });

            // Threshold 5s — primary returns immediately, hedge never fires.
            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromSeconds(5));
            ClientCollectionCache cache = BuildClientCollectionCache(storeModel.Object, strategy);

            ContainerProperties result = await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, sendCount);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ClientCollectionCache_WithStrategy_ColdStart_PrimarySlow_HedgeFires_TwoSends()
        {
            int sendCount = 0;
            Mock<IStoreModel> storeModel = new Mock<IStoreModel>(MockBehavior.Strict);
            storeModel
                .Setup(s => s.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>(async (req, ct) =>
                {
                    int callIndex = Interlocked.Increment(ref sendCount);
                    if (callIndex == 1)
                    {
                        // Primary stalls long enough for the hedge to fire.
                        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    }
                    return BuildOkContainerResponse();
                });

            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            ClientCollectionCache cache = BuildClientCollectionCache(storeModel.Object, strategy);

            ContainerProperties result = await cache.ResolveByNameAsync(
                apiVersion: HttpConstants.Versions.CurrentVersion,
                resourceAddress: "dbs/db/colls/coll",
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, sendCount);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static ClientCollectionCache BuildClientCollectionCache(
            IStoreModel storeModel,
            MetadataHedgingStrategy strategy)
        {
            return new ClientCollectionCache(
                sessionContainer: null,
                storeModel: storeModel,
                tokenProvider: BuildTokenProviderMock().Object,
                retryPolicy: BuildRetryPolicyFactoryMock().Object,
                telemetryToServiceHelper: BuildDisabledTelemetryHelper(),
                enableAsyncCacheExceptionNoSharing: true,
                metadataHedgingStrategy: strategy,
                globalEndpointManager: null);
        }

        private static MetadataHedgingStrategy BuildStrategy(TimeSpan? threshold = null)
        {
            Mock<IGlobalEndpointManager> gem = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            ReadOnlyCollection<Uri> reads = new ReadOnlyCollection<Uri>(new[] { PrimaryEndpoint, HedgeEndpoint });
            gem.Setup(g => g.ReadEndpoints).Returns(reads);
            gem.Setup(g => g.GetApplicableEndpoints(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>())).Returns(reads);
            gem.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(PrimaryEndpoint);
            gem.Setup(g => g.GetLocation(PrimaryEndpoint)).Returns("East US");
            gem.Setup(g => g.GetLocation(HedgeEndpoint)).Returns("West US");

            return new MetadataHedgingStrategy(
                globalEndpointManager: gem.Object,
                isHedgingDisabledByGateway: () => false,
                isPpafEnabled: () => true,
                customerOptIn: true,
                threshold: threshold ?? TimeSpan.FromMilliseconds(50));
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

        private static Mock<IRetryPolicyFactory> BuildRetryPolicyFactoryMock()
        {
            Mock<IRetryPolicyFactory> mock = new Mock<IRetryPolicyFactory>(MockBehavior.Loose);
            Mock<IDocumentClientRetryPolicy> policy = new Mock<IDocumentClientRetryPolicy>(MockBehavior.Loose);
            policy
                .Setup(p => p.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ShouldRetryResult.NoRetry());
            mock.Setup(f => f.GetRequestPolicy()).Returns(policy.Object);
            return mock;
        }

        private static Mock<IStoreModel> BuildStoreModelMock(Func<DocumentServiceRequest, DocumentServiceResponse> respond)
        {
            Mock<IStoreModel> mock = new Mock<IStoreModel>(MockBehavior.Strict);
            mock
                .Setup(s => s.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<DocumentServiceRequest, CancellationToken>((req, ct) => Task.FromResult(respond(req)));
            return mock;
        }

        private static TelemetryToServiceHelper BuildDisabledTelemetryHelper()
        {
            ConnectionPolicy policy = new ConnectionPolicy
            {
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions
                {
                    DisableSendingMetricsToService = true,
                },
            };
            return TelemetryToServiceHelper.CreateAndInitializeClientConfigAndTelemetryJob(
                clientId: "test-client",
                connectionPolicy: policy,
                cosmosAuthorization: null,
                httpClient: null,
                serviceEndpoint: PrimaryEndpoint,
                globalEndpointManager: null,
                cancellationTokenSource: new CancellationTokenSource());
        }

        private static DocumentServiceResponse BuildOkContainerResponse()
        {
            ContainerProperties props = new ContainerProperties("coll", "/pk");
            SetResourceId(props, "JsAtAA==");
            string json = JsonConvert.SerializeObject(props);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            return new DocumentServiceResponse(new MemoryStream(bytes), new StoreResponseNameValueCollection(), HttpStatusCode.OK);
        }

        private static void SetResourceId(ContainerProperties props, string rid)
        {
            typeof(ContainerProperties)
                .GetProperty("ResourceId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.SetValue(props, rid);
        }

        // ---------------------------------------------------------------
        // Test subclass that captures factory invocations.
        // ---------------------------------------------------------------
        private sealed class CapturingCollectionCache : CollectionCache
        {
            public int GetByNameCallCount { get; private set; }
            public int GetByRidCallCount { get; private set; }

            protected override Task<ContainerProperties> GetByRidAsync(
                string apiVersion,
                string collectionRid,
                ITrace trace,
                IClientSideRequestStatistics clientSideRequestStatistics,
                CancellationToken cancellationToken)
            {
                this.GetByRidCallCount++;
                ContainerProperties props = new ContainerProperties("coll", "/pk");
                SetResourceIdStatic(props, "JsAtAA==");
                return Task.FromResult(props);
            }

            protected override Task<ContainerProperties> GetByNameAsync(
                string apiVersion,
                string resourceAddress,
                ITrace trace,
                IClientSideRequestStatistics clientSideRequestStatistics,
                CancellationToken cancellationToken)
            {
                this.GetByNameCallCount++;
                ContainerProperties props = new ContainerProperties("coll", "/pk");
                SetResourceIdStatic(props, "JsAtAA==");
                return Task.FromResult(props);
            }

            private static void SetResourceIdStatic(ContainerProperties props, string rid)
            {
                typeof(ContainerProperties)
                    .GetProperty("ResourceId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.SetValue(props, rid);
            }
        }
    }
}
