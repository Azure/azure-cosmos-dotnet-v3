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
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;

    internal class CosmosQueryContextCore : CosmosQueryContext
    {
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly object diagnosticLock = new object();
        private CosmosDiagnosticsContext diagnosticsContext;

        public CosmosQueryContextCore(
            CosmosQueryClient client,
            QueryRequestOptions queryRequestOptions,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            string resourceLink,
            Guid correlatedActivityId,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            CosmosDiagnosticsContext diagnosticsContext,
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
                containerResourceId)
        {
            this.queryRequestOptions = queryRequestOptions;
            this.diagnosticsContext = diagnosticsContext;
        }

        internal override IDisposable CreateDiagnosticScope(string name)
        {
            return this.diagnosticsContext.CreateScope(name);
        }

        internal CosmosDiagnosticsContext GetAndResetDiagnostics()
        {
            // Safely swap the current diagnostics for the new diagnostics.
            lock (this.diagnosticLock)
            {
                CosmosDiagnosticsContext current = this.diagnosticsContext;
                this.diagnosticsContext = CosmosDiagnosticsContext.Create(this.queryRequestOptions);
                return current;
            }
        }

        internal override Task<TryCatch<QueryPage>> ExecuteQueryAsync(
            SqlQuerySpec querySpecForInit,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
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
                clientQueryCorrelationId: this.CorrelatedActivityId,
                requestOptions: requestOptions,
                sqlQuerySpec: querySpecForInit,
                continuationToken: continuationToken,
                partitionKeyRange: partitionKeyRange,
                isContinuationExpected: isContinuationExpected,
                pageSize: pageSize,
                queryPageDiagnostics: this.AddQueryPageDiagnostic,
                cancellationToken: cancellationToken);
        }

        internal override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            string resourceUri,
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

        private void AddQueryPageDiagnostic(QueryPageDiagnostics queryPageDiagnostics)
        {
            // Prevent a swap while adding context
            lock (this.diagnosticLock)
            {
                this.diagnosticsContext.AddDiagnosticsInternal(queryPageDiagnostics);
            }
        }
    }
}
