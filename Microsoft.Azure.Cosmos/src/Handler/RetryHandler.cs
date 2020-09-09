//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Handler to wrap the pipeline into a retry mechanism defined by a <see cref="IDocumentClientRetryPolicy"/>
    /// </summary>
    internal class RetryHandler : AbstractRetryHandler
    {
        private readonly CosmosClient client;

        public RetryHandler(CosmosClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
        }

        internal override IDocumentClientRetryPolicy GetRetryPolicy(RequestMessage request)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.client.DocumentClient.ResetSessionTokenRetryPolicy.GetRequestPolicy();
#if !INTERNAL
            Debug.Assert(request.OnBeforeSendRequestActions == null, "Cosmos Request message only supports a single retry policy");
#endif
            return retryPolicyInstance;
        }
    }
}
