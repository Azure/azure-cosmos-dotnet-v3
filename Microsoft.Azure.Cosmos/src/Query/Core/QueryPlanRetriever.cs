//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using HttpConstants = Documents.HttpConstants;
    using OperationType = Documents.OperationType;
    using PartitionKeyDefinition = Documents.PartitionKeyDefinition;
    using ResourceType = Documents.ResourceType;
    using RuntimeConstants = Documents.RuntimeConstants;

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
            CancellationToken cancellationToken = default(CancellationToken))
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

            return (await queryPlanHandler.TryGetQueryPlanAsync(
                    sqlQuerySpec,
                    partitionKeyDefinition,
                    QueryPlanRetriever.SupportedQueryFeatures,
                    hasLogicalPartitionKey,
                    cancellationToken)).ThrowIfException;
        }

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            Uri resourceLink,
            CancellationToken cancellationToken = default(CancellationToken))
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
                QueryPlanRetriever.SupportedQueryFeaturesString,
                cancellationToken);
        }
    }
}