//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class CosmosQueryClient
    {
        internal abstract Action<IQueryable> OnExecuteScalarQueryCallback { get; }

        internal abstract Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(
            Uri containerLink,
            PartitionKey? partitionKey,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns list of effective partition key ranges for a collection.
        /// </summary>
        /// <param name="collectionResourceId">Collection for which to retrieve routing map.</param>
        /// <param name="range">This method will return all ranges which overlap this range.</param>
        /// <param name="forceRefresh">Whether forcefully refreshing the routing map is necessary</param>
        /// <returns>List of effective partition key ranges for a collection or null if collection doesn't exist.</returns>
        internal abstract Task<IReadOnlyList<Documents.PartitionKeyRange>> TryGetOverlappingRangesAsync(
            string collectionResourceId,
            Documents.Routing.Range<string> range,
            bool forceRefresh = false);

        internal abstract Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken);

        internal abstract Task<QueryResponseCore> ExecuteItemQueryAsync<QueryRequestOptionType>(
            Uri resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            QueryRequestOptionType requestOptions,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            Documents.PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            CancellationToken cancellationToken);

        internal abstract Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            string supportedQueryFeatures,
            CancellationToken cancellationToken);

        internal abstract void ClearSessionTokenCache(string collectionFullName);

        internal abstract Task<List<Documents.PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString);

        internal abstract Task<List<Documents.PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            string resourceLink,
            string collectionResourceId,
            List<Documents.Routing.Range<string>> providedRanges);

        internal abstract bool ByPassQueryParsing();

        internal abstract Task ForceRefreshCollectionCacheAsync(
            string collectionLink,
            CancellationToken cancellationToken);

        internal abstract Exception CreateBadRequestException(string message);
    }
}