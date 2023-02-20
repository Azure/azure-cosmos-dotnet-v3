//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Cosmos.Telemetry.Resolver;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    internal class ClientTelemetryProcessor
    {
        private static readonly Uri endpointUrl = ClientTelemetryOptions.GetClientTelemetryEndpoint();
        
        private readonly AuthorizationTokenProvider tokenProvider;
        private readonly CosmosHttpClient httpClient;

        internal ClientTelemetryProcessor(CosmosHttpClient httpClient, AuthorizationTokenProvider tokenProvider)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        internal Task ProcessAndSendAsync(
            ClientTelemetryProperties clientTelemetryInfo, 
            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot,
            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () => await this.GenerateOptimalSizeOfPayloadAndSendAsync(clientTelemetryInfo, operationInfoSnapshot, cacheRefreshInfoSnapshot, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// If JSON is greater than 2 MB, then 
        ///     1. It sends all the properties containing metrics information in different payloads
        ///     2. If still payload size is greater than 2 MB, it breaks the list of objects into multiple chunks and send it one by one.
        /// else send as it is.
        /// </summary>
        /// <param name="clientTelemetryInfo"></param>
        /// <param name="operationInfoSnapshot"></param>
        /// <param name="cacheRefreshInfoSnapshot"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>void</returns>
        internal async Task GenerateOptimalSizeOfPayloadAndSendAsync(ClientTelemetryProperties clientTelemetryInfo,
            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot,
            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot,
            CancellationToken cancellationToken)
        {
            clientTelemetryInfo.OperationInfo = ClientTelemetryHelper.ToListWithMetricsInfo(operationInfoSnapshot);
            clientTelemetryInfo.CacheRefreshInfo = ClientTelemetryHelper.ToListWithMetricsInfo(cacheRefreshInfoSnapshot);

            JsonSerializerSettings settings = ClientTelemetryOptions.JsonSerializerSettings;
            string json = JsonConvert.SerializeObject(clientTelemetryInfo, settings);

            // If JSON is greater than 2 MB
            if (json.Length > ClientTelemetryOptions.PayloadSizeThreshold)
            {
                foreach (string property in ClientTelemetryOptions.PropertiesContainMetrics)
                {
                    clientTelemetryInfo.OperationInfo = null; //clearing everything
                    clientTelemetryInfo.CacheRefreshInfo = null;

                    if (property == "OperationInfo")
                    {
                        List<Dictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>> pages 
                            = this.GetPages<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>(operationInfoSnapshot, property);

                        foreach (var page in pages)
                        {
                            List<OperationInfo> oplist = ClientTelemetryHelper.ToListWithMetricsInfo(page);
                            clientTelemetryInfo.OperationInfo = oplist;

                            await this.SendAsync(clientTelemetryInfo, cancellationToken);
                        }
                    }
                    else if (property == "CacheRefreshInfo")
                    {
                        List<Dictionary<CacheRefreshInfo, LongConcurrentHistogram>> pages = this.GetPages<CacheRefreshInfo, LongConcurrentHistogram>(cacheRefreshInfoSnapshot, property);
                        foreach (var page in pages)
                        {
                            List<CacheRefreshInfo> crList = ClientTelemetryHelper.ToListWithMetricsInfo(page);
                            clientTelemetryInfo.CacheRefreshInfo = crList;

                            await this.SendAsync(clientTelemetryInfo, cancellationToken);
                        }
                    }
                }
            }
            else
            {
                await this.SendAsync(clientTelemetryInfo.GlobalDatabaseAccountName, json, cancellationToken);
            }
        }

        private List<Dictionary<T, V>> GetPages<T, V>(ConcurrentDictionary<T, V> operationInfoSnapshot, string property)
        {
            var list = operationInfoSnapshot.ToList();
            int totalItems = list.Count();

            int pageSize = ClientTelemetryOptions.PropertiesWithPageSize[property];
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            List<Dictionary<T, V>> pages = new List<Dictionary<T, V>>();
            for (int i = 1; i <= totalPages; i++)
            {
                pages.Add(list.GetRange((i - 1) * pageSize, Math.Min(pageSize, totalItems)) // get current page items
                                         .ToDictionary(k => k.Key, v => v.Value)); // convert to dictionary

                totalItems -= pageSize; // update remaining items
            }

            return pages;
        }

        /// <summary>
        /// Task to send telemetry information to configured Juno endpoint. 
        /// If endpoint is not configured then it won't even try to send information. It will just trace an error message.
        /// In any case it resets the telemetry information to collect the latest one.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SendAsync(ClientTelemetryProperties clientTelemetryInfo, CancellationToken cancellationToken)
        {
            string jsonPayload = JsonConvert.SerializeObject(clientTelemetryInfo);

            await this.SendAsync(clientTelemetryInfo.GlobalDatabaseAccountName, jsonPayload, cancellationToken);
        }
        
        /// <summary>
        /// Task to send telemetry information to configured Juno endpoint. 
        /// If endpoint is not configured then it won't even try to send information. It will just trace an error message.
        /// In any case it resets the telemetry information to collect the latest one.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SendAsync(string globalDatabaseAccountName, string jsonPayload, CancellationToken cancellationToken)
        {
            if (endpointUrl == null)
            {
                DefaultTrace.TraceError("Telemetry is enabled but endpoint is not configured");
                return;
            }

            try
            {
                DefaultTrace.TraceInformation("Sending Telemetry Data to {0}", endpointUrl.AbsoluteUri);
                
                using HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = endpointUrl,
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
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

                    request.Headers.Add(HttpConstants.HttpHeaders.DatabaseAccountName, globalDatabaseAccountName);
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
                                                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    DefaultTrace.TraceError("Juno API response not successful. Status Code : {0},  Message : {1}", response.StatusCode, response.ReasonPhrase);
                    throw new Exception(string.Format("Juno API response not successful. Status Code : {0},  Message : {1}", response.StatusCode, response.ReasonPhrase));
                }
                else
                {
                    DefaultTrace.TraceInformation("Telemetry data sent successfully.");
                }

            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while sending telemetry data : {0}", ex.Message);
                throw;
            }
        }

    }
}
