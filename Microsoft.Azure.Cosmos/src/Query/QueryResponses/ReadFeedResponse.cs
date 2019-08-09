//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
        protected ReadFeedResponse(
            ICollection<T> resource,
            Headers responseMessageHeaders)
            : base(
                httpStatusCode: HttpStatusCode.Accepted,
                headers: responseMessageHeaders,
                resource: resource)
        {
            this.Count = resource.Count;
        }

        public override int Count { get; }

        public override string ContinuationToken => this.Headers?.ContinuationToken;

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            ResponseMessage responseMessage,
            CosmosSerializer jsonSerializer)
        {
            using (responseMessage)
            {
                ICollection<TInput> resources = default(ICollection<TInput>);
                if (responseMessage.Content != null)
                {
                    CosmosFeedResponseUtil<TInput> response = jsonSerializer.FromStream<CosmosFeedResponseUtil<TInput>>(responseMessage.Content);
                    resources = response.Data;
                }

                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    resource: resources,
                    responseMessageHeaders: responseMessage.Headers);

                return readFeedResponse;
            }
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            Headers responseMessageHeaders,
            ICollection<TInput> resources,
            bool hasMoreResults)
        {
            ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                resource: resources,
                responseMessageHeaders: responseMessageHeaders);

            return readFeedResponse;
        }
    }
}