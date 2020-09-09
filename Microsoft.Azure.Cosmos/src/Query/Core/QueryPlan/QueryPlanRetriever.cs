//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using OperationType = Documents.OperationType;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;
    using ResourceType = Documents.ResourceType;

    internal static class QueryPlanRetriever
    {
        private static readonly QueryFeatures SupportedQueryFeatures =
            QueryFeatures.Aggregate
            | QueryFeatures.Distinct
            | QueryFeatures.GroupBy
            | QueryFeatures.MultipleOrderBy
            | QueryFeatures.MultipleAggregates
            | QueryFeatures.OffsetAndLimit
            | QueryFeatures.OrderBy
            | QueryFeatures.Top
            | QueryFeatures.NonValueAggregate;

        private static readonly string SupportedQueryFeaturesString = SupportedQueryFeatures.ToString();

        public static PartitionedQueryExecutionInfo GetQueryPlanWithServiceInterop(
            CosmosQueryClient queryClient,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken = default)
        {
            if (queryClient == null)
            {
                throw new ArgumentNullException(nameof(queryClient));
            }

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (partitionKeyDefinition == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyDefinition));
            }

            cancellationToken.ThrowIfCancellationRequested();
            QueryPlanHandler queryPlanHandler = new QueryPlanHandler(queryClient);

            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = queryPlanHandler.TryGetQueryPlan(
                sqlQuerySpec,
                partitionKeyDefinition,
                QueryPlanRetriever.SupportedQueryFeatures,
                hasLogicalPartitionKey,
                cancellationToken);

            if (!tryGetQueryPlan.Succeeded)
            {
                if (tryGetQueryPlan.Exception is CosmosException)
                {
                    throw tryGetQueryPlan.Exception;
                }

                throw CosmosExceptionFactory.CreateBadRequestException(
                    message: tryGetQueryPlan.Exception.ToString(),
                    stackTrace: tryGetQueryPlan.Exception.StackTrace);
            }

            return tryGetQueryPlan.Result;
        }

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            CosmosQueryContext queryContext,
            SqlQuerySpec sqlQuerySpec,
            string resourceLink,
            PartitionKey? partitionKey,
            CancellationToken cancellationToken = default)
        {
            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(queryContext));
            }

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (resourceLink == null)
            {
                throw new ArgumentNullException(nameof(resourceLink));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return queryContext.ExecuteQueryPlanRequestAsync(
                resourceLink,
                ResourceType.Document,
                OperationType.QueryPlan,
                sqlQuerySpec,
                partitionKey,
                QueryPlanRetriever.SupportedQueryFeaturesString,
                cancellationToken);
        }
    }
}