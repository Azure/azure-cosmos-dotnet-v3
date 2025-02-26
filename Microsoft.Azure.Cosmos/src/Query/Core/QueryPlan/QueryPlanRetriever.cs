//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Runtime.InteropServices;
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
            | QueryFeatures.NonValueAggregate
            | QueryFeatures.DCount
            | QueryFeatures.NonStreamingOrderBy
            | QueryFeatures.CountIf
            | QueryFeatures.HybridSearch;

        private static readonly QueryFeatures SupportedQueryFeaturesWithoutNonStreamingOrderBy =
            SupportedQueryFeatures & (~QueryFeatures.NonStreamingOrderBy);

        private static readonly string SupportedQueryFeaturesString = SupportedQueryFeatures.ToString();

        private static readonly string SupportedQueryFeaturesWithoutNonStreamingOrderByString =
            SupportedQueryFeaturesWithoutNonStreamingOrderBy.ToString();

        private static string GetSupportedQueryFeaturesString(bool isNonStreamingOrderByQueryFeatureDisabled)
        {
            return isNonStreamingOrderByQueryFeatureDisabled ?
                SupportedQueryFeaturesWithoutNonStreamingOrderByString :
                SupportedQueryFeaturesString;
        }

        public static async Task<PartitionedQueryExecutionInfo> GetQueryPlanWithServiceInteropAsync(
            CosmosQueryClient queryClient,
            SqlQuerySpec sqlQuerySpec,
            Documents.ResourceType resourceType,
            PartitionKeyDefinition partitionKeyDefinition,
            VectorEmbeddingPolicy vectorEmbeddingPolicy,
            bool hasLogicalPartitionKey,
            GeospatialType geospatialType,
            bool useSystemPrefix,
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
                    resourceType,
                    partitionKeyDefinition,
                    vectorEmbeddingPolicy,
                    hasLogicalPartitionKey,
                    useSystemPrefix,
                    geospatialType,
                    cancellationToken);

                if (!tryGetQueryPlan.Succeeded)
                {
                    if (ExceptionToCosmosException.TryCreateFromException(tryGetQueryPlan.Exception, serviceInteropTrace, out CosmosException cosmosException))
                    {
                        throw cosmosException;
                    }
                    else
                    {
                        throw ExceptionWithStackTraceException.UnWrapMonadExcepion(tryGetQueryPlan.Exception, serviceInteropTrace);
                    }
                }

                return tryGetQueryPlan.Result;
            }
        }

        public static Task<PartitionedQueryExecutionInfo> GetQueryPlanThroughGatewayAsync(
            CosmosQueryContext queryContext,
            SqlQuerySpec sqlQuerySpec,
            string resourceLink,
            PartitionKey? partitionKey,
            bool isNonStreamingOrderByQueryFeatureDisabled,
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            && Documents.ServiceInteropWrapper.Is64BitProcess)
                {
                    // It's Windows and x64, should have loaded the DLL
                    gatewayQueryPlanTrace.AddDatum("ServiceInterop unavailable", true);
                }
                
                return queryContext.ExecuteQueryPlanRequestAsync(
                    resourceLink,
                    ResourceType.Document,
                    OperationType.QueryPlan,
                    sqlQuerySpec,
                    partitionKey,
                    GetSupportedQueryFeaturesString(isNonStreamingOrderByQueryFeatureDisabled),
                    trace,
                    cancellationToken);
            }
        }
    }
}