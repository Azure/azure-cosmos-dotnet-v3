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
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This class is used to failover single partitions to different regions.
    /// The client retry policy will mark a partition as down. The PartitionKeyRangeToLocation
    /// will add an override to the next read region. When the request is retried it will 
    /// override the default location with the new region from the PartitionKeyRangeToLocation.
    /// </summary>
    internal sealed class GlobalPartitionEndpointManagerCore : GlobalPartitionEndpointManager
    {
        private readonly IGlobalEndpointManager globalEndpointManager;
        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocation = new Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>>(
            () => new ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>());

        public GlobalPartitionEndpointManagerCore(
            IGlobalEndpointManager globalEndpointManager)
        {
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
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

            if (this.PartitionKeyRangeToLocation.IsValueCreated
                && this.PartitionKeyRangeToLocation.Value.TryGetValue(
                    partitionKeyRange,
                    out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
            {
                DefaultTrace.TraceVerbose("Partition level override. URI: {0}, PartitionKeyRange: {1}",
                    partitionKeyRangeFailover.Current,
                    partitionKeyRange.Id);

                request.RequestContext.RouteToLocation(partitionKeyRangeFailover.Current);
                return true;
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

            // Only do partition level failover if it is a write operation.
            // Write operation will throw a write forbidden if it is not the primary
            // region.
            if (request.IsReadOnlyRequest)
            {
                return false;
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

            Uri? failedLocation = request.RequestContext.LocationEndpointToRoute;
            if (failedLocation == null)
            {
                return false;
            }

            PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocation.Value.GetOrAdd(
                partitionKeyRange,
                new PartitionKeyRangeFailoverInfo(failedLocation));

            // Will return true if it was able to update to a new region
            if (partionFailover.TryMoveNextLocation(
                    locations: this.globalEndpointManager.ReadEndpoints,
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

            this.PartitionKeyRangeToLocation.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);
            return false;

        }

        private sealed class PartitionKeyRangeFailoverInfo
        {
            // HashSet is not thread safe and should only accessed in the lock
            private readonly HashSet<Uri> FailedLocations;

            public PartitionKeyRangeFailoverInfo(
                Uri currentLocation)
            {
                this.Current = currentLocation;
                this.FailedLocations = new HashSet<Uri>();
            }

            public Uri Current { get; private set; }
            
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

                        if (this.FailedLocations.Contains(location))
                        {
                            continue;
                        }

                        this.FailedLocations.Add(failedLocation);
                        this.Current = location;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
