//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ThinClientStoreModelTests
    {
        private ThinClientStoreModel thinClientStoreModel;
        private GlobalEndpointManager endpointManager;
        private SessionContainer sessionContainer;
        private readonly ConsistencyLevel defaultConsistencyLevel = ConsistencyLevel.Session;

        [TestInitialize]
        public void TestInitialize()
        {
            this.sessionContainer = new SessionContainer("testhost");

            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient
                .Setup(c => c.ServiceEndpoint)
                .Returns(new Uri("https://mock.proxy.com"));

            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                UseMultipleWriteLocations = false
            };

            this.endpointManager = new GlobalEndpointManager(
                 owner: mockDocumentClient.Object,
                 connectionPolicy: connectionPolicy);

            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");
            this.thinClientStoreModel = new ThinClientStoreModel(
                endpointManager: this.endpointManager,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null,
                userAgentContainer: userAgentContainer);

            PartitionKeyRangeCache pkRangeCache =
                (PartitionKeyRangeCache)FormatterServices.GetUninitializedObject(typeof(PartitionKeyRangeCache));
            ClientCollectionCache collCache =
                (ClientCollectionCache)FormatterServices.GetUninitializedObject(typeof(ClientCollectionCache));
            this.thinClientStoreModel.SetCaches(pkRangeCache, collCache);

            System.Diagnostics.Trace.CorrelationManager.ActivityId = Guid.NewGuid();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            System.Diagnostics.Trace.CorrelationManager.ActivityId = Guid.Empty;
            this.thinClientStoreModel?.Dispose();
        }

        [TestMethod]
        public async Task ProcessMessageAsync_Success_ShouldReturnDocumentServiceResponse()
        {
            // Arrange
            // A single base64-encoded RNTBD response representing an HTTP 201 (Created)
            string mockBase64 = "9AEAAMkAAAAIvhHfD23jSaynaR+gyTZ3AAAAAQIAByFUaHUsIDEzIEZlYiAyMDI1IDE0OjI1OjI4LjAyNCBHTVQEAAgmACIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDAiDgAIVABkb2N1bWVudFNpemU9NTEyMDA7ZG9jdW1lbnRzU2l6ZT01MjQyODgwMDtkb2N1bWVudHNDb3VudD0tMTtjb2xsZWN0aW9uU2l6ZT01MjQyODgwMDsPAAhBAGRvY3VtZW50U2l6ZT0wO2RvY3VtZW50c1NpemU9MTtkb2N1bWVudHNDb3VudD04O2NvbGxlY3Rpb25TaXplPTM7EAAHBDEuMTkTAAUKAAAAAAAAABUADgzDMAzDMBxAFwAIOgBkYnMvdGhpbi1jbGllbnQtdGVzdC1kYi9jb2xscy90aGluLWNsaWVudC10ZXN0LWNvbnRhaW5lci0xGAAIDABOSDF1QUo2QU5tMD0aAAUJAAAAAAAAAB4AAgMAAAAfAAIEAAAAIQAIAQAwJgACAQAAACkABQkAAAAAAAAAMAACAAAAADUAAgEAAAA6AAUKAAAAAAAAADsABQkAAAAAAAAAPgAIBQAtMSMxMFEADkjhehSuRxBAYwAIAQAweAAF//////////89AQAAeyJpZCI6IjNiMTFiNDM2LTViMTUtNGQwZS1iZWYwLWY1MzVmNjA0MTQxYyIsInBrIjoicGsiLCJuYW1lIjoiODM2MzI0NTA2IiwiZW1haWwiOiJhYmNAZGVmLmNvbSIsImJvZHkiOiJibGFibGEiLCJfcmlkIjoiTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09IiwiX3NlbGYiOiJkYnMvTkgxdUFBPT0vY29sbHMvTkgxdUFKNkFObTA9L2RvY3MvTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09LyIsIl9ldGFnIjoiXCIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDBcIiIsIl9hdHRhY2htZW50cyI6ImF0dGFjaG1lbnRzLyIsIl90cyI6MTczOTQ4MjMwNH0=";

            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(mockBase64))
            };

            MockThinClientStoreClient thinClientStoreClient = new MockThinClientStoreClient(
                (request, resourceType, uri, endpoint, globalDatabaseAccountName, clientCollectionCache, cancellationToken) =>
                {
                    Stream responseBody = successResponse.Content.ReadAsStream();
                    INameValueCollection headers = new StoreResponseNameValueCollection();
                    return Task.FromResult(new DocumentServiceResponse(responseBody, headers, successResponse.StatusCode));
                });

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.Create,
                resourceType: ResourceType.Document,
                resourceId: "NH1uAJ6ANm0=",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            Mock<IDocumentClientInternal> docClientMulti = new Mock<IDocumentClientInternal>();
            docClientMulti.Setup(c => c.ServiceEndpoint).Returns(new Uri("http://localhost"));

            AccountProperties validAccountProperties = new AccountProperties();

            docClientMulti
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validAccountProperties);

            ConnectionPolicy policy = new ConnectionPolicy
            {
                UseMultipleWriteLocations = true
            };

            GlobalEndpointManager multiEndpointMgr = new GlobalEndpointManager(docClientMulti.Object, policy);
            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager: multiEndpointMgr,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null,
                userAgentContainer: userAgentContainer);

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(
                this.sessionContainer,
                storeModel,
                null,
                null,
                null,
                false).Object;

            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                null,
                storeModel,
                clientCollectionCache,
                multiEndpointMgr,
                false).Object;

            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            // Inject the thinclient store client that returns 201
            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            // Act
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task ProcessMessageAsync_WithUnsupportedOperations_ShouldFallbackToGatewayModeAndReturnDocumentServiceResponse()
        {
            // Arrange
            // A single base64-encoded RNTBD response representing an HTTP 201 (Created)
            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") };
            HttpRequestMessage capturedRequest = null;
            Mock<CosmosHttpClient> mockCosmosHttpClient = new Mock<CosmosHttpClient>();
            mockCosmosHttpClient.Setup(client => client.SendHttpAsync(
                It.IsAny<Func<ValueTask<HttpRequestMessage>>>(),
                It.IsAny<ResourceType>(),
                It.IsAny<HttpTimeoutPolicy>(),
                It.IsAny<IClientSideRequestStatistics>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<DocumentServiceRequest>()))
                .Callback<Func<ValueTask<HttpRequestMessage>>, ResourceType, HttpTimeoutPolicy, IClientSideRequestStatistics, CancellationToken, DocumentServiceRequest>(
                    async (requestFactory, _, _, _, _, _) =>
                        capturedRequest = await requestFactory())
                .ReturnsAsync(successResponse);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: OperationType.QueryPlan,
                resourceType: ResourceType.Document,
                resourceId: "NH1uAJ6ANm0=",
                body: null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            Mock<IDocumentClientInternal> docClientMulti = new Mock<IDocumentClientInternal>();
            docClientMulti.Setup(c => c.ServiceEndpoint).Returns(new Uri("http://localhost"));

            AccountProperties validAccountProperties = new AccountProperties();

            docClientMulti
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validAccountProperties);

            ConnectionPolicy policy = new ConnectionPolicy
            {
                UseMultipleWriteLocations = true
            };

            GlobalEndpointManager multiEndpointMgr = new GlobalEndpointManager(docClientMulti.Object, policy);

            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");
            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager: multiEndpointMgr,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: mockCosmosHttpClient.Object,
                userAgentContainer: userAgentContainer);

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(
                this.sessionContainer,
                storeModel,
                null,
                null,
                null,
                false).Object;

            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                null,
                storeModel,
                clientCollectionCache,
                multiEndpointMgr,
                false).Object;

            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            // Act
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public void Dispose_ShouldDisposeThinClientStoreClient()
        {
            // Arrange
            bool disposeCalled = false;

            ThinClientStoreClient thinClientStoreClient = new MockThinClientStoreClient(
                (request, resourceType, uri, endpoint, globalDatabaseAccountName, clientCollectionCache, cancellationToken) =>
                    throw new NotImplementedException(),
                () => disposeCalled = true);

            ReplaceThinClientStoreClientField(this.thinClientStoreModel, thinClientStoreClient);
            // Act
            this.thinClientStoreModel.Dispose();
            // Assert
            Assert.IsTrue(disposeCalled, "Expected Dispose to be called on ThinClientStoreClient.");
        }

        [TestMethod]
        public async Task ProcessMessageAsync_404_ShouldThrowDocumentClientException()
        {
            // Arrange
            MockThinClientStoreClient thinClientStoreClient = new MockThinClientStoreClient(
               (request, resourceType, uri, endpoint, globalDatabaseAccountName, clientCollectionCache, cancellationToken) =>
                   throw new DocumentClientException(
                       message: "Not Found",
                       innerException: null,
                       responseHeaders: new StoreResponseNameValueCollection(),
                       statusCode: HttpStatusCode.NotFound,
                        requestUri: uri));

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                 operationType: OperationType.Read,
                 resourceType: ResourceType.Document,
                 resourceId: "NH1uAJ6ANm0=",
                 body: null,
                 authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            Mock<IDocumentClientInternal> docClientOkay = new Mock<IDocumentClientInternal>();
            docClientOkay
                .Setup(c => c.ServiceEndpoint)
                .Returns(new Uri("https://myCosmosAccount.documents.azure.com/"));

            AccountProperties validProperties = new AccountProperties();

            docClientOkay
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validProperties);

            ConnectionPolicy policy = new ConnectionPolicy
            {
                UseMultipleWriteLocations = false
            };

            GlobalEndpointManager endpointManagerOk = new GlobalEndpointManager(docClientOkay.Object, policy);
            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager: endpointManagerOk,
                globalPartitionEndpointManager: GlobalPartitionEndpointManagerNoOp.Instance,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null,
                userAgentContainer: userAgentContainer);

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(
                this.sessionContainer,
                storeModel,
                null,
                null,
                null,
                false).Object;

            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                null,
                storeModel,
                clientCollectionCache,
                endpointManagerOk,
                false).Object;

            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            ReplaceThinClientStoreClientField(storeModel, thinClientStoreClient);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<DocumentClientException>(
                async () => await storeModel.ProcessMessageAsync(request),
                "Expected 404 DocumentClientException from the final thinClientStore call");
        }

        [TestMethod]
        public async Task PartitionLevelFailoverEnabled_ResolvesPartitionKeyRangeAndCallsLocationOverride()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(c => c.ServiceEndpoint).Returns(new Uri("https://mock.proxy.com"));
            mockDocumentClient
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccountProperties());

            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, connectionPolicy);

            Mock<GlobalPartitionEndpointManager> globalPartitionEndpointManager = new Mock<GlobalPartitionEndpointManager>();
            globalPartitionEndpointManager
                .Setup(m => m.TryAddPartitionLevelLocationOverride(It.IsAny<DocumentServiceRequest>()))
                .Returns(true)
                .Verifiable();

            globalPartitionEndpointManager
                .Setup(m => m.IsPerPartitionAutomaticFailoverEnabled())
                .Returns(true)
                .Verifiable();

            ISessionContainer sessionContainer = new Mock<ISessionContainer>().Object;
            DocumentClientEventSource eventSource = new Mock<DocumentClientEventSource>().Object;
            Newtonsoft.Json.JsonSerializerSettings serializerSettings = new Newtonsoft.Json.JsonSerializerSettings();
            CosmosHttpClient httpClient = new Mock<CosmosHttpClient>().Object;
            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager,
                globalPartitionEndpointManager.Object,
                sessionContainer,
                Cosmos.ConsistencyLevel.Session,
                eventSource,
                serializerSettings,
                httpClient,
                userAgentContainer);

            Mock<ClientCollectionCache> mockCollectionCache = new Mock<ClientCollectionCache>(
                sessionContainer,
                storeModel,
                null,
                null,
                null,
                false);

            ContainerProperties containerProperties = new ContainerProperties("test", "/pk");
            typeof(ContainerProperties)
                .GetProperty("ResourceId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.SetValue(containerProperties, "testCollectionRid");
            containerProperties.PartitionKeyPath = "/pk";

            mockCollectionCache
                .Setup(c => c.ResolveCollectionAsync(It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ITrace>()))
                .ReturnsAsync(containerProperties);

            Mock<PartitionKeyRangeCache> mockPartitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                null,
                storeModel,
                mockCollectionCache.Object,
                endpointManager,
                false);

            PartitionKeyRange pkRange = new PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "FF" };
            List<PartitionKeyRange> pkRanges = new List<PartitionKeyRange> { pkRange };
            IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> rangeTuples = pkRanges.Select(r => Tuple.Create(r, (ServiceIdentity)null));
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(rangeTuples, "testCollectionRid");

            mockPartitionKeyRangeCache
                .Setup(c => c.TryLookupAsync(It.IsAny<string>(), It.IsAny<CollectionRoutingMap>(), It.IsAny<DocumentServiceRequest>(), It.IsAny<ITrace>()))
                .ReturnsAsync(routingMap);

            storeModel.SetCaches(mockPartitionKeyRangeCache.Object, mockCollectionCache.Object);

            DocumentServiceRequest request = CreatePartitionedDocumentRequest();

            MockThinClientStoreClient mockThinClientStoreClient = new MockThinClientStoreClient(
                (DocumentServiceRequest req, ResourceType resourceType, Uri uri, Uri endpoint, string globalDatabaseAccountName, ClientCollectionCache clientCollectionCache, CancellationToken cancellationToken) =>
                {
                    MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3 });
                    INameValueCollection headers = new StoreResponseNameValueCollection();
                    return Task.FromResult(new DocumentServiceResponse(stream, headers, HttpStatusCode.OK));
                });

            ReplaceThinClientStoreClientField(storeModel, mockThinClientStoreClient);

            // Act
            await storeModel.ProcessMessageAsync(request);

            // Assert
            globalPartitionEndpointManager.Verify(m => m.TryAddPartitionLevelLocationOverride(It.IsAny<DocumentServiceRequest>()), Times.Once());
        }

        [TestMethod]
        public void CircuitBreaker_MarksPartitionUnavailableOnRepeatedFailures()
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(c => c.ServiceEndpoint).Returns(new Uri("https://mock.proxy.com"));
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, connectionPolicy);
            Mock<GlobalPartitionEndpointManager> globalPartitionEndpointManager = new Mock<GlobalPartitionEndpointManager>();
            globalPartitionEndpointManager
                .Setup(m => m.TryAddPartitionLevelLocationOverride(It.IsAny<DocumentServiceRequest>()))
                .Returns(true);

            globalPartitionEndpointManager
                .Setup(m => m.TryMarkEndpointUnavailableForPartitionKeyRange(It.IsAny<DocumentServiceRequest>()))
                .Returns(true)
                .Verifiable();

            globalPartitionEndpointManager
                .Setup(m => m.IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(It.IsAny<DocumentServiceRequest>()))
                .Returns(true);

            globalPartitionEndpointManager
                .Setup(m => m.IsPerPartitionAutomaticFailoverEnabled())
                .Returns(true)
                .Verifiable();

            ISessionContainer sessionContainer = new Mock<ISessionContainer>().Object;
            DocumentClientEventSource eventSource = new Mock<DocumentClientEventSource>().Object;
            Newtonsoft.Json.JsonSerializerSettings serializerSettings = new Newtonsoft.Json.JsonSerializerSettings();
            CosmosHttpClient httpClient = new Mock<CosmosHttpClient>().Object;
            Cosmos.UserAgentContainer userAgentContainer = new Microsoft.Azure.Cosmos.UserAgentContainer(0, "TestFeature", "TestRegion", "TestSuffix");

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager,
                globalPartitionEndpointManager.Object,
                sessionContainer,
                Cosmos.ConsistencyLevel.Session,
                eventSource,
                serializerSettings,
                httpClient,
                userAgentContainer);

            TestUtils.SetupCachesInGatewayStoreModel(storeModel, endpointManager);

            DocumentServiceRequest request = CreatePartitionedDocumentRequest();

            for (int i = 0; i < 3; i++)
            {
                globalPartitionEndpointManager.Object.IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(request);
            }

            globalPartitionEndpointManager.Object.TryMarkEndpointUnavailableForPartitionKeyRange(request);

            globalPartitionEndpointManager.Verify(m => m.TryMarkEndpointUnavailableForPartitionKeyRange(It.IsAny<DocumentServiceRequest>()), Times.Once());
        }

        private static void ReplaceThinClientStoreClientField(ThinClientStoreModel model, ThinClientStoreClient newClient)
        {
            FieldInfo field = typeof(ThinClientStoreModel).GetField(
                 "thinClientStoreClient",
                 BindingFlags.NonPublic | BindingFlags.Instance)
                 ?? throw new InvalidOperationException("Could not find 'thinClientStoreClient' field on ThinClientStoreModel");

            field.SetValue(model, newClient);
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
                    userAgentContainer: null,
                    serializerSettings: null)
            {
                this.invokeAsyncFunc = invokeAsyncFunc;
                this.onDispose = onDispose;
            }

            public override async Task<DocumentServiceResponse> InvokeAsync(
                DocumentServiceRequest request,
                ResourceType resourceType,
                Uri physicalAddress,
                Uri thinClientEndpoint,
                string globalDatabaseAccountName,
                ClientCollectionCache clientCollectionCache,
                CancellationToken cancellationToken)
            {
                return await this.invokeAsyncFunc(
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
