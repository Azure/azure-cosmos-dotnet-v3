namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;

    /// <summary>
    /// A helper that forces the SDK to use the gateway or the service interop for the query plan
    /// </summary>
    internal sealed class MockCosmosQueryClient : CosmosQueryClientCore
    {
        /// <summary>
        /// True it will use the gateway query plan.
        /// False it will use the service interop
        /// </summary>
        private readonly bool forceQueryPlanGatewayElseServiceInterop;

        public MockCosmosQueryClient(
            CosmosClientContext clientContext,
            ContainerInternal cosmosContainerCore,
            bool forceQueryPlanGatewayElseServiceInterop) : base(
                clientContext,
                cosmosContainerCore)
        {
            this.forceQueryPlanGatewayElseServiceInterop = forceQueryPlanGatewayElseServiceInterop;
        }

        public int QueryPlanCalls { get; private set; }

        public override bool ByPassQueryParsing()
        {
            return this.forceQueryPlanGatewayElseServiceInterop;
        }

        public override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Cosmos.PartitionKey? partitionKey,
            string supportedQueryFeatures,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            this.QueryPlanCalls++;
            return base.ExecuteQueryPlanRequestAsync(
                resourceUri,
                resourceType,
                operationType,
                sqlQuerySpec,
                partitionKey,
                supportedQueryFeatures,
                diagnosticsContext,
                cancellationToken);
        }

        public override Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            Guid clientQueryCorrelationId,
            QueryRequestOptions requestOptions,
            Action<QueryPageDiagnostics> queryPageDiagnostics,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            CancellationToken cancellationToken)
        {
            return base.ExecuteItemQueryAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                clientQueryCorrelationId: clientQueryCorrelationId,
                requestOptions: requestOptions,
                queryPageDiagnostics: queryPageDiagnostics,
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: continuationToken,
                partitionKeyRange: partitionKeyRange,
                isContinuationExpected: isContinuationExpected,
                pageSize: pageSize,
                cancellationToken: cancellationToken);
        }
    }
}
