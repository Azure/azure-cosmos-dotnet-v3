//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class PartitionKeyRangeGoneRetryPolicy : IDocumentClientRetryPolicy
    {
        private readonly CollectionCache collectionCache;
        private readonly IDocumentClientRetryPolicy nextRetryPolicy;
        private readonly PartitionKeyRangeCache partitionKeyRangeCache;
        private readonly string collectionLink;
        private bool retried;

        public PartitionKeyRangeGoneRetryPolicy(
            CollectionCache collectionCache,
            PartitionKeyRangeCache partitionKeyRangeCache,
            string collectionLink,
            IDocumentClientRetryPolicy nextRetryPolicy)
        {
            this.collectionCache = collectionCache;
            this.partitionKeyRangeCache = partitionKeyRangeCache;
            this.collectionLink = collectionLink;
            this.nextRetryPolicy = nextRetryPolicy;
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="exception">Exception that occured when the operation was tried</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            DocumentClientException clientException = exception as DocumentClientException;
            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(clientException?.StatusCode,
                clientException?.GetSubStatus(),
                cancellationToken);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextRetryPolicy != null ? await this.nextRetryPolicy?.ShouldRetryAsync(exception, cancellationToken) : ShouldRetryResult.NoRetry();
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="ResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(cosmosResponseMessage?.StatusCode,
                cosmosResponseMessage?.Headers.SubStatusCode,
                cancellationToken);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextRetryPolicy != null ? await this.nextRetryPolicy?.ShouldRetryAsync(cosmosResponseMessage, cancellationToken) : ShouldRetryResult.NoRetry();
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextRetryPolicy?.OnBeforeSendRequest(request);
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            CancellationToken cancellationToken)
        {
            if (!statusCode.HasValue
                && (!subStatusCode.HasValue
                || subStatusCode.Value == SubStatusCodes.Unknown))
            {
                return null;
            }

            if (statusCode == HttpStatusCode.Gone
                && subStatusCode == SubStatusCodes.PartitionKeyRangeGone)
            {
                if (this.retried)
                {
                    return ShouldRetryResult.NoRetry();
                }

                using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.Read,
                    ResourceType.Collection,
                    this.collectionLink,
                    null,
                    AuthorizationTokenType.PrimaryMasterKey))
                {
                    ContainerProperties collection = await this.collectionCache.ResolveCollectionAsync(request, cancellationToken);
                    CollectionRoutingMap routingMap = await this.partitionKeyRangeCache.TryLookupAsync(collection.ResourceId, null, request, cancellationToken);
                    if (routingMap != null)
                    {
                        // Force refresh.
                        await this.partitionKeyRangeCache.TryLookupAsync(
                                collection.ResourceId,
                                routingMap,
                                request,
                                cancellationToken);
                    }
                }

                this.retried = true;
                return ShouldRetryResult.RetryAfter(TimeSpan.FromSeconds(0));
            }

            return null;
        }
    }
}
