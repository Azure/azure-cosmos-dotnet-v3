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
        /// <param name="cancellation"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellation)
        {
            DocumentClientException clientException = exception as DocumentClientException;
            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(clientException?.StatusCode,
                clientException?.GetSubStatus(),
                cancellation);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextRetryPolicy != null ? await this.nextRetryPolicy?.ShouldRetryAsync(exception, cancellation) : ShouldRetryResult.NoRetry();
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> in return of the request</param>
        /// <param name="cancellation"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage cosmosResponseMessage,
            CancellationToken cancellation)
        {
            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(cosmosResponseMessage?.StatusCode,
                cosmosResponseMessage?.Headers.SubStatusCode,
                cancellation);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            return this.nextRetryPolicy != null ? await this.nextRetryPolicy?.ShouldRetryAsync(cosmosResponseMessage, cancellation) : ShouldRetryResult.NoRetry();
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextRetryPolicy.OnBeforeSendRequest(request);
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            CancellationToken cancellation)
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
                    CosmosContainerSettings collection = await this.collectionCache.ResolveCollectionAsync(request, cancellation);
                    CollectionRoutingMap routingMap = await this.partitionKeyRangeCache.TryLookupAsync(collection.ResourceId, null, request, cancellation);
                    if (routingMap != null)
                    {
                        // Force refresh.
                        await this.partitionKeyRangeCache.TryLookupAsync(
                                collection.ResourceId,
                                routingMap,
                                request,
                                cancellation);
                    }
                }

                this.retried = true;
                return ShouldRetryResult.RetryAfter(TimeSpan.FromSeconds(0));
            }

            return null;
        }
    }
}
