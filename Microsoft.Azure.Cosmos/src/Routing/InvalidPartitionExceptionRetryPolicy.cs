//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;

    internal class InvalidPartitionExceptionRetryPolicy : IDocumentClientRetryPolicy
    {
        private readonly CollectionCache clientCollectionCache;

        private readonly IDocumentClientRetryPolicy nextPolicy;

        private bool retried;

        public InvalidPartitionExceptionRetryPolicy(
            CollectionCache clientCollectionCache,
            IDocumentClientRetryPolicy nextPolicy)
        {
            if (clientCollectionCache == null)
            {
                throw new ArgumentNullException("clientCollectionCache");
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
                () => this.nextPolicy?.ShouldRetryAsync(exception, cancellationToken));
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage httpResponseMessage, 
            CancellationToken cancellationToken)
        {
            Debug.Assert(this.nextPolicy == null);
            return this.ShouldRetryAsyncInternal(
                httpResponseMessage.StatusCode,
                httpResponseMessage.Headers.SubStatusCode,
                httpResponseMessage.GetResourceAddress(),

                // In the new OM, retries are chained by handlers, not by chaining retry policies. Consequently, the next policy should be null here.
                continueIfNotHandled: null);
        }

        private Task<ShouldRetryResult> ShouldRetryAsyncInternal(
            HttpStatusCode? statusCode, 
            SubStatusCodes? subStatusCode, 
            string resourceIdOrFullName, 
            Func<Task<ShouldRetryResult>> continueIfNotHandled)
        {
            if (statusCode.HasValue
                && subStatusCode.HasValue
                && statusCode == HttpStatusCode.Gone
                && subStatusCode == SubStatusCodes.NameCacheIsStale)
            {
                if (!this.retried)
                {
                    if (!string.IsNullOrEmpty(resourceIdOrFullName))
                    {
                        this.clientCollectionCache.Refresh(resourceIdOrFullName);
                    }

                    this.retried = true;
                    return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.Zero));
                }
                else
                {
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                }
            }

            if (continueIfNotHandled != null)
            {
                return continueIfNotHandled().ContinueWith(x => x.Result ?? ShouldRetryResult.NoRetry());
            }
            else
            {
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextPolicy.OnBeforeSendRequest(request);
        }        
    }
}
