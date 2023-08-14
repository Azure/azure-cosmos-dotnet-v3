//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;

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

            helper.InitializeClientTelemetry();

            return helper;
#endif
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
        private void InitializeClientTelemetry()
        {
            try
            {
                this.clientTelemetry = ClientTelemetry.CreateAndStartBackgroundTelemetry(
                    clientId: this.clientId,
                    httpClient: this.httpClient,
                    userAgent: this.connectionPolicy.UserAgentContainer.BaseUserAgent,
                    connectionMode: this.connectionPolicy.ConnectionMode,
                    authorizationTokenProvider: this.cosmosAuthorization,
                    diagnosticsHelper: DiagnosticsHandlerHelper.Instance,
                    preferredRegions: this.connectionPolicy.PreferredLocations,
                    globalEndpointManager: this.globalEndpointManager);

                this.collector = new TelemetryCollector(this.clientTelemetry, this.connectionPolicy);

                DefaultTrace.TraceVerbose("Client Telemetry Enabled.");
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning($"Error While starting Telemetry Job : {ex.Message}. Hence disabling Client Telemetry");
                this.connectionPolicy.EnableClientTelemetry = false;
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