//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal class ClientTelemetryHelper
    {
        internal static AzureVMMetadata azMetadata = null;

        private static readonly Uri vmMetadataEndpointUrl = ClientTelemetryOptions.GetVmMetadataUrl();

        /// <summary>
        /// Task to get Account Properties from cache if available otherwise make a network call.
        /// </summary>
        /// <returns>Async Task</returns>
        internal static async Task<AccountProperties> SetAccountNameAsync(DocumentClient documentclient)
        {
            DefaultTrace.TraceInformation("Getting Account Information for Telemetry.");
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
                DefaultTrace.TraceInformation("Getting VM Metadata Information for Telemetry.");
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
        /// Record Memory Usage and return recorded memory metrics
        /// </summary>
        /// <param name="systemUsageRecorder"></param>
        /// <returns>ReportPayload</returns>
        internal static SystemInfo RecordMemoryUsage(CpuAndMemoryUsageRecorder systemUsageRecorder)
        {
            LongConcurrentHistogram memoryHistogram = new LongConcurrentHistogram(1,
                                                            ClientTelemetryOptions.MemoryMax,
                                                            ClientTelemetryOptions.MemoryPrecision);

            MemoryLoadHistory memoryLoadHistory = systemUsageRecorder.MemoryUsage;
            if (memoryLoadHistory == null)
            {
                return null;
            }

            foreach (MemoryLoad memoryLoad in memoryLoadHistory.MemoryLoad)
            {
                long memoryLoadInMb = memoryLoad.Value / ClientTelemetryOptions.BytesToMb;
                memoryHistogram.RecordValue(memoryLoadInMb);
            }

            SystemInfo memoryInfoPayload = new SystemInfo(ClientTelemetryOptions.MemoryName, ClientTelemetryOptions.MemoryUnit);
            memoryInfoPayload.SetAggregators(memoryHistogram);

            return memoryInfoPayload;
        }

        /// <summary>
        /// Record CPU Usage and return recorded Cpu metrics
        /// </summary>
        /// <param name="systemUsageRecorder"></param>
        /// <returns>ReportPayload</returns>
        internal static SystemInfo RecordCpuUsage(CpuAndMemoryUsageRecorder systemUsageRecorder)
        {
            LongConcurrentHistogram cpuHistogram = new LongConcurrentHistogram(1,
                                                        ClientTelemetryOptions.CpuMax,
                                                        ClientTelemetryOptions.CpuPrecision);

            CpuLoadHistory cpuLoadHistory = systemUsageRecorder.CpuUsage;
            if (cpuLoadHistory == null)
            {
                return null;
            }

            foreach (CpuLoad cpuLoad in cpuLoadHistory.CpuLoad)
            {
                float cpuValue = cpuLoad.Value;
                if (!float.IsNaN(cpuValue))
                {
                    cpuHistogram.RecordValue((long)cpuValue);
                }

            }

            SystemInfo cpuInfoPayload = new SystemInfo(ClientTelemetryOptions.CpuName, ClientTelemetryOptions.CpuUnit);
            cpuInfoPayload.SetAggregators(cpuHistogram);

            return cpuInfoPayload;
        }

        /// <summary>
        /// Convert map with operation information to list of operations along with request latency and request charge metrics
        /// </summary>
        /// <param name="metrics"></param>
        /// <returns>Collection of ReportPayload</returns>
        internal static List<OperationInfo> ToListWithMetricsInfo(IDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> metrics)
        {
            DefaultTrace.TraceInformation("Aggregating operation information to list started");

            List<OperationInfo> payloadWithMetricInformation = new List<OperationInfo>();
            foreach (KeyValuePair<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> entry in metrics)
            {
                OperationInfo payloadForLatency = entry.Key;
                payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                payloadForLatency.SetAggregators(entry.Value.latency, ClientTelemetryOptions.AdjustmentFactor);

                payloadWithMetricInformation.Add(payloadForLatency);

                OperationInfo payloadForRequestCharge = payloadForLatency.Copy();
                payloadForRequestCharge.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestChargeName, ClientTelemetryOptions.RequestChargeUnit);
                payloadForRequestCharge.SetAggregators(entry.Value.requestcharge, ClientTelemetryOptions.AdjustmentFactor);

                payloadWithMetricInformation.Add(payloadForRequestCharge);
            }

            DefaultTrace.TraceInformation("Aggregating operation information to list done");

            return payloadWithMetricInformation;
        }

    }
}
