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

    internal sealed class PartitionKeyRangeLocationCache
    {
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocation = new Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>>(
            () => new ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>());

        public PartitionKeyRangeLocationCache(
            GlobalEndpointManager globalEndpointManager)
        {
            this.globalEndpointManager = globalEndpointManager;
        }

        private bool CanUsePartitionLevelFailoverLocations(DocumentServiceRequest request)
        {
            return request.ResourceType == ResourceType.Document && 
                !this.globalEndpointManager.CanUseMultipleWriteLocations(request); // Disable for multimaster
        }

        public bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request)
        {
            if (!this.CanUsePartitionLevelFailoverLocations(request))
            {
                return false;
            }

            if (this.PartitionKeyRangeToLocation.IsValueCreated
                && this.PartitionKeyRangeToLocation.Value.TryGetValue(
                    request.RequestContext.ResolvedPartitionKeyRange,
                    out PartitionKeyRangeFailoverInfo partitionKeyRangeFailover))
            {
                DefaultTrace.TraceInformation("Partition level override. URI: {0}, PartitionKeyRange: {1}",
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
        public bool TryMarkEndpointUnavailableForPartitionKeyRange(
            PartitionKeyRange partitionKeyRange,
            Uri failedLocation)
        {
            DefaultTrace.TraceInformation("Partition level override Removed. PartitionKeyRange: {0}",
                partitionKeyRange);

            PartitionKeyRangeFailoverInfo partionFailover = this.PartitionKeyRangeToLocation.Value.GetOrAdd(
                partitionKeyRange,
                new PartitionKeyRangeFailoverInfo(
                    failedLocation));

            // All the locations have been tried. Remove the override information
            if (!partionFailover.TryMoveNextLocation(
                    locations: this.globalEndpointManager.ReadEndpoints,
                    failedLocation: failedLocation))
            {
                this.PartitionKeyRangeToLocation.Value.TryRemove(partitionKeyRange, out PartitionKeyRangeFailoverInfo _);
                return false;
            }

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
                ReadOnlyCollection<Uri> locations,
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
