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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using HdrHistogram;
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
        private readonly object backgroundAccountRefreshLock = new ();

        private readonly IGlobalEndpointManager globalEndpointManager;

        private readonly CancellationTokenSource cancellationTokenSource = new ();

        private readonly int backgroundRefreshLocationTimeIntervalInMS = 60000;

        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocationForWrite = new Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>>(
            () => new ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>());

        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocationForRead = new Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>>(
            () => new ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>());

        private int disposeCounter = 0;

        private bool isBackgroundAccountRefreshActive = false;

        private Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task<bool>>? backgroundConnectionInitTask;

        public GlobalPartitionEndpointManagerCore(
            IGlobalEndpointManager globalEndpointManager)
        {
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
            this.InitializeAndStartCircuitBreakerFailbackBackgroundRefresh();
        }

        public override void SetBackgroundConnectionInitTask(
            Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task<bool>> backgroundConnectionInitTask)
        {
            this.backgroundConnectionInitTask = backgroundConnectionInitTask;
        }

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
            }

            return false;
        }

        public override bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request)
        {
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

            PartitionKeyRange? partitionKeyRange = request.RequestContext.ResolvedPartitionKeyRange;
            if (partitionKeyRange == null)
            {
                return false;
            }

            if (request.IsReadOnlyRequest)
            {
                if (this.PartitionKeyRangeToLocationForRead.IsValueCreated
                    && this.PartitionKeyRangeToLocationForRead.Value.TryGetValue(
                        partitionKeyRange,
                        out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
                {
                    DefaultTrace.TraceVerbose("Partition level override. URI: {0}, PartitionKeyRange: {1}",
                        partitionKeyRangeFailover.Current,
                        partitionKeyRange.Id);

                    request.RequestContext.RouteToLocation(partitionKeyRangeFailover.Current);
                    return true;
                }
            }
            else
            {
                if (this.PartitionKeyRangeToLocationForWrite.IsValueCreated
                    && this.PartitionKeyRangeToLocationForWrite.Value.TryGetValue(
                        partitionKeyRange,
                        out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
                {
                    DefaultTrace.TraceVerbose("Partition level override. URI: {0}, PartitionKeyRange: {1}",
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
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            PartitionKeyRange? partitionKeyRange = request.RequestContext.ResolvedPartitionKeyRange;
            if (partitionKeyRange == null)
            {
                return false;
            }

            Uri? failedLocation = request.RequestContext.LocationEndpointToRoute;
            if (failedLocation == null)
            {
                return false;
            }

            // Only do partition level failover if it is a write operation.
            // Write operation will throw a write forbidden if it is not the primary
            // region.
            if (request.IsReadOnlyRequest)
            {
                PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForRead.Value.GetOrAdd(
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
                    DefaultTrace.TraceInformation("Partition level override added to new location for Reads. PartitionKeyRange: {0}, failedLocation: {1}, new location: {2}",
                        partitionKeyRange,
                        failedLocation,
                        partionFailover.Current);

                    return true;
                }

                // All the locations have been tried. Remove the override information
                DefaultTrace.TraceInformation("Partition level override removed. PartitionKeyRange: {0}, failedLocation: {1}",
                       partitionKeyRange,
                       failedLocation);

                this.PartitionKeyRangeToLocationForRead.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);
                return false;
            }
            else
            {
                if (request.RequestContext == null)
                {
                    return false;
                }

                if (!this.CanUsePartitionLevelFailoverLocations(request))
                {
                    return false;
                }

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
                    DefaultTrace.TraceInformation("Partition level override added to new location. PartitionKeyRange: {0}, failedLocation: {1}, new location: {2}",
                        partitionKeyRange,
                        failedLocation,
                        partionFailover.Current);

                    return true;
                }

                // All the locations have been tried. Remove the override information
                DefaultTrace.TraceInformation("Partition level override removed. PartitionKeyRange: {0}, failedLocation: {1}",
                       partitionKeyRange,
                       failedLocation);

                this.PartitionKeyRangeToLocationForWrite.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);
                return false;
            }
        }

        public override bool IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(
            DocumentServiceRequest request)
        {
            if (!this.IsRequestValidForPartitionFailover(
                request,
                out PartitionKeyRange? partitionKeyRange,
                out Uri? failedLocation))
            {
                return false;
            }

            if (partitionKeyRange == null || failedLocation == null)
            {
                return false;
            }

            PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForRead.Value.GetOrAdd(
                partitionKeyRange,
                (_) => new PartitionKeyRangeFailoverInfo(
                    request.RequestContext.ResolvedCollectionRid,
                    failedLocation));

            partionFailover.IncrementRequestFailureCounts();

            return partionFailover.CanCircuitBreakerTriggerPartitionFailOver();
        }

        private bool IsRequestValidForPartitionFailover(
            DocumentServiceRequest request,
            out PartitionKeyRange? partitionKeyRange,
            out Uri? failedLocation)
        {
            partitionKeyRange = default;
            failedLocation = default;
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Only do partition level failover if it is a write operation.
            // Write operation will throw a write forbidden if it is not the primary
            // region.
            //if (request.IsReadOnlyRequest)
            //{
            //    return false;
            //}

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

            failedLocation = request.RequestContext.LocationEndpointToRoute;
            if (failedLocation == null)
            {
                return false;
            }

            return true;
        }

        public void InitializeAndStartCircuitBreakerFailbackBackgroundRefresh()
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (this.isBackgroundAccountRefreshActive)
            {
                return;
            }

            lock (this.backgroundAccountRefreshLock)
            {
                if (this.isBackgroundAccountRefreshActive)
                {
                    return;
                }

                this.isBackgroundAccountRefreshActive = true;
            }

            try
            {
                this.InitiateCircuitBreakerFailbackLoop();
            }
            catch
            {
                this.isBackgroundAccountRefreshActive = false;
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

            DefaultTrace.TraceInformation("GlobalPartitionEndpointManagerCore: InitializeAccountPropertiesAndStartBackgroundRefresh() trying to get address and open connections for failed locations.");

            try
            {
                await Task.Delay(this.backgroundRefreshLocationTimeIntervalInMS, this.cancellationTokenSource.Token);

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

                DefaultTrace.TraceCritical("GlobalPartitionEndpointManagerCore: InitializeAccountPropertiesAndStartBackgroundRefresh() - Unable to get address and open connections. Exception: {0}", ex.ToString());
            }

            // Call itself to create a loop to continuously do background refresh every 5 minutes
            this.InitiateCircuitBreakerFailbackLoop();
        }

        public async Task TryOpenConnectionToUnhealthyEndpointsAndInitiateFailbackAsync()
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (this.backgroundConnectionInitTask != null)
            {
                Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>> pkRangeToEndpointMappings = new ();
                foreach (PartitionKeyRange pkRange in this.PartitionKeyRangeToLocationForRead.Value.Keys)
                {
                    PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForRead.Value[pkRange];

                    // TODO: Change this to use the first preferred location.
                    Uri originalFailedLocation = partionFailover.GetFailedLocations().First().Key;

                    pkRangeToEndpointMappings.Add(
                        key: pkRange,
                        value: new Tuple<string, Uri, TransportAddressHealthState.HealthStatus>(partionFailover.CollectionRid, originalFailedLocation, TransportAddressHealthState.HealthStatus.Unhealthy));
                }

                await this.backgroundConnectionInitTask(pkRangeToEndpointMappings);

                foreach (PartitionKeyRange pkRange in pkRangeToEndpointMappings.Keys)
                {
                    Uri originalFailedLocation = pkRangeToEndpointMappings[pkRange].Item2;
                    TransportAddressHealthState.HealthStatus currentHealthState = pkRangeToEndpointMappings[pkRange].Item3;

                    if (currentHealthState == TransportAddressHealthState.HealthStatus.Connected)
                    {
                        // Initiate Failback.
                        DefaultTrace.TraceInformation($"Initiating Failback to endpoint: {originalFailedLocation}, for partition key range: {pkRange}");

                        PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocationForRead.Value[pkRange];
                        partionFailover.SetCurrentLocation(originalFailedLocation);
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
            private readonly ConcurrentDictionary<Uri, DateTime> FailedLocations;
            private readonly TimeSpan TimeoutCounterResetWindowInMinutes;
            private readonly int RequestFailureCounterThreshold;
            private DateTime LastRequestTimeoutTime;
            private int ConsecutiveRequestFailureCount;

            public PartitionKeyRangeFailoverInfo(
                string collectionRid,
                Uri currentLocation)
            {
                this.CollectionRid = collectionRid;
                this.Current = currentLocation;
                this.FailedLocations = new ConcurrentDictionary<Uri, DateTime>();
                this.ConsecutiveRequestFailureCount = 0;
                this.RequestFailureCounterThreshold = 0; // Get this from environment variable
                this.TimeoutCounterResetWindowInMinutes = TimeSpan.FromMinutes(1);
            }

            public Uri Current { get; private set; }

            public string CollectionRid { get; private set; }

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
                return this.ConsecutiveRequestFailureCount > this.RequestFailureCounterThreshold;
            }

            public void IncrementRequestFailureCounts()
            {
                DateTime now = DateTime.UtcNow;
                if (now - this.LastRequestTimeoutTime > this.TimeoutCounterResetWindowInMinutes)
                {
                    this.ConsecutiveRequestFailureCount = 0;
                }
                this.ConsecutiveRequestFailureCount += 1;
                this.LastRequestTimeoutTime = now;
            }

            public ConcurrentDictionary<Uri, DateTime> GetFailedLocations()
            {
                return this.FailedLocations;
            }

            public void SetCurrentLocation(
                Uri currentLocation)
            {
                this.Current = currentLocation;
                this.FailedLocations.TryRemove(currentLocation, out _);
                this.ConsecutiveRequestFailureCount = 0;
            }
        }
    }
}
