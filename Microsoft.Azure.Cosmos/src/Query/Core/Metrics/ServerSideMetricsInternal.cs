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
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class ServerSideMetricsInternal : ServerSideMetrics
    {
        /// <summary>
        /// QueryMetrics with all members having default (but not null) members.
        /// </summary>
        public static readonly ServerSideMetricsInternal Empty = new ServerSideMetricsInternal(
            retrievedDocumentCount: 0,
            retrievedDocumentSize: 0,
            outputDocumentCount: 0,
            outputDocumentSize: 0,
            indexHitRatio: 0,
            totalQueryExecutionTime: TimeSpan.Zero,
            queryPreparationTimes: QueryPreparationTimesInternal.Zero,
            indexLookupTime: TimeSpan.Zero,
            documentLoadTime: TimeSpan.Zero,
            vmExecutionTime: TimeSpan.Zero,
            runtimeExecutionTimes: RuntimeExecutionTimesInternal.Empty,
            documentWriteTime: TimeSpan.Zero,
            requestCharge: 0);

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
        /// <param name="feedRange"></param>
        /// <param name="partitionKeyRangeId"></param>
        /// <param name="requestCharge"></param>
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
           TimeSpan documentWriteTime,
           string feedRange = null,
           int? partitionKeyRangeId = null,
           double requestCharge = 0)
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
            this.FeedRange = feedRange;
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.RequestCharge = requestCharge;
        }

        public override TimeSpan TotalTime { get; }

        public override long RetrievedDocumentCount { get; }

        public override long RetrievedDocumentSize { get; }

        public override long OutputDocumentCount { get; }

        public override long OutputDocumentSize { get; }

        public QueryPreparationTimesInternal QueryPreparationTimes { get; }

        public override TimeSpan QueryPreparationTime => 
            this.QueryPreparationTimes.LogicalPlanBuildTime + 
            this.QueryPreparationTimes.PhysicalPlanBuildTime + 
            this.QueryPreparationTimes.QueryCompilationTime + 
            this.QueryPreparationTimes.QueryOptimizationTime;

        public override TimeSpan IndexLookupTime { get; }

        public override TimeSpan DocumentLoadTime { get; }

        public RuntimeExecutionTimesInternal RuntimeExecutionTimes { get; }

        public override TimeSpan RuntimeExecutionTime => 
            this.RuntimeExecutionTimes.QueryEngineExecutionTime + 
            this.RuntimeExecutionTimes.SystemFunctionExecutionTime + 
            this.RuntimeExecutionTimes.UserDefinedFunctionExecutionTime;

        public override TimeSpan DocumentWriteTime { get; }

        public override double IndexHitRatio { get; }

        public override TimeSpan VMExecutionTime { get; }

        public override double RequestCharge { get; internal set; }

        public string FeedRange { get; set; }

        public int? PartitionKeyRangeId { get; set; }

        public static ServerSideMetricsInternal Create(IEnumerable<ServerSideMetricsInternal> serverSideMetricsEnumerable)
        {
            ServerSideMetricsInternalAccumulator accumulator = new ServerSideMetricsInternalAccumulator();
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
