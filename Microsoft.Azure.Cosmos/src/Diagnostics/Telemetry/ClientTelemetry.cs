//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
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
        private readonly CancellationTokenSource CancellationTokenSource;

        private readonly LongConcurrentHistogram cpuHistogram = new LongConcurrentHistogram(1,
                                                        ClientTelemetryOptions.CpuMax,
                                                        ClientTelemetryOptions.CpuPrecision);
        private readonly LongConcurrentHistogram memoryHistogram = new LongConcurrentHistogram(1,
                         ClientTelemetryOptions.MemoryMax,
                         ClientTelemetryOptions.MemoryPrecision);
        private readonly string EndpointUrl;
        private readonly TimeSpan ClientTelemetryScheduledTimeSpan;

        internal volatile ClientTelemetryInfo ClientTelemetryInfo;

        internal DocumentClient documentClient;
        internal CosmosHttpClient httpClient;
        internal AuthorizationTokenProvider TokenProvider;
        internal DiagnosticsHandlerHelper diagnosticsHelper;

        private bool isDisposed = false;

        private Task telemetryTask;

        internal ClientTelemetry(
            DocumentClient documentClient,
            string userAgent,
            ConnectionMode connectionMode,
            AuthorizationTokenProvider authorizationTokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper)
        {
            this.documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
            this.diagnosticsHelper = diagnosticsHelper ?? throw new ArgumentNullException(nameof(diagnosticsHelper));
            this.TokenProvider = authorizationTokenProvider ?? throw new ArgumentNullException(nameof(authorizationTokenProvider));

            this.ClientTelemetryInfo = new ClientTelemetryInfo(
                clientId: Guid.NewGuid().ToString(), 
                processId: System.Diagnostics.Process.GetCurrentProcess().ProcessName, 
                userAgent: userAgent, 
                connectionMode: connectionMode);

            this.ClientTelemetryScheduledTimeSpan = ClientTelemetryOptions.GetScheduledTimeSpan();
            this.httpClient = documentClient.httpClient;
            this.CancellationTokenSource = new CancellationTokenSource();

            this.EndpointUrl = ClientTelemetryOptions.GetClientTelemetryEndpoint();
        }

        /// <summary>
        ///  Start telemetry Process which Calculate and Send telemetry Information (never ending task)
        /// </summary>
        internal void Start()
        {
            this.telemetryTask = Task.Run(this.CalculateAndSendTelemetryInformationAsync, this.CancellationTokenSource.Token);
        }

        /// <summary>
        /// Task to get Account Properties from cache if available otherwise make a network call.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SetAccountNameAsync()
        {
            try
            {
                if (this.documentClient.GlobalEndpointManager != null)
                {
                    AccountProperties accountProperties = await this.documentClient.GlobalEndpointManager.GetDatabaseAccountAsync();
                    this.ClientTelemetryInfo.GlobalDatabaseAccountName = accountProperties?.Id;
                }
            } 
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while getting account information in client telemetry : " + ex.Message);
            }
           
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
                if (this.CancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

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
                    .SendHttpAsync(CreateRequestMessage, 
                    ResourceType.Telemetry, 
                    HttpTimeoutPolicyDefault.Instance, 
                    null, 
                    this.CancellationTokenSource.Token);
                   
                AzureVMMetadata azMetadata = await ClientTelemetryOptions.ProcessResponseAsync(httpResponseMessage);

                this.ClientTelemetryInfo.ApplicationRegion = azMetadata?.Location;
                this.ClientTelemetryInfo.HostEnvInfo = ClientTelemetryOptions.GetHostInformation(azMetadata);
                //TODO: Set AcceleratingNetwork flag from instance metadata once it is available.
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception in LoadAzureVmMetaDataAsync() " + ex.Message);
            }
        }

        /// <summary>
        /// Task which does below operations , periodically
        ///  1. Set Account information (one time at the time of initialization)
        ///  2. Load VM metedata information (one time at the time of initialization)
        ///  3. Calculate and Send telemetry Information to juno service (never ending task)/// </summary>
        /// <returns>Async Task</returns>
        private async Task CalculateAndSendTelemetryInformationAsync()
        {
            Console.WriteLine("Background task started");
            try
            {
                while (!this.CancellationTokenSource.IsCancellationRequested)
                {
                    // Load account information if not available, cache is already implemented
                    if (string.IsNullOrEmpty(this.ClientTelemetryInfo.GlobalDatabaseAccountName))
                    {
                        await this.SetAccountNameAsync();
                    }

                    // Load host information if not available
                    if (string.IsNullOrEmpty(this.ClientTelemetryInfo.HostEnvInfo))
                    {
                        await this.LoadAzureVmMetaDataAsync();
                    }

                    Console.WriteLine("waiting started (" + this.ClientTelemetryScheduledTimeSpan + "):" + DateTime.UtcNow);
                    await Task.Delay(this.ClientTelemetryScheduledTimeSpan, this.CancellationTokenSource.Token);
                    Console.WriteLine("waiting ended:" + DateTime.UtcNow);

                    this.ClientTelemetryInfo.TimeStamp = DateTime.UtcNow.ToString(ClientTelemetryOptions.DateFormat);

                    Console.WriteLine("Recording System Information started");
                    this.RecordSystemUtilization();
                    Console.WriteLine("Recording System Information ended");

                    Console.WriteLine("sending data to juno");
                    await this.SendAsync();
                }
                Console.WriteLine("Background task ended");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Background task ended with exception " + ex.Message);
                DefaultTrace.TraceError("Exception in CalculateAndSendTelemetryInformationAsync() : " + ex.Message);
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
            Console.WriteLine("I am collecting");
            string regionsContacted = this.GetContactedRegions(cosmosDiagnostics);

            // If consistency level is not mentioned in request then take the sdk/account level
            if (consistencyLevel == null)
            {
                consistencyLevel = (Cosmos.ConsistencyLevel)this.documentClient.ConsistencyLevel;
            }

            // Recording Request Latency and Request Charge
            ReportPayload payloadKey = new ReportPayload(regionsContacted: regionsContacted.ToString(),
                                            responseSizeInBytes: responseSizeInBytes,
                                            consistency: consistencyLevel,
                                            databaseName: databaseId,
                                            containerName: containerId,
                                            operation: operationType,
                                            resource: resourceType,
                                            statusCode: (int)statusCode);

            (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge) = this.ClientTelemetryInfo
                    .OperationInfoMap
                    .GetOrAdd(payloadKey, x => (latency: new LongConcurrentHistogram(1,
                                                        ClientTelemetryOptions.RequestLatencyMaxMicroSec,
                                                        statusCode.IsSuccess() ?
                                                            ClientTelemetryOptions.RequestLatencySuccessPrecision :
                                                            ClientTelemetryOptions.RequestLatencyFailurePrecision),
                         requestcharge: new LongConcurrentHistogram(1,
                                                        ClientTelemetryOptions.RequestChargeMax,
                                                        ClientTelemetryOptions.RequestChargePrecision)));

            latency.RecordValue((long)cosmosDiagnostics.GetClientElapsedTime().TotalMilliseconds * 1000);
            requestcharge.RecordValue((long)requestCharge);

            Console.WriteLine(this.ClientTelemetryInfo.OperationInfo.Count);

            Console.WriteLine("Collection done");
        }

        /// <summary>
        /// Get comma separated list of regions contacted from the diagnostic
        /// </summary>
        /// <param name="cosmosDiagnostics"></param>
        /// <returns>Comma separated region list</returns>
        private string GetContactedRegions(CosmosDiagnostics cosmosDiagnostics)
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

        /// <summary>
        /// Record CPU and memory usage which will be sent as part of telemetry information
        /// </summary>
        private void RecordSystemUtilization()
        {
            try
            {
                CpuAndMemoryUsageRecorder systemUsageRecorder = this.diagnosticsHelper.GetUsageRecorder(DiagnosticsHandlerHelper.Telemetrykey);

                if (systemUsageRecorder != null )
                {
                    CpuLoadHistory cpuLoadHistory = systemUsageRecorder.CpuUsage;
                    if (cpuLoadHistory != null)
                    {
                        foreach (CpuLoad cpuLoad in cpuLoadHistory.CpuLoad)
                        {
                            this.cpuHistogram.RecordValue((long)cpuLoad.Value);
                        }
                        this.ClientTelemetryInfo.SystemInfo.Add(
                            new ReportPayload(ClientTelemetryOptions.CpuName, ClientTelemetryOptions.CpuUnit)
                            .SetAggregators(this.cpuHistogram));
                    }

                    MemoryLoadHistory memoryLoadHistory = systemUsageRecorder.MemoryUsage;
                    if (memoryLoadHistory != null)
                    {
                        foreach (MemoryLoad memoryLoad in memoryLoadHistory.MemoryLoad)
                        {
                            long memoryLoadInMb = memoryLoad.Value / (1024 * 1024);
                            this.memoryHistogram.RecordValue(memoryLoadInMb);
                        }

                        this.ClientTelemetryInfo.SystemInfo.Add(
                            new ReportPayload(ClientTelemetryOptions.MemoryName, ClientTelemetryOptions.MemoryUnit)
                            .SetAggregators(this.memoryHistogram));
                    }
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
            if (this.EndpointUrl != null)
            {
                Console.WriteLine("Start sending data to juno ");

                string json = JsonConvert.SerializeObject(this.ClientTelemetryInfo,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore
                  });
                // Reset everything to have new data
                this.Reset();

                Uri endpoint = new Uri(this.EndpointUrl);
                using HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = endpoint,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                async ValueTask<HttpRequestMessage> CreateRequestMessage()
                {
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
                    using HttpResponseMessage response = await this.httpClient.SendHttpAsync(CreateRequestMessage,
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
            }
            else
            {
                DefaultTrace.TraceError("Telemetry is enabled but endpoint is not configured");
            }
        }

        /// <summary>
        /// Reset all the operation, System Utilization and Cache refresh related collections
        /// </summary>
        internal void Reset()
        {
            this.cpuHistogram.Reset();
            this.memoryHistogram.Reset();
            this.ClientTelemetryInfo.Clear();
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

                    this.Reset();
                }

                this.isDisposed = true;
            }
        }

        internal bool IsTelemetryTaskRunning => this.telemetryTask.Status == TaskStatus.Running;
    }
}
