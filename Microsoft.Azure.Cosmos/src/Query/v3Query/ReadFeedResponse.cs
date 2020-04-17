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
        private readonly ResponseMessage responseMessage;
        protected ReadFeedResponse(
            ResponseMessage responseMessage,
            CosmosArray cosmosArray,
            CosmosSerializerCore serializerCore)
        {
            this.responseMessage = responseMessage;
            this.Count = cosmosArray != null ? cosmosArray.Count : 0;
            this.Headers = responseMessage.Headers;
            this.StatusCode = responseMessage.StatusCode;
            this.Diagnostics = responseMessage.Diagnostics;
            this.Resource = CosmosElementSerializer.GetResources<T>(
                cosmosArray: cosmosArray,
                serializerCore: serializerCore);
        }

        public override int Count { get; }

        public override string ContinuationToken => this.responseMessage.ContinuationToken;

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
                    responseMessage: responseMessage,
                    cosmosArray: cosmosArray,
                    serializerCore: serializerCore);

                return readFeedResponse;
            }
        }
    }
}