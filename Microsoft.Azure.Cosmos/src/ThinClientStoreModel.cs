//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// An IStoreModelExtension implementation that routes operations through the ThinClient proxy. 
    /// It applies session tokens, resolves partition key ranges, and delegates requests to ThinClientStoreClient.
    /// </summary>
    internal class ThinClientStoreModel : GatewayStoreModel
    {
        /// <summary>
        /// An instance of <see cref="CancellationTokenSource"/> used to cancel the background connection initialization task.
        /// </summary>
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        /// <summary>
        /// A readonly integer containing the partition failback refresh interval in seconds. The default value is 5 minutes.
        /// </summary>
        private readonly int backgroundFailbackTimeIntervalInSeconds = ConfigurationManager.GetStalePartitionUnavailabilityRefreshIntervalInSeconds(300);

        private readonly int requestFailureCounterThreshold = ConfigurationManager.GetCircuitBreakerConsecutiveFailureCountForReads(10);

        private readonly TimeSpan timeoutCounterResetWindowInMinutes = TimeSpan.FromMinutes(1);

        private readonly object timestampLock = new ();

        private readonly object counterLock = new ();

        private GatewayStoreClient storeClient;

        private ThinClientStoreClient thinClientStoreClient;

        private int consecutiveRequestFailureCount;

        private DateTime lastRequestFailureTime;

        /// <summary>
        /// An integer indicating how many times the dispose was invoked.
        /// </summary>
        private int disposeCounter = 0;

        public ThinClientStoreModel(
            GlobalEndpointManager endpointManager,
            GlobalPartitionEndpointManager globalPartitionEndpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient)
            : base(endpointManager,
                  sessionContainer,
                  defaultConsistencyLevel,
                  eventSource,
                  serializerSettings,
                  httpClient,
                  globalPartitionEndpointManager)
        {
            this.thinClientStoreClient = new ThinClientStoreClient(
                httpClient,
                eventSource,
                serializerSettings);
            this.storeClient = this.thinClientStoreClient;
            this.InitiateGlobalFailbackLoop();
        }

        public override async Task<DocumentServiceResponse> ProcessMessageAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default)
        {
            await GatewayStoreModel.ApplySessionTokenAsync(
                request,
                base.defaultConsistencyLevel,
                base.sessionContainer,
                base.partitionKeyRangeCache,
                base.clientCollectionCache,
                base.endpointManager);

            DocumentServiceResponse response;
            try
            {
                Uri physicalAddress = ThinClientStoreClient.IsFeedRequest(request.OperationType) ? base.GetFeedUri(request) : base.GetEntityUri(request);
                if (request.ResourceType.Equals(ResourceType.Document) && base.endpointManager.TryGetLocationForGatewayDiagnostics(
                    request.RequestContext.LocationEndpointToRoute,
                    out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }

                AccountProperties properties = await this.GetDatabaseAccountPropertiesAsync();
                response = await this.storeClient.InvokeAsync(
                    request,
                    request.ResourceType,
                    physicalAddress,
                    this.endpointManager.ResolveThinClientEndpoint(request),
                    properties.Id,
                    base.clientCollectionCache,
                    cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if (exception.StatusCode == HttpStatusCode.ServiceUnavailable
                    || exception.StatusCode == HttpStatusCode.InternalServerError)
                {
                    this.IncrementRequestFailureCounts(
                        currentTime: DateTime.UtcNow);

                    this.SnapshotConsecutiveRequestFailureCount(
                        out int consecutiveRequestFailureCount);

                    if (consecutiveRequestFailureCount == this.requestFailureCounterThreshold)
                    {
                        Interlocked.Exchange(ref this.storeClient, base.gatewayStoreClient);
                    }
                }

                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    await base.CaptureSessionTokenAndHandleSplitAsync(
                        exception.StatusCode,
                        exception.GetSubStatus(),
                        request,
                        exception.Headers);
                }

                throw;
            }

            await this.CaptureSessionTokenAndHandleSplitAsync(
                response.StatusCode,
                response.SubStatusCode,
                request,
                response.Headers);

            return response;
        }

        public void IncrementRequestFailureCounts(
            DateTime currentTime)
        {
            this.SnapshotGlobalFailoverTimestamps(
                out DateTime lastRequestFailureTime);

            if (currentTime - lastRequestFailureTime > this.timeoutCounterResetWindowInMinutes)
            {
                Interlocked.Exchange(ref this.consecutiveRequestFailureCount, 0);
            }

            Interlocked.Increment(ref this.consecutiveRequestFailureCount);
            this.lastRequestFailureTime = currentTime;
        }

        /// <summary>
        /// Helper method to snapshot the last request failure timestamps.
        /// </summary>
        /// <param name="lastRequestFailureTime">A <see cref="DateTime"/> field containing th e last send attempt time.</param>
        public void SnapshotGlobalFailoverTimestamps(
            out DateTime lastRequestFailureTime)
        {
            Debug.Assert(!Monitor.IsEntered(this.timestampLock));
            lock (this.timestampLock)
            {
                lastRequestFailureTime = this.lastRequestFailureTime;
            }
        }

        public void SnapshotConsecutiveRequestFailureCount(
            out int consecutiveRequestFailureCount)
        {
            Debug.Assert(!Monitor.IsEntered(this.counterLock));
            lock (this.counterLock)
            {
                consecutiveRequestFailureCount = this.consecutiveRequestFailureCount;
            }
        }

        /// <summary>
        /// This method that will run a continious loop with a delay of one minute to refresh the connection to the failed backend replicas.
        /// The loop will break, when a cancellation is requested.
        /// Note that the refresh interval can configured by the end user using the environment variable:
        /// AZURE_COSMOS_PPCB_STALE_PARTITION_UNAVAILABILITY_REFRESH_INTERVAL_IN_SECONDS.
        /// </summary>
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void InitiateGlobalFailbackLoop()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(this.backgroundFailbackTimeIntervalInSeconds),
                        this.cancellationTokenSource.Token);

                    if (this.cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    DefaultTrace.TraceInformation("ThinClientStoreModel: InitiateGlobalFailbackLoop() trying to fail back to thin client mode.");
                    this.FallbackToThinClientMode();
                }
                catch (Exception ex)
                {
                    if (this.cancellationTokenSource.IsCancellationRequested && (ex is OperationCanceledException || ex is ObjectDisposedException))
                    {
                        break;
                    }

                    DefaultTrace.TraceCritical("ThinClientStoreModel: InitiateGlobalFailbackLoop() - Unable fail back to thin client mode. Exception: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Attempts to mark the unhealthy endpoints for a faulty partition to healthy state, un-deterministically. This is done
        /// specifically for the gateway mode to get the faulty partition failed back to the original location.
        /// </summary>
        public void FallbackToThinClientMode()
        {
            if (this.storeClient is not ThinClientStoreClient)
            {
                Interlocked.Exchange(ref this.storeClient, this.thinClientStoreClient);
            }
        }

        private async Task<AccountProperties> GetDatabaseAccountPropertiesAsync()
        {
            try
            {
                AccountProperties accountProperties = await this.endpointManager.GetDatabaseAccountAsync();

                if (accountProperties != null)
                {
                    return accountProperties;
                }

                throw new InvalidOperationException("Failed to retrieve AccountProperties. The response was null.");
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while retrieving database account information: {0}", ex.Message);
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Interlocked.Increment(ref this.disposeCounter) == 1)
                {
                    this.cancellationTokenSource?.Cancel();
                    this.cancellationTokenSource?.Dispose();
                }

                if (this.thinClientStoreClient != null)
                {
                    try
                    {
                        this.thinClientStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception.Message);
                    }
                    this.thinClientStoreClient = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}