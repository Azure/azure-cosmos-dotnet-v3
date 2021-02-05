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
    using Microsoft.Azure.Cosmos.Diagnostics;
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
            Documents.PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken);

        public abstract Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(
            string resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            Guid clientQueryCorrelationId,
            FeedRange feedRange,
            QueryRequestOptions requestOptions,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            bool isContinuationExpected,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            string resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            PartitionKey? partitionKey,
            string supportedQueryFeatures,
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract void ClearSessionTokenCache(string collectionFullName);

        public abstract Task<List<Documents.PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString,
            bool forceRefresh,
            ITrace trace);

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
            List<Documents.Routing.Range<string>> providedRanges,
            bool forceRefresh,
            ITrace trace);

        public abstract bool ByPassQueryParsing();

        public abstract Task ForceRefreshCollectionCacheAsync(
            string collectionLink,
            CancellationToken cancellationToken);
    }
}