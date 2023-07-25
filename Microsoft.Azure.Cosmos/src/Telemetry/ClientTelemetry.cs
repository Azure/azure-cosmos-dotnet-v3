//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Handler;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.Settings;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Util;

    /// <summary>
    /// This class collects and send all the telemetry information.
    /// Multiplying Request Charge and CPU Usages with 1000 at the time of collection to preserve precision of upto 3 decimals. 
    /// Dividing these same values with 1000 during Serialization.
    /// This Class get initiated with the client and get disposed with client.
    /// </summary>
    internal class ClientTelemetry
    {
        private static readonly TimeSpan observingWindow = ClientTelemetryOptions.DefaultTimeStampInSeconds;

        private readonly ClientTelemetryProperties clientTelemetryInfo;
        private readonly ClientTelemetryProcessor processor;
        private readonly DiagnosticsHandlerHelper diagnosticsHelper;
        private readonly NetworkDataRecorder networkDataRecorder;
        
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly GlobalEndpointManager globalEndpointManager;

        private readonly Uri endpointUrl;
        
        private Task telemetryTask;

        private ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoMap 
            = new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();

        private ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoMap 
            = new ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram>();

        /// <summary>
        /// Only for Mocking in tests
        /// </summary>
        internal ClientTelemetry()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Factory method to intiakize telemetry object and start observer task
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="httpClient"></param>
        /// <param name="userAgent"></param>
        /// <param name="connectionMode"></param>
        /// <param name="authorizationTokenProvider"></param>
        /// <param name="diagnosticsHelper"></param>
        /// <param name="preferredRegions"></param>
        /// <param name="globalEndpointManager"></param>
        /// <param name="databaseAccountClientConfigs"></param>
        /// <returns>ClientTelemetry</returns>
        public static ClientTelemetry CreateAndStartBackgroundTelemetry(
            string clientId,
            CosmosHttpClient httpClient,
            string userAgent,
            ConnectionMode connectionMode,
            AuthorizationTokenProvider authorizationTokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper,
            IReadOnlyList<string> preferredRegions,
            GlobalEndpointManager globalEndpointManager,
            AccountClientConfigProperties databaseAccountClientConfigs)
        {
            DefaultTrace.TraceInformation("Initiating telemetry with background task.");

            ClientTelemetry clientTelemetry = new ClientTelemetry(
                clientId,
                httpClient,
                userAgent,
                connectionMode,
                authorizationTokenProvider,
                diagnosticsHelper,
                preferredRegions,
                globalEndpointManager,
                databaseAccountClientConfigs);

            clientTelemetry.StartObserverTask();

            return clientTelemetry;
        }

        private ClientTelemetry(
            string clientId,
            CosmosHttpClient httpClient,
            string userAgent,
            ConnectionMode connectionMode,
            AuthorizationTokenProvider authorizationTokenProvider,
            DiagnosticsHandlerHelper diagnosticsHelper,
            IReadOnlyList<string> preferredRegions,
            GlobalEndpointManager globalEndpointManager,
            AccountClientConfigProperties databaseAccountClientConfigs)
        {
            this.diagnosticsHelper = diagnosticsHelper ?? throw new ArgumentNullException(nameof(diagnosticsHelper));
            this.globalEndpointManager = globalEndpointManager;
            
            this.processor = new ClientTelemetryProcessor(httpClient, authorizationTokenProvider);

            this.endpointUrl = new Uri(databaseAccountClientConfigs.ClientTelemetryConfiguration.Endpoint);
            
            this.clientTelemetryInfo = new ClientTelemetryProperties(
                clientId: clientId, 
                processId: HashingExtension.ComputeHash(System.Diagnostics.Process.GetCurrentProcess().ProcessName), 
                userAgent: userAgent, 
                connectionMode: connectionMode,
                preferredRegions: preferredRegions,
                aggregationIntervalInSec: (int)observingWindow.TotalSeconds);

            this.networkDataRecorder = new NetworkDataRecorder();
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
        ///  1. Set Account information (one time during initialization)
        ///  2. Load VM metedata information (one time during initialization)
        ///  3. Calculate and Send telemetry Information to Client Telemetry Service (never ending task)/// </summary>
        /// <returns>Async Task</returns>
        private async Task EnrichAndSendAsync()
        {
            DefaultTrace.TraceInformation("Telemetry Job Started with Observing window : {0}", observingWindow);

            try
            {
                while (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    if (string.IsNullOrEmpty(this.clientTelemetryInfo?.GlobalDatabaseAccountName))
                    {
                        AccountProperties accountProperties = await ClientTelemetryHelper.SetAccountNameAsync(this.globalEndpointManager);
                        this.clientTelemetryInfo.GlobalDatabaseAccountName = accountProperties.Id;
                    }
                  
                    await Task.Delay(observingWindow, this.cancellationTokenSource.Token);

                    this.clientTelemetryInfo.DateTimeUtc = DateTime.UtcNow.ToString(ClientTelemetryOptions.DateFormat);
                    this.clientTelemetryInfo.MachineId = VmMetadataApiHandler.GetMachineId();

                    // Load host information from cache
                    Compute vmInformation = VmMetadataApiHandler.GetMachineInfo();
                    this.clientTelemetryInfo.ApplicationRegion = vmInformation?.Location;
                    this.clientTelemetryInfo.HostEnvInfo = ClientTelemetryOptions.GetHostInformation(vmInformation);
                    
                    this.clientTelemetryInfo.SystemInfo = ClientTelemetryHelper.RecordSystemUtilization(this.diagnosticsHelper,
                        this.clientTelemetryInfo.IsDirectConnectionMode);

                    // Take the copy for further processing i.e. serializing and dividing into chunks
                    ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot
                        = Interlocked.Exchange(ref this.operationInfoMap, new ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>());

                    ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot
                        = Interlocked.Exchange(ref this.cacheRefreshInfoMap, new ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram>());

                    List<RequestInfo> requestInfoSnapshot = this.networkDataRecorder.GetRequests();
                    try
                    {
                        CancellationTokenSource cancellationToken = new CancellationTokenSource(ClientTelemetryOptions.ClientTelemetryProcessorTimeOut);
                        Task processorTask = Task.Run(() => this.processor
                                                                    .ProcessAndSendAsync(
                                                                            endpointUrl: this.endpointUrl,
                                                                            clientTelemetryInfo: this.clientTelemetryInfo,
                                                                            operationInfoSnapshot: operationInfoSnapshot,
                                                                            cacheRefreshInfoSnapshot: cacheRefreshInfoSnapshot,
                                                                            requestInfoSnapshot: requestInfoSnapshot,
                                                                            cancellationToken: cancellationToken.Token), cancellationToken.Token);

                        // Initiating Telemetry Data Processor task which will serialize and send telemetry information to Client Telemetry Service
                        // Not disposing this task. If we dispose a client then, telemetry job(telemetryTask) should stop but processor task(processorTask) should make best effort to finish the job in background.
                        _ = ClientTelemetry.RunProcessorTaskAsync(this.clientTelemetryInfo.DateTimeUtc, processorTask, ClientTelemetryOptions.ClientTelemetryProcessorTimeOut);

                    }
                    catch (Exception ex)
                    {
                        DefaultTrace.TraceError("Exception while initiating processing task : {0} with telemetry date as {1}", ex.Message, this.clientTelemetryInfo.DateTimeUtc);
                    }
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception in EnrichAndSendAsync() : {0}", ex);
            }

            DefaultTrace.TraceInformation("Telemetry Job Stopped.");
        }

        /// <summary>
        /// This Task makes sure, processing task is timing out after 5 minute of timeout
        /// </summary>
        /// <param name="telemetryDate"></param>
        /// <param name="processingTask"></param>
        /// <param name="timeout"></param>
        internal static async Task RunProcessorTaskAsync(string telemetryDate, Task processingTask, TimeSpan timeout)
        {
            using (CancellationTokenSource tokenForDelayTask = new CancellationTokenSource())
            {
                Task delayTask = Task.Delay(timeout, tokenForDelayTask.Token);
                
                Task resultTask = await Task.WhenAny(processingTask, delayTask);
                if (resultTask == delayTask)
                {
                    DefaultTrace.TraceVerbose($"Processor task with date as {telemetryDate} is canceled as it did not finish in {timeout}");
                    // Operation cancelled
                    throw new OperationCanceledException($"Processor task with date as {telemetryDate} is canceled as it did not finish in {timeout}");
                }
                else
                {
                    // Cancel the timer task so that it does not fire
                    tokenForDelayTask.Cancel();
                }
            }
        }

        /// <summary>
        /// Collects Cache Telemetry Information.
        /// </summary>
        public void CollectCacheInfo(CacheTelemetryData data)
        {
            if (string.IsNullOrEmpty(data.cacheRefreshSource))
            {
                throw new ArgumentNullException(nameof(data.cacheRefreshSource));
            }

            DefaultTrace.TraceVerbose($"Collecting cacheRefreshSource {0} data for Telemetry.", data.cacheRefreshSource);

            string regionsContacted = ClientTelemetryHelper.GetContactedRegions(data.regionsContactedList);

            // Recording Request Latency
            CacheRefreshInfo payloadKey = new CacheRefreshInfo(cacheRefreshSource: data.cacheRefreshSource,
                                            regionsContacted: regionsContacted?.ToString(),
                                            responseSizeInBytes: data.responseSizeInBytes,
                                            consistency: data.consistencyLevel,
                                            databaseName: data.databaseId,
                                            containerName: data.containerId,
                                            operation: data.operationType,
                                            resource: data.resourceType,
                                            statusCode: (int)data.statusCode,
                                            subStatusCode: (int)data.subStatusCode);

            LongConcurrentHistogram latency = this.cacheRefreshInfoMap
                    .GetOrAdd(payloadKey, new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                        ClientTelemetryOptions.RequestLatencyMax,
                                                        ClientTelemetryOptions.RequestLatencyPrecision));
            try
            {
                latency.RecordValue(data.requestLatency.Value.Ticks);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Latency Recording Failed by Telemetry. Exception : {0}", ex);
            }
        }

        /// <summary>
        /// Collects Telemetry Information.
        /// </summary>
        public void CollectOperationInfo(OperationTelemetryData data)
        {
            DefaultTrace.TraceVerbose("Collecting Operation data for Telemetry.");

            if (data.cosmosDiagnostics == null)
            {
                throw new ArgumentNullException(nameof(data.cosmosDiagnostics));
            }

            // Record Network/Replica Information
            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(data.trace);
            this.networkDataRecorder.Record(summaryDiagnostics.StoreResponseStatistics.Value, data.databaseId, data.containerId);

            string regionsContacted = ClientTelemetryHelper.GetContactedRegions(data.cosmosDiagnostics.GetContactedRegions());

            // Recording Request Latency and Request Charge
            OperationInfo payloadKey = new OperationInfo(regionsContacted: regionsContacted?.ToString(),
                                            responseSizeInBytes: data.responseSizeInBytes,
                                            consistency: data.consistencyLevel,
                                            databaseName: data.databaseId,
                                            containerName: data.containerId,
                                            operation: data.operationType,
                                            resource: data.resourceType,
                                            statusCode: (int)data.statusCode,
                                            subStatusCode: (int)data.subStatusCode);

            (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge) = this.operationInfoMap
                    .GetOrAdd(payloadKey, x => (latency: new LongConcurrentHistogram(ClientTelemetryOptions.RequestLatencyMin,
                                                        ClientTelemetryOptions.RequestLatencyMax,
                                                        ClientTelemetryOptions.RequestLatencyPrecision),
                            requestcharge: new LongConcurrentHistogram(ClientTelemetryOptions.RequestChargeMin,
                                                        ClientTelemetryOptions.RequestChargeMax,
                                                        ClientTelemetryOptions.RequestChargePrecision)));
            try
            {
                latency.RecordValue(data.cosmosDiagnostics.GetClientElapsedTime().Ticks);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Latency Recording Failed by Telemetry. Exception : {0}", ex);
            }

            long requestChargeToRecord = (long)(data.requestCharge * ClientTelemetryOptions.HistogramPrecisionFactor);
            try
            {
                requestcharge.RecordValue(requestChargeToRecord);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Request Charge Recording Failed by Telemetry. Request Charge Value : {0}  Exception : {1} ", requestChargeToRecord, ex);
            }
        }

        /// <summary>
        /// Dispose of cosmos client.It will get disposed with client so not making it thread safe.
        /// </summary>
        public void Dispose()
        {
            if (!this.cancellationTokenSource.IsCancellationRequested)
            {
                this.cancellationTokenSource.Cancel();
                this.cancellationTokenSource.Dispose();
            }
            this.telemetryTask = null;
        }
    }
}
