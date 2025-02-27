//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ThinClientStoreModelTests
    {
        private ThinClientStoreModel thinClientStoreModel;

        private Mock<IGlobalEndpointManager> mockEndpointManager;
        private SessionContainer sessionContainer;

        private readonly ConsistencyLevel defaultConsistencyLevel = ConsistencyLevel.Session;

        [TestInitialize]
        public void TestInitialize()
        {
            this.sessionContainer = new SessionContainer("testhost");

            this.mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Strict);
            this.mockEndpointManager
                .Setup(x => x.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(new Uri("https://mock.proxy.com"));

            this.thinClientStoreModel = new ThinClientStoreModel(
                endpointManager: this.mockEndpointManager.Object,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null,
                proxyEndpoint: new Uri("https://mock.proxy.com"),
                globalDatabaseAccountName: "MockAccount");

            PartitionKeyRangeCache pkRangeCache = (PartitionKeyRangeCache)FormatterServices.GetUninitializedObject(typeof(PartitionKeyRangeCache));
            ClientCollectionCache collCache = (ClientCollectionCache)FormatterServices.GetUninitializedObject(typeof(ClientCollectionCache));
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
            // A single base64-encoded RNTBD response representing an HTTP 201 (Created) in RNTBD format.
            string mockBase64 = "9AEAAMkAAAAIvhHfD23jSaynaR+gyTZ3AAAAAQIAByFUaHUsIDEzIEZlYiAyMDI1IDE0OjI1OjI4LjAyNCBHTVQEAAgmACIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDAiDgAIVABkb2N1bWVudFNpemU9NTEyMDA7ZG9jdW1lbnRzU2l6ZT01MjQyODgwMDtkb2N1bWVudHNDb3VudD0tMTtjb2xsZWN0aW9uU2l6ZT01MjQyODgwMDsPAAhBAGRvY3VtZW50U2l6ZT0wO2RvY3VtZW50c1NpemU9MTtkb2N1bWVudHNDb3VudD04O2NvbGxlY3Rpb25TaXplPTM7EAAHBDEuMTkTAAUKAAAAAAAAABUADgzDMAzDMBxAFwAIOgBkYnMvdGhpbi1jbGllbnQtdGVzdC1kYi9jb2xscy90aGluLWNsaWVudC10ZXN0LWNvbnRhaW5lci0xGAAIDABOSDF1QUo2QU5tMD0aAAUJAAAAAAAAAB4AAgMAAAAfAAIEAAAAIQAIAQAwJgACAQAAACkABQkAAAAAAAAAMAACAAAAADUAAgEAAAA6AAUKAAAAAAAAADsABQkAAAAAAAAAPgAIBQAtMSMxMFEADkjhehSuRxBAYwAIAQAweAAF//////////89AQAAeyJpZCI6IjNiMTFiNDM2LTViMTUtNGQwZS1iZWYwLWY1MzVmNjA0MTQxYyIsInBrIjoicGsiLCJuYW1lIjoiODM2MzI0NTA2IiwiZW1haWwiOiJhYmNAZGVmLmNvbSIsImJvZHkiOiJibGFibGEiLCJfcmlkIjoiTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09IiwiX3NlbGYiOiJkYnMvTkgxdUFBPT0vY29sbHMvTkgxdUFKNkFObTA9L2RvY3MvTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09LyIsIl9ldGFnIjoiXCIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDBcIiIsIl9hdHRhY2htZW50cyI6ImF0dGFjaG1lbnRzLyIsIl90cyI6MTczOTQ4MjMwNH0=";

            HttpResponseMessage successResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(mockBase64))
            };

            MockProxyStoreClient proxyStoreClient = new MockProxyStoreClient(
                invokeAsyncFunc: (request, resourceType, uri, cancellationToken) =>
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

            Mock<IGlobalEndpointManager> endpointManager = new Mock<IGlobalEndpointManager>();
            endpointManager.Setup(gem => gem.CanUseMultipleWriteLocations(It.Is<DocumentServiceRequest>(dsr => dsr == request))).Returns(true);
            endpointManager.Setup(gem => gem.ResolveServiceEndpoint(request)).Returns(new Uri("http://localhost"));

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager: endpointManager.Object,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null,  // We will override the proxy client
                proxyEndpoint: new Uri("https://foo.com/dbs/db1/colls/coll1"),
                globalDatabaseAccountName: "MockAccount");

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(
                this.sessionContainer,
                storeModel,
                null,
                null,
                null).Object;

            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                /* ICosmosAuthorizationTokenProvider */ null,
                /* IStoreModel */ storeModel,
                /* CollectionCache */ clientCollectionCache,
                /* IGlobalEndpointManager */ endpointManager.Object).Object;

            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            // Inject the ProxyStoreClient into the ThinClientStoreModel
            ReplaceProxyStoreClientField(storeModel, proxyStoreClient);

            // Act
            DocumentServiceResponse response = await storeModel.ProcessMessageAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        [TestMethod]
        public void Dispose_ShouldDisposeProxyStoreClient()
        {
            // Arrange
            bool disposeCalled = false;

            ThinClientStoreClient proxyStoreClient = new MockProxyStoreClient(
                invokeAsyncFunc: (request, resourceType, uri, cancellationToken) => throw new NotImplementedException(),
                onDispose: () => disposeCalled = true);

            // Inject the ProxyStoreClient into the ThinClientStoreModel
            ReplaceProxyStoreClientField(this.thinClientStoreModel, proxyStoreClient);

            // Act
            this.thinClientStoreModel.Dispose();

            // Assert
            Assert.IsTrue(disposeCalled, "Expected Dispose to be called on ProxyStoreClient.");
        }

        [TestMethod]
        public async Task ProcessMessageAsync_404_ShouldThrowDocumentClientException()
        {
            // Arrange
            MockProxyStoreClient proxyStoreClient = new MockProxyStoreClient(
                invokeAsyncFunc: (request, resourceType, uri, cancellationToken) => throw new DocumentClientException(
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

            Mock<IGlobalEndpointManager> endpointManager = new Mock<IGlobalEndpointManager>();
            endpointManager
                .Setup(gem => gem.CanUseMultipleWriteLocations(It.IsAny<DocumentServiceRequest>()))
                .Returns(true);
            endpointManager
                .Setup(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(new Uri("https://foo.com/dbs/db1/colls/coll1"));

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager: endpointManager.Object,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null,
                proxyEndpoint: new Uri("https://foo.com/dbs/db1/colls/coll1"),
                globalDatabaseAccountName: "MockAccount");

            ClientCollectionCache clientCollectionCache = new Mock<ClientCollectionCache>(
                this.sessionContainer,
                storeModel,
                null,
                null,
                null).Object;

            PartitionKeyRangeCache partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(
                null,
                storeModel,
                clientCollectionCache,
                endpointManager.Object).Object;

            storeModel.SetCaches(partitionKeyRangeCache, clientCollectionCache);

            ReplaceProxyStoreClientField(storeModel, proxyStoreClient);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<DocumentClientException>(async () => await storeModel.ProcessMessageAsync(request));
        }

        // Helper method to inject the ProxyStoreClient into ThinClientStoreModel
        private static void ReplaceProxyStoreClientField(ThinClientStoreModel model, ThinClientStoreClient newClient)
        {
            FieldInfo field = typeof(ThinClientStoreModel).GetField(
                "proxyStoreClient",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Could not find 'proxyStoreClient' field on ThinClientStoreModel");
            field.SetValue(model, newClient);
        }

        internal class MockProxyStoreClient : ThinClientStoreClient
        {
            private readonly Func<DocumentServiceRequest, ResourceType, Uri, CancellationToken, Task<DocumentServiceResponse>> invokeAsyncFunc;
            private readonly Action onDispose;

            public MockProxyStoreClient(
                Func<DocumentServiceRequest, ResourceType, Uri, CancellationToken, Task<DocumentServiceResponse>> invokeAsyncFunc,
                Action onDispose = null)
                : base(
                    httpClient: null,
                    eventSource: null,
                    proxyEndpoint: new Uri("https://mock.proxy.com"),
                    globalDatabaseAccountName: "MockAccount",
                    serializerSettings: null)
            {
                this.invokeAsyncFunc = invokeAsyncFunc;
                this.onDispose = onDispose;
            }

            public override async Task<DocumentServiceResponse> InvokeAsync(
                DocumentServiceRequest request,
                ResourceType resourceType,
                Uri physicalAddress,
                CancellationToken cancellationToken)
            {
                return await this.invokeAsyncFunc(request, resourceType, physicalAddress, cancellationToken);
            }

            public override void Dispose()
            {
                base.Dispose();
                this.onDispose?.Invoke();
            }
        }
    }
}
