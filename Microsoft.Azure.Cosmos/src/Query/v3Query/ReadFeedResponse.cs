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

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
        protected ReadFeedResponse(
            HttpStatusCode httpStatusCode,
            CosmosArray cosmosArray,
            CosmosSerializerCore serializerCore,
            Headers responseMessageHeaders,
            CosmosDiagnostics diagnostics)
        {
            this.Count = cosmosArray != null ? cosmosArray.Count : 0;
            this.Headers = responseMessageHeaders;
            this.StatusCode = httpStatusCode;
            this.Diagnostics = diagnostics;
            this.Resource = CosmosElementSerializer.GetResources<T>(
                cosmosArray: cosmosArray,
                serializerCore: serializerCore);
        }

        protected ReadFeedResponse(
            HttpStatusCode httpStatusCode,
            IReadOnlyList<CosmosElement> cosmosArray,
            CosmosSerializerCore serializerCore,
            Headers responseMessageHeaders,
            CosmosDiagnostics diagnostics,
            IReadOnlyList<DecryptionInfo> decryptionInfo)
        {
            this.Count = cosmosArray != null ? cosmosArray.Count : 0;
            this.Headers = responseMessageHeaders;
            this.StatusCode = httpStatusCode;
            this.Diagnostics = diagnostics;
            this.Resource = CosmosElementSerializer.GetResources<T>(
                cosmosArray: cosmosArray,
                serializerCore: serializerCore);
            this.DecryptionInfo = decryptionInfo;
        }

        public override int Count { get; }

        public override string ContinuationToken => this.Headers?.ContinuationToken;

        public override Headers Headers { get; }

        public override IEnumerable<T> Resource { get; }

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        public override IReadOnlyList<DecryptionInfo> DecryptionInfo { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            ResponseMessage responseMessage,
            CosmosSerializerCore serializerCore,
            Documents.ResourceType resourceType)
        {
            using (responseMessage)
            {
                // ReadFeed can return 304 on some scenarios (Change Feed for example)
                if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                {
                    responseMessage.EnsureSuccessStatusCode();
                }

                ReadFeedResponse readFeedResponse = responseMessage as ReadFeedResponse;
                if (readFeedResponse != null)
                {
                    return ReadFeedResponse<TInput>.CreateResponse<TInput>(
                        cosmosFeedResponse: readFeedResponse,
                        serializerCore: serializerCore);
                }

                CosmosArray cosmosArray = null;
                if (responseMessage.Content != null)
                {
                    cosmosArray = CosmosElementSerializer.ToCosmosElements(
                        responseMessage.Content,
                        resourceType,
                        null);
                }

                return new ReadFeedResponse<TInput>(
                    httpStatusCode: responseMessage.StatusCode,
                    cosmosArray: cosmosArray,
                    serializerCore: serializerCore,
                    responseMessageHeaders: responseMessage.Headers,
                    diagnostics: responseMessage.Diagnostics);
            }
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            ReadFeedResponse cosmosFeedResponse,
            CosmosSerializerCore serializerCore)
        {
            ReadFeedResponse<TInput> readFeedResponse;
            using (cosmosFeedResponse)
            {
                readFeedResponse = new ReadFeedResponse<TInput>(
                    httpStatusCode: cosmosFeedResponse.StatusCode,
                    cosmosArray: cosmosFeedResponse.CosmosElements,
                    responseMessageHeaders: cosmosFeedResponse.Headers,
                    diagnostics: cosmosFeedResponse.Diagnostics,
                    serializerCore: serializerCore,
                    decryptionInfo: cosmosFeedResponse.DecryptionInfo);
            }

            return readFeedResponse;
        }        
    }

    internal class ReadFeedResponse : ResponseMessage
    {
        private readonly Lazy<MemoryStream> memoryStream;

        internal virtual IReadOnlyList<CosmosElement> CosmosElements { get; }

        internal virtual IReadOnlyList<DecryptionInfo> DecryptionInfo { get; }

        public override Stream Content
        {
            get
            {
                return this.memoryStream?.Value;
            }
        }

        private ReadFeedResponse(
            IReadOnlyList<CosmosElement> result,
            Headers responseHeaders,
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            CosmosDiagnosticsContext diagnostics,
            CosmosException cosmosException,
            Lazy<MemoryStream> memoryStream,
            IReadOnlyList<DecryptionInfo> decryptionInfo)
            : base(
                statusCode: statusCode,
                requestMessage: requestMessage,
                cosmosException: cosmosException,
                headers: responseHeaders,
                diagnostics: diagnostics)
        {
            this.CosmosElements = result;
            this.memoryStream = memoryStream;
            this.DecryptionInfo = decryptionInfo;
        }

        internal static ReadFeedResponse CreateSuccess(
            string containerRid,
            IReadOnlyList<CosmosElement> result,
            Headers responseHeaders,
            CosmosDiagnosticsContext diagnostics,
            IReadOnlyList<DecryptionInfo> decryptionInfo)
        {
            Lazy<MemoryStream> memoryStream = new Lazy<MemoryStream>(() => CosmosElementSerializer.ToStream(
                containerRid,
                result,
                Documents.ResourceType.Document));

            ReadFeedResponse readFeedResponse = new ReadFeedResponse(
               result: result,
               responseHeaders: responseHeaders,
               diagnostics: diagnostics,
               statusCode: HttpStatusCode.OK,
               cosmosException: null,
               requestMessage: null,
               memoryStream: memoryStream,
               decryptionInfo: decryptionInfo);

            return readFeedResponse;
        }
    }
}