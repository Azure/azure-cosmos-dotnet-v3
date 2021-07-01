// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Documents;

    internal sealed class SummaryDiagnosticsTraceDatum : TraceDatum
    {
        private const string TransitTimeEventName = "Transit Time";

        public SummaryDiagnosticsTraceDatum(ITrace trace)
        {
            this.NumberOfRequestsPerStatusCode = new Dictionary<StatusCodes, int>();
            this.NumberOfGateWayRequestsPerStatusCode = new Dictionary<string, int>(); // string to capture exceptions
            this.TotalTimeInMs = trace.Duration.TotalMilliseconds;
            this.CollectSummaryFromTraceTree(trace);
        }

        public double TotalTimeInMs { get; }
        public double MaxServiceProcessingTimeInMs { get; private set; }
        public double MaxNetworkingTimeInMs { get; private set; }
        public double MaxGatewayRequestTimeInMs { get; private set; }
        public Dictionary<StatusCodes, int> NumberOfRequestsPerStatusCode { get; }
        public Dictionary<string, int> NumberOfGateWayRequestsPerStatusCode { get; }

        private void CollectSummaryFromTraceTree(ITrace currentTrace)
        {
            foreach (object datums in currentTrace.Data.Values)
            {
                // TODO: Add MaxCpuUsage using CpuHistoryTraceDatum
                if (datums is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    this.AgrregateStatsFromStoreResults(clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList);
                    this.AgrregateGatewayStatistics(clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList);
                    return;
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                this.CollectSummaryFromTraceTree(childTrace);
            }
        }

        private void AgrregateGatewayStatistics(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics> httpResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpResponseStatistics in httpResponseStatisticsList)
            {
                string statusCodeOrException = (httpResponseStatistics.Exception != null) ? httpResponseStatistics.Exception.GetType().ToString() :
                                                httpResponseStatistics.HttpResponseMessage.StatusCode.ToString();

                if (this.NumberOfGateWayRequestsPerStatusCode.ContainsKey(statusCodeOrException))
                {
                    this.NumberOfGateWayRequestsPerStatusCode[statusCodeOrException]++;
                }
                else
                {
                    this.NumberOfGateWayRequestsPerStatusCode[statusCodeOrException] = 1;
                }

                if (httpResponseStatistics.Duration.TotalMilliseconds > this.MaxGatewayRequestTimeInMs)
                {
                    this.MaxGatewayRequestTimeInMs = httpResponseStatistics.Duration.TotalMilliseconds;
                }
            }
        }

        private void AgrregateStatsFromStoreResults(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics> storeResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStatistics in storeResponseStatisticsList)
            {
                StatusCodes statusCode = storeResponseStatistics.StoreResult.StatusCode;
                if (this.NumberOfRequestsPerStatusCode.ContainsKey(statusCode))
                {
                    this.NumberOfRequestsPerStatusCode[statusCode]++;
                }
                else
                {
                    this.NumberOfRequestsPerStatusCode[statusCode] = 1;
                }

                double? transitTimeInMs = null;
                TransportRequestStats transportRequestStats = storeResponseStatistics.StoreResult.TransportRequestStats;
                if (transportRequestStats != null)
                {
                    foreach (TransportRequestStats.RequestEvent requestEvent in transportRequestStats.GetRequestTimeline())
                    {
                        if (requestEvent.EventName == SummaryDiagnosticsTraceDatum.TransitTimeEventName)
                        {
                            transitTimeInMs = (double)requestEvent.DurationInMicroSec / 1000;
                        }
                    }
                }

                if (double.TryParse(storeResponseStatistics.StoreResult.BackendRequestDurationInMs, out double backendLatency))
                {
                    if (backendLatency > this.MaxServiceProcessingTimeInMs)
                    {
                        this.MaxServiceProcessingTimeInMs = backendLatency;
                    }

                    if (transitTimeInMs.HasValue && (transitTimeInMs.Value - backendLatency > this.MaxNetworkingTimeInMs))
                    {
                        this.MaxNetworkingTimeInMs = transitTimeInMs.Value - backendLatency;
                    }
                } 
            }
        }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }
    }
}
