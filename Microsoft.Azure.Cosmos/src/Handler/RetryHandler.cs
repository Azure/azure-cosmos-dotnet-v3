//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler to wrap the pipeline into a retry mechanism defined by a <see cref="IDocumentClientRetryPolicy"/>
    /// </summary>
    internal class RetryHandler : AbstractRetryHandler
    {
        private readonly IRetryPolicyFactory retryPolicyFactory;

        public RetryHandler(IRetryPolicyFactory retryPolicyFactory)
        {
            if (retryPolicyFactory == null)
            {
                throw new ArgumentNullException(nameof(retryPolicyFactory));
            }

            this.retryPolicyFactory = retryPolicyFactory;
        }

        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicy(CosmosRequestMessage request)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = retryPolicyFactory.GetRequestPolicy();
            request.DocumentClientRetryPolicy = retryPolicyInstance;
            return Task.FromResult(retryPolicyInstance);
        }
    }
}
