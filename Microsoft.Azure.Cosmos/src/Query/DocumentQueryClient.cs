//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;
    using System.Linq;

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

        RetryPolicy IDocumentQueryClient.RetryPolicy
        {
            get
            {
                return this.innerClient.RetryPolicy;
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

        public async Task<DocumentServiceResponse> ExecuteQueryAsync(DocumentServiceRequest request, CancellationToken cancellationToken)
        {
            return await this.innerClient.ExecuteQueryAsync(request, cancellationToken);
        }

        public async Task<DocumentServiceResponse> ReadFeedAsync(DocumentServiceRequest request, CancellationToken cancellationToken)
        {
            return await this.innerClient.ReadFeedAsync(request, cancellationToken);
        }

        public async Task<ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            return await this.innerClient.GetDefaultConsistencyLevelAsync();
        }

        public async Task<ConsistencyLevel?> GetDesiredConsistencyLevelAsync()
        {
            return await this.innerClient.GetDesiredConsistencyLevelAsync();
        }

        public Task EnsureValidOverwrite(ConsistencyLevel requestedConsistencyLevel)
        {
            this.innerClient.EnsureValidOverwrite(requestedConsistencyLevel);
            return CompletedTask.Instance;
        }

        public async Task<PartitionKeyRangeCache> GetPartitionKeyRangeCache()
        {
            return await this.innerClient.GetPartitionKeyRangeCacheAsync();
        }
    }
}