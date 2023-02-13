﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="GatewayAddressCache"/>.
    /// </summary>
    [TestClass]
    public class GatewayAddressCacheTests
    {
        private const string DatabaseAccountApiEndpoint = "https://endpoint.azure.com";
        private readonly Mock<ICosmosAuthorizationTokenProvider> mockTokenProvider;
        private readonly Mock<IServiceConfigurationReader> mockServiceConfigReader;
        private readonly Mock<PartitionKeyRangeCache> partitionKeyRangeCache;
        private readonly int targetReplicaSetSize = 4;
        private readonly PartitionKeyRangeIdentity testPartitionKeyRangeIdentity;
        private readonly ServiceIdentity serviceIdentity;
        private readonly Uri serviceName;

        public GatewayAddressCacheTests()
        {
            this.mockTokenProvider = new Mock<ICosmosAuthorizationTokenProvider>();
            this.mockTokenProvider.Setup(foo => foo.GetUserAuthorizationTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Documents.Collections.INameValueCollection>(), It.IsAny<AuthorizationTokenType>(), It.IsAny<ITrace>()))
                .Returns(new ValueTask<string>("token!"));
            this.mockServiceConfigReader = new Mock<IServiceConfigurationReader>();
            this.mockServiceConfigReader.Setup(foo => foo.SystemReplicationPolicy).Returns(new ReplicationPolicy() { MaxReplicaSetSize = this.targetReplicaSetSize });
            this.mockServiceConfigReader.Setup(foo => foo.UserReplicationPolicy).Returns(new ReplicationPolicy() { MaxReplicaSetSize = this.targetReplicaSetSize });
            this.testPartitionKeyRangeIdentity = new PartitionKeyRangeIdentity("YxM9ANCZIwABAAAAAAAAAA==", "YxM9ANCZIwABAAAAAAAAAA==");
            this.serviceName = new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint);
            this.serviceIdentity = new ServiceIdentity("federation1", this.serviceName, false);

            List<PartitionKeyRange> partitionKeyRanges = new ()
            {
                new PartitionKeyRange()
                {
                    MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    Id = "0"
                }
            };

            this.partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, null, null);
            this.partitionKeyRangeCache
                .Setup(m => m.TryGetOverlappingRangesAsync(
                    It.IsAny<string>(),
                    It.IsAny<Documents.Routing.Range<string>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<bool>()))
                .Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)partitionKeyRanges));
        }

        [TestMethod]
        public void TestGatewayAddressCacheAutoRefreshOnSuboptimalPartition()
        {
            FakeMessageHandler messageHandler = new FakeMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            GatewayAddressCache cache = new GatewayAddressCache(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: null,
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
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: null,
                suboptimalPartitionForceRefreshIntervalInSeconds: 2,
                enableTcpConnectionEndpointRediscovery: true);

            PartitionAddressInformation addresses = await cache.TryGetAddressesAsync(
             DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
             this.testPartitionKeyRangeIdentity,
             this.serviceIdentity,
             false,
             CancellationToken.None);

            Assert.IsNotNull(addresses.AllAddresses.Select(address => address.PhysicalUri == "https://blabla.com"));

            // call updateAddress
            cache.TryRemoveAddresses(new Documents.Rntbd.ServerKey(new Uri("https://blabla.com")));

            // check if the addresss is updated
            addresses = await cache.TryGetAddressesAsync(
             DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid),
             this.testPartitionKeyRangeIdentity,
             this.serviceIdentity,
             false,
             CancellationToken.None);

            Assert.IsNotNull(addresses.AllAddresses.Select(address => address.PhysicalUri == "https://blabla5.com"));
        }

        [TestMethod]
        public async Task TestGatewayAddressCacheAvoidCacheRefresWhenAlreadyUpdatedAsync()
        {
            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);
            string oldAddress = "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/4s";
            string newAddress = "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/5s";
            mockHttpHandler.SetupSequence(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
                 .Returns(MockCosmosUtil.CreateHttpResponseOfAddresses(new List<string>()
                 {
                     "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/1p",
                     "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/2s",
                     "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/3s",
                     oldAddress,
                 }))
                 .Returns(MockCosmosUtil.CreateHttpResponseOfAddresses(new List<string>()
                 {
                     "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/1p",
                     "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/2s",
                     "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/3s",
                     newAddress,
                 }));

            HttpClient httpClient = new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object));
            GatewayAddressCache cache = new GatewayAddressCache(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: null,
                suboptimalPartitionForceRefreshIntervalInSeconds: 2,
                enableTcpConnectionEndpointRediscovery: true);

            DocumentServiceRequest request1 = DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid);
            DocumentServiceRequest request2 = DocumentServiceRequest.Create(OperationType.Invalid, ResourceType.Address, AuthorizationTokenType.Invalid);

            PartitionAddressInformation request1Addresses = await cache.TryGetAddressesAsync(
                request: request1,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: false,
                cancellationToken: CancellationToken.None);

            PartitionAddressInformation request2Addresses = await cache.TryGetAddressesAsync(
                request: request2,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: false,
                cancellationToken: CancellationToken.None);

            Assert.AreEqual(request1Addresses, request2Addresses);
            Assert.AreEqual(4, request1Addresses.AllAddresses.Count());
            Assert.AreEqual(1, request1Addresses.AllAddresses.Count(x => x.PhysicalUri == oldAddress));
            Assert.AreEqual(0, request1Addresses.AllAddresses.Count(x => x.PhysicalUri == newAddress));

            // check if the addresss is updated
            request1Addresses = await cache.TryGetAddressesAsync(
                request: request1,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: true,
                cancellationToken: CancellationToken.None);

            // Even though force refresh is true it will just use the new cache
            // value rather than doing a gateway call to do another refresh since the value
            // already changed from the last cache access
            request2Addresses = await cache.TryGetAddressesAsync(
                request: request2,
                partitionKeyRangeIdentity: this.testPartitionKeyRangeIdentity,
                serviceIdentity: this.serviceIdentity,
                forceRefreshPartitionAddresses: true,
                cancellationToken: CancellationToken.None);

            Assert.AreEqual(request1Addresses, request2Addresses);
            Assert.AreEqual(4, request1Addresses.AllAddresses.Count());
            Assert.AreEqual(0, request1Addresses.AllAddresses.Count(x => x.PhysicalUri == oldAddress));
            Assert.AreEqual(1, request1Addresses.AllAddresses.Count(x => x.PhysicalUri == newAddress));

            mockHttpHandler.VerifyAll();
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
                    UserAgentContainer container = new UserAgentContainer(clientId: 0);
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
                        httpClient: MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

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
            HttpClient httpClient = new(messageHandler);
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            GatewayAddressCache cache = new GatewayAddressCache(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: null,
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

        /// <summary>
        /// Test to validate that when <see cref="GatewayAddressCache.OpenConnectionsAsync()"/> is called with a
        /// valid open connection handler, the handler method is indeed invoked.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenConnectionsAsync_WithValidOpenConnectionHandler_ShouldInvokeHandlerMethod()
        {
            // Arrange.
            FakeMessageHandler messageHandler = new ();
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new (failingIndexes: new HashSet<int>());
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProperties.Id = "TestId";
            containerProperties.PartitionKeyPath = "/pk";
            HttpClient httpClient = new(messageHandler)
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            GatewayAddressCache cache = new (
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: fakeOpenConnectionHandler,
                suboptimalPartitionForceRefreshIntervalInSeconds: 2);

            // Act.
            await cache.OpenConnectionsAsync(
                databaseName: "test-database",
                collection: containerProperties,
                partitionKeyRangeIdentities: new List<PartitionKeyRangeIdentity>()
                {
                    this.testPartitionKeyRangeIdentity
                },
                shouldOpenRntbdChannels: true,
                cancellationToken: CancellationToken.None);

            // Assert.
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 0,
                expectedMethodInvocationCount: 1,
                expectedReceivedAddressesCount: 3,
                expectedSuccessCount: 3);
        }

        /// <summary>
        /// Test to validate that when <see cref="GatewayAddressCache.OpenConnectionsAsync()"/> is invoked with a
        /// open connection handler that throws an exception, the handler method is indeed invoked
        /// and the exception is handled in such a way that the cosmos client initialization does not fail.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenConnectionsAsync_WhenConnectionHandlerThrowsException_ShouldNotFailInitialization()
        {
            // Arrange.
            FakeMessageHandler messageHandler = new ();
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new(failingIndexes: new HashSet<int>() { 0, 1, 2});
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProperties.Id = "TestId";
            containerProperties.PartitionKeyPath = "/pk";
            HttpClient httpClient = new(messageHandler);

            GatewayAddressCache cache = new (
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: fakeOpenConnectionHandler,
                suboptimalPartitionForceRefreshIntervalInSeconds: 2);

            // Act.
            await cache.OpenConnectionsAsync(
                databaseName: "test-database",
                collection: containerProperties,
                partitionKeyRangeIdentities: new List<PartitionKeyRangeIdentity>()
                {
                    this.testPartitionKeyRangeIdentity
                },
                shouldOpenRntbdChannels: true,
                cancellationToken: CancellationToken.None);

            // Assert.
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 3,
                expectedMethodInvocationCount: 1,
                expectedReceivedAddressesCount: 3,
                expectedSuccessCount: 0);
        }

        /// <summary>
        /// Test to validate that when <see cref="GatewayAddressCache.OpenConnectionsAsync()"/> is invoked with a null
        /// open connection handler, the handler method is never invoked, thus no attempt to open connections
        /// to the backend replica happens.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenConnectionsAsync_WithNullOpenConnectionHandler_ShouldNotInvokeHandlerMethod()
        {
            // Arrange.
            FakeMessageHandler messageHandler = new ();
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new (failingIndexes: new HashSet<int>());
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProperties.Id = "TestId";
            containerProperties.PartitionKeyPath = "/pk";
            HttpClient httpClient = new(messageHandler)
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            GatewayAddressCache cache = new(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                MockCosmosUtil.CreateCosmosHttpClient(() => httpClient),
                openConnectionsHandler: null,
                suboptimalPartitionForceRefreshIntervalInSeconds: 2);

            // Act.
            await cache.OpenConnectionsAsync(
                databaseName: "test-database",
                collection: containerProperties,
                partitionKeyRangeIdentities: new List<PartitionKeyRangeIdentity>()
                {
                    this.testPartitionKeyRangeIdentity
                },
                shouldOpenRntbdChannels: true,
                cancellationToken: CancellationToken.None);

            // Assert.
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 0,
                expectedMethodInvocationCount: 0,
                expectedReceivedAddressesCount: 0,
                expectedSuccessCount: 0);
        }

        /// <summary>
        /// Test to validate that when <see cref="GlobalAddressResolver.OpenConnectionsToAllReplicasAsync()"/> is called with a
        /// valid open connection handler, the handler method is indeed invoked and an attempt is made to open
        /// the connections to the backend replicas.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task GlobalAddressResolver_OpenConnectionsToAllReplicasAsync_WithValidHandler_ShouldOpenConnectionsToBackend()
        {
            // Arrange.
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new (failingIndexes: new HashSet<int>());
            UserAgentContainer container = new (clientId: 0);
            FakeMessageHandler messageHandler = new ();
            AccountProperties databaseAccount = new ();

            Mock<IDocumentClientInternal> mockDocumentClient = new ();
            mockDocumentClient
                .Setup(owner => owner.ServiceEndpoint)
                .Returns(new Uri("https://blabla.com/"));

            mockDocumentClient
                .Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            GlobalEndpointManager globalEndpointManager = new (
                mockDocumentClient.Object,
                new ConnectionPolicy());
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache = new GlobalPartitionEndpointManagerCore(globalEndpointManager);

            ConnectionPolicy connectionPolicy = new ()
            {
                RequestTimeout = TimeSpan.FromSeconds(120)
            };

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProperties.Id = "TestId";
            containerProperties.PartitionKeyPath = "/pk";

            Mock<CollectionCache> mockCollectionCahce = new (MockBehavior.Strict);
            mockCollectionCahce
                .Setup(x => x.ResolveByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(containerProperties));

            GlobalAddressResolver globalAddressResolver = new (
                endpointManager: globalEndpointManager,
                partitionKeyRangeLocationCache: partitionKeyRangeLocationCache,
                protocol: Documents.Client.Protocol.Tcp,
                tokenProvider: this.mockTokenProvider.Object,
                collectionCache: mockCollectionCahce.Object,
                routingMapProvider: this.partitionKeyRangeCache.Object,
                serviceConfigReader: this.mockServiceConfigReader.Object,
                connectionPolicy: connectionPolicy,
                httpClient: MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            globalAddressResolver.SetOpenConnectionsHandler(
                openConnectionsHandler: fakeOpenConnectionHandler);

            // Act.
            await globalAddressResolver.OpenConnectionsToAllReplicasAsync(
                databaseName: "test-db",
                containerLinkUri: "https://test.uri.cosmos.com",
                CancellationToken.None);

            // Assert.
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 0,
                expectedMethodInvocationCount: 1,
                expectedReceivedAddressesCount: 3,
                expectedSuccessCount: 3);
        }

        /// <summary>
        /// Test to validate that when <see cref="GlobalAddressResolver.OpenConnectionsToAllReplicasAsync()"/> is called with a
        /// open connection handler that throws an exception, the handler method is indeed invoked and the exception is handled
        /// in such a way that the cosmos client initialization does not fail.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task GlobalAddressResolver_OpenConnectionsToAllReplicasAsync_WhenHandlerDelegateThrowsException_ShouldNotFailInitialization()
        {
            // Arrange.
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new (failingIndexes: new HashSet<int>() { 0, 2});
            UserAgentContainer container = new(clientId: 0);
            FakeMessageHandler messageHandler = new();
            AccountProperties databaseAccount = new();

            Mock<IDocumentClientInternal> mockDocumentClient = new();
            mockDocumentClient
                .Setup(owner => owner.ServiceEndpoint)
                .Returns(new Uri("https://blabla.com/"));

            mockDocumentClient
                .Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            GlobalEndpointManager globalEndpointManager = new(
                mockDocumentClient.Object,
                new ConnectionPolicy());
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache = new GlobalPartitionEndpointManagerCore(globalEndpointManager);

            ConnectionPolicy connectionPolicy = new()
            {
                RequestTimeout = TimeSpan.FromSeconds(120)
            };

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProperties.Id = "TestId";
            containerProperties.PartitionKeyPath = "/pk";

            Mock<CollectionCache> mockCollectionCahce = new(MockBehavior.Strict);
            mockCollectionCahce
                .Setup(x => x.ResolveByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(containerProperties));

            GlobalAddressResolver globalAddressResolver = new(
                endpointManager: globalEndpointManager,
                partitionKeyRangeLocationCache: partitionKeyRangeLocationCache,
                protocol: Documents.Client.Protocol.Tcp,
                tokenProvider: this.mockTokenProvider.Object,
                collectionCache: mockCollectionCahce.Object,
                routingMapProvider: this.partitionKeyRangeCache.Object,
                serviceConfigReader: this.mockServiceConfigReader.Object,
                connectionPolicy: connectionPolicy,
                httpClient: MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            globalAddressResolver.SetOpenConnectionsHandler(
                openConnectionsHandler: fakeOpenConnectionHandler);

            // Act.
            await globalAddressResolver.OpenConnectionsToAllReplicasAsync(
                databaseName: "test-db",
                containerLinkUri: "https://test.uri.cosmos.com",
                CancellationToken.None);

            // Assert.
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 2,
                expectedMethodInvocationCount: 1,
                expectedReceivedAddressesCount: 3,
                expectedSuccessCount: 1);
        }

        /// <summary>
        /// Test to validate that when <see cref="GlobalAddressResolver.OpenConnectionsToAllReplicasAsync()"/> is invoked and
        /// and an internal operation throws an exception which is other than a transport exception, then the exception is indeed
        /// bubbled up and thrown during the cosmos client initialization.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task GlobalAddressResolver_OpenConnectionsToAllReplicasAsync_WhenInternalExceptionThrownApartFromTransportError_ShouldThrowException()
        {
            // Arrange.
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new (failingIndexes: new HashSet<int>());
            UserAgentContainer container = new(clientId: 0);
            FakeMessageHandler messageHandler = new();
            AccountProperties databaseAccount = new();

            Mock<IDocumentClientInternal> mockDocumentClient = new();
            mockDocumentClient
                .Setup(owner => owner.ServiceEndpoint)
                .Returns(new Uri("https://blabla.com/"));

            mockDocumentClient
                .Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            GlobalEndpointManager globalEndpointManager = new (
                mockDocumentClient.Object,
                new ConnectionPolicy());
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache = new GlobalPartitionEndpointManagerCore(globalEndpointManager);

            ConnectionPolicy connectionPolicy = new ()
            {
                RequestTimeout = TimeSpan.FromSeconds(120)
            };

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProperties.Id = "TestId";
            containerProperties.PartitionKeyPath = "/pk";

            Mock<CollectionCache> mockCollectionCahce = new (MockBehavior.Strict);
            mockCollectionCahce
                .Setup(x => x.ResolveByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(containerProperties));

            string exceptionMessage = "Failed to lookup partition key ranges.";
            Mock<PartitionKeyRangeCache> partitionKeyRangeCache = new (null, null, null);
            partitionKeyRangeCache
                .Setup(m => m.TryGetOverlappingRangesAsync(
                    It.IsAny<string>(),
                    It.IsAny<Documents.Routing.Range<string>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(
                    new Exception(exceptionMessage));

            GlobalAddressResolver globalAddressResolver = new(
                endpointManager: globalEndpointManager,
                partitionKeyRangeLocationCache: partitionKeyRangeLocationCache,
                protocol: Documents.Client.Protocol.Tcp,
                tokenProvider: this.mockTokenProvider.Object,
                collectionCache: mockCollectionCahce.Object,
                routingMapProvider: partitionKeyRangeCache.Object,
                serviceConfigReader: this.mockServiceConfigReader.Object,
                connectionPolicy: connectionPolicy,
                httpClient: MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            globalAddressResolver.SetOpenConnectionsHandler(
                openConnectionsHandler: fakeOpenConnectionHandler);

            // Act.
            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => globalAddressResolver.OpenConnectionsToAllReplicasAsync(
                databaseName: "test-db",
                containerLinkUri: "https://test.uri.cosmos.com",
                CancellationToken.None));

            // Assert.
            Assert.IsNotNull(ex);
            Assert.AreEqual(exceptionMessage, ex.Message);
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 0,
                expectedMethodInvocationCount: 0,
                expectedReceivedAddressesCount: 0,
                expectedSuccessCount: 0);
        }

        /// <summary>
        /// Test to validate that when <see cref="GlobalAddressResolver.OpenConnectionsToAllReplicasAsync()"/> is invoked and
        /// no valid collection could be resolved for the given database name and container link uri, thus a null value is
        /// returned, then a <see cref="CosmosException"/> is thrown during the cosmos client initialization.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task GlobalAddressResolver_OpenConnectionsToAllReplicasAsync_WhenNullCollectionReturned_ShouldThrowException()
        {
            // Arrange.
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new (failingIndexes: new HashSet<int>());
            UserAgentContainer container = new (clientId: 0);
            FakeMessageHandler messageHandler = new ();
            AccountProperties databaseAccount = new ();

            Mock<IDocumentClientInternal> mockDocumentClient = new ();
            mockDocumentClient
                .Setup(owner => owner.ServiceEndpoint)
                .Returns(new Uri("https://blabla.com/"));

            mockDocumentClient
                .Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseAccount);

            GlobalEndpointManager globalEndpointManager = new (
                mockDocumentClient.Object,
                new ConnectionPolicy());
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache = new GlobalPartitionEndpointManagerCore(globalEndpointManager);

            ConnectionPolicy connectionPolicy = new ()
            {
                RequestTimeout = TimeSpan.FromSeconds(120)
            };

            Mock<CollectionCache> mockCollectionCahce = new (MockBehavior.Strict);
            mockCollectionCahce
                .Setup(x => x.ResolveByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<ContainerProperties>(null));

            GlobalAddressResolver globalAddressResolver = new (
                endpointManager: globalEndpointManager,
                partitionKeyRangeLocationCache: partitionKeyRangeLocationCache,
                protocol: Documents.Client.Protocol.Tcp,
                tokenProvider: this.mockTokenProvider.Object,
                collectionCache: mockCollectionCahce.Object,
                routingMapProvider: this.partitionKeyRangeCache.Object,
                serviceConfigReader: this.mockServiceConfigReader.Object,
                connectionPolicy: connectionPolicy,
                httpClient: MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(messageHandler)));

            globalAddressResolver.SetOpenConnectionsHandler(
                openConnectionsHandler: fakeOpenConnectionHandler);

            // Act.
            CosmosException ce = await Assert.ThrowsExceptionAsync<CosmosException>(() => globalAddressResolver.OpenConnectionsToAllReplicasAsync(
                databaseName: "test-db",
                containerLinkUri: "https://test.uri.cosmos.com",
                CancellationToken.None));

            // Assert.
            Assert.IsNotNull(ce);
            Assert.IsTrue(ce.Message.Contains("Could not resolve the collection"));
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 0,
                expectedMethodInvocationCount: 0,
                expectedReceivedAddressesCount: 0,
                expectedSuccessCount: 0);
        }

        /// <summary>
        /// Test to validate that when <see cref="GatewayAddressCache.OpenConnectionsAsync()"/> is called with a
        /// valid open connection handler and some of the address resolving fails with exception, then the
        /// GatewayAddressCache should ignore the failed addresses and the handler method is indeed invoked
        /// for all resolved addresses.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenConnectionsAsync_WhenSomeAddressResolvingFailsWithException_ShouldIgnoreExceptionsAndInvokeHandlerMethodForOtherAddresses()
        {
            // Arrange.
            FakeMessageHandler messageHandler = new ();
            FakeOpenConnectionHandler fakeOpenConnectionHandler = new (failingIndexes: new HashSet<int>());
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProperties.Id = "TestId";
            containerProperties.PartitionKeyPath = "/pk";
            List<PartitionKeyRangeIdentity> partitionKeyRangeIdentities = Enumerable.Repeat(this.testPartitionKeyRangeIdentity, 70).ToList();

            List<Address> addresses = new ()
            {
                new Address() { IsPrimary = true, PhysicalUri = "https://blabla.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                new Address() { IsPrimary = false, PhysicalUri = "https://blabla3.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                new Address() { IsPrimary = false, PhysicalUri = "https://blabla2.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                new Address() { IsPrimary = false, PhysicalUri = "https://blabla4.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                new Address() { IsPrimary = false, PhysicalUri = "https://blabla5.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" }
            };

            FeedResource<Address> addressFeedResource = new ()
            {
                Id = "YxM9ANCZIwABAAAAAAAAAA==",
                SelfLink = "dbs/YxM9AA==/colls/YxM9ANCZIwA=/docs/YxM9ANCZIwABAAAAAAAAAA==/",
                Timestamp = DateTime.Now,
                InnerCollection = new Collection<Address>(addresses),
            };

            StringBuilder feedResourceString = new ();
            addressFeedResource.SaveTo(feedResourceString);

            StringContent content = new (feedResourceString.ToString());
            HttpResponseMessage responseMessage = new ()
            {
                StatusCode = HttpStatusCode.OK,
                Content = content,
            };

            Mock<CosmosHttpClient> mockHttpClient = new ();
            mockHttpClient.SetupSequence(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.Collections.INameValueCollection>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<HttpTimeoutPolicy>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Some random error occurred."))
                .ReturnsAsync(responseMessage);

            GatewayAddressCache cache = new(
                new Uri(GatewayAddressCacheTests.DatabaseAccountApiEndpoint),
                Documents.Client.Protocol.Tcp,
                this.mockTokenProvider.Object,
                this.mockServiceConfigReader.Object,
                mockHttpClient.Object,
                openConnectionsHandler: fakeOpenConnectionHandler,
                suboptimalPartitionForceRefreshIntervalInSeconds: 2);

            // Act.
            await cache.OpenConnectionsAsync(
                databaseName: "test-database",
                collection: containerProperties,
                partitionKeyRangeIdentities: partitionKeyRangeIdentities,
                shouldOpenRntbdChannels: true,
                cancellationToken: CancellationToken.None);

            // Assert.
            GatewayAddressCacheTests.AssertOpenConnectionHandlerAttributes(
                fakeOpenConnectionHandler: fakeOpenConnectionHandler,
                expectedExceptionCount: 0,
                expectedMethodInvocationCount: 1,
                expectedReceivedAddressesCount: addresses.Count,
                expectedSuccessCount: addresses.Count);
        }

        /// <summary>
        /// Helper method to assert on the <see cref="FakeOpenConnectionHandler"/> class attributes
        /// to match with that of the expected ones.
        /// </summary>
        /// <param name="fakeOpenConnectionHandler">An instance of the <see cref="FakeOpenConnectionHandler"/>.</param>
        /// <param name="expectedExceptionCount">The expected exception count for the test.</param>
        /// <param name="expectedMethodInvocationCount">The expected method invocation count for the test.</param>
        /// <param name="expectedReceivedAddressesCount">The expected received addresses count for the test.</param>
        /// <param name="expectedSuccessCount">The expected successful messages count for the test.</param>
        private static void AssertOpenConnectionHandlerAttributes(
            FakeOpenConnectionHandler fakeOpenConnectionHandler,
            int expectedExceptionCount,
            int expectedMethodInvocationCount,
            int expectedReceivedAddressesCount,
            int expectedSuccessCount)
        {
            Assert.AreEqual(expectedExceptionCount, fakeOpenConnectionHandler.GetExceptionCount());
            Assert.AreEqual(expectedMethodInvocationCount, fakeOpenConnectionHandler.GetMethodInvocationCount());
            Assert.AreEqual(expectedReceivedAddressesCount, fakeOpenConnectionHandler.GetReceivedAddressesCount());
            Assert.AreEqual(expectedSuccessCount, fakeOpenConnectionHandler.GetSuccessfulInvocationCount());
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
                    new Address() { IsPrimary = true, PhysicalUri = "https://blabla.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                    new Address() { IsPrimary = false, PhysicalUri = "https://blabla3.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                    new Address() { IsPrimary = false, PhysicalUri = "https://blabla2.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" },
                };

                if (this.returnFullReplicaSet)
                {
                    addresses.Add(new Address() { IsPrimary = false, PhysicalUri = "https://blabla4.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" });
                    this.returnFullReplicaSet = false;
                }
                else
                {
                    this.returnFullReplicaSet = true;
                }

                if (this.returnUpdatedAddresses)
                {
                    addresses.RemoveAll(address => address.IsPrimary == true);
                    addresses.Add(new Address() { IsPrimary = true, PhysicalUri = "https://blabla5.com", Protocol = RuntimeConstants.Protocols.RNTBD, PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA==" });
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
            private readonly object locker = new object();
            public override void Post(SendOrPostCallback d, object state)
            {
                lock (this.locker)
                {
                    d(state);
                }
            }
        }

        public class FakeOpenConnectionHandler : IOpenConnectionsHandler
        {
            private int exceptionCounter = 0;
            private int methodInvocationCounter = 0;
            private int successInvocationCounter = 0;
            private int totalReceivedAddressesCounter = 0;
            private readonly HashSet<int> failingIndexes;
            private readonly bool useAttemptBasedFailingIndexs;
            private readonly ManualResetEvent manualResetEvent;
            private readonly Dictionary<int, HashSet<int>> failIndexesByAttempts;

            public FakeOpenConnectionHandler(
                HashSet<int> failingIndexes,
                ManualResetEvent manualResetEvent = null)
            {
                this.failingIndexes = failingIndexes;
                this.manualResetEvent = manualResetEvent;
            }

            public FakeOpenConnectionHandler(
                Dictionary<int, HashSet<int>> failIndexesByAttempts,
                ManualResetEvent manualResetEvent = null)
            {
                this.useAttemptBasedFailingIndexs = true;
                this.failIndexesByAttempts = failIndexesByAttempts;
                this.manualResetEvent = manualResetEvent;
            }

            public int GetSuccessfulInvocationCount()
            {
                return this.successInvocationCounter;
            }

            public int GetExceptionCount()
            {
                return this.exceptionCounter;
            }

            public int GetReceivedAddressesCount()
            {
                return this.totalReceivedAddressesCounter;
            }

            public int GetMethodInvocationCount()
            {
                return this.methodInvocationCounter;
            }

            Task IOpenConnectionsHandler.TryOpenRntbdChannelsAsync(
                IReadOnlyList<TransportAddressUri> addresses)
            {
                this.totalReceivedAddressesCounter = addresses.Count;
                for (int i = 0; i < addresses.Count; i++)
                {
                    if (this.useAttemptBasedFailingIndexs)
                    {
                        if (this.failIndexesByAttempts.ContainsKey(i) && this.failIndexesByAttempts[i].Contains(this.methodInvocationCounter))
                        {
                            this.ExecuteFailureCondition(
                                addresses: addresses,
                                index: i);
                        }
                        else
                        {
                            this.ExecuteSuccessCondition(
                                addresses: addresses,
                                index: i);
                        }
                    }
                    else
                    {
                        if (this.failingIndexes.Contains(i))
                        {
                            this.ExecuteFailureCondition(
                                addresses: addresses,
                                index: i);
                        }
                        else
                        {
                            this.ExecuteSuccessCondition(
                                addresses: addresses,
                                index: i);
                        }
                    }
                }

                this.methodInvocationCounter++;
                this.manualResetEvent?.Set();
                return Task.CompletedTask;
            }

            private void ExecuteSuccessCondition(
                IReadOnlyList<TransportAddressUri> addresses,
                int index)
            {
                addresses[index].SetConnected();
                this.successInvocationCounter++;
            }

            private void ExecuteFailureCondition(
                IReadOnlyList<TransportAddressUri> addresses,
                int index)
            {
                addresses[index].SetUnhealthy();
                this.exceptionCounter++;
            }
        }
    }
}
