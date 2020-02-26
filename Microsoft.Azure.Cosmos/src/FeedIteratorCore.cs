//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator for ReadFeed operations
    /// </summary>
    internal class FeedIteratorCore : FeedIteratorInternal
    {
        private readonly ContainerCore containerCore;
        private readonly CosmosClientContext clientContext;
        private readonly Uri resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private bool hasMoreResultsInternal;
        private FeedTokenInternal feedTokenInternal;

        /// <summary>
        /// For non-partitioned resources
        /// </summary>
        internal FeedIteratorCore(
            CosmosClientContext clientContext,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedTokenInternal feedTokenInternal,
            QueryRequestOptions options)
        {
            this.resourceLink = resourceLink;
            this.clientContext = clientContext;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.feedTokenInternal = feedTokenInternal;
            this.continuationToken = continuationToken ?? this.feedTokenInternal?.GetContinuation();
            this.requestOptions = options;
            this.hasMoreResultsInternal = true;
        }

        /// <summary>
        /// For partitioned resources
        /// </summary>
        internal FeedIteratorCore(
            ContainerCore containerCore,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedTokenInternal feedTokenInternal,
            QueryRequestOptions options)
        {
            this.resourceLink = resourceLink;
            this.containerCore = containerCore;
            this.clientContext = containerCore.ClientContext;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.feedTokenInternal = feedTokenInternal;
            this.continuationToken = continuationToken ?? this.feedTokenInternal?.GetContinuation();
            this.requestOptions = options;
            this.hasMoreResultsInternal = true;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

#if PREVIEW
        public override
#else
        internal
#endif
        FeedToken FeedToken => this.feedTokenInternal;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected QueryRequestOptions requestOptions { get; }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken { get; set; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.requestOptions);
            using (diagnostics.CreateOverallScope("QueryReadNextAsync"))
            {
                return this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }
        }

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnostics,
            CancellationToken cancellationToken = default)
        {
            Stream stream = null;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                stream = this.clientContext.SerializerCore.ToStreamSqlQuerySpec(this.querySpec, this.resourceType);
                operation = OperationType.Query;
            }

            if (this.feedTokenInternal == null)
            {
                TryCatch<FeedTokenInternal> tryCatchFeedTokeninternal = await this.TryInitializeFeedTokenAsync(cancellationToken);
                if (!tryCatchFeedTokeninternal.Succeeded)
                {
                    CosmosException cosmosException = tryCatchFeedTokeninternal.Exception.InnerException as CosmosException;
                    return cosmosException.ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                }

                this.feedTokenInternal = tryCatchFeedTokeninternal.Result;
            }

            ResponseMessage response = await this.clientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.resourceLink,
               resourceType: this.resourceType,
               operationType: operation,
               requestOptions: this.requestOptions,
               cosmosContainerCore: null,
               partitionKey: this.requestOptions?.PartitionKey,
               streamPayload: stream,
               requestEnricher: request =>
               {
                   QueryRequestOptions.FillContinuationToken(request, this.continuationToken);
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }

                   this.feedTokenInternal?.EnrichRequest(request);
               },
               diagnosticsScope: diagnostics,
               cancellationToken: cancellationToken);

            // Retry in case of splits or other scenarios only on partitioned resources
            if (this.containerCore != null
                && await this.feedTokenInternal.ShouldRetryAsync(this.containerCore, response, cancellationToken))
            {
                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                this.feedTokenInternal.UpdateContinuation(response.Headers.ContinuationToken);
                this.continuationToken = this.feedTokenInternal.GetContinuation();
                this.hasMoreResultsInternal = !this.feedTokenInternal.IsDone;
            }
            else
            {
                this.hasMoreResultsInternal = false;
            }

            return response;
        }

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            continuationToken = this.continuationToken;
            return true;
        }

        private async Task<TryCatch<FeedTokenInternal>> TryInitializeFeedTokenAsync(CancellationToken cancellationToken)
        {
            string containerRId = string.Empty;
            if (this.containerCore != null)
            {
                try
                {
                    containerRId = await this.containerCore.GetRIDAsync(cancellationToken);
                }
                catch (CosmosException cosmosException)
                {
                    return TryCatch<FeedTokenInternal>.FromException(cosmosException);
                }
            }

            // Create FeedToken for the full Range
            FeedTokenEPKRange feedTokenInternal = new FeedTokenEPKRange(
                containerRId,
                new PartitionKeyRange()
                {
                    MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey
                });
            // Initialize with the ContinuationToken that the user passed, if any
            if (this.continuationToken != null)
            {
                feedTokenInternal.UpdateContinuation(this.continuationToken);
            }

            return TryCatch<FeedTokenInternal>.FromResult(feedTokenInternal);
        }
    }

    /// <summary>
    /// Cosmos feed iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <typeparam name="T">The response object type that can be deserialized</typeparam>
    internal sealed class FeedIteratorCore<T> : FeedIteratorInternal<T>
    {
        private readonly FeedIteratorInternal feedIterator;
        private readonly Func<ResponseMessage, FeedResponse<T>> responseCreator;

        internal FeedIteratorCore(
            FeedIteratorInternal feedIterator,
            Func<ResponseMessage, FeedResponse<T>> responseCreator)
        {
            this.responseCreator = responseCreator;
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

#if PREVIEW
        public override FeedToken FeedToken => this.feedIterator.FeedToken;
#endif

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

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            return this.feedIterator.TryGetContinuationToken(out continuationToken);
        }
    }
}
