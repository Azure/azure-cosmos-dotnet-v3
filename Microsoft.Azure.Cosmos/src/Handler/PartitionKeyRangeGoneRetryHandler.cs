//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Handler to ensure that CollectionCache and PartitionRoutingMap for a given collection exists
    /// </summary>
    internal class PartitionKeyRangeGoneRetryHandler : AbstractRetryHandler
    {
        private readonly Func<Task<PartitionKeyRangeCache>> getPartitionKeyRangeCacheAsync;
        private readonly Func<Task<ClientCollectionCache>> getCollectionCacheAsync;

        public PartitionKeyRangeGoneRetryHandler(
            Func<Task<PartitionKeyRangeCache>> getPartitionKeyRangeCacheAsync,
            Func<Task<ClientCollectionCache>> getCollectionCacheAsync)
        {
            if (getPartitionKeyRangeCacheAsync == null)
            {
                throw new ArgumentNullException(nameof(getPartitionKeyRangeCacheAsync));
            }

            if (getCollectionCacheAsync == null)
            {
                throw new ArgumentNullException(nameof(getCollectionCacheAsync));
            }

            this.getPartitionKeyRangeCacheAsync = getPartitionKeyRangeCacheAsync;
            this.getCollectionCacheAsync = getCollectionCacheAsync;
        }

        internal override async Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            return new PartitionKeyRangeGoneRetryPolicy(
                await this.getCollectionCacheAsync(),
                await this.getPartitionKeyRangeCacheAsync(),
                PathsHelper.GetCollectionPath(request.RequestUri.ToString()),
                null);
        }
    }
}
