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
         CosmosDiagnostics diagnostics,
         RequestMessage requestMessage)
        {
            this.Count = resourceCount;
            this.Headers = responseMessageHeaders;
            this.StatusCode = httpStatusCode;
            this.Diagnostics = diagnostics;
            this.Resource = resources;
            this.RequestMessage = requestMessage;   
        }

        public override int Count { get; }

        public override string ContinuationToken => this.Headers?.ContinuationToken;

        public override Headers Headers { get; }

        public override IEnumerable<T> Resource { get; }

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        public override string IndexMetrics { get; }

        internal override string QueryAdvice { get; }

        internal override RequestMessage RequestMessage { get; }

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
                // ReadFeed can return 304 on Change Feed responses
                if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                {
                    responseMessage.EnsureSuccessStatusCode();
                }

                IReadOnlyCollection<TInput> resources = CosmosFeedResponseSerializer.FromFeedResponseStream<TInput>(
                        serializerCore,
                        responseMessage.Content);

                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    httpStatusCode: responseMessage.StatusCode,
                    resources: resources,
                    resourceCount: resources.Count,
                    responseMessageHeaders: responseMessage.Headers,
                    diagnostics: responseMessage.Diagnostics,
                    requestMessage: responseMessage.RequestMessage);

                return readFeedResponse;
            }
        }
    }
}