//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Refreshed named cache in-case of Gone with NameCacheIsStale
    /// </summary>
    internal class NamedCacheRetryHandler : AbstractRetryHandler
    {
        public NamedCacheRetryHandler()
        {
        }

        internal override IDocumentClientRetryPolicy GetRetryPolicy(RequestMessage request)
        {
            return new InvalidPartitionExceptionRetryPolicy(null);
        }
    }
}