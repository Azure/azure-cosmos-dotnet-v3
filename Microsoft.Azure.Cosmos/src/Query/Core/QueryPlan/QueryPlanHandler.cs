//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;

    internal sealed class QueryPlanHandler
    {
        private readonly CosmosQueryClient queryClient;

        public QueryPlanHandler(CosmosQueryClient queryClient)
        {
            this.queryClient = queryClient ?? throw new ArgumentNullException($"{nameof(queryClient)}");
        }

        public async Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetQueryPlanAsync(
            SqlQuerySpec sqlQuerySpec,
            Documents.ResourceType resourceType,
            PartitionKeyDefinition partitionKeyDefinition,
            VectorEmbeddingPolicy vectorEmbeddingPolicy,
            bool hasLogicalPartitionKey,
            bool useSystemPrefix,
            GeospatialType geospatialType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException($"{nameof(sqlQuerySpec)}");
            }

            if (partitionKeyDefinition == null)
            {
                throw new ArgumentNullException($"{nameof(partitionKeyDefinition)}");
            }

            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryInfo = await this.TryGetQueryInfoAsync(
                sqlQuerySpec,
                resourceType,
                partitionKeyDefinition,
                vectorEmbeddingPolicy,
                hasLogicalPartitionKey,
                useSystemPrefix,
                geospatialType,
                cancellationToken);
            if (!tryGetQueryInfo.Succeeded)
            {
                return tryGetQueryInfo;
            }

            return tryGetQueryInfo;
        }

        private Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetQueryInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            Documents.ResourceType resourceType,
            PartitionKeyDefinition partitionKeyDefinition,
            VectorEmbeddingPolicy vectorEmbeddingPolicy,
            bool hasLogicalPartitionKey,
            bool useSystemPrefix,
            Cosmos.GeospatialType geospatialType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.queryClient.TryGetPartitionedQueryExecutionInfoAsync(
                sqlQuerySpec: sqlQuerySpec,
                resourceType: resourceType,
                partitionKeyDefinition: partitionKeyDefinition,
                vectorEmbeddingPolicy: vectorEmbeddingPolicy,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: hasLogicalPartitionKey,
                allowDCount: true,
                useSystemPrefix: useSystemPrefix,
                geospatialType: geospatialType,
                cancellationToken: cancellationToken);
        }
    }
}