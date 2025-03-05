//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram.Encoding;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class TelemetryToServiceHelper : IDisposable
    {
        private ITelemetryCollector collector = new TelemetryCollectorNoOp();

        internal static TimeSpan DefaultBackgroundRefreshClientConfigTimeInterval 
            = TimeSpan.FromMinutes(10);

        private readonly AuthorizationTokenProvider cosmosAuthorization;
        private readonly CosmosHttpClient httpClient;
        private readonly Uri serviceEnpoint;
        private readonly ConnectionPolicy connectionPolicy;
        private readonly string clientId;
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly CancellationTokenSource cancellationTokenSource;

        private ClientTelemetry clientTelemetry = null;

        private TelemetryToServiceHelper()
        {
            //NoOpConstructor
        }

        private TelemetryToServiceHelper(
             string clientId,
             ConnectionPolicy connectionPolicy,
             AuthorizationTokenProvider cosmosAuthorization,
             CosmosHttpClient httpClient,
             Uri serviceEndpoint,
             GlobalEndpointManager globalEndpointManager,
             CancellationTokenSource cancellationTokenSource)
        {
            this.clientId = clientId;
            this.cosmosAuthorization = cosmosAuthorization;
            this.httpClient = httpClient;
            this.connectionPolicy = connectionPolicy;
            this.serviceEnpoint = serviceEndpoint;
            this.globalEndpointManager = globalEndpointManager;
            this.cancellationTokenSource = cancellationTokenSource;
        }

        public static TelemetryToServiceHelper CreateAndInitializeClientConfigAndTelemetryJob(string clientId,
           ConnectionPolicy connectionPolicy,
           AuthorizationTokenProvider cosmosAuthorization,
           CosmosHttpClient httpClient,
           Uri serviceEndpoint,
           GlobalEndpointManager globalEndpointManager,
           CancellationTokenSource cancellationTokenSource,
           bool faultInjectionClient = false)
        {
#if INTERNAL
            return new TelemetryToServiceHelper();
#else
            if (connectionPolicy.CosmosClientTelemetryOptions.DisableSendingMetricsToService)
            {
                return new TelemetryToServiceHelper();
            }

            TelemetryToServiceHelper helper = new TelemetryToServiceHelper(
                clientId: clientId, 
                connectionPolicy: connectionPolicy, 
                cosmosAuthorization: cosmosAuthorization, 
                httpClient: httpClient,
                serviceEndpoint: serviceEndpoint, 
                globalEndpointManager: globalEndpointManager, 
                cancellationTokenSource: cancellationTokenSource);

            _ = helper.RetrieveConfigAndInitiateTelemetryAsync(faultInjectionClient); // Let it run in backgroud

            return helper;
#endif
        }

        private async Task RetrieveConfigAndInitiateTelemetryAsync(bool faultInjectionClient)
        {
            try
            {
                Uri serviceEndpointWithPath = new Uri(this.serviceEnpoint + Paths.ClientConfigPathSegment);
                while (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    TryCatch<AccountClientConfiguration> databaseAccountClientConfigs = await this.GetDatabaseAccountClientConfigAsync(
                        cosmosAuthorization: this.cosmosAuthorization,
                        httpClient: this.httpClient, 
                        clientConfigEndpoint: serviceEndpointWithPath,
                        faultInjectionClient: faultInjectionClient);

                    if (databaseAccountClientConfigs.Succeeded)
                    {
                        this.InitializeClientTelemetry(
                            clientConfig: databaseAccountClientConfigs.Result);
                    }
                    else if (databaseAccountClientConfigs.Exception is ObjectDisposedException)
                    {
                        DefaultTrace.TraceWarning("Client is being disposed for {0} at {1}", serviceEndpointWithPath, DateTime.UtcNow);
                        break;
                    }
                    else if (!this.cancellationTokenSource.IsCancellationRequested)
                    {
                        DefaultTrace.TraceWarning("Exception while calling client config {0} ", databaseAccountClientConfigs.Exception);
                    }

                    await Task.Delay(
                        delay: TelemetryToServiceHelper.DefaultBackgroundRefreshClientConfigTimeInterval,
                        cancellationToken: this.cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Exception while running client config job: {0}", ex);
            }
        }

        private async Task<TryCatch<AccountClientConfiguration>> GetDatabaseAccountClientConfigAsync(AuthorizationTokenProvider cosmosAuthorization,
            CosmosHttpClient httpClient,
            Uri clientConfigEndpoint,
            bool faultInjectionClient)
        {
            INameValueCollection headers = new RequestNameValueCollection();
            await cosmosAuthorization.AddAuthorizationHeaderAsync(
                headersCollection: headers,
                clientConfigEndpoint,
                HttpConstants.HttpMethods.Get,
                AuthorizationTokenType.PrimaryMasterKey);

            using (ITrace trace = Trace.GetRootTrace("Account Client Config Read", TraceComponent.Transport, TraceLevel.Info))
            {
                try
                {
                    if (faultInjectionClient)
                    {
                        return await this.GetDatabaseAccountClientConfigFaultInjectionHelperAsync(
                            httpClient: httpClient,
                            clientConfigEndpoint: clientConfigEndpoint,
                            headers: headers);
                    }

                    using (HttpResponseMessage responseMessage = await httpClient.GetAsync(
                        uri: clientConfigEndpoint,
                        additionalHeaders: headers,
                        resourceType: ResourceType.DatabaseAccount,
                        timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                        clientSideRequestStatistics: null,
                        cancellationToken: default))
                    {
                        // It means feature flag is off at gateway, then log the exception and retry after defined interval.
                        // If feature flag is OFF at gateway, SDK won't refresh the latest state of the flag.
                        if (responseMessage.StatusCode == System.Net.HttpStatusCode.BadRequest)
                        {
                            string responseFromGateway = await responseMessage.Content.ReadAsStringAsync();
                            return TryCatch<AccountClientConfiguration>.FromException(
                                new InvalidOperationException($"Client Config API is not enabled at compute gateway. Response is {responseFromGateway}"));
                        }

                        using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                        {
                            return TryCatch<AccountClientConfiguration>.FromResult(
                                CosmosResource.FromStream<AccountClientConfiguration>(documentServiceResponse));
                        }
                    }
                }
                catch (Exception ex)
                {
                    return TryCatch<AccountClientConfiguration>.FromException(ex);
                }
            }
        }

        private async Task<TryCatch<AccountClientConfiguration>> GetDatabaseAccountClientConfigFaultInjectionHelperAsync(
            CosmosHttpClient httpClient,
            Uri clientConfigEndpoint,
            INameValueCollection headers)
        {
            using (DocumentServiceRequest documentServiceRequest = DocumentServiceRequest.Create(
                operationType: OperationType.Read,
                resourceType: ResourceType.DatabaseAccount,
                relativePath: clientConfigEndpoint.AbsolutePath,
                headers: headers,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey))
            {
                using (HttpResponseMessage responseMessage = await httpClient.GetAsync(
                        uri: clientConfigEndpoint,
                        additionalHeaders: headers,
                        resourceType: ResourceType.DatabaseAccount,
                        timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                        clientSideRequestStatistics: null,
                        cancellationToken: default,
                        documentServiceRequest: documentServiceRequest))
                {
                    // It means feature flag is off at gateway, then log the exception and retry after defined interval.
                    // If feature flag is OFF at gateway, SDK won't refresh the latest state of the flag.
                    if (responseMessage.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        string responseFromGateway = await responseMessage.Content.ReadAsStringAsync();
                        return TryCatch<AccountClientConfiguration>.FromException(
                            new InvalidOperationException($"Client Config API is not enabled at compute gateway. Response is {responseFromGateway}"));
                    }

                    using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                    {
                        return TryCatch<AccountClientConfiguration>.FromResult(
                            CosmosResource.FromStream<AccountClientConfiguration>(documentServiceResponse));
                    }
                }
            }
        }

        public ITelemetryCollector GetCollector()
        {
            return this.collector;
        }

        public bool IsClientTelemetryJobRunning()
        {
            return this.clientTelemetry != null;
        }

        /// <summary>
        /// Trigger Client Telemetry job when it is enabled and not already running.
        /// </summary>
        private void InitializeClientTelemetry(AccountClientConfiguration clientConfig)
        {
            // If state of the job is same as state of the flag, then no need to do anything.
            if (clientConfig.IsClientTelemetryEnabled() == this.IsClientTelemetryJobRunning()) 
            {
                return;
            }
            
            DiagnosticsHandlerHelper.Refresh(clientConfig.IsClientTelemetryEnabled());

            if (clientConfig.IsClientTelemetryEnabled())
            { 
                try
                {
                    this.clientTelemetry = ClientTelemetry.CreateAndStartBackgroundTelemetry(
                        clientId: this.clientId,
                        httpClient: this.httpClient,
                        userAgent: this.connectionPolicy.UserAgentContainer.BaseUserAgent,
                        connectionMode: this.connectionPolicy.ConnectionMode,
                        authorizationTokenProvider: this.cosmosAuthorization,
                        diagnosticsHelper: DiagnosticsHandlerHelper.GetInstance(),
                        preferredRegions: this.connectionPolicy.PreferredLocations,
                        globalEndpointManager: this.globalEndpointManager,
                        endpointUrl: clientConfig.ClientTelemetryConfiguration.Endpoint);

                    this.collector = new TelemetryCollector(this.clientTelemetry, this.connectionPolicy);

                    DefaultTrace.TraceVerbose("Client Telemetry Enabled.");
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning($"Error While starting Telemetry Job : {0}. Hence disabling Client Telemetry", ex);
                    this.connectionPolicy.CosmosClientTelemetryOptions.DisableSendingMetricsToService = true;
                }
            }
            else
            {
                this.StopClientTelemetry();
                DefaultTrace.TraceVerbose("Client Telemetry Disabled.");    
            }
        }

        public void Dispose()
        {
            this.StopClientTelemetry();
        }

        /// <summary>
        /// Stopping a client telemetry job means now there shouldn't be any valid collector available, Hence switch it to NoOp collector.
        /// Along with it, send a signal to stop client telemetry job.
        /// </summary>
        private void StopClientTelemetry()
        {
            try
            {
                this.collector = new TelemetryCollectorNoOp();

                this.clientTelemetry?.Dispose();
                this.clientTelemetry = null;

                DiagnosticsHandlerHelper.Refresh(isClientTelemetryEnabled: false);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Error While stopping Telemetry Job : {0}", ex);
            }   
        }
    }
}