//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class CosmosClientPipelineBuilderContext : ClientPipelineBuilderContext
    {
        private readonly CosmosClient cosmosClient;
        public CosmosClientPipelineBuilderContext(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }

        public override IAuthorizationTokenProvider AuthorizationTokenProvider => (Documents.IAuthorizationTokenProvider)this.cosmosClient.DocumentClient;

        public override IRetryPolicyFactory RetryPolicyFactory => this.cosmosClient.DocumentClient.ResetSessionTokenRetryPolicy;

        public override bool UseMultipleWriteLocations => this.cosmosClient.DocumentClient.UseMultipleWriteLocations;

        public override CosmosClientOptions CosmosClientOptions => this.cosmosClient.ClientOptions;

        public override void CaptureSessionToken(DocumentServiceRequest request, DocumentServiceResponse response)
        {
            this.cosmosClient.DocumentClient.CaptureSessionToken(request, response);
        }

        public override Task EnsureClientIsValidAsync() => this.cosmosClient.DocumentClient.EnsureValidClientAsync();

        public override Task<ConsistencyLevel> GetAccountConsistencyLevelAsync() => this.cosmosClient.GetAccountConsistencyLevelAsync();

        public override Task<ClientCollectionCache> GetCollectionCacheAsync() => this.cosmosClient.DocumentClient.GetCollectionCacheAsync();

        public override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync() => this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync();

        public override IStoreModel GetStoreModel(DocumentServiceRequest request) => this.cosmosClient.DocumentClient.GetStoreProxy(request);
    }
}
