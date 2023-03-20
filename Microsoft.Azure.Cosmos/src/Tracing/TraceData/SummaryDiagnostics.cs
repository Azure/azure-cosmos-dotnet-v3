// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    internal struct SummaryDiagnostics
    {
        public SummaryDiagnostics(ITrace trace)
            : this()
        {
            this.DirectRequestsSummary 
                = new Lazy<Dictionary<(int, int), int>>(() => new Dictionary<(int, int), int>());
            this.GatewayRequestsSummary 
                = new Lazy<Dictionary<(int, int), int>>(() => new Dictionary<(int, int), int>());
            this.AllRegionsContacted 
                = new Lazy<HashSet<Uri>>(() => new HashSet<Uri>());
            
            this.CollectSummaryFromTraceTree(trace);
        }

        public Lazy<HashSet<string>> AllRegionsNameContacted { get; private set; } = new Lazy<HashSet<string>>(() => new HashSet<string>());
        public Lazy<HashSet<Uri>> AllRegionsContacted { get; private set; }

        public Lazy<List<StoreResponseStatistics>> StoreResponseStatistics { get; private set; } = new Lazy<List<StoreResponseStatistics>>(() => new List<StoreResponseStatistics>());
        // Count of (StatusCode, SubStatusCode) tuples
        public Lazy<Dictionary<(int statusCode, int subStatusCode), int>> DirectRequestsSummary { get; private set; }

        public Lazy<List<HttpResponseStatistics>> HttpResponseStatistics { get; private set; } = new Lazy<List<HttpResponseStatistics>>(() => new List<HttpResponseStatistics>());
        public Lazy<Dictionary<(int statusCode, int subStatusCode), int>> GatewayRequestsSummary { get; private set; }

        private void CollectSummaryFromTraceTree(ITrace currentTrace)
        {
            foreach (object datums in currentTrace.Data.Values)
            {
                if (datums is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    this.AggregateStatsFromStoreResults(clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList);
                    this.AggregateGatewayStatistics(clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList);
                    this.AggregateRegionsContacted(clientSideRequestStatisticsTraceDatum.RegionsContacted);
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                this.CollectSummaryFromTraceTree(childTrace);
            }
        }

        private void AggregateRegionsContacted(HashSet<(string, Uri)> regionsContacted)
        {
            foreach ((string name, Uri uri) in regionsContacted)
            {
                this.AllRegionsContacted.Value.Add(uri);
                this.AllRegionsNameContacted.Value.Add(name);
            }
        }

        private void AggregateGatewayStatistics(IReadOnlyList<ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics> httpResponseStatisticsList)
        {
            foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpResponseStatistics in httpResponseStatisticsList)
            {
                this.HttpResponseStatistics.Value.Add(httpResponseStatistics);
                
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
                this.StoreResponseStatistics.Value.Add(storeResponseStatistics);
                
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