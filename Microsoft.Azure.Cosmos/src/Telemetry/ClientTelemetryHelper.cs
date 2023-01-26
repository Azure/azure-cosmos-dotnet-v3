//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal static class ClientTelemetryHelper
    {
        /// <summary>
        /// Task to get Account Properties from cache if available otherwise make a network call.
        /// </summary>
        /// <returns>Async Task</returns>
        internal static async Task<AccountProperties> SetAccountNameAsync(GlobalEndpointManager globalEndpointManager)
        {
            DefaultTrace.TraceVerbose("Getting Account Information for Telemetry.");
            try
            {
                if (globalEndpointManager != null)
                {
                    return await globalEndpointManager.GetDatabaseAccountAsync();
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while getting account information in client telemetry : {0}", ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Record System Usage and update passed system Info collection. Right now, it collects following metrics
        /// 1) CPU Usage
        /// 2) Memory Remaining
        /// 3) Available Threads
        /// 
        /// </summary>
        /// <param name="systemUsageHistory"></param>
        /// <param name="systemInfoCollection"></param>
        /// <param name="isDirectConnectionMode"></param>
        internal static void RecordSystemUsage(
                SystemUsageHistory systemUsageHistory, 
                List<SystemInfo> systemInfoCollection,
                bool isDirectConnectionMode)
        {
            if (systemUsageHistory.Values == null)
            {
                return;
            }

            DefaultTrace.TraceVerbose("System Usage recorded by telemetry is : {0}", systemUsageHistory);

            systemInfoCollection.Add(TelemetrySystemUsage.GetCpuInfo(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetMemoryRemainingInfo(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetAvailableThreadsInfo(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetThreadWaitIntervalInMs(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetThreadStarvationSignalCount(systemUsageHistory.Values));

            if (isDirectConnectionMode)
            {
                systemInfoCollection.Add(TelemetrySystemUsage.GetTcpConnectionCount(systemUsageHistory.Values));
            }

        }

        /// <summary>
        /// Convert map with operation information to list of operations along with request latency and request charge metrics
        /// </summary>
        /// <param name="metrics"></param>
        /// <returns>Collection of ReportPayload</returns>
        internal static List<OperationInfo> ToListWithMetricsInfo(
                IDictionary<OperationInfo, 
                (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> metrics)
        {
            DefaultTrace.TraceVerbose("Aggregating operation information to list started");

            List<OperationInfo> payloadWithMetricInformation = new List<OperationInfo>();
            foreach (KeyValuePair<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> entry in metrics)
            {
                OperationInfo payloadForLatency = entry.Key;
                payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                payloadForLatency.SetAggregators(entry.Value.latency, ClientTelemetryOptions.TicksToMsFactor);
                
                payloadWithMetricInformation.Add(payloadForLatency);

                OperationInfo payloadForRequestCharge = payloadForLatency.Copy();
                payloadForRequestCharge.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestChargeName, ClientTelemetryOptions.RequestChargeUnit);
                payloadForRequestCharge.SetAggregators(entry.Value.requestcharge, ClientTelemetryOptions.HistogramPrecisionFactor);

                payloadWithMetricInformation.Add(payloadForRequestCharge);
            }

            DefaultTrace.TraceInformation("Aggregating operation information to list done");

            return payloadWithMetricInformation;
        }

        /// <summary>
        /// Convert map with request information to list of operations along with request latency and request charge metrics
        /// </summary>
        /// <param name="metrics"></param>
        /// <returns>Collection of ReportPayload</returns>
        internal static List<RequestInfo> ToListWithMetricsInfo(
                IDictionary<RequestInfo,
                LongConcurrentHistogram> metrics)
        {
            DefaultTrace.TraceVerbose("Aggregating RequestInfo information to list started");

            List<RequestInfo> payloadWithMetricInformation = new List<RequestInfo>();
            foreach (KeyValuePair<RequestInfo, LongConcurrentHistogram> entry in metrics)
            {
                MetricInfo metricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                metricInfo.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);
                
                RequestInfo payloadForLatency = entry.Key;
                payloadForLatency.Metrics.Add(metricInfo);
              
                payloadWithMetricInformation.Add(payloadForLatency);
            }

            DefaultTrace.TraceInformation("Aggregating RequestInfo information to list done");

            return payloadWithMetricInformation;
        }

        /// <summary>
        /// Convert map with CacheRefreshInfo information to list of operations along with request latency and request charge metrics
        /// </summary>
        /// <param name="metrics"></param>
        /// <returns>Collection of ReportPayload</returns>
        internal static List<CacheRefreshInfo> ToListWithMetricsInfo(IDictionary<CacheRefreshInfo, LongConcurrentHistogram> metrics)
        {
            DefaultTrace.TraceVerbose("Aggregating CacheRefreshInfo information to list started");

            List<CacheRefreshInfo> payloadWithMetricInformation = new List<CacheRefreshInfo>();
            foreach (KeyValuePair<CacheRefreshInfo, LongConcurrentHistogram> entry in metrics)
            {
                CacheRefreshInfo payloadForLatency = entry.Key;
                payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                payloadForLatency.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                payloadWithMetricInformation.Add(payloadForLatency);
            }

            DefaultTrace.TraceVerbose("Aggregating CacheRefreshInfo information to list done");

            return payloadWithMetricInformation;
        }

        /// <summary>
        /// Get comma separated list of regions contacted from the diagnostic
        /// </summary>
        /// <returns>Comma separated region list</returns>
        internal static string GetContactedRegions(IReadOnlyCollection<(string regionName, Uri uri)> regionList)
        {
            if (regionList == null || regionList.Count == 0)
            {
                return null;
            }

            if (regionList.Count == 1)
            {
                return regionList.ElementAt(0).regionName;
            }
            
            StringBuilder regionsContacted = new StringBuilder();
            foreach ((string name, _) in regionList)
            {
                if (regionsContacted.Length > 0)
                {
                    regionsContacted.Append(",");

                }

                regionsContacted.Append(name);
            }

            return regionsContacted.ToString();
        }

    }
}
