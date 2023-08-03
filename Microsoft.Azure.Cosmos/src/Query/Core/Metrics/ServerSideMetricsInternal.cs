//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// internal implementation of metrics received for queries from the backend.
    /// </summary>
    internal sealed class ServerSideMetricsInternal
    {
        /// <summary>
        /// QueryMetrics that with all members having default (but not null) members.
        /// </summary>
        public static readonly ServerSideMetricsInternal Empty = new ServerSideMetricsInternal(
            retrievedDocumentCount: default,
            retrievedDocumentSize: default,
            outputDocumentCount: default,
            outputDocumentSize: default,
            indexHitRatio: default,
            totalQueryExecutionTime: default,
            queryPreparationTimes: QueryPreparationTimesInternal.Zero,
            indexLookupTime: default,
            documentLoadTime: default,
            vmExecutionTime: default,
            runtimeExecutionTimes: RuntimeExecutionTimesInternal.Empty,
            documentWriteTime: default);

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideMetricsInternal"/> class.
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
        public ServerSideMetricsInternal(
           long retrievedDocumentCount,
           long retrievedDocumentSize,
           long outputDocumentCount,
           long outputDocumentSize,
           double indexHitRatio,
           TimeSpan totalQueryExecutionTime,
           QueryPreparationTimesInternal queryPreparationTimes,
           TimeSpan indexLookupTime,
           TimeSpan documentLoadTime,
           TimeSpan vmExecutionTime,
           RuntimeExecutionTimesInternal runtimeExecutionTimes,
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
        public QueryPreparationTimesInternal QueryPreparationTimes { get; }

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
        public RuntimeExecutionTimesInternal RuntimeExecutionTimes { get; }

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

        public static ServerSideMetricsInternal Create(IEnumerable<ServerSideMetricsInternal> serverSideMetricsEnumerable)
        {
            ServerSideMetricsAccumulator accumulator = default;
            foreach (ServerSideMetricsInternal serverSideMetrics in serverSideMetricsEnumerable)
            {
                accumulator.Accumulate(serverSideMetrics);
            }

            return accumulator.GetServerSideMetrics();
        }

        public static bool TryParseFromDelimitedString(string delimitedString, out ServerSideMetricsInternal serverSideMetrics)
        {
            return ServerSideMetricsParser.TryParse(delimitedString, out serverSideMetrics);
        }

        public static ServerSideMetricsInternal ParseFromDelimitedString(string delimitedString)
        {
            if (!ServerSideMetricsParser.TryParse(delimitedString, out ServerSideMetricsInternal serverSideMetrics))
            {
                throw new FormatException();
            }

            return serverSideMetrics;
        }    
    }
}
