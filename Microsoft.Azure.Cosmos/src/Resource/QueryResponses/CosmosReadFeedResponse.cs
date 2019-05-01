//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Documents;

    internal class CosmosReadFeedResponse<T> : CosmosFeedResponse<T>
    {
        private readonly CosmosResponseMessageHeaders responseHeaders;
        private IEnumerable<T> resources;

        protected CosmosReadFeedResponse(
            CosmosResponseMessageHeaders responseMessageHeaders,
            bool hasMoreResults)
        {
            this.responseHeaders = responseMessageHeaders ?? throw new ArgumentNullException(nameof(responseMessageHeaders));
            this.HasMoreResults = hasMoreResults;
        }

        public override string Continuation => this.responseHeaders.Continuation;

        public override double RequestCharge => this.responseHeaders.RequestCharge;

        public override CosmosResponseMessageHeaders Headers { get; }

        public override int Count { get; }

        public override string SessionToken => this.Headers.GetValueOrDefault(HttpConstants.HttpHeaders.SessionToken);

        internal override string InternalContinuationToken => this.Continuation;

        internal override bool HasMoreResults { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            if (this.resources == null)
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }

            return this.resources.GetEnumerator();
        }

        internal static CosmosReadFeedResponse<TInput> CreateResponse<TInput>(
            CosmosResponseMessageHeaders responseMessageHeaders,
            Stream stream,
            CosmosJsonSerializer jsonSerializer,
            bool hasMoreResults)
        {
            using (stream)
            {
                CosmosReadFeedResponse<TInput> readFeedResponse = new CosmosReadFeedResponse<TInput>(
                    responseMessageHeaders: responseMessageHeaders,
                    hasMoreResults: hasMoreResults);

                readFeedResponse.InitializeResource(stream, jsonSerializer);
                return readFeedResponse;
            }
        }

        internal static CosmosReadFeedResponse<TInput> CreateResponse<TInput>(
            CosmosResponseMessageHeaders responseMessageHeaders,
            IEnumerable<TInput> resources,
            bool hasMoreResults)
        {

            CosmosReadFeedResponse<TInput> readFeedResponse = new CosmosReadFeedResponse<TInput>(
                responseMessageHeaders: responseMessageHeaders,
                hasMoreResults: hasMoreResults);

            readFeedResponse.resources = resources;
            return readFeedResponse;
        }

        private void InitializeResource(
            Stream stream,
            CosmosJsonSerializer jsonSerializer)
        {
            this.resources = jsonSerializer.FromStream<CosmosFeedResponseUtil<T>>(stream).Data;
        }
    }
}