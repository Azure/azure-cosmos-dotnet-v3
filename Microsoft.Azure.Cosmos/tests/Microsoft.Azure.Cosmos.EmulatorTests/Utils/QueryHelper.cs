//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal static class QueryHelper
    {
        internal static ContainerCore GetContainerWithForcedGatewayQueryPlan(
            ContainerCore containerCore)
        {
            MockCosmosQueryClient cosmosQueryClientCore = new MockCosmosQueryClient(
                   containerCore.ClientContext,
                   containerCore,
                   true);

            return new ContainerCore(
                containerCore.ClientContext,
                (DatabaseCore)containerCore.Database,
                containerCore.Id,
                cosmosQueryClientCore);
        }

        /// <summary>
        /// A helper that forces the SDK to use the gateway or the service interop for the query plan
        /// </summary>
        private class MockCosmosQueryClient : CosmosQueryClientCore
        {
            /// <summary>
            /// True it will use the gateway query plan.
            /// False it will use the service interop
            /// </summary>
            private readonly bool forceQueryPlanGatewayElseServiceInterop;

            public MockCosmosQueryClient(
                CosmosClientContext clientContext,
                ContainerCore cosmosContainerCore,
                bool forceQueryPlanGatewayElseServiceInterop) : base(
                    clientContext,
                    cosmosContainerCore)
            {
                this.forceQueryPlanGatewayElseServiceInterop = forceQueryPlanGatewayElseServiceInterop;
            }

            public int QueryPlanCalls { get; private set; }

            internal override bool ByPassQueryParsing()
            {
                return this.forceQueryPlanGatewayElseServiceInterop;
            }

            internal override Task<(PartitionedQueryExecutionInfo, CosmosDiagnosticsContext)> ExecuteQueryPlanRequestAsync(
                Uri resourceUri,
                ResourceType resourceType,
                OperationType operationType,
                SqlQuerySpec sqlQuerySpec,
                Cosmos.PartitionKey? partitionKey,
                string supportedQueryFeatures,
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
                    cancellationToken);
            }

            internal override Task<QueryResponseCore> ExecuteItemQueryAsync<RequestOptionType>(
                Uri resourceUri,
                ResourceType resourceType,
                OperationType operationType,
                RequestOptionType requestOptions,
                SqlQuerySpec sqlQuerySpec,
                string continuationToken,
                PartitionKeyRangeIdentity partitionKeyRange,
                bool isContinuationExpected,
                int pageSize,
                SchedulingStopwatch schedulingStopwatch,
                CancellationToken cancellationToken)
            {
                Assert.IsFalse(this.forceQueryPlanGatewayElseServiceInterop && this.QueryPlanCalls == 0, "Query Plan is force gateway mode, but no ExecuteQueryPlanRequestAsync have been called");
                return base.ExecuteItemQueryAsync(
                    resourceUri: resourceUri,
                    resourceType: resourceType,
                    operationType: operationType,
                    requestOptions: requestOptions,
                    sqlQuerySpec: sqlQuerySpec,
                    continuationToken: continuationToken,
                    partitionKeyRange: partitionKeyRange,
                    isContinuationExpected: isContinuationExpected,
                    pageSize: pageSize,
                    schedulingStopwatch: schedulingStopwatch,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
