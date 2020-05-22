//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal sealed class FeedRangeIteratorCore : FeedIteratorInternal
    {
        internal readonly FeedRangeInternal FeedRangeInternal;
        internal FeedRangeContinuation FeedRangeContinuation { get; private set; }
        private readonly ContainerInternal containerCore;
        private readonly CosmosClientContext clientContext;
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly AsyncLazy<TryCatch<string>> lazyContainerRid;
        private bool hasMoreResultsInternal;

        public static FeedRangeIteratorCore Create(
            ContainerInternal containerCore,
            FeedRangeInternal feedRangeInternal,
            string continuation,
            QueryRequestOptions options)
        {
            if (!string.IsNullOrEmpty(continuation))
            {
                if (FeedRangeContinuation.TryParse(continuation, out FeedRangeContinuation feedRangeContinuation))
                {
                    return new FeedRangeIteratorCore(containerCore, feedRangeContinuation, options);
                }

                // Backward compatible with old format
                feedRangeInternal = FeedRangeEPK.ForCompleteRange();
                feedRangeContinuation = new FeedRangeCompositeContinuation(
                    string.Empty,
                    feedRangeInternal,
                    new List<Documents.Routing.Range<string>>()
                    {
                            new Documents.Routing.Range<string>(
                                Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                                Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                                isMinInclusive: true,
                                isMaxInclusive: false)
                    },
                    continuation);
                return new FeedRangeIteratorCore(containerCore, feedRangeContinuation, options);
            }

            feedRangeInternal = feedRangeInternal ?? FeedRangeEPK.ForCompleteRange();
            return new FeedRangeIteratorCore(containerCore, feedRangeInternal, options);
        }

        /// <summary>
        /// For unit tests
        /// </summary>
        internal FeedRangeIteratorCore(
            ContainerInternal containerCore,
            FeedRangeContinuation feedRangeContinuation,
            QueryRequestOptions options)
            : this(containerCore, feedRangeContinuation.FeedRange, options)
        {
            this.FeedRangeContinuation = feedRangeContinuation;
        }

        private FeedRangeIteratorCore(
            ContainerInternal containerCore,
            FeedRangeInternal feedRangeInternal,
            QueryRequestOptions options)
            : this(containerCore, options)
        {
            this.FeedRangeInternal = feedRangeInternal ?? throw new ArgumentNullException(nameof(feedRangeInternal));
        }

        private FeedRangeIteratorCore(
            ContainerInternal containerCore,
            QueryRequestOptions options)
        {
            this.containerCore = containerCore ?? throw new ArgumentNullException(nameof(containerCore));
            this.clientContext = containerCore.ClientContext;
            this.queryRequestOptions = options;
            this.hasMoreResultsInternal = true;
            this.lazyContainerRid = new AsyncLazy<TryCatch<string>>(valueFactory: (innerCancellationToken) =>
            {
                return this.TryInitializeContainerRIdAsync(innerCancellationToken);
            });
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.clientContext.OperationHelperAsync(
                nameof(FeedRangeIteratorCore),
                this.queryRequestOptions,
                async (diagnostics) =>
                {
                    if (!this.lazyContainerRid.ValueInitialized)
                    {
                        using (diagnostics.CreateScope("InitializeContainerResourceId"))
                        {
                            TryCatch<string> tryInitializeContainerRId = await this.lazyContainerRid.GetValueAsync(cancellationToken);
                            if (!tryInitializeContainerRId.Succeeded)
                            {
                                CosmosException cosmosException = tryInitializeContainerRId.Exception.InnerException as CosmosException;
                                return cosmosException.ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
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
                            else
                            {
                                await this.InitializeFeedContinuationAsync(cancellationToken);
                            }
                        }
                    }

                    return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
                });
        }

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnostics,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.containerCore.LinkUri,
               resourceType: ResourceType.Document,
               operationType: OperationType.ReadFeed,
               requestOptions: this.queryRequestOptions,
               cosmosContainerCore: this.containerCore,
               partitionKey: this.queryRequestOptions?.PartitionKey,
               streamPayload: null,
               requestEnricher: request =>
               {
                   FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(request);
                   this.FeedRangeInternal.Accept(feedRangeVisitor);
                   this.FeedRangeContinuation.Accept(feedRangeVisitor, QueryRequestOptions.FillContinuationToken);
               },
               diagnosticsContext: diagnostics,
               cancellationToken: cancellationToken);

            ShouldRetryResult shouldRetryOnSplit = await this.FeedRangeContinuation.HandleSplitAsync(this.containerCore, responseMessage, cancellationToken);
            if (shouldRetryOnSplit.ShouldRetry)
            {
                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }

            if (responseMessage.Content != null)
            {
                await CosmosElementSerializer.RewriteStreamAsTextAsync(responseMessage, this.queryRequestOptions);
            }

            if (responseMessage.IsSuccessStatusCode)
            {
                this.FeedRangeContinuation.ReplaceContinuation(responseMessage.Headers.ContinuationToken);
                this.hasMoreResultsInternal = !this.FeedRangeContinuation.IsDone;
                return FeedRangeResponse.CreateSuccess(responseMessage, this.FeedRangeContinuation);
            }
            else
            {
                this.hasMoreResultsInternal = false;
                return FeedRangeResponse.CreateFailure(responseMessage);
            }
        }

        private async Task<TryCatch<string>> TryInitializeContainerRIdAsync(CancellationToken cancellationToken)
        {
            try
            {
                string containerRId = await this.containerCore.GetRIDAsync(cancellationToken);
                return TryCatch<string>.FromResult(containerRId);
            }
            catch (CosmosException cosmosException)
            {
                return TryCatch<string>.FromException(cosmosException);
            }
        }

        private async Task InitializeFeedContinuationAsync(CancellationToken cancellationToken)
        {
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
            List<Documents.Routing.Range<string>> effectiveRanges = await this.FeedRangeInternal.GetEffectiveRangesAsync(
                routingMapProvider: partitionKeyRangeCache,
                containerRid: this.lazyContainerRid.Result.Result,
                partitionKeyDefinition: null);

            this.FeedRangeContinuation = new FeedRangeCompositeContinuation(
                containerRid: this.lazyContainerRid.Result.Result,
                feedRange: this.FeedRangeInternal,
                effectiveRanges);
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}
