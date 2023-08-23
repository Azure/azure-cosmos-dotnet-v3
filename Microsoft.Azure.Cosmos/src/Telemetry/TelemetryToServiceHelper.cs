//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
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

        internal static int DefaultBackgroundRefreshClientConfigTimeIntervalInMS = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;

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
           CancellationTokenSource cancellationTokenSource)
        {
#if INTERNAL
            return new TelemetryToServiceHelper();
#else
            if (!connectionPolicy.EnableClientTelemetry)
            {
                return new TelemetryToServiceHelper();
            }

            TelemetryToServiceHelper helper = new TelemetryToServiceHelper(
                clientId, connectionPolicy, cosmosAuthorization, httpClient, serviceEndpoint, globalEndpointManager, cancellationTokenSource);

            _ = helper.RetrieveConfigAndInitiateTelemetryAsync(); // Let it run in backgroud

            return helper;
#endif
        }

        private async Task RetrieveConfigAndInitiateTelemetryAsync()
        {
            try
            {
                Uri serviceEndpointWithPath = new Uri(this.serviceEnpoint + Paths.ClientConfigPathSegment);

                TryCatch<AccountClientConfiguration> databaseAccountClientConfigs = await this.GetDatabaseAccountClientConfigAsync(this.cosmosAuthorization, this.httpClient, serviceEndpointWithPath);
                if (databaseAccountClientConfigs.Succeeded)
                {
                    this.InitializeClientTelemetry(databaseAccountClientConfigs.Result);
                }
                else if (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    DefaultTrace.TraceWarning($"Exception while calling client config " + databaseAccountClientConfigs.Exception.ToString());
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning($"Exception while running client config job " + ex.ToString());
            }
        }

        private async Task<TryCatch<AccountClientConfiguration>> GetDatabaseAccountClientConfigAsync(AuthorizationTokenProvider cosmosAuthorization,
            CosmosHttpClient httpClient,
            Uri clientConfigEndpoint)
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
                    using (HttpResponseMessage responseMessage = await httpClient.GetAsync(
                        uri: clientConfigEndpoint,
                        additionalHeaders: headers,
                        resourceType: ResourceType.DatabaseAccount,
                        timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                        clientSideRequestStatistics: null,
                        cancellationToken: default))
                    using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                    {
                        return TryCatch<AccountClientConfiguration>.FromResult(CosmosResource.FromStream<AccountClientConfiguration>(documentServiceResponse));
                    }
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning($"Exception while calling client config " + ex.StackTrace);
                    return TryCatch<AccountClientConfiguration>.FromException(ex);
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
                    DefaultTrace.TraceWarning($"Error While starting Telemetry Job : {0}. Hence disabling Client Telemetry", ex.Message);
                    this.connectionPolicy.EnableClientTelemetry = false;
                }
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
            this.collector = new TelemetryCollectorNoOp();

            this.clientTelemetry?.Dispose();
            this.clientTelemetry = null;
        }
    }
}