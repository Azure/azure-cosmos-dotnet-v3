//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Globalization;
    using System.Text;
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
    using System.Collections.ObjectModel;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System.IO;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class MockDocumentClient : DocumentClient, ICosmosAuthorizationTokenProvider
    {
        Mock<ClientCollectionCache> collectionCache;
        Mock<PartitionKeyRangeCache> partitionKeyRangeCache;
        Mock<GlobalEndpointManager> globalEndpointManager;
        private static readonly PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
        {
            Kind = PartitionKind.Hash,
            Paths = new Collection<string>()
            {
                "/id"
            }
        };

        string[] dummyHeaderNames;
        private IComputeHash authKeyHashFunction;

        public static CosmosClient CreateMockCosmosClient(
            bool useCustomSerializer = false,
            Action < CosmosClientBuilder> customizeClientBuilder = null)
        {
            MockDocumentClient documentClient = new MockDocumentClient();
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
            cosmosClientBuilder.WithConnectionModeDirect();
            customizeClientBuilder?.Invoke(cosmosClientBuilder);

            if (useCustomSerializer)
            {
                cosmosClientBuilder.WithSerializerOptions(
                    new CosmosSerializationOptions()
                    {
                        IgnoreNullValues = true,
                    });
            }

            documentClient.dummyHeaderNames = new string[100];
            for (int i = 0; i < documentClient.dummyHeaderNames.Length; i++)
            {
                documentClient.dummyHeaderNames[i] = Guid.NewGuid().ToString();
            }
            documentClient.dummyHeaderNames[0] = HttpConstants.HttpHeaders.ActivityId;
            documentClient.dummyHeaderNames[1] = HttpConstants.HttpHeaders.SessionToken;
            documentClient.dummyHeaderNames[2] = HttpConstants.HttpHeaders.ConsistencyLevel;
            documentClient.dummyHeaderNames[3] = HttpConstants.HttpHeaders.XDate;

            return cosmosClientBuilder.Build(documentClient);
        }

        public MockDocumentClient()
            : base(new Uri("http://localhost"), null)
        {
            this.authKeyHashFunction = new StringHMACSHA256Hash(MockDocumentClient.GenerateRandomKey());

            this.Init();
        }

        internal override async Task EnsureValidClientAsync(ITrace trace)
        {
            await Task.Yield();
        }

        public override Documents.ConsistencyLevel ConsistencyLevel => Documents.ConsistencyLevel.Session;

        public static string GenerateRandomKey()
        {
            int keyLength = 64;
            byte[] randomEntries = new byte[keyLength];

            Random r = new Random((int)DateTime.Now.Ticks);
            r.NextBytes(randomEntries);

            return Convert.ToBase64String(randomEntries);
        }

        internal override IRetryPolicyFactory ResetSessionTokenRetryPolicy => new RetryPolicy(
            this.globalEndpointManager.Object,
            new ConnectionPolicy(), 
            new GlobalPartitionEndpointManagerCore(this.globalEndpointManager.Object));

        internal override Task<ClientCollectionCache> GetCollectionCacheAsync(ITrace trace)
        {
            return Task.FromResult(this.collectionCache.Object);
        }

        internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync(ITrace trace)
        {
            return Task.FromResult(this.partitionKeyRangeCache.Object);
        }

        ValueTask<string> ICosmosAuthorizationTokenProvider.GetUserAuthorizationTokenAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            ITrace trace) // unused, use token based upon what is passed in constructor 
        {
            // this is masterkey authZ
            headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

            string authorization = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                    verb: requestVerb,
                    resourceId: resourceAddress,
                    resourceType: resourceType,
                    headers: headers,
                    stringHMACSHA256Helper: this.authKeyHashFunction,
                    payload: out AuthorizationHelper.ArrayOwner payload);

            using (payload)
            {
                return new ValueTask<string>(authorization);
            }
        }

        private void Init()
        {
            this.collectionCache = new Mock<ClientCollectionCache>(null, new ServerStoreModel(null), null, null);

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("test");
            containerProperties.PartitionKey = partitionKeyDefinition;
            this.collectionCache.Setup
                    (m =>
                        m.ResolveCollectionAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<ITrace>()
                    )
                ).Returns(Task.FromResult(containerProperties));

            this.collectionCache.Setup(x =>
                x.ResolveByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>())).Returns(Task.FromResult(containerProperties));

            this.partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, null, null);
            this.partitionKeyRangeCache.Setup(
                        m => m.TryLookupAsync(
                            It.IsAny<string>(),
                            It.IsAny<CollectionRoutingMap>(),
                            It.IsAny<DocumentServiceRequest>(),
                            It.IsAny<CancellationToken>(),
                            It.IsAny<ITrace>()
                        )
                ).Returns(Task.FromResult<CollectionRoutingMap>(null));

            List<PartitionKeyRange> result = new List<PartitionKeyRange>
            {
                new PartitionKeyRange()
                {
                    MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    Id = "0"
                }
            };

            this.partitionKeyRangeCache
                .Setup(m => m.TryGetOverlappingRangesAsync(
                    It.IsAny<string>(),
                    It.IsAny<Documents.Routing.Range<string>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<bool>()))
                .Returns(Task.FromResult((IReadOnlyList<PartitionKeyRange>)result));

            this.globalEndpointManager = new Mock<GlobalEndpointManager>(this, new ConnectionPolicy());

            this.InitStoreModels();
        }

        private void InitStoreModels()
        {
            this.GatewayStoreModel = this.GetMockGatewayStoreModel();

            SessionContainer sessionContainer = new SessionContainer("localhost");
            this.sessionContainer = sessionContainer;

            AddressInformation[] addressInformation = this.GetMockAddressInformation();
            Mock<IAddressResolver> mockAddressCache = this.GetMockAddressCache(addressInformation);

            ReplicationPolicy replicationPolicy = new ReplicationPolicy
            {
                MaxReplicaSetSize = 1
            };
            Mock<IServiceConfigurationReader> mockServiceConfigReader = new Mock<IServiceConfigurationReader>();

            Mock<IAuthorizationTokenProvider> mockAuthorizationTokenProvider = new Mock<IAuthorizationTokenProvider>();
            mockServiceConfigReader.SetupGet(x => x.UserReplicationPolicy).Returns(replicationPolicy);
            mockServiceConfigReader.SetupGet(x => x.SystemReplicationPolicy).Returns(replicationPolicy);
            mockServiceConfigReader.SetupGet(x => x.DefaultConsistencyLevel).Returns(Documents.ConsistencyLevel.Eventual);

            this.StoreModel = new ServerStoreModel(new StoreClient(
                        mockAddressCache.Object,
                        sessionContainer,
                        mockServiceConfigReader.Object,
                        mockAuthorizationTokenProvider.Object,
                        Protocol.Tcp,
                        this.GetMockTransportClient(addressInformation),
                        enableRequestDiagnostics: true));
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
                addressInformation[i] = new AddressInformation
                {
                    PhysicalUri =
                    "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/PARTITIONGUID/replicas/"
                    + i.ToString("G", CultureInfo.CurrentCulture) + (i == 0 ? "p" : "s") + "/",
                    IsPrimary = i == 0,
                    Protocol = Protocol.Tcp,
                    IsPublic = true
                };
            }
            return addressInformation;
        }

        private TransportClient GetMockTransportClient(AddressInformation[] addressInformation)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();

            mockTransportClient.Setup(
                client => client.InvokeResourceOperationAsync(
                    It.IsAny<TransportAddressUri>(),
                    It.Is<DocumentServiceRequest>(e => this.IsValidDsr(e))))
                    .Returns((TransportAddressUri uri, DocumentServiceRequest documentServiceRequest) => Task.FromResult(MockRequestHelper.GetStoreResponse(documentServiceRequest)));

            return mockTransportClient.Object;
        }

        private bool IsValidDsr(DocumentServiceRequest dsr)
        {
            for (int i = 0; i < this.dummyHeaderNames.Length; i++)
            {
                _ = dsr.Headers[this.dummyHeaderNames[i]];
            }

            return true;
        }

        private IStoreModel GetMockGatewayStoreModel()
        {
            Mock<IStoreModel> gatewayStoreModel = new Mock<IStoreModel>();

            gatewayStoreModel.Setup(
                storeModel => storeModel.ProcessMessageAsync(
                    It.IsAny<DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                    .Returns((DocumentServiceRequest documentServiceRequest, CancellationToken cancellationToken) =>
                        Task.FromResult(MockRequestHelper.GetDocumentServiceResponse(documentServiceRequest)));

            return gatewayStoreModel.Object;
        }
    }
}
