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

            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = await queryPlanHandler.TryGetQueryPlanAsync(
                sqlQuerySpec,
                partitionKeyDefinition,
                QueryPlanRetriever.SupportedQueryFeatures,
                hasLogicalPartitionKey,
                cancellationToken);

            if (!tryGetQueryPlan.Succeeded)
            {
                throw new CosmosException(
                    System.Net.HttpStatusCode.BadRequest,
                    tryGetQueryPlan.Exception.ToString());
            }

            return tryGetQueryPlan.Result;
        }

        public static Task<(PartitionedQueryExecutionInfo, CosmosDiagnosticsContext)> GetQueryPlanThroughGatewayAsync(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            Uri resourceLink,
            PartitionKey? partitionKey,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
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

            return client.ExecuteQueryPlanRequestAsync(
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