//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
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

        // TODO: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2750
        public override string IndexMetrics => null;

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static DecryptableFeedResponse<T> CreateResponse(
            ResponseMessage responseMessage,
            IReadOnlyCollection<T> resource)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(responseMessage);
#else
            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }
#endif

            using (responseMessage)
            {
                // ReadFeed can return 304 on Change Feed responses
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