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
    using Microsoft.Azure.Documents;

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
            ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(
                clientException?.StatusCode,
                clientException?.GetSubStatus(),
                clientException?.ResourceAddress);
            if (shouldRetryResult != null)
            {
                return Task.FromResult(shouldRetryResult);
            }

            return this.nextPolicy != null ? this.nextPolicy.ShouldRetryAsync(exception, cancellationToken) : Task.FromResult(ShouldRetryResult.NoRetry());
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage httpResponseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryResult =  this.ShouldRetryInternal(
                httpResponseMessage.StatusCode,
                httpResponseMessage.Headers.SubStatusCode,
                httpResponseMessage.GetResourceAddress());

            if (shouldRetryResult != null)
            {
                return Task.FromResult(shouldRetryResult);
            }

            return this.nextPolicy != null ? this.nextPolicy.ShouldRetryAsync(httpResponseMessage, cancellationToken) : Task.FromResult(ShouldRetryResult.NoRetry());
        }

        private ShouldRetryResult ShouldRetryInternal(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            string resourceIdOrFullName)
        {
            if (!statusCode.HasValue
                && (!subStatusCode.HasValue
                || subStatusCode.Value == SubStatusCodes.Unknown))
            {
                return null;
            }

            if (statusCode == HttpStatusCode.Gone
                && subStatusCode == SubStatusCodes.NameCacheIsStale)
            {
                if (!this.retried)
                {
                    if (!string.IsNullOrEmpty(resourceIdOrFullName))
                    {
                        this.clientCollectionCache.Refresh(resourceIdOrFullName);
                    }

                    this.retried = true;
                    return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                }
                else
                {
                    return ShouldRetryResult.NoRetry();
                }
            }

            return null;
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextPolicy.OnBeforeSendRequest(request);
        }
    }
}
