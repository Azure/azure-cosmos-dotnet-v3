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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;

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

        public override bool BypassQueryParsing()
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
            Guid correlatedActivityId,
            ITrace trace,
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
                correlatedActivityId,
                trace,
                cancellationToken);
        }

        public override Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            FeedRange feedRange,
            QueryRequestOptions requestOptions,
            AdditionalRequestHeaders additionalRequestHeaders,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return base.ExecuteItemQueryAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                feedRange: feedRange,
                requestOptions: requestOptions,
                additionalRequestHeaders: additionalRequestHeaders,
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: continuationToken,
                pageSize: pageSize,
                trace: trace,
                cancellationToken: cancellationToken);
        }
    }
}
