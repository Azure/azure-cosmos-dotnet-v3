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

        public List<ServerSideMetricsInternal> GetPartitionedServerSideMetrics()
        {
            return this.serverSideMetricsList;
        }

        public static void WalkTraceTreeForQueryMetrics(ITrace currentTrace, ServerSideMetricsInternalAccumulator accumulator)
        {
            if (currentTrace == null)
            {
                return;
            }

            foreach (object datum in currentTrace.Data.Values)
            {
                if (datum is QueryMetricsTraceDatum queryMetricsTraceDatum)
                {
                    queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.FeedRange = currentTrace.Name;
                    queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.PartitionKeyRangeId = WalkTraceTreeForPartitionKeyRangeId(currentTrace);
                    queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.RequestCharge = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.PartitionKeyRangeId != null
                        ? WalkTraceTreeForRequestCharge(currentTrace)
                        : WalkTraceTreeForRequestChargeGateway(currentTrace);

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

        private static int? WalkTraceTreeForPartitionKeyRangeId(ITrace currentTrace)
        {
            if (currentTrace == null)
            {
                return null;
            }

            foreach (Object datum in currentTrace.Data.Values)
            {
                if (datum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    if (clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList.Count > 0)
                    {
                        return int.TryParse(clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList[0].StoreResult.PartitionKeyRangeId, out int pKRangeId)
                            ? pKRangeId
                            : null;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                int? partitionKeyRangeId = WalkTraceTreeForPartitionKeyRangeId(childTrace);
                if (partitionKeyRangeId != null)
                {
                    return partitionKeyRangeId;
                }
            }

            return null;
        }

        private static double? WalkTraceTreeForRequestCharge(ITrace currentTrace)
        {
            if (currentTrace == null)
            {
                return null;
            }

            foreach (Object datum in currentTrace.Data.Values)
            {
                if (datum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    if (clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList.Count > 0)
                    {
                        return clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList[0].StoreResult.RequestCharge;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                double? requestCharge = WalkTraceTreeForRequestCharge(childTrace);
                if (requestCharge != null)
                {
                    return requestCharge;
                }
            }

            return null;
        }

        private static double? WalkTraceTreeForRequestChargeGateway(ITrace currentTrace)
        {
            if (currentTrace == null)
            {
                return null;
            }

            foreach (Object datum in currentTrace.Data.Values)
            {
                if (datum is PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
                {
                    return pointOperationStatisticsTraceDatum.RequestCharge;
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                double? requestCharge = WalkTraceTreeForRequestChargeGateway(childTrace);
                if (requestCharge != null)
                {
                    return requestCharge;
                }
            }

            return null;
        }
    }
}
