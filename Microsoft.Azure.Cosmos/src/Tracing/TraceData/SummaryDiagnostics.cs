// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;

    internal struct SummaryDiagnostics
    {
        public SummaryDiagnostics(ITrace trace)
            : this()
        {
            this.CollectSummaryFromTraceTree(trace.Summary);
        }

        public Lazy<HashSet<Uri>> AllRegionsContacted { get; private set; } = new Lazy<HashSet<Uri>>();

        // Count of (StatusCode, SubStatusCode) tuples
        public Lazy<Dictionary<(int statusCode, int subStatusCode), int>> DirectRequestsSummary { get; private set; } = new Lazy<Dictionary<(int statusCode, int subStatusCode), int>>();

        // Count of (StatusCode, SubStatusCode) tuples
        public Lazy<Dictionary<(int statusCode, int subStatusCode), int>> GatewayRequestsSummary { get; private set; } = new Lazy<Dictionary<(int statusCode, int subStatusCode), int>>();

        private void CollectSummaryFromTraceTree(TraceSummary summary)
        {
            this.AggregateStatsFromStoreResults(summary.StoreResponseStatistics);
            this.AggregateGatewayStatistics(summary.HttpResponseStatistics);
            this.AggregateRegionsContacted(summary.RegionsContacted);
        }

        private void AggregateRegionsContacted(IReadOnlyList<(string, Uri)> regionsContacted)
        {
            foreach ((string _, Uri uri) in regionsContacted)
            {
                this.AllRegionsContacted.Value.Add(uri);
            }
        }

        private void AggregateGatewayStatistics(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics> httpResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpResponseStatistics in httpResponseStatisticsList)
            {
                int statusCode = 0;
                int substatusCode = 0;
                if (httpResponseStatistics.HttpResponseMessage != null)
                {
                    statusCode = (int)httpResponseStatistics.HttpResponseMessage.StatusCode;
                    HttpResponseHeadersWrapper gatewayHeaders = new HttpResponseHeadersWrapper(
                                                    httpResponseStatistics.HttpResponseMessage.Headers,
                                                    httpResponseStatistics.HttpResponseMessage.Content?.Headers);
                    if (!int.TryParse(gatewayHeaders.SubStatus,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out substatusCode))
                    {
                        substatusCode = 0;
                    }
                }

                if (!this.GatewayRequestsSummary.Value.ContainsKey((statusCode, substatusCode)))
                {
                    this.GatewayRequestsSummary.Value[(statusCode, substatusCode)] = 1;
                }
                else
                {
                    this.GatewayRequestsSummary.Value[(statusCode, substatusCode)]++;
                }
            }
        }

        private void AggregateStatsFromStoreResults(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics> storeResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStatistics in storeResponseStatisticsList)
            {
                int statusCode = (int)storeResponseStatistics.StoreResult.StatusCode;
                int subStatusCode = (int)storeResponseStatistics.StoreResult.SubStatusCode;
                if (!this.DirectRequestsSummary.Value.ContainsKey((statusCode, subStatusCode)))
                {
                    this.DirectRequestsSummary.Value[(statusCode, subStatusCode)] = 1;
                }
                else
                {
                    this.DirectRequestsSummary.Value[(statusCode, subStatusCode)]++;
                }
            }
        }

        public void WriteSummaryDiagnostics(IJsonWriter jsonWriter)
        {
            jsonWriter.WriteObjectStart();

            if (this.DirectRequestsSummary.IsValueCreated)
            {
                jsonWriter.WriteFieldName("DirectCalls");
                jsonWriter.WriteObjectStart();
                foreach (KeyValuePair<(int, int), int> kvp in this.DirectRequestsSummary.Value)
                {
                    jsonWriter.WriteFieldName(kvp.Key.ToString());
                    jsonWriter.WriteNumber64Value(kvp.Value);
                }
                jsonWriter.WriteObjectEnd();
            }

            if (this.AllRegionsContacted.IsValueCreated)
            {
                jsonWriter.WriteFieldName("RegionsContacted");
                jsonWriter.WriteNumber64Value(this.AllRegionsContacted.Value.Count);
            }

            if (this.GatewayRequestsSummary.IsValueCreated)
            {
                jsonWriter.WriteFieldName("GatewayCalls");
                jsonWriter.WriteObjectStart();
                foreach (KeyValuePair<(int, int), int> kvp in this.GatewayRequestsSummary.Value)
                {
                    jsonWriter.WriteFieldName(kvp.Key.ToString());
                    jsonWriter.WriteNumber64Value(kvp.Value);
                }
                jsonWriter.WriteObjectEnd();
            }

            jsonWriter.WriteObjectEnd();
        }
    }
}