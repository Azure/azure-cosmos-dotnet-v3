//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal sealed class ServerSideMetricsInternalAccumulator
    {
        private readonly List<ServerSideMetricsInternal> serverSideMetricsList;

        public ServerSideMetricsInternalAccumulator()
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
            double requestCharge = 0;

            foreach (ServerSideMetricsInternal serverSideMetrics in this.serverSideMetricsList)
            {
                indexHitRatio = (retrievedDocumentCount + serverSideMetrics.RetrievedDocumentCount) != 0 ?
                    ((retrievedDocumentCount * indexHitRatio) + (serverSideMetrics.RetrievedDocumentCount * serverSideMetrics.IndexHitRatio)) / (retrievedDocumentCount + serverSideMetrics.RetrievedDocumentCount) :
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
                requestCharge += serverSideMetrics.RequestCharge;
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
                documentWriteTime: documentWriteTime,
                requestCharge: requestCharge);
        }

        public List<ServerSideMetricsInternal> GetPartitionedServerSideMetrics()
        {
            return this.serverSideMetricsList;
        }
    }
}
