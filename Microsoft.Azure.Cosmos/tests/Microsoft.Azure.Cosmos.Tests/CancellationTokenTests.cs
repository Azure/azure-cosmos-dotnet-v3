//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for <see cref="CancellationToken"/>  scenarios.
    /// </summary>
    [TestClass]
    public class CancellationTokenTests
    {
        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task GatewayProcessMessageAsyncCancels()
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                CancellationToken cancellationToken = source.Token;

                int run = 0;
                async Task<HttpResponseMessage> sendFunc(HttpRequestMessage request)
                {
                    string content = await request.Content.ReadAsStringAsync();
                    Assert.AreEqual("content1", content);

                    if (run == 0)
                    {
                        // We force a retry but cancel the token to verify if the retry mechanism cancels inbetween
                        source.Cancel();
                        throw new WebException("", WebExceptionStatus.ConnectFailure);
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Response") };
                }

                Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

                using GlobalEndpointManager endpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
                ISessionContainer sessionContainer = new SessionContainer(string.Empty);
                DocumentClientEventSource eventSource = DocumentClientEventSource.Instance;
                HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
                using GatewayStoreModel storeModel = new GatewayStoreModel(
                    endpointManager,
                    sessionContainer,
                    ConsistencyLevel.Eventual,
                    eventSource,
                    null,
                    MockCosmosUtil.CreateCosmosHttpClient(
                        () => new HttpClient(messageHandler),
                        eventSource));

                using (new ActivityScope(Guid.NewGuid()))
                {
                    using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        Documents.OperationType.Query,
                        Documents.ResourceType.Document,
                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
                    {
                        await storeModel.ProcessMessageAsync(request, cancellationToken);
                    }
                }

                Assert.Fail();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task GatewayProcessMessageAsyncCancelsOnDeadline()
        {
            // Cancellation deadline is before Request timeout
            using (CancellationTokenSource source = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                CancellationToken cancellationToken = source.Token;

                static async Task<HttpResponseMessage> sendFunc(HttpRequestMessage request)
                {
                    string content = await request.Content.ReadAsStringAsync();
                    Assert.AreEqual("content1", content);

                    // Wait until CancellationTokenSource deadline expires
                    Thread.Sleep(2000);
                    // Force retries
                    throw new WebException("", WebExceptionStatus.ConnectFailure);
                }

                GatewayStoreModel storeModel = MockGatewayStoreModel(sendFunc);

                using (new ActivityScope(Guid.NewGuid()))
                {
                    using (DocumentServiceRequest request =
                    DocumentServiceRequest.Create(
                        Documents.OperationType.Query,
                        Documents.ResourceType.Document,
                        new Uri("https://foo.com/dbs/db1/colls/coll1", UriKind.Absolute),
                        new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                        AuthorizationTokenType.PrimaryMasterKey,
                        null))
                    {
                        await storeModel.ProcessMessageAsync(request, cancellationToken);
                    }
                }
                Assert.Fail();
            }
        }

        /// <summary>
        /// This test verifies that if an HttpRequestException happens during an address resolution, the region is marked unavailable even if the user cancellationtoken is canceled.
        /// 
        /// 1. Get Addresses from AddressResolver (forceRefresh false)
        /// 2. Do TCP requests calling the lambda -> Throws 410
        /// 3. ReplicatedResourceClient + StoreClient handle the 410 by issueing an Address refresh
        /// 4. AddressResolver throws HttpRequestException (forceRefresh true) simulating an HTTP connection error and cancels the token.
        /// 5. ClientRetryPolicy should get it, mark the region unavailable on the GlobalEndpointManager.
        /// 6. Initiate retry, which should be canceled because the token was canceled.
        /// </summary>
        [DataTestMethod]
        [Timeout(5000)]
        [DataRow(ConsistencyLevel.Eventual)]
        [DataRow(ConsistencyLevel.Session)]
        public async Task CancellationTokenDoesNotCancelFailover(ConsistencyLevel consistencyLevel)
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                CancellationToken cancellationToken = source.Token;

                Task<HttpResponseMessage> sendFunc(HttpRequestMessage request)
                {
                    Assert.Fail("There should be no HTTP requests on this test, address resolution requests are handled by the mocked AddressResolver.");
                    throw new InvalidOperationException();
                }

                // TCP requests return 410 with TransportException to force address resolution
                int tcpRequests = 0;
                StoreResponse sendDirectFunc(Uri uri, ResourceOperation resourceOperation, DocumentServiceRequest request)
                {
                    if (++tcpRequests > 1)
                    {
                        Assert.Fail("There should only be 1 TCP request that triggers an address resolution.");
                    }

                    throw new GoneException(new TransportException(TransportErrorCode.ConnectFailed, null, Guid.NewGuid(), new Uri("http://one.com"), "description", true, true), SubStatusCodes.Unknown);
                }

                using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
                client.DocumentClient.GatewayStoreModel = MockGatewayStoreModel(sendFunc);
                client.DocumentClient.StoreModel = MockServerStoreModel(client.DocumentClient.Session, (Documents.ConsistencyLevel)consistencyLevel, source, sendDirectFunc);

                CosmosOperationCanceledException ex = await Assert.ThrowsExceptionAsync<CosmosOperationCanceledException>(() => client.GetContainer("test", "test").ReadItemAsync<dynamic>("id", partitionKey: new Cosmos.PartitionKey("id"), cancellationToken: cancellationToken),
                    "Should have surfaced OperationCanceledException because the token was canceled.");
                Assert.IsTrue(ex.CancellationToken.IsCancellationRequested);
                Assert.AreEqual(cancellationToken.IsCancellationRequested, ex.CancellationToken.IsCancellationRequested);
                Assert.AreEqual(cancellationToken.CanBeCanceled, ex.CancellationToken.CanBeCanceled);
                Assert.AreEqual(cancellationToken.WaitHandle, ex.CancellationToken.WaitHandle);

                ((MockDocumentClient)client.DocumentClient).MockGlobalEndpointManager.Verify(gep => gep.MarkEndpointUnavailableForRead(It.IsAny<Uri>()), Times.Once, "Should had marked the endpoint unavailable");
                ((MockDocumentClient)client.DocumentClient).MockGlobalEndpointManager.Verify(gep => gep.RefreshLocationAsync(false), Times.Once, "Should had refreshed the account information");

                string expectedHelpLink = "https://aka.ms/cosmosdb-tsg-request-timeout";
                string expectedCancellationTokenStatus = $"Cancellation Token has expired: {cancellationToken.IsCancellationRequested}";
                Assert.IsTrue(ex.Message.Contains(expectedHelpLink));
                Assert.IsTrue(ex.Message.Contains(expectedCancellationTokenStatus));
                Assert.IsTrue(ex.ToString().Contains(expectedHelpLink));
                Assert.IsTrue(ex.ToString().Contains(expectedCancellationTokenStatus));
            }
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

        private static GatewayStoreModel MockGatewayStoreModel(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendFunc)
        {
            Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
            mockDocumentClient.Setup(client => client.ServiceEndpoint).Returns(new Uri("https://foo"));

            Mock<GlobalEndpointManager> endpointManager = new Mock<GlobalEndpointManager>(mockDocumentClient.Object, new ConnectionPolicy());
            endpointManager.Setup(gep => gep.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(new Uri("http://localhost"));
            ISessionContainer sessionContainer = new SessionContainer(string.Empty);
            HttpMessageHandler messageHandler = new MockMessageHandler(sendFunc);
            return new GatewayStoreModel(
                endpointManager.Object,
                sessionContainer,
                Cosmos.ConsistencyLevel.Eventual,
                new DocumentClientEventSource(),
                new JsonSerializerSettings(),
                MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));
        }

        // Creates a StoreModel that will return addresses for normal requests and throw for address refresh
        private static ServerStoreModel MockServerStoreModel(
            object sessionContainer,
            Documents.ConsistencyLevel consistencyLevel,
            CancellationTokenSource cancellationTokenSource,
            Func<Uri, ResourceOperation, DocumentServiceRequest, StoreResponse> sendDirectFunc)
        {
            MockTransportClient mockTransportClient = new MockTransportClient(sendDirectFunc);

            AddressInformation[] addressInformation = GetMockAddressInformation();

            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();

            // Return mocked address for initial request
            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.Is<bool>(forceRefresh => forceRefresh == false),
                    It.IsAny<CancellationToken>()))
                    .Callback((DocumentServiceRequest dsr, bool refresh, CancellationToken ct) => dsr.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" })
                    .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            // Return HttpRequestException for address refresh
            int refreshes = 0;
            mockAddressCache.Setup(
                cache => cache.ResolveAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.Is<bool>(forceRefresh => forceRefresh == true),
                    It.IsAny<CancellationToken>()))
                    .Callback((DocumentServiceRequest dsr, bool refresh, CancellationToken ct) =>
                    {
                        dsr.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange() { Id = "0" };
                        // First refresh comes from a background task
                        if (++refreshes > 1)
                        {
                            cancellationTokenSource.Cancel();
                            throw new HttpRequestException();
                        }
                    })
                    .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            ReplicationPolicy replicationPolicy = new ReplicationPolicy
            {
                MaxReplicaSetSize = 1
            };
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);
            mockServiceConfigReader.SetupGet(x => x.DefaultConsistencyLevel).Returns(consistencyLevel);

            return new ServerStoreModel(new StoreClient(
                        mockAddressCache.Object,
                        (SessionContainer)sessionContainer,
                        mockServiceConfigReader.Object,
                        mockAuthorizationTokenProvider.Object,
                        Documents.Client.Protocol.Tcp,
                        mockTransportClient));
        }

        private class MockTransportClient : TransportClient
        {
            private readonly Func<Uri, ResourceOperation, DocumentServiceRequest, StoreResponse> sendDirectFunc;
            public MockTransportClient(Func<Uri, ResourceOperation, DocumentServiceRequest, StoreResponse> sendDirectFunc)
            {
                this.sendDirectFunc = sendDirectFunc;
            }

            internal override Task<StoreResponse> InvokeStoreAsync(Uri physicalAddress, ResourceOperation resourceOperation, DocumentServiceRequest request)
            {
                return Task.FromResult(this.sendDirectFunc(physicalAddress, resourceOperation, request));
            }
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
    }
}