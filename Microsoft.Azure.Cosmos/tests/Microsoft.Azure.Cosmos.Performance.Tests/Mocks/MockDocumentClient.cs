//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Moq;

    internal class MockDocumentClient : DocumentClient, IAuthorizationTokenProvider
    {
        Mock<ClientCollectionCache> collectionCache;
        Mock<PartitionKeyRangeCache> partitionKeyRangeCache;
        Mock<GlobalEndpointManager> globalEndpointManager;

        public static CosmosClient CreateMockCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            DocumentClient documentClient = new MockDocumentClient();
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());
            cosmosClientBuilder.WithConnectionModeDirect();
            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            return cosmosClientBuilder.Build(documentClient);
        }

        public MockDocumentClient()
            : base(new Uri("http://localhost"), null)
        {
            this.Init();
        }

        internal override async Task EnsureValidClientAsync()
        {
            await Task.Yield();
        }

        public override Documents.ConsistencyLevel ConsistencyLevel => Documents.ConsistencyLevel.Session;

        internal override IRetryPolicyFactory ResetSessionTokenRetryPolicy => new RetryPolicy(this.globalEndpointManager.Object, new ConnectionPolicy());

        internal override Task<ClientCollectionCache> GetCollectionCacheAsync()
        {
            return Task.FromResult(this.collectionCache.Object);
        }

        internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync()
        {
            return Task.FromResult(this.partitionKeyRangeCache.Object);
        }

        string IAuthorizationTokenProvider.GetUserAuthorizationToken(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType) /* unused, use token based upon what is passed in constructor */
        {
            return null;
        }

        private void Init()
        {
            this.collectionCache = new Mock<ClientCollectionCache>(null, new ServerStoreModel(null), null, null);
            this.collectionCache.Setup
                    (m =>
                        m.ResolveCollectionAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                ).Returns(Task.FromResult(ContainerProperties.CreateWithResourceId("test")));

            this.partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, null, null);
            this.partitionKeyRangeCache.Setup(
                        m => m.TryLookupAsync(
                            It.IsAny<string>(),
                            It.IsAny<CollectionRoutingMap>(),
                            It.IsAny<DocumentServiceRequest>(),
                            It.IsAny<CancellationToken>()
                        )
                ).Returns(Task.FromResult<CollectionRoutingMap>(null));


            this.globalEndpointManager = new Mock<GlobalEndpointManager>(this, new ConnectionPolicy());

            this.InitStoreModels();
        }

        private void InitStoreModels()
        {
            this.GatewayStoreModel = GetMockGatewayStoreModel();

            this.sessionContainer = new SessionContainer("localhost");
            this.Session = this.sessionContainer;

            AddressInformation[] addressInformation = GetMockAddressInformation();
            var mockAddressCache = GetMockAddressCache(addressInformation);

            ReplicationPolicy replicationPolicy = new ReplicationPolicy();
            replicationPolicy.MaxReplicaSetSize = 1;
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);

            this.StoreModel = new ServerStoreModel(new StoreClient(
                        mockAddressCache.Object,
                        sessionContainer,
                        mockServiceConfigReader.Object,
                        mockAuthorizationTokenProvider.Object,
                        Protocol.Tcp,
                        GetMockTransportClient(addressInformation)));
        }

        private Mock<IAddressResolver> GetMockAddressCache(AddressInformation[] addressInformation)
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

        private AddressInformation[] GetMockAddressInformation()
        {
            // setup mocks for address information
            AddressInformation[] addressInformation = new AddressInformation[3];

            // construct URIs that look like the actual uri
            // rntbd://yt1prdddc01-docdb-1.documents.azure.com:14003/apps/ce8ab332-f59e-4ce7-a68e-db7e7cfaa128/services/68cc0b50-04c6-4716-bc31-2dfefd29e3ee/partitions/5604283d-0907-4bf4-9357-4fa9e62de7b5/replicas/131170760736528207s/
            for (int i = 0; i <= 2; i++)
            {
                addressInformation[i] = new AddressInformation();
                addressInformation[i].PhysicalUri =
                    "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/"
                    + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/";
                addressInformation[i].IsPrimary = i == 0 ? true : false;
                addressInformation[i].Protocol = Protocol.Tcp;
                addressInformation[i].IsPublic = true;
            }
            return addressInformation;
        }

        private TransportClient GetMockTransportClient(AddressInformation[] addressInformation)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    It.IsAny<Uri>(), It.IsAny<DocumentServiceRequest>()))
                    .Returns((Uri uri, DocumentServiceRequest documentServiceRequest) => MockRequestHelper.GetStoreResponse(documentServiceRequest));

            return mockTransportClient.Object;
        }

        private IStoreModel GetMockGatewayStoreModel()
        {
            Mock<IStoreModel> gatewayStoreModel = new Mock<IStoreModel>();

            gatewayStoreModel.Setup(
                storeModel => storeModel.ProcessMessageAsync(
                    It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                    .Returns((DocumentServiceRequest documentServiceRequest, CancellationToken cancellationToken) => MockRequestHelper.GetDocumentServiceResponse(documentServiceRequest));

            return gatewayStoreModel.Object;
        }
    }
}
