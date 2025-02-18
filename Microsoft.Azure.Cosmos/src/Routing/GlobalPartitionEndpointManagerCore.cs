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
    /// The client retry policy will mark a partition as down. The PartitionKeyRangeToLocationForWrite
    /// will add an override to the next read region. When the request is retried it will 
    /// override the default location with the new region from the PartitionKeyRangeToLocationForWrite.
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
        private Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task<bool>>? backgroundOpenConnectionTask;

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

        /// <summary>
        /// Sets the background connection initialization task.
        /// </summary>
        /// <param name="backgroundConnectionInitTask"></param>
        public override void SetBackgroundConnectionInitTask(
            Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task<bool>> backgroundConnectionInitTask)
        {
            this.backgroundOpenConnectionTask = backgroundConnectionInitTask;
        }

        /// <summary>
        /// Updates the DocumentServiceRequest routing location to point
        /// </summary>
        /// <param name="request"></param>
        /// <returns>A boolean flag</returns>
        public override bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request)
        {
            if (!this.IsRequestValidForPartitionFailover(
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

            if (request.IsReadOnlyRequest
                || (!request.IsReadOnlyRequest
                    && this.isPartitionLevelCircuitBreakerEnabled
                    && this.globalEndpointManager.CanUseMultipleWriteLocations(request)))
            {
                if (this.PartitionKeyRangeToLocationForReadAndWrite.IsValueCreated
                    && this.PartitionKeyRangeToLocationForReadAndWrite.Value.TryGetValue(
                        partitionKeyRange,
                        out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
                {
                    DefaultTrace.TraceVerbose("Partition level override for reads. URI: {0}, PartitionKeyRange: {1}",
                        partitionKeyRangeFailover.Current,
                        partitionKeyRange.Id);

                    request.RequestContext.RouteToLocation(partitionKeyRangeFailover.Current);
                    return true;
                }
            }
            else if (this.isPartitionLevelFailoverEnabled && !request.IsReadOnlyRequest)
            {
                if (this.PartitionKeyRangeToLocationForWrite.IsValueCreated
                    && this.PartitionKeyRangeToLocationForWrite.Value.TryGetValue(
                        partitionKeyRange,
                        out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
                {
                    DefaultTrace.TraceVerbose("Partition level override for writes. URI: {0}, PartitionKeyRange: {1}",
                        partitionKeyRangeFailover.Current,
                        partitionKeyRange.Id);

                    request.RequestContext.RouteToLocation(partitionKeyRangeFailover.Current);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Marks the current location unavailable for write
        /// </summary>
        public override bool TryMarkEndpointUnavailableForPartitionKeyRange(
            DocumentServiceRequest request)
        {
            if (!this.IsRequestValidForPartitionFailover(
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

            // Only do partition level failover if it is a write operation.
            // Write operation will throw a write forbidden if it is not the primary
            // region.
            if (request.IsReadOnlyRequest 
                || (!request.IsReadOnlyRequest
                    && this.isPartitionLevelCircuitBreakerEnabled 
                    && this.globalEndpointManager.CanUseMultipleWriteLocations(request)))
            {
                PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForReadAndWrite.Value.GetOrAdd(
                    partitionKeyRange,
                    (_) => new PartitionKeyRangeFailoverInfo(
                        request.RequestContext.ResolvedCollectionRid,
                        failedLocation));

                ReadOnlyCollection<Uri> nextLocations = this.globalEndpointManager.ReadEndpoints;

                // Will return true if it was able to update to a new region
                if (partionFailover.TryMoveNextLocation(
                        locations: nextLocations,
                        failedLocation: failedLocation))
                {
                    DefaultTrace.TraceInformation("Partition level override for reads added to new location for Reads. PartitionKeyRange: {0}, failedLocation: {1}, new location: {2}",
                        partitionKeyRange,
                        failedLocation,
                        partionFailover.Current);

                    return true;
                }

                // All the locations have been tried. Remove the override information
                DefaultTrace.TraceInformation("Partition level override for reads removed. PartitionKeyRange: {0}, failedLocation: {1}",
                       partitionKeyRange,
                       failedLocation);

                this.PartitionKeyRangeToLocationForReadAndWrite.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);
            }
            else if (this.isPartitionLevelFailoverEnabled && !request.IsReadOnlyRequest)
            {
                PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForWrite.Value.GetOrAdd(
                    partitionKeyRange,
                    (_) => new PartitionKeyRangeFailoverInfo(
                        request.RequestContext.ResolvedCollectionRid,
                        failedLocation));

                // For any single master write accounts, the next locations to fail over will be the read regions configured at the account level.
                // For multi master write accounts, since all the regions are treated as write regions, the next locations to fail over
                // will be the preferred read regions that are configured in the application preferred regions in the CosmosClientOptions.
                bool isSingleMasterWriteAccount = !this.globalEndpointManager.CanUseMultipleWriteLocations(request);

                ReadOnlyCollection<Uri> nextLocations = isSingleMasterWriteAccount
                    ? this.globalEndpointManager.AccountReadEndpoints
                    : this.globalEndpointManager.ReadEndpoints;

                // Will return true if it was able to update to a new region
                if (partionFailover.TryMoveNextLocation(
                        locations: nextLocations,
                        failedLocation: failedLocation))
                {
                    DefaultTrace.TraceInformation("Partition level override for writes added to new location. PartitionKeyRange: {0}, failedLocation: {1}, new location: {2}",
                        partitionKeyRange,
                        failedLocation,
                        partionFailover.Current);

                    return true;
                }

                // All the locations have been tried. Remove the override information
                DefaultTrace.TraceInformation("Partition level override for writes removed. PartitionKeyRange: {0}, failedLocation: {1}",
                       partitionKeyRange,
                       failedLocation);

                this.PartitionKeyRangeToLocationForWrite.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);
            }

            DefaultTrace.TraceInformation("Skipping Partition level override.");

            return false;
        }

        /// <summary>
        /// Can Partition fail over on request timeouts.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>A boolean flag.</returns>
        public override bool IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(
            DocumentServiceRequest request)
        {
            if (!this.IsRequestValidForPartitionFailover(
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

            partionFailover.IncrementRequestFailureCounts(DateTime.UtcNow);

            return partionFailover.CanCircuitBreakerTriggerPartitionFailOver();
        }

        /// <summary>
        /// Can Partition fail over on request timeouts.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>A boolean</returns>
        private bool CanUsePartitionLevelFailoverLocations(DocumentServiceRequest request)
        {
            if (this.globalEndpointManager.ReadEndpoints.Count <= 1)
            {
                return false;
            }

            if (request.ResourceType == ResourceType.Document ||
                (request.ResourceType == ResourceType.StoredProcedure && request.OperationType == Documents.OperationType.ExecuteJavaScript))
            {
                // Disable for multimaster because it currently 
                // depends on 403.3 to signal the primary region is backup
                // and to fail back over
                if (!this.globalEndpointManager.CanUseMultipleWriteLocations(request))
                {
                    return true;
                }
                else
                {
                    // Right now, for multi master, only reads are supported for circuit breaker.
                    // return request.OperationType == Documents.OperationType.Read;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Helper method to check if the request is valid for partition failover.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="shouldValidateFailedLocation"></param>
        /// <param name="partitionKeyRange"></param>
        /// <param name="failedLocation"></param>
        /// <returns>A bool.</returns>
        private bool IsRequestValidForPartitionFailover(
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
        /// Initialize and start the background connection initialization.
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

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void InitiateCircuitBreakerFailbackLoop()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            DefaultTrace.TraceInformation("GlobalPartitionEndpointManagerCore: InitiateCircuitBreakerFailbackLoop() trying to get address and open connections for failed locations.");

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(this.backgroundConnectionInitTimeIntervalInSeconds),
                    this.cancellationTokenSource.Token);

                if (this.cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                await this.TryOpenConnectionToUnhealthyEndpointsAndInitiateFailbackAsync();
            }
            catch (Exception ex)
            {
                if (this.cancellationTokenSource.IsCancellationRequested && (ex is OperationCanceledException || ex is ObjectDisposedException))
                {
                    return;
                }

                DefaultTrace.TraceCritical("GlobalPartitionEndpointManagerCore: InitiateCircuitBreakerFailbackLoop() - Unable to get address and open connections. Exception: {0}", ex.ToString());
            }

            // Call itself to create a loop to continuously do background refresh every 5 minutes
            this.InitiateCircuitBreakerFailbackLoop();
        }

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
                        // TODO: Change this to use the first preferred location.
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
                            // Initiate Failback.
                            DefaultTrace.TraceInformation($"Initiating Failback to endpoint: {originalFailedLocation}, for partition key range: {pkRange}");

                            // Think about the possibility of removing the partition key range from the dictionary.
                            this.PartitionKeyRangeToLocationForReadAndWrite.Value.TryRemove(pkRange, out PartitionKeyRangeFailoverInfo _);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Increment(ref this.disposeCounter) == 1)
            {
                this.cancellationTokenSource?.Cancel();
                this.cancellationTokenSource?.Dispose();
            }
        }

        internal sealed class PartitionKeyRangeFailoverInfo
        {
            // HashSet is not thread safe and should only accessed in the lock
            private readonly object counterLock = new ();
            private readonly object timestampLock = new ();
            private readonly ConcurrentDictionary<Uri, DateTime> FailedLocations;
            private readonly TimeSpan TimeoutCounterResetWindowInMinutes;
            private readonly int RequestFailureCounterThreshold;
            private DateTime LastRequestFailureTime;
            private int ConsecutiveRequestFailureCount;

            public PartitionKeyRangeFailoverInfo(
                string collectionRid,
                Uri currentLocation)
            {
                this.CollectionRid = collectionRid;
                this.Current = currentLocation;
                this.FirstFailedLocation = currentLocation;
                this.FailedLocations = new ConcurrentDictionary<Uri, DateTime>();
                this.ConsecutiveRequestFailureCount = 0;
                this.RequestFailureCounterThreshold = ConfigurationManager.GetCircuitBreakerConsecutiveFailureCount(10);
                this.TimeoutCounterResetWindowInMinutes = TimeSpan.FromMinutes(1);
                this.FirstRequestFailureTime = DateTime.UtcNow;
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

            public bool CanCircuitBreakerTriggerPartitionFailOver()
            {
                this.SnapshotConsecutiveRequestFailureCount(
                    out int consecutiveRequestFailureCount);

                return consecutiveRequestFailureCount > this.RequestFailureCounterThreshold;
            }

            public void IncrementRequestFailureCounts(
                DateTime currentTime)
            {
                this.SnapshotPartitionFailoverTimestamps(
                    out DateTime _,
                    out DateTime lastRequestFailureTime);

                if (currentTime - lastRequestFailureTime > this.TimeoutCounterResetWindowInMinutes)
                {
                    Interlocked.Exchange(ref this.ConsecutiveRequestFailureCount, 0);
                }

                Interlocked.Increment(ref this.ConsecutiveRequestFailureCount);
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
                out int consecutiveRequestFailureCount)
            {
                Debug.Assert(!Monitor.IsEntered(this.counterLock));
                lock (this.counterLock)
                {
                    consecutiveRequestFailureCount = this.ConsecutiveRequestFailureCount;
                }
            }
        }
    }
}
