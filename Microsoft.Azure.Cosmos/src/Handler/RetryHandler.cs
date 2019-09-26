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
        private readonly ClientPipelineBuilderContext clientPipelineBuilderContext;

        public RetryHandler(ClientPipelineBuilderContext clientPipelineBuilderContext)
        {
            if (clientPipelineBuilderContext == null)
            {
                throw new ArgumentNullException(nameof(clientPipelineBuilderContext));
            }

            this.clientPipelineBuilderContext = clientPipelineBuilderContext;
        }

        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.clientPipelineBuilderContext.RetryPolicyFactory.GetRequestPolicy();
            Debug.Assert(request.OnBeforeSendRequestActions == null, "Cosmos Request message only supports a single retry policy");
            return Task.FromResult(retryPolicyInstance);
        }
    }
}
