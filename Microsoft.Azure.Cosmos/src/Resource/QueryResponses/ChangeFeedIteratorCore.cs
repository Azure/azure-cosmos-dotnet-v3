//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos Change Feed iterator using FeedToken
    /// </summary>
    internal sealed class ChangeFeedIteratorCore : FeedIteratorInternal
    {
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore container;
        private FeedTokenInternal feedTokenInternal;
        private bool hasMoreResults = true;

        internal ChangeFeedIteratorCore(
            CosmosClientContext clientContext,
            ContainerCore container,
            FeedTokenInternal feedTokenInternal,
            ChangeFeedRequestOptions changeFeedRequestOptions)
            : this(clientContext, container, changeFeedRequestOptions)
        {
            if (feedTokenInternal == null) throw new ArgumentNullException(nameof(feedTokenInternal));
            this.feedTokenInternal = feedTokenInternal;
        }

        internal ChangeFeedIteratorCore(
            CosmosClientContext clientContext,
            ContainerCore container,
            ChangeFeedRequestOptions changeFeedRequestOptions)
        {
            if (clientContext == null) throw new ArgumentNullException(nameof(clientContext));
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (changeFeedRequestOptions != null
                && changeFeedRequestOptions.MaxItemCount.HasValue
                && changeFeedRequestOptions.MaxItemCount.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(changeFeedRequestOptions.MaxItemCount));
            }

            this.clientContext = clientContext;
            this.container = container;
            this.changeFeedOptions = changeFeedRequestOptions ?? new ChangeFeedRequestOptions();
        }

        public override bool HasMoreResults => this.hasMoreResults;

#if PREVIEW
        public
#else
        internal
#endif
        override FeedToken FeedToken => this.feedTokenInternal; 

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.changeFeedOptions);
            using (diagnostics.CreateScope("ChangeFeedReadNextAsync"))
            {
                return this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }
        }

        public override bool TryGetContinuationToken(out string state)
        {
            state = this.feedTokenInternal.GetContinuation();
            return true;
        }

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnosticsScope,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.feedTokenInternal == null)
            {
                Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                string containerRId = await this.container.GetRIDAsync(cancellationToken);
                IReadOnlyList<Documents.PartitionKeyRange> partitionKeyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                        containerRId,
                        new Documents.Routing.Range<string>(
                            Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            isMinInclusive: true,
                            isMaxInclusive: false),
                        forceRefresh: true);
                // ReadAll scenario, initialize with one token for all
                this.feedTokenInternal = new FeedTokenEPKRange(containerRId, partitionKeyRanges);
            }

            Uri resourceUri = this.container.LinkUri;
            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: this.changeFeedOptions,
                cosmosContainerCore: this.container,
                requestEnricher: request =>
                {
                    ChangeFeedRequestOptions.FillContinuationToken(request, this.feedTokenInternal.GetContinuation());
                    this.feedTokenInternal.EnrichRequest(request);
                },
                partitionKey: null,
                streamPayload: null,
                diagnosticsScope: diagnosticsScope,
                cancellationToken: cancellationToken);

            // Retry in case of splits or other scenarios
            if (await this.feedTokenInternal.ShouldRetryAsync(this.container, responseMessage, cancellationToken))
            {
                if (responseMessage.IsSuccessStatusCode
                    || responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    // Change Feed read uses Etag for continuation
                    this.feedTokenInternal.UpdateContinuation(responseMessage.Headers.ETag);
                }

                return await this.ReadNextInternalAsync(diagnosticsScope, cancellationToken);
            }

            if (responseMessage.IsSuccessStatusCode
                || responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                // Change Feed read uses Etag for continuation
                this.feedTokenInternal.UpdateContinuation(responseMessage.Headers.ETag);
            }

            this.hasMoreResults = responseMessage.IsSuccessStatusCode;
            return responseMessage;
        }
    }
}