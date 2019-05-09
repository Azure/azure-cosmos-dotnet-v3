//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal abstract class CosmosQueryClient
    {
        internal abstract IDocumentClientRetryPolicy GetRetryPolicy();

        internal abstract Task<CollectionCache> GetCollectionCacheAsync();

        internal abstract Task<IRoutingMapProvider> GetRoutingMapProviderAsync();

        internal abstract Task<QueryPartitionProvider> GetQueryPartitionProviderAsync(CancellationToken cancellationToken);

        internal abstract Task<CosmosQueryResponse> ExecuteItemQueryAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            QueryRequestOptions requestOptions,
            SqlQuerySpec sqlQuerySpec,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken);

        internal abstract Task<Documents.ConsistencyLevel> GetDefaultConsistencyLevelAsync();

        internal abstract Task<Documents.ConsistencyLevel?> GetDesiredConsistencyLevelAsync();

        internal abstract Task EnsureValidOverwrite(Documents.ConsistencyLevel desiredConsistencyLevel);

        internal abstract Task<PartitionKeyRangeCache> GetPartitionKeyRangeCache();

        internal abstract Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkString(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString);

        internal abstract Task<List<PartitionKeyRange>> GetTargetPartitionKeyRanges(
            string resourceLink,
            string collectionResourceId,
            List<Range<string>> providedRanges);

        internal abstract bool ByPassQueryParsing();
    }
}
