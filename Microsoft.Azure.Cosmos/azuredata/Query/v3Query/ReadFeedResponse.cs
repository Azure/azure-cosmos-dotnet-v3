//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
        private readonly Response response;
        protected ReadFeedResponse(
            Response response,
            ICollection<T> resource)
        {
            this.response = response;
            this.Count = resource.Count;
            this.Value = resource;
            this.ContinuationToken = response.Headers.GetContinuationToken();
        }

        public override int Count { get; }

        public override string ContinuationToken { get; }

        public override IEnumerable<T> Value { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Value.GetEnumerator();
        }

        public override Response GetRawResponse() => this.response;

        internal static async Task<ReadFeedResponse<TInput>> CreateResponseAsync<TInput>(
            Response responseMessage,
            CosmosSerializer jsonSerializer,
            CancellationToken cancellationToken)
        {
            using (responseMessage)
            {
                ICollection<TInput> resources = default(ICollection<TInput>);
                if (responseMessage.ContentStream != null)
                {
                    CosmosFeedResponseUtil<TInput> response = await jsonSerializer.FromStreamAsync<CosmosFeedResponseUtil<TInput>>(responseMessage.ContentStream, cancellationToken);
                    resources = response.Data;
                }

                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    response: responseMessage,
                    resource: resources);

                return readFeedResponse;
            }
        }
    }
}