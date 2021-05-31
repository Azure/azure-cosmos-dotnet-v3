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
    /// This class collect all the telemetry information
    /// </summary>
    internal class ClientTelemetry : IDisposable
    {
        internal readonly CancellationTokenSource CancellationTokenSource;
        internal ClientTelemetryInfo ClientTelemetryInfo;
        internal TimeSpan ClientTelemetrySchedulingInSeconds;
        internal DocumentClient documentClient;
        internal CosmosHttpClient httpClient;
        internal AuthorizationTokenProvider TokenProvider;

        private bool isDisposed = false;
        private Task accountInfoTask;
        private Task vmTask;
        private Task telemetryTask;

        public ClientTelemetry(
            DocumentClient documentClient,
            bool? acceleratedNetworking,
            ConnectionPolicy connectionPolicy,
            AuthorizationTokenProvider authorizationTokenProvider)
        {
            this.documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
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
        ///  1. Set Account information
        ///  2. Load VM metedata information
        ///  3. Calculate and Send telemetry Information (infinite process)
        /// </summary>
        internal void Start()
        {
            this.accountInfoTask = Task.Run(this.SetAccountNameAsync);
            this.vmTask = Task.Run(this.LoadAzureVmMetaDataAsync);

            this.telemetryTask = Task.Run(this.CalculateAndSendTelemetryInformationAsync);
        }

        /// <summary>
        /// Gets Account Properties from cache if available otherwise make a network call
        /// </summary>
        /// <returns>It is a Task</returns>
        internal async Task SetAccountNameAsync()
        {
            AccountProperties accountProperties = await this.documentClient.GlobalEndpointManager.GetDatabaseAccountAsync();
            this.ClientTelemetryInfo.GlobalDatabaseAccountName = accountProperties.Id;
        }

        /// <summary>
        /// It is a separate thread which collects virtual machine metadata information.
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

        private async Task CalculateAndSendTelemetryInformationAsync()
        {
            if (this.CancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(this.ClientTelemetrySchedulingInSeconds, this.CancellationTokenSource.Token);

                this.ClientTelemetryInfo.TimeStamp = DateTime.UtcNow.ToString(ClientTelemetryOptions.DateFormat);

                this.RecordSystemUtilization();
                this.CalculateMetrics();
                string endpointUrl = ClientTelemetryOptions.GetClientTelemetryEndpoint();

                string json = JsonConvert.SerializeObject(this.ClientTelemetryInfo);

                Console.WriteLine("endpointUrl : " + endpointUrl);
                Console.WriteLine("json : " + json);
                //await this.SendAsync();
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
        /// This function is being called on each operation and it collects corresponding telemetry information.
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
            //Console.WriteLine(DateTime.UtcNow + " : Collecting telemetry for : " + operationType);
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
            this.ClientTelemetryInfo
                .OperationInfoMap
                .GetOrAdd(new ReportPayload(regionsContacted: string.Join(",", regionUris),
                                            responseSizeInBytes: responseSizeInBytes,
                                            consistency: consistencyLevel.GetValueOrDefault(),
                                            databaseName: databaseId,
                                            containerName: containerId,
                                            operation: operationType,
                                            resource: resourceType,
                                            statusCode: (int)statusCode, 
                                            ClientTelemetryOptions.RequestLatencyName, 
                                            ClientTelemetryOptions.RequestLatencyUnit), 
                          new LongConcurrentHistogram(1,
                                                      ClientTelemetryOptions.RequestLatencyMaxMicroSec,
                                                      statusCode.IsSuccess() ? 
                                                        ClientTelemetryOptions.RequestLatencySuccessPrecision : 
                                                        ClientTelemetryOptions.RequestLatencyFailurePrecision))
                .RecordValue((long)cosmosDiagnostics.GetClientElapsedTime().TotalMilliseconds * 1000);
            
            // Recording Request Charge
            this.ClientTelemetryInfo
                .OperationInfoMap
                .GetOrAdd(new ReportPayload(regionsContacted: string.Join(",", regionUris),
                                                            responseSizeInBytes: responseSizeInBytes,
                                                            consistency: consistencyLevel.GetValueOrDefault(),
                                                            databaseName: databaseId,
                                                            containerName: containerId,
                                                            operation: operationType,
                                                            resource: resourceType,
                                                            statusCode: (int)statusCode, 
                                                            ClientTelemetryOptions.RequestChargeName, 
                                                            ClientTelemetryOptions.RequestChargeUnit), 
                          new LongConcurrentHistogram(1, 
                                                      ClientTelemetryOptions.RequestChargeMax, 
                                                      ClientTelemetryOptions.RequestChargePrecision))
                .RecordValue((long)requestCharge);
        }

        private void RecordSystemUtilization()
        {
            Tuple<CpuLoadHistory, MemoryLoadHistory> usages 
                = DiagnosticsHandlerHelper.Instance.GetCpuAndMemoryUsage(DiagnosticsHandlerHelper.Telemetrykey);

            CpuLoadHistory cpuLoadHistory = usages.Item1;
            if (cpuLoadHistory != null)
            {
                LongConcurrentHistogram cpuHistogram = new LongConcurrentHistogram(1,
                                                     ClientTelemetryOptions.CpuMax,
                                                     ClientTelemetryOptions.CpuPrecision);
                foreach (CpuLoad cpuLoad in cpuLoadHistory.CpuLoad)
                {
                    cpuHistogram.RecordValue((long)cpuLoad.Value);
                }

                MetricInfo cpuMetric = new MetricInfo(ClientTelemetryOptions.CpuName, ClientTelemetryOptions.CpuUnit);
                cpuMetric.SetAggregators(cpuHistogram);
                this.ClientTelemetryInfo.SystemInfo.Add(cpuMetric);
            }

            MemoryLoadHistory memoryLoadHistory = usages.Item2;
            if (memoryLoadHistory != null)
            {
                LongConcurrentHistogram memoryHistogram = new LongConcurrentHistogram(1,
                                         ClientTelemetryOptions.MemoryMax,
                                         ClientTelemetryOptions.MemoryPrecision);
                foreach (MemoryLoad memoryLoad in usages.Item2.MemoryLoad)
                {
                    memoryHistogram.RecordValue((long)memoryLoad.Value);
                }

                MetricInfo memoryMetric = new MetricInfo(ClientTelemetryOptions.MemoryName, ClientTelemetryOptions.MemoryUnit);
                memoryMetric.SetAggregators(memoryHistogram);
                this.ClientTelemetryInfo.SystemInfo.Add(memoryMetric);
            }
        }

        private void CalculateMetrics()
        {
            this.FillMetricInformation(this.ClientTelemetryInfo.CacheRefreshInfoMap);
            this.FillMetricInformation(this.ClientTelemetryInfo.OperationInfoMap);
        }

        private void FillMetricInformation(IDictionary<ReportPayload, LongConcurrentHistogram> metrics)
        {
            foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in metrics)
            {
                ReportPayload payload = entry.Key;
                LongConcurrentHistogram histogram = entry.Value;

                payload.MetricInfo.SetAggregators((LongConcurrentHistogram)histogram.Copy());
            }
        }

        private async Task SendAsync()
        {
            string endpointUrl = ClientTelemetryOptions.GetClientTelemetryEndpoint();

            string json = JsonConvert.SerializeObject(this.ClientTelemetryInfo);
            // If endpoint is not configured then do not send telemetry information
            if (string.IsNullOrEmpty(endpointUrl))
            {
                DefaultTrace.TraceError("Telemetry endpoint is not configured");
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

                request.Headers.Add(HttpConstants.HttpHeaders.ContentType, "application/json");
                request.Headers.Add(HttpConstants.HttpHeaders.ContentEncoding, "gzip");
                request.Headers.Add(HttpConstants.HttpHeaders.XDate, headersCollection[HttpConstants.HttpHeaders.XDate]);
                request.Headers.Add(HttpConstants.HttpHeaders.Authorization, headersCollection[HttpConstants.HttpHeaders.Authorization]);
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
                //Clean Maps to collect latest information.
                this.Reset();
            } 
            catch (Exception ex)
            {
                DefaultTrace.TraceError(ex.Message);
            }

        }

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
                    this.vmTask = null;
                    this.accountInfoTask = null;
                }

                this.isDisposed = true;
            }
        }
    }
}
