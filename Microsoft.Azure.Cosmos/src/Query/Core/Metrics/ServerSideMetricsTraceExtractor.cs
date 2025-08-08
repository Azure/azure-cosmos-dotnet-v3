//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Linq;
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

            // Use thread-safe enumeration to avoid concurrency issues  
            object[] dataSnapshot = null;
            if (currentTrace is Tracing.Trace concreteTrace)
            {
                var dataSnapshotPairs = concreteTrace.GetDataSnapshot();
                dataSnapshot = dataSnapshotPairs.Select(kvp => kvp.Value).ToArray();
            }
            else
            {
                dataSnapshot = currentTrace.Data.Values.ToArray();
            }

            foreach (object datum in dataSnapshot)
            {
                if (datum is QueryMetricsTraceDatum queryMetricsTraceDatum)
                {
                    ServerSideMetricsInternal serverSideMetrics = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics;
                    serverSideMetrics.FeedRange = currentTrace.Name;
                    ServerSideMetricsTraceExtractor.WalkTraceTreeForPartitionInfo(currentTrace, serverSideMetrics);
                    accumulator.Accumulate(serverSideMetrics);
                }
            }

            // Use thread-safe enumeration to avoid concurrency issues
            ITrace[] childrenSnapshot = null;
            if (currentTrace is Tracing.Trace concreteTraceForChildren)
            {
                childrenSnapshot = concreteTraceForChildren.GetChildrenSnapshot();
            }
            else
            {
                childrenSnapshot = currentTrace.Children.ToArray();
            }

            foreach (ITrace childTrace in childrenSnapshot)
            {
                ServerSideMetricsTraceExtractor.WalkTraceTreeForQueryMetrics(childTrace, accumulator);
            }
        }

        private static void WalkTraceTreeForPartitionInfo(ITrace currentTrace, ServerSideMetricsInternal serverSideMetrics)
        {
            if (currentTrace == null)
            {
                return;
            }

            // Use thread-safe enumeration to avoid concurrency issues  
            object[] dataSnapshot = null;
            if (currentTrace is Tracing.Trace concreteTrace)
            {
                var dataSnapshotPairs = concreteTrace.GetDataSnapshot();
                dataSnapshot = dataSnapshotPairs.Select(kvp => kvp.Value).ToArray();
            }
            else
            {
                dataSnapshot = currentTrace.Data.Values.ToArray();
            }

            foreach (Object datum in dataSnapshot)
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
                else if (datum is PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
                {
                    serverSideMetrics.RequestCharge = pointOperationStatisticsTraceDatum.RequestCharge;
                }
            }

            // Use thread-safe enumeration to avoid concurrency issues
            ITrace[] childrenSnapshot2 = null;
            if (currentTrace is Tracing.Trace concreteTraceForChildren2)
            {
                childrenSnapshot2 = concreteTraceForChildren2.GetChildrenSnapshot();
            }
            else
            {
                childrenSnapshot2 = currentTrace.Children.ToArray();
            }

            foreach (ITrace childTrace in childrenSnapshot2)
            {
                ServerSideMetricsTraceExtractor.WalkTraceTreeForPartitionInfo(childTrace, serverSideMetrics);
            }
        }
    }
}
