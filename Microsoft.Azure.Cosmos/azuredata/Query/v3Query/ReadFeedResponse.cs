//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Data.Cosmos
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
        protected ReadFeedResponse(
            Response response,
            ICollection<T> resource)
        {
            this.Count = resource.Count;
            this.Value = resource;
            this.ContinuationToken = response.Headers.GetContinuationToken();
        }

        public override int Count { get; }

        public override string ContinuationToken { get; }

        public override IEnumerable<T> Value { get; }

        //public override string ContinuationToken => this.Headers?.ContinuationToken;

        //public override Headers Headers { get; }

        //public override IEnumerable<T> Resource { get; }

        //public override HttpStatusCode StatusCode { get; }

        //public override CosmosDiagnostics Diagnostics { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Value.GetEnumerator();
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            Response responseMessage,
            CosmosSerializer jsonSerializer)
        {
            using (responseMessage)
            {
                ICollection<TInput> resources = default(ICollection<TInput>);
                if (responseMessage.ContentStream != null)
                {
                    CosmosFeedResponseUtil<TInput> response = jsonSerializer.FromStream<CosmosFeedResponseUtil<TInput>>(responseMessage.ContentStream);
                    resources = response.Data;
                }

                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    response: responseMessage,
                    resource: resources);

                return readFeedResponse;
            }
        }

        public override Response GetRawResponse()
        {
            throw new System.NotImplementedException();
        }
    }
}