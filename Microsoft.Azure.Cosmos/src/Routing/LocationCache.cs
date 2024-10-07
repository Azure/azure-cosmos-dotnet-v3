//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Implements the abstraction to resolve target location for geo-replicated DatabaseAccount
    /// with multiple writable and readable locations.
    /// </summary>
    internal sealed class LocationCache
    {
        private const string UnavailableLocationsExpirationTimeInSeconds = "UnavailableLocationsExpirationTimeInSeconds";
        private static int DefaultUnavailableLocationsExpirationTimeInSeconds = 5 * 60;

        private readonly bool enableEndpointDiscovery;
        private readonly Uri defaultEndpoint;
        private readonly bool useMultipleWriteLocations;
        private readonly object lockObject;
        private readonly TimeSpan unavailableLocationsExpirationTime;
        private readonly int connectionLimit;
        private readonly ConcurrentDictionary<Uri, LocationUnavailabilityInfo> locationUnavailablityInfoByEndpoint;
        private readonly RegionNameMapper regionNameMapper;

        private DatabaseAccountLocationsInfo locationInfo;
        private DateTime lastCacheUpdateTimestamp;
        private bool enableMultipleWriteLocations;

        public LocationCache(
            ReadOnlyCollection<string> preferredLocations,
            Uri defaultEndpoint,
            bool enableEndpointDiscovery,
            int connectionLimit,
            bool useMultipleWriteLocations)
        {
            this.locationInfo = new DatabaseAccountLocationsInfo(preferredLocations, defaultEndpoint);
            this.defaultEndpoint = defaultEndpoint;
            this.enableEndpointDiscovery = enableEndpointDiscovery;
            this.useMultipleWriteLocations = useMultipleWriteLocations;
            this.connectionLimit = connectionLimit;

            this.lockObject = new object();
            this.locationUnavailablityInfoByEndpoint = new ConcurrentDictionary<Uri, LocationUnavailabilityInfo>();
            this.lastCacheUpdateTimestamp = DateTime.MinValue;
            this.enableMultipleWriteLocations = false;
            this.unavailableLocationsExpirationTime = TimeSpan.FromSeconds(LocationCache.DefaultUnavailableLocationsExpirationTimeInSeconds);
            this.regionNameMapper = new RegionNameMapper();

#if !(NETSTANDARD15 || NETSTANDARD16)
#if NETSTANDARD20
            // GetEntryAssembly returns null when loaded from native netstandard2.0
            if (System.Reflection.Assembly.GetEntryAssembly() != null)
            {
#endif
                string unavailableLocationsExpirationTimeInSecondsConfig = System.Configuration.ConfigurationManager.AppSettings[LocationCache.UnavailableLocationsExpirationTimeInSeconds];
                if (!string.IsNullOrEmpty(unavailableLocationsExpirationTimeInSecondsConfig))
                {
                    int unavailableLocationsExpirationTimeinSecondsConfigValue;

                    if (!int.TryParse(unavailableLocationsExpirationTimeInSecondsConfig, out unavailableLocationsExpirationTimeinSecondsConfigValue))
                    {
                        this.unavailableLocationsExpirationTime = TimeSpan.FromSeconds(LocationCache.DefaultUnavailableLocationsExpirationTimeInSeconds);
                    }
                    else
                    {
                        this.unavailableLocationsExpirationTime = TimeSpan.FromSeconds(unavailableLocationsExpirationTimeinSecondsConfigValue);
                    }
                }
#if NETSTANDARD20
            }
#endif  
#endif
        }

        /// <summary>
        /// Gets list of read endpoints ordered by
        /// 1. Preferred location
        /// 2. Endpoint availablity
        /// </summary>
        public ReadOnlyCollection<Uri> ReadEndpoints
        {
            get
            {
                // Hot-path: avoid ConcurrentDictionary methods which acquire locks
                if (DateTime.UtcNow - this.lastCacheUpdateTimestamp > this.unavailableLocationsExpirationTime
                    && this.locationUnavailablityInfoByEndpoint.Any())
                {
                    this.UpdateLocationCache();
                }

                return this.locationInfo.ReadEndpoints;
            }
        }

        /// <summary>
        /// Gets list of account level read endpoints.
        /// </summary>
        public ReadOnlyCollection<Uri> AccountReadEndpoints => this.locationInfo.AccountReadEndpoints;

        /// <summary>
        /// Gets list of write endpoints ordered by
        /// 1. Preferred location
        /// 2. Endpoint availablity
        /// </summary>
        public ReadOnlyCollection<Uri> WriteEndpoints
        {
            get
            {
                // Hot-path: avoid ConcurrentDictionary methods which acquire locks
                if (DateTime.UtcNow - this.lastCacheUpdateTimestamp > this.unavailableLocationsExpirationTime
                    && this.locationUnavailablityInfoByEndpoint.Any())
                {
                    this.UpdateLocationCache();
                }

                return this.locationInfo.WriteEndpoints;
            }
        }

        /// <summary>
        /// Returns the location corresponding to the endpoint if location specific endpoint is provided.
        /// For the defaultEndPoint, we will return the first available write location.
        /// Returns null, in other cases.
        /// </summary>
        /// <remarks>
        /// Today we return null for defaultEndPoint if multiple write locations can be used.
        /// This needs to be modifed to figure out proper location in such case.
        /// </remarks>
        public string GetLocation(Uri endpoint)
        {
            string location = this.locationInfo.AvailableWriteEndpointByLocation.FirstOrDefault(uri => uri.Value == endpoint).Key ?? this.locationInfo.AvailableReadEndpointByLocation.FirstOrDefault(uri => uri.Value == endpoint).Key;

            if (location == null && endpoint == this.defaultEndpoint && !this.CanUseMultipleWriteLocations())
            {
                if (this.locationInfo.AvailableWriteEndpointByLocation.Any())
                {
                    return this.locationInfo.AvailableWriteEndpointByLocation.First().Key;
                }
            }

            return location;
        }

        /// <summary>
        /// Set region name for a location if present in the locationcache otherwise set region name as null.
        /// If endpoint's hostname is same as default endpoint hostname, set regionName as null.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="regionName"></param>
        /// <returns>true if region found else false</returns>
        public bool TryGetLocationForGatewayDiagnostics(Uri endpoint, out string regionName)
        {
            if (Uri.Compare(
                    endpoint, 
                    this.defaultEndpoint, 
                    UriComponents.Host, 
                    UriFormat.SafeUnescaped, 
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                regionName = null;
                return false;
            }

            regionName = this.GetLocation(endpoint);
            return true;
        }

        /// <summary>
        /// Marks the current location unavailable for read
        /// </summary>
        public void MarkEndpointUnavailableForRead(Uri endpoint)
        {
            this.MarkEndpointUnavailable(endpoint, OperationType.Read);
        }

        /// <summary>
        /// Marks the current location unavailable for write
        /// </summary>
        public void MarkEndpointUnavailableForWrite(Uri endpoint)
        {
            this.MarkEndpointUnavailable(endpoint, OperationType.Write);
        }

        /// <summary>
        /// Invoked when <see cref="AccountProperties"/> is read
        /// </summary>
        /// <param name="databaseAccount">Read DatabaseAccoaunt </param>
        public void OnDatabaseAccountRead(AccountProperties databaseAccount)
        {
            this.UpdateLocationCache(
                databaseAccount.WritableRegions,
                databaseAccount.ReadableRegions,
                preferenceList: null,
                enableMultipleWriteLocations: databaseAccount.EnableMultipleWriteLocations);
        }

        /// <summary>
        /// Invoked when <see cref="ConnectionPolicy.PreferredLocations"/> changes
        /// </summary>
        /// <param name="preferredLocations"></param>
        public void OnLocationPreferenceChanged(ReadOnlyCollection<string> preferredLocations)
        {
            this.UpdateLocationCache(
                preferenceList: preferredLocations);
        }

        public bool IsMetaData(DocumentServiceRequest request)
        {
            return (request.OperationType != Documents.OperationType.ExecuteJavaScript && request.ResourceType == ResourceType.StoredProcedure) ||
                request.ResourceType != ResourceType.Document;
   
        }
        public bool IsMultimasterMetadataWriteRequest(DocumentServiceRequest request)
        {
            return !request.IsReadOnlyRequest && this.locationInfo.AvailableWriteLocations.Count > 1
                && this.IsMetaData(request) 
                && this.CanUseMultipleWriteLocations();

        }

        /// <summary>
        /// Gets the default endpoint of the account
        /// </summary>
        /// <returns>the default endpoint.</returns>
        public Uri GetDefaultEndpoint()
        {
            return this.defaultEndpoint;
        }

        /// <summary>
        /// Gets the mapping of available write region names to the respective endpoints
        /// </summary>
        public ReadOnlyDictionary<string, Uri> GetAvailableWriteEndpointsByLocation()
        {
            return this.locationInfo.AvailableWriteEndpointByLocation;
        }

        /// <summary>
        /// Gets the mapping of available read region names to the respective endpoints
        /// </summary>
        public ReadOnlyDictionary<string, Uri> GetAvailableReadEndpointsByLocation()
        {
            return this.locationInfo.AvailableReadEndpointByLocation;
        }

        public Uri GetHubUri()
        {
            DatabaseAccountLocationsInfo currentLocationInfo = this.locationInfo;
            string writeLocation = currentLocationInfo.AvailableWriteLocations[0];
            Uri locationEndpointToRoute = currentLocationInfo.AvailableWriteEndpointByLocation[writeLocation];
            return locationEndpointToRoute;
        }

        public ReadOnlyCollection<string> GetAvailableReadLocations()
        {
            return this.locationInfo.AvailableReadLocations;
        }

        public ReadOnlyCollection<string> GetAvailableWriteLocations()
        {
            return this.locationInfo.AvailableWriteLocations;
        }

        /// <summary>
        /// Resolves request to service endpoint. 
        /// 1. If this is a write request
        ///    (a) If UseMultipleWriteLocations = true
        ///        (i) For document writes, resolve to most preferred and available write endpoint.
        ///            Once the endpoint is marked unavailable, it is moved to the end of available write endpoint. Current request will
        ///            be retried on next preferred available write endpoint.
        ///        (ii) For all other resources, always resolve to first/second (regardless of preferred locations)
        ///             write endpoint in <see cref="AccountProperties.WritableRegions"/>.
        ///             Endpoint of first write location in <see cref="AccountProperties.WritableRegions"/> is the only endpoint that supports
        ///             write operation on all resource types (except during that region's failover). 
        ///             Only during manual failover, client would retry write on second write location in <see cref="AccountProperties.WritableRegions"/>.
        ///    (b) Else resolve the request to first write endpoint in <see cref="AccountProperties.writeRegions"/> OR 
        ///        second write endpoint in <see cref="AccountProperties.WritableRegions"/> in case of manual failover of that location.
        /// 2. Else resolve the request to most preferred available read endpoint (automatic failover for read requests)
        /// </summary>
        /// <param name="request">Request for which endpoint is to be resolved</param>
        /// <returns>Resolved endpoint</returns>
        public Uri ResolveServiceEndpoint(DocumentServiceRequest request)
        {
            if (request.RequestContext != null && request.RequestContext.LocationEndpointToRoute != null)
            {
                return request.RequestContext.LocationEndpointToRoute;
            }

            int locationIndex = request.RequestContext.LocationIndexToRoute.GetValueOrDefault(0);

            Uri locationEndpointToRoute = this.defaultEndpoint;

            if (!request.RequestContext.UsePreferredLocations.GetValueOrDefault(true) // Should not use preferred location ?
                || (request.OperationType.IsWriteOperation() && !this.CanUseMultipleWriteLocations(request)))
            {
                // For non-document resource types in case of client can use multiple write locations
                // or when client cannot use multiple write locations, flip-flop between the 
                // first and the second writable region in DatabaseAccount (for manual failover)
                DatabaseAccountLocationsInfo currentLocationInfo = this.locationInfo;

                if (this.enableEndpointDiscovery && currentLocationInfo.AvailableWriteLocations.Count > 0)
                {
                    locationIndex = Math.Min(locationIndex % 2, currentLocationInfo.AvailableWriteLocations.Count - 1);
                    string writeLocation = currentLocationInfo.AvailableWriteLocations[locationIndex];
                    locationEndpointToRoute = currentLocationInfo.AvailableWriteEndpointByLocation[writeLocation];
                }
            }
            else
            {
                ReadOnlyCollection<Uri> endpoints = this.GetApplicableEndpoints(request, !request.OperationType.IsWriteOperation());
                locationEndpointToRoute = endpoints[locationIndex % endpoints.Count];
            }

            request.RequestContext.RouteToLocation(locationEndpointToRoute);
            return locationEndpointToRoute;
        }

        public ReadOnlyCollection<Uri> GetApplicableEndpoints(DocumentServiceRequest request, bool isReadRequest)
        {
            ReadOnlyCollection<Uri> endpoints = 
                isReadRequest 
                ? this.ReadEndpoints 
                : this.WriteEndpoints;

            if (request.RequestContext.ExcludeRegions == null || request.RequestContext.ExcludeRegions.Count == 0)
            {
                return endpoints;
            }

            return this.GetApplicableEndpoints(
                isReadRequest ? this.locationInfo.AvailableReadEndpointByLocation : this.locationInfo.AvailableWriteEndpointByLocation,
                this.defaultEndpoint,
                request.RequestContext.ExcludeRegions);
        }

        public ReadOnlyCollection<string> GetApplicableRegions(IEnumerable<string> excludeRegions, bool isReadRequest)
        {
            return this.GetApplicableRegions(
                isReadRequest ? this.locationInfo.AvailableReadLocations : this.locationInfo.AvailableWriteLocations,
                this.locationInfo.PreferredLocations[0],
                excludeRegions);
        }

        /// <summary>
        /// Gets applicable endpoints for a request, if there are no applicable endpoints, returns the fallback endpoint
        /// </summary>
        /// <param name="regionNameByEndpoint"></param>
        /// <param name="fallbackEndpoint"></param>
        /// <param name="excludeRegions"></param>
        /// <returns>a list of applicable endpoints for a request</returns>
        private ReadOnlyCollection<Uri> GetApplicableEndpoints(
            ReadOnlyDictionary<string, Uri> regionNameByEndpoint,
            Uri fallbackEndpoint,
            IEnumerable<string> excludeRegions)
        {
            List<Uri> applicableEndpoints = new List<Uri>(regionNameByEndpoint.Count);
            HashSet<string> excludeRegionsHash = excludeRegions == null ? null : new HashSet<string>(excludeRegions);

            if (excludeRegions != null)
            {
                foreach (string region in this.locationInfo.PreferredLocations)
                {
                    if (!excludeRegionsHash.Contains(region)
                        && regionNameByEndpoint.TryGetValue(region, out Uri endpoint))
                    {
                        applicableEndpoints.Add(endpoint);
                    }
                }
            }
            else
            {
                foreach (string region in this.locationInfo.PreferredLocations)
                {
                    if (regionNameByEndpoint.TryGetValue(region, out Uri endpoint))
                    {
                        applicableEndpoints.Add(endpoint);
                    }
                }
            }

            if (applicableEndpoints.Count == 0)
            {
                applicableEndpoints.Add(fallbackEndpoint);
            }

            return new ReadOnlyCollection<Uri>(applicableEndpoints);
        }

        /// <summary>
        /// Gets applicable endpoints for a request, if there are no applicable endpoints, returns the fallback endpoint
        /// </summary>
        /// <param name="regionNameByEndpoint"></param>
        /// <param name="fallbackRegion"></param>
        /// <param name="excludeRegions"></param>
        /// <returns>a list of applicable endpoints for a request</returns>
        private ReadOnlyCollection<string> GetApplicableRegions(
            ReadOnlyCollection<string> regionNameByEndpoint,
            string fallbackRegion,
            IEnumerable<string> excludeRegions)
        {
            List<string> applicableRegions = new List<string>(regionNameByEndpoint.Count);
            HashSet<string> excludeRegionsHash = excludeRegions == null ? null : new HashSet<string>(excludeRegions);

            if (excludeRegions != null)
            {
                foreach (string region in this.locationInfo.PreferredLocations)
                {
                    if (regionNameByEndpoint.Contains(region)
                        && !excludeRegionsHash.Contains(region))
                    {
                        applicableRegions.Add(region);
                    }
                }
            }
            else
            {
                foreach (string region in this.locationInfo.PreferredLocations)
                {
                    if (regionNameByEndpoint.Contains(region))
                    {
                        applicableRegions.Add(region);
                    }
                }
            }

            if (applicableRegions.Count == 0)
            {
                applicableRegions.Add(fallbackRegion);
            }

            return new ReadOnlyCollection<string>(applicableRegions);
        }

        public bool ShouldRefreshEndpoints(out bool canRefreshInBackground)
        {
            canRefreshInBackground = true;
            DatabaseAccountLocationsInfo currentLocationInfo = this.locationInfo;

            string mostPreferredLocation = currentLocationInfo.PreferredLocations.FirstOrDefault();

            // we should schedule refresh in background if we are unable to target the user's most preferredLocation.
            if (this.enableEndpointDiscovery)
            {
                // Refresh if client opts-in to useMultipleWriteLocations but server-side setting is disabled
                bool shouldRefresh = this.useMultipleWriteLocations && !this.enableMultipleWriteLocations;

                ReadOnlyCollection<Uri> readLocationEndpoints = currentLocationInfo.ReadEndpoints;

                if (this.IsEndpointUnavailable(readLocationEndpoints[0], OperationType.Read))
                {
                    canRefreshInBackground = readLocationEndpoints.Count > 1;
                    DefaultTrace.TraceInformation("ShouldRefreshEndpoints = true since the first read endpoint {0} is not available for read. canRefreshInBackground = {1}",
                        readLocationEndpoints[0],
                        canRefreshInBackground);

                    return true;
                }

                if (!string.IsNullOrEmpty(mostPreferredLocation))
                {
                    Uri mostPreferredReadEndpoint;

                    if (currentLocationInfo.AvailableReadEndpointByLocation.TryGetValue(mostPreferredLocation, out mostPreferredReadEndpoint))
                    {
                        if (mostPreferredReadEndpoint != readLocationEndpoints[0])
                        {
                            // For reads, we can always refresh in background as we can alternate to
                            // other available read endpoints
                            DefaultTrace.TraceInformation("ShouldRefreshEndpoints = true since most preferred location {0} is not available for read.", mostPreferredLocation);
                            return true;
                        }
                    }
                    else
                    {
                        DefaultTrace.TraceInformation("ShouldRefreshEndpoints = true since most preferred location {0} is not in available read locations.", mostPreferredLocation);
                        return true;
                    }
                }

                Uri mostPreferredWriteEndpoint;
                ReadOnlyCollection<Uri> writeLocationEndpoints = currentLocationInfo.WriteEndpoints;

                if (!this.CanUseMultipleWriteLocations())
                {
                    if (this.IsEndpointUnavailable(writeLocationEndpoints[0], OperationType.Write))
                    {
                        // Since most preferred write endpoint is unavailable, we can only refresh in background if 
                        // we have an alternate write endpoint
                        canRefreshInBackground = writeLocationEndpoints.Count > 1;
                        DefaultTrace.TraceInformation("ShouldRefreshEndpoints = true since most preferred location {0} endpoint {1} is not available for write. canRefreshInBackground = {2}",
                            mostPreferredLocation,
                            writeLocationEndpoints[0],
                            canRefreshInBackground);

                        return true;
                    }
                    else
                    {
                        return shouldRefresh;
                    }
                }
                else if (!string.IsNullOrEmpty(mostPreferredLocation))
                {
                    if (currentLocationInfo.AvailableWriteEndpointByLocation.TryGetValue(mostPreferredLocation, out mostPreferredWriteEndpoint))
                    {
                        shouldRefresh |= mostPreferredWriteEndpoint != writeLocationEndpoints[0];
                        DefaultTrace.TraceInformation("ShouldRefreshEndpoints = {0} since most preferred location {1} is not available for write.", shouldRefresh, mostPreferredLocation);
                        return shouldRefresh;
                    }
                    else
                    {
                        DefaultTrace.TraceInformation("ShouldRefreshEndpoints = true since most preferred location {0} is not in available write locations", mostPreferredLocation);
                        return true;
                    }
                }
                else
                {
                    return shouldRefresh;
                }
            }
            else
            {
                return false;
            }
        }

        public bool CanUseMultipleWriteLocations(DocumentServiceRequest request)
        {
            return this.CanUseMultipleWriteLocations() &&
                (request.ResourceType == ResourceType.Document ||
                (request.ResourceType == ResourceType.StoredProcedure && request.OperationType == Documents.OperationType.ExecuteJavaScript));
        }

        private void ClearStaleEndpointUnavailabilityInfo()
        {
            if (this.locationUnavailablityInfoByEndpoint.Any())
            {
                List<Uri> unavailableEndpoints = this.locationUnavailablityInfoByEndpoint.Keys.ToList();

                foreach (Uri unavailableEndpoint in unavailableEndpoints)
                {
                    LocationUnavailabilityInfo unavailabilityInfo;
                    LocationUnavailabilityInfo removed;

                    if (this.locationUnavailablityInfoByEndpoint.TryGetValue(unavailableEndpoint, out unavailabilityInfo)
                        && DateTime.UtcNow - unavailabilityInfo.LastUnavailabilityCheckTimeStamp > this.unavailableLocationsExpirationTime
                        && this.locationUnavailablityInfoByEndpoint.TryRemove(unavailableEndpoint, out removed))
                    {
                        DefaultTrace.TraceInformation(
                            "Removed endpoint {0} unavailable for operations {1} from unavailableEndpoints",
                            unavailableEndpoint,
                            unavailabilityInfo.UnavailableOperations);
                    }
                }
            }
        }

        private bool IsEndpointUnavailable(Uri endpoint, OperationType expectedAvailableOperations)
        {
            LocationUnavailabilityInfo unavailabilityInfo;

            if (expectedAvailableOperations == OperationType.None
                || !this.locationUnavailablityInfoByEndpoint.TryGetValue(endpoint, out unavailabilityInfo)
                || !unavailabilityInfo.UnavailableOperations.HasFlag(expectedAvailableOperations))
            {
                return false;
            }
            else
            {
                if (DateTime.UtcNow - unavailabilityInfo.LastUnavailabilityCheckTimeStamp > this.unavailableLocationsExpirationTime)
                {
                    return false;
                }
                else
                {
                    DefaultTrace.TraceInformation(
                        "Endpoint {0} unavailable for operations {1} present in unavailableEndpoints",
                        endpoint,
                        unavailabilityInfo.UnavailableOperations);
                    // Unexpired entry present. Endpoint is unavailable
                    return true;
                }
            }
        }

        private void MarkEndpointUnavailable(
            Uri unavailableEndpoint,
            OperationType unavailableOperationType)
        {
            DateTime currentTime = DateTime.UtcNow;
            LocationUnavailabilityInfo updatedInfo = this.locationUnavailablityInfoByEndpoint.AddOrUpdate(
                unavailableEndpoint,
                (Uri endpoint) =>
                {
                    return new LocationUnavailabilityInfo()
                    {
                        LastUnavailabilityCheckTimeStamp = currentTime,
                        UnavailableOperations = unavailableOperationType,
                    };
                },
                (Uri endpoint, LocationUnavailabilityInfo info) =>
                {
                    info.LastUnavailabilityCheckTimeStamp = currentTime;
                    info.UnavailableOperations |= unavailableOperationType;
                    return info;
                });

            this.UpdateLocationCache();

            DefaultTrace.TraceInformation(
                "Endpoint {0} unavailable for {1} added/updated to unavailableEndpoints with timestamp {2}",
                unavailableEndpoint,
                unavailableOperationType,
                updatedInfo.LastUnavailabilityCheckTimeStamp);
        }

        private void UpdateLocationCache(
            IEnumerable<AccountRegion> writeLocations = null,
            IEnumerable<AccountRegion> readLocations = null,
            ReadOnlyCollection<string> preferenceList = null,
            bool? enableMultipleWriteLocations = null)
        {
            lock (this.lockObject)
            {
                DatabaseAccountLocationsInfo nextLocationInfo = new DatabaseAccountLocationsInfo(this.locationInfo);

                if (preferenceList != null)
                {
                    nextLocationInfo.PreferredLocations = preferenceList;
                }

                if (enableMultipleWriteLocations.HasValue)
                {
                    this.enableMultipleWriteLocations = enableMultipleWriteLocations.Value;
                }

                this.ClearStaleEndpointUnavailabilityInfo();

                if (readLocations != null)
                {
                    nextLocationInfo.AvailableReadEndpointByLocation = this.GetEndpointByLocation(
                        readLocations,
                        out ReadOnlyCollection<string> availableReadLocations);

                    nextLocationInfo.AvailableReadLocations = availableReadLocations;
                    nextLocationInfo.AccountReadEndpoints = nextLocationInfo.AvailableReadEndpointByLocation.Select(x => x.Value).ToList().AsReadOnly();
                }

                if (writeLocations != null)
                {
                    nextLocationInfo.AvailableWriteEndpointByLocation = this.GetEndpointByLocation(
                        writeLocations,
                        out ReadOnlyCollection<string> availableWriteLocations);

                    nextLocationInfo.AvailableWriteLocations = availableWriteLocations;
                }

                nextLocationInfo.WriteEndpoints = this.GetPreferredAvailableEndpoints(
                    endpointsByLocation: nextLocationInfo.AvailableWriteEndpointByLocation,
                    orderedLocations: nextLocationInfo.AvailableWriteLocations,
                    expectedAvailableOperation: OperationType.Write,
                    fallbackEndpoint: this.defaultEndpoint);

                nextLocationInfo.ReadEndpoints = this.GetPreferredAvailableEndpoints(
                    endpointsByLocation: nextLocationInfo.AvailableReadEndpointByLocation,
                    orderedLocations: nextLocationInfo.AvailableReadLocations,
                    expectedAvailableOperation: OperationType.Read,
                    fallbackEndpoint: nextLocationInfo.WriteEndpoints[0]);

                this.lastCacheUpdateTimestamp = DateTime.UtcNow;

                DefaultTrace.TraceInformation("Current WriteEndpoints = ({0}) ReadEndpoints = ({1})",
                    string.Join(", ", nextLocationInfo.WriteEndpoints.Select(endpoint => endpoint.ToString())),
                    string.Join(", ", nextLocationInfo.ReadEndpoints.Select(endpoint => endpoint.ToString())));

                this.locationInfo = nextLocationInfo;
            }
        }

        private ReadOnlyCollection<Uri> GetPreferredAvailableEndpoints(ReadOnlyDictionary<string, Uri> endpointsByLocation, ReadOnlyCollection<string> orderedLocations, OperationType expectedAvailableOperation, Uri fallbackEndpoint)
        {
            List<Uri> endpoints = new List<Uri>();
            DatabaseAccountLocationsInfo currentLocationInfo = this.locationInfo;

            // if enableEndpointDiscovery is false, we always use the defaultEndpoint that user passed in during documentClient init
            if (this.enableEndpointDiscovery)
            {
                if (this.CanUseMultipleWriteLocations() || expectedAvailableOperation.HasFlag(OperationType.Read))
                {
                    List<Uri> unavailableEndpoints = new List<Uri>();

                    // When client can not use multiple write locations, preferred locations list should only be used
                    // determining read endpoints order. 
                    // If client can use multiple write locations, preferred locations list should be used for determining
                    // both read and write endpoints order.

                    foreach (string location in currentLocationInfo.PreferredLocations)
                    {
                        if (endpointsByLocation.TryGetValue(location, out Uri endpoint))
                        {
                            if (this.IsEndpointUnavailable(endpoint, expectedAvailableOperation))
                            {
                                unavailableEndpoints.Add(endpoint);
                            }
                            else
                            {
                                endpoints.Add(endpoint);
                            }
                        }
                    }

                    if (endpoints.Count == 0)
                    {
                        endpoints.Add(fallbackEndpoint);
                        unavailableEndpoints.Remove(fallbackEndpoint);
                    }

                    endpoints.AddRange(unavailableEndpoints);
                }
                else
                {
                    foreach (string location in orderedLocations)
                    {
                        if (!string.IsNullOrEmpty(location) && // location is empty during manual failover
                            endpointsByLocation.TryGetValue(location, out Uri endpoint))
                        {
                            endpoints.Add(endpoint);
                        }
                    }
                }
            }

            if (endpoints.Count == 0)
            {
                endpoints.Add(fallbackEndpoint);
            }

            return endpoints.AsReadOnly();
        }

        private ReadOnlyDictionary<string, Uri> GetEndpointByLocation(IEnumerable<AccountRegion> locations, out ReadOnlyCollection<string> orderedLocations)
        {
            Dictionary<string, Uri> endpointsByLocation = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
            List<string> parsedLocations = new List<string>();

            foreach (AccountRegion location in locations)
            {
                Uri endpoint;
                if (!string.IsNullOrEmpty(location.Name)
                    && Uri.TryCreate(location.Endpoint, UriKind.Absolute, out endpoint))
                {
                    endpointsByLocation[location.Name] = endpoint;
                    parsedLocations.Add(location.Name);
                    this.SetServicePointConnectionLimit(endpoint);
                }
                else
                {
                    DefaultTrace.TraceInformation("GetAvailableEndpointsByLocation() - skipping add for location = {0} as it is location name is either empty or endpoint is malformed {1}",
                        location.Name,
                        location.Endpoint);
                }
            }

            orderedLocations = parsedLocations.AsReadOnly();
            return new ReadOnlyDictionary<string, Uri>(endpointsByLocation);
        }

        internal bool CanUseMultipleWriteLocations()
        {
            return this.useMultipleWriteLocations && this.enableMultipleWriteLocations;
        }

        private void SetServicePointConnectionLimit(Uri endpoint)
        {
#if !NETSTANDARD16
            ServicePointAccessor servicePoint = ServicePointAccessor.FindServicePoint(endpoint);
            servicePoint.ConnectionLimit = this.connectionLimit;
#endif
        }

        private sealed class LocationUnavailabilityInfo
        {
            public DateTime LastUnavailabilityCheckTimeStamp { get; set; }
            public OperationType UnavailableOperations { get; set; }
        }

        private sealed class DatabaseAccountLocationsInfo
        {
            public DatabaseAccountLocationsInfo(ReadOnlyCollection<string> preferredLocations, Uri defaultEndpoint)
            {
                this.PreferredLocations = preferredLocations;
                this.AvailableWriteLocations = new List<string>().AsReadOnly();
                this.AvailableReadLocations = new List<string>().AsReadOnly();
                this.AvailableWriteEndpointByLocation = new ReadOnlyDictionary<string, Uri>(new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase));
                this.AvailableReadEndpointByLocation = new ReadOnlyDictionary<string, Uri>(new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase));
                this.WriteEndpoints = new List<Uri>() { defaultEndpoint }.AsReadOnly();
                this.AccountReadEndpoints = new List<Uri>() { defaultEndpoint }.AsReadOnly();
                this.ReadEndpoints = new List<Uri>() { defaultEndpoint }.AsReadOnly();
            }

            public DatabaseAccountLocationsInfo(DatabaseAccountLocationsInfo other)
            {
                this.PreferredLocations = other.PreferredLocations;
                this.AvailableWriteLocations = other.AvailableWriteLocations;
                this.AvailableReadLocations = other.AvailableReadLocations;
                this.AvailableWriteEndpointByLocation = other.AvailableWriteEndpointByLocation;
                this.AvailableReadEndpointByLocation = other.AvailableReadEndpointByLocation;
                this.WriteEndpoints = other.WriteEndpoints;
                this.AccountReadEndpoints = other.AccountReadEndpoints;
                this.ReadEndpoints = other.ReadEndpoints;
            }

            public ReadOnlyCollection<string> PreferredLocations { get; set; }
            public ReadOnlyCollection<string> AvailableWriteLocations { get; set; }
            public ReadOnlyCollection<string> AvailableReadLocations { get; set; }
            public ReadOnlyDictionary<string, Uri> AvailableWriteEndpointByLocation { get; set; }
            public ReadOnlyDictionary<string, Uri> AvailableReadEndpointByLocation { get; set; }
            public ReadOnlyCollection<Uri> WriteEndpoints { get; set; }
            public ReadOnlyCollection<Uri> ReadEndpoints { get; set; }
            public ReadOnlyCollection<Uri> AccountReadEndpoints { get; set; }
        }

        [Flags]
        private enum OperationType
        {
            None = 0x0,
            Read = 0x1,
            Write = 0x2
        }
    }
}
