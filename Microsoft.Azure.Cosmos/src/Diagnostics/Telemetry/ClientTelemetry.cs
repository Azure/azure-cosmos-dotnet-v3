//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
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
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Handlers.DiagnosticsHandler;

    /// <summary>
    /// This class collects and send all the telemetry information.
    /// </summary>
    internal class ClientTelemetry : IDisposable
    {
        internal readonly CancellationTokenSource CancellationTokenSource;
        private readonly LongConcurrentHistogram cpuHistogram = new LongConcurrentHistogram(1,
                                                        ClientTelemetryOptions.CpuMax,
                                                        ClientTelemetryOptions.CpuPrecision);
        private readonly LongConcurrentHistogram memoryHistogram = new LongConcurrentHistogram(1,
                         ClientTelemetryOptions.MemoryMax,
                         ClientTelemetryOptions.MemoryPrecision);

        internal ClientTelemetryInfo ClientTelemetryInfo;
        internal TimeSpan ClientTelemetrySchedulingInSeconds;
        internal DocumentClient documentClient;
        internal CosmosHttpClient httpClient;
        internal AuthorizationTokenProvider TokenProvider;
        internal DiagnosticsHandlerHelper diagnosticsHelper;

        private bool isDisposed = false;

        private Task telemetryTask;

        /// <summary>
        /// Only for tests
        /// </summary>
        internal ClientTelemetry()
        {
            this.ClientTelemetryInfo = new ClientTelemetryInfo(
                clientId: Guid.NewGuid().ToString(),
                processId: System.Diagnostics.Process.GetCurrentProcess().ProcessName,
                userAgent: "useragent",
                connectionMode: ConnectionMode.Direct,
                acceleratedNetworking: null);
        }

        internal ClientTelemetry(
            DocumentClient documentClient,
            bool? acceleratedNetworking,
            ConnectionPolicy connectionPolicy,
            AuthorizationTokenProvider authorizationTokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper)
        {
            this.documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
            this.diagnosticsHelper = diagnosticsHelper;
            if (connectionPolicy == null)
            {
                throw new ArgumentNullException(nameof(connectionPolicy));
            }

            this.ClientTelemetryInfo = new ClientTelemetryInfo(
                clientId: Guid.NewGuid().ToString(), 
                processId: System.Diagnostics.Process.GetCurrentProcess().ProcessName, 
                userAgent: connectionPolicy.UserAgentContainer.UserAgent, 
                connectionMode: connectionPolicy.ConnectionMode,
                acceleratedNetworking: acceleratedNetworking);

            this.ClientTelemetrySchedulingInSeconds = ClientTelemetryOptions.GetSchedulingInSeconds();
            this.httpClient = documentClient.httpClient;
            this.CancellationTokenSource = new CancellationTokenSource();
            this.TokenProvider = authorizationTokenProvider;
        }

        /// <summary>
        ///  Start telemetry Process which trigger 3 seprate processes
        ///  1. Set Account information (one time at the time of initialization)
        ///  2. Load VM metedata information (one time at the time of initialization)
        ///  3. Calculate and Send telemetry Information (never ending task)
        /// </summary>
        internal void Start()
        {
            this.telemetryTask = Task.Run(this.CalculateAndSendTelemetryInformationAsync);
        }

        /// <summary>
        /// Task to get Account Properties from cache if available otherwise make a network call.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SetAccountNameAsync()
        {
            AccountProperties accountProperties = await this.documentClient.GlobalEndpointManager.GetDatabaseAccountAsync();
            this.ClientTelemetryInfo.GlobalDatabaseAccountName = accountProperties.Id;
        }

        /// <summary>
        /// Task to collect virtual machine metadata information. using instance metedata service API.
        /// ref: https://docs.microsoft.com/en-us/azure/virtual-machines/windows/instance-metadata-service?tabs=windows
        /// Collects only application region and environment information
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task LoadAzureVmMetaDataAsync()
        {
            try
            {
                static ValueTask<HttpRequestMessage> CreateRequestMessage()
                {
                    HttpRequestMessage request = new HttpRequestMessage()
                    {
                        RequestUri = new Uri(ClientTelemetryOptions.GetVmMetadataUrl()),
                        Method = HttpMethod.Get,
                    };
                    request.Headers.Add("Metadata", "true");

                    return new ValueTask<HttpRequestMessage>(request);
                }
                using HttpResponseMessage httpResponseMessage = await this.httpClient
                    .SendHttpAsync(CreateRequestMessage, ResourceType.Unknown, HttpTimeoutPolicyDefault.Instance, null, this.CancellationTokenSource.Token);
                   
                AzureVMMetadata azMetadata = await ClientTelemetryOptions.ProcessResponseAsync(httpResponseMessage);

                this.ClientTelemetryInfo.ApplicationRegion = azMetadata.Location;
                this.ClientTelemetryInfo.HostEnvInfo = ClientTelemetryOptions.GetHostInformation(azMetadata);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception in LoadAzureVmMetaDataAsync() " + ex.Message);
            }
        }

        /// <summary>
        /// Task which collects System Information and send telemetry information to juno service, periodically
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task CalculateAndSendTelemetryInformationAsync()
        {
            if (this.CancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                // Load account information if not available
                if (string.IsNullOrEmpty(this.ClientTelemetryInfo.GlobalDatabaseAccountName))
                {
                    await this.SetAccountNameAsync();
                }

                // Load host information if not available
                if (string.IsNullOrEmpty(this.ClientTelemetryInfo.HostEnvInfo))
                {
                    await this.LoadAzureVmMetaDataAsync();
                }
               
                await Task.Delay(this.ClientTelemetrySchedulingInSeconds, this.CancellationTokenSource.Token);
                this.ClientTelemetryInfo.TimeStamp = DateTime.UtcNow.ToString(ClientTelemetryOptions.DateFormat);
                this.RecordSystemUtilization();
                await this.SendAsync();
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

        /// <summary>
        /// Collects Telemetry Information.
        /// </summary>
        /// <param name="cosmosDiagnostics"></param>
        /// <param name="statusCode"></param>
        /// <param name="responseSizeInBytes"></param>
        /// <param name="containerId"></param>
        /// <param name="databaseId"></param>
        /// <param name="operationType"></param>
        /// <param name="resourceType"></param>
        /// <param name="consistencyLevel"></param>
        /// <param name="requestCharge"></param>
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
            IReadOnlyList<(string regionName, Uri uri)> regionList = cosmosDiagnostics.GetContactedRegions();
            IList<Uri> regionUris = new List<Uri>();
            foreach ((_, Uri uri) in regionList)
            {
                regionUris.Add(uri);
            }
            
            // If consistency level is not mentioned in request then take the sdk/account level
            if (consistencyLevel == null)
            {
                consistencyLevel = (Cosmos.ConsistencyLevel)this.documentClient.ConsistencyLevel;
            }

            // Recordig Request Latency
            ReportPayload latencyPayloadKey = new ReportPayload(regionsContacted: string.Join(",", regionUris),
                                            responseSizeInBytes: responseSizeInBytes,
                                            consistency: consistencyLevel.GetValueOrDefault(),
                                            databaseName: databaseId,
                                            containerName: containerId,
                                            operation: operationType,
                                            resource: resourceType,
                                            statusCode: (int)statusCode,
                                            ClientTelemetryOptions.RequestLatencyName,
                                            ClientTelemetryOptions.RequestLatencyUnit);
            if (!this.ClientTelemetryInfo
                .OperationInfoMap
                .TryGetValue(latencyPayloadKey, out LongConcurrentHistogram latencyHistogram))
            {
                latencyHistogram = new LongConcurrentHistogram(1,
                                                      ClientTelemetryOptions.RequestLatencyMaxMicroSec,
                                                      statusCode.IsSuccess() ?
                                                        ClientTelemetryOptions.RequestLatencySuccessPrecision :
                                                        ClientTelemetryOptions.RequestLatencyFailurePrecision);
                this.ClientTelemetryInfo.OperationInfoMap.TryAdd(latencyPayloadKey, latencyHistogram);
            }
            latencyHistogram.RecordValue((long)cosmosDiagnostics.GetClientElapsedTime().TotalMilliseconds * 1000);

            // Recording Request Charge
            ReportPayload requestChargePayloadKey = new ReportPayload(regionsContacted: string.Join(",", regionUris),
                                                            responseSizeInBytes: responseSizeInBytes,
                                                            consistency: consistencyLevel.GetValueOrDefault(),
                                                            databaseName: databaseId,
                                                            containerName: containerId,
                                                            operation: operationType,
                                                            resource: resourceType,
                                                            statusCode: (int)statusCode,
                                                            ClientTelemetryOptions.RequestChargeName,
                                                            ClientTelemetryOptions.RequestChargeUnit);
            if (!this.ClientTelemetryInfo
                .OperationInfoMap
                .TryGetValue(requestChargePayloadKey, out LongConcurrentHistogram requestChargeHistogram)) 
            {
                requestChargeHistogram = new LongConcurrentHistogram(1,
                                                      ClientTelemetryOptions.RequestChargeMax,
                                                      ClientTelemetryOptions.RequestChargePrecision);
                this.ClientTelemetryInfo.OperationInfoMap.TryAdd(requestChargePayloadKey, requestChargeHistogram);
            }
            requestChargeHistogram.RecordValue((long)requestCharge);
        }

        /// <summary>
        /// Record CPU and memory usage which will be sent as part of telemetry information
        /// </summary>
        private void RecordSystemUtilization()
        {
            try
            {
                Tuple<CpuLoadHistory, MemoryLoadHistory> usages = this.diagnosticsHelper.GetCpuAndMemoryUsage(DiagnosticsHandlerHelper.Telemetrykey);
                
                this.cpuHistogram.Reset();
                this.memoryHistogram.Reset();

                CpuLoadHistory cpuLoadHistory = usages.Item1;
                if (cpuLoadHistory != null)
                {
                    foreach (CpuLoad cpuLoad in cpuLoadHistory.CpuLoad)
                    {
                        this.cpuHistogram.RecordValue((long)cpuLoad.Value);
                    }
                    this.ClientTelemetryInfo.SystemInfo.Add(
                        new MetricInfo(ClientTelemetryOptions.CpuName, ClientTelemetryOptions.CpuUnit)
                        .SetAggregators(this.cpuHistogram));
                }

                MemoryLoadHistory memoryLoadHistory = usages.Item2;
                if (memoryLoadHistory != null)
                {
                    foreach (MemoryLoad memoryLoad in memoryLoadHistory.MemoryLoad)
                    {
                        long memoryLoadInMb = memoryLoad.Value / (1024 * 1024);
                        this.memoryHistogram.RecordValue(memoryLoadInMb);
                    }

                    this.ClientTelemetryInfo.SystemInfo.Add(
                        new MetricInfo(ClientTelemetryOptions.MemoryName, ClientTelemetryOptions.MemoryUnit)
                        .SetAggregators(this.memoryHistogram));
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
            }
           
        }

        /// <summary>
        /// Task to send telemetry information to configured Juno endpoint. 
        /// If endpoint is not configured then it won't even try to send information. It will just trace an error message.
        /// In any case it reset the telemetry information to collect the latest one.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SendAsync()
        {
            string endpointUrl = ClientTelemetryOptions.GetClientTelemetryEndpoint();
            string json = JsonConvert.SerializeObject(this.ClientTelemetryInfo);
            // If endpoint is not configured then do not send telemetry information
            if (string.IsNullOrEmpty(endpointUrl))
            {
                DefaultTrace.TraceError("Telemetry endpoint is not configured");
                //Clean Maps to collect latest information.
                this.Reset();

                return;
            }

            async ValueTask<HttpRequestMessage> CreateRequestMessage()
            {
                Uri endpoint = new Uri(endpointUrl);
                HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = endpoint,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                INameValueCollection headersCollection = new NameValueCollectionWrapperFactory().CreateNewNameValueCollection();
                await this.TokenProvider.AddAuthorizationHeaderAsync(
                       headersCollection,
                       endpoint,
                       "POST",
                       AuthorizationTokenType.PrimaryMasterKey);

                foreach (string key in headersCollection.AllKeys())
                {
                    request.Headers.Add(key, headersCollection[key]);
                }

                request.Headers.Add(HttpConstants.HttpHeaders.DatabaseAccountName, this.ClientTelemetryInfo.GlobalDatabaseAccountName);
                String envName = ClientTelemetryOptions.GetEnvironmentName();
                if (!string.IsNullOrEmpty(envName))
                {
                    request.Headers.Add(HttpConstants.HttpHeaders.EnvironmentName, envName);
                }
                return request;
            }

            try 
            {
                HttpResponseMessage response = await this.httpClient.SendHttpAsync(CreateRequestMessage,
                                                    ResourceType.Telemetry,
                                                    HttpTimeoutPolicyDefault.Instance,
                                                    null,
                                                    this.CancellationTokenSource.Token);

                if (!response.IsSuccessStatusCode)
                {
                    DefaultTrace.TraceError(response.ReasonPhrase);
                }
            } 
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
            }
            finally
            {
                //Clean collections to collect latest information.
                this.Reset();
            }

        }

        /// <summary>
        /// Reset all the operation, System Utilization and Cache refresh related collections
        /// </summary>
        internal void Reset()
        {
            this.ClientTelemetryInfo.OperationInfoMap.Clear();
            this.ClientTelemetryInfo.SystemInfo.Clear();
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

                    this.telemetryTask = null;
                }

                this.isDisposed = true;
            }
        }

        internal bool IsTelemetryTaskRunning => this.telemetryTask.Status == TaskStatus.Running;
    }
}
