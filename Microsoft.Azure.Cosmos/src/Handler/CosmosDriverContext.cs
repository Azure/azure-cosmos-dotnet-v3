// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal abstract class CosmosDriverContext
    {
        public abstract IAuthorizationTokenProvider AuthorizationTokenProvider { get; }

        public abstract IRetryPolicyFactory RetryPolicyFactory { get; }

        public abstract bool UseMultipleWriteLocations { get; }

        public abstract Documents.ConsistencyLevel? ConsistencyLevel { get; }

        public abstract IStoreModel GetStoreModel(DocumentServiceRequest request);

        public abstract void CaptureSessionToken(DocumentServiceRequest request, DocumentServiceResponse response);

        public abstract Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync();

        public abstract Task<ClientCollectionCache> GetCollectionCacheAsync();

        public abstract Task<Documents.ConsistencyLevel> GetAccountConsistencyLevelAsync();

        public abstract Task EnsureClientIsValidAsync();

    }
}
