//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents the template class used by feed methods (enumeration operations) for the Azure Cosmos DB service.
    /// </summary>
    internal class QueryResponse : ResponseMessage
    {
        /// <summary>
        /// Used for unit testing only
        /// </summary>
        internal QueryResponse()
        {
        }

        private QueryResponse(
            IEnumerable<CosmosElement> result,
            int count,
            long responseLengthBytes,
            CosmosQueryResponseMessageHeaders responseHeaders,
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            string errorMessage,
            Error error)
            : base(
                statusCode: statusCode,
                requestMessage: requestMessage,
                errorMessage: errorMessage,
                error: error,
                headers: responseHeaders)
        {
            this.CosmosElements = result;
            this.Count = count;
            this.ResponseLengthBytes = responseLengthBytes;
        }

        public int Count { get; }

        public override Stream Content => CosmosElementSerializer.ToStream(
            this.QueryHeaders.ContainerRid,
            this.CosmosElements, 
            this.QueryHeaders.ResourceType, 
            this.CosmosSerializationOptions);

        internal virtual IEnumerable<CosmosElement> CosmosElements { get; }

        internal virtual CosmosQueryResponseMessageHeaders QueryHeaders => (CosmosQueryResponseMessageHeaders)this.Headers;

        /// <summary>
        /// Gets the response length in bytes
        /// </summary>
        /// <remarks>
        /// This value is only set for Direct mode.
        /// </remarks>
        internal long ResponseLengthBytes { get; }

        /// <summary>
        /// Get the client side request statistics for the current request.
        /// </summary>
        /// <remarks>
        /// This value is currently used for tracking replica Uris.
        /// </remarks>
        internal ClientSideRequestStatistics RequestStatistics { get; }

        internal virtual CosmosSerializationOptions CosmosSerializationOptions { get; set; }

        internal bool GetHasMoreResults()
        {
            return !string.IsNullOrEmpty(this.Headers.Continuation);
        }

        internal static QueryResponse CreateSuccess(
            IEnumerable<CosmosElement> result,
            int count,
            long responseLengthBytes,
            CosmosQueryResponseMessageHeaders responseHeaders)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (responseHeaders == null)
            {
                throw new ArgumentNullException(nameof(responseHeaders));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count must be positive");
            }

            if (responseLengthBytes < 0)
            {
                throw new ArgumentOutOfRangeException("responseLengthBytes must be positive");
            }

            QueryResponse cosmosQueryResponse = new QueryResponse(
               result: result,
               count: count,
               responseLengthBytes: responseLengthBytes,
               responseHeaders: responseHeaders,
               statusCode: HttpStatusCode.Accepted,
               errorMessage: null,
               error: null,
               requestMessage: null);

            return cosmosQueryResponse;
        }

        internal static QueryResponse CreateFailure(
            CosmosQueryResponseMessageHeaders responseHeaders,
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            string errorMessage,
            Error error)
        {
            if (responseHeaders == null)
            {
                throw new ArgumentNullException(nameof(responseHeaders));
            }

            if (errorMessage == null)
            {
                throw new ArgumentNullException(nameof(errorMessage));
            }

            QueryResponse cosmosQueryResponse = new QueryResponse(
                result: Enumerable.Empty<CosmosElement>(),
                count: 0,
                responseLengthBytes: 0,
                responseHeaders: responseHeaders,
                statusCode: statusCode,
                errorMessage: errorMessage,
                error: error,
                requestMessage: requestMessage);

            return cosmosQueryResponse;
        }
    }

    /// <summary>
    /// The cosmos query response
    /// </summary>
    /// <typeparam name="T">The type for the query response.</typeparam>
    internal class QueryResponse<T> : FeedResponse<T>
    {
        private readonly IEnumerable<CosmosElement> cosmosElements;
        private readonly CosmosSerializer jsonSerializer;
        private readonly CosmosSerializationOptions serializationOptions;
        private IEnumerable<T> resources;

        /// <summary>
        /// Create a <see cref="QueryResponse{T}"/>
        /// </summary>
        private QueryResponse(
            IEnumerable<CosmosElement> cosmosElements,
            CosmosQueryResponseMessageHeaders responseMessageHeaders,
            bool hasMoreResults,
            CosmosSerializer jsonSerializer,
            CosmosSerializationOptions serializationOptions)
        {
            this.cosmosElements = cosmosElements;
            this.QueryHeaders = responseMessageHeaders;
            this.HasMoreResults = hasMoreResults;
            this.jsonSerializer = jsonSerializer;
            this.serializationOptions = serializationOptions;
        }

        /// <summary>
        /// Gets the continuation token
        /// </summary>
        public override string Continuation => this.Headers.Continuation;

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public override double RequestCharge => this.Headers.RequestCharge;

        /// <summary>
        /// The headers of the response
        /// </summary>
        public override Headers Headers => this.QueryHeaders;

        /// <summary>
        /// The number of items in the stream.
        /// </summary>
        public override int Count { get; }

        internal CosmosQueryResponseMessageHeaders QueryHeaders { get; }

        internal override string InternalContinuationToken => this.QueryHeaders?.InternalContinuationToken;

        internal override bool HasMoreResults { get; }

        /// <summary>
        /// Get the enumerators to iterate through the results
        /// </summary>
        /// <returns>An enumerator of the response objects</returns>
        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        /// <summary>
        /// Get the enumerators to iterate through the results
        /// </summary>
        /// <returns>An enumerator of the response objects</returns>
        public override IEnumerable<T> Resource
        {
            get
            {
                if (this.resources == null)
                {
                    this.resources = CosmosElementSerializer.Deserialize<T>(
                        this.QueryHeaders.ContainerRid,
                        this.cosmosElements,
                        this.QueryHeaders.ResourceType,
                        this.jsonSerializer,
                        this.serializationOptions);
                }

                return this.resources;
            }
        }

        internal static QueryResponse<TInput> CreateResponse<TInput>(
            QueryResponse cosmosQueryResponse,
            CosmosSerializer jsonSerializer,
            bool hasMoreResults)
        {
            QueryResponse<TInput> queryResponse;
            using (cosmosQueryResponse)
            {
                cosmosQueryResponse.EnsureSuccessStatusCode();
                queryResponse = new QueryResponse<TInput>(
                    cosmosElements: cosmosQueryResponse.CosmosElements,
                    responseMessageHeaders: cosmosQueryResponse.QueryHeaders,
                    hasMoreResults: hasMoreResults,
                    jsonSerializer: jsonSerializer,
                    serializationOptions: cosmosQueryResponse.CosmosSerializationOptions);
            }

            return queryResponse;
        }
    }
}