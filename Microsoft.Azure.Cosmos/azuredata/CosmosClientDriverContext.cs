//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class CosmosClientDriverContext : CosmosDriverContext
    {
        private readonly CosmosClient cosmosClient;
        public CosmosClientDriverContext(CosmosClient cosmosClient)
        {
            if (cosmosClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient));
            }

            if (cosmosClient.DocumentClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient.DocumentClient));
            }

            this.cosmosClient = cosmosClient;
        }

        public override IAuthorizationTokenProvider AuthorizationTokenProvider => (Microsoft.Azure.Documents.IAuthorizationTokenProvider)this.cosmosClient.DocumentClient;

        public override IRetryPolicyFactory RetryPolicyFactory => this.cosmosClient.DocumentClient.ResetSessionTokenRetryPolicy;

        public override bool UseMultipleWriteLocations => this.cosmosClient.DocumentClient.UseMultipleWriteLocations;

        public override Microsoft.Azure.Documents.ConsistencyLevel? ConsistencyLevel
        {
            get
            {
                if (this.cosmosClient.ClientOptions.ConsistencyLevel.HasValue)
                {
                    return (Microsoft.Azure.Documents.ConsistencyLevel)this.cosmosClient.ClientOptions.ConsistencyLevel;
                }

                return null;
            }
        }

        public override void CaptureSessionToken(DocumentServiceRequest request, DocumentServiceResponse response)
        {
            this.cosmosClient.DocumentClient.CaptureSessionToken(request, response);
        }

        public override Task EnsureClientIsValidAsync() => this.cosmosClient.DocumentClient.EnsureValidClientAsync();

        public override async Task<Microsoft.Azure.Documents.ConsistencyLevel> GetAccountConsistencyLevelAsync() => (Microsoft.Azure.Documents.ConsistencyLevel)await this.cosmosClient.GetAccountConsistencyLevelAsync();

        public override Task<ClientCollectionCache> GetCollectionCacheAsync() => this.cosmosClient.DocumentClient.GetCollectionCacheAsync();

        public override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync() => this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync();

        public override IStoreModel GetStoreModel(DocumentServiceRequest request) => this.cosmosClient.DocumentClient.GetStoreProxy(request);
    }
}
