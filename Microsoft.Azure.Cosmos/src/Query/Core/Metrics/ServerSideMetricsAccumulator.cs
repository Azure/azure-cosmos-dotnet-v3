//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal class ServerSideMetricsAccumulator
    {
        private readonly List<ServerSideMetricsInternal> serverSideMetricsList;

        public ServerSideMetricsAccumulator()
        {
            this.serverSideMetricsList = new List<ServerSideMetricsInternal>();
        }

        public void Accumulate(ServerSideMetricsInternal serverSideMetrics)
        {
            if (serverSideMetrics == null)
            {
                throw new ArgumentNullException(nameof(serverSideMetrics));
            }

            this.serverSideMetricsList.Add(serverSideMetrics);
        }

        public ServerSideMetricsInternal GetServerSideMetrics()
        {
            TimeSpan totalTime = TimeSpan.Zero;
            long retrievedDocumentCount = 0;
            long retrievedDocumentSize = 0;
            long outputDocumentCount = 0;
            long outputDocumentSize = 0;
            double indexHitRatio = 0;
            QueryPreparationTimesAccumulator queryPreparationTimesAccumulator = new QueryPreparationTimesAccumulator();
            TimeSpan indexLookupTime = TimeSpan.Zero;
            TimeSpan documentLoadTime = TimeSpan.Zero;
            RuntimeExecutionTimesAccumulator runtimeExecutionTimesAccumulator = new RuntimeExecutionTimesAccumulator();
            TimeSpan documentWriteTime = TimeSpan.Zero;
            TimeSpan vMExecutionTime = TimeSpan.Zero;

            foreach (ServerSideMetricsInternal serverSideMetrics in this.serverSideMetricsList)
            {
                indexHitRatio = (retrievedDocumentCount + serverSideMetrics.RetrievedDocumentCount) != 0 ?
                    ((outputDocumentCount * indexHitRatio) + (serverSideMetrics.OutputDocumentCount * serverSideMetrics.IndexHitRatio)) / (retrievedDocumentCount + serverSideMetrics.RetrievedDocumentCount) :
                    0;
                totalTime += serverSideMetrics.TotalTime;
                retrievedDocumentCount += serverSideMetrics.RetrievedDocumentCount;
                retrievedDocumentSize += serverSideMetrics.RetrievedDocumentSize;
                outputDocumentCount += serverSideMetrics.OutputDocumentCount;
                outputDocumentSize += serverSideMetrics.OutputDocumentSize;
                queryPreparationTimesAccumulator.Accumulate(serverSideMetrics.QueryPreparationTimes);
                indexLookupTime += serverSideMetrics.IndexLookupTime;
                documentLoadTime += serverSideMetrics.DocumentLoadTime;
                runtimeExecutionTimesAccumulator.Accumulate(serverSideMetrics.RuntimeExecutionTimes);
                documentWriteTime += serverSideMetrics.DocumentWriteTime;
                vMExecutionTime += serverSideMetrics.VMExecutionTime;
            }

            return new ServerSideMetricsInternal(
                retrievedDocumentCount: retrievedDocumentCount,
                retrievedDocumentSize: retrievedDocumentSize,
                outputDocumentCount: outputDocumentCount,
                outputDocumentSize: outputDocumentSize,
                indexHitRatio: indexHitRatio,
                totalQueryExecutionTime: totalTime,
                queryPreparationTimes: queryPreparationTimesAccumulator.GetQueryPreparationTimes(),
                indexLookupTime: indexLookupTime,
                documentLoadTime: documentLoadTime,
                vmExecutionTime: vMExecutionTime,
                runtimeExecutionTimes: runtimeExecutionTimesAccumulator.GetRuntimeExecutionTimes(),
                documentWriteTime: documentWriteTime);
        }

        public static void WalkTraceTreeForQueryMetrics(ITrace currentTrace, ServerSideMetricsAccumulator accumulator)
        {
            if (currentTrace == null)
            {
                return;
            }

            foreach (object datum in currentTrace.Data.Values)
            {
                if (datum is QueryMetricsTraceDatum queryMetricsTraceDatum)
                {
                    accumulator.Accumulate(queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics);
                    return;
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                WalkTraceTreeForQueryMetrics(childTrace, accumulator);
            }

            return;
        }
    }
}
