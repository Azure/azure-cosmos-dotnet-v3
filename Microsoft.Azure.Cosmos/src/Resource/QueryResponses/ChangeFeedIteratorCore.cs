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
    internal sealed class ChangeFeedIteratorCore : FeedTokenIterator
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

        public override FeedToken FeedToken => this.feedTokenInternal; 

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
                // ReadAll scenario, initialize with one token for all
                IReadOnlyList<FeedToken> tokens = await this.container.GetFeedTokensAsync(1, cancellationToken);
                Debug.Assert(tokens.Count > 0, "FeedToken count should be more than 0.");
                this.feedTokenInternal = tokens[0] as FeedTokenInternal;
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

    /// <summary>
    /// Cosmos Change Feed iterator using FeedToken
    /// </summary>
    internal sealed class ChangeFeedIteratorCore<T> : FeedTokenIterator<T>
    {
        private readonly FeedTokenIterator feedIterator;
        private readonly Func<ResponseMessage, FeedResponse<T>> responseCreator;

        internal ChangeFeedIteratorCore(
            FeedTokenIterator feedIterator,
            Func<ResponseMessage, FeedResponse<T>> responseCreator)
        {
            this.responseCreator = responseCreator;
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override FeedToken FeedToken => this.feedIterator.FeedToken;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage response = await this.feedIterator.ReadNextAsync(cancellationToken);
            return this.responseCreator(response);
        }

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            return this.feedIterator.TryGetContinuationToken(out continuationToken);
        }
    }
}