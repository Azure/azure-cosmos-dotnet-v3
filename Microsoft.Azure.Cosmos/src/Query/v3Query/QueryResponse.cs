//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryAdvisor;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

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
            this.memoryStream = memoryStream;
            this.CosmosSerializationOptions = serializationOptions;
        }

        public int Count { get; }

        public override Stream Content => this.memoryStream?.Value;

        internal virtual IReadOnlyList<CosmosElement> CosmosElements { get; }

        internal virtual CosmosQueryResponseMessageHeaders QueryHeaders => (CosmosQueryResponseMessageHeaders)this.Headers;

        internal virtual CosmosSerializationFormatOptions CosmosSerializationOptions { get; }

        internal bool GetHasMoreResults()
        {
            return !string.IsNullOrEmpty(this.Headers.ContinuationToken);
        }

        internal static QueryResponse CreateSuccess(
            IReadOnlyList<CosmosElement> result,
            int count,
            CosmosQueryResponseMessageHeaders responseHeaders,
            CosmosSerializationFormatOptions serializationOptions,
            ITrace trace)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count must be positive");
            }

            Lazy<MemoryStream> memoryStream = new Lazy<MemoryStream>(() => CosmosElementSerializer.ToStream(
                responseHeaders.ContainerRid,
                result,
                responseHeaders.ResourceType,
                serializationOptions));

            QueryResponse cosmosQueryResponse = new QueryResponse(
               result: result,
               count: count,
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
        private readonly IReadOnlyList<T> resource;

        private QueryResponse(
            HttpStatusCode httpStatusCode,
            IReadOnlyList<CosmosElement> cosmosElements,
            CosmosQueryResponseMessageHeaders responseMessageHeaders,
            CosmosDiagnostics diagnostics,
            CosmosSerializerCore serializerCore,
            CosmosSerializationFormatOptions serializationOptions,
            RequestMessage requestMessage)
        {
            this.QueryHeaders = responseMessageHeaders;
            this.Diagnostics = diagnostics;
            this.serializerCore = serializerCore;
            this.serializationOptions = serializationOptions;
            this.StatusCode = httpStatusCode;
            this.resource = CosmosElementSerializer.GetResources<T>(
                cosmosArray: cosmosElements,
                serializerCore: serializerCore);

            // 1/25/2024: The default for request message is plain text
            // for any release after this date, no longer base64 encoded
            this.IndexUtilizationText = ResponseMessage.DecodeIndexMetrics(
                responseMessageHeaders, 
                isBase64Encoded: false);

            this.QueryAdviceText = (this.Headers?.QueryAdvice != null)
                ? new Lazy<string>(() =>
                {
                    Query.Core.QueryAdvisor.QueryAdvice.TryCreateFromString(this.Headers.QueryAdvice, out QueryAdvice queryAdvice);
                    return queryAdvice?.ToString();
                })
                : null;

            this.RequestMessage = requestMessage;
        }

        public override string ContinuationToken => this.Headers.ContinuationToken;

        public override double RequestCharge => this.Headers.RequestCharge;

        public override Headers Headers => this.QueryHeaders;

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        public override int Count => this.resource.Count;

        internal CosmosQueryResponseMessageHeaders QueryHeaders { get; }

        private Lazy<string> IndexUtilizationText { get; }

        public override string IndexMetrics => this.IndexUtilizationText?.Value;

        private Lazy<string> QueryAdviceText { get; }

        internal override string QueryAdvice => this.QueryAdviceText?.Value;

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        public override IEnumerable<T> Resource => this.resource;

        internal override RequestMessage RequestMessage { get; }

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
                    serializationOptions: cosmosQueryResponse.CosmosSerializationOptions,
                    requestMessage: cosmosQueryResponse.RequestMessage);
            }
            return queryResponse;
        }
    }
}