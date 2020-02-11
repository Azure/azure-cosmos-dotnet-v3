//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos Stand-By Feed iterator implementing Composite Continuation Token
    /// </summary>
    /// <remarks>
    /// Legacy.
    /// </remarks>
    internal class ChangeFeedIteratorCore : FeedIteratorInternal
    {
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore container;
        private readonly FeedTokenInternal feedTokenInternal;
        private readonly int? maxItemCount;

        internal ChangeFeedIteratorCore(
            CosmosClientContext clientContext,
            ContainerCore container,
            FeedTokenInternal feedTokenInternal,
            int? maxItemCount,
            ChangeFeedRequestOptions options)
        {
            if (clientContext == null) throw new ArgumentNullException(nameof(clientContext));
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (feedTokenInternal == null) throw new ArgumentNullException(nameof(feedTokenInternal));

            this.clientContext = clientContext;
            this.container = container;
            this.changeFeedOptions = options;
            this.maxItemCount = maxItemCount;
            this.feedTokenInternal = feedTokenInternal;
        }

        public override bool HasMoreResults => true;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            Uri resourceUri = this.container.LinkUri;
            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationAsync<ResponseMessage>(
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: this.changeFeedOptions,
                cosmosContainerCore: this.container,
                requestEnricher: request =>
                {
                    ChangeFeedRequestOptions.FillMaxItemCount(request, this.maxItemCount);
                    this.feedTokenInternal.FillHeaders(this.clientContext, request);
                },
                responseCreator: response => response,
                partitionKey: null,
                streamPayload: null,
                diagnosticsScope: null,
                cancellationToken: cancellationToken);

            // Retry in case of splits or other scenarios
            if (await this.feedTokenInternal.ShouldRetryAsync(this.clientContext, responseMessage, cancellationToken))
            {
                if (responseMessage.IsSuccessStatusCode
                    || responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    // Change Feed read uses Etag for continuation
                    this.feedTokenInternal.UpdateContinuation(responseMessage.Headers.ETag);
                }

                return await this.ReadNextAsync(cancellationToken);
            }

            if (responseMessage.IsSuccessStatusCode
                || responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                // Change Feed read uses Etag for continuation
                this.feedTokenInternal.UpdateContinuation(responseMessage.Headers.ETag);
            }

            return responseMessage;
        }

        public override bool TryGetContinuationToken(out string state)
        {
            state = this.feedTokenInternal.GetContinuation();
            return true;
        }
    }
}