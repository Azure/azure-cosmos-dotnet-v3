// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;

    internal struct SummaryDiagnostics
    {
        public SummaryDiagnostics(ITrace trace)
            : this()
        {
            this.DirectRequestsSummary = new RequestSummary();
            this.GatewayRequestsSummary = new RequestSummary();
            this.TotalTimeInMs = trace.Duration.TotalMilliseconds;
            this.CollectSummaryFromTraceTree(trace);
        }

        public double TotalTimeInMs { get; }
        public double MaxServiceProcessingTimeInMs { get; private set; }
        public double MaxGatewayRequestTimeInMs { get; private set; }
        public RequestSummary DirectRequestsSummary { get; private set; }
        public RequestSummary GatewayRequestsSummary { get; private set; }

        private void CollectSummaryFromTraceTree(ITrace currentTrace)
        {
            foreach (object datums in currentTrace.Data.Values)
            {
                // TODO: Add MaxCpuUsage using CpuHistoryTraceDatum
                if (datums is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    this.AggregateStatsFromStoreResults(clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList);
                    this.AggregateGatewayStatistics(clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList);
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                this.CollectSummaryFromTraceTree(childTrace);
            }
        }

        private void AggregateGatewayStatistics(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics> httpResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpResponseStatistics in httpResponseStatisticsList)
            {
                this.GatewayRequestsSummary = this.GatewayRequestsSummary.RecordStatusCode((httpResponseStatistics.HttpResponseMessage != null) ? 
                                                              (int)httpResponseStatistics.HttpResponseMessage.StatusCode : 0);

                if (httpResponseStatistics.Duration.TotalMilliseconds > this.MaxGatewayRequestTimeInMs)
                {
                    this.MaxGatewayRequestTimeInMs = httpResponseStatistics.Duration.TotalMilliseconds;
                }
            }
        }

        private void AggregateStatsFromStoreResults(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics> storeResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStatistics in storeResponseStatisticsList)
            {
                StatusCodes statusCode = storeResponseStatistics.StoreResult.StatusCode;
                this.DirectRequestsSummary = this.DirectRequestsSummary.RecordStatusCode((int)statusCode);

                if (double.TryParse(storeResponseStatistics.StoreResult.BackendRequestDurationInMs, 
                                    NumberStyles.Number,
                                    CultureInfo.InvariantCulture,
                                    out double backendLatency))
                {
                    if (backendLatency > this.MaxServiceProcessingTimeInMs)
                    {
                        this.MaxServiceProcessingTimeInMs = backendLatency;
                    }
                }
            }
        }

        public void WriteSummaryDiagnostics(IJsonWriter jsonWriter)
        {
            jsonWriter.WriteObjectStart();

            jsonWriter.WriteFieldName("TotalTimeMs");
            jsonWriter.WriteNumber64Value(this.TotalTimeInMs);

            if (this.DirectRequestsSummary.TotalCalls > 0)
            {
                jsonWriter.WriteFieldName("Direct");
                jsonWriter.WriteObjectStart();
                SummaryDiagnostics.WriteRequestSummaryObject(jsonWriter, this.DirectRequestsSummary);
                jsonWriter.WriteObjectEnd();

                jsonWriter.WriteFieldName("MaxBELatencyMs");
                jsonWriter.WriteNumber64Value(this.MaxServiceProcessingTimeInMs);
            }

            if (this.GatewayRequestsSummary.TotalCalls > 0)
            {
                jsonWriter.WriteFieldName("Gateway");
                jsonWriter.WriteObjectStart();
                SummaryDiagnostics.WriteRequestSummaryObject(jsonWriter, this.GatewayRequestsSummary);
                jsonWriter.WriteObjectEnd();

                jsonWriter.WriteFieldName("MaxGatewayRequestTimeInMs");
                jsonWriter.WriteNumber64Value(this.MaxGatewayRequestTimeInMs);
            }

            jsonWriter.WriteObjectEnd();
        }

        private static void WriteRequestSummaryObject(IJsonWriter jsonWriter, RequestSummary requestSummary)
        {
            if (requestSummary.SuccessfullCalls > 0)
            {
                jsonWriter.WriteFieldName("SuccessfullCalls");
                jsonWriter.WriteNumber64Value(requestSummary.SuccessfullCalls);
            }

            if (requestSummary.NumberOf404s > 0)
            {
                jsonWriter.WriteFieldName("404");
                jsonWriter.WriteNumber64Value(requestSummary.NumberOf404s);
            }

            if (requestSummary.NumberOf408s > 0)
            {
                jsonWriter.WriteFieldName("408");
                jsonWriter.WriteNumber64Value(requestSummary.NumberOf408s);
            }

            if (requestSummary.NumberOf410s > 0)
            {
                jsonWriter.WriteFieldName("410");
                jsonWriter.WriteNumber64Value(requestSummary.NumberOf410s);
            }

            if (requestSummary.NumberOf429s > 0)
            {
                jsonWriter.WriteFieldName("429");
                jsonWriter.WriteNumber64Value(requestSummary.NumberOf429s);
            }

            if (requestSummary.NumberOf449s > 0)
            {
                jsonWriter.WriteFieldName("449");
                jsonWriter.WriteNumber64Value(requestSummary.NumberOf449s);
            }

            if (requestSummary.OtherErrors > 0)
            {
                jsonWriter.WriteFieldName("OtherStatusCodes");
                jsonWriter.WriteNumber64Value(requestSummary.OtherErrors);
            }
        }

        public struct RequestSummary
        {
            public int TotalCalls { get; private set; }
            public int NumberOf429s { get; private set; }
            public int NumberOf410s { get; private set; }
            public int NumberOf408s { get; private set; }
            public int NumberOf449s { get; private set; }
            public int NumberOf404s { get; private set; }
            public int OtherErrors { get; private set; }
            public int SuccessfullCalls { get; private set; }

            public RequestSummary RecordStatusCode(int statusCode)
            {
                this.TotalCalls++;
                if (statusCode >= 200 && statusCode <= 299)
                {
                    this.SuccessfullCalls++;
                    return this;
                }

                switch (statusCode)
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

                return this;
            }
        }
    }
}