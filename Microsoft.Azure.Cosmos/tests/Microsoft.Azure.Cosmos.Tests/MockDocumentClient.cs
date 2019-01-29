//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Client.Core.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using System;
    using System.Security;
    using Microsoft.Azure.Cosmos.Routing;
    using Moq;
    using System.Threading;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Common;

    internal class MockDocumentClient : DocumentClient
    {
        Mock<ClientCollectionCache> collectionCache;
        Mock<PartitionKeyRangeCache> partitionKeyRangeCache;

        public static CosmosClient CreateMockCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            DocumentClient documentClient = new MockDocumentClient();
            
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());
            if (customizeClientBuilder != null)
            {
                customizeClientBuilder(cosmosClientBuilder);
            }

            return cosmosClientBuilder.Build(documentClient);
        }

        public MockDocumentClient()
            : base(null, null)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, SecureString authKey, ConnectionPolicy connectionPolicy = null, ConsistencyLevel? desiredConsistencyLevel = null) 
            : base(serviceEndpoint, authKey, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, string authKeyOrResourceToken, ConnectionPolicy connectionPolicy = null, ConsistencyLevel? desiredConsistencyLevel = null) 
            : base(serviceEndpoint, authKeyOrResourceToken, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, IList<Permission> permissionFeed, ConnectionPolicy connectionPolicy = null, ConsistencyLevel? desiredConsistencyLevel = null) 
            : base(serviceEndpoint, permissionFeed, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, SecureString authKey, JsonSerializerSettings serializerSettings, ConnectionPolicy connectionPolicy = null, ConsistencyLevel? desiredConsistencyLevel = null) 
            : base(serviceEndpoint, authKey, serializerSettings, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        public MockDocumentClient(Uri serviceEndpoint, string authKeyOrResourceToken, JsonSerializerSettings serializerSettings, ConnectionPolicy connectionPolicy = null, ConsistencyLevel? desiredConsistencyLevel = null) 
            : base(serviceEndpoint, authKeyOrResourceToken, serializerSettings, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        internal MockDocumentClient(Uri serviceEndpoint, IList<ResourceToken> resourceTokens, ConnectionPolicy connectionPolicy = null, ConsistencyLevel? desiredConsistencyLevel = null) 
            : base(serviceEndpoint, resourceTokens, connectionPolicy, desiredConsistencyLevel)
        {
            this.Init();
        }

        internal MockDocumentClient(
            Uri serviceEndpoint, 
            string authKeyOrResourceToken, 
            EventHandler<SendingRequestEventArgs> sendingRequestEventArgs, 
            ConnectionPolicy connectionPolicy = null, 
            ConsistencyLevel? desiredConsistencyLevel = null,
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
                  transportClientHandlerFactory)
        {
            this.Init();
        }

        internal override async Task EnsureValidClientAsync()
        {
            await Task.Yield();
        }

        public override ConsistencyLevel ConsistencyLevel => ConsistencyLevel.Session;

        internal override IRetryPolicyFactory ResetSessionTokenRetryPolicy => new RetryPolicy(null, new ConnectionPolicy());

        internal override Task<ClientCollectionCache> GetCollectionCacheAsync()
        {
            return Task.FromResult(this.collectionCache.Object);
        }

        internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync()
        {
            return Task.FromResult(this.partitionKeyRangeCache.Object);
        }

        private void Init()
        {
            this.collectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), new ServerStoreModel(null), null, null);
            this.collectionCache.Setup
                    (m =>
                        m.ResolveCollectionAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                ).Returns(Task.FromResult(new CosmosContainerSettings { ResourceId = "test" }));

            this.partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, null, null);
            this.partitionKeyRangeCache.Setup(
                        m => m.TryLookupAsync(
                            It.IsAny<string>(),
                            It.IsAny<CollectionRoutingMap>(),
                            It.IsAny<DocumentServiceRequest>(),
                            It.IsAny<bool>(),
                            It.IsAny<CancellationToken>()
                        )
                ).Returns(Task.FromResult<CollectionRoutingMap>(null));
        }
    }
}
