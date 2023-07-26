//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal ref struct BackendMetricsAccumulator
    {
        public BackendMetricsAccumulator(
            TimeSpan totalTime,
            long retrievedDocumentCount,
            long retrievedDocumentSize,
            long outputDocumentCount,
            long outputDocumentSize,
            double indexHitRatio,
            QueryPreparationTimesAccumulator queryPreparationTimesAccumulator,
            TimeSpan indexLookupTime,
            TimeSpan documentLoadTime,
            RuntimeExecutionTimesAccumulator runtimeExecutionTimesAccumulator,
            TimeSpan documentWriteTime,
            TimeSpan vmExecutionTime)
        {
            this.TotalTime = totalTime;
            this.RetrievedDocumentCount = retrievedDocumentCount;
            this.RetrievedDocumentSize = retrievedDocumentSize;
            this.OutputDocumentCount = outputDocumentCount;
            this.OutputDocumentSize = outputDocumentSize;
            this.IndexHitRatio = indexHitRatio;
            this.QueryPreparationTimesAccumulator = queryPreparationTimesAccumulator;
            this.IndexLookupTime = indexLookupTime;
            this.DocumentLoadTime = documentLoadTime;
            this.RuntimeExecutionTimesAccumulator = runtimeExecutionTimesAccumulator;
            this.DocumentWriteTime = documentWriteTime;
            this.VMExecutionTime = vmExecutionTime;
        }

        public TimeSpan TotalTime { get; set; }
        public long RetrievedDocumentCount { get; set; }
        public long RetrievedDocumentSize { get; set; }
        public long OutputDocumentCount { get; set; }
        public long OutputDocumentSize { get; set; }
        public double IndexHitRatio { get; set; }
        public QueryPreparationTimesAccumulator QueryPreparationTimesAccumulator { get; set; }
        public TimeSpan IndexLookupTime { get; set; }
        public TimeSpan DocumentLoadTime { get; set; }
        public RuntimeExecutionTimesAccumulator RuntimeExecutionTimesAccumulator { get; set; }
        public TimeSpan DocumentWriteTime { get; set; }
        public TimeSpan VMExecutionTime { get; set; }

        public void Accumulate(BackendMetrics backendMetrics)
        {
            this.TotalTime += backendMetrics.TotalTime;
            this.RetrievedDocumentCount += backendMetrics.RetrievedDocumentCount;
            this.RetrievedDocumentSize += backendMetrics.RetrievedDocumentSize;
            this.OutputDocumentCount += backendMetrics.OutputDocumentCount;
            this.OutputDocumentSize += backendMetrics.OutputDocumentSize;
            this.IndexHitRatio = ((this.OutputDocumentCount * this.IndexHitRatio) + (backendMetrics.OutputDocumentCount * backendMetrics.IndexHitRatio)) / (this.RetrievedDocumentCount + backendMetrics.RetrievedDocumentCount);
            this.QueryPreparationTimesAccumulator.Accumulate(backendMetrics.QueryPreparationTimes);
            this.IndexLookupTime += backendMetrics.IndexLookupTime;
            this.DocumentLoadTime += backendMetrics.DocumentLoadTime;
            this.RuntimeExecutionTimesAccumulator.Accumulate(backendMetrics.RuntimeExecutionTimes);
            this.DocumentWriteTime += backendMetrics.DocumentWriteTime;
            this.VMExecutionTime += backendMetrics.VMExecutionTime;
        }

        public static BackendMetrics ToBackendMetrics(BackendMetricsAccumulator accumulator)
        {
            return new BackendMetrics(
                retrievedDocumentCount: accumulator.RetrievedDocumentCount,
                retrievedDocumentSize: accumulator.RetrievedDocumentSize,
                outputDocumentCount: accumulator.OutputDocumentCount,
                outputDocumentSize: accumulator.OutputDocumentSize,
                indexHitRatio: accumulator.IndexHitRatio,
                totalQueryExecutionTime: accumulator.TotalTime,
                queryPreparationTimes: QueryPreparationTimesAccumulator.ToQueryPreparationTimes(accumulator.QueryPreparationTimesAccumulator),
                indexLookupTime: accumulator.IndexLookupTime,
                documentLoadTime: accumulator.DocumentLoadTime,
                vmExecutionTime: accumulator.VMExecutionTime,
                runtimeExecutionTimes: RuntimeExecutionTimesAccumulator.ToRuntimeExecutionTimes(accumulator.RuntimeExecutionTimesAccumulator),
                documentWriteTime: accumulator.DocumentWriteTime);
        }

        public static void WalkTraceTreeForQueryMetrics(ITrace currentTrace, BackendMetricsAccumulator accumulator)
        {
            if (currentTrace == null)
            {
                return;
            }

            foreach (object datum in currentTrace.Data.Values)
            {
                if (datum is QueryMetricsTraceDatum queryMetricsTraceDatum)
                {
                    accumulator.Accumulate(queryMetricsTraceDatum.QueryMetrics.BackendMetrics);
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
