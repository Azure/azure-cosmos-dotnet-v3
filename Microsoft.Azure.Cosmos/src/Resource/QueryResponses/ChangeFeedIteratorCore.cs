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
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos Change Feed iterator using FeedToken
    /// </summary>
    internal sealed class ChangeFeedIteratorCore : FeedIteratorInternal
    {
        internal FeedRangeInternal FeedRangeInternal;
        internal FeedRangeContinuation FeedRangeContinuation { get; private set; }
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore container;
        private readonly AsyncLazy<TryCatch<string>> lazyContainerRid;
        private bool hasMoreResults = true;

        public static ChangeFeedIteratorCore Create(
            ContainerCore container,
            ChangeFeedRequestOptions changeFeedRequestOptions)
        {
            if (changeFeedRequestOptions?.From is ChangeFeedRequestOptions.StartFromContinuation startFromContinuation)
            {
                if (FeedRangeContinuation.TryParse(startFromContinuation.Continuation, out FeedRangeContinuation feedRangeContinuation))
                {
                    return new ChangeFeedIteratorCore(container, feedRangeContinuation, changeFeedRequestOptions);
                }
                else
                {
                    throw new ArgumentException(string.Format(ClientResources.FeedToken_UnknownFormat, startFromContinuation.Continuation));
                }
            }

            changeFeedRequestOptions.FeedRange ??= FeedRangeEPK.ForCompleteRange();
            return new ChangeFeedIteratorCore(container, (FeedRangeInternal)changeFeedRequestOptions.FeedRange, changeFeedRequestOptions);
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
                diagnostics.AddDiagnosticsInternal(new FeedRangeStatistics(this.FeedRangeInternal));
                if (!this.lazyContainerRid.ValueInitialized)
                {
                    using (diagnostics.CreateScope("InitializeContainerResourceId"))
                    {
                        TryCatch<string> tryInitializeContainerRId = await this.lazyContainerRid.GetValueAsync(cancellationToken);
                        if (!tryInitializeContainerRId.Succeeded)
                        {
                            CosmosException cosmosException = tryInitializeContainerRId.Exception.InnerException as CosmosException;
                            return cosmosException.ToCosmosResponseMessage(
                                new RequestMessage(
                                    method: null,
                                    requestUri: null,
                                    diagnosticsContext: diagnostics));
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

        public override CosmosElement GetCosmsoElementContinuationToken() => CosmosElement.Parse(this.FeedRangeContinuation.ToString());

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnosticsScope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: this.changeFeedOptions,
                cosmosContainerCore: this.container,
                requestEnricher: default,
                partitionKey: default,
                streamPayload: default,
                diagnosticsContext: diagnosticsScope,
                cancellationToken: cancellationToken);

            if (await this.ShouldRetryAsync(responseMessage, cancellationToken))
            {
                return await this.ReadNextInternalAsync(diagnosticsScope, cancellationToken);
            }

            if (responseMessage.IsSuccessStatusCode
                || (responseMessage.StatusCode == HttpStatusCode.NotModified))
            {
                // Change Feed read uses Etag for continuation
                this.FeedRangeContinuation.ReplaceContinuation(responseMessage.Headers.ETag);
                this.changeFeedOptions.From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(responseMessage.Headers.ETag);
                this.hasMoreResults = responseMessage.IsSuccessStatusCode;
                return FeedRangeResponse.CreateSuccess(
                    responseMessage,
                    this.FeedRangeContinuation);
            }
            else
            {
                this.hasMoreResults = false;
                return FeedRangeResponse.CreateFailure(responseMessage);
            }
        }

        private async Task<bool> ShouldRetryAsync(
            ResponseMessage responseMessage,
            CancellationToken cancellationToken)
        {
            ShouldRetryResult shouldRetryOnNotModified = this.FeedRangeContinuation.HandleChangeFeedNotModified(responseMessage);
            if (shouldRetryOnNotModified.ShouldRetry)
            {
                return true;
            }

            ShouldRetryResult shouldRetryOnSplit = await this.FeedRangeContinuation.HandleSplitAsync(this.container, responseMessage, cancellationToken);
            if (shouldRetryOnSplit.ShouldRetry)
            {
                return true;
            }

            return false;
        }

        private async Task<TryCatch<string>> TryInitializeContainerRIdAsync(CancellationToken cancellationToken)
        {
            try
            {
                string containerRId = await this.container.GetRIDAsync(cancellationToken);
                return TryCatch<string>.FromResult(containerRId);
            }
            catch (CosmosException cosmosException)
            {
                return TryCatch<string>.FromException(cosmosException);
            }
        }

        private async Task InitializeFeedContinuationAsync(CancellationToken cancellationToken)
        {
            if (this.FeedRangeContinuation == null)
            {
                Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                List<Documents.Routing.Range<string>> ranges;
                if (this.FeedRangeInternal is FeedRangePartitionKey)
                {
                    PartitionKeyDefinition partitionKeyDefinition = await this.container.GetPartitionKeyDefinitionAsync(cancellationToken);
                    ranges = await this.FeedRangeInternal.GetEffectiveRangesAsync(partitionKeyRangeCache, this.lazyContainerRid.Result.Result, partitionKeyDefinition);
                }
                else
                {
                    IReadOnlyList<PartitionKeyRange> pkRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                            collectionRid: this.lazyContainerRid.Result.Result,
                            range: (this.FeedRangeInternal as FeedRangeEPK).Range,
                            forceRefresh: false);
                    ranges = pkRanges.Select(pkRange => pkRange.ToRange()).ToList();
                }

                this.FeedRangeContinuation = new FeedRangeCompositeContinuation(
                    containerRid: this.lazyContainerRid.Result.Result,
                    feedRange: this.FeedRangeInternal,
                    ranges: ranges);
            }
            else if (this.FeedRangeInternal is FeedRangePartitionKeyRange)
            {
                // Migration from PKRangeId scenario
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

            this.changeFeedOptions.FeedRange = this.FeedRangeInternal;
        }
    }
}