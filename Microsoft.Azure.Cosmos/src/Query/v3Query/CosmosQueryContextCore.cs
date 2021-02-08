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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class CosmosQueryContextCore : CosmosQueryContext
    {
        private readonly object diagnosticLock = new object();
        private CosmosDiagnosticsContext diagnosticsContext;

        public CosmosQueryContextCore(
            CosmosQueryClient client,
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
                this.diagnosticsContext = CosmosDiagnosticsContext.Create(new RequestOptions());
                current.GetOverallScope().Dispose();
                return current;
            }
        }

        internal override Task<TryCatch<QueryPage>> ExecuteQueryAsync(
            SqlQuerySpec querySpecForInit,
            QueryRequestOptions queryRequestOptions,
            string continuationToken,
            FeedRange feedRange,
            bool isContinuationExpected,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.QueryClient.ExecuteItemQueryAsync(
                resourceUri: this.ResourceLink,
                resourceType: this.ResourceTypeEnum,
                operationType: this.OperationTypeEnum,
                clientQueryCorrelationId: this.CorrelatedActivityId,
                requestOptions: queryRequestOptions,
                sqlQuerySpec: querySpecForInit,
                continuationToken: continuationToken,
                feedRange: feedRange,
                isContinuationExpected: isContinuationExpected,
                pageSize: pageSize,
                queryPageDiagnostics: this.AddQueryPageDiagnostic,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        internal override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            string resourceUri,
            Documents.ResourceType resourceType,
            Documents.OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Cosmos.PartitionKey? partitionKey,
            string supportedQueryFeatures,
            ITrace trace,
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
                trace,
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
