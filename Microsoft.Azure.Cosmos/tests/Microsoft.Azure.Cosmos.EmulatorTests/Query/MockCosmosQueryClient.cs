namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// A helper that forces the SDK to use the gateway or the service interop for the query plan
    /// </summary>
    internal sealed class MockCosmosQueryClient : CosmosQueryClient
    {
        /// <summary>
        /// True it will use the gateway query plan.
        /// False it will use the service interop
        /// </summary>
        private readonly bool forceQueryPlanGatewayElseServiceInterop;

        private readonly CosmosQueryClient cosmosQueryClient;

        public MockCosmosQueryClient(
            CosmosClientContext clientContext,
            ContainerCore cosmosContainerCore,
            bool forceQueryPlanGatewayElseServiceInterop)
        {
            this.forceQueryPlanGatewayElseServiceInterop = forceQueryPlanGatewayElseServiceInterop;
            this.cosmosQueryClient = new CosmosQueryClientCore(clientContext, cosmosContainerCore);
        }

        public int QueryPlanCalls { get; private set; }

        internal override Action<IQueryable> OnExecuteScalarQueryCallback => this.cosmosQueryClient.OnExecuteScalarQueryCallback;

        internal override bool ByPassQueryParsing()
        {
            return this.forceQueryPlanGatewayElseServiceInterop;
        }

        internal override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Cosmos.PartitionKey? partitionKey,
            string supportedQueryFeatures,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            this.QueryPlanCalls++;
            return this.cosmosQueryClient.ExecuteQueryPlanRequestAsync(
                resourceUri,
                resourceType,
                operationType,
                sqlQuerySpec,
                partitionKey,
                supportedQueryFeatures,
                diagnosticsContext,
                cancellationToken);
        }

        internal override Task<QueryResponseCore> ExecuteItemQueryAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            QueryRequestOptions requestOptions,
            Action<QueryPageDiagnostics> queryPageDiagnostics,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Assert.IsFalse(
                this.forceQueryPlanGatewayElseServiceInterop && this.QueryPlanCalls == 0,
                "Query Plan is force gateway mode, but no ExecuteQueryPlanRequestAsync have been called");
            return this.cosmosQueryClient.ExecuteItemQueryAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                queryPageDiagnostics: queryPageDiagnostics,
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: continuationToken,
                partitionKeyRange: partitionKeyRange,
                isContinuationExpected: isContinuationExpected,
                pageSize: pageSize,
                cancellationToken: cancellationToken);
        }

        internal override Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(Uri containerLink, Cosmos.PartitionKey? partitionKey, CancellationToken cancellationToken)
        {
            return this.cosmosQueryClient.GetCachedContainerQueryPropertiesAsync(containerLink, partitionKey, cancellationToken);
        }

        internal override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(string collectionResourceId, Range<string> range, bool forceRefresh = false)
        {
            return this.cosmosQueryClient.TryGetOverlappingRangesAsync(collectionResourceId, range, forceRefresh);
        }

        internal override Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(SqlQuerySpec sqlQuerySpec, PartitionKeyDefinition partitionKeyDefinition, bool requireFormattableOrderByQuery, bool isContinuationExpected, bool allowNonValueAggregateQuery, bool hasLogicalPartitionKey, CancellationToken cancellationToken)
        {
            return this.cosmosQueryClient.TryGetPartitionedQueryExecutionInfoAsync(sqlQuerySpec, partitionKeyDefinition, requireFormattableOrderByQuery, isContinuationExpected, allowNonValueAggregateQuery, hasLogicalPartitionKey, cancellationToken);
        }

        internal override void ClearSessionTokenCache(string collectionFullName)
        {
            this.cosmosQueryClient.ClearSessionTokenCache(collectionFullName);
        }

        internal override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(string resourceLink, string collectionResourceId, string effectivePartitionKeyString)
        {
            return this.cosmosQueryClient.GetTargetPartitionKeyRangesByEpkStringAsync(resourceLink, collectionResourceId, effectivePartitionKeyString);
        }

        internal override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(string resourceLink, string collectionResourceId, List<Range<string>> providedRanges)
        {
            return this.cosmosQueryClient.GetTargetPartitionKeyRangesAsync(resourceLink, collectionResourceId, providedRanges);
        }

        internal override Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
        {
            return this.cosmosQueryClient.ForceRefreshCollectionCacheAsync(collectionLink, cancellationToken);
        }
    }
}
