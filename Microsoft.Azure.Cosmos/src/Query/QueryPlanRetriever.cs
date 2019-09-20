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
    using Microsoft.Azure.Documents;

    internal static class QueryPlanRetriever
    {
        private static readonly QueryFeatures SupportedQueryFeatures =
            QueryFeatures.Aggregate
            | QueryFeatures.Distinct
            | QueryFeatures.MultipleOrderBy
            | QueryFeatures.MultipleAggregates
            | QueryFeatures.OffsetAndLimit
            | QueryFeatures.OrderBy
            | QueryFeatures.Top
            | QueryFeatures.NonValueAggregate;

        private static readonly string SupportedQueryFeaturesString = SupportedQueryFeatures.ToString();

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanWithServiceInteropAsync(
            CosmosQueryClient queryClient,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Contract.Requires<ArgumentNullException>(queryClient != null, nameof(queryClient));
            Contract.Requires<ArgumentNullException>(sqlQuerySpec != null, nameof(sqlQuerySpec));
            Contract.Requires<ArgumentNullException>(partitionKeyDefinition != null, nameof(partitionKeyDefinition));

            cancellationToken.ThrowIfCancellationRequested();
            QueryPlanHandler queryPlanHandler = new QueryPlanHandler(queryClient);

            return queryPlanHandler.GetQueryPlanAsync(
                    sqlQuerySpec,
                    partitionKeyDefinition,
                    QueryPlanRetriever.SupportedQueryFeatures,
                    hasLogicalPartitionKey,
                    cancellationToken);
        }

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            Uri resourceLink,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Contract.Requires<ArgumentNullException>(client != null, nameof(client));
            Contract.Requires<ArgumentNullException>(sqlQuerySpec != null, nameof(sqlQuerySpec));
            Contract.Requires<ArgumentNullException>(resourceLink != null, nameof(resourceLink));

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