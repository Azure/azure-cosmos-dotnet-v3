//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Handler to ensure that CollectionCache and PartitionRoutingMap for a given collection exists
    /// </summary>
    internal class PartitionKeyRangeGoneRetryHandler : AbstractRetryHandler
    {
        private readonly CosmosClient client;

        public PartitionKeyRangeGoneRetryHandler(CosmosClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            this.client = client;
        }

        internal override async Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            return new PartitionKeyRangeGoneRetryPolicy(
                await this.client.DocumentClient.GetCollectionCacheAsync(),
                await this.client.DocumentClient.GetPartitionKeyRangeCacheAsync(),
                PathsHelper.GetCollectionPath(request.RequestUri.ToString()),
                null);
        }
    }
}
