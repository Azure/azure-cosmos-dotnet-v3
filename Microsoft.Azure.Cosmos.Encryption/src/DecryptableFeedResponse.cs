//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Collections.Generic;
    using System.Net;

    internal class DecryptableFeedResponse<T> : FeedResponse<T>
    {
        protected DecryptableFeedResponse(
            ResponseMessage responseMessage,
            IReadOnlyCollection<T> resource)
        {
            this.Count = resource?.Count ?? 0;
            this.Headers = responseMessage.Headers;
            this.StatusCode = responseMessage.StatusCode;
            this.Diagnostics = responseMessage.Diagnostics;
            this.Resource = resource;
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

        internal static DecryptableFeedResponse<T> CreateResponse(
            ResponseMessage responseMessage,
            IReadOnlyCollection<T> resource)
        {
            using (responseMessage)
            {
                // ReadFeed can return 304 in some scenarios (for example Change Feed)
                if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                {
                    responseMessage.EnsureSuccessStatusCode();
                }

                return new DecryptableFeedResponse<T>(
                    responseMessage,
                    resource);
            }
        }
    }
}