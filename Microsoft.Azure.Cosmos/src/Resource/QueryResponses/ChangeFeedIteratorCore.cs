//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;

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
        private string containerRId = null;

        internal ChangeFeedIteratorCore(
            ContainerCore container,
            FeedTokenInternal feedTokenInternal,
            ChangeFeedRequestOptions changeFeedRequestOptions)
            : this(container, changeFeedRequestOptions)
        {
            if (feedTokenInternal == null) throw new ArgumentNullException(nameof(feedTokenInternal));
            this.feedTokenInternal = feedTokenInternal;
        }

        internal ChangeFeedIteratorCore(
            ContainerCore container,
            ChangeFeedRequestOptions changeFeedRequestOptions)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (changeFeedRequestOptions != null
                && changeFeedRequestOptions.MaxItemCount.HasValue
                && changeFeedRequestOptions.MaxItemCount.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(changeFeedRequestOptions.MaxItemCount));
            }

            this.clientContext = container.ClientContext;
            this.container = container;
            this.changeFeedOptions = changeFeedRequestOptions ?? new ChangeFeedRequestOptions();
        }

        public override bool HasMoreResults => this.hasMoreResults;

#if PREVIEW
        public override
#else
        internal
#endif
        FeedToken FeedToken => this.feedTokenInternal; 

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.changeFeedOptions);
            using (diagnostics.CreateOverallScope("ChangeFeedReadNextAsync"))
            {
                if (this.containerRId == null)
                {
                    TryCatch<string> tryInitializeContainerRId = await this.TryInitializeContainerRIdAsync(cancellationToken);
                    if (!tryInitializeContainerRId.Succeeded)
                    {
                        if (tryInitializeContainerRId.Exception.InnerException is CosmosException cosmosException)
                        {
                            return cosmosException.ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                        }

                        return CosmosExceptionFactory.CreateInternalServerErrorException(
                            message: tryInitializeContainerRId.Exception.InnerException.Message,
                            innerException: tryInitializeContainerRId.Exception.InnerException,
                            diagnosticsContext: diagnostics).ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                    }

                    this.containerRId = tryInitializeContainerRId.Result;
                    // If there is an initial FeedToken, validate Container
                    if (this.feedTokenInternal != null)
                    {
                        TryCatch validateContainer = this.feedTokenInternal.ValidateContainer(this.containerRId);
                        if (!validateContainer.Succeeded)
                        {
                            return CosmosExceptionFactory.CreateInternalServerErrorException(
                                message: validateContainer.Exception.InnerException.Message,
                                innerException: validateContainer.Exception.InnerException,
                                diagnosticsContext: diagnostics).ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                        }
                    }
                }

                if (this.feedTokenInternal == null)
                {
                    this.feedTokenInternal = await this.InitializeFeedTokenAsync(cancellationToken);
                }

                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }
        }

        public override bool TryGetFeedToken(out FeedToken feedToken)
        {
            feedToken = this.feedTokenInternal;
            return true;
        }

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnosticsScope,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        private async Task<TryCatch<string>> TryInitializeContainerRIdAsync(CancellationToken cancellationToken)
        {
            try
            {
                string containerRId = await this.container.GetRIDAsync(cancellationToken);
                return TryCatch<string>.FromResult(containerRId);
            }
            catch (Exception cosmosException)
            {
                return TryCatch<string>.FromException(cosmosException);
            }
        }

        private async Task<FeedTokenInternal> InitializeFeedTokenAsync(CancellationToken cancellationToken)
        {
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
            IReadOnlyList<Documents.PartitionKeyRange> partitionKeyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                    this.containerRId,
                    new Documents.Routing.Range<string>(
                        Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        isMinInclusive: true,
                        isMaxInclusive: false),
                    forceRefresh: true);
            // ReadAll scenario, initialize with one token for all
            return new FeedTokenEPKRange(this.containerRId, partitionKeyRanges.Select(pkRange => pkRange.ToRange()).ToList(), continuationToken: null);
        }

        public override CosmosElement GetCosmsoElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}