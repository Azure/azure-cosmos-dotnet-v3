//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;

    internal sealed class DocumentQueryClient : IDocumentQueryClient
    {
        private readonly DocumentClient innerClient;
        private QueryPartitionProvider queryPartitionProvider;
        private readonly SemaphoreSlim semaphore;

        public DocumentQueryClient(DocumentClient innerClient)
        {
            if (innerClient == null)
            {
                throw new ArgumentNullException("innerClient");
            }

            this.innerClient = innerClient;
            this.semaphore = new SemaphoreSlim(1, 1);
        }

        public void Dispose()
        {
            this.innerClient.Dispose();
            if (this.queryPartitionProvider != null)
            {
                this.queryPartitionProvider.Dispose();
            }
        }

        QueryCompatibilityMode IDocumentQueryClient.QueryCompatibilityMode
        {
            get
            {
                return this.innerClient.QueryCompatibilityMode;
            }

            set
            {
                this.innerClient.QueryCompatibilityMode = value;
            }
        }

        IRetryPolicyFactory IDocumentQueryClient.ResetSessionTokenRetryPolicy
        {
            get
            {
                return this.innerClient.ResetSessionTokenRetryPolicy;
            }
        }

        Uri IDocumentQueryClient.ServiceEndpoint
        {
            get
            {
                return this.innerClient.ReadEndpoint;
            }
        }

        ConnectionMode IDocumentQueryClient.ConnectionMode
        {
            get
            {
                return this.innerClient.ConnectionPolicy.ConnectionMode;
            }
        }

        Action<IQueryable> IDocumentQueryClient.OnExecuteScalarQueryCallback
        {
            get { return this.innerClient.OnExecuteScalarQueryCallback; }
        }

        async Task<CollectionCache> IDocumentQueryClient.GetCollectionCacheAsync()
        {
            return await this.innerClient.GetCollectionCacheAsync();
        }

        async Task<IRoutingMapProvider> IDocumentQueryClient.GetRoutingMapProviderAsync()
        {
            return await this.innerClient.GetPartitionKeyRangeCacheAsync();
        }

        public async Task<QueryPartitionProvider> GetQueryPartitionProviderAsync(CancellationToken cancellationToken)
        {
            if (this.queryPartitionProvider == null)
            {
                await this.semaphore.WaitAsync(cancellationToken);

                if (this.queryPartitionProvider == null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    this.queryPartitionProvider = new QueryPartitionProvider(await this.innerClient.GetQueryEngineConfiguration());
                }

                this.semaphore.Release();
            }

            return this.queryPartitionProvider;
        }

        public Task<DocumentServiceResponse> ExecuteQueryAsync(DocumentServiceRequest request, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            return this.innerClient.ExecuteQueryAsync(request, retryPolicyInstance, cancellationToken);
        }

        public Task<DocumentServiceResponse> ReadFeedAsync(DocumentServiceRequest request, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            return this.innerClient.ReadFeedAsync(request, retryPolicyInstance, cancellationToken);
        }

        public async Task<ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            return (ConsistencyLevel)await this.innerClient.GetDefaultConsistencyLevelAsync();
        }

        public Task<ConsistencyLevel?> GetDesiredConsistencyLevelAsync()
        {
            return this.innerClient.GetDesiredConsistencyLevelAsync();
        }

        public Task EnsureValidOverwrite(ConsistencyLevel requestedConsistencyLevel)
        {
            this.innerClient.EnsureValidOverwrite(requestedConsistencyLevel);
            return CompletedTask.Instance;
        }

        public Task<PartitionKeyRangeCache> GetPartitionKeyRangeCache()
        {
            return this.innerClient.GetPartitionKeyRangeCacheAsync();
        }
    }
}