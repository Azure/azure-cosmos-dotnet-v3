//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class ThinClientStoreModelTests
    {
        private const string TestResourceId = "NH1uAJ6ANm0=";

        [DataTestMethod]
        [Owner("aavasthy")]
        [DataRow((int)ResourceType.Document, (int)OperationType.Read, null, true, DisplayName = "Document/Read -> supported")]
        [DataRow((int)ResourceType.Document, (int)OperationType.ReadFeed, HttpConstants.A_IMHeaderValues.IncrementalFeed, true, DisplayName = "Document/ReadFeed Incremental -> supported")]
        [DataRow((int)ResourceType.Document, (int)OperationType.ReadFeed, HttpConstants.A_IMHeaderValues.FullFidelityFeed, false, DisplayName = "Document/ReadFeed FullFidelity -> unsupported")]
        [DataRow((int)ResourceType.Document, (int)OperationType.ReadFeed, "Unknown-Feed", false, DisplayName = "Document/ReadFeed unknown A-IM -> unsupported (fail-safe)")]
        [DataRow((int)ResourceType.Document, (int)OperationType.ReadFeed, null, false, DisplayName = "Document/ReadFeed without A-IM -> unsupported")]
        [DataRow((int)ResourceType.StoredProcedure, (int)OperationType.ExecuteJavaScript, null, true, DisplayName = "StoredProcedure/ExecuteJavaScript -> supported")]
        [DataRow((int)ResourceType.StoredProcedure, (int)OperationType.Create, null, false, DisplayName = "StoredProcedure/Create -> unsupported")]
        [DataRow((int)ResourceType.Collection, (int)OperationType.Read, null, false, DisplayName = "Collection/Read -> unsupported")]
        public void IsOperationSupportedByThinClient_Matrix(
            int resourceType,
            int operationType,
            string aImHeader,
            bool expected)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: (OperationType)operationType,
                resourceType: (ResourceType)resourceType,
                resourceId: TestResourceId,
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
            if (aImHeader != null)
            {
                request.Headers[HttpConstants.HttpHeaders.A_IM] = aImHeader;
            }

            Assert.AreEqual(expected, ThinClientStoreModel.IsOperationSupportedByThinClient(request));
        }

        [DataTestMethod]
        [Owner("aavasthy")]
        [DataRow(true,  true,  true,  DisplayName = "Read + read locations advertised -> routable")]
        [DataRow(true,  false, false, DisplayName = "Read + read locations withdrawn -> not routable")]
        [DataRow(false, true,  true,  DisplayName = "Write + write locations advertised -> routable")]
        [DataRow(false, false, false, DisplayName = "Write + write locations withdrawn -> not routable")]
        public void IsThinClientRoutable_RespectsDirectionAwareAvailability(
            bool isReadRequest,
            bool hasMatchingDirectionLocations,
            bool expected)
        {
            Mock<IGlobalEndpointManager> endpointManager = new();
            endpointManager.SetupGet(m => m.HasThinClientReadLocations).Returns(isReadRequest && hasMatchingDirectionLocations);
            endpointManager.SetupGet(m => m.HasThinClientWriteLocations).Returns(!isReadRequest && hasMatchingDirectionLocations);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: isReadRequest ? OperationType.Read : OperationType.Create,
                resourceType: ResourceType.Document,
                resourceId: TestResourceId,
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            Assert.AreEqual(expected, ThinClientStoreModel.IsThinClientRoutable(endpointManager.Object, request));
        }


        [DataTestMethod]
        [Owner("aavasthy")]
        [DataRow(true,  true,  DisplayName = "Read locations advertised -> read-routable for a write request")]
        [DataRow(false, false, DisplayName = "Read locations withdrawn -> not read-routable")]
        public void IsThinClientReadRoutable_IgnoresRequestDirection(bool hasReadLocations, bool expected)
        {
            Mock<IGlobalEndpointManager> endpointManager = new();
            endpointManager.SetupGet(m => m.AreAllThinClientReadEndpointsHealthy).Returns(true);
            endpointManager.SetupGet(m => m.HasThinClientReadLocations).Returns(hasReadLocations);
            // Write locations are intentionally left default (false) to prove the read-direction
            // variant does not consult them.

            DocumentServiceRequest writeRequest = DocumentServiceRequest.Create(
                operationType: OperationType.Create,
                resourceType: ResourceType.Document,
                resourceId: TestResourceId,
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            Assert.AreEqual(expected, ThinClientStoreModel.IsThinClientReadRoutable(endpointManager.Object, writeRequest));
        }

        [DataTestMethod]
        [Owner("aavasthy")]
        [DataRow(true,  true,  DisplayName = "All read endpoints probe-healthy -> read-routable")]
        [DataRow(false, false, DisplayName = "Not all read endpoints probe-healthy -> not read-routable (Gateway V1 fallback)")]
        public void IsThinClientReadRoutable_GatedByAllReadEndpointsHealthy(bool allReadEndpointsHealthy, bool expected)
        {
            Mock<IGlobalEndpointManager> endpointManager = new();
            endpointManager.SetupGet(m => m.AreAllThinClientReadEndpointsHealthy).Returns(allReadEndpointsHealthy);
            endpointManager.SetupGet(m => m.HasThinClientReadLocations).Returns(true);

            DocumentServiceRequest readRequest = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.Document,
                resourceId: TestResourceId,
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            Assert.AreEqual(expected, ThinClientStoreModel.IsThinClientReadRoutable(endpointManager.Object, readRequest));
        }

       
        [TestMethod]
        [Owner("aavasthy")]
        public async Task ProcessMessageAsync_RoutableRequest_DispatchesViaThinClient()
        {
            // This base64 payload is a real binary response captured from the proxy and
            // exercises ParseResponseAsync's binary-JSON deserialization path.
            const string mockBase64 = "9AEAAMkAAAAIvhHfD23jSaynaR+gyTZ3AAAAAQIAByFUaHUsIDEzIEZlYiAyMDI1IDE0OjI1OjI4LjAyNCBHTVQEAAgmACIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDAiDgAIVABkb2N1bWVudFNpemU9NTEyMDA7ZG9jdW1lbnRzU2l6ZT01MjQyODgwMDtkb2N1bWVudHNDb3VudD0tMTtjb2xsZWN0aW9uU2l6ZT01MjQyODgwMDsPAAhBAGRvY3VtZW50U2l6ZT0wO2RvY3VtZW50c1NpemU9MTtkb2N1bWVudHNDb3VudD04O2NvbGxlY3Rpb25TaXplPTM7EAAHBDEuMTkTAAUKAAAAAAAAABUADgzDMAzDMBxAFwAIOgBkYnMvdGhpbi1jbGllbnQtdGVzdC1kYi9jb2xscy90aGluLWNsaWVudC10ZXN0LWNvbnRhaW5lci0xGAAIDABOSDF1QUo2QU5tMD0aAAUJAAAAAAAAAB4AAgMAAAAfAAIEAAAAIQAIAQAwJgACAQAAACkABQkAAAAAAAAAMAACAAAAADUAAgEAAAA6AAUKAAAAAAAAADsABQkAAAAAAAAAPgAIBQAtMSMxMFEADkjhehSuRxBAYwAIAQAweAAF//////////89AQAAeyJpZCI6IjNiMTFiNDM2LTViMTUtNGQwZS1iZWYwLWY1MzVmNjA0MTQxYyIsInBrIjoicGsiLCJuYW1lIjoiODM2MzI0NTA2IiwiZW1haWwiOiJhYmNAZGVmLmNvbSIsImJvZHkiOiJibGFibGEiLCJfcmlkIjoiTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09IiwiX3NlbGYiOiJkYnMvTkgxdUFBPT0vY29sbHMvTkgxdUFKNkFObTA9L2RvY3MvTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09LyIsIl9ldGFnIjoiXCIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDBcIiIsIl9hdHRhY2htZW50cyI6ImF0dGFjaG1lbnRzLyIsIl90cyI6MTczOTQ4MjMwNH0=";
            HttpResponseMessage successResponse = new(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(mockBase64))
            };

            MockThinClientStoreClient thinClientStoreClient = new(
                (request, resourceType, uri, endpoint, accountName, cache, ct) =>
                {
                    Stream body = successResponse.Content.ReadAsStream();
                    return Task.FromResult(new DocumentServiceResponse(body, new StoreResponseNameValueCollection(), successResponse.StatusCode));
                });

            ThinClientStoreModel storeModel = this.BuildStoreModel(
                out GlobalEndpointManager endpointManager,
                advertiseThinClientLocations: true);
            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            DocumentServiceRequest request = CreateDocumentRequest(OperationType.Create);

            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }


        [TestMethod]
        [Owner("aavasthy")]
        public async Task ProcessMessageAsync_AfterServiceWithdrawsThinLocations_FallsBackToGateway()
        {
            int thinClientInvocations = 0;
            MockThinClientStoreClient thinClientStoreClient = new(
                (request, resourceType, uri, endpoint, accountName, cache, ct) =>
                {
                    Interlocked.Increment(ref thinClientInvocations);
                    return Task.FromResult(new DocumentServiceResponse(Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.OK));
                });

            int gatewayInvocations = 0;
            Mock<CosmosHttpClient> mockHttpClient = new();
            mockHttpClient
                .Setup(c => c.SendHttpAsync(
                    It.IsAny<Func<ValueTask<HttpRequestMessage>>>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<HttpTimeoutPolicy>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<DocumentServiceRequest>()))
                .Callback(() => Interlocked.Increment(ref gatewayInvocations))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") });

            ThinClientStoreModel storeModel = this.BuildStoreModel(
                out GlobalEndpointManager endpointManager,
                advertiseThinClientLocations: true,
                httpClient: mockHttpClient.Object);
            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            // Phase 1: thin-client locations are advertised → request routes through thin client.
            await storeModel.ProcessMessageAsync(CreateDocumentRequest(OperationType.Create));
            Assert.AreEqual(1, thinClientInvocations, "First request must route via thin-client while thin locations are advertised.");
            Assert.AreEqual(0, gatewayInvocations, "Gateway HTTP path must NOT be invoked while thin-client is available.");

            // Phase 2: service withdraws thin-client locations on the next account refresh.
            TestUtils.DisableThinClientLocationsForTest(endpointManager);

            await storeModel.ProcessMessageAsync(CreateDocumentRequest(OperationType.Create));
            Assert.AreEqual(1, thinClientInvocations, "Thin-client must NOT be invoked after service withdraws thin locations.");
            Assert.AreEqual(1, gatewayInvocations, "Second request must fall back to the gateway HTTP path on the very next dispatch.");
        }


        [TestMethod]
        [Owner("aavasthy")]
        public async Task ProcessMessageAsync_PPAFEnabled_CallsLocationOverride()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new();
            mockDocumentClient.Setup(c => c.ServiceEndpoint).Returns(new Uri("https://mock.proxy.com"));
            mockDocumentClient
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccountProperties());

            GlobalEndpointManager endpointManager = new(mockDocumentClient.Object, new ConnectionPolicy());
            TestUtils.EnableThinClientLocationsForTest(endpointManager);

            Mock<GlobalPartitionEndpointManager> globalPartitionEndpointManager = new();
            globalPartitionEndpointManager
                .Setup(m => m.IsPartitionLevelAutomaticFailoverEnabled())
                .Returns(true);
            globalPartitionEndpointManager
                .Setup(m => m.TryAddPartitionLevelLocationOverride(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>()))
                .Returns(true)
                .Verifiable();

            ThinClientStoreModel storeModel = new(
                endpointManager,
                new Mock<ISessionContainer>().Object,
                ConsistencyLevel.Session,
                new Mock<DocumentClientEventSource>().Object,
                new JsonSerializerSettings(),
                new Mock<CosmosHttpClient>().Object,
                globalPartitionEndpointManager.Object,
                new UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix"));

            // A fully-mocked routing setup is required because the thin-client path always
            // pre-resolves PKR for partitioned, non-master requests.
            Mock<ClientCollectionCache> mockCollectionCache = new(
                Mock.Of<ISessionContainer>(), storeModel, null, null, null, false);
            ContainerProperties containerProperties = new("test", "/pk");
            typeof(ContainerProperties)
                .GetProperty("ResourceId", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.SetValue(containerProperties, "testCollectionRid");
            mockCollectionCache
                .Setup(c => c.ResolveCollectionAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ITrace>()))
                .ReturnsAsync(containerProperties);

            Mock<PartitionKeyRangeCache> mockPartitionKeyRangeCache = new(
                null, storeModel, mockCollectionCache.Object, endpointManager, false, false);
            PartitionKeyRange pkRange = new() { Id = "0", MinInclusive = "", MaxExclusive = "FF" };
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new List<PartitionKeyRange> { pkRange }.Select(r => Tuple.Create(r, (ServiceIdentity)null)),
                "testCollectionRid",
                false);
            mockPartitionKeyRangeCache
                .Setup(c => c.TryLookupAsync(It.IsAny<string>(), It.IsAny<CollectionRoutingMap>(), It.IsAny<DocumentServiceRequest>(), It.IsAny<ITrace>()))
                .ReturnsAsync(routingMap);

            storeModel.SetCaches(mockPartitionKeyRangeCache.Object, mockCollectionCache.Object);

            MockThinClientStoreClient thinClientStoreClient = new(
                (req, _, _, _, _, _, _) => Task.FromResult(
                    new DocumentServiceResponse(new MemoryStream(new byte[] { 1, 2, 3 }), new StoreResponseNameValueCollection(), HttpStatusCode.OK)));
            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            await storeModel.ProcessMessageAsync(CreatePartitionedDocumentRequest());

            globalPartitionEndpointManager.Verify(
                m => m.TryAddPartitionLevelLocationOverride(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>()),
                Times.Once,
                "PPAF-enabled thin-client dispatch must register a partition-level location override exactly once.");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task ProcessMessageAsync_PPAFDisabled_ResolvesPartitionKeyRangeWithoutLocationOverride()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new();
            mockDocumentClient.Setup(c => c.ServiceEndpoint).Returns(new Uri("https://mock.proxy.com"));
            mockDocumentClient
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccountProperties());

            GlobalEndpointManager endpointManager = new(mockDocumentClient.Object, new ConnectionPolicy());
            TestUtils.EnableThinClientLocationsForTest(endpointManager);

            Mock<GlobalPartitionEndpointManager> globalPartitionEndpointManager = new();
            globalPartitionEndpointManager
                .Setup(m => m.IsPartitionLevelAutomaticFailoverEnabled())
                .Returns(false);
            globalPartitionEndpointManager
                .Setup(m => m.IsPartitionLevelCircuitBreakerEnabled())
                .Returns(false);

            ThinClientStoreModel storeModel = new(
                endpointManager,
                new Mock<ISessionContainer>().Object,
                ConsistencyLevel.Session,
                new Mock<DocumentClientEventSource>().Object,
                new JsonSerializerSettings(),
                new Mock<CosmosHttpClient>().Object,
                globalPartitionEndpointManager.Object,
                new UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix"));

            // The thin-client path always pre-resolves PKR for partitioned, non-master requests,
            // independent of PPAF, so a full routing mock setup is required.
            Mock<ClientCollectionCache> mockCollectionCache = new(
                Mock.Of<ISessionContainer>(), storeModel, null, null, null, false);
            ContainerProperties containerProperties = new("test", "/pk");
            typeof(ContainerProperties)
                .GetProperty("ResourceId", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.SetValue(containerProperties, "testCollectionRid");
            mockCollectionCache
                .Setup(c => c.ResolveCollectionAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ITrace>()))
                .ReturnsAsync(containerProperties);

            Mock<PartitionKeyRangeCache> mockPartitionKeyRangeCache = new(
                null, storeModel, mockCollectionCache.Object, endpointManager, false, false);
            PartitionKeyRange pkRange = new() { Id = "0", MinInclusive = "", MaxExclusive = "FF" };
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new List<PartitionKeyRange> { pkRange }.Select(r => Tuple.Create(r, (ServiceIdentity)null)),
                "testCollectionRid",
                false);
            mockPartitionKeyRangeCache
                .Setup(c => c.TryLookupAsync(It.IsAny<string>(), It.IsAny<CollectionRoutingMap>(), It.IsAny<DocumentServiceRequest>(), It.IsAny<ITrace>()))
                .ReturnsAsync(routingMap);

            storeModel.SetCaches(mockPartitionKeyRangeCache.Object, mockCollectionCache.Object);

            PartitionKeyRange resolvedRange = null;
            MockThinClientStoreClient thinClientStoreClient = new(
                (req, _, _, _, _, _, _) =>
                {
                    resolvedRange = req.RequestContext?.ResolvedPartitionKeyRange;
                    return Task.FromResult(
                        new DocumentServiceResponse(new MemoryStream(new byte[] { 1, 2, 3 }), new StoreResponseNameValueCollection(), HttpStatusCode.OK));
                });
            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            await storeModel.ProcessMessageAsync(CreatePartitionedDocumentRequest());

            Assert.IsNotNull(
                resolvedRange,
                "Thin-client path must resolve the PartitionKeyRange even when PPAF / PLCB is disabled.");
            Assert.AreEqual(
                "0",
                resolvedRange.Id,
                "The resolved PartitionKeyRange must be passed through to the thin-client invoke.");
            globalPartitionEndpointManager.Verify(
                m => m.TryAddPartitionLevelLocationOverride(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>()),
                Times.Never,
                "No partition-level location override may be registered while PPAF / PLCB is disabled.");
        }


        [TestMethod]
        [Owner("aavasthy")]
        public void Dispose_DisposesThinClientStoreClient()
        {
            bool disposeCalled = false;
            MockThinClientStoreClient thinClientStoreClient = new(
                (req, _, _, _, _, _, _) => throw new NotImplementedException(),
                () => disposeCalled = true);

            ThinClientStoreModel storeModel = this.BuildStoreModel(out _);
            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            storeModel.Dispose();

            Assert.IsTrue(disposeCalled, "ThinClientStoreModel.Dispose must dispose its ThinClientStoreClient.");
        }


        [TestMethod]
        [Owner("aavasthy")]
        public async Task ProcessMessageAsync_ThinClientThrows404_PropagatesDocumentClientException()
        {
            MockThinClientStoreClient thinClientStoreClient = new(
                (request, resourceType, uri, endpoint, accountName, cache, ct) =>
                    throw new DocumentClientException(
                        message: "Not Found",
                        innerException: null,
                        responseHeaders: new StoreResponseNameValueCollection(),
                        statusCode: HttpStatusCode.NotFound,
                        requestUri: uri));

            ThinClientStoreModel storeModel = this.BuildStoreModel(out _, advertiseThinClientLocations: true);
            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            await Assert.ThrowsExceptionAsync<DocumentClientException>(
                () => storeModel.ProcessMessageAsync(CreateDocumentRequest(OperationType.Read)),
                "404 from ThinClientStoreClient must propagate as DocumentClientException.");
        }


        private ThinClientStoreModel BuildStoreModel(
            out GlobalEndpointManager endpointManager,
            bool advertiseThinClientLocations = false,
            CosmosHttpClient httpClient = null)
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new();
            mockDocumentClient.Setup(c => c.ServiceEndpoint).Returns(new Uri("https://mock.proxy.com"));
            mockDocumentClient
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccountProperties());

            endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            if (advertiseThinClientLocations)
            {
                TestUtils.EnableThinClientLocationsForTest(endpointManager);
            }

            SessionContainer sessionContainer = new("testhost");
            UserAgentContainer userAgentContainer = new(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreModel storeModel = new(
                endpointManager,
                sessionContainer,
                ConsistencyLevel.Session,
                new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: httpClient,
                GlobalPartitionEndpointManagerNoOp.Instance,
                userAgentContainer);

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(
                sessionContainer, storeModel, null, null, null, false).Object;
            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                null, storeModel, clientCollectionCache, endpointManager, false, false).Object;
            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            return storeModel;
        }

        private static DocumentServiceRequest CreateDocumentRequest(OperationType operationType)
        {
            return DocumentServiceRequest.Create(
                operationType: operationType,
                resourceType: ResourceType.Document,
                resourceId: TestResourceId,
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
        }

        private static DocumentServiceRequest CreatePartitionedDocumentRequest()
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                "/dbs/test/colls/test/docs/test",
                AuthorizationTokenType.PrimaryMasterKey);
            request.Headers[HttpConstants.HttpHeaders.PartitionKey] = "[\"test\"]";
            request.RequestContext = new DocumentServiceRequestContext();
            return request;
        }

        /// <summary>
        /// Reflection-based swap of the private <c>thinClientStoreClient</c> field so tests can
        /// inject a mock without exposing a setter on the production type.
        /// </summary>
        private static void ReplaceThinClientStoreClientField(ThinClientStoreModel model, ThinClientStoreClient newClient)
        {
            FieldInfo field = typeof(ThinClientStoreModel).GetField(
                "thinClientStoreClient",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Could not find 'thinClientStoreClient' field on ThinClientStoreModel");

            field.SetValue(model, newClient);
        }

        internal class MockThinClientStoreClient : ThinClientStoreClient
        {
            private readonly Func<DocumentServiceRequest, ResourceType, Uri, Uri, string, ClientCollectionCache, CancellationToken, Task<DocumentServiceResponse>> invokeAsyncFunc;
            private readonly Action onDispose;

            public MockThinClientStoreClient(
                Func<DocumentServiceRequest, ResourceType, Uri, Uri, string, ClientCollectionCache, CancellationToken, Task<DocumentServiceResponse>> invokeAsyncFunc,
                Action onDispose = null)
                : base(
                    httpClient: null,
                    eventSource: null,
                    userAgentContainer: new UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix"),
                    globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance,
                    serializerSettings: null)
            {
                this.invokeAsyncFunc = invokeAsyncFunc;
                this.onDispose = onDispose;
            }

            public override Task<DocumentServiceResponse> InvokeAsync(
                DocumentServiceRequest request,
                ResourceType resourceType,
                Uri physicalAddress,
                Uri thinClientEndpoint,
                string globalDatabaseAccountName,
                ClientCollectionCache clientCollectionCache,
                CancellationToken cancellationToken)
            {
                return this.invokeAsyncFunc(
                    request,
                    resourceType,
                    physicalAddress,
                    thinClientEndpoint,
                    globalDatabaseAccountName,
                    clientCollectionCache,
                    cancellationToken);
            }

            public override void Dispose()
            {
                base.Dispose();
                this.onDispose?.Invoke();
            }
        }
    }
}
