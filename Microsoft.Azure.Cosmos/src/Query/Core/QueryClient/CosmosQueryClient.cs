//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryClient
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract class CosmosQueryClient
    {
        public abstract Action<IQueryable> OnExecuteScalarQueryCallback { get; }

        public abstract Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(
            string containerLink,
            PartitionKey? partitionKey,
            ITrace trace,
            CancellationToken cancellationToken);

        // ISSUE-TODO-adityasa-2025/12/29 - Reduce Coupling: we should not use PartitionKeyRange as return type for this internal interface.
        // PartitionKeyRange contains a lot more information (for e.g. RidPrefix, Throughput related information, LSN, parent range id etc),
        //  none of which is required by callers of these methods. The only information required is min & max values.
        // Furthermore, the range is always min-inclusive and max-exclusive (since original PartitionKeyRange is such).
        // Callers ultimately convert the returned PartitionKeyRange into a FeedRangeEpk.
        // Applies to other methods below as well.

        /// <summary>
        /// Returns list of effective partition key ranges for a collection.
        /// </summary>
        /// <param name="collectionResourceId">Collection for which to retrieve routing map.</param>
        /// <param name="range">This method will return all ranges which overlap this range.</param>
        /// <param name="forceRefresh">Whether forcefully refreshing the routing map is necessary</param>
        /// <returns>List of effective partition key ranges for a collection or null if collection doesn't exist.</returns>
        public abstract Task<IReadOnlyList<Documents.PartitionKeyRange>> TryGetOverlappingRangesAsync(
            string collectionResourceId,
            Documents.Routing.Range<string> range,
            bool forceRefresh = false);

        public abstract Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            Documents.ResourceType resourceType,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            Cosmos.VectorEmbeddingPolicy vectorEmbeddingPolicy,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            bool allowDCount,
            bool useSystemPrefix,
            bool isHybridSearchQueryPlanOptimizationDisabled,
            Cosmos.GeospatialType geospatialType,
            CancellationToken cancellationToken);

        public abstract Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(
            string resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            FeedRange feedRange,
            QueryRequestOptions requestOptions,
            AdditionalRequestHeaders additionalRequestHeaders,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract Task<bool> GetClientDisableOptimisticDirectExecutionAsync();

        public abstract Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            string resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            PartitionKey? partitionKey,
            string supportedQueryFeatures,
            Guid clientQueryCorrelationId,
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract void ClearSessionTokenCache(string collectionFullName);

        public abstract Task<List<Documents.PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedRangeAsync(
            string resourceLink,    
            string collectionResourceId,
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            FeedRangeInternal feedRangeInternal,
            bool forceRefresh,
            ITrace trace);

        public abstract Task<List<Documents.PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            string resourceLink,
            string collectionResourceId,
            IReadOnlyList<Documents.Routing.Range<string>> providedRanges,
            bool forceRefresh,
            ITrace trace);

        public abstract bool BypassQueryParsing();

        public abstract Task ForceRefreshCollectionCacheAsync(
            string collectionLink,
            CancellationToken cancellationToken);
    }
}