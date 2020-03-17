//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;

    internal class CosmosQueryContextCore : CosmosQueryContext
    {
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly CosmosDiagnosticsContext diagnosticsContext;

        public CosmosQueryContextCore(
            CosmosQueryClient client,
            QueryRequestOptions queryRequestOptions,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            Uri resourceLink,
            Guid correlatedActivityId,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            CosmosDiagnosticsContext diagnosticsContext,
            QueryPipelineDiagnostics queryPipelineDiagnostics,
            string containerResourceId = null)
            : base(
                client,
                resourceTypeEnum,
                operationType,
                resourceType,
                resourceLink,
                correlatedActivityId,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                queryPipelineDiagnostics,
                containerResourceId)
        {
            this.queryRequestOptions = queryRequestOptions;
            this.diagnosticsContext = diagnosticsContext ?? throw new ArgumentNullException(nameof(queryPipelineDiagnostics));
        }

        internal override Task<QueryResponseCore> ExecuteQueryAsync(
            SqlQuerySpec querySpecForInit,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            SchedulingStopwatch schedulingStopwatch,
            CancellationToken cancellationToken)
        {
            QueryRequestOptions requestOptions = null;
            if (this.queryRequestOptions != null)
            {
                requestOptions = this.queryRequestOptions.Clone();
            }    

            return this.QueryClient.ExecuteItemQueryAsync(
                           resourceUri: this.ResourceLink,
                           resourceType: this.ResourceTypeEnum,
                           operationType: this.OperationTypeEnum,
                           requestOptions: requestOptions,
                           sqlQuerySpec: querySpecForInit,
                           continuationToken: continuationToken,
                           partitionKeyRange: partitionKeyRange,
                           isContinuationExpected: isContinuationExpected,
                           pageSize: pageSize,
                           schedulingStopwatch: schedulingStopwatch,
                           cancellationToken: cancellationToken);
        }

        internal override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Cosmos.PartitionKey? partitionKey,
            string supportedQueryFeatures,
            CancellationToken cancellationToken)
        {
            return this.QueryClient.ExecuteQueryPlanRequestAsync(
                resourceUri,
                resourceType,
                operationType,
                sqlQuerySpec,
                partitionKey,
                supportedQueryFeatures,
                this.diagnosticsContext,
                cancellationToken);
        }
    }
}
