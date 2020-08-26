//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler to wrap the pipeline into a retry mechanism defined by a <see cref="IDocumentClientRetryPolicy"/>
    /// </summary>
    internal class RetryHandler : AbstractRetryHandler
    {
        private readonly CosmosClient client;

        public RetryHandler(CosmosClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.client.DocumentClient.ResetSessionTokenRetryPolicy.GetRequestPolicy();
#if !INTERNAL
            Debug.Assert(request.OnBeforeSendRequestActions == null, "Cosmos Request message only supports a single retry policy");
#endif
            return Task.FromResult(retryPolicyInstance);
        }
    }
}
