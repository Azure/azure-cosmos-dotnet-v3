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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;

    /// <summary>
    /// Cosmos Change Feed iterator using FeedToken
    /// </summary>
    internal sealed class ChangeFeedIteratorCore : ChangeFeedIterator
    {
        internal FeedRangeContinuation FeedRangeContinuation { get; private set; }
        internal FeedRangeInternal FeedRangeInternal { get; private set; }
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore container;
        private readonly AsyncLazy<TryCatch<string>> lazyContainerRid;
        private bool hasMoreResults = true;

        public static ChangeFeedIteratorCore Create(
            ContainerCore container,
            FeedRangeInternal feedRangeInternal,
            string continuation,
            ChangeFeedRequestOptions changeFeedRequestOptions)
        {
            if (!string.IsNullOrEmpty(continuation))
            {
                if (FeedRangeContinuation.TryCreateFromString(continuation, out FeedRangeContinuation feedRangeContinuation))
                {
                    return new ChangeFeedIteratorCore(container, feedRangeContinuation, changeFeedRequestOptions);
                }
                else
                {
                    throw new ArgumentException(ClientResources.FeedToken_UnknownFormat);
                }
            }

            feedRangeInternal = feedRangeInternal ?? FeedRangeEPK.ForCompleteRange();
            return new ChangeFeedIteratorCore(container, feedRangeInternal, changeFeedRequestOptions);
        }

        internal ChangeFeedIteratorCore(
            ContainerCore container,
            FeedRangeContinuation feedRangeContinuation,
            ChangeFeedRequestOptions changeFeedRequestOptions)
            : this(container, feedRangeContinuation.FeedRange, changeFeedRequestOptions)
        {
            this.FeedRangeContinuation = feedRangeContinuation ?? throw new ArgumentNullException(nameof(feedRangeContinuation));
        }

        private ChangeFeedIteratorCore(
            ContainerCore container,
            FeedRangeInternal feedRangeInternal,
            ChangeFeedRequestOptions changeFeedRequestOptions)
            : this(container, changeFeedRequestOptions)
        {
            this.FeedRangeInternal = feedRangeInternal ?? throw new ArgumentNullException(nameof(feedRangeInternal));
        }

        private ChangeFeedIteratorCore(
            ContainerCore container,
            ChangeFeedRequestOptions changeFeedRequestOptions)
        {
            if (changeFeedRequestOptions != null
                && changeFeedRequestOptions.MaxItemCount.HasValue
                && changeFeedRequestOptions.MaxItemCount.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(changeFeedRequestOptions.MaxItemCount));
            }

            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.clientContext = container.ClientContext;
            this.changeFeedOptions = changeFeedRequestOptions ?? new ChangeFeedRequestOptions();
            this.lazyContainerRid = new AsyncLazy<TryCatch<string>>(valueFactory: (innerCancellationToken) =>
            {
                return this.TryInitializeContainerRIdAsync(innerCancellationToken);
            });
        }

        public override bool HasMoreResults => this.hasMoreResults;

        public override string Continuation => this.FeedRangeContinuation?.ToString();

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.changeFeedOptions);
            using (diagnostics.GetOverallScope())
            {
                if (!this.lazyContainerRid.ValueInitialized)
                {
                    using (diagnostics.CreateScope("InitializeContainerResourceId"))
                    {
                        TryCatch<string> tryInitializeContainerRId = await this.lazyContainerRid.GetValueAsync(cancellationToken);
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
                    }

                    using (diagnostics.CreateScope("InitializeContinuation"))
                    {
                        if (this.FeedRangeContinuation != null)
                        {
                            TryCatch validateContainer = this.FeedRangeContinuation.ValidateContainer(this.lazyContainerRid.Result.Result);
                            if (!validateContainer.Succeeded)
                            {
                                return CosmosExceptionFactory.CreateBadRequestException(
                                    message: validateContainer.Exception.InnerException.Message,
                                    innerException: validateContainer.Exception.InnerException,
                                    diagnosticsContext: diagnostics).ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                            }
                        }

                        await this.InitializeFeedContinuationAsync(cancellationToken);
                    }
                }

                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }
        }

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnosticsScope,
            CancellationToken cancellationToken)
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
                    FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(request);
                    this.FeedRangeInternal.Accept(feedRangeVisitor);
                    this.FeedRangeContinuation.Accept(feedRangeVisitor, ChangeFeedRequestOptions.FillContinuationToken);
                },
                partitionKey: null,
                streamPayload: null,
                diagnosticsContext: diagnosticsScope,
                cancellationToken: cancellationToken);

            // Retry in case of splits or other scenarios
            if (await this.FeedRangeContinuation.ShouldRetryAsync(this.container, responseMessage, cancellationToken))
            {
                if (responseMessage.IsSuccessStatusCode
                    || responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    // Change Feed read uses Etag for continuation
                    this.FeedRangeContinuation.UpdateContinuation(responseMessage.Headers.ETag);
                }

                return await this.ReadNextInternalAsync(diagnosticsScope, cancellationToken);
            }

            if (responseMessage.IsSuccessStatusCode
                || responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                // Change Feed read uses Etag for continuation
                this.FeedRangeContinuation.UpdateContinuation(responseMessage.Headers.ETag);
            }

            this.hasMoreResults = responseMessage.IsSuccessStatusCode;
            return new FeedRangeResponse(
                responseMessage,
                this.FeedRangeContinuation);
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

        private async Task InitializeFeedContinuationAsync(CancellationToken cancellationToken)
        {
            if (this.FeedRangeContinuation == null)
            {
                if (this.FeedRangeInternal is FeedRangePartitionKey)
                {
                    this.FeedRangeContinuation = new FeedRangeSimpleContinuation(
                        containerRid: this.lazyContainerRid.Result.Result,
                        feedRange: this.FeedRangeInternal);
                }
                else
                {
                    Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                    IReadOnlyList<Documents.PartitionKeyRange> pkRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                        collectionRid: this.lazyContainerRid.Result.Result,
                        range: (this.FeedRangeInternal as FeedRangeEPK).Range,
                        forceRefresh: false);

                    this.FeedRangeContinuation = new FeedRangeCompositeContinuation(
                        containerRid: this.lazyContainerRid.Result.Result,
                        feedRange: this.FeedRangeInternal,
                        ranges: pkRanges.Select(pkRange => pkRange.ToRange()).ToList());
                }
            }
            else if (this.FeedRangeInternal is FeedRangePartitionKeyRange)
            {
                // Migration from PKRangeId scenario from V2 SDK
                Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                List<Documents.Routing.Range<string>> effectiveRanges = await this.FeedRangeInternal.GetEffectiveRangesAsync(
                    routingMapProvider: partitionKeyRangeCache,
                    containerRid: this.lazyContainerRid.Result.Result,
                    partitionKeyDefinition: null);

                // Override the original PKRangeId based FeedRange
                this.FeedRangeInternal = new FeedRangeEPK(effectiveRanges[0]);
                this.FeedRangeContinuation = new FeedRangeCompositeContinuation(
                    containerRid: this.lazyContainerRid.Result.Result,
                    feedRange: this.FeedRangeInternal,
                    ranges: effectiveRanges,
                    continuation: this.FeedRangeContinuation.GetContinuation());
            }
        }
    }

    internal sealed class ChangeFeedIteratorCore<T> : ChangeFeedIterator<T>
    {
        private readonly ChangeFeedIteratorCore feedIterator;
        private readonly Func<ResponseMessage, FeedResponse<T>> responseCreator;

        internal ChangeFeedIteratorCore(
            ChangeFeedIteratorCore feedIterator,
            Func<ResponseMessage, FeedResponse<T>> responseCreator)
        {
            this.responseCreator = responseCreator;
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override string Continuation => this.feedIterator.Continuation;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage response = await this.feedIterator.ReadNextAsync(cancellationToken);
            return this.responseCreator(response);
        }
    }
}