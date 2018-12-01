//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;

    internal class NonRetriableInvalidPartitionExceptionRetryPolicy : IDocumentClientRetryPolicy
    {
        private readonly CollectionCache clientCollectionCache;

        private readonly IDocumentClientRetryPolicy nextPolicy;

        public NonRetriableInvalidPartitionExceptionRetryPolicy(
            CollectionCache clientCollectionCache,
            IDocumentClientRetryPolicy nextPolicy)
        {
            if (clientCollectionCache == null)
            {
                throw new ArgumentNullException("clientCollectionCache");
            }

            if (nextPolicy == null)
            {
                throw new ArgumentNullException("nextPolicy");
            }

            this.clientCollectionCache = clientCollectionCache;
            this.nextPolicy = nextPolicy;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception, 
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentClientException clientException = exception as DocumentClientException;
            return this.ShouldRetryAsyncInternal(clientException?.StatusCode,
                clientException?.GetSubStatus(),
                clientException?.ResourceAddress,
                () => this.nextPolicy.ShouldRetryAsync(exception, cancellationToken));
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage cosmosResponseMessage, 
            CancellationToken cancellationToken)
        {
            // Its used by query/feed and are not yet needed.
            throw new NotImplementedException();
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextPolicy.OnBeforeSendRequest(request);
        }

        private Task<ShouldRetryResult> ShouldRetryAsyncInternal(
            HttpStatusCode? statusCode, 
            SubStatusCodes? subStatusCode, 
            string resourceIdOrFullName, 
            Func<Task<ShouldRetryResult>> continueIfNotHandled)
        {
            if (!statusCode.HasValue
                && !subStatusCode.HasValue)
            {
                return continueIfNotHandled();
            }

            if (statusCode == HttpStatusCode.Gone
                && subStatusCode == SubStatusCodes.NameCacheIsStale)
            {
                if (!string.IsNullOrEmpty(resourceIdOrFullName))
                {
                    this.clientCollectionCache.Refresh(resourceIdOrFullName);
                }

                return Task.FromResult(ShouldRetryResult.NoRetry(new NotFoundException()));
            }

            return continueIfNotHandled();
        }
    }
}
