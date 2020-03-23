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
    internal sealed class NamedCacheRetryHandler : AbstractRetryHandler
    {
        public NamedCacheRetryHandler()
        {
        }

        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            return Task.FromResult<IDocumentClientRetryPolicy>(new InvalidPartitionExceptionRetryPolicy(null));
        }
    }
}