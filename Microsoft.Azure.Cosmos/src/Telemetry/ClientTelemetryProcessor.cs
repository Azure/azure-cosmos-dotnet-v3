//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Cosmos.Util;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Newtonsoft.Json;
    using ResourceType = Documents.ResourceType;

    internal class ClientTelemetryProcessor
    {
        private static readonly Uri endpointUrl = ClientTelemetryOptions.GetClientTelemetryEndpoint();
        
        private readonly CosmosHttpClient httpClient;
        private readonly AuthorizationTokenProvider tokenProvider;
        private readonly DiagnosticsHandlerHelper diagnosticsHelper;
        private readonly CancellationToken cancellationToken;

        private int numberOfFailures = 0;
        
        private ClientTelemetryProcessor(
            CosmosHttpClient httpClient,
            AuthorizationTokenProvider tokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper,
            CancellationToken cancellationToken)
        {
            this.httpClient = httpClient;
            this.tokenProvider = tokenProvider;
            this.diagnosticsHelper = diagnosticsHelper;
            this.cancellationToken = cancellationToken;
        }
        
        public static void Run(
            string accountId,
            CosmosHttpClient httpClient,
            AuthorizationTokenProvider tokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper,
            string clientId,
            string userAgent,
            ConnectionMode connectionMode,
            IReadOnlyList<string> preferredRegions,
            string time,
            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoMap,
            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoMap,
            CancellationToken cancellationToken)
        {
            new ClientTelemetryProcessor(
                httpClient, 
                tokenProvider, 
                diagnosticsHelper,
                cancellationToken)
                .ProcessAndSend(
                    accountId: accountId,
                    clientId: clientId,
                    userAgent: userAgent,
                    connectionMode: connectionMode,
                    preferredRegions: preferredRegions,
                    time: time,
                    operationInfoMap: operationInfoMap,
                    cacheRefreshInfoMap: cacheRefreshInfoMap);
        }
        
        private void ProcessAndSend(
            string accountId,
            string clientId,
            string userAgent,
            ConnectionMode connectionMode,
            IReadOnlyList<string> preferredRegions,
            string time,
            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoMap,
            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoMap)
        {
            _ = Task.Run(async () =>
            {
                // Load host information from cache
                Compute vmInformation = VmMetadataApiHandler.GetMachineInfo();

                ClientTelemetryProperties clientTelemetryInfo = new ClientTelemetryProperties(
                                                                        clientId: clientId,
                                                                        processId: HashingExtension.ComputeHash(System.Diagnostics.Process.GetCurrentProcess().ProcessName),
                                                                        userAgent: userAgent,
                                                                        connectionMode: connectionMode,
                                                                        preferredRegions: preferredRegions,
                                                                        aggregationIntervalInSec: (int)ClientTelemetryOptions.GetScheduledTimeSpan().TotalSeconds)
                {
                    DateTimeUtc = time,
                    MachineId = VmMetadataApiHandler.GetMachineId(),
                    ApplicationRegion = vmInformation?.Location,
                    HostEnvInfo = ClientTelemetryOptions.GetHostInformation(vmInformation),
                    GlobalDatabaseAccountName = accountId,
                    OperationInfo = ClientTelemetryHelper.ToListWithMetricsInfo(operationInfoMap),
                    CacheRefreshInfo = ClientTelemetryHelper.ToListWithMetricsInfo(cacheRefreshInfoMap)
                };

                this.RecordSystemUtilization(clientTelemetryInfo);

                await this.SendAsync(clientTelemetryInfo);
            });

        }

        /// <summary>
        /// Record CPU and memory usage which will be sent as part of telemetry information
        /// </summary>
        private void RecordSystemUtilization(ClientTelemetryProperties clientTelemetryInfo)
        {
            try
            {
                DefaultTrace.TraceVerbose("Started Recording System Usage for telemetry.");

                SystemUsageHistory systemUsageHistory = this.diagnosticsHelper.GetClientTelemetrySystemHistory();

                if (systemUsageHistory != null)
                {
                    ClientTelemetryHelper.RecordSystemUsage(
                        systemUsageHistory: systemUsageHistory,
                        systemInfoCollection: clientTelemetryInfo.SystemInfo,
                        isDirectConnectionMode: clientTelemetryInfo.IsDirectConnectionMode);
                }
                else
                {
                    DefaultTrace.TraceWarning("System Usage History not available");
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("System Usage Recording Error : {0}", ex.Message);
            }
        }

        /// <summary>
        /// Task to send telemetry information to configured Juno endpoint. 
        /// If endpoint is not configured then it won't even try to send information. It will just trace an error message.
        /// In any case it resets the telemetry information to collect the latest one.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SendAsync(ClientTelemetryProperties clientTelemetryInfo)
        {
            if (endpointUrl == null)
            {
                DefaultTrace.TraceError("Telemetry is enabled but endpoint is not configured");
                return;
            }

            try
            {
                DefaultTrace.TraceInformation("Sending Telemetry Data to {0}", endpointUrl.AbsoluteUri);

                string json = JsonConvert.SerializeObject(clientTelemetryInfo, ClientTelemetryOptions.JsonSerializerSettings);

                using HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = endpointUrl,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                async ValueTask<HttpRequestMessage> CreateRequestMessage()
                {
                    INameValueCollection headersCollection = new StoreResponseNameValueCollection();
                    await this.tokenProvider.AddAuthorizationHeaderAsync(
                            headersCollection,
                            endpointUrl,
                            "POST",
                            AuthorizationTokenType.PrimaryMasterKey);

                    foreach (string key in headersCollection.AllKeys())
                    {
                        request.Headers.Add(key, headersCollection[key]);
                    }

                    request.Headers.Add(HttpConstants.HttpHeaders.DatabaseAccountName, clientTelemetryInfo.GlobalDatabaseAccountName);
                    String envName = ClientTelemetryOptions.GetEnvironmentName();
                    if (!String.IsNullOrEmpty(envName))
                    {
                        request.Headers.Add(HttpConstants.HttpHeaders.EnvironmentName, envName);
                    }

                    return request;
                }

                using HttpResponseMessage response = await this.httpClient.SendHttpAsync(CreateRequestMessage,
                                                    ResourceType.Telemetry,
                                                    HttpTimeoutPolicyNoRetry.Instance,
                                                    null,
                                                    this.cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    this.numberOfFailures++;

                    DefaultTrace.TraceError("Juno API response not successful. Status Code : {0},  Message : {1}", response.StatusCode, response.ReasonPhrase);
                }
                else
                {
                    this.numberOfFailures = 0; // Ressetting failure counts on success call.
                    DefaultTrace.TraceInformation("Telemetry data sent successfully.");
                }

            }
            catch (Exception ex)
            {
                this.numberOfFailures++;

                DefaultTrace.TraceError("Exception while sending telemetry data : {0}", ex.Message);
            }
        }

    }
}
