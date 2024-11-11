//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class ExceptionlessTests
    {
        private static readonly Uri resourceUri = new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute);

        [TestMethod]
        [ExpectedException(typeof(NotFoundException))]
        public void TransportClient_DoesThrowFor404WithReadSessionNotAvailable_WithUseStatusCodeForFailures()
        {
            using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        OperationType.Query,
                        ResourceType.Document,
                        ExceptionlessTests.resourceUri,
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
            {
                request.UseStatusCodeForFailures = true;
                StoreResponse mockStoreResponse404 = new StoreResponse
                {
                    Headers = new StoreResponseNameValueCollection()
                };
                mockStoreResponse404.Headers.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString());
                mockStoreResponse404.Status = (int)HttpStatusCode.NotFound;


                TransportClient.ThrowServerException(
                    string.Empty,
                    mockStoreResponse404,
                    ExceptionlessTests.resourceUri,
                    Guid.NewGuid(),
                    request);
            }
        }

        [TestMethod]
        [DataRow((int)HttpStatusCode.NotFound)]
        [DataRow((int)HttpStatusCode.PreconditionFailed)]
        [DataRow((int)HttpStatusCode.Conflict)]
        public void TransportClient_DoesNotThrowFor4XX_WithUseStatusCodeForFailures(int statusCode)
        {
            using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        OperationType.Query,
                        ResourceType.Document,
                        ExceptionlessTests.resourceUri,
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
            {
                request.UseStatusCodeForFailures = true;
                StoreResponse mockStoreResponse4XX = new StoreResponse
                {
                    Status = statusCode
                };


                TransportClient.ThrowServerException(
                    string.Empty,
                    mockStoreResponse4XX,
                    ExceptionlessTests.resourceUri,
                    Guid.NewGuid(),
                    request);
            }
        }

        [TestMethod]
        public void TransportClient_DoesNotThrowFor429_WithUseStatusCodeFor429()
        {
            using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        OperationType.Query,
                        ResourceType.Document,
                        ExceptionlessTests.resourceUri,
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
            {
                request.UseStatusCodeFor429 = true;
                StoreResponse mockStoreResponse429 = new StoreResponse
                {
                    Status = (int)StatusCodes.TooManyRequests
                };

                TransportClient.ThrowServerException(
                    string.Empty,
                    mockStoreResponse429,
                    ExceptionlessTests.resourceUri,
                    Guid.NewGuid(),
                    request);
            }
        }

        [TestMethod]
        public async Task GatewayStoreClient_DoesNotThrowFor429_WithUseStatusCodeFor429()
        {
            using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    OperationType.Query,
                    ResourceType.Document,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
            {
                request.UseStatusCodeFor429 = true;
                await GatewayStoreClientRunScenario(request, (int)StatusCodes.TooManyRequests);

            }
        }

        [TestMethod]
        [DataRow((int)HttpStatusCode.NotFound)]
        [DataRow((int)HttpStatusCode.PreconditionFailed)]
        [DataRow((int)HttpStatusCode.Conflict)]
        public async Task GatewayStoreClient_DoesNotThrowFor4XX_UseStatusCodeForFailures(int statusCode)
        {
            using (DocumentServiceRequest request =
                DocumentServiceRequest.Create(
                    OperationType.Query,
                    ResourceType.Document,
                    new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                    new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                    AuthorizationTokenType.PrimaryMasterKey,
                    null))
            {
                request.UseStatusCodeForFailures = true;
                await GatewayStoreClientRunScenario(request, statusCode);

            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]

        public async Task TransportHandler_DoesNotCatch_For409(bool goThroughGateway)
        {
            MockTransportHandler transportHandler = await TransportHandlerRunScenario((int)HttpStatusCode.Conflict, goThroughGateway);
            Assert.IsFalse(transportHandler.ProcessMessagesAsyncThrew);
            Assert.IsTrue(transportHandler.SendAsyncCalls == 1);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TransportHandler_DoesNotCatch_For404(bool goThroughGateway)
        {
            MockTransportHandler transportHandler = await TransportHandlerRunScenario((int)HttpStatusCode.NotFound, goThroughGateway);
            Assert.IsFalse(transportHandler.ProcessMessagesAsyncThrew);
            Assert.IsTrue(transportHandler.SendAsyncCalls == 1);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TransportHandler_DoesNotCatch_For412(bool goThroughGateway)
        {
            MockTransportHandler transportHandler = await TransportHandlerRunScenario((int)HttpStatusCode.PreconditionFailed, goThroughGateway);
            Assert.IsFalse(transportHandler.ProcessMessagesAsyncThrew);
            Assert.IsTrue(transportHandler.SendAsyncCalls == 1);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TransportHandler_DoesNotCatch_For429(bool goThroughGateway)
        {
            MockTransportHandler transportHandler = await TransportHandlerRunScenario((int)StatusCodes.TooManyRequests, goThroughGateway);
            Assert.IsFalse(transportHandler.ProcessMessagesAsyncThrew);
            Assert.IsTrue(transportHandler.SendAsyncCalls > 1);
        }

        /// <summary>
        /// Creates a CosmosClient with a mock TransportHandler that will capture and detect Exceptions happening inside ProcessChangesAsync.
        /// Since we want to be Exception-less, this will help detect if the Store Model is throwing or not.
        /// </summary>
        /// <param name="goThroughGateway">Whether or not to run the scenario using Gateway. If false, Direct will be used.</param>
        private static async Task<MockTransportHandler> TransportHandlerRunScenario(int responseStatusCode, bool goThroughGateway = true)
        {
            async Task<HttpResponseMessage> sendFunc(HttpRequestMessage httpRequest)
            {
                return await Task.FromResult(new HttpResponseMessage((HttpStatusCode)responseStatusCode)
                {
                    Content = new StringContent("{}"),
                    RequestMessage = httpRequest
                });
            }

            StoreResponse sendDirectFunc(TransportAddressUri uri, DocumentServiceRequest request)
            {
                return new StoreResponse()
                {
                    ResponseBody = Stream.Null,
                    Status = responseStatusCode,
                    Headers = new StoreResponseNameValueCollection()
                };
            }

            // This is needed because in order to Mock a TransportClient we previously need an instance of CosmosClient
            using CosmosClient internalClient = MockCosmosUtil.CreateMockCosmosClient();
            internalClient.DocumentClient.GatewayStoreModel = MockGatewayStoreModel(sendFunc);
            internalClient.DocumentClient.StoreModel = MockServerStoreModel(internalClient.DocumentClient.Session, sendDirectFunc);


            RetryHandler retryHandler = new RetryHandler(internalClient);
            MockTransportHandler transportHandler = new MockTransportHandler(internalClient);

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (builder) => builder.AddCustomHandlers(retryHandler, transportHandler));

            try
            {
                if (goThroughGateway)
                {
                    DatabaseResponse response = await client.CreateDatabaseAsync("test");
                }
                else
                {
                    ItemResponse<dynamic> response = await client.GetContainer("test", "test").CreateItemAsync<dynamic>(item: new { id = "id" }, partitionKey: new Cosmos.PartitionKey("id"));
                }
            }
            catch (CosmosException)
            {
                // Swallow CosmosExceptions as the point is to test the TransportHandler
            }

            return transportHandler;
        }

        private static ServerStoreModel MockServerStoreModel(
            object sessionContainer,
            Func<TransportAddressUri, DocumentServiceRequest, StoreResponse> sendDirectFunc)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                    .ReturnsAsync(sendDirectFunc);

            AddressInformation[] addressInformation = GetMockAddressInformation();
            Mock<IAddressResolver> mockAddressCache = GetMockAddressCache(addressInformation);

            ReplicationPolicy replicationPolicy = new ReplicationPolicy
            {
                MaxReplicaSetSize = 1
            };
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

            return new ServerStoreModel(new StoreClient(
                        mockAddressCache.Object,
                        (SessionContainer)sessionContainer,
                        mockServiceConfigReader.Object,
                        mockAuthorizationTokenProvider.Object,
                        Protocol.Tcp,
                        mockTransportClient.Object));
        }


        /// <summary>
        /// Sends a request with a particular response status code through the GatewayStoreModel
        /// </summary>
        private static async Task GatewayStoreClientRunScenario(
            DocumentServiceRequest request,
            int responseStatusCode)
        {
            async Task<HttpResponseMessage> sendFunc(HttpRequestMessage httpRequest)
            {
                return await Task.FromResult(new HttpResponseMessage((HttpStatusCode)responseStatusCode));
            }

            using GatewayStoreModel storeModel = MockGatewayStoreModel(sendFunc);

            using (new ActivityScope(Guid.NewGuid()))
            {
                await storeModel.ProcessMessageAsync(request);
            }
        }

        private static GatewayStoreModel MockGatewayStoreModel(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc)
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            return new GatewayStoreModel(
                endpointManager,
                sessionContainer,
                Cosmos.ConsistencyLevel.Eventual,
                new DocumentClientEventSource(),
                new JsonSerializerSettings(),
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));
        }

        private static Mock<IAddressResolver> GetMockAddressCache(AddressInformation[] addressInformation)
        {
            // Address Selector is an internal sealed class that can't be mocked, but its dependency
            // AddressCache can be mocked.
            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();

            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    false /*forceRefresh*/,
                    new CancellationToken()))
                    .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            return mockAddressCache;
        }

        private static AddressInformation[] GetMockAddressInformation()
        {
            // setup mocks for address information
            AddressInformation[] addressInformation = new AddressInformation[3];

            // construct URIs that look like the actual uri
            // rntbd://yt1prdddc01-docdb-1.documents.azure.com:14003/apps/ce8ab332-f59e-4ce7-a68e-db7e7cfaa128/services/68cc0b50-04c6-4716-bc31-2dfefd29e3ee/partitions/5604283d-0907-4bf4-9357-4fa9e62de7b5/replicas/131170760736528207s/
            for (int i = 0; i <= 2; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri: "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    isPrimary: i == 0,
                    protocol: Documents.Client.Protocol.Tcp,
                    isPublic: true);
            }
            return addressInformation;
        }

        private class MockMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc;

            public MockMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> func)
            {
                this.sendFunc = func;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await this.sendFunc(request);
            }
        }

        /// <summary>
        /// This TransportHandler sends the request and watches for DocumentClientExceptions and set a readable flag.
        /// </summary>
        private class MockTransportHandler : TransportHandler
        {
            public bool ProcessMessagesAsyncThrew { get; private set; }
            public int SendAsyncCalls { get; private set; }

            public MockTransportHandler(CosmosClient client) : base(client)
            {
            }

            public override async Task<ResponseMessage> SendAsync(
                RequestMessage request,
                CancellationToken cancellationToken)
            {
                this.ProcessMessagesAsyncThrew = false;
                this.SendAsyncCalls++;
                try
                {
                    using (new ActivityScope(Guid.NewGuid()))
                    {
                        return await base.ProcessMessageAsync(request, cancellationToken);
                    }
                }
                catch (DocumentClientException)
                {
                    this.ProcessMessagesAsyncThrew = true;
                    throw;
                }
            }
        }
    }
}