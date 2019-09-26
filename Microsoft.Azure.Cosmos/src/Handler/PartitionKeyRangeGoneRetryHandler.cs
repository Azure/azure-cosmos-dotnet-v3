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
        private readonly ClientPipelineBuilderContext clientPipelineBuilderContext;

        public PartitionKeyRangeGoneRetryHandler(ClientPipelineBuilderContext clientPipelineBuilderContext)
        {
            if (clientPipelineBuilderContext == null)
            {
                throw new ArgumentNullException(nameof(clientPipelineBuilderContext));
            }

            this.clientPipelineBuilderContext = clientPipelineBuilderContext;
        }

        internal override async Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(RequestMessage request)
        {
            return new PartitionKeyRangeGoneRetryPolicy(
                await this.clientPipelineBuilderContext.GetCollectionCacheAsync(),
                await this.clientPipelineBuilderContext.GetPartitionKeyRangeCacheAsync(),
                PathsHelper.GetCollectionPath(request.RequestUri.ToString()),
                null);
        }
    }
}
