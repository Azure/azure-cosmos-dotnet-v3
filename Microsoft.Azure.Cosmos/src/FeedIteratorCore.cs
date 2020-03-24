//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal sealed class FeedIteratorCore : FeedIteratorInternal
    {
        private readonly ContainerCore containerCore;
        private readonly CosmosClientContext clientContext;
        private readonly Uri resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private readonly AsyncLazy<TryCatch<string>> lazyContainerRid;
        private IQueryFeedToken queryFeedToken;
        private bool hasMoreResultsInternal;

        internal static FeedIteratorCore CreateForNonPartitionedResource( 
            CosmosClientContext clientContext,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions options)
        {
            return new FeedIteratorCore(
                clientContext: clientContext,
                containerCore: null,
                resourceLink: resourceLink,
                resourceType: resourceType,
                queryDefinition: queryDefinition,
                continuationToken: continuationToken,
                queryFeedToken: null,
                options: options);
        }

        internal static FeedIteratorCore CreateForPartitionedResource(
            ContainerCore containerCore,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            IQueryFeedToken queryFeedToken,
            QueryRequestOptions options)
        {
            if (containerCore == null)
            {
                throw new ArgumentNullException(nameof(containerCore));
            }

            return new FeedIteratorCore(
                containerCore: containerCore,
                clientContext: containerCore.ClientContext,
                resourceLink: resourceLink,
                resourceType: resourceType,
                queryDefinition: queryDefinition,
                continuationToken: continuationToken,
                queryFeedToken: queryFeedToken,
                options: options);
        }

        private FeedIteratorCore(
            ContainerCore containerCore,
            CosmosClientContext clientContext,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            IQueryFeedToken queryFeedToken,
            QueryRequestOptions options)
        {
            this.resourceLink = resourceLink;
            this.containerCore = containerCore;
            this.clientContext = clientContext;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.queryFeedToken = queryFeedToken;
            this.ContinuationToken = continuationToken ?? this.queryFeedToken?.GetContinuation();
            this.requestOptions = options;
            this.hasMoreResultsInternal = true;
            this.lazyContainerRid = new AsyncLazy<TryCatch<string>>(valueFactory: (innerCancellationToken) =>
            {
                return this.TryInitializeContainerRIdAsync(innerCancellationToken);
            });
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

#if PREVIEW
        public override
#else
        internal
#endif
        QueryFeedToken FeedToken => new QueryFeedTokenInternal(this.queryFeedToken, queryDefinition: null);

        /// <summary>
        /// The query options for the result set
        /// </summary>
        public QueryRequestOptions requestOptions { get; }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnostics = CosmosDiagnosticsContext.Create(this.requestOptions);
            using (diagnostics.GetOverallScope())
            {
                if (!this.lazyContainerRid.ValueInitialized)
                {
                    TryCatch<string> tryInitializeContainerRId = await this.lazyContainerRid.GetValueAsync(cancellationToken);
                    if (!tryInitializeContainerRId.Succeeded)
                    {
                        if (tryInitializeContainerRId.Exception.InnerException is CosmosException cosmosException)
                        {
                            return cosmosException.ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                        }

                        return CosmosExceptionFactory.CreateInternalServerErrorException(
                            message: ClientResources.FeedToken_CannotGetContainerRid,
                            innerException: tryInitializeContainerRId.Exception.InnerException,
                            diagnosticsContext: diagnostics).ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                    }

                    // If there is an initial FeedToken, validate Container
                    if (this.queryFeedToken != null)
                    {
                        TryCatch validateContainer = this.queryFeedToken.ValidateContainer(this.lazyContainerRid.Result.Result);
                        if (!validateContainer.Succeeded)
                        {
                            return CosmosExceptionFactory.CreateInternalServerErrorException(
                                message: validateContainer.Exception.InnerException.Message,
                                innerException: validateContainer.Exception.InnerException,
                                diagnosticsContext: diagnostics).ToCosmosResponseMessage(new RequestMessage(method: null, requestUri: null, diagnosticsContext: diagnostics));
                        }
                    }
                }

                if (this.queryFeedToken == null)
                {
                    this.queryFeedToken = this.InitializeFeedToken();
                }

                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }
        }

        private async Task<ResponseMessage> ReadNextInternalAsync(
            CosmosDiagnosticsContext diagnostics,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Stream stream = null;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                stream = this.clientContext.SerializerCore.ToStreamSqlQuerySpec(this.querySpec, this.resourceType);
                operation = OperationType.Query;
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
                   QueryRequestOptions.FillContinuationToken(request, this.ContinuationToken);
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }

                   this.queryFeedToken?.EnrichRequest(request);
               },
               diagnosticsContext: diagnostics,
               cancellationToken: cancellationToken);

            // Retry in case of splits or other scenarios only on partitioned resources
            if (this.containerCore != null
                && await this.queryFeedToken.ShouldRetryAsync(this.containerCore, response, cancellationToken))
            {
                return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                this.queryFeedToken.UpdateContinuation(response.Headers.ContinuationToken);
                this.ContinuationToken = this.queryFeedToken.GetContinuation();
                this.hasMoreResultsInternal = !this.queryFeedToken.IsDone;
            }
            else
            {
                this.hasMoreResultsInternal = false;
            }

            return response;
        }

        private async Task<TryCatch<string>> TryInitializeContainerRIdAsync(CancellationToken cancellationToken)
        {
            string containerRId = string.Empty;
            if (this.containerCore != null)
            {
                try
                {
                    containerRId = await this.containerCore.GetRIDAsync(cancellationToken);
                }
                catch (Exception cosmosException)
                {
                    return TryCatch<string>.FromException(cosmosException);
                }
            }

            return TryCatch<string>.FromResult(containerRId);
        }

        private IQueryFeedToken InitializeFeedToken()
        {
            // Create FeedToken for the full Range
            FeedTokenEPKRange feedTokenInternal = new FeedTokenEPKRange(
                        this.lazyContainerRid.Result.Result,
                        new Documents.Routing.Range<string>(Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, true, false),
                        continuationToken: this.ContinuationToken);

            return feedTokenInternal;
        }

        public override CosmosElement GetCosmsoElementContinuationToken()
        {
            throw new NotImplementedException();
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

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.feedIterator.GetCosmsoElementContinuationToken();
        }

#if PREVIEW
        public override QueryFeedToken FeedToken => this.feedIterator.FeedToken;
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
    }
}
