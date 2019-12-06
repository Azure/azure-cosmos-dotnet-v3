//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents the template class used by feed methods (enumeration operations) for the Azure Cosmos DB service.
    /// </summary>
    internal class QueryResponse : ResponseMessage
    {
        private readonly Lazy<MemoryStream> memoryStream;

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
            this.memoryStream = new Lazy<MemoryStream>(() => CosmosElementSerializer.ToStream(
                        this.QueryHeaders.ContainerRid,
                        this.CosmosElements,
                        this.QueryHeaders.ResourceType,
                        this.CosmosSerializationOptions));
        }

        public int Count { get; }

        public override Stream ContentStream
        {
            get
            {
                return this.memoryStream.Value;
            }
        }

        internal virtual IEnumerable<CosmosElement> CosmosElements { get; }

        internal virtual CosmosQueryResponseMessageHeaders QueryHeaders => (CosmosQueryResponseMessageHeaders)this.CosmosHeaders;

        /// <summary>
        /// Gets the response length in bytes
        /// </summary>
        /// <remarks>
        /// This value is only set for Direct mode.
        /// </remarks>
        internal long ResponseLengthBytes { get; }

        internal virtual CosmosSerializationFormatOptions CosmosSerializationOptions { get; set; }

        internal bool GetHasMoreResults()
        {
            return !string.IsNullOrEmpty(this.CosmosHeaders.ContinuationToken);
        }

        internal static QueryResponse CreateSuccess(
            IEnumerable<CosmosElement> result,
            int count,
            long responseLengthBytes,
            CosmosQueryResponseMessageHeaders responseHeaders)
        {
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
               statusCode: HttpStatusCode.OK,
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
        private readonly CosmosSerializationFormatOptions serializationOptions;
        private readonly Response response;
        private IEnumerable<T> resources;

        private QueryResponse(
            Response response,
            IEnumerable<CosmosElement> cosmosElements,
            CosmosQueryResponseMessageHeaders responseMessageHeaders,
            CosmosSerializer jsonSerializer,
            CosmosSerializationFormatOptions serializationOptions)
        {
            this.cosmosElements = cosmosElements;
            this.QueryHeaders = responseMessageHeaders;
            this.response = response;
            this.jsonSerializer = jsonSerializer;
            this.serializationOptions = serializationOptions;
        }

        public override string ContinuationToken => this.response.Headers.GetContinuationToken();

        //public override double RequestCharge => this.Headers.RequestCharge;

        //public override Headers Headers => this.QueryHeaders;

        //public override HttpStatusCode StatusCode { get; }

        //public override CosmosDiagnostics Diagnostics { get; }

        public override int Count => this.cosmosElements?.Count() ?? 0;

        internal CosmosQueryResponseMessageHeaders QueryHeaders { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Value.GetEnumerator();
        }

        public override IEnumerable<T> Value
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
            CosmosSerializer jsonSerializer)
        {
            QueryResponse<TInput> queryResponse;
            using (cosmosQueryResponse)
            {
                cosmosQueryResponse.EnsureSuccessStatusCode();
                queryResponse = new QueryResponse<TInput>(
                    response: cosmosQueryResponse,
                    cosmosElements: cosmosQueryResponse.CosmosElements,
                    responseMessageHeaders: cosmosQueryResponse.QueryHeaders,
                    jsonSerializer: jsonSerializer,
                    serializationOptions: cosmosQueryResponse.CosmosSerializationOptions);
            }
            return queryResponse;
        }

        public override Response GetRawResponse() => this.response;
    }
}