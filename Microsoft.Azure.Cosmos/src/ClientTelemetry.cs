//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Handler;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Util;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Handlers.DiagnosticsHandler;

    internal class ClientTelemetry : IDisposable
    {
        internal const String RequestKey = "telemetry";

        internal const int OneKbToBytes = 1024;

        internal const int RequestLatencyMaxMicroSec = 300000000;
        internal const int RequestLatencySuccessPrecision = 4;
        internal const int RequestLatencyFailurePrecision = 2;
        internal const string RequestLatencyName = "RequestLatency";
        internal const string RequestLatencyUnit = "MicroSec";

        internal const int RequestChargeMax = 10000;
        internal const int RequestChargePrecision = 2;
        internal const string RequestChargeName = "RequestCharge";
        internal const string RequestChargeUnit = "RU";

        internal const int CpuMax = 100;
        internal const int CpuPrecision = 2;
        internal const String CpuName = "CPU";
        internal const String CpuUnit = "Percentage";

        internal const string VmMetadataUrL = "http://169.254.169.254/metadata/instance?api-version=2020-06-01";

        internal const double Percentile50 = 50.0;
        internal const double Percentile90 = 90.0;
        internal const double Percentile95 = 95.0;
        internal const double Percentile99 = 99.0;
        internal const double Percentile999 = 99.9;
        internal const string DateFormat = "YYYY-MM-ddTHH:mm:ssZ";
        public const string EnvPropsClientTelemetrySchedulingInSeconds = "COSMOS.CLIENT_TELEMETRY_SCHEDULING_IN_SECONDS";
        public const string EnvPropsClientTelemetryEnabled = "COSMOS.CLIENT_TELEMETRY_ENABLED";
        internal const double DefaultTimeStampInSeconds = 600;

        internal static readonly List<ResourceType> AllowedResourceTypes = new List<ResourceType>(new ResourceType[]
        {
            ResourceType.Document
        });

        internal readonly ClientTelemetryInfo ClientTelemetryInfo;
        internal readonly CosmosHttpClient HttpClient;
        internal readonly CancellationTokenSource CancellationTokenSource;

        internal bool IsClientTelemetryEnabled;

        internal double ClientTelemetrySchedulingInSeconds;

        private bool isDisposed = false;

        public ClientTelemetry(bool? acceleratedNetworking,
                               string clientId,
                               string processId,
                               string userAgent,
                               ConnectionMode connectionMode,
                               string globalDatabaseAccountName,
                               CosmosHttpClient httpClient,
                               bool isClientTelemetryEnabled)
        {
            this.ClientTelemetryInfo = new ClientTelemetryInfo(
                clientId: clientId, 
                processId: processId, 
                userAgent: userAgent, 
                connectionMode: connectionMode,
                globalDatabaseAccountName: globalDatabaseAccountName, 
                acceleratedNetworking: acceleratedNetworking);

            this.HttpClient = httpClient;
            this.IsClientTelemetryEnabled = IsTelemetryEnabled(isClientTelemetryEnabled);
            this.ClientTelemetrySchedulingInSeconds = CosmosConfigurationManager
                .GetEnvironmentVariable<double>(
                    EnvPropsClientTelemetrySchedulingInSeconds, 
                    DefaultTimeStampInSeconds);
            this.CancellationTokenSource = new CancellationTokenSource();
        }

        internal static bool IsTelemetryEnabled(bool defaultValue)
        {
            return CosmosConfigurationManager
                .GetEnvironmentVariable<bool>(ClientTelemetry.EnvPropsClientTelemetryEnabled,
                    defaultValue);
        }

        internal async Task LoadAzureVmMetaDataAsync()
        {
            try
            {
                static ValueTask<HttpRequestMessage> CreateRequestMessage()
                {
                    HttpRequestMessage request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(VmMetadataUrL)
                    };
                    request.Headers.Add("Metadata", "true");

                    return new ValueTask<HttpRequestMessage>(request);
                }

                using HttpResponseMessage httpResponseMessage = await this.HttpClient.SendHttpAsync(
                    createRequestMessageAsync: CreateRequestMessage,
                    resourceType: ResourceType.Unknown,
                    timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                    clientSideRequestStatistics: null,
                    cancellationToken: default);
                AzureVMMetadata azMetadata = await ProcessResponseAsync(httpResponseMessage);

                this.ClientTelemetryInfo.ApplicationRegion = azMetadata.Location;
                this.ClientTelemetryInfo.HostEnvInfo = String.Concat(azMetadata.OSType, "|",
                    azMetadata.SKU, "|",
                    azMetadata.VMSize, "|",
                    azMetadata.AzEnvironment);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception in LoadAzureVmMetaDataAsync() " + ex.Message);
            }
        }

        internal static async Task<AzureVMMetadata> ProcessResponseAsync(HttpResponseMessage httpResponseMessage)
        {
            string jsonVmInfo = await httpResponseMessage.Content.ReadAsStringAsync();
            return JObject.Parse(jsonVmInfo).ToObject<AzureVMMetadata>();
        }

        internal void Collect(CosmosDiagnostics cosmosDiagnostics,
                            HttpStatusCode statusCode,
                            int responseSizeInBytes,
                            string containerId,
                            string databaseId,
                            OperationType operationType,
                            ResourceType resourceType,
                            ConsistencyLevel? consistencyLevel,
                            double requestCharge)
        {
            ReportPayload reportPayloadLatency =
                this.CreateReportPayload(cosmosDiagnostics: cosmosDiagnostics, 
                    statusCode: statusCode, 
                    responseSizeInBytes: responseSizeInBytes, 
                    containerId: containerId, 
                    databaseId: databaseId, 
                    operationType: operationType,
                    resourceType: resourceType, 
                    consistencyLevel: consistencyLevel, 
                    metricsName: RequestLatencyName, 
                    unitName: RequestLatencyUnit);

            this.ClientTelemetryInfo
                .OperationInfoMap
                .TryGetValue(reportPayloadLatency, out LongConcurrentHistogram latencyHistogram);

            latencyHistogram ??= statusCode.IsSuccess()
                ? new LongConcurrentHistogram(1, RequestLatencyMaxMicroSec, RequestLatencySuccessPrecision)
                : new LongConcurrentHistogram(1, RequestLatencyMaxMicroSec, RequestLatencyFailurePrecision);
            latencyHistogram.RecordValue((long)cosmosDiagnostics.GetClientElapsedTime().TotalMilliseconds * 1000);
            this.ClientTelemetryInfo.OperationInfoMap[reportPayloadLatency] = latencyHistogram;

            ReportPayload reportPayloadRequestCharge =
               this.CreateReportPayload(cosmosDiagnostics: cosmosDiagnostics, 
                   statusCode: statusCode, 
                   responseSizeInBytes: responseSizeInBytes, 
                   containerId: containerId, 
                   databaseId: databaseId, 
                   operationType: operationType,
                   resourceType: resourceType, 
                   consistencyLevel: consistencyLevel, 
                   metricsName: RequestChargeName, 
                   unitName: RequestChargeUnit);

            this.ClientTelemetryInfo
                .OperationInfoMap
                .TryGetValue(reportPayloadLatency, out LongConcurrentHistogram requestChargeHistogram);

            requestChargeHistogram ??= new LongConcurrentHistogram(1, RequestChargeMax, RequestChargePrecision);
            requestChargeHistogram.RecordValue((long)requestCharge);
            this.ClientTelemetryInfo.OperationInfoMap[reportPayloadRequestCharge] = requestChargeHistogram;
        }

        internal ReportPayload CreateReportPayload(CosmosDiagnostics cosmosDiagnostics,
                                                  HttpStatusCode statusCode,
                                                  int responseSizeInBytes,
                                                  string containerId,
                                                  string databaseId,
                                                  OperationType operationType,
                                                  ResourceType resourceType,
                                                  ConsistencyLevel? consistencyLevel,
                                                  string metricsName,
                                                  string unitName)
        {
            IReadOnlyList<(string regionName, Uri uri)> regionList = cosmosDiagnostics.GetContactedRegions();
            IList<Uri> regionUris = new List<Uri>();
            foreach ((_, Uri uri) in regionList)
            {
                regionUris.Add(uri);
            }

            ReportPayload reportPayload = new ReportPayload(metricsName, unitName)
            {
                RegionsContacted = string.Join(",", regionUris),
                Consistency = consistencyLevel.GetValueOrDefault(),
                DatabaseName = databaseId,
                ContainerName = containerId,
                Operation = operationType,
                Resource = resourceType,
                StatusCode = (int)statusCode
            };

            if (responseSizeInBytes != 0)
            {
                reportPayload.GreaterThan1Kb = responseSizeInBytes > OneKbToBytes;
            }

            return reportPayload;
        }

        internal async Task CalculateAndSendTelemetryInformationAsync()
        {
            if (this.CancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
           
            if (this.IsClientTelemetryEnabled)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(this.ClientTelemetrySchedulingInSeconds),
                        this.CancellationTokenSource.Token);

                    this.ClientTelemetryInfo.TimeStamp = DateTime.UtcNow.ToString(DateFormat);

                    this.RecordCpuUtilization();
                    this.CalculateMetrics();
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError("Exception in CalculateAndSendTelemetryInformationAsync() : " + ex.Message);
                }
                finally
                {
                    await this.CalculateAndSendTelemetryInformationAsync();
                }
            }
            else
            {
                DefaultTrace.TraceWarning("CalculateAndSendTelemetryInformationAsync() : Client Telemetry is disabled.");
            }
        }

        private void CalculateMetrics()
        {
            this.FillMetric(this.ClientTelemetryInfo.CacheRefreshInfoMap);
            this.FillMetric(this.ClientTelemetryInfo.OperationInfoMap);
            this.FillMetric(this.ClientTelemetryInfo.SystemInfoMap);
        }

        private void RecordCpuUtilization()
        {
            LongConcurrentHistogram cpuHistogram = new LongConcurrentHistogram(1, CpuMax, CpuPrecision);
            List<double> cpuHistory = DiagnosticsHandlerHelper.Instance.GetCpuDiagnostics();

            foreach (double cpuUtilization in cpuHistory)
            {
                cpuHistogram.RecordValue((long)cpuUtilization);
            }
            ReportPayload cpuReportPayload = new ReportPayload(CpuName, CpuUnit);
            this.ClientTelemetryInfo.SystemInfoMap.Add(cpuReportPayload, cpuHistogram);
        }

        private void FillMetric(IDictionary<ReportPayload, LongConcurrentHistogram> metrics)
        {
            foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in metrics)
            {
                this.FillMetricsInfo(entry.Key, entry.Value);
            }
        }

        private void FillMetricsInfo(ReportPayload payload, LongConcurrentHistogram histogram)
        {
            LongConcurrentHistogram copyHistogram = (LongConcurrentHistogram)histogram.Copy();
            payload.MetricInfo.Count = copyHistogram.TotalCount;
            payload.MetricInfo.Max = copyHistogram.GetMaxValue();
            payload.MetricInfo.Min = copyHistogram.GetMinValue();
            payload.MetricInfo.Mean = copyHistogram.GetMean();
            IDictionary<Double, Double> percentile = new Dictionary<Double, Double>
            {
                { Percentile50,  copyHistogram.GetValueAtPercentile(Percentile50) },
                { Percentile90,  copyHistogram.GetValueAtPercentile(Percentile90) },
                { Percentile95,  copyHistogram.GetValueAtPercentile(Percentile95) },
                { Percentile99,  copyHistogram.GetValueAtPercentile(Percentile99) },
                { Percentile999, copyHistogram.GetValueAtPercentile(Percentile999) }
            };
            payload.MetricInfo.Percentiles = percentile;
        }

        internal async Task InitAsync()
        {
            if (this.IsClientTelemetryEnabled)
            {
                await this.LoadAzureVmMetaDataAsync();
                _ = Task.Run(this.CalculateAndSendTelemetryInformationAsync);
            } 
            else
            {
                DefaultTrace.TraceWarning("Client Telemetry is disabled");
            }
           
        }

        internal void Reset()
        {
            this.ClientTelemetryInfo.OperationInfoMap.Clear();
            this.ClientTelemetryInfo.SystemInfoMap.Clear();
            this.ClientTelemetryInfo.CacheRefreshInfoMap.Clear();

        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        /// <param name="disposing">True if disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing && !this.CancellationTokenSource.IsCancellationRequested)
                {
                    this.CancellationTokenSource.Cancel();
                    this.CancellationTokenSource.Dispose();
                }

                this.isDisposed = true;
            }
        }
    }
}
