//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;

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
            return await this.ShouldRetryAsyncInternal(clientException?.StatusCode,
                clientException?.GetSubStatus(),
                cancellationToken,
                () => this.nextRetryPolicy?.ShouldRetryAsync(exception, cancellationToken));
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            CosmosResponseMessage cosmosResponseMessage, 
            CancellationToken cancellationToken)
        {
            return await this.ShouldRetryAsyncInternal(cosmosResponseMessage?.StatusCode,
                cosmosResponseMessage?.Headers.SubStatusCode,
                cancellationToken,
                continueIfNotHandled: null);
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.nextRetryPolicy.OnBeforeSendRequest(request);
        }

        private async Task<ShouldRetryResult> ShouldRetryAsyncInternal(
            HttpStatusCode? statusCode, 
            SubStatusCodes? subStatusCode, 
            CancellationToken cancellationToken, 
            Func<Task<ShouldRetryResult>> continueIfNotHandled)
        {
            if (statusCode.HasValue
                && subStatusCode.HasValue
                && statusCode == HttpStatusCode.Gone
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
                    CosmosContainerSettings collection = await this.collectionCache.ResolveCollectionAsync(request, cancellationToken);
                    CollectionRoutingMap routingMap = await this.partitionKeyRangeCache.TryLookupAsync(collection.ResourceId, null, cancellationToken);
                    if (routingMap != null)
                    {
                        // Force refresh.
                        await this.partitionKeyRangeCache.TryLookupAsync(
                                collection.ResourceId,
                                routingMap,
                                cancellationToken);
                    }
                }

                this.retried = true;
                return ShouldRetryResult.RetryAfter(TimeSpan.FromSeconds(0));
            }

            if(continueIfNotHandled != null)
            {
                return await continueIfNotHandled() ?? ShouldRetryResult.NoRetry();
            }
            else
            {
                return await Task.FromResult(ShouldRetryResult.NoRetry());
            }
        }
    }
}
