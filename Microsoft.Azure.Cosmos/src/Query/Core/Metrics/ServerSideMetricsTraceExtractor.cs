//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal static class ServerSideMetricsTraceExtractor
    {
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
                    ServerSideMetricsInternal serverSideMetrics = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics;
                    serverSideMetrics.FeedRange = currentTrace.Name;
                    ServerSideMetricsTraceExtractor.WalkTraceTreeForPartitionInfo(currentTrace, serverSideMetrics);
                    accumulator.Accumulate(serverSideMetrics);
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                ServerSideMetricsTraceExtractor.WalkTraceTreeForQueryMetrics(childTrace, accumulator);
            }

            return;
        }

        private static void WalkTraceTreeForPartitionInfo(ITrace currentTrace, ServerSideMetricsInternal serverSideMetrics)
        {
            if (currentTrace == null)
            {
                return;
            }

            foreach (Object datum in currentTrace.Data.Values)
            {
                if (datum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    if (clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList.Count > 0)
                    {
                        if (int.TryParse(clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList[0].StoreResult.PartitionKeyRangeId, out int pKRangeId))
                        {
                            serverSideMetrics.PartitionKeyRangeId = pKRangeId;
                        }

                        serverSideMetrics.RequestCharge = clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList[0].StoreResult.RequestCharge;
                    }
                }
                if (datum is PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
                {
                    serverSideMetrics.RequestCharge = pointOperationStatisticsTraceDatum.RequestCharge;
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                ServerSideMetricsTraceExtractor.WalkTraceTreeForPartitionInfo(childTrace, serverSideMetrics);
            }
        }
    }
}
