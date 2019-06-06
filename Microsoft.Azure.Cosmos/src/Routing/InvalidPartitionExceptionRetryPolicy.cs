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
        private readonly DocumentClient documentClient;
        private readonly IDocumentClientRetryPolicy nextPolicy;

        private bool retried;

        public InvalidPartitionExceptionRetryPolicy(
            DocumentClient documentClient,
            IDocumentClientRetryPolicy nextPolicy)
        {
            if (documentClient == null)
            {
                throw new ArgumentNullException(nameof(documentClient));
            }

            this.documentClient = documentClient;
            this.nextPolicy = nextPolicy;
        }

        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentClientException clientException = exception as DocumentClientException;
            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                clientException?.StatusCode,
                clientException?.GetSubStatus(),
                clientException?.ResourceAddress);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextPolicy != null ? await this.nextPolicy.ShouldRetryAsync(exception, cancellationToken) : ShouldRetryResult.NoRetry();
        }

        public async Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage httpResponseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                httpResponseMessage.StatusCode,
                httpResponseMessage.Headers.SubStatusCode,
                httpResponseMessage.GetResourceAddress());

            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextPolicy != null ? await this.nextPolicy.ShouldRetryAsync(httpResponseMessage, cancellationToken) : ShouldRetryResult.NoRetry();
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
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
                    if (!string.IsNullOrEmpty(resourceIdOrFullName) && PathsHelper.IsNameBased(resourceIdOrFullName))
                    {
                        CollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync();
                        collectionCache.Refresh(resourceIdOrFullName);

                        ISessionContainer sessionContainer = this.documentClient.sessionContainer;
                        sessionContainer.ClearTokenByCollectionFullname(resourceIdOrFullName);
                    }
                    else
                    {
                        Debug.Fail($"{nameof(InvalidPartitionExceptionRetryPolicy)} can only handle name based requests. The cache will not be refreshed.");
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
