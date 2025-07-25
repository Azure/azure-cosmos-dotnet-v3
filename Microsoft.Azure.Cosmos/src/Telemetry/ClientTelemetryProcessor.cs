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
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class ClientTelemetryProcessor
    {
        private readonly AuthorizationTokenProvider tokenProvider;
        private readonly CosmosHttpClient httpClient;
            
        internal ClientTelemetryProcessor(CosmosHttpClient httpClient, AuthorizationTokenProvider tokenProvider)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        /// <summary>
        /// It will create Task to process and send client telemetry payload to Client Telemetry Service.
        /// </summary>
        /// <returns>Task</returns>
        internal async Task ProcessAndSendAsync(
            ClientTelemetryProperties clientTelemetryInfo, 
            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharget)> operationInfoSnapshot,
            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot,
            IReadOnlyList<RequestInfo> requestInfoSnapshot,
            string endpointUrl,
            CancellationToken cancellationToken)
        {
            try
            {
                await ClientTelemetryPayloadWriter.SerializedPayloadChunksAsync(
                    properties: clientTelemetryInfo,
                    operationInfoSnapshot: operationInfoSnapshot,
                    cacheRefreshInfoSnapshot: cacheRefreshInfoSnapshot,
                    sampledRequestInfo: requestInfoSnapshot,
                    callback: async (payload) => await this.SendAsync(clientTelemetryInfo.GlobalDatabaseAccountName, payload, endpointUrl, cancellationToken));
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while serializing telemetry payload or sending data to service: {0}", ex.Message);
            }
           
        }
        
        /// <summary>
        /// Task to send telemetry information to configured Juno endpoint. 
        /// If endpoint is not configured then it won't even try to send information. It will just trace an error message.
        /// In any case it resets the telemetry information to collect the latest one.
        /// </summary>
        /// <returns>Async Task</returns>
        private async Task SendAsync(
            string globalDatabaseAccountName, 
            string jsonPayload, 
            string endpointUrl,
            CancellationToken cancellationToken)
        {
            if (endpointUrl == null)
            {
                DefaultTrace.TraceError("Telemetry is enabled but endpoint is not configured");
                return;
            }
            
            DefaultTrace.TraceInformation("Sending Telemetry Data to {0}", endpointUrl);
                
            using HttpRequestMessage request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpointUrl),
                Content = new StringContent(jsonPayload, Encoding.UTF8, RuntimeConstants.MediaTypes.Json)
            };

            async ValueTask<HttpRequestMessage> CreateRequestMessage()
            {
                INameValueCollection headersCollection = new StoreResponseNameValueCollection();
                await this.tokenProvider.AddAuthorizationHeaderAsync(
                        headersCollection: headersCollection,
                        requestAddress: new Uri(endpointUrl),
                        verb: HttpMethod.Post.Method,
                        tokenType: AuthorizationTokenType.PrimaryMasterKey);

                foreach (string key in headersCollection.AllKeys())
                {
                    request.Headers.Add(key, headersCollection[key]);
                }

                request.Headers.Add(HttpConstants.HttpHeaders.DatabaseAccountName, globalDatabaseAccountName);
                string envName = ClientTelemetryOptions.GetEnvironmentName();
                if (!string.IsNullOrEmpty(envName))
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
                DefaultTrace.TraceError("Telemetry Service API response not successful. Status Code : {0},  Message : {1}", response.StatusCode, response.ReasonPhrase);
            }
            else
            {
                DefaultTrace.TraceInformation("Telemetry data sent successfully.");
            }
        }

    }
}
