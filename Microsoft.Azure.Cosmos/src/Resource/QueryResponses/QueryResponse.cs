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
    using Microsoft.Azure.Cosmos.Query;
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
            IReadOnlyDictionary<string, QueryMetrics> queryMetrics,
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
            this.queryMetrics = queryMetrics;
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

        internal IReadOnlyDictionary<string, QueryMetrics> queryMetrics { get; set; }

        internal virtual CosmosSerializationFormatOptions CosmosSerializationOptions { get; set; }

        internal bool GetHasMoreResults()
        {
            return !string.IsNullOrEmpty(this.Headers.ContinuationToken);
        }

        internal static QueryResponse CreateSuccess(
            IEnumerable<CosmosElement> result,
            int count,
            long responseLengthBytes,
            CosmosQueryResponseMessageHeaders responseHeaders,
            IReadOnlyDictionary<string, QueryMetrics> queryMetrics = null)
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
               queryMetrics: queryMetrics,
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
                queryMetrics: null,
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
        private IEnumerable<T> resources;

        private QueryResponse(
            HttpStatusCode httpStatusCode,
            IEnumerable<CosmosElement> cosmosElements,
            CosmosQueryResponseMessageHeaders responseMessageHeaders,
            CosmosDiagnostics diagnostics,
            CosmosSerializer jsonSerializer,
            CosmosSerializationFormatOptions serializationOptions)
        {
            this.cosmosElements = cosmosElements;
            this.QueryHeaders = responseMessageHeaders;
            this.Diagnostics = diagnostics;
            this.jsonSerializer = jsonSerializer;
            this.serializationOptions = serializationOptions;
            this.StatusCode = httpStatusCode;
        }

        public override string ContinuationToken => this.Headers.ContinuationToken;

        public override double RequestCharge => this.Headers.RequestCharge;

        public override Headers Headers => this.QueryHeaders;

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        public override int Count => this.cosmosElements?.Count() ?? 0;

        internal CosmosQueryResponseMessageHeaders QueryHeaders { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

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
            CosmosSerializer jsonSerializer)
        {
            QueryResponse<TInput> queryResponse;
            using (cosmosQueryResponse)
            {
                cosmosQueryResponse.EnsureSuccessStatusCode();
                queryResponse = new QueryResponse<TInput>(
                    httpStatusCode: cosmosQueryResponse.StatusCode,
                    cosmosElements: cosmosQueryResponse.CosmosElements,
                    responseMessageHeaders: cosmosQueryResponse.QueryHeaders,
                    diagnostics: cosmosQueryResponse.Diagnostics,
                    jsonSerializer: jsonSerializer,
                    serializationOptions: cosmosQueryResponse.CosmosSerializationOptions);
            }
            return queryResponse;
        }
    }
}