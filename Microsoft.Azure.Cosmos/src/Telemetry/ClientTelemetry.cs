//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Handler;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Newtonsoft.Json;

    /// <summary>
    /// This class collects and send all the telemetry information.
    /// Multiplying Request Charge and CPU Usages with 1000 at the time of collection to preserve precision of upto 3 decimals. 
    /// Dividing these same values with 1000 during Serialization.
    /// This Class get initiated with the client and get disposed with client.
    /// </summary>
    internal class ClientTelemetry : IDisposable
    {
        private static readonly Uri endpointUrl = ClientTelemetryOptions.GetClientTelemetryEndpoint();
        private static readonly TimeSpan observingWindow = ClientTelemetryOptions.GetScheduledTimeSpan();

        private readonly ClientTelemetryProperties clientTelemetryInfo;

        private readonly DocumentClient documentClient;
        private readonly CosmosHttpClient httpClient;
        private readonly AuthorizationTokenProvider tokenProvider;
        private readonly DiagnosticsHandlerHelper diagnosticsHelper;
        private readonly CancellationTokenSource cancellationTokenSource;

        private string accountConsistency;

        private Task telemetryTask;

        private ConcurrentDictionary<OperationInfo, (IList<long> latency, IList<long> requestcharge)> operationInfoMap 
            = new ConcurrentDictionary<OperationInfo, (IList<long> latency, IList<long> requestcharge)>();

        /// <summary>
        /// Factory method to intiakize telemetry object and start observer task
        /// </summary>
        /// <param name="documentClient"></param>
        /// <param name="userAgent"></param>
        /// <param name="connectionMode"></param>
        /// <param name="authorizationTokenProvider"></param>
        /// <param name="diagnosticsHelper"></param>
        /// <param name="preferredRegions"></param>
        /// <returns>ClientTelemetry</returns>
        public static ClientTelemetry CreateAndStartBackgroundTelemetry(DocumentClient documentClient,
            string userAgent,
            ConnectionMode connectionMode,
            AuthorizationTokenProvider authorizationTokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper,
            IReadOnlyList<string> preferredRegions)
        {
            DefaultTrace.TraceInformation("Initiating telemetry with background task.");

            ClientTelemetry clientTelemetry = new ClientTelemetry(documentClient,
            userAgent,
            connectionMode,
            authorizationTokenProvider,
            diagnosticsHelper,
            preferredRegions);

            clientTelemetry.StartObserverTask();

            return clientTelemetry;
        }

        private ClientTelemetry(
            DocumentClient documentClient,
            string userAgent,
            ConnectionMode connectionMode,
            AuthorizationTokenProvider authorizationTokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper,
            IReadOnlyList<string> preferredRegions)
        {
            this.documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
            this.diagnosticsHelper = diagnosticsHelper ?? throw new ArgumentNullException(nameof(diagnosticsHelper));
            this.tokenProvider = authorizationTokenProvider ?? throw new ArgumentNullException(nameof(authorizationTokenProvider));

            this.clientTelemetryInfo = new ClientTelemetryProperties(
                clientId: Guid.NewGuid().ToString(), 
                processId: System.Diagnostics.Process.GetCurrentProcess().ProcessName, 
                userAgent: userAgent, 
                connectionMode: connectionMode,
                preferredRegions: preferredRegions);

            this.httpClient = documentClient.httpClient;
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        ///  Start telemetry Process which Calculate and Send telemetry Information (never ending task)
        /// </summary>
        private void StartObserverTask()
        {
            this.telemetryTask = Task.Run(this.EnrichAndSendAsync, this.cancellationTokenSource.Token);
        }

        /// <summary>
        /// Task which does below operations , periodically
        ///  1. Set Account information (one time at the time of initialization)
        ///  2. Load VM metedata information (one time at the time of initialization)
        ///  3. Calculate and Send telemetry Information to juno service (never ending task)/// </summary>
        /// <returns>Async Task</returns>
        private async Task EnrichAndSendAsync()
        {
            DefaultTrace.TraceInformation("Telemetry Job Started with Observing window : " + observingWindow);
            try
            {
                while (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    // Load account information if not available, cache is already implemented
                    if (String.IsNullOrEmpty(this.clientTelemetryInfo.GlobalDatabaseAccountName) ||
                        this.accountConsistency == null)
                    {
                        AccountProperties accountProperties = await ClientTelemetryHelper.SetAccountNameAsync(this.documentClient);
                        this.clientTelemetryInfo.GlobalDatabaseAccountName = accountProperties?.Id;
                        this.accountConsistency = accountProperties?.Consistency.DefaultConsistencyLevel.ToString().ToUpper();
                    }

                    // Load host information if not available (it caches the information)
                    AzureVMMetadata azMetadata = await ClientTelemetryHelper.LoadAzureVmMetaDataAsync(this.httpClient);

                    Compute vmInformation = azMetadata?.Compute;
                    if (vmInformation != null)
                    {
                        this.clientTelemetryInfo.ApplicationRegion = vmInformation.Location;
                        this.clientTelemetryInfo.HostEnvInfo = ClientTelemetryOptions.GetHostInformation(vmInformation);
                        //TODO: Set AcceleratingNetwork flag from instance metadata once it is available.
                    }

                    await Task.Delay(observingWindow, this.cancellationTokenSource.Token);

                    // If cancellation is requested after the delay then return from here.
                    if (this.cancellationTokenSource.IsCancellationRequested)
                    {
                        DefaultTrace.TraceInformation("Observer Task Cancelled.");
                        return;
                    }

                    this.RecordSystemUtilization();

                    this.clientTelemetryInfo.DateTimeUtc = DateTime.UtcNow.ToString(ClientTelemetryOptions.DateFormat);

                    ConcurrentDictionary<OperationInfo, (IList<long> latency, IList<long> requestcharge)> operationInfoSnapshot 
                        = Interlocked.Exchange(ref this.operationInfoMap, new ConcurrentDictionary<OperationInfo, (IList<long> latency, IList<long> requestcharge)>());

                    this.clientTelemetryInfo.OperationInfo = ClientTelemetryHelper.ToListWithMetricsInfo(operationInfoSnapshot, this.accountConsistency);

                    await this.SendAsync();
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception in EnrichAndSendAsync() : " + ex.Message);
            }

            DefaultTrace.TraceInformation("Telemetry Job Stopped.");
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
                            long responseSizeInBytes,
                            string containerId,
                            string databaseId,
                            OperationType operationType,
                            ResourceType resourceType,
                            string consistencyLevel,
                            double requestCharge)
        {
            DefaultTrace.TraceVerbose("Collecting Operation data for Telemetry.");

            if (cosmosDiagnostics == null)
            {
                throw new ArgumentNullException(nameof(cosmosDiagnostics));
            }

            TimeSpan diagnosticsClientElapsedTime = cosmosDiagnostics.GetClientElapsedTime();
            if (diagnosticsClientElapsedTime == null || diagnosticsClientElapsedTime.Equals(TimeSpan.Zero))
            {
                DefaultTrace.TraceWarning("Diagnostics Client Elased Time is not Available : " + cosmosDiagnostics.ToString());

                //Don't record these values
                return;
            }

            string regionsContacted = ClientTelemetryHelper.GetContactedRegions(cosmosDiagnostics);
            if (String.IsNullOrEmpty(regionsContacted))
            {
                DefaultTrace.TraceWarning("Diagnostics Region Contacted is not Available : " + cosmosDiagnostics.ToString());
            }

            // Recording Request Latency and Request Charge
            OperationInfo payloadKey = new OperationInfo(regionsContacted: regionsContacted?.ToString(),
                                            responseSizeInBytes: responseSizeInBytes,
                                            consistency: consistencyLevel?.ToUpper(),
                                            databaseName: databaseId,
                                            containerName: containerId,
                                            operation: operationType,
                                            resource: resourceType,
                                            statusCode: (int)statusCode);

            (IList<long> latency, IList<long> requestcharge) = this.operationInfoMap.GetOrAdd(payloadKey, x => (latency: new List<long>(), requestcharge: new List<long>()));
            try
            {
                latency.Add(cosmosDiagnostics.GetClientElapsedTime().Ticks);
            } 
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Latency Recording Failed by Telemetry. Exception : " + ex.Message);
            }

            long requestChargeToRecord = (long)(requestCharge * ClientTelemetryOptions.HistogramPrecisionFactor);
            try
            {
                requestcharge.Add(requestChargeToRecord);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Request Charge Recording Failed by Telemetry. Request Charge Value : " + requestChargeToRecord + "  Exception : " + ex.Message);
            }
        }

        /// <summary>
        /// Record CPU and memory usage which will be sent as part of telemetry information
        /// </summary>
        private void RecordSystemUtilization()
        {
            try
            {
                DefaultTrace.TraceVerbose("Started Recording System Usage for telemetry.");

                SystemUsageHistory systemUsageHistory = this.diagnosticsHelper.GetClientTelemtrySystemHistory();

                if (systemUsageHistory != null )
                {
                    (SystemInfo cpuUsagePayload, SystemInfo memoryUsagePayload) = ClientTelemetryHelper.RecordSystemUsage(systemUsageHistory);
                    if (cpuUsagePayload != null)
                    {
                        this.clientTelemetryInfo.SystemInfo.Add(cpuUsagePayload);
                        DefaultTrace.TraceVerbose("Recorded CPU Usage for telemetry.");
                    }

                    if (memoryUsagePayload != null)
                    {
                        this.clientTelemetryInfo.SystemInfo.Add(memoryUsagePayload);
                        DefaultTrace.TraceVerbose("Recorded Memory Usage for telemetry.");
                    }
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("System Usage Recording Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Task to send telemetry information to configured Juno endpoint. 
        /// If endpoint is not configured then it won't even try to send information. It will just trace an error message.
        /// In any case it resets the telemetry information to collect the latest one.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SendAsync()
        {
            if (endpointUrl == null)
            {
                DefaultTrace.TraceError("Telemetry is enabled but endpoint is not configured");
                return;
            }

            try
            {
                DefaultTrace.TraceInformation("Sending Telemetry Data to " + endpointUrl.AbsoluteUri);

                string json = JsonConvert.SerializeObject(this.clientTelemetryInfo, ClientTelemetryOptions.JsonSerializerSettings);

                using HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = endpointUrl,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                async ValueTask<HttpRequestMessage> CreateRequestMessage()
                {
                    INameValueCollection headersCollection = new NameValueCollectionWrapperFactory().CreateNewNameValueCollection();
                    await this.tokenProvider.AddAuthorizationHeaderAsync(
                            headersCollection: headersCollection,
                            requestAddress: endpointUrl,
                            verb: "POST",
                            tokenType: AuthorizationTokenType.PrimaryMasterKey);

                    foreach (string key in headersCollection.AllKeys())
                    {
                        request.Headers.Add(key, headersCollection[key]);
                    }

                    request.Headers.Add(HttpConstants.HttpHeaders.DatabaseAccountName, this.clientTelemetryInfo.GlobalDatabaseAccountName);
                    String envName = ClientTelemetryOptions.GetEnvironmentName();
                    if (!String.IsNullOrEmpty(envName))
                    {
                        request.Headers.Add(HttpConstants.HttpHeaders.EnvironmentName, envName);
                    }

                    return request;
                }

                using HttpResponseMessage response = await this.httpClient.SendHttpAsync(CreateRequestMessage,
                                                    ResourceType.Telemetry,
                                                    HttpTimeoutPolicyDefault.Instance,
                                                    null,
                                                    this.cancellationTokenSource.Token);

                if (!response.IsSuccessStatusCode)
                {
                    DefaultTrace.TraceError("Juno API response not successful. Status Code : " + response.StatusCode + ", Message : " + response.ReasonPhrase);
                } 
                else
                {
                    DefaultTrace.TraceInformation("Telemetry data sent successfully.");
                }

            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while sending telemetry data : " + ex.Message);
            }
            finally
            {
                // Reset SystemInfo Dictionary for new data.
                this.Reset();
            }
        }

        /// <summary>
        /// Reset all the operation, System Utilization and Cache refresh related collections
        /// </summary>
        private void Reset()
        {
            this.clientTelemetryInfo.SystemInfo.Clear();
        }

        /// <summary>
        /// Dispose of cosmos client.It will get disposed with client so not making it thread safe.
        /// </summary>
        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();

            this.telemetryTask = null;
        }
    }
}
