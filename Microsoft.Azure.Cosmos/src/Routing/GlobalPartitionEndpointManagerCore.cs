//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This class is used to failover single partitions to different regions.
    /// The client retry policy will mark a partition as down. The PartitionKeyRangeToLocationForReadAndWrite
    /// will add an override to the next read region. When the request is retried it will 
    /// override the default location with the new region from the PartitionKeyRangeToLocationForReadAndWrite.
    /// </summary>
    internal sealed class GlobalPartitionEndpointManagerCore : GlobalPartitionEndpointManager, IDisposable
    {
        /// <summary>
        /// A readonly object used as a lock to synchronize the background connection initialization.
        /// </summary>
        private readonly object backgroundConnectionInitLock = new ();

        /// <summary>
        /// An instance of <see cref="IGlobalEndpointManager"/>.
        /// </summary>
        private readonly IGlobalEndpointManager globalEndpointManager;

        /// <summary>
        /// An instance of <see cref="CancellationTokenSource"/> used to cancel the background connection initialization task.
        /// </summary>
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        /// <summary>
        /// A readonly integer containing the partition unavailability duration in seconds, before it can be considered for a refresh by the background
        /// recursive task. The default value is 5 seconds.
        /// </summary>
        private readonly int partitionUnavailabilityDurationInSeconds = ConfigurationManager.GetAllowedPartitionUnavailabilityDurationInSeconds(5);

        /// <summary>
        /// A readonly integer containing the partition failback refresh interval in seconds. The default value is 60 seconds.
        /// </summary>
        private readonly int backgroundConnectionInitTimeIntervalInSeconds = ConfigurationManager.GetStalePartitionUnavailabilityRefreshIntervalInSeconds(60);

        /// <summary>
        /// A readonly boolean flag used to determine if partition level failover is enabled.
        /// </summary>
        private readonly bool isPartitionLevelFailoverEnabled;

        /// <summary>
        /// A readonly boolean flag used to determine if partition level circuit breaker is enabled.
        /// </summary>
        private readonly bool isPartitionLevelCircuitBreakerEnabled;

        /// <summary>
        /// A <see cref="Lazy{T}"/> instance of <see cref="ConcurrentDictionary{K,V}"/> containing the partition key range to failover info mapping.
        /// This mapping is primarily used for writes in a single master account.
        /// </summary>
        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocationForWrite = new (
            () => new ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>());

        /// <summary>
        /// A <see cref="Lazy{T}"/> instance of <see cref="ConcurrentDictionary{K,V}"/> containing the partition key range to failover info mapping.
        /// This mapping is primarily used for reads in a single master account, and both reads and writes in a multi master account.
        /// </summary>
        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocationForReadAndWrite = new (
            () => new ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>());

        /// <summary>
        /// An integer indicating how many times the dispose was invoked.
        /// </summary>
        private int disposeCounter = 0;

        /// <summary>
        /// A boolean flag indicating if the background connection initialization recursive task is active.
        /// </summary>
        private bool isBackgroundConnectionInitActive = false;

        /// <summary>
        /// A callback func delegate used by the background connection refresh recursive task to establish rntbd connections to backend replicas.
        /// </summary>
        private Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task>? backgroundOpenConnectionTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalPartitionEndpointManagerCore"/> class.
        /// </summary>
        /// <param name="globalEndpointManager">An instance of <see cref="GlobalEndpointManager"/>.</param>
        /// <param name="isPartitionLevelFailoverEnabled">A boolean flag indicating if partition level failover is enabled.</param>
        /// <param name="isPartitionLevelCircuitBreakerEnabled">A boolean flag indicating if partition level circuit breaker is enabled.</param>
        public GlobalPartitionEndpointManagerCore(
            IGlobalEndpointManager globalEndpointManager,
            bool isPartitionLevelFailoverEnabled = false,
            bool isPartitionLevelCircuitBreakerEnabled = false)
        {
            this.isPartitionLevelFailoverEnabled = isPartitionLevelFailoverEnabled;
            this.isPartitionLevelCircuitBreakerEnabled = isPartitionLevelCircuitBreakerEnabled;
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
            this.InitializeAndStartCircuitBreakerFailbackBackgroundRefresh();
        }

        /// <inheritdoc/>
        public override void SetBackgroundConnectionPeriodicRefreshTask(
            Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task> backgroundConnectionInitTask)
        {
            this.backgroundOpenConnectionTask = backgroundConnectionInitTask;
        }

        /// <inheritdoc/>
        public override bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request)
        {
            if (!this.IsRequestEligibleForPartitionFailover(
                request,
                shouldValidateFailedLocation: false,
                out PartitionKeyRange? partitionKeyRange,
                out Uri? _))
            {
                return false;
            }

            if (partitionKeyRange == null)
            {
                return false;
            }

            if (this.IsRequestEligibleForPartitionLevelCircuitBreaker(request))
            {
                return this.TryRouteRequestForPartitionLevelOverride(
                    partitionKeyRange,
                    request,
                    this.PartitionKeyRangeToLocationForReadAndWrite);
            }
            else if (this.IsRequestEligibleForPerPartitionAutomaticFailover(request))
            {
                return this.TryRouteRequestForPartitionLevelOverride(
                    partitionKeyRange,
                    request,
                    this.PartitionKeyRangeToLocationForWrite);
            }

            return false;
        }

        /// <inheritdoc/>
        public override bool TryMarkEndpointUnavailableForPartitionKeyRange(
            DocumentServiceRequest request)
        {
            if (!this.IsRequestEligibleForPartitionFailover(
                request,
                shouldValidateFailedLocation: true,
                out PartitionKeyRange? partitionKeyRange,
                out Uri? failedLocation))
            {
                return false;
            }

            if (partitionKeyRange == null || failedLocation == null)
            {
                return false;
            }

            if (this.IsRequestEligibleForPartitionLevelCircuitBreaker(request))
            {
                // For multi master write accounts, since all the regions are treated as write regions, the next locations to fail over
                // will be the preferred read regions that are configured in the application preferred regions in the CosmosClientOptions.
                ReadOnlyCollection<Uri> nextLocations = this.globalEndpointManager.ReadEndpoints;

                return this.TryAddOrUpdatePartitionFailoverInfoAndMoveToNextLocation(
                    partitionKeyRange,
                    failedLocation,
                    nextLocations,
                    request,
                    this.PartitionKeyRangeToLocationForReadAndWrite);
            }
            else if (this.IsRequestEligibleForPerPartitionAutomaticFailover(request))
            {
                // For any single master write accounts, the next locations to fail over will be the read regions configured at the account level.
                ReadOnlyCollection<Uri> nextLocations = this.globalEndpointManager.AccountReadEndpoints;

                return this.TryAddOrUpdatePartitionFailoverInfoAndMoveToNextLocation(
                    partitionKeyRange,
                    failedLocation,
                    nextLocations,
                    request,
                    this.PartitionKeyRangeToLocationForWrite);
            }

            DefaultTrace.TraceInformation("Partition level override was skipped since the request did not met the minimum requirements.");
            return false;
        }

        /// <inheritdoc/>
        public override bool IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(
            DocumentServiceRequest request)
        {
            if (!this.IsRequestEligibleForPartitionFailover(
                request,
                shouldValidateFailedLocation: true,
                out PartitionKeyRange? partitionKeyRange,
                out Uri? failedLocation))
            {
                return false;
            }

            if (partitionKeyRange == null || failedLocation == null)
            {
                return false;
            }

            PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForReadAndWrite.Value.GetOrAdd(
                partitionKeyRange,
                (_) => new PartitionKeyRangeFailoverInfo(
                    request.RequestContext.ResolvedCollectionRid,
                    failedLocation));

            partionFailover.IncrementRequestFailureCounts(
                isReadOnlyRequest: request.IsReadOnlyRequest,
                currentTime: DateTime.UtcNow);

            return partionFailover.CanCircuitBreakerTriggerPartitionFailOver(
                isReadOnlyRequest: request.IsReadOnlyRequest);
        }

        /// <summary>
        /// Determines if a request is eligible for per-partition automatic failover.
        /// A request is eligible if it is a write request, partition level failover is enabled,
        /// and the global endpoint manager cannot use multiple write locations for the request.
        /// </summary>
        /// <param name="request">The document service request to check.</param>
        /// <returns>True if the request is eligible for per-partition automatic failover, otherwise false.</returns>
        public override bool IsRequestEligibleForPerPartitionAutomaticFailover(
            DocumentServiceRequest request)
        {
            return this.isPartitionLevelFailoverEnabled
                && !request.IsReadOnlyRequest
                && !this.globalEndpointManager.CanSupportMultipleWriteLocations(request.ResourceType, request.OperationType);
        }

        /// <summary>
        /// Determines if a request is eligible for partition-level circuit breaker.
        /// This method checks if the request is a read-only request, if partition-level circuit breaker is enabled,
        /// and if the partition key range location cache indicates that the partition can fail over based on the number of request failures.
        /// </summary>
        /// <returns>
        /// True if the read request is eligible for partition-level circuit breaker, otherwise false.
        /// </returns>
        public override bool IsRequestEligibleForPartitionLevelCircuitBreaker(
            DocumentServiceRequest request)
        {
            return this.isPartitionLevelCircuitBreakerEnabled
                && (request.IsReadOnlyRequest
                || (!request.IsReadOnlyRequest && this.globalEndpointManager.CanSupportMultipleWriteLocations(request.ResourceType, request.OperationType)));
        }

        /// <summary>
        /// Disposes the <see cref="GlobalPartitionEndpointManagerCore"/> class.
        /// Usage of the disposeCounter was used to make the operation atomic.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Increment(ref this.disposeCounter) == 1)
            {
                this.cancellationTokenSource?.Cancel();
                this.cancellationTokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Validates if the given request is eligible for partition failover.
        /// </summary>
        /// <param name="request">An instance of the <see cref="DocumentServiceRequest"/>.</param>
        /// <param name="shouldValidateFailedLocation">A boolean flag indicating whether to validate the failed location.</param>
        /// <param name="partitionKeyRange">The resolved <see cref="PartitionKeyRange"/> for the request.</param>
        /// <param name="failedLocation">The failed location <see cref="Uri"/>, if applicable.</param>
        /// <returns>True if the request is valid for partition failover, otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        private bool IsRequestEligibleForPartitionFailover(
            DocumentServiceRequest request,
            bool shouldValidateFailedLocation,
            out PartitionKeyRange? partitionKeyRange,
            out Uri? failedLocation)
        {
            partitionKeyRange = default;
            failedLocation = default;

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.RequestContext == null)
            {
                return false;
            }

            if (!this.CanUsePartitionLevelFailoverLocations(request))
            {
                return false;
            }

            partitionKeyRange = request.RequestContext.ResolvedPartitionKeyRange;
            if (partitionKeyRange == null)
            {
                return false;
            }

            if (shouldValidateFailedLocation)
            {
                failedLocation = request.RequestContext.LocationEndpointToRoute;
                if (failedLocation == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines if partition level failover locations can be used for the given request.
        /// </summary>
        /// <param name="request">An instance of the <see cref="DocumentServiceRequest"/>.</param>
        /// <returns>True if partition level failover locations can be used, otherwise false.</returns>
        private bool CanUsePartitionLevelFailoverLocations(
            DocumentServiceRequest request)
        {
            if (this.globalEndpointManager.ReadEndpoints.Count <= 1)
            {
                return false;
            }

            if (request.ResourceType == ResourceType.Document ||
                (request.ResourceType == ResourceType.StoredProcedure && request.OperationType == OperationType.ExecuteJavaScript))
            {
                // Right now, for single-master only reads are supported for circuit breaker, and writes are supported for automatic.
                // failover. For multi master, both reads and writes are supported. Hence return true for both the cases.
                return true;
            }

            return false;
        }

        /// <summary>
        /// Initialize and start the background connection periodic refresh task.
        /// </summary>
        internal void InitializeAndStartCircuitBreakerFailbackBackgroundRefresh()
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (this.isBackgroundConnectionInitActive)
            {
                return;
            }

            lock (this.backgroundConnectionInitLock)
            {
                if (this.isBackgroundConnectionInitActive)
                {
                    return;
                }

                this.isBackgroundConnectionInitActive = true;
            }

            try
            {
                this.InitiateCircuitBreakerFailbackLoop();
            }
            catch
            {
                this.isBackgroundConnectionInitActive = false;
                throw;
            }
        }

        /// <summary>
        /// This method that will run a continious loop with a delay of one minute to refresh the connection to the failed backend replicas.
        /// The loop will break, when a cancellation is requested.
        /// Note that the refresh interval can configured by the end user using the environment variable:
        /// AZURE_COSMOS_PPCB_STALE_PARTITION_UNAVAILABILITY_REFRESH_INTERVAL_IN_SECONDS.
        /// </summary>
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void InitiateCircuitBreakerFailbackLoop()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(this.backgroundConnectionInitTimeIntervalInSeconds),
                        this.cancellationTokenSource.Token);

                    if (this.cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    DefaultTrace.TraceInformation("GlobalPartitionEndpointManagerCore: InitiateCircuitBreakerFailbackLoop() trying to get address and open connections for failed locations.");
                    await this.TryOpenConnectionToUnhealthyEndpointsAndInitiateFailbackAsync();
                }
                catch (Exception ex)
                {
                    if (this.cancellationTokenSource.IsCancellationRequested && (ex is OperationCanceledException || ex is ObjectDisposedException))
                    {
                        break;
                    }

                    DefaultTrace.TraceCritical("GlobalPartitionEndpointManagerCore: InitiateCircuitBreakerFailbackLoop() - Unable to get address and open connections. Exception: {0}", ex.ToString());
                }
            }
        }

        /// <summary>
        /// Attempts to open connections to unhealthy endpoints and initiates failback if the connections are successful.
        /// This method checks the partition key ranges that have failed locations and tries to re-establish connections
        /// to those locations. If a connection is successfully re-established, it initiates a failback to the original
        /// location for the partition key range.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task TryOpenConnectionToUnhealthyEndpointsAndInitiateFailbackAsync()
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (this.backgroundOpenConnectionTask != null)
            {
                Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>> pkRangeToEndpointMappings = new ();
                foreach (PartitionKeyRange pkRange in this.PartitionKeyRangeToLocationForReadAndWrite.Value.Keys)
                {
                    PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForReadAndWrite.Value[pkRange];

                    partionFailover.SnapshotPartitionFailoverTimestamps(
                        out DateTime firstRequestFailureTime,
                        out DateTime _);

                    if (DateTime.UtcNow - firstRequestFailureTime > TimeSpan.FromSeconds(this.partitionUnavailabilityDurationInSeconds))
                    {
                        // The first failed location would always be the first preferred location.
                        Uri originalFailedLocation = partionFailover.FirstFailedLocation;

                        pkRangeToEndpointMappings.Add(
                            key: pkRange,
                            value: new Tuple<string, Uri, TransportAddressHealthState.HealthStatus>(
                                partionFailover.CollectionRid,
                                originalFailedLocation,
                                TransportAddressHealthState.HealthStatus.Unhealthy));

                    }
                }

                if (pkRangeToEndpointMappings.Count > 0)
                {
                    await this.backgroundOpenConnectionTask(pkRangeToEndpointMappings);

                    foreach (PartitionKeyRange pkRange in pkRangeToEndpointMappings.Keys)
                    {
                        Uri originalFailedLocation = pkRangeToEndpointMappings[pkRange].Item2;
                        TransportAddressHealthState.HealthStatus currentHealthState = pkRangeToEndpointMappings[pkRange].Item3;

                        if (currentHealthState == TransportAddressHealthState.HealthStatus.Connected)
                        {
                            // Initiate Failback to the original failed location.
                            DefaultTrace.TraceInformation($"Initiating Failback to endpoint: {originalFailedLocation}, for partition key range: {pkRange}");
                            this.PartitionKeyRangeToLocationForReadAndWrite.Value.TryRemove(pkRange, out PartitionKeyRangeFailoverInfo _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to route the request to a partition level override location if available.
        /// This method checks if there is a failover location for the given partition key range
        /// and updates the request context to route to that location.
        /// </summary>
        /// <param name="partitionKeyRange">The partition key range for which the request is being routed.</param>
        /// <param name="request">The document service request to be routed.</param>
        /// <param name="partitionKeyRangeToLocationMapping">The mapping of partition key ranges to their failover locations.</param>
        /// <returns>True if the request was successfully routed to a partition level override location, otherwise false.</returns>
        private bool TryRouteRequestForPartitionLevelOverride(
            PartitionKeyRange partitionKeyRange,
            DocumentServiceRequest request,
            Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> partitionKeyRangeToLocationMapping)
        {
            if (partitionKeyRangeToLocationMapping.IsValueCreated
                && partitionKeyRangeToLocationMapping.Value.TryGetValue(
                    partitionKeyRange,
                    out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
            {
                if (this.IsRequestEligibleForPartitionLevelCircuitBreaker(request)
                    && !partitionKeyRangeFailover.CanCircuitBreakerTriggerPartitionFailOver(request.IsReadOnlyRequest))
                {
                    return false;
                }

                string triggeredBy = this.isPartitionLevelFailoverEnabled ? "Automatic Failover" : "Circuit Breaker";
                DefaultTrace.TraceInformation("Attempting to route request for partition level override triggered by {0}, for operation type: {1}. URI: {2}, PartitionKeyRange: {3}",
                    triggeredBy,
                    request.OperationType,
                    partitionKeyRangeFailover.Current,
                    partitionKeyRange.Id);

                request.RequestContext.RouteToLocation(partitionKeyRangeFailover.Current);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to add or update the partition failover information and move to the next available location.
        /// This method checks if the current location for the partition key range has failed and updates the failover
        /// information to route the request to the next available location. If all locations have been tried, it removes
        /// the failover information for the partition key range.
        /// </summary>
        /// <param name="partitionKeyRange">The partition key range for which the failover information is being updated.</param>
        /// <param name="failedLocation">The URI of the failed location.</param>
        /// <param name="nextLocations">A read-only collection of URIs representing the next available locations.</param>
        /// <param name="request">The document service request being routed.</param>
        /// <param name="partitionKeyRangeToLocationMapping">The mapping of partition key ranges to their failover information.</param>
        /// <returns>True if the failover information was successfully updated and the request was routed to a new location, otherwise false.</returns>
        private bool TryAddOrUpdatePartitionFailoverInfoAndMoveToNextLocation(
            PartitionKeyRange partitionKeyRange,
            Uri failedLocation,
            ReadOnlyCollection<Uri> nextLocations,
            DocumentServiceRequest request,
            Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> partitionKeyRangeToLocationMapping)
        {
            string triggeredBy = this.isPartitionLevelFailoverEnabled ? "Automatic Failover" : "Circuit Breaker";
            PartitionKeyRangeFailoverInfo partionFailover = partitionKeyRangeToLocationMapping.Value.GetOrAdd(
                partitionKeyRange,
                (_) => new PartitionKeyRangeFailoverInfo(
                    request.RequestContext.ResolvedCollectionRid,
                    failedLocation));

            // Will return true if it was able to update to a new region
            if (partionFailover.TryMoveNextLocation(
                    locations: nextLocations,
                    failedLocation: failedLocation))
            {
                DefaultTrace.TraceInformation("Partition level override triggered by {0}, added to new location for {1}. PartitionKeyRange: {2}, failedLocation: {3}, new location: {4}",
                    triggeredBy,
                    request.OperationType,
                    partitionKeyRange,
                    failedLocation,
                    partionFailover.Current);

                return true;
            }

            // All the locations have been tried. Remove the override information
            DefaultTrace.TraceInformation("Partition level override removed for {0}. PartitionKeyRange: {1}, failedLocation: {2}",
                request.OperationType,
                partitionKeyRange,
                failedLocation);

            partitionKeyRangeToLocationMapping.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);

            return false;
        }

        internal sealed class PartitionKeyRangeFailoverInfo
        {
            // HashSet is not thread safe and should only accessed in the lock
            private readonly object counterLock = new ();
            private readonly object timestampLock = new ();
            private readonly ConcurrentDictionary<Uri, DateTime> FailedLocations;
            private readonly TimeSpan TimeoutCounterResetWindowInMinutes;
            private readonly int ReadRequestFailureCounterThreshold;
            private readonly int WriteRequestFailureCounterThreshold;
            private DateTime LastRequestFailureTime;
            private int ConsecutiveReadRequestFailureCount;
            private int ConsecutiveWriteRequestFailureCount;

            public PartitionKeyRangeFailoverInfo(
                string collectionRid,
                Uri currentLocation)
            {
                this.CollectionRid = collectionRid;
                this.Current = currentLocation;
                this.FirstFailedLocation = currentLocation;
                this.FailedLocations = new ConcurrentDictionary<Uri, DateTime>();
                this.ConsecutiveReadRequestFailureCount = 0;
                this.ConsecutiveWriteRequestFailureCount = 0;
                this.ReadRequestFailureCounterThreshold = ConfigurationManager.GetCircuitBreakerConsecutiveFailureCountForReads(10);
                this.WriteRequestFailureCounterThreshold = ConfigurationManager.GetCircuitBreakerConsecutiveFailureCountForWrites(5);
                this.TimeoutCounterResetWindowInMinutes = TimeSpan.FromMinutes(1);
                this.FirstRequestFailureTime = DateTime.UtcNow;
                this.LastRequestFailureTime = DateTime.UtcNow;
            }

            public Uri Current { get; private set; }

            public Uri FirstFailedLocation { get; private set; }

            public string CollectionRid { get; private set; }

            public DateTime FirstRequestFailureTime { get; private set; }

            public bool TryMoveNextLocation(
                IReadOnlyCollection<Uri> locations,
                Uri failedLocation)
            {
                // Another thread already updated it
                if (failedLocation != this.Current)
                {
                    return true;
                }

                lock (this.FailedLocations)
                {
                    // Another thread already updated it
                    if (failedLocation != this.Current)
                    {
                        return true;
                    }

                    foreach (Uri? location in locations)
                    {
                        if (this.Current == location)
                        {
                            continue;
                        }

                        if (this.FailedLocations.ContainsKey(location))
                        {
                            continue;
                        }

                        this.FailedLocations[failedLocation] = DateTime.UtcNow;
                        this.Current = location;
                        return true;
                    }
                }

                return false;
            }

            public bool CanCircuitBreakerTriggerPartitionFailOver(
                bool isReadOnlyRequest) 
            {
                this.SnapshotConsecutiveRequestFailureCount(
                    out int consecutiveReadRequestFailureCount,
                    out int consecutiveWriteRequestFailureCount);

                return isReadOnlyRequest
                    ? consecutiveReadRequestFailureCount > this.ReadRequestFailureCounterThreshold
                    : consecutiveWriteRequestFailureCount > this.WriteRequestFailureCounterThreshold;
            }

            public void IncrementRequestFailureCounts(
                bool isReadOnlyRequest,
                DateTime currentTime)
            {
                this.SnapshotPartitionFailoverTimestamps(
                    out DateTime _,
                    out DateTime lastRequestFailureTime);

                if (currentTime - lastRequestFailureTime > this.TimeoutCounterResetWindowInMinutes)
                {
                    Interlocked.Exchange(ref this.ConsecutiveReadRequestFailureCount, 0);
                    Interlocked.Exchange(ref this.ConsecutiveWriteRequestFailureCount, 0);
                }

                if (isReadOnlyRequest)
                {
                    Interlocked.Increment(ref this.ConsecutiveReadRequestFailureCount);
                }
                else
                {
                    Interlocked.Increment(ref this.ConsecutiveWriteRequestFailureCount);

                }

                this.LastRequestFailureTime = currentTime;
            }

            /// <summary>
            /// Helper method to snapshot the connection timestamps.
            /// </summary>
            /// <param name="firstRequestFailureTime">A <see cref="DateTime"/> field containing the last send attempt time.</param>
            /// <param name="lastRequestFailureTime">A <see cref="DateTime"/> field containing th e last send attempt time.</param>
            public void SnapshotPartitionFailoverTimestamps(
                out DateTime firstRequestFailureTime,
                out DateTime lastRequestFailureTime)
            {
                Debug.Assert(!Monitor.IsEntered(this.timestampLock));
                lock (this.timestampLock)
                {
                    firstRequestFailureTime = this.FirstRequestFailureTime;
                    lastRequestFailureTime = this.LastRequestFailureTime;
                }
            }

            public void SnapshotConsecutiveRequestFailureCount(
                out int consecutiveReadRequestFailureCount,
                out int consecutiveWriteRequestFailureCount)
            {
                Debug.Assert(!Monitor.IsEntered(this.counterLock));
                lock (this.counterLock)
                {
                    consecutiveReadRequestFailureCount = this.ConsecutiveReadRequestFailureCount;
                    consecutiveWriteRequestFailureCount = this.ConsecutiveWriteRequestFailureCount;
                }
            }
        }
    }
}
