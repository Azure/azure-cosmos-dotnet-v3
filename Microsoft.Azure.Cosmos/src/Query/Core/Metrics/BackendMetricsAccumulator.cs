//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal class BackendMetricsAccumulator
    {
        public BackendMetricsAccumulator()
        {
            this.BackendMetricsList = new List<BackendMetrics>();
        }

        private readonly List<BackendMetrics> BackendMetricsList;

        public void Accumulate(BackendMetrics backendMetrics)
        {
            if (backendMetrics == null)
            {
                throw new ArgumentNullException(nameof(backendMetrics));
            }

            this.BackendMetricsList.Add(backendMetrics);
        }

        public BackendMetrics GetBackendMetrics()
        {
            TimeSpan totalTime = default;
            long retrievedDocumentCount = default;
            long retrievedDocumentSize = default;
            long outputDocumentCount = default;
            long outputDocumentSize = default;
            double indexHitRatio = default;
            QueryPreparationTimesAccumulator queryPreparationTimesAccumulator = new QueryPreparationTimesAccumulator();
            TimeSpan indexLookupTime = default;
            TimeSpan documentLoadTime = default;
            RuntimeExecutionTimesAccumulator runtimeExecutionTimesAccumulator = new RuntimeExecutionTimesAccumulator();
            TimeSpan documentWriteTime = default;
            TimeSpan vMExecutionTime = default;

            foreach (BackendMetrics backendMetrics in this.BackendMetricsList)
            {
                indexHitRatio = ((outputDocumentCount * indexHitRatio) + (backendMetrics.OutputDocumentCount * backendMetrics.IndexHitRatio)) / (retrievedDocumentCount + backendMetrics.RetrievedDocumentCount);
                totalTime += backendMetrics.TotalTime;
                retrievedDocumentCount += backendMetrics.RetrievedDocumentCount;
                retrievedDocumentSize += backendMetrics.RetrievedDocumentSize;
                outputDocumentCount += backendMetrics.OutputDocumentCount;
                outputDocumentSize += backendMetrics.OutputDocumentSize;
                queryPreparationTimesAccumulator.Accumulate(backendMetrics.QueryPreparationTimes);
                indexLookupTime += backendMetrics.IndexLookupTime;
                documentLoadTime += backendMetrics.DocumentLoadTime;
                runtimeExecutionTimesAccumulator.Accumulate(backendMetrics.RuntimeExecutionTimes);
                documentWriteTime += backendMetrics.DocumentWriteTime;
                vMExecutionTime += backendMetrics.VMExecutionTime;
            }

            return new BackendMetrics(
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
