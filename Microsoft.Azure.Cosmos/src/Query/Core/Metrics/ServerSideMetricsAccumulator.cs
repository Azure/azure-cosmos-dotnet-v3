//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal sealed class ServerSideMetricsAccumulator
    {
        private readonly List<ServerSideMetrics> serverSideMetricsList;

        public ServerSideMetricsAccumulator()
        {
            this.serverSideMetricsList = new List<ServerSideMetrics>();
        }

        public void Accumulate(ServerSideMetrics serverSideMetrics)
        {
            if (serverSideMetrics == null)
            {
                throw new ArgumentNullException(nameof(serverSideMetrics));
            }

            this.serverSideMetricsList.Add(serverSideMetrics);
        }

        public ServerSideMetrics GetServerSideMetrics()
        {
            TimeSpan totalTime = TimeSpan.Zero;
            long retrievedDocumentCount = 0;
            long retrievedDocumentSize = 0;
            long outputDocumentCount = 0;
            long outputDocumentSize = 0;
            double indexHitRatio = 0;
            TimeSpan queryPreparationTime = TimeSpan.Zero;
            TimeSpan indexLookupTime = TimeSpan.Zero;
            TimeSpan documentLoadTime = TimeSpan.Zero;
            TimeSpan runtimeExecutionTime = TimeSpan.Zero;
            TimeSpan documentWriteTime = TimeSpan.Zero;
            TimeSpan vMExecutionTime = TimeSpan.Zero;

            foreach (ServerSideMetrics serverSideMetrics in this.serverSideMetricsList)
            {
                indexHitRatio = (retrievedDocumentCount + serverSideMetrics.RetrievedDocumentCount) != 0 ?
                    ((retrievedDocumentCount * indexHitRatio) + (serverSideMetrics.RetrievedDocumentCount * serverSideMetrics.IndexHitRatio)) / (retrievedDocumentCount + serverSideMetrics.RetrievedDocumentCount) :
                    0;
                totalTime += serverSideMetrics.TotalTime;
                retrievedDocumentCount += serverSideMetrics.RetrievedDocumentCount;
                retrievedDocumentSize += serverSideMetrics.RetrievedDocumentSize;
                outputDocumentCount += serverSideMetrics.OutputDocumentCount;
                outputDocumentSize += serverSideMetrics.OutputDocumentSize;
                queryPreparationTime += serverSideMetrics.QueryPreparationTime;
                indexLookupTime += serverSideMetrics.IndexLookupTime;
                documentLoadTime += serverSideMetrics.DocumentLoadTime;
                runtimeExecutionTime += serverSideMetrics.RuntimeExecutionTime;
                documentWriteTime += serverSideMetrics.DocumentWriteTime;
                vMExecutionTime += serverSideMetrics.VMExecutionTime;
            }

            return new ServerSideMetrics(
                retrievedDocumentCount: retrievedDocumentCount,
                retrievedDocumentSize: retrievedDocumentSize,
                outputDocumentCount: outputDocumentCount,
                outputDocumentSize: outputDocumentSize,
                indexHitRatio: indexHitRatio,
                totalQueryExecutionTime: totalTime,
                queryPreparationTimes: queryPreparationTime,
                indexLookupTime: indexLookupTime,
                documentLoadTime: documentLoadTime,
                vmExecutionTime: vMExecutionTime,
                runtimeExecutionTimes: runtimeExecutionTime,
                documentWriteTime: documentWriteTime);
        }
    }
}
