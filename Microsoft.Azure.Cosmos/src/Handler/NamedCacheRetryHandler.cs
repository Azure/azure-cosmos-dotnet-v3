//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Refreshed named cache in-case of Gone with NameCacheIsStale
    /// </summary>
    internal class NamedCacheRetryHandler : AbstractRetryHandler
    {
        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicy(CosmosRequestMessage request)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = new InvalidPartitionExceptionRetryPolicy(null);
            request.OnBeforeRequestHandler += retryPolicyInstance.OnBeforeSendRequest;
            return Task.FromResult<IDocumentClientRetryPolicy>(retryPolicyInstance);
        }
    }
}