//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;

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

        public override int Count { get; }

        public override string ContinuationToken => this.Headers?.ContinuationToken;

        public override Headers Headers { get; }

        public override IEnumerable<T> Resource { get; }

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

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

                CosmosArray cosmosArray = null;
                if (responseMessage.Content != null)
                {
                    cosmosArray = CosmosElementSerializer.ToCosmosElements(
                        responseMessage.Content,
                        resourceType,
                        null);
                }

                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    httpStatusCode: responseMessage.StatusCode,
                    cosmosArray: cosmosArray,
                    serializerCore: serializerCore,
                    responseMessageHeaders: responseMessage.Headers,
                    diagnostics: responseMessage.Diagnostics);

                return readFeedResponse;
            }
        }
    }
}