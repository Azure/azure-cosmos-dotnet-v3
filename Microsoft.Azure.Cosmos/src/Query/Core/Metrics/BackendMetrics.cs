//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Metrics received for queries from the backend.
    /// </summary>
    public sealed class BackendMetrics
    {
        /// <summary>
        /// QueryMetrics that with all members having default (but not null) members.
        /// </summary>
        internal static readonly BackendMetrics Empty = new BackendMetrics(
            retrievedDocumentCount: default,
            retrievedDocumentSize: default,
            outputDocumentCount: default,
            outputDocumentSize: default,
            indexHitRatio: default,
            totalQueryExecutionTime: default,
            queryPreparationTimes: QueryPreparationTimes.Zero,
            indexLookupTime: default,
            documentLoadTime: default,
            vmExecutionTime: default,
            runtimeExecutionTimes: RuntimeExecutionTimes.Empty,
            documentWriteTime: default);

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendMetrics"/> class.
        /// </summary>
        /// <param name="retrievedDocumentCount"></param>
        /// <param name="retrievedDocumentSize"></param>
        /// <param name="outputDocumentCount"></param>
        /// <param name="outputDocumentSize"></param>
        /// <param name="indexHitRatio"></param>
        /// <param name="totalQueryExecutionTime"></param>
        /// <param name="queryPreparationTimes"></param>
        /// <param name="indexLookupTime"></param>
        /// <param name="documentLoadTime"></param>
        /// <param name="vmExecutionTime"></param>
        /// <param name="runtimeExecutionTimes"></param>
        /// <param name="documentWriteTime"></param>
        public BackendMetrics(
           long retrievedDocumentCount,
           long retrievedDocumentSize,
           long outputDocumentCount,
           long outputDocumentSize,
           double indexHitRatio,
           TimeSpan totalQueryExecutionTime,
           QueryPreparationTimes queryPreparationTimes,
           TimeSpan indexLookupTime,
           TimeSpan documentLoadTime,
           TimeSpan vmExecutionTime,
           RuntimeExecutionTimes runtimeExecutionTimes,
           TimeSpan documentWriteTime)
        {
            this.RetrievedDocumentCount = retrievedDocumentCount;
            this.RetrievedDocumentSize = retrievedDocumentSize;
            this.OutputDocumentCount = outputDocumentCount;
            this.OutputDocumentSize = outputDocumentSize;
            this.IndexHitRatio = indexHitRatio;
            this.TotalTime = totalQueryExecutionTime;
            this.QueryPreparationTimes = queryPreparationTimes ?? throw new ArgumentNullException($"{nameof(queryPreparationTimes)} can not be null.");
            this.IndexLookupTime = indexLookupTime;
            this.DocumentLoadTime = documentLoadTime;
            this.VMExecutionTime = vmExecutionTime;
            this.RuntimeExecutionTimes = runtimeExecutionTimes ?? throw new ArgumentNullException($"{nameof(runtimeExecutionTimes)} can not be null.");
            this.DocumentWriteTime = documentWriteTime;
        }

        /// <summary>
        /// Gets the total query time in the Azure Cosmos database service.
        /// </summary>
        public TimeSpan TotalTime { get; }

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
        public QueryPreparationTimes QueryPreparationTimes { get; }

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
        public RuntimeExecutionTimes RuntimeExecutionTimes { get; }

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
        /// String representation of BackendMetric.
        /// </summary>
        /// <returns>BackendMetric text</returns>
        public override string ToString() //todo move this logic elsewhere?
        {
            return $"totalExecutionTimeInMs={this.TotalTime.TotalMilliseconds};queryCompileTimeInMs={this.QueryPreparationTimes.QueryCompilationTime.TotalMilliseconds};queryLogicalPlanBuildTimeInMs={this.QueryPreparationTimes.LogicalPlanBuildTime.TotalMilliseconds};queryPhysicalPlanBuildTimeInMs={this.QueryPreparationTimes.PhysicalPlanBuildTime.TotalMilliseconds};queryOptimizationTimeInMs={this.QueryPreparationTimes.QueryOptimizationTime.TotalMilliseconds};indexLookupTimeInMs={this.IndexLookupTime.TotalMilliseconds};documentLoadTimeInMs={this.DocumentLoadTime.TotalMilliseconds};systemFunctionExecuteTimeInMs={this.RuntimeExecutionTimes.SystemFunctionExecutionTime.TotalMilliseconds};userFunctionExecuteTimeInMs={this.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime.TotalMilliseconds};retrievedDocumentCount={this.RetrievedDocumentCount};retrievedDocumentSize={this.RetrievedDocumentSize};outputDocumentCount={this.OutputDocumentCount};outputDocumentSize={this.OutputDocumentSize};writeOutputTimeInMs={this.DocumentWriteTime.TotalMilliseconds};indexUtilizationRatio={this.IndexHitRatio}";
        }

        internal static BackendMetrics CreateFromIEnumerable(IEnumerable<BackendMetrics> backendMetricsEnumerable)
        {
            BackendMetricsAccumulator accumulator = default;
            foreach (BackendMetrics backendMetrics in backendMetricsEnumerable)
            {
                accumulator.Accumulate(backendMetrics);
            }

            return BackendMetricsAccumulator.ToBackendMetrics(accumulator);
        }

        internal static bool TryParseFromDelimitedString(string delimitedString, out BackendMetrics backendMetrics)
        {
            return BackendMetricsParser.TryParse(delimitedString, out backendMetrics);
        }

        internal static BackendMetrics ParseFromDelimitedString(string delimitedString)
        {
            if (!BackendMetricsParser.TryParse(delimitedString, out BackendMetrics backendMetrics))
            {
                throw new FormatException();
            }

            return backendMetrics;
        }    
    }
}
