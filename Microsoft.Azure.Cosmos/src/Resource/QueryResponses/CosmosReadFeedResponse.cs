//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;

    internal class CosmosReadFeedResponse<T> : FeedResponse<T>
    {
        protected CosmosReadFeedResponse(
            IEnumerable<T> resource,
            CosmosResponseMessageHeaders responseMessageHeaders,
            bool hasMoreResults): base(
                httpStatusCode: HttpStatusCode.Accepted,
                headers: responseMessageHeaders,
                resource: resource)
        {
            this.HasMoreResults = hasMoreResults;
        }

        public override int Count { get; }

        public override string Continuation => this.Headers.Continuation;

        internal override string InternalContinuationToken => this.Continuation;

        internal override bool HasMoreResults { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static CosmosReadFeedResponse<TInput> CreateResponse<TInput>(
            CosmosResponseMessageHeaders responseMessageHeaders,
            Stream stream,
            CosmosJsonSerializer jsonSerializer,
            bool hasMoreResults)
        {
            using (stream)
            {
                IEnumerable<TInput> resources = jsonSerializer.FromStream<CosmosFeedResponseUtil<TInput>>(stream).Data;
                CosmosReadFeedResponse<TInput> readFeedResponse = new CosmosReadFeedResponse<TInput>(
                    resource: resources,
                    responseMessageHeaders: responseMessageHeaders,
                    hasMoreResults: hasMoreResults);

                return readFeedResponse;
            }
        }

        internal static CosmosReadFeedResponse<TInput> CreateResponse<TInput>(
            CosmosResponseMessageHeaders responseMessageHeaders,
            IEnumerable<TInput> resources,
            bool hasMoreResults)
        {
            CosmosReadFeedResponse<TInput> readFeedResponse = new CosmosReadFeedResponse<TInput>(
                resource: resources,
                responseMessageHeaders: responseMessageHeaders,
                hasMoreResults: hasMoreResults);

            return readFeedResponse;
        }
    }
}