// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Azure.Documents;

    internal sealed class SummaryDiagnosticsTraceDatum : TraceDatum
    {
        private const string TransitTimeEventName = "Transit Time";

        public SummaryDiagnosticsTraceDatum(ITrace trace)
        {
            this.DirectRequestsSummary = new RequestSummary();
            this.GatewayRequestsSummary = new GatewayRequestSummary();
            this.TotalTimeInMs = trace.Duration.TotalMilliseconds;
            this.CollectSummaryFromTraceTree(trace);
        }

        public double TotalTimeInMs { get; }
        public double MaxServiceProcessingTimeInMs { get; private set; }
        public double MaxNetworkingTimeInMs { get; private set; }
        public double MaxGatewayRequestTimeInMs { get; private set; }
        public RequestSummary DirectRequestsSummary { get; }
        public GatewayRequestSummary GatewayRequestsSummary { get; }

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

            //foreach (ITrace childTrace in currentTrace.Children)
            //{
            //    this.CollectSummaryFromTraceTree(childTrace);
            //}
        }

        private void AgrregateGatewayStatistics(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics> httpResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpResponseStatistics in httpResponseStatisticsList)
            {
                this.GatewayRequestsSummary.RecordHttpResponse(httpResponseStatistics);

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
                this.DirectRequestsSummary.RecordStatusCode((int)statusCode);

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

        public class RequestSummary
        {
            public int TotalCalls { get; protected set; }
            public int NumberOf429s { get; private set; }
            public int NumberOf410s { get; private set; }
            public int NumberOf408s { get; private set; }
            public int NumberOf449s { get; private set; }
            public int NumberOf404s { get; private set; }
            public int OtherErrors { get; private set; }
            public int SuccessfullCalls { get; private set; }

            public void RecordStatusCode(int statusCode)
            {
                this.TotalCalls++;
                if (statusCode >= 200 && statusCode <= 299)
                {
                    this.SuccessfullCalls++;
                    return;
                }

                switch ((int)statusCode)
                {
                    case 429:
                        this.NumberOf429s++;
                        break;
                    case 410:
                        this.NumberOf410s++;
                        break;
                    case 408:
                        this.NumberOf408s++;
                        break;
                    case 449:
                        this.NumberOf449s++;
                        break;
                    case 404:
                        this.NumberOf404s++;
                        break;
                    default:
                        this.OtherErrors++;
                        break;
                }
            }
        }

        public class GatewayRequestSummary : RequestSummary
        {
            public int NumberOfOperationCancelledExceptions { get; private set; }
            public int NumberOfWebExceptions { get; private set; }
            public int NumberOfHttpRequestExceptions { get; private set; }
            public int OtherExceptions { get; private set; }

            public void RecordHttpResponse(ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpResponseStatistics)
            {
                this.TotalCalls++;
                if (httpResponseStatistics.Exception != null)
                {
                    switch (httpResponseStatistics.Exception)
                    {
                        case OperationCanceledException operationCanceledException:
                            this.NumberOfOperationCancelledExceptions++;
                            break;
                        case WebException webException:
                            this.NumberOfWebExceptions++;
                            break;
                        case HttpRequestException httpRequestException:
                            this.NumberOfHttpRequestExceptions++;
                            break;
                        default:
                            this.OtherExceptions++;
                            break;
                    }
                }
                else if (httpResponseStatistics.HttpResponseMessage != null)
                {
                    base.RecordStatusCode((int)httpResponseStatistics.HttpResponseMessage.StatusCode);
                }
            }
        }
    }
}
