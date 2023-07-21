//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Exposed metrics received for queries from the backend.
    /// </summary>
    public sealed class BackendAccumulatedMetrics
    {
        internal BackendAccumulatedMetrics(QueryMetrics queryMetrics)
        {
            this.RetrievedDocumentCount = queryMetrics.BackendMetrics.RetrievedDocumentCount;
            this.RetrievedDocumentSize = queryMetrics.BackendMetrics.RetrievedDocumentSize;
            this.OutputDocumentCount = queryMetrics.BackendMetrics.OutputDocumentCount;
            this.OutputDocumentSize = queryMetrics.BackendMetrics.OutputDocumentSize;
            this.IndexHitRatio = queryMetrics.BackendMetrics.IndexHitRatio;
            this.TotalTime = queryMetrics.BackendMetrics.TotalTime;
            this.QueryCompilationTime = queryMetrics.BackendMetrics.QueryPreparationTimes.QueryCompilationTime;
            this.LogicalPlanBuildTime = queryMetrics.BackendMetrics.QueryPreparationTimes.LogicalPlanBuildTime;
            this.PhysicalPlanBuildTime = queryMetrics.BackendMetrics.QueryPreparationTimes.PhysicalPlanBuildTime;
            this.QueryOptimizationTime = queryMetrics.BackendMetrics.QueryPreparationTimes.QueryOptimizationTime;
            this.IndexLookupTime = queryMetrics.BackendMetrics.IndexLookupTime;
            this.DocumentLoadTime = queryMetrics.BackendMetrics.DocumentLoadTime;
            this.VMExecutionTime = queryMetrics.BackendMetrics.VMExecutionTime;
            this.SystemFunctionExecutionTime = queryMetrics.BackendMetrics.RuntimeExecutionTimes.SystemFunctionExecutionTime;
            this.UserDefinedFunctionExecutionTime = queryMetrics.BackendMetrics.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime;
            this.QueryEngineExecutionTime = queryMetrics.BackendMetrics.RuntimeExecutionTimes.QueryEngineExecutionTime;
            this.DocumentWriteTime = queryMetrics.BackendMetrics.DocumentWriteTime;
        }

        /// <summary>
        /// Gets the total query time in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan TotalTime { get; }

        /// <summary>
        /// Gets the query compile time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan QueryCompilationTime { get; }

        /// <summary>
        /// Gets the query logical plan build time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan LogicalPlanBuildTime { get; }

        /// <summary>
        /// Gets the query physical plan build time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan PhysicalPlanBuildTime { get; }

        /// <summary>
        /// Gets the query optimization time in the Azure DocumentDB database service. 
        /// </summary>
        public TimeSpan QueryOptimizationTime { get; }

        /// <summary>
        /// Gets the number of documents retrieved during query in the Azure Cosmos database service.
        /// </summary>
        public long RetrievedDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents retrieved in bytes during query in the Azure Cosmos DB service.
        /// </summary>
        public long RetrievedDocumentSize { get; }

        /// <summary>
        /// Gets the number of documents returned by query in the Azure Cosmos DB service.
        /// </summary>
        public long OutputDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents outputted in bytes during query in the Azure Cosmos database service.
        /// </summary>
        public long OutputDocumentSize { get; }

        /// <summary>
        /// Gets the query QueryPreparationTimes in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan QueryPreparationTimes { get; }

        /// <summary>
        /// Gets the query index lookup time in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan IndexLookupTime { get; }

        /// <summary>
        /// Gets the document loading time during query in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan DocumentLoadTime { get; }

        /// <summary>
        /// Gets the query runtime execution times during query in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan RuntimeExecutionTimes { get; }

        /// <summary>
        /// Gets the output writing/serializing time during query in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan DocumentWriteTime { get; }

        /// <summary>
        /// Gets the index hit ratio by query in the Azure Cosmos database service.
        /// </summary>
        public double IndexHitRatio { get; }

        /// <summary>
        /// Gets the VMExecution Time.
        /// </summary>
        public TimeSpan VMExecutionTime { get; }

        /// <summary>
        /// Gets the query system function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan SystemFunctionExecutionTime { get; }

        /// <summary>
        /// Gets the query user defined function execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan UserDefinedFunctionExecutionTime { get; }

        /// <summary>
        /// Gets the total query runtime execution time in the Azure Cosmos DB service.
        /// </summary>
        public TimeSpan QueryEngineExecutionTime { get; }

        /// <summary>
        /// String representation of the QueryBackendMetrics.
        /// </summary>
        /// <returns>Metric text</returns>
        public override string ToString()
        {
            return $"totalExecutionTimeInMs={this.TotalTime.TotalMilliseconds};queryCompileTimeInMs={this.QueryCompilationTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={this.LogicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={this.PhysicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={this.QueryOptimizationTime.TotalMilliseconds};indexLookupTimeInMs={this.IndexLookupTime.TotalMilliseconds};documentLoadTimeInMs={this.DocumentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={this.SystemFunctionExecutionTime.TotalMilliseconds};userFunctionExecuteTimeInMs={this.UserDefinedFunctionExecutionTime.TotalMilliseconds};retrievedDocumentCount={this.RetrievedDocumentCount};retrievedDocumentSize={this.RetrievedDocumentSize};outputDocumentCount={this.OutputDocumentCount};outputDocumentSize={this.OutputDocumentSize};writeOutputTimeInMs={this.DocumentWriteTime.TotalMilliseconds};indexUtilizationRatio={this.IndexHitRatio}";
        }

    }
}