//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Metrics received for queries from the backend.
    /// </summary>
    public class ServerSideMetrics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideMetricsInternal"/> class.
        /// </summary>
        /// <param name="serverSideMetricsInternal"></param>
        internal ServerSideMetrics(ServerSideMetricsInternal serverSideMetricsInternal)
        {
            this.RetrievedDocumentCount = serverSideMetricsInternal.RetrievedDocumentCount;
            this.RetrievedDocumentSize = serverSideMetricsInternal.RetrievedDocumentSize;
            this.OutputDocumentCount = serverSideMetricsInternal.OutputDocumentCount;
            this.OutputDocumentSize = serverSideMetricsInternal.OutputDocumentSize;
            this.IndexHitRatio = serverSideMetricsInternal.IndexHitRatio;
            this.TotalTime = serverSideMetricsInternal.TotalTime;
            this.QueryPreparationTime = serverSideMetricsInternal.QueryPreparationTimes.LogicalPlanBuildTime 
                + serverSideMetricsInternal.QueryPreparationTimes.PhysicalPlanBuildTime 
                + serverSideMetricsInternal.QueryPreparationTimes.QueryCompilationTime 
                + serverSideMetricsInternal.QueryPreparationTimes.QueryOptimizationTime;
            this.IndexLookupTime = serverSideMetricsInternal.IndexLookupTime;
            this.DocumentLoadTime = serverSideMetricsInternal.DocumentLoadTime;
            this.VMExecutionTime = serverSideMetricsInternal.VMExecutionTime;
            this.RuntimeExecutionTime = serverSideMetricsInternal.RuntimeExecutionTimes.QueryEngineExecutionTime
                + serverSideMetricsInternal.RuntimeExecutionTimes.SystemFunctionExecutionTime
                + serverSideMetricsInternal.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime;
            this.DocumentWriteTime = serverSideMetricsInternal.DocumentWriteTime;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideMetrics"/> class.
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
        internal ServerSideMetrics(
           long retrievedDocumentCount,
           long retrievedDocumentSize,
           long outputDocumentCount,
           long outputDocumentSize,
           double indexHitRatio,
           TimeSpan totalQueryExecutionTime,
           TimeSpan queryPreparationTimes,
           TimeSpan indexLookupTime,
           TimeSpan documentLoadTime,
           TimeSpan vmExecutionTime,
           TimeSpan runtimeExecutionTimes,
           TimeSpan documentWriteTime)
        {
            this.RetrievedDocumentCount = retrievedDocumentCount;
            this.RetrievedDocumentSize = retrievedDocumentSize;
            this.OutputDocumentCount = outputDocumentCount;
            this.OutputDocumentSize = outputDocumentSize;
            this.IndexHitRatio = indexHitRatio;
            this.TotalTime = totalQueryExecutionTime;
            this.QueryPreparationTime = queryPreparationTimes;
            this.IndexLookupTime = indexLookupTime;
            this.DocumentLoadTime = documentLoadTime;
            this.VMExecutionTime = vmExecutionTime;
            this.RuntimeExecutionTime = runtimeExecutionTimes;
            this.DocumentWriteTime = documentWriteTime;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideMetrics"/> class.
        /// </summary>
        public ServerSideMetrics()
        {
        }

        /// <summary>
        /// Gets the total query time in the Azure Cosmos database service.
        /// </summary>
        public virtual TimeSpan TotalTime { get; }

        /// <summary>
        /// Gets the number of documents retrieved during query in the Azure Cosmos database service.
        /// </summary>
        public virtual long RetrievedDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents retrieved in bytes during query in the Azure Cosmos DB service.
        /// </summary>
        public virtual long RetrievedDocumentSize { get; }

        /// <summary>
        /// Gets the number of documents returned by query in the Azure Cosmos DB service.
        /// </summary>
        public virtual long OutputDocumentCount { get; }

        /// <summary>
        /// Gets the size of documents outputted in bytes during query in the Azure Cosmos database service.
        /// </summary>
        public virtual long OutputDocumentSize { get; }

        /// <summary>
        /// Gets the query preparation time in the Azure Cosmos database service.
        /// </summary>
        public virtual TimeSpan QueryPreparationTime { get; }

        /// <summary>
        /// Gets the query index lookup time in the Azure Cosmos database service.
        /// </summary>
        public virtual TimeSpan IndexLookupTime { get; }

        /// <summary>
        /// Gets the document loading time during query in the Azure Cosmos database service.
        /// </summary>
        public virtual TimeSpan DocumentLoadTime { get; }

        /// <summary>
        /// Gets the query runtime execution time during query in the Azure Cosmos database service.
        /// </summary>
        public virtual TimeSpan RuntimeExecutionTime { get; }

        /// <summary>
        /// Gets the output writing/serializing time during query in the Azure Cosmos database service.
        /// </summary>
        public virtual TimeSpan DocumentWriteTime { get; }

        /// <summary>
        /// Gets the index hit ratio by query in the Azure Cosmos database service.
        /// </summary>
        public virtual double IndexHitRatio { get; }

        /// <summary>
        /// Gets the VMExecution Time.
        /// </summary>
        public virtual TimeSpan VMExecutionTime { get; }

        internal static ServerSideMetrics Create(IEnumerable<ServerSideMetrics> serverSideMetricsEnumerable)
        {
            ServerSideMetricsAccumulator accumulator = new ServerSideMetricsAccumulator();
            foreach (ServerSideMetrics serverSideMetrics in serverSideMetricsEnumerable)
            {
                accumulator.Accumulate(serverSideMetrics);
            }

            return accumulator.GetServerSideMetrics();
        }
    }
}
