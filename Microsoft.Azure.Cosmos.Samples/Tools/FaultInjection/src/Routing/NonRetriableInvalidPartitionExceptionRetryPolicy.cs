//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Documents;

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
            DocumentClientException clientException = exception as DocumentClientException;
            ShouldRetryResult shouldRetryResult = this.ShouldRetryInternal(
                clientException?.StatusCode,
                clientException?.GetSubStatus(),
                clientException?.ResourceAddress);
            if (shouldRetryResult != null)
            {
                return Task.FromResult(shouldRetryResult);
            }

            return this.nextPolicy.ShouldRetryAsync(exception, cancellationToken);
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            // Its used by query/feed and are not yet needed.
            throw new NotImplementedException();
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextPolicy.OnBeforeSendRequest(request);
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
                if (!string.IsNullOrEmpty(resourceIdOrFullName))
                {
                    this.clientCollectionCache.Refresh(resourceIdOrFullName);
                }

                return ShouldRetryResult.NoRetry(new NotFoundException());
            }

            return null;
        }
    }
}
