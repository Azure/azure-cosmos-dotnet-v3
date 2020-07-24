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
        private readonly ContainerInternal container;
        private readonly CosmosClientContext clientContext;
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly AsyncLazy<TryCatch<string>> lazyContainerRid;
        private bool hasMoreResults;

        private FeedRangeContinuation FeedRangeContinuation;

        public ChangeFeedIteratorCore(
            ContainerInternal container,
            ChangeFeedRequestOptions changeFeedRequestOptions)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.clientContext = container.ClientContext;
            this.changeFeedOptions = changeFeedRequestOptions ?? new ChangeFeedRequestOptions();
            this.lazyContainerRid = new AsyncLazy<TryCatch<string>>(valueFactory: (innerCancellationToken) =>
            {
                return this.TryInitializeContainerRIdAsync(innerCancellationToken);
            });
            this.hasMoreResults = true;

            if (changeFeedRequestOptions?.From is ChangeFeedRequestOptions.StartFromContinuation startFromContinuation)
            {
                if (!FeedRangeContinuation.TryParse(startFromContinuation.Continuation, out FeedRangeContinuation feedRangeContinuation))
                {
                    throw new ArgumentException(string.Format(ClientResources.FeedToken_UnknownFormat, startFromContinuation.Continuation));
                }

                this.FeedRangeContinuation = feedRangeContinuation;
                this.changeFeedOptions.FeedRange = feedRangeContinuation.GetFeedRange();
                string continuationToken = feedRangeContinuation.GetContinuation();
                if (continuationToken != null)
                {
                    this.changeFeedOptions.From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuationToken);
                }
                else
                {
                    this.changeFeedOptions.From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning();
                }
            }
        }

        public override bool HasMoreResults => this.hasMoreResults;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.changeFeedOptions);
            using (diagnostics.GetOverallScope())
            {
                diagnostics.AddDiagnosticsInternal(new FeedRangeStatistics(this.changeFeedOptions.FeedRange));
                if (!this.lazyContainerRid.ValueInitialized)
                {
                    using (diagnostics.CreateScope("InitializeContainerResourceId"))
                    {
                        TryCatch<string> tryInitializeContainerRId = await this.lazyContainerRid.GetValueAsync(cancellationToken);
                        if (!tryInitializeContainerRId.Succeeded)
                        {
                            if (!(tryInitializeContainerRId.Exception.InnerException is CosmosException cosmosException))
                            {
                                throw new InvalidOperationException("Failed to convert to CosmosException.");
                            }

                            return cosmosException.ToCosmosResponseMessage(
                                new RequestMessage(
                                    method: null,
                                    requestUriString: null,
                                    diagnosticsContext: diagnostics));
                        }
                    }

                    if (this.FeedRangeContinuation == null)
                    {
                        using (diagnostics.CreateScope("InitializeContinuation"))
                        {
                            await this.InitializeFeedContinuationAsync(cancellationToken);
                        }
                    }

                    TryCatch validateContainer = this.FeedRangeContinuation.ValidateContainer(this.lazyContainerRid.Result.Result);
                    if (!validateContainer.Succeeded)
                    {
                        return CosmosExceptionFactory
                            .CreateBadRequestException(
                                message: validateContainer.Exception.InnerException.Message,
                                innerException: validateContainer.Exception.InnerException,
                                diagnosticsContext: diagnostics)
                            .ToCosmosResponseMessage(
                                new RequestMessage(
                                    method: null,
                                    requestUriString: null,
                                    diagnosticsContext: diagnostics));
                    }
                }

                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }
        }

        public override CosmosElement GetCosmosElementContinuationToken() => CosmosElement.Parse(this.FeedRangeContinuation.ToString());

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnosticsScope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string continuation = this.FeedRangeContinuation.GetContinuation();
            if (continuation != null)
            {
                this.changeFeedOptions.From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(this.FeedRangeContinuation.GetContinuation());
            }

            if ((this.changeFeedOptions.FeedRange == null) || this.changeFeedOptions.FeedRange is FeedRangeEpk)
            {
                // For now the backend does not support EPK Ranges if they don't line up with a PKRangeId
                // So if the range the user supplied is a logical pk value, then we don't want to overwrite it.
                this.changeFeedOptions.FeedRange = this.FeedRangeContinuation.GetFeedRange();
            }

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
                this.hasMoreResults = responseMessage.IsSuccessStatusCode;
                this.FeedRangeContinuation.ReplaceContinuation(responseMessage.Headers.ETag);
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
            // Initializing FeedRangeContinuation (double init pattern, since async needs to be deffered until the first read).
            if (this.FeedRangeContinuation == null)
            {
                FeedRangePartitionKeyRangeExtractor feedRangePartitionKeyRangeExtractor = new FeedRangePartitionKeyRangeExtractor(this.container);

                IReadOnlyList<Documents.Routing.Range<string>> ranges = await ((FeedRangeInternal)this.changeFeedOptions.FeedRange).AcceptAsync(
                    feedRangePartitionKeyRangeExtractor,
                    cancellationToken);

                this.FeedRangeContinuation = new FeedRangeCompositeContinuation(
                    containerRid: this.lazyContainerRid.Result.Result,
                    feedRange: (FeedRangeInternal)this.changeFeedOptions.FeedRange,
                    ranges: ranges);
            }
        }
    }
}