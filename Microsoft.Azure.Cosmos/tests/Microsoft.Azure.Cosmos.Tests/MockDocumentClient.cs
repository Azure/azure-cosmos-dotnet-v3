//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Client.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Moq;
    using Newtonsoft.Json;

    internal class MockDocumentClient : DocumentClient, IAuthorizationTokenProvider
    {
        Mock<ClientCollectionCache> collectionCache;
        Mock<PartitionKeyRangeCache> partitionKeyRangeCache;
        Mock<GlobalEndpointManager> globalEndpointManager;

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
            : base(new Uri("http://localhost"), null)
        {
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

        public MockDocumentClient(Uri serviceEndpoint, IList<Permission> permissionFeed, ConnectionPolicy connectionPolicy = null, Documents.ConsistencyLevel? desiredConsistencyLevel = null) 
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
            this.collectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), new ServerStoreModel(null), null, null);
            this.collectionCache.Setup
                    (m =>
                        m.ResolveCollectionAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<CancellationToken>()
                    )
                ).Returns(Task.FromResult(CosmosContainerSettings.CreateWithResourceId("test")));

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

            var sessionContainer = new SessionContainer(this.ServiceEndpoint.Host);
            this.sessionContainer = sessionContainer;
        }
    }
}
