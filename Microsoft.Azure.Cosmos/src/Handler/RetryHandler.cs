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
        private readonly IRetryPolicyFactory clientRetryPolicyFactory;

        public RetryHandler(IRetryPolicyFactory clientRetryPolicyFactory)
        {
            if (clientRetryPolicyFactory == null)
            {
                throw new ArgumentNullException(nameof(clientRetryPolicyFactory));
            }

            this.clientRetryPolicyFactory = clientRetryPolicyFactory;
        }

        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.clientRetryPolicyFactory.GetRequestPolicy();
            Debug.Assert(request.OnBeforeSendRequestActions == null, "Cosmos Request message only supports a single retry policy");
            return Task.FromResult(retryPolicyInstance);
        }
    }
}
