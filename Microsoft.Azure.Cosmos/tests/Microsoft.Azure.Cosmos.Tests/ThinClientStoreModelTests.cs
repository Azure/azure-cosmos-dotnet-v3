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

            this.thinClientStoreModel = new ThinClientStoreModel(
                endpointManager: this.endpointManager,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null);

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

            AccountProperties validAccountProperties = new AccountProperties
            {
                ThinClientEndpoint = new Uri("http://localhost/thinClient/")
            };

            docClientMulti
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validAccountProperties);

            ConnectionPolicy policy = new ConnectionPolicy
            {
                UseMultipleWriteLocations = true
            };

            GlobalEndpointManager multiEndpointMgr = new GlobalEndpointManager(docClientMulti.Object, policy);

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager: multiEndpointMgr,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null);

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

            AccountProperties validProperties = new AccountProperties
            {
                ThinClientEndpoint = new Uri("https://myThinClientEndpoint/")
            };

            docClientOkay
                .Setup(c => c.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validProperties);

            ConnectionPolicy policy = new ConnectionPolicy
            {
                UseMultipleWriteLocations = false
            };

            GlobalEndpointManager endpointManagerOk = new GlobalEndpointManager(docClientOkay.Object, policy);

            ThinClientStoreModel storeModel = new ThinClientStoreModel(
                endpointManager: endpointManagerOk,
                sessionContainer: this.sessionContainer,
                defaultConsistencyLevel: (Cosmos.ConsistencyLevel)this.defaultConsistencyLevel,
                eventSource: new DocumentClientEventSource(),
                serializerSettings: null,
                httpClient: null);

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
