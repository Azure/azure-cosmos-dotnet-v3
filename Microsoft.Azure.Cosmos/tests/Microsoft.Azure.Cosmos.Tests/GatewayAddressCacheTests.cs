//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Net;
    using System.Text;
    using System.Collections.ObjectModel;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests;

    /// <summary>
    /// Tests for <see cref="GatewayAddressCache"/>.
    /// </summary>
    [TestClass]
    public class GatewayAddressCacheTests
    {
        private const string DatabaseAccountApiEndpoint = "https://endpoint.azure.com";
        private Mock<IAuthorizationTokenProvider> mockTokenProvider;
        private Mock<IServiceConfigurationReader> mockServiceConfigReader;
        private int targetReplicaSetSize = 4;
        private PartitionKeyRangeIdentity testPartitionKeyRangeIdentity;
        private ServiceIdentity serviceIdentity;
        private Uri serviceName;

        public GatewayAddressCacheTests()
        {
            this.mockTokenProvider = new Mock<IAuthorizationTokenProvider>();
            this.mockTokenProvider.Setup(foo => foo.GetUserAuthorizationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Documents.Collections.INameValueCollection>(), It.IsAny<AuthorizationTokenType>()))
                .Returns(new ValueTask<(string, string)>(("token!", null)));
            this.mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            this.mockServiceConfigReader.Setup(foo => foo.SystemReplicationPolicy).Returns(new ReplicationPolicy() { MaxReplicaSetSize = this.targetReplicaSetSize });
            this.mockServiceConfigReader.Setup(foo => foo.UserReplicationPolicy).Returns(new ReplicationPolicy() { MaxReplicaSetSize = this.targetReplicaSetSize });
            this.testPartitionKeyRangeIdentity = new PartitionKeyRangeIdentity("YxM9ANCZIwABAAAAAAAAAA==", "YxM9ANCZIwABAAAAAAAAAA==");
            this.serviceName = new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint);
            this.serviceIdentity = new ServiceIdentity("federation1", this.serviceName, false);
        }

        [TestMethod]
        public void TestGatewayAddressCacheAutoRefreshOnSuboptimalPartition()
        {
            FakeMessageHandler messageHandler = new FakeMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            GatewayAddressCache cache = new GatewayAddressCache(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Https,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                suboptimalPartitionForceRefreshIntervalInSeconds: 2);

            int initialAddressesCount = cache.TryGetAddressesAsync(
                DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
                this.testPartitionKeyRangeIdentity,
                this.serviceIdentity,
                false,
                CancellationToken.None).Result.AllAddresses.Count();

            Assert.IsTrue(initialAddressesCount < this.targetReplicaSetSize);

            Task.Delay(3000).Wait();

            int finalAddressCount = cache.TryGetAddressesAsync(
                DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
                this.testPartitionKeyRangeIdentity,
                this.serviceIdentity,
                false,
                CancellationToken.None).Result.AllAddresses.Count();

            Assert.IsTrue(finalAddressCount == this.targetReplicaSetSize);
        }

        [TestMethod]
        public async Task TestGatewayAddressCacheUpdateOnConnectionResetAsync()
        {
            FakeMessageHandler messageHandler = new FakeMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            GatewayAddressCache cache = new GatewayAddressCache(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Https,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                suboptimalPartitionForceRefreshIntervalInSeconds: 2,
                enableTcpConnectionEndpointRediscovery: true);

            PartitionAddressInformation addresses = cache.TryGetAddressesAsync(
             DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
             this.testPartitionKeyRangeIdentity,
             this.serviceIdentity,
             false,
             CancellationToken.None).Result;

            Assert.IsNotNull(addresses.AllAddresses.Select(address => address.PhysicalUri == "https://blabla.com"));

            // call updateAddress
            await cache.TryRemoveAddressesAsync(new Documents.Rntbd.ServerKey(new Uri("https://blabla.com")), CancellationToken.None);

            // check if the addresss is updated
            addresses = cache.TryGetAddressesAsync(
             DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
             this.testPartitionKeyRangeIdentity,
             this.serviceIdentity,
             false,
             CancellationToken.None).Result;

            Assert.IsNotNull(addresses.AllAddresses.Select(address => address.PhysicalUri == "https://blabla5.com"));
        }

        [TestMethod]
        [Timeout(2000)]
        public void GlobalAddressResolverUpdateAsyncSynchronizationTest()
        {
            SynchronizationContext prevContext = SynchronizationContext.Current;
            try
            {
                TestSynchronizationContext syncContext = new TestSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
                syncContext.Post(_ =>
                {
                    UserAgentContainer container = new UserAgentContainer();
                    FakeMessageHandler messageHandler = new FakeMessageHandler();

                    AccountProperties databaseAccount = new AccountProperties();
                    Mock<IDocumentClientInternal> mockDocumentClient = new Mock<IDocumentClientInternal>();
                    mockDocumentClient.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://blabla.com/"));
                    mockDocumentClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(databaseAccount);

                    GlobalEndpointManager globalEndpointManager = new GlobalEndpointManager(mockDocumentClient.Object, new ConnectionPolicy());
                    GlobalPartitionEndpointManager partitionKeyRangeLocationCache = new GlobalPartitionEndpointManagerCore(globalEndpointManager);

                    ConnectionPolicy connectionPolicy = new ConnectionPolicy
                    {
                        RequestTimeout = TimeSpan.FromSeconds(10)
                    };

                    GlobalAddressResolver globalAddressResolver = new GlobalAddressResolver(
                        endpointManager: globalEndpointManager,
                        partitionKeyRangeLocationCache: partitionKeyRangeLocationCache,
                        protocol: Documents.Client.Protocol.Tcp,
                        tokenProvider: this.mockTokenProvider.Object,
                        collectionCache: null,
                        routingMapProvider: null,
                        serviceConfigReader: this.mockServiceConfigReader.Object,
                        connectionPolicy: connectionPolicy,
                        httpClient: MockCosmosUtil.CreateCosmosHttpClient(() =>new HttpClient(messageHandler)));

                    ConnectionStateListener connectionStateListener = new ConnectionStateListener(globalAddressResolver);
                    connectionStateListener.OnConnectionEvent(ConnectionEvent.ReadEof, DateTime.Now, new Documents.Rntbd.ServerKey(new Uri("https://endpoint.azure.com:4040/")));

                }, state: null);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        [TestMethod]
        [Owner("aysarkar")]
        public async Task GatewayAddressCacheInNetworkRequestTestAsync()
        {
            FakeMessageHandler messageHandler = new FakeMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            GatewayAddressCache cache = new GatewayAddressCache(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Https,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                suboptimalPartitionForceRefreshIntervalInSeconds: 2,
                enableTcpConnectionEndpointRediscovery: true);

            // No header should be present.
            PartitionAddressInformation legacyRequest = await cache.TryGetAddressesAsync(
                DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
                this.testPartitionKeyRangeIdentity,
                this.serviceIdentity,
                false,
                CancellationToken.None);


            Assert.IsFalse(legacyRequest.IsLocalRegion);

            // Header indicates the request is from the same azure region.
            messageHandler.Headers[HttpConstants.HttpHeaders.LocalRegionRequest] = "true";
            PartitionAddressInformation inNetworkAddresses = await cache.TryGetAddressesAsync(
                DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
                this.testPartitionKeyRangeIdentity,
                this.serviceIdentity,
                true,
                CancellationToken.None);
            Assert.IsTrue(inNetworkAddresses.IsLocalRegion);

            // Header indicates the request is not from the same azure region.
            messageHandler.Headers[HttpConstants.HttpHeaders.LocalRegionRequest] = "false";
            PartitionAddressInformation outOfNetworkAddresses = await cache.TryGetAddressesAsync(
                DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
                this.testPartitionKeyRangeIdentity,
                this.serviceIdentity,
                true,
                CancellationToken.None);
            Assert.IsFalse(outOfNetworkAddresses.IsLocalRegion);
        }

        private class FakeMessageHandler : HttpMessageHandler
        {
            private bool returnFullReplicaSet;
            private bool returnUpdatedAddresses;

            public Dictionary<string, string> Headers { get; set; }

            public FakeMessageHandler()
            {
                this.returnFullReplicaSet = false;
                this.returnUpdatedAddresses = false;
                this.Headers = new Dictionary<string, string>();
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                List<Address> addresses = new List<Address>()
                {
                    new Address() { IsPrimary = true, PhysicalUri = "https://blabla.com", Protocol = RuntimeConstants.Protocols.HTTPS, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                    new Address() { IsPrimary = false, PhysicalUri = "https://blabla3.com", Protocol = RuntimeConstants.Protocols.HTTPS, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                    new Address() { IsPrimary = false, PhysicalUri = "https://blabla2.com", Protocol = RuntimeConstants.Protocols.HTTPS, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                };

                if (this.returnFullReplicaSet)
                {
                    addresses.Add(new Address() { IsPrimary = false, PhysicalUri = "https://blabla4.com", Protocol = RuntimeConstants.Protocols.HTTPS, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" });
                    this.returnFullReplicaSet = false;
                }
                else
                {
                    this.returnFullReplicaSet = true;
                }

                if (this.returnUpdatedAddresses)
                {
                    addresses.RemoveAll(address => address.IsPrimary == true);
                    addresses.Add(new Address() { IsPrimary = true, PhysicalUri = "https://blabla5.com", Protocol = RuntimeConstants.Protocols.HTTPS, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" });
                    this.returnUpdatedAddresses = false;
                }
                else
                {
                    this.returnUpdatedAddresses = true;
                }

                FeedResource<Address> addressFeedResource = new FeedResource<Address>()
                {
                    Id = "YxM9ANCZIwABAAAAAAAAAA==",
                    SelfLink = "dbs/YxM9AA==/colls/YxM9ANCZIwA=/docs/YxM9ANCZIwABAAAAAAAAAA==/",
                    Timestamp = DateTime.Now,
                    InnerCollection = new Collection<Address>(addresses),
                };

                StringBuilder feedResourceString = new StringBuilder();
                addressFeedResource.SaveTo(feedResourceString);

                StringContent content = new StringContent(feedResourceString.ToString());
                HttpResponseMessage responseMessage = new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = content,
                };

                if (this.Headers != null)
                {
                    foreach (KeyValuePair<string, string> headerPair in this.Headers)
                    {
                        responseMessage.Headers.Add(headerPair.Key, headerPair.Value);
                    }
                }

                return Task.FromResult<HttpResponseMessage>(responseMessage);
            }
        }

        public class TestSynchronizationContext : SynchronizationContext
        {
            private object locker = new object();
            public override void Post(SendOrPostCallback d, object state)
            {
                lock (this.locker)
                {
                    d(state);
                }
            }
        }
    }
}
