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
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal class FeedIteratorCore : FeedIterator
    {
        private readonly CosmosClientContext clientContext;
        private readonly Uri resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private readonly bool usePropertySerializer;
        private bool hasMoreResultsInternal;

        internal FeedIteratorCore(
            CosmosClientContext clientContext,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions options,
            bool usePropertySerializer = false)
        {
            this.resourceLink = resourceLink;
            this.clientContext = clientContext;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.continuationToken = continuationToken;
            this.requestOptions = options;
            this.usePropertySerializer = usePropertySerializer;
            this.hasMoreResultsInternal = true;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

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
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Stream stream = null;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                // Use property serializer is for internal query operations like throughput
                // that should not use custom serializer
                CosmosSerializer serializer = this.usePropertySerializer ?
                    this.clientContext.PropertiesSerializer :
                    this.clientContext.SqlQuerySpecSerializer;

                stream = serializer.ToStream(this.querySpec);    
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
                   QueryRequestOptions.FillContinuationToken(request, this.continuationToken);
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }
               },
               cancellationToken: cancellationToken);

            this.continuationToken = response.Headers.ContinuationToken;
            this.hasMoreResultsInternal = GetHasMoreResults(this.continuationToken, response.StatusCode);
            return response;
        }

        internal static string GetContinuationToken(ResponseMessage httpResponseMessage)
        {
            return httpResponseMessage.Headers.ContinuationToken;
        }

        internal static bool GetHasMoreResults(string continuationToken, HttpStatusCode statusCode)
        {
            // this logic might not be sufficient composite continuation token https://msdata.visualstudio.com/CosmosDB/SDK/_workitems/edit/269099
            // in the case where this is a result set iterator for a change feed, not modified indicates that
            // the enumeration is done for now.
            return continuationToken != null && statusCode != HttpStatusCode.NotModified;
        }
    }

    /// <summary>
    /// Cosmos feed iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <typeparam name="T">The response object type that can be deserialized</typeparam>
    internal class FeedIteratorCore<T> : FeedIterator<T>
    {
        private readonly FeedIterator feedIterator;
        private readonly Func<ResponseMessage, FeedResponse<T>> responseCreator;

        internal FeedIteratorCore(
            FeedIterator feedIterator,
            Func<ResponseMessage, FeedResponse<T>> responseCreator)
        {
            this.responseCreator = responseCreator;
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage response = await this.feedIterator.ReadNextAsync(cancellationToken);
            return this.responseCreator(response);
        }
    }
}