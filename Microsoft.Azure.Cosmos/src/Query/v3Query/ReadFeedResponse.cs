//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Net;

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
        internal ReadFeedResponse(
            HttpStatusCode httpStatusCode,
            IEnumerable<T> resources,
            int resourceCount,
            Headers responseMessageHeaders,
            CosmosDiagnostics diagnostics)
        {
            this.Count = resourceCount;
            this.Headers = responseMessageHeaders;
            this.StatusCode = httpStatusCode;
            this.Diagnostics = diagnostics;
            this.Resource = resources;
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
            CosmosSerializerCore serializerCore)
        {
            using (responseMessage)
            {
                responseMessage.EnsureSuccessStatusCode();

                IReadOnlyCollection<TInput> resources = CosmosFeedResponseSerializer.FromFeedResponseStream<TInput>(
                        serializerCore,
                        responseMessage.Content);

                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    httpStatusCode: responseMessage.StatusCode,
                    resources: resources,
                    resourceCount: resources.Count,
                    responseMessageHeaders: responseMessage.Headers,
                    diagnostics: responseMessage.Diagnostics);

                return readFeedResponse;
            }
        }
    }
}