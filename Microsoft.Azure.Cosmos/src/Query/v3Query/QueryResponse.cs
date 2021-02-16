//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Represents the template class used by feed methods (enumeration operations) for the Azure Cosmos DB service.
    /// </summary>
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    class QueryResponse : ResponseMessage
    {
        private readonly Lazy<MemoryStream> memoryStream;

        /// <summary>
        /// Used for unit testing only
        /// </summary>
        internal QueryResponse()
        {
        }

        private QueryResponse(
            IReadOnlyList<CosmosElement> result,
            int count,
            long responseLengthBytes,
            CosmosQueryResponseMessageHeaders responseHeaders,
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            CosmosException cosmosException,
            Lazy<MemoryStream> memoryStream,
            CosmosSerializationFormatOptions serializationOptions,
            ITrace trace)
            : base(
                statusCode: statusCode,
                requestMessage: requestMessage,
                cosmosException: cosmosException,
                headers: responseHeaders,
                trace: trace)
        {
            this.CosmosElements = result;
            this.Count = count;
            this.ResponseLengthBytes = responseLengthBytes;
            this.memoryStream = memoryStream;
            this.CosmosSerializationOptions = serializationOptions;
        }

        public int Count { get; }

        public override Stream Content => this.memoryStream?.Value;

        internal virtual IReadOnlyList<CosmosElement> CosmosElements { get; }

        internal virtual CosmosQueryResponseMessageHeaders QueryHeaders => (CosmosQueryResponseMessageHeaders)this.Headers;

        /// <summary>
        /// Gets the response length in bytes
        /// </summary>
        /// <remarks>
        /// This value is only set for Direct mode.
        /// </remarks>
        internal long ResponseLengthBytes { get; }

        internal virtual CosmosSerializationFormatOptions CosmosSerializationOptions { get; }

        internal bool GetHasMoreResults()
        {
            return !string.IsNullOrEmpty(this.Headers.ContinuationToken);
        }

        internal static QueryResponse CreateSuccess(
            IReadOnlyList<CosmosElement> result,
            int count,
            long responseLengthBytes,
            CosmosQueryResponseMessageHeaders responseHeaders,
            CosmosSerializationFormatOptions serializationOptions,
            ITrace trace)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count must be positive");
            }

            if (responseLengthBytes < 0)
            {
                throw new ArgumentOutOfRangeException("responseLengthBytes must be positive");
            }

            Lazy<MemoryStream> memoryStream = new Lazy<MemoryStream>(() => CosmosElementSerializer.ToStream(
                responseHeaders.ContainerRid,
                result,
                responseHeaders.ResourceType,
                serializationOptions));

            QueryResponse cosmosQueryResponse = new QueryResponse(
               result: result,
               count: count,
               responseLengthBytes: responseLengthBytes,
               responseHeaders: responseHeaders,
               statusCode: HttpStatusCode.OK,
               cosmosException: null,
               requestMessage: null,
               memoryStream: memoryStream,
               serializationOptions: serializationOptions,
               trace: trace);

            return cosmosQueryResponse;
        }

        internal static QueryResponse CreateFailure(
            CosmosQueryResponseMessageHeaders responseHeaders,
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            CosmosException cosmosException,
            ITrace trace)
        {
            QueryResponse cosmosQueryResponse = new QueryResponse(
                result: new List<CosmosElement>(),
                count: 0,
                responseLengthBytes: 0,
                responseHeaders: responseHeaders,
                statusCode: statusCode,
                cosmosException: cosmosException,
                requestMessage: requestMessage,
                memoryStream: null,
                serializationOptions: null,
                trace: trace);

            return cosmosQueryResponse;
        }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
    }

    /// <summary>
    /// The cosmos query response
    /// </summary>
    /// <typeparam name="T">The type for the query response.</typeparam>
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    class QueryResponse<T> : FeedResponse<T>
    {
        private readonly CosmosSerializerCore serializerCore;
        private readonly CosmosSerializationFormatOptions serializationOptions;

        private QueryResponse(
            HttpStatusCode httpStatusCode,
            IReadOnlyList<CosmosElement> cosmosElements,
            CosmosQueryResponseMessageHeaders responseMessageHeaders,
            CosmosDiagnostics diagnostics,
            CosmosSerializerCore serializerCore,
            CosmosSerializationFormatOptions serializationOptions)
        {
            this.QueryHeaders = responseMessageHeaders;
            this.Diagnostics = diagnostics;
            this.serializerCore = serializerCore;
            this.serializationOptions = serializationOptions;
            this.StatusCode = httpStatusCode;
            this.Count = cosmosElements.Count;
            this.Resource = CosmosElementSerializer.GetResources<T>(
                cosmosArray: cosmosElements,
                serializerCore: serializerCore);
        }

        public override string ContinuationToken => this.Headers.ContinuationToken;

        public override double RequestCharge => this.Headers.RequestCharge;

        public override Headers Headers => this.QueryHeaders;

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        public override int Count { get; }

        internal CosmosQueryResponseMessageHeaders QueryHeaders { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        public override IEnumerable<T> Resource { get; }

        internal static QueryResponse<TInput> CreateResponse<TInput>(
            QueryResponse cosmosQueryResponse,
            CosmosSerializerCore serializerCore)
        {
            QueryResponse<TInput> queryResponse;
            using (cosmosQueryResponse)
            {
                _ = cosmosQueryResponse.EnsureSuccessStatusCode();

                queryResponse = new QueryResponse<TInput>(
                    httpStatusCode: cosmosQueryResponse.StatusCode,
                    cosmosElements: cosmosQueryResponse.CosmosElements,
                    responseMessageHeaders: cosmosQueryResponse.QueryHeaders,
                    diagnostics: cosmosQueryResponse.Diagnostics,
                    serializerCore: serializerCore,
                    serializationOptions: cosmosQueryResponse.CosmosSerializationOptions);
            }
            return queryResponse;
        }
    }
}