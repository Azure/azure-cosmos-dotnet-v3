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
    internal sealed class GlobalPartitionFailoverEndpointManagerCore : GlobalPartitionFailoverEndpointManager
    {
        private readonly IGlobalEndpointManager globalEndpointManager;
        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocation = new Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>>(
            () => new ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>());

        public GlobalPartitionFailoverEndpointManagerCore(
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

            if (!this.CanUsePartitionLevelFailoverLocations(request))
            {
                return false;
            }

            if (this.PartitionKeyRangeToLocation.IsValueCreated
                && this.PartitionKeyRangeToLocation.Value.TryGetValue(
                    request.RequestContext.ResolvedPartitionKeyRange,
                    out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
            {
                DefaultTrace.TraceVerbose("Partition level override. URI: {0}, PartitionKeyRange: {1}",
                    partitionKeyRangeFailover.Current,
                    request.RequestContext.ResolvedPartitionKeyRange.Id);

                request.RequestContext.RouteToLocation(partitionKeyRangeFailover.Current);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Marks the current location unavailable for write
        /// </summary>
        public override bool TryMarkEndpointUnavailableForPartitionKeyRange(
            DocumentServiceRequest request,
            Uri failedLocation)
        {
            if (request == null || failedLocation == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!this.CanUsePartitionLevelFailoverLocations(request))
            {
                return false;
            }

            if (request?.RequestContext?.ResolvedPartitionKeyRange == null)
            {
                return false;
            }

            PartitionKeyRange partitionKeyRange = request.RequestContext.ResolvedPartitionKeyRange;
            PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocation.Value.GetOrAdd(
                partitionKeyRange,
                new PartitionKeyRangeFailoverInfo(failedLocation));

            // All the locations have been tried. Remove the override information
            if (!partionFailover.TryMoveNextLocation(
                    locations: this.globalEndpointManager.ReadEndpoints,
                    failedLocation: failedLocation))
            {
                DefaultTrace.TraceInformation("Partition level override removed. PartitionKeyRange: {0}, failedLocation: {1}",
                    partitionKeyRange,
                    failedLocation);

                this.PartitionKeyRangeToLocation.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);
                return false;
            }

            DefaultTrace.TraceInformation("Partition level override added to new location. PartitionKeyRange: {0}, failedLocation: {1}, new location: {2}",
                    partitionKeyRange,
                    failedLocation,
                    partionFailover.Current);

            return true;
        }

        private sealed class PartitionKeyRangeFailoverInfo
        {
            public PartitionKeyRangeFailoverInfo(
                Uri currentLocation)
            {
                this.Current = currentLocation;
                this.FailedLocations = new HashSet<Uri>();
            }

            public Uri Current { get; private set; }
            private HashSet<Uri> FailedLocations { get; }
            public bool TryMoveNextLocation(
                IReadOnlyCollection<Uri> locations,
                Uri failedLocation)
            {
                lock (this.Current)
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
