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
    using Microsoft.Azure.Cosmos.Util;
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
                DefaultTrace.TraceError("Exception while getting account information in client telemetry : " + ex.Message);
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
                    DefaultTrace.TraceError("Exception in LoadAzureVmMetaDataAsync() " + ex.Message);
                }
            }

            return azMetadata;
        }

        /// <summary>
        /// Record System Usage and return recorded metrics
        /// </summary>
        /// <param name="systemUsageHistory"></param>
        /// <returns>ReportPayload</returns>
        internal static (SystemInfo cpuInfo, SystemInfo memoryInfo) RecordSystemUsage(SystemUsageHistory systemUsageHistory)
        {
            if (systemUsageHistory.Values == null)
            {
                return (null, null);
            }

            DefaultTrace.TraceInformation("System Usage recorded by telemetry is : " + systemUsageHistory);
            Console.WriteLine("RecordSystemUsage 6.4.2.1: " + GC.GetTotalMemory(true));

            LongConcurrentHistogram cpuHistogram = new LongConcurrentHistogram(ClientTelemetryOptions.CpuMin,
                                            ClientTelemetryOptions.CpuMax,
                                            ClientTelemetryOptions.CpuPrecision);
            Console.WriteLine("RecordSystemUsage 6.4.2.2: " + GC.GetTotalMemory(true));
            LongConcurrentHistogram memoryHistogram = new LongConcurrentHistogram(ClientTelemetryOptions.MemoryMin,
                                                           ClientTelemetryOptions.MemoryMax,
                                                           ClientTelemetryOptions.MemoryPrecision);
            Console.WriteLine("RecordSystemUsage 6.4.2.3: " + GC.GetTotalMemory(true));
            foreach (SystemUsageLoad systemUsage in systemUsageHistory.Values)
            {
                float? cpuValue = systemUsage.CpuUsage;
                if (cpuValue.HasValue && !float.IsNaN(cpuValue.Value))
                {
                    cpuHistogram.RecordValue((long)(cpuValue * ClientTelemetryOptions.HistogramPrecisionFactor));
                }
                Console.WriteLine("RecordSystemUsage 6.4.2.4: " + GC.GetTotalMemory(true));
                long? memoryLoad = systemUsage.MemoryAvailable;
                if (memoryLoad.HasValue)
                {
                    memoryHistogram.RecordValue(memoryLoad.Value);
                }
                Console.WriteLine("RecordSystemUsage 6.4.2.5: " + GC.GetTotalMemory(true));
            }

            SystemInfo memoryInfoPayload = new SystemInfo(ClientTelemetryOptions.MemoryName, ClientTelemetryOptions.MemoryUnit);
            memoryInfoPayload.SetAggregators(memoryHistogram, ClientTelemetryOptions.KbToMbFactor);
            Console.WriteLine("RecordSystemUsage 6.4.2.6: " + GC.GetTotalMemory(true));
            SystemInfo cpuInfoPayload = new SystemInfo(ClientTelemetryOptions.CpuName, ClientTelemetryOptions.CpuUnit);
            cpuInfoPayload.SetAggregators(cpuHistogram, ClientTelemetryOptions.HistogramPrecisionFactor);
            Console.WriteLine("RecordSystemUsage 6.4.2.7: " + GC.GetTotalMemory(true));
            return (cpuInfoPayload, memoryInfoPayload);
        }

        /// <summary>
        /// Convert map with operation information to list of operations along with request latency and request charge metrics
        /// </summary>
        /// <param name="metrics"></param>
        /// <param name="accountConsistency"></param>
        /// <returns>Collection of ReportPayload</returns>
        internal static IList<OperationInfo> ToListWithMetricsInfo(IDictionary<OperationInfo, (IList<long> latency, IList<long> requestcharge)> metrics,
            string accountConsistency)
        {
            DefaultTrace.TraceInformation("Aggregating operation information to list started");
            Console.WriteLine("ToListWithMetricsInfo 6.6.1: " + GC.GetTotalMemory(true));

            LongConcurrentHistogram latencyHist = new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                  ClientTelemetryOptions.RequestLatencyMax,
                                                  ClientTelemetryOptions.RequestLatencyPrecision);
            Console.WriteLine("ToListWithMetricsInfo 6.6.2: " + GC.GetTotalMemory(true));
            LongConcurrentHistogram rcHist = new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                   ClientTelemetryOptions.RequestChargeMax,
                                                   ClientTelemetryOptions.RequestChargePrecision);
            Console.WriteLine("ToListWithMetricsInfo 6.6.3: " + GC.GetTotalMemory(true));

            IList<OperationInfo> payloadWithMetricInformation = new List<OperationInfo>();
            foreach (KeyValuePair<OperationInfo, (IList<long> latency, IList<long> requestcharge)> entry in metrics)
            {
                latencyHist.ResetAndRecordValues(entry.Value.latency);
                rcHist.ResetAndRecordValues(entry.Value.requestcharge);
                Console.WriteLine("ToListWithMetricsInfo 6.6.4: " + GC.GetTotalMemory(true));
                OperationInfo payloadForLatency = entry.Key;

                if (String.IsNullOrEmpty(payloadForLatency.Consistency))
                {
                    payloadForLatency.Consistency = accountConsistency;
                }
                payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit)
                    .SetAggregators(latencyHist, ClientTelemetryOptions.TicksToMsFactor);
                Console.WriteLine("ToListWithMetricsInfo 6.6.5: " + GC.GetTotalMemory(true));
                payloadWithMetricInformation.Add(payloadForLatency);

                OperationInfo payloadForRequestCharge = payloadForLatency.Copy();
                payloadForRequestCharge.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestChargeName, ClientTelemetryOptions.RequestChargeUnit)
                    .SetAggregators(rcHist, ClientTelemetryOptions.HistogramPrecisionFactor);
                Console.WriteLine("ToListWithMetricsInfo 6.6.6: " + GC.GetTotalMemory(true));
                payloadWithMetricInformation.Add(payloadForRequestCharge);
            }

            DefaultTrace.TraceInformation("Aggregating operation information to list done");
            Console.WriteLine("ToListWithMetricsInfo 6.6.7: " + GC.GetTotalMemory(true));
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

            if (regionList.Count == 1)
            {
                return regionList[0].regionName;
            }

            StringBuilder regionsContacted = new StringBuilder();
            foreach ((_, Uri uri) in regionList)
            {
                if (regionsContacted.Length > 0)
                {
                    regionsContacted.Append(",");

                }

                regionsContacted.Append(uri);
            }

            return regionsContacted.ToString();
        }

    }
}
