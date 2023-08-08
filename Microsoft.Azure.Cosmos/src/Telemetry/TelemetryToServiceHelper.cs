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
    using Microsoft.Azure.Cosmos.Resource.Settings;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class TelemetryToServiceHelper : IDisposable
    {
        private IClientTelemetryCollectors collector = new TelemetryToServiceCollectorNoOp();

        internal static int DefaultBackgroundRefreshClientConfigTimeIntervalInMS = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;

        private readonly AuthorizationTokenProvider cosmosAuthorization;
        private readonly CosmosHttpClient httpClient;
        private readonly Uri serviceEnpoint;
        private readonly ConnectionPolicy connectionPolicy;
        private readonly string clientId;
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly CancellationTokenSource cancellationTokenSource;

        private Task accountClientConfigTask = null;

        private ClientTelemetryJob clientTelemetry = null;

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
            if (connectionPolicy.DisableClientTelemetryToService)
            {
                return new TelemetryToServiceHelper();
            }

            TelemetryToServiceHelper helper = new TelemetryToServiceHelper(
                clientId, connectionPolicy, cosmosAuthorization, httpClient, serviceEndpoint, globalEndpointManager, cancellationTokenSource);

            helper.Initialize();

            return helper;
#endif
        }

        public IClientTelemetryCollectors GetCollector()
        {
            return this.collector;
        }

        private void Initialize()
        {
            this.accountClientConfigTask = this.RefreshDatabaseAccountClientConfigInternalAsync();
        }

        private async Task RefreshDatabaseAccountClientConfigInternalAsync()
        {
            try
            {
                while (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    Uri serviceEndpointWithPath = new Uri(this.serviceEnpoint + Paths.ClientConfigPathSegment);

                    TryCatch<AccountClientConfigProperties> databaseAccountClientConfigs = await this.GetDatabaseAccountClientConfigAsync(this.cosmosAuthorization, this.httpClient, serviceEndpointWithPath);
                    if (databaseAccountClientConfigs.Succeeded)
                    {
                        this.InitializeClientTelemetry(databaseAccountClientConfigs.Result);
                    }
                    else if (!this.cancellationTokenSource.IsCancellationRequested)
                    {
                        DefaultTrace.TraceWarning($"Exception while calling client config " + databaseAccountClientConfigs.Exception.ToString());
                    }

                    await Task.Delay(DefaultBackgroundRefreshClientConfigTimeIntervalInMS, this.cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning($"Exception while running client config job " + ex.ToString());
            }
        }

        private async Task<TryCatch<AccountClientConfigProperties>> GetDatabaseAccountClientConfigAsync(AuthorizationTokenProvider cosmosAuthorization,
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
                        return TryCatch<AccountClientConfigProperties>.FromResult(CosmosResource.FromStream<AccountClientConfigProperties>(documentServiceResponse));
                    }
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning($"Exception while calling client config " + ex.StackTrace);
                    return TryCatch<AccountClientConfigProperties>.FromException(ex);
                }
            }
        }

        public bool IsClientTelemetryJobRunning()
        {
            return this.clientTelemetry != null;
        }

        /// <summary>
        /// Trigger Client Telemetry job when it is enabled and not already running.
        /// </summary>
        private void InitializeClientTelemetry(AccountClientConfigProperties databaseAccountClientConfigs)
        {
            if (databaseAccountClientConfigs.IsClientTelemetryEnabled())
            {
                if (this.IsClientTelemetryJobRunning())
                {
                    DefaultTrace.TraceVerbose("Client Telemetry Job already running.");

                    return;
                }

                try
                {
                    this.clientTelemetry = ClientTelemetryJob.CreateAndStartBackgroundTelemetry(
                        clientId: this.clientId,
                        httpClient: this.httpClient,
                        userAgent: this.connectionPolicy.UserAgentContainer.BaseUserAgent,
                        connectionMode: this.connectionPolicy.ConnectionMode,
                        authorizationTokenProvider: this.cosmosAuthorization,
                        diagnosticsHelper: DiagnosticsHandlerHelper.Instance,
                        preferredRegions: this.connectionPolicy.PreferredLocations,
                        globalEndpointManager: this.globalEndpointManager,
                        databaseAccountClientConfigs: databaseAccountClientConfigs);

                    this.collector = new TelemetryToServiceCollector(this.clientTelemetry, this.connectionPolicy);

                    DefaultTrace.TraceVerbose("Client Telemetry Enabled.");
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning($"Error While starting Telemetry Job : {ex.Message}");
                }
            }
            else
            {
                if (this.IsClientTelemetryJobRunning())
                {
                    DefaultTrace.TraceInformation("Stopping Client Telemetry Job.");
                    this.StopClientTelemetry();
                }
                else
                {
                    DefaultTrace.TraceInformation("Client Telemetry already Disabled.");
                }
            }
        }

        public void Dispose()
        {
            this.accountClientConfigTask = null;

            this.StopClientTelemetry();
        }

        private void StopClientTelemetry()
        {
            this.collector = new TelemetryToServiceCollectorNoOp();

            this.clientTelemetry?.Dispose();
            this.clientTelemetry = null;
        }
    }
}
