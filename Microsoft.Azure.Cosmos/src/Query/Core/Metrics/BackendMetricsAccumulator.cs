//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal class BackendMetricsAccumulator
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

        public BackendMetricsAccumulator()
        {
            this.TotalTime = default;
            this.RetrievedDocumentCount = default;
            this.RetrievedDocumentSize = default;
            this.OutputDocumentCount = default;
            this.OutputDocumentSize = default;
            this.IndexHitRatio = default;
            this.QueryPreparationTimesAccumulator = new QueryPreparationTimesAccumulator();
            this.IndexLookupTime = default;
            this.DocumentLoadTime = default;
            this.RuntimeExecutionTimesAccumulator = new RuntimeExecutionTimesAccumulator();
            this.DocumentWriteTime = default;
            this.VMExecutionTime = default;
        }

        private TimeSpan TotalTime { get; set; }
        private long RetrievedDocumentCount { get; set; }
        private long RetrievedDocumentSize { get; set; }
        private long OutputDocumentCount { get; set; }
        private long OutputDocumentSize { get; set; }
        private double IndexHitRatio { get; set; }
        private QueryPreparationTimesAccumulator QueryPreparationTimesAccumulator { get; }
        private TimeSpan IndexLookupTime { get; set; }
        private TimeSpan DocumentLoadTime { get; set; }
        private RuntimeExecutionTimesAccumulator RuntimeExecutionTimesAccumulator { get; }
        private TimeSpan DocumentWriteTime { get; set; }
        private TimeSpan VMExecutionTime { get; set; }

        public void Accumulate(BackendMetrics backendMetrics)
        {
            this.IndexHitRatio = ((this.OutputDocumentCount * this.IndexHitRatio) + (backendMetrics.OutputDocumentCount * backendMetrics.IndexHitRatio)) / (this.RetrievedDocumentCount + backendMetrics.RetrievedDocumentCount);
            this.TotalTime += backendMetrics.TotalTime;
            this.RetrievedDocumentCount += backendMetrics.RetrievedDocumentCount;
            this.RetrievedDocumentSize += backendMetrics.RetrievedDocumentSize;
            this.OutputDocumentCount += backendMetrics.OutputDocumentCount;
            this.OutputDocumentSize += backendMetrics.OutputDocumentSize;
            this.QueryPreparationTimesAccumulator.Accumulate(backendMetrics.QueryPreparationTimes);
            this.IndexLookupTime += backendMetrics.IndexLookupTime;
            this.DocumentLoadTime += backendMetrics.DocumentLoadTime;
            this.RuntimeExecutionTimesAccumulator.Accumulate(backendMetrics.RuntimeExecutionTimes);
            this.DocumentWriteTime += backendMetrics.DocumentWriteTime;
            this.VMExecutionTime += backendMetrics.VMExecutionTime;
        }

        public BackendMetrics GetBackendMetrics()
        {
            return new BackendMetrics(
                retrievedDocumentCount: this.RetrievedDocumentCount,
                retrievedDocumentSize: this.RetrievedDocumentSize,
                outputDocumentCount: this.OutputDocumentCount,
                outputDocumentSize: this.OutputDocumentSize,
                indexHitRatio: this.IndexHitRatio,
                totalQueryExecutionTime: this.TotalTime,
                queryPreparationTimes: this.QueryPreparationTimesAccumulator.GetQueryPreparationTimes(),
                indexLookupTime: this.IndexLookupTime,
                documentLoadTime: this.DocumentLoadTime,
                vmExecutionTime: this.VMExecutionTime,
                runtimeExecutionTimes: this.RuntimeExecutionTimesAccumulator.GetRuntimeExecutionTimes(),
                documentWriteTime: this.DocumentWriteTime);
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
