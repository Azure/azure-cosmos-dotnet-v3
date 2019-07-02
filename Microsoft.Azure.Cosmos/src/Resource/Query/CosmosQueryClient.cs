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
        internal abstract Task<CollectionCache> GetCollectionCacheAsync();

        internal abstract Task<IRoutingMapProvider> GetRoutingMapProviderAsync();

        internal abstract Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken);

        internal abstract Task<QueryResponse> ExecuteItemQueryAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            string containerResourceId,
            QueryRequestOptions requestOptions,
            SqlQuerySpec sqlQuerySpec,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken);

        internal abstract Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken);

        internal abstract Task<Documents.ConsistencyLevel> GetDefaultConsistencyLevelAsync();

        internal abstract Task<Documents.ConsistencyLevel?> GetDesiredConsistencyLevelAsync();

        internal abstract Task EnsureValidOverwriteAsync(Documents.ConsistencyLevel desiredConsistencyLevel);

        internal abstract Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync();

        internal abstract void ClearSessionTokenCache(string collectionFullName);

        internal abstract Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString);

        internal abstract Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            string resourceLink,
            string collectionResourceId,
            List<Range<string>> providedRanges);

        internal abstract bool ByPassQueryParsing();
    }
}
