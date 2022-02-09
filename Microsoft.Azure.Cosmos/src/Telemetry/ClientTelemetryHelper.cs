//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal static class ClientTelemetryHelper
    {
        internal static AzureVMMetadata azMetadata = null;

        private static readonly Uri vmMetadataEndpointUrl = ClientTelemetryOptions.GetVmMetadataUrl();

        /// <summary>
        /// Task to get Account Properties from cache if available otherwise make a network call.
        /// </summary>
        /// <returns>Async Task</returns>
        internal static async Task<AccountProperties> SetAccountNameAsync(DocumentClient documentclient)
        {
            DefaultTrace.TraceVerbose("Getting Account Information for Telemetry.");
            try
            {
                if (documentclient.GlobalEndpointManager != null)
                {
                    return await documentclient.GlobalEndpointManager.GetDatabaseAccountAsync();
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while getting account information in client telemetry : {0}", ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Task to collect virtual machine metadata information. using instance metedata service API.
        /// ref: https://docs.microsoft.com/en-us/azure/virtual-machines/windows/instance-metadata-service?tabs=windows
        /// Collects only application region and environment information
        /// </summary>
        /// <returns>Async Task</returns>
        internal static async Task<AzureVMMetadata> LoadAzureVmMetaDataAsync(CosmosHttpClient httpClient)
        {
            if (azMetadata == null)
            {
                DefaultTrace.TraceVerbose("Getting VM Metadata Information for Telemetry.");
                try
                {
                    static ValueTask<HttpRequestMessage> CreateRequestMessage()
                    {
                        HttpRequestMessage request = new HttpRequestMessage()
                        {
                            RequestUri = vmMetadataEndpointUrl,
                            Method = HttpMethod.Get,
                        };
                        request.Headers.Add("Metadata", "true");

                        return new ValueTask<HttpRequestMessage>(request);
                    }

                    using HttpResponseMessage httpResponseMessage = await httpClient
                        .SendHttpAsync(createRequestMessageAsync: CreateRequestMessage,
                        resourceType: ResourceType.Telemetry,
                        timeoutPolicy: HttpTimeoutPolicyDefault.Instance,
                        clientSideRequestStatistics: null,
                        cancellationToken: new CancellationToken()); // Do not want to cancel the whole process if this call fails

                    azMetadata = await ClientTelemetryOptions.ProcessResponseAsync(httpResponseMessage);

                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError("Exception in LoadAzureVmMetaDataAsync() {0}", ex.Message);
                }
            }

            return azMetadata;
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
        internal static void RecordSystemUsage(
                SystemUsageHistory systemUsageHistory, 
                List<SystemInfo> systemInfoCollection)
        {
            if (systemUsageHistory.Values == null)
            {
                return;
            }

            DefaultTrace.TraceInformation("System Usage recorded by telemetry is : {0}", systemUsageHistory);

            systemInfoCollection.Add(TelemetrySystemUsage.GetCpuInfo(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetMemoryRemainingInfo(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetAvailableThreadsInfo(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetThreadWaitIntervalInMs(systemUsageHistory.Values));
            systemInfoCollection.Add(TelemetrySystemUsage.GetThreadStarvationSignalCount(systemUsageHistory.Values));
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
            DefaultTrace.TraceInformation("Aggregating operation information to list started");

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
        /// Get comma separated list of regions contacted from the diagnostic
        /// </summary>
        /// <param name="cosmosDiagnostics"></param>
        /// <returns>Comma separated region list</returns>
        internal static string GetContactedRegions(CosmosDiagnostics cosmosDiagnostics)
        {
            IReadOnlyList<(string regionName, Uri uri)> regionList = cosmosDiagnostics.GetContactedRegions();

            if (regionList == null || regionList.Count == 0)
            {
                return null;
            }

            if (regionList.Count == 1)
            {
                return regionList[0].regionName;
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
