//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net.Http;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Moq;
    using Newtonsoft.Json;

    internal class MockDocumentClient : DocumentClient, IAuthorizationTokenProvider, ICosmosAuthorizationTokenProvider
    {
        private Mock<ClientCollectionCache> collectionCache;
        private Mock<PartitionKeyRangeCache> partitionKeyRangeCache;
        private Mock<GlobalEndpointManager> globalEndpointManager;
        private readonly Cosmos.ConsistencyLevel accountConsistencyLevel;

        public MockDocumentClient()
            : base(new Uri("http://localhost"), null)
        {
            this.Init();
        }

        public MockDocumentClient(Cosmos.ConsistencyLevel accountConsistencyLevel)
            : base(new Uri("http://localhost"), null)
        {
            this.accountConsistencyLevel = accountConsistencyLevel;
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, SecureString authKey, ConnectionPolicy connectionPolicy = null, Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : base(serviceEndpoint, authKey, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, string authKeyOrResourceToken, ConnectionPolicy connectionPolicy = null, Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : base(serviceEndpoint, authKeyOrResourceToken, (HttpMessageHandler)null, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, IList<Documents.Permission> permissionFeed, ConnectionPolicy connectionPolicy = null, Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : base(serviceEndpoint, permissionFeed, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, SecureString authKey, JsonSerializerSettings serializerSettings, ConnectionPolicy connectionPolicy = null, Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : base(serviceEndpoint, authKey, serializerSettings, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, string authKeyOrResourceToken, JsonSerializerSettings serializerSettings, ConnectionPolicy connectionPolicy = null, Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : base(serviceEndpoint, authKeyOrResourceToken, serializerSettings, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        internal MockDocumentClient(Uri serviceEndpoint, IList<ResourceToken> resourceTokens, ConnectionPolicy connectionPolicy = null, Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : base(serviceEndpoint, resourceTokens, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        internal MockDocumentClient(
            Uri serviceEndpoint,
            string authKeyOrResourceToken,
            EventHandler<SendingRequestEventArgs> sendingRequestEventArgs,
            ConnectionPolicy connectionPolicy = null,
            Documents.ConsistencyLevel? desiredConsistencyLevel = null,
            JsonSerializerSettings serializerSettings = null,
            ApiType apitype = ApiType.None,
            EventHandler<ReceivedResponseEventArgs> receivedResponseEventArgs = null,
            Func<TransportClient, TransportClient> transportClientHandlerFactory = null)
            : base(serviceEndpoint,
                  authKeyOrResourceToken,
                  sendingRequestEventArgs,
                  connectionPolicy,
                  desiredConsistencyLevel,
                  serializerSettings,
                  apitype,
                  receivedResponseEventArgs,
                  null,
                  null,
                  true,
                  transportClientHandlerFactory)
        {
            this.Init();
        }

        internal override async Task EnsureValidClientAsync()
        {
            await Task.Yield();
        }

        public override Documents.ConsistencyLevel ConsistencyLevel => Documents.ConsistencyLevel.Session;

        internal override Task<Cosmos.ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            return Task.FromResult(this.accountConsistencyLevel);
        }

        internal override IRetryPolicyFactory ResetSessionTokenRetryPolicy => new RetryPolicy(this.globalEndpointManager.Object, new ConnectionPolicy());

        internal override Task<ClientCollectionCache> GetCollectionCacheAsync()
        {
            return Task.FromResult(this.collectionCache.Object);
        }

        internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync()
        {
            return Task.FromResult(this.partitionKeyRangeCache.Object);
        }

        internal override Task<QueryPartitionProvider> QueryPartitionProvider => Task.FromResult(new QueryPartitionProvider(
JsonConvert.DeserializeObject<Dictionary<string, object>>("{\"maxSqlQueryInputLength\":262144,\"maxJoinsPerSqlQuery\":5,\"maxLogicalAndPerSqlQuery\":500,\"maxLogicalOrPerSqlQuery\":500,\"maxUdfRefPerSqlQuery\":10,\"maxInExpressionItemsCount\":16000,\"queryMaxInMemorySortDocumentCount\":500,\"maxQueryRequestTimeoutFraction\":0.9,\"sqlAllowNonFiniteNumbers\":false,\"sqlAllowAggregateFunctions\":true,\"sqlAllowSubQuery\":true,\"sqlAllowScalarSubQuery\":true,\"allowNewKeywords\":true,\"sqlAllowLike\":false,\"sqlAllowGroupByClause\":false,\"maxSpatialQueryCells\":12,\"spatialMaxGeometryPointCount\":256,\"sqlAllowTop\":true,\"enableSpatialIndexing\":true}")));

        ValueTask<(string token, string payload)> IAuthorizationTokenProvider.GetUserAuthorizationAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType)
        {
            return new ValueTask<(string token, string payload)>((null, null));
        }

        string ICosmosAuthorizationTokenProvider.GetUserAuthorizationToken(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType) /* unused, use token based upon what is passed in constructor */
        {
            return null;
        }

        internal virtual IReadOnlyList<PartitionKeyRange> ResolveOverlapingPartitionKeyRanges(string collectionRid, Documents.Routing.Range<string> range, bool forceRefresh)
        {
            return new List<Documents.PartitionKeyRange>() { new Documents.PartitionKeyRange() { MinInclusive = "", MaxExclusive = "FF", Id = "0" } };
        }

        private void Init()
        {
            this.collectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), new ServerStoreModel(null), null, null);
            const string pkPath = "/pk";
            this.collectionCache.Setup
                    (m =>
                        m.ResolveCollectionAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                ).Returns(() =>
                {
                    ContainerProperties cosmosContainerSetting = ContainerProperties.CreateWithResourceId("test");
                    cosmosContainerSetting.PartitionKey = new PartitionKeyDefinition()
                    {
                        Kind = PartitionKind.Hash,
                        Paths = new Collection<string>()
                        {
                            pkPath
                        }
                    };

                    return Task.FromResult(cosmosContainerSetting);
                });
            this.collectionCache.Setup
                    (m =>
                        m.ResolveByNameAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                ).Returns(() =>
                {
                    ContainerProperties containerSettings = ContainerProperties.CreateWithResourceId("test");
                    containerSettings.PartitionKey.Paths = new Collection<string>() { pkPath };
                    return Task.FromResult(containerSettings);
                });

            this.collectionCache.Setup
                    (m =>
                        m.ResolveByNameAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                ).Returns(() =>
                {
                    ContainerProperties cosmosContainerSetting = ContainerProperties.CreateWithResourceId("test");
                    cosmosContainerSetting.PartitionKey = new PartitionKeyDefinition()
                    {
                        Kind = PartitionKind.Hash,
                        Paths = new Collection<string>()
                        {
                            pkPath
                        }
                    };

                    return Task.FromResult(cosmosContainerSetting);
                });

            this.partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, null, null);
            this.partitionKeyRangeCache.Setup(
                        m => m.TryLookupAsync(
                            It.IsAny<string>(),
                            It.IsAny<CollectionRoutingMap>(),
                            It.IsAny<DocumentServiceRequest>(),
                            It.IsAny<CancellationToken>()
                        )
                ).Returns(Task.FromResult<CollectionRoutingMap>(null));
            this.partitionKeyRangeCache.Setup(
                        m => m.TryGetOverlappingRangesAsync(
                            It.IsAny<string>(),
                            It.IsAny<Documents.Routing.Range<string>>(),
                            It.IsAny<bool>()
                        )
                ).Returns((string collectionRid, Documents.Routing.Range<string> range, bool forceRefresh) =>
                {
                    return Task.FromResult<IReadOnlyList<PartitionKeyRange>>(this.ResolveOverlapingPartitionKeyRanges(collectionRid, range, forceRefresh));
                });

            this.globalEndpointManager = new Mock<GlobalEndpointManager>(this, new ConnectionPolicy());

            SessionContainer sessionContainer = new SessionContainer(this.ServiceEndpoint.Host);
            this.sessionContainer = sessionContainer;
        }
    }
}
