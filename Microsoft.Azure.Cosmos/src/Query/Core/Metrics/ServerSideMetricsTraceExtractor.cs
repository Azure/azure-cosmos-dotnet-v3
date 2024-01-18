//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal class ServerSideMetricsTraceExtractor
    {
        public ServerSideMetricsInternal ServerSideMetrics { get; }

        public ServerSideMetricsTraceExtractor(ServerSideMetricsInternal serverSideMetrics, ITrace trace)
        {
            this.ServerSideMetrics = serverSideMetrics;
            this.ServerSideMetrics.FeedRange = trace.Name;
            this.WalkTraceTree(trace);
        }

        private void WalkTraceTree(ITrace currentTrace)
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
                            this.ServerSideMetrics.PartitionKeyRangeId = pKRangeId;
                        }

                        this.ServerSideMetrics.RequestCharge = clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList[0].StoreResult.RequestCharge;
                    }
                }
                if (datum is PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
                {
                    this.ServerSideMetrics.RequestCharge = pointOperationStatisticsTraceDatum.RequestCharge;
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                this.WalkTraceTree(childTrace);
            }
        }
    }
}
