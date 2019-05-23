//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal sealed class DocumentQueryClient : IDocumentQueryClient
    {
        private readonly DocumentClient innerClient;
        private readonly SemaphoreSlim semaphore;
        private QueryPartitionProvider queryPartitionProvider;

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
                    this.queryPartitionProvider = new QueryPartitionProvider(await this.innerClient.GetQueryEngineConfigurationAsync());
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

        public Task EnsureValidOverwriteAsync(ConsistencyLevel requestedConsistencyLevel)
        {
            this.innerClient.EnsureValidOverwrite(requestedConsistencyLevel);
            return CompletedTask.Instance;
        }

        public Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync()
        {
            return this.innerClient.GetPartitionKeyRangeCacheAsync();
        }
    }
}