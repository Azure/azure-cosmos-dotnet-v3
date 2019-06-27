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
            IEnumerable<T> resource,
            Headers responseMessageHeaders,
            bool hasMoreResults)
            : base(
                httpStatusCode: HttpStatusCode.Accepted,
                headers: responseMessageHeaders,
                resource: resource)
        {
            this.HasMoreResults = hasMoreResults;
        }

        public override int Count { get; }

        public override string Continuation => this.Headers?.Continuation;

        internal override string InternalContinuationToken => this.Continuation;

        internal override bool HasMoreResults { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            Headers responseMessageHeaders,
            Stream stream,
            CosmosSerializer jsonSerializer,
            bool hasMoreResults)
        {
            using (stream)
            {
                CosmosFeedResponseUtil<TInput> response = jsonSerializer.FromStream<CosmosFeedResponseUtil<TInput>>(stream);
                IEnumerable<TInput> resources = response.Data;
                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    resource: resources,
                    responseMessageHeaders: responseMessageHeaders,
                    hasMoreResults: hasMoreResults);

                return readFeedResponse;
            }
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            Headers responseMessageHeaders,
            IEnumerable<TInput> resources,
            bool hasMoreResults)
        {
            ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                resource: resources,
                responseMessageHeaders: responseMessageHeaders,
                hasMoreResults: hasMoreResults);

            return readFeedResponse;
        }
    }
}