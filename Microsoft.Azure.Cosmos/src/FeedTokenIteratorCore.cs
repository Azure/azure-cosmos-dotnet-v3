//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal class FeedTokenIteratorCore : FeedTokenIterator
    {
        private readonly ContainerCore containerCore;
        private readonly Uri resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private readonly FeedTokenInternal feedTokenInternal;
        private bool hasMoreResultsInternal;

        internal FeedTokenIteratorCore(
            ContainerCore containerCore,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            FeedTokenInternal feedTokenInternal,
            QueryRequestOptions options)
        {
            this.resourceLink = resourceLink;
            this.containerCore = containerCore;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.feedTokenInternal = feedTokenInternal;
            this.requestOptions = options;
            this.hasMoreResultsInternal = true;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        public override FeedToken FeedToken => this.feedTokenInternal;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected QueryRequestOptions requestOptions { get; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            Stream stream = null;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                stream = this.containerCore.ClientContext.SerializerCore.ToStreamSqlQuerySpec(this.querySpec, this.resourceType);    
                operation = OperationType.Query;
            }

            ResponseMessage response = await this.containerCore.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.resourceLink,
               resourceType: this.resourceType,
               operationType: operation,
               requestOptions: this.requestOptions,
               cosmosContainerCore: null,
               partitionKey: this.requestOptions?.PartitionKey,
               streamPayload: stream,
               requestEnricher: request =>
               {
                   QueryRequestOptions.FillContinuationToken(request, this.feedTokenInternal.GetContinuation());
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }

                   this.feedTokenInternal.EnrichRequest(request);
               },
               diagnosticsScope: null,
               cancellationToken: cancellationToken);

            string responseContinuation = response.Headers.ContinuationToken;
            // Retry in case of splits or other scenarios
            if (await this.feedTokenInternal.ShouldRetryAsync(this.containerCore, response, cancellationToken))
            {
                if (response.IsSuccessStatusCode)
                {
                    this.feedTokenInternal.UpdateContinuation(responseContinuation);
                }

                return await this.ReadNextAsync(cancellationToken);
            }

            this.feedTokenInternal.UpdateContinuation(responseContinuation);
            // TODO: How to make this work
            this.hasMoreResultsInternal = this.feedTokenInternal.IsDone();
            return response;
        }

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            continuationToken = this.feedTokenInternal.GetContinuation();
            return true;
        }
    }

    /// <summary>
    /// Cosmos feed iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <typeparam name="T">The response object type that can be deserialized</typeparam>
    internal sealed class FeedTokenIteratorCore<T> : FeedTokenIterator<T>
    {
        private readonly FeedTokenIterator feedIterator;
        private readonly Func<ResponseMessage, FeedResponse<T>> responseCreator;

        internal FeedTokenIteratorCore(
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