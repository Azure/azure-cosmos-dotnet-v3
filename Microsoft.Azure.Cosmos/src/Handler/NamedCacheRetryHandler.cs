//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Refreshed named cache in-case of Gone with NameCacheIsStale
    /// </summary>
    internal class NamedCacheRetryHandler : AbstractRetryHandler
    {
        public NamedCacheRetryHandler()
        {
        }

        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            return Task.FromResult<IDocumentClientRetryPolicy>(new InvalidPartitionExceptionRetryPolicy(null));
        }

        internal override GlobalPartitionEndpointManager GetGlobalPartitionEndpointManager()
        {
            // The named-cache retry handler is only responsible for refreshing the name cache on
            // InvalidPartitionException; it does not participate in hub region discovery. Returning
            // the NoOp instance keeps the on-success caching path in AbstractRetryHandler null-safe
            // and a guaranteed no-op for this handler.
            return GlobalPartitionEndpointManagerNoOp.Instance;
        }
    }
}