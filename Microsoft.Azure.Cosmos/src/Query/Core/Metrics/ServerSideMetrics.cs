//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Metrics received for queries from the backend.
    /// </summary>
    public sealed class ServerSideMetrics
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
            this.QueryPreparationTimes = new QueryPreparationTimes(serverSideMetricsInternal.QueryPreparationTimes);
            this.IndexLookupTime = serverSideMetricsInternal.IndexLookupTime;
            this.DocumentLoadTime = serverSideMetricsInternal.DocumentLoadTime;
            this.VMExecutionTime = serverSideMetricsInternal.VMExecutionTime;
            this.RuntimeExecutionTimes = new RuntimeExecutionTimes(serverSideMetricsInternal.RuntimeExecutionTimes);
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
        public ServerSideMetrics(
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
    }
}
