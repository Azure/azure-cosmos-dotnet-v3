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
    using Microsoft.Azure.Cosmos.Tracing;
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

        public static async Task<PartitionedQueryExecutionInfo> GetQueryPlanWithServiceInteropAsync(
            CosmosQueryClient queryClient,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool hasLogicalPartitionKey,
            ITrace trace,
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

            using (ITrace serviceInteropTrace = trace.StartChild("Service Interop Query Plan", TraceComponent.Query, TraceLevel.Info))
            {
                QueryPlanHandler queryPlanHandler = new QueryPlanHandler(queryClient);

                TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = await queryPlanHandler.TryGetQueryPlanAsync(
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
                        headers: new Headers(),
                        stackTrace: tryGetQueryPlan.Exception.StackTrace,
                        trace: trace);
                }

                return tryGetQueryPlan.Result;
            }
        }

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            CosmosQueryContext queryContext,
            SqlQuerySpec sqlQuerySpec,
            string resourceLink,
            PartitionKey? partitionKey,
            ITrace trace,
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

            using (ITrace gatewayQueryPlanTrace = trace.StartChild("Gateway QueryPlan", TraceComponent.Query, TraceLevel.Info))
            {
                return queryContext.ExecuteQueryPlanRequestAsync(
                    resourceLink,
                    ResourceType.Document,
                    OperationType.QueryPlan,
                    sqlQuerySpec,
                    partitionKey,
                    QueryPlanRetriever.SupportedQueryFeaturesString,
                    trace,
                    cancellationToken);
            }
        }
    }
}