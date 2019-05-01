//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents the template class used by feed methods (enumeration operations) for the Azure Cosmos DB service.
    /// </summary>
    internal class CosmosQueryResponse : CosmosResponseMessage
    {
        internal CosmosQueryResponse(
            IEnumerable<CosmosElement> result,
            int count,
            CosmosQueryResponseMessageHeaders responseHeaders,
            long responseLengthBytes): base(
                statusCode: HttpStatusCode.Accepted,
                requestMessage: null,
                errorMessage: null,
                error: null,
                headers: responseHeaders)
        {
            this.CosmosElements = result;
            this.Count = count;
            this.ResponseLengthBytes = responseLengthBytes;
        }

        internal CosmosQueryResponse(
            HttpStatusCode statusCode,
            CosmosRequestMessage requestMessage,
            string errorMessage,
            Error error,
            CosmosQueryResponseMessageHeaders responseHeaders) 
            : base(
                statusCode: statusCode,
                requestMessage: requestMessage,
                errorMessage: errorMessage,
                error: error,
                headers: responseHeaders)
        {

            this.CosmosElements = Enumerable.Empty<CosmosElement>();
            this.Count = 0;
            this.ResponseLengthBytes = 0;
        }

        public int Count { get; }

        public override Stream Content => CosmosElementSerializer.ToStream(this.CosmosElements, this.CosmosSerializationOptions);

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
    }

    /// <summary>
    /// The cosmos query response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CosmosQueryResponse<T> : CosmosFeedResponse<T>
    {
        private readonly IEnumerable<CosmosElement> cosmosElements;
        private readonly CosmosJsonSerializer jsonSerializer;
        private readonly CosmosSerializationOptions serializationOptions;
        private IEnumerable<T> resources;

        /// <summary>
        /// Create a <see cref="CosmosQueryResponse{T}"/>
        /// </summary>
        private CosmosQueryResponse(
            IEnumerable<CosmosElement> cosmosElements,
            CosmosQueryResponseMessageHeaders responseMessageHeaders,
            bool hasMoreResults,
            CosmosJsonSerializer jsonSerializer,
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
        public override string Continuation
        {
            get => this.Headers.Continuation;
        }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public override double RequestCharge => this.Headers.RequestCharge;

        /// <summary>
        /// Gets the session token for use in session consistency reads from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use in session consistency.
        /// </value>
        public override string SessionToken => this.Headers.GetValueOrDefault(HttpConstants.HttpHeaders.SessionToken);

        /// <summary>
        /// The headers of the response
        /// </summary>
        public override CosmosResponseMessageHeaders Headers => this.QueryHeaders;

        /// <summary>
        /// The number of items in the stream.
        /// </summary>
        public override int Count { get; }

        internal CosmosQueryResponseMessageHeaders QueryHeaders { get; }

        internal override string InternalContinuationToken => this.QueryHeaders.InternalContinuationToken;

        internal override bool HasMoreResults { get; }

        /// <summary>
        /// Get the enumerators to iterate through the results
        /// </summary>
        /// <returns>An enumerator of the response objects</returns>
        public override IEnumerator<T> GetEnumerator()
        {
            if (this.resources == null)
            {
                this.resources = CosmosElementSerializer.Deserialize<T>(
                    this.cosmosElements, 
                    this.jsonSerializer, 
                    this.serializationOptions);
            }

            return this.resources.GetEnumerator();
        }

        internal static CosmosQueryResponse<TInput> CreateResponse<TInput>(
            CosmosQueryResponse cosmosQueryResponse,
            CosmosJsonSerializer jsonSerializer,
            bool hasMoreResults)
        {
            CosmosQueryResponse<TInput> queryResponse;
            using (cosmosQueryResponse)
            {
                cosmosQueryResponse.EnsureSuccessStatusCode();
                queryResponse = new CosmosQueryResponse<TInput>(
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