//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// AddressCache implementation for client SDK. Supports cross region address routing based on 
    /// availability and preference list.
    /// </summary>
    /// Marking it as non-sealed in order to unit test it using Moq framework
    internal class GlobalEndpointManager : IGlobalEndpointManager
    {
        private const int DefaultBackgroundRefreshLocationTimeIntervalInMS = 5 * 60 * 1000;

        private const string BackgroundRefreshLocationTimeIntervalInMS = "BackgroundRefreshLocationTimeIntervalInMS";
        private const string MinimumIntervalForNonForceRefreshLocationInMS = "MinimumIntervalForNonForceRefreshLocationInMS";
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly LocationCache locationCache;
        private readonly Uri defaultEndpoint;
        private readonly ConnectionPolicy connectionPolicy;
        private readonly IDocumentClientInternal owner;
        private readonly AsyncCache<string, AccountProperties> databaseAccountCache;
        private readonly TimeSpan MinTimeBetweenAccountRefresh = TimeSpan.FromSeconds(15);
        private readonly int backgroundRefreshLocationTimeIntervalInMS = GlobalEndpointManager.DefaultBackgroundRefreshLocationTimeIntervalInMS;
        private readonly object backgroundAccountRefreshLock = new object();
        private readonly object isAccountRefreshInProgressLock = new object();
        private bool isAccountRefreshInProgress = false;
        private bool isBackgroundAccountRefreshActive = false;
        private DateTime LastBackgroundRefreshUtc = DateTime.MinValue;

        // Drives the thin client HTTP/2 connectivity probe.
        private EndpointProbeClient? thinClientProbeClient;

        // Last observed value of the account-level disableCrossRegionalHedging flag.
        // Tracked separately so the change event fires when only this flag toggles.
        private bool lastKnownDisableCrossRegionalHedging = false;

        // Last observed value of the account-level EnablePartitionLevelFailover flag.
        // Tracked separately (mirroring lastKnownDisableCrossRegionalHedging) so PPAF-enablement
        // change detection keys off GEM's own baseline rather than connectionPolicy.EnablePartitionLevelFailover,
        // which the subscriber (DocumentClient) mutates. This decouples "what the gateway reported"
        // from "what we last applied" and keeps detection correct against any future external writer
        // of connectionPolicy.EnablePartitionLevelFailover.
        private bool lastKnownEnablePartitionLevelFailover = false;

        /// <summary>
        /// Event that is raised when PPAF (Per Partition Automatic Failover) enablement status changes
        /// or when the gateway-controlled disableCrossRegionalHedging flag toggles.
        /// </summary>
        /// <remarks>
        /// First argument is the latest <c>EnablePartitionLevelFailover</c> value observed from the
        /// Gateway (falls back to <see cref="lastKnownEnablePartitionLevelFailover"/> when the property
        /// is absent, so a dropped property preserves the previously-honored value rather than implying
        /// a transition). Second argument is the latest <c>disableCrossRegionalHedging</c> value (falls
        /// back to <see cref="lastKnownDisableCrossRegionalHedging"/> when absent from the Gateway response).
        /// </remarks>
        internal event Action<bool, bool>? OnEnablePartitionLevelFailoverConfigChanged;

        public GlobalEndpointManager(
            IDocumentClientInternal owner,
            ConnectionPolicy connectionPolicy,
            bool enableAsyncCacheExceptionNoSharing = true)
        {
            this.locationCache = new LocationCache(
                new ReadOnlyCollection<string>(connectionPolicy.PreferredLocations),
                owner.ServiceEndpoint,
                connectionPolicy.EnableEndpointDiscovery,
                connectionPolicy.MaxConnectionLimit,
                connectionPolicy.UseMultipleWriteLocations,
                isPartitionLevelFailoverEnabled: () => connectionPolicy.EnablePartitionLevelFailover);

            this.owner = owner;
            this.defaultEndpoint = owner.ServiceEndpoint;
            this.connectionPolicy = connectionPolicy;

            this.connectionPolicy.PreferenceChanged += this.OnPreferenceChanged;
            this.databaseAccountCache = new AsyncCache<string, AccountProperties>(enableAsyncCacheExceptionNoSharing);

#if !(NETSTANDARD15 || NETSTANDARD16)
#if NETSTANDARD20
            // GetEntryAssembly returns null when loaded from native netstandard2.0
            if (System.Reflection.Assembly.GetEntryAssembly() != null)
            {
#endif
                string backgroundRefreshLocationTimeIntervalInMSConfig = System.Configuration.ConfigurationManager.AppSettings[GlobalEndpointManager.BackgroundRefreshLocationTimeIntervalInMS];
                if (!string.IsNullOrEmpty(backgroundRefreshLocationTimeIntervalInMSConfig))
                {
                    if (!int.TryParse(backgroundRefreshLocationTimeIntervalInMSConfig, out this.backgroundRefreshLocationTimeIntervalInMS))
                    {
                        this.backgroundRefreshLocationTimeIntervalInMS = GlobalEndpointManager.DefaultBackgroundRefreshLocationTimeIntervalInMS;
                    }
                }
#if NETSTANDARD20
            }
#endif  
#endif
            string minimumIntervalForNonForceRefreshLocationInMSConfig = Environment.GetEnvironmentVariable(GlobalEndpointManager.MinimumIntervalForNonForceRefreshLocationInMS);
            if (!string.IsNullOrEmpty(minimumIntervalForNonForceRefreshLocationInMSConfig))
            {
                if (int.TryParse(minimumIntervalForNonForceRefreshLocationInMSConfig, out int minimumIntervalForNonForceRefreshLocationInMS))
                {
                    this.MinTimeBetweenAccountRefresh = TimeSpan.FromMilliseconds(minimumIntervalForNonForceRefreshLocationInMS);
                }
                else
                {
                    DefaultTrace.TraceError($"GlobalEndpointManager: Failed to parse {GlobalEndpointManager.MinimumIntervalForNonForceRefreshLocationInMS}; Value:{minimumIntervalForNonForceRefreshLocationInMSConfig}");
                }
            }
        }

        public ReadOnlyCollection<Uri> ReadEndpoints => this.locationCache.ReadEndpoints;

        public ReadOnlyCollection<Uri> AccountReadEndpoints => this.locationCache.AccountReadEndpoints;

        public ReadOnlyCollection<Uri> WriteEndpoints => this.locationCache.WriteEndpoints;

        public ReadOnlyCollection<Uri> ThinClientReadEndpoints => this.locationCache.ThinClientReadEndpoints;

        public ReadOnlyCollection<Uri> ThinClientWriteEndpoints => this.locationCache.ThinClientWriteEndpoints;

        public bool HasThinClientReadLocations => this.locationCache.HasThinClientReadLocations;

        public bool HasThinClientWriteLocations => this.locationCache.HasThinClientWriteLocations;

        /// <summary>
        /// Returns true only when the endpoint has been confirmed healthy by the connectivity probe.
        /// Fails closed: an un-probed or failed endpoint, or a missing probe client, reports unhealthy, so the
        /// routing site uses the proxy only for probe-confirmed regions and Gateway V1 otherwise.
        /// </summary>
        public bool IsProxyEndpointHealthy(Uri thinClientEndpoint)
        {
            EndpointProbeClient? probeClient = this.thinClientProbeClient;
            return probeClient != null && probeClient.IsEndpointHealthy(thinClientEndpoint);
        }

        /// <summary>
        /// True only when every advertised thin client READ regional endpoint is probe-healthy. Used by the
        /// failover walk, which routes a whole read-endpoint list rather than a single endpoint. Fails closed:
        /// returns false when no probe client is wired or no read endpoints are advertised.
        /// </summary>
        public bool AreAllThinClientReadEndpointsHealthy
        {
            get
            {
                EndpointProbeClient? probeClient = this.thinClientProbeClient;
                if (probeClient == null)
                {
                    return false;
                }

                ReadOnlyCollection<Uri> readEndpoints = this.ThinClientReadEndpoints;
                if (readEndpoints == null || readEndpoints.Count == 0)
                {
                    return false;
                }

                foreach (Uri endpoint in readEndpoints)
                {
                    if (!probeClient.IsEndpointHealthy(endpoint))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Computes the thin client regional endpoint for <paramref name="request"/> without pinning it onto the
        /// request context, so the routing layer can evaluate <see cref="IsProxyEndpointHealthy"/> before
        /// deciding whether to pin the thin client endpoint or fall back to the gateway (service) endpoint.
        /// </summary>
        public Uri GetThinClientEndpointCandidate(DocumentServiceRequest request)
        {
            return this.locationCache.GetThinClientEndpointCandidate(request, request.IsReadOnlyRequest);
        }

        public int PreferredLocationCount
        {
            get
            {
                IList<string> effectivePreferredLocations = this.GetEffectivePreferredLocations();

                return effectivePreferredLocations.Count;
            }
        }

        public bool IsMultimasterMetadataWriteRequest(DocumentServiceRequest request)
        {
            return this.locationCache.IsMultimasterMetadataWriteRequest(request);
        }

        public Uri GetHubUri()
        {
            return this.locationCache.GetHubUri();
        }

        /// <summary>
        /// This will get the account information.
        /// It will try the global endpoint first. 
        /// If no response in 5 seconds it will create 2 additional tasks
        /// The 2 additional tasks will go through all the preferred regions in parallel
        /// It will return the first success and stop the parallel tasks.
        /// </summary>
        public static async Task<AccountProperties> GetDatabaseAccountFromAnyLocationsAsync(
            Uri defaultEndpoint,
            IList<string>? locations,
            IList<Uri>? accountInitializationCustomEndpoints,
            Func<Uri, Task<AccountProperties>> getDatabaseAccountFn,
            CancellationToken cancellationToken)
        {
            using (GetAccountPropertiesHelper threadSafeGetAccountHelper = new GetAccountPropertiesHelper(
               defaultEndpoint,
               locations,
               accountInitializationCustomEndpoints,
               getDatabaseAccountFn,
               cancellationToken))
            {
                return await threadSafeGetAccountHelper.GetAccountPropertiesAsync();
            }
        }

        /// <summary>
        /// This is a helper class to 
        /// </summary>
        private class GetAccountPropertiesHelper : IDisposable
        {
            private readonly CancellationTokenSource CancellationTokenSource;
            private readonly Uri DefaultEndpoint;
            private readonly bool LimitToGlobalEndpointOnly;
            private readonly IEnumerator<Uri> ServiceEndpointEnumerator;
            private readonly Func<Uri, Task<AccountProperties>> GetDatabaseAccountFn;
            private readonly List<Exception> TransientExceptions = new List<Exception>();
            private AccountProperties? AccountProperties = null;
            private Exception? NonRetriableException = null;
            private int disposeCounter = 0;

            public GetAccountPropertiesHelper(
                Uri defaultEndpoint,
                IList<string>? locations,
                IList<Uri>? accountInitializationCustomEndpoints,
                Func<Uri, Task<AccountProperties>> getDatabaseAccountFn,
                CancellationToken cancellationToken)
            {
                this.DefaultEndpoint = defaultEndpoint;
                this.LimitToGlobalEndpointOnly = (locations == null || locations.Count == 0) && (accountInitializationCustomEndpoints == null || accountInitializationCustomEndpoints.Count == 0);
                this.GetDatabaseAccountFn = getDatabaseAccountFn;
                this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                this.ServiceEndpointEnumerator = GetAccountPropertiesHelper
                    .GetServiceEndpoints(
                        defaultEndpoint,
                        locations,
                        accountInitializationCustomEndpoints)
                    .GetEnumerator();
            }

            public async Task<AccountProperties> GetAccountPropertiesAsync()
            {
                // If there are no preferred regions or private endpoints, then just wait for the global endpoint results
                if (this.LimitToGlobalEndpointOnly)
                {
                    return await this.GetOnlyGlobalEndpointAsync();
                }

                Task globalEndpointTask = this.GetAndUpdateAccountPropertiesAsync(this.DefaultEndpoint);

                // Start a timer to start secondary requests in parallel.
                Task timerTask = Task.Delay(TimeSpan.FromSeconds(5));
                await Task.WhenAny(globalEndpointTask, timerTask);
                if (this.AccountProperties != null)
                {
                    return this.AccountProperties;
                }

                if (this.NonRetriableException != null)
                {
                    ExceptionDispatchInfo.Capture(this.NonRetriableException).Throw();
                }

                // Start 2 additional tasks to try to get the account information
                // from the preferred region list since global account has not succeed yet.
                HashSet<Task> tasksToWaitOn = new HashSet<Task>
                {
                    globalEndpointTask,
                    this.TryGetAccountPropertiesFromAllLocationsAsync(),
                    this.TryGetAccountPropertiesFromAllLocationsAsync()
                };

                while (tasksToWaitOn.Any())
                {
                    Task completedTask = await Task.WhenAny(tasksToWaitOn);
                    if (this.AccountProperties != null)
                    {
                        return this.AccountProperties;
                    }

                    if (this.NonRetriableException != null)
                    {
                        ExceptionDispatchInfo.Capture(this.NonRetriableException).Throw();
                    }

                    tasksToWaitOn.Remove(completedTask);
                }

                if (this.TransientExceptions.Count == 0)
                {
                    throw new ArgumentException("Account properties and NonRetriableException are null and there are no TransientExceptions.");
                }

                if (this.TransientExceptions.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(this.TransientExceptions[0]).Throw();
                }

                throw new AggregateException(this.TransientExceptions);
            }

            private async Task<AccountProperties> GetOnlyGlobalEndpointAsync()
            {
                if (!this.LimitToGlobalEndpointOnly)
                {
                    throw new ArgumentException("GetOnlyGlobalEndpointAsync should only be called if there are no other private endpoints or regions");
                }

                await this.GetAndUpdateAccountPropertiesAsync(this.DefaultEndpoint);

                if (this.AccountProperties != null)
                {
                    return this.AccountProperties;
                }

                if (this.NonRetriableException != null)
                {
                    throw this.NonRetriableException;
                }

                if (this.TransientExceptions.Count == 0)
                {
                    throw new ArgumentException("Account properties and NonRetriableException are null and there are no TransientExceptions.");
                }

                if (this.TransientExceptions.Count == 1)
                {
                    throw this.TransientExceptions[0];
                }

                throw new AggregateException(this.TransientExceptions);
            }

            /// <summary>
            /// This is done in a thread safe way to allow multiple tasks to iterate over the list of service endpoints.
            /// </summary>
            private async Task TryGetAccountPropertiesFromAllLocationsAsync()
            {
                while (this.TryMoveNextServiceEndpointhreadSafe(
                        out Uri? serviceEndpoint))
                {
                    if (serviceEndpoint == null)
                    {
                        DefaultTrace.TraceCritical("GlobalEndpointManager: serviceEndpoint is null for TryMoveNextServiceEndpointhreadSafe.");
                        return;
                    }

                    await this.GetAndUpdateAccountPropertiesAsync(endpoint: serviceEndpoint);
                }
            }

            /// <summary>
            /// We first iterate through all the private endpoints to fetch the account information.
            /// If all the attempt fails to fetch the metadata from the private endpoints, we will
            /// attempt to retrieve the account information from the regional endpoints constructed
            /// using the preferred regions list.
            /// </summary>
            /// <param name="serviceEndpoint">An instance of <see cref="Uri"/> that will contain the service endpoint.</param>
            /// <returns>A boolean flag indicating if the <see cref="ServiceEndpointEnumerator"/> was advanced in a thread safe manner.</returns>
            private bool TryMoveNextServiceEndpointhreadSafe(
                out Uri? serviceEndpoint)
            {
                if (this.CancellationTokenSource.IsCancellationRequested)
                {
                    serviceEndpoint = null;
                    return false;
                }

                lock (this.ServiceEndpointEnumerator)
                {
                    if (!this.ServiceEndpointEnumerator.MoveNext())
                    {
                        serviceEndpoint = null;
                        return false;
                    }

                    serviceEndpoint = this.ServiceEndpointEnumerator.Current;
                    return true;
                }
            }

            private async Task GetAndUpdateAccountPropertiesAsync(Uri endpoint)
            {
                try
                {
                    if (this.CancellationTokenSource.IsCancellationRequested)
                    {
                        lock (this.TransientExceptions)
                        {
                            this.TransientExceptions.Add(new OperationCanceledException($"GlobalEndpointManager: Get account information canceled for URI: {endpoint}"));
                        }

                        return;
                    }

                    AccountProperties databaseAccount = await this.GetDatabaseAccountFn(endpoint);

                    if (databaseAccount != null)
                    {
                        this.AccountProperties = databaseAccount;
                        try
                        {
                            this.CancellationTokenSource.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Ignore the exception if the cancellation token source is already disposed
                        }
                    }
                }
                catch (Exception e)
                {
                    if (DiagnosticsHandlerHelper.ShouldTrace(System.Diagnostics.TraceEventType.Information))
                    {
                        DefaultTrace.TraceInformation("GlobalEndpointManager: Fail to reach gateway endpoint {0}, {1}", endpoint, e.Message);
                    }
                    if (GetAccountPropertiesHelper.IsNonRetriableException(e))
                    {
                        DefaultTrace.TraceInformation("GlobalEndpointManager: Exception is not retriable");
                        try
                        {
                            this.CancellationTokenSource.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Ignore the exception if the cancellation token source is already disposed
                        }
                        this.NonRetriableException = e;
                    }
                    else
                    {
                        lock (this.TransientExceptions)
                        {
                            this.TransientExceptions.Add(e);
                        }
                    }
                }
            }

            private static bool IsNonRetriableException(Exception exception)
            {
                if (exception is DocumentClientException dce && 
                    (dce.StatusCode == HttpStatusCode.Unauthorized || dce.StatusCode == HttpStatusCode.Forbidden))
                {
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Returns an instance of <see cref="IEnumerable{Uri}"/> containing the private and regional service endpoints to iterate over.
            /// </summary>
            /// <param name="defaultEndpoint">An instance of <see cref="Uri"/> containing the default global endpoint.</param>
            /// <param name="locations">An instance of <see cref="IList{T}"/> containing the preferred serviceEndpoint names.</param>
            /// <param name="accountInitializationCustomEndpoints">An instance of <see cref="IList{T}"/> containing the custom private endpoints.</param>
            /// <returns>An instance of <see cref="IEnumerator{T}"/> containing the service endpoints.</returns>
            private static IEnumerable<Uri> GetServiceEndpoints(
                Uri defaultEndpoint,
                IList<string>? locations,
                IList<Uri>? accountInitializationCustomEndpoints)
            {
                // We first iterate over all the private endpoints and yield return them.
                if (accountInitializationCustomEndpoints?.Count > 0)
                {
                    foreach (Uri customEndpoint in accountInitializationCustomEndpoints)
                    {
                        // Yield return all of the custom private endpoints first.
                        yield return customEndpoint;
                    }
                }

                // The next step is to iterate over the preferred locations, construct and yield return the regional endpoints one by one.
                // The regional endpoints will be constructed by appending the preferred region name as a suffix to the default global endpoint.
                if (locations?.Count > 0)
                {
                    foreach (string location in locations)
                    {
                        // Yield return all of the regional endpoints once the private custom endpoints are visited.
                        yield return LocationHelper.GetLocationEndpoint(defaultEndpoint, location);
                    }
                }
            }
            public void Dispose()
            {
                // Dispose of unmanaged resources.
                this.Dispose(true);
                // Suppress finalization.
                GC.SuppressFinalize(this);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (Interlocked.Increment(ref this.disposeCounter) != 1)
                {
                    return;
                }

                if (disposing)
                {
                    try
                    {
                        this.CancellationTokenSource?.Cancel();
                        this.CancellationTokenSource?.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignore exceptions during dispose
                    }

                }

            }

        }

        public virtual Uri ResolveServiceEndpoint(DocumentServiceRequest request)
        {
            // For PPAF write hedging in single-master: route to read endpoints
            // when ExcludeRegions is set to allow failover to read regions.
            // The AZURE_COSMOS_PPAF_WRITE_HEDGING_ENABLED env var (default true) is checked last so it
            // only evaluates for the single-master PPAF write path this feature targets.
            if (this.connectionPolicy.EnablePartitionLevelFailover
                && request.OperationType.IsWriteOperation()
                && !this.locationCache.CanUseMultipleWriteLocations(request)
                && request.RequestContext?.ExcludeRegions != null
                && request.RequestContext.ExcludeRegions.Count > 0
                && Microsoft.Azure.Cosmos.ConfigurationManager.IsPpafWriteHedgingEnabled())
            {
                ReadOnlyCollection<Uri> readEndpoints = this.locationCache.GetApplicableEndpoints(request, isReadRequest: true);
                int locationIndex = request.RequestContext.LocationIndexToRoute.GetValueOrDefault(0);
                Uri endpoint = readEndpoints[locationIndex % readEndpoints.Count];
                request.RequestContext.RouteToLocation(endpoint);
                return endpoint;
            }

            return this.locationCache.ResolveServiceEndpoint(request);
        }

        /// <summary>
        /// Gets the default endpoint of the account
        /// </summary>
        /// <returns>the default endpoint.</returns>
        public Uri GetDefaultEndpoint()
        {
            return this.locationCache.GetDefaultEndpoint();
        }

        /// <summary>
        /// Gets the mapping of available write region names to the respective endpoints
        /// </summary>
        public ReadOnlyDictionary<string, Uri> GetAvailableWriteEndpointsByLocation()
        {
            return this.locationCache.GetAvailableWriteEndpointsByLocation();
        }

        /// <summary>
        /// Gets the mapping of available read region names to the respective endpoints
        /// </summary>
        public ReadOnlyDictionary<string, Uri> GetAvailableReadEndpointsByLocation()
        {
            return this.locationCache.GetAvailableReadEndpointsByLocation();
        }

        /// <summary>
        /// Returns serviceEndpoint corresponding to the endpoint
        /// </summary>
        /// <param name="endpoint"></param>
        public string GetLocation(Uri endpoint)
        {
            return this.locationCache.GetLocation(endpoint);
        }

        public ReadOnlyCollection<Uri> GetApplicableEndpoints(DocumentServiceRequest request, bool isReadRequest)
        {
            return this.locationCache.GetApplicableEndpoints(request, isReadRequest);
        }

        public ReadOnlyCollection<string> GetApplicableRegions(IEnumerable<string> excludeRegions, bool isReadRequest)
        {
            return this.locationCache.GetApplicableRegions(excludeRegions, isReadRequest);
        }

        public ReadOnlyCollection<string> GetApplicableAccountLevelReadRegions(IEnumerable<string> excludeRegions)
        {
            return this.locationCache.GetApplicableAccountLevelReadRegions(excludeRegions);
        }

        public bool TryGetLocationForGatewayDiagnostics(Uri endpoint, out string regionName)
        {
            return this.locationCache.TryGetLocationForGatewayDiagnostics(endpoint, out regionName);
        }

        public virtual void MarkEndpointUnavailableForRead(Uri endpoint)
        {
            DefaultTrace.TraceInformation("GlobalEndpointManager: Marking endpoint {0} unavailable for read", endpoint);

            this.locationCache.MarkEndpointUnavailableForRead(endpoint);
        }

        public virtual void MarkEndpointUnavailableForWrite(Uri endpoint)
        {
            DefaultTrace.TraceInformation("GlobalEndpointManager: Marking endpoint {0} unavailable for Write", endpoint);

            this.locationCache.MarkEndpointUnavailableForWrite(endpoint);
        }

        public bool CanUseMultipleWriteLocations(DocumentServiceRequest request)
        {
            return this.locationCache.CanUseMultipleWriteLocations(request);
        }

        public void Dispose()
        {
            this.connectionPolicy.PreferenceChanged -= this.OnPreferenceChanged;

            this.thinClientProbeClient?.Dispose();

            if (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    // This can cause task canceled exceptions if the user disposes of the object while awaiting an async call.
                    this.cancellationTokenSource.Cancel();
                    // The background timer task can hit a ObjectDisposedException but it's an async background task
                    // that is never awaited on so it will not be thrown back to the caller.
                    this.cancellationTokenSource.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Ignore the exception if the cancellation token source is already disposed

                }
            }
        }

        /// <summary>
        /// Parse thinClientWritableLocations / thinClientReadableLocations from AdditionalProperties. 
        /// </summary>
        private static void ParseThinClientLocationsFromAdditionalProperties(AccountProperties databaseAccount)
        {
            if (databaseAccount?.AdditionalProperties != null)
            {
                if (databaseAccount.AdditionalProperties.TryGetValue("thinClientWritableLocations", out JToken writableToken)
                    && writableToken is JArray writableArray)
                {
                    databaseAccount.ThinClientWritableLocationsInternal = ParseAccountRegionArray(writableArray);
                }

                if (databaseAccount.AdditionalProperties.TryGetValue("thinClientReadableLocations", out JToken readableToken)
                    && readableToken is JArray readableArray)
                {
                    databaseAccount.ThinClientReadableLocationsInternal = ParseAccountRegionArray(readableArray);
                }
            }
        }

        private static Collection<AccountRegion> ParseAccountRegionArray(JArray array)
        {
            Collection<AccountRegion> result = new Collection<AccountRegion>();
            foreach (JToken token in array)
            {
                if (token is not JObject obj)
                {
                    continue;
                }

                string? regionName = obj["name"]?.ToString();
                string? endpointStr = obj["databaseAccountEndpoint"]?.ToString();

                if (!string.IsNullOrEmpty(regionName) && !string.IsNullOrEmpty(endpointStr))
                {
                    result.Add(new AccountRegion
                    {
                        Name = regionName,
                        Endpoint = endpointStr
                    });
                }
            }
            return result;
        }

        public virtual void InitializeAccountPropertiesAndStartBackgroundRefresh(AccountProperties databaseAccount)
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (!this.connectionPolicy.DisablePartitionLevelFailoverClientLevelOverride && databaseAccount.EnablePartitionLevelFailover.HasValue)
            {
                this.connectionPolicy.EnablePartitionLevelFailover = databaseAccount.EnablePartitionLevelFailover.Value;
            }

            // Capture initial disableCrossRegionalHedging baseline so the change-event only fires on
            // subsequent transitions, not on the first observation.
            this.lastKnownDisableCrossRegionalHedging = databaseAccount.DisableCrossRegionalHedging ?? false;

            // Capture the initial EnablePartitionLevelFailover baseline from the effective
            // (post-client-override) value applied just above, so the change-event only fires on
            // subsequent transitions, not on the first observation.
            this.lastKnownEnablePartitionLevelFailover = this.connectionPolicy.EnablePartitionLevelFailover;

            GlobalEndpointManager.ParseThinClientLocationsFromAdditionalProperties(databaseAccount);

            this.locationCache.OnDatabaseAccountRead(databaseAccount);

            _ = this.RunThinClientProbeCycleAsync();

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
                this.StartLocationBackgroundRefreshLoop();
            }
            catch
            {
                this.isBackgroundAccountRefreshActive = false;
                throw;
            }
        }

        public virtual async Task RefreshLocationAsync(bool forceRefresh = false)
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            await this.RefreshDatabaseAccountInternalAsync(forceRefresh: forceRefresh);
        }

        /// <summary>
        /// Determines whether the current configuration and state of the service allow for supporting multiple write locations.
        /// This method returns True is the AvailableWriteLocations in LocationCache is more than 1. Otherwise, it returns False.
        /// </summary>
        /// <param name="resourceType"> resource type of the request</param>
        /// <param name="operationType"> operation type of the request</param>
        /// <returns>A boolean flag indicating if the available write locations are more than one.</returns>
        public bool CanSupportMultipleWriteLocations(
            ResourceType resourceType,
            OperationType operationType)
        {
            return this.locationCache.CanUseMultipleWriteLocations()
                && this.locationCache.GetAvailableAccountLevelWriteLocations()?.Count > 1
                && (resourceType == ResourceType.Document ||
                (resourceType == ResourceType.StoredProcedure && operationType == OperationType.Execute));
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void StartLocationBackgroundRefreshLoop()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            DefaultTrace.TraceInformation("GlobalEndpointManager: StartLocationBackgroundRefreshWithTimer() refreshing locations");

            if (!this.locationCache.ShouldRefreshEndpoints(out bool canRefreshInBackground))
            {
                if (!canRefreshInBackground)
                {
                    DefaultTrace.TraceInformation("GlobalEndpointManager: StartLocationBackgroundRefreshWithTimer() stropped.");
                    lock (this.backgroundAccountRefreshLock)
                    {
                        this.isBackgroundAccountRefreshActive = false;
                    }

                    return;
                }
            }

            try
            {
                await Task.Delay(this.backgroundRefreshLocationTimeIntervalInMS, this.cancellationTokenSource.Token);

                DefaultTrace.TraceInformation("GlobalEndpointManager: StartLocationBackgroundRefreshWithTimer() - Invoking refresh");

                if (this.cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                await this.RefreshDatabaseAccountInternalAsync(forceRefresh: false);
            }
            catch (Exception ex)
            {
                if (this.cancellationTokenSource.IsCancellationRequested && (ex is OperationCanceledException || ex is ObjectDisposedException))
                {
                    return;
                }
                
                if (DiagnosticsHandlerHelper.ShouldTrace(System.Diagnostics.TraceEventType.Critical))
                {
                    DefaultTrace.TraceCritical("GlobalEndpointManager: StartLocationBackgroundRefreshWithTimer() - Unable to refresh database account from any serviceEndpoint. Exception: {0}", ex.Message);
                }
            }

            // Call itself to create a loop to continuously do background refresh every 5 minutes
            this.StartLocationBackgroundRefreshLoop();
        }

        private Task<AccountProperties> GetDatabaseAccountAsync(Uri serviceEndpoint)
        {
            return this.owner.GetDatabaseAccountInternalAsync(serviceEndpoint, this.cancellationTokenSource.Token);
        }

        private void OnPreferenceChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.locationCache.OnLocationPreferenceChanged(new ReadOnlyCollection<string>(
                this.connectionPolicy.PreferredLocations));
        }

        /// <summary>
        /// Thread safe refresh account and serviceEndpoint info.
        /// </summary>
        private async Task RefreshDatabaseAccountInternalAsync(bool forceRefresh)
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (this.SkipRefresh(forceRefresh))
            {
                return;
            }
            
            lock (this.isAccountRefreshInProgressLock)
            {
                // Check again if should refresh after obtaining the lock
                if (this.SkipRefresh(forceRefresh))
                {
                    return;
                }

                // If the refresh is already in progress just return. No reason to do another refresh.
                if (this.isAccountRefreshInProgress)
                {
                    return;
                }

                this.isAccountRefreshInProgress = true;
            }

            try
            {
                this.LastBackgroundRefreshUtc = DateTime.UtcNow;
                AccountProperties accountProperties = await this.GetDatabaseAccountAsync(true);

                bool ignorePpafChanges = this.connectionPolicy.DisablePartitionLevelFailoverClientLevelOverride;

                bool ppafEnablementChanged = !ignorePpafChanges
                    && accountProperties.EnablePartitionLevelFailover.HasValue
                    && (this.lastKnownEnablePartitionLevelFailover != accountProperties.EnablePartitionLevelFailover.Value);

                // Hedging change-detection mirrors the PPAF .HasValue guard above:
                // a missing property in the response is "no signal", NOT an implicit false.
                // This prevents a transient gateway response that drops the property
                // (e.g., partial regional failover, stale gateway version) from being
                // interpreted as a true -> false transition that re-enables hedging
                // during the very window the operator most wants it disabled.
                //
                // Runbook contract: on-call disables via an explicit "false" property
                // value, not by removing the property override.
                bool disableHedgingFlagChanged = !ignorePpafChanges
                    && accountProperties.DisableCrossRegionalHedging.HasValue
                    && (accountProperties.DisableCrossRegionalHedging.Value != this.lastKnownDisableCrossRegionalHedging);

                if (ppafEnablementChanged || disableHedgingFlagChanged)
                {
                    bool latestPpafEnabled = accountProperties.EnablePartitionLevelFailover
                        ?? this.lastKnownEnablePartitionLevelFailover;

                    // Only advance lastKnown when the gateway emitted an explicit value; otherwise
                    // preserve the cached value so a later property-restored response diffs against
                    // the previously-honored state (rather than against an implicit false baseline).
                    bool latestDisableHedging = accountProperties.DisableCrossRegionalHedging
                        ?? this.lastKnownDisableCrossRegionalHedging;

                    bool previousPpafEnabled = this.lastKnownEnablePartitionLevelFailover;
                    bool previousDisableHedging = this.lastKnownDisableCrossRegionalHedging;
                    this.lastKnownEnablePartitionLevelFailover = latestPpafEnabled;
                    this.lastKnownDisableCrossRegionalHedging = latestDisableHedging;
                    try
                    {
                        this.OnEnablePartitionLevelFailoverConfigChanged?.Invoke(latestPpafEnabled, latestDisableHedging);
                    }
                    catch
                    {
                        // Restore both baselines so the next refresh re-detects and retries the missed
                        // transition rather than diffing against an already-advanced value and going silent.
                        // The subscriber reverts its own cached state in tandem (see
                        // DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh).
                        this.lastKnownEnablePartitionLevelFailover = previousPpafEnabled;
                        this.lastKnownDisableCrossRegionalHedging = previousDisableHedging;
                        throw;
                    }
                }

                GlobalEndpointManager.ParseThinClientLocationsFromAdditionalProperties(accountProperties);

                this.locationCache.OnDatabaseAccountRead(accountProperties);

                // Probe the thin client regional endpoints after every account-topology refresh so the
                // routing gate reflects the latest proxy connectivity health. Fire-and-forget (not awaited):
                // this method is shared with the forced-refresh path that ClientRetryPolicy invokes inline on
                // a request's failover retry, and an unreachable endpoint can take seconds to time out. The
                // probe is self-guarded (never throws, single-flight, permanent success cache) and the next
                // dispatch safely falls back to Gateway V1 until a region is confirmed healthy, so there is no
                // need to block the refresh on it.
                _ = this.RunThinClientProbeCycleAsync();
            }
            catch (Exception ex)
            {
                if (DiagnosticsHandlerHelper.ShouldTrace(System.Diagnostics.TraceEventType.Warning))
                {
                    DefaultTrace.TraceWarning("Failed to refresh database account with exception: {0}. Activity Id: '{1}'",
                        ex.Message,
                        System.Diagnostics.Trace.CorrelationManager.ActivityId);
                }
            }
            finally
            {
                lock (this.isAccountRefreshInProgressLock)
                {
                    this.isAccountRefreshInProgress = false;
                }
            }
        }

        internal async Task<AccountProperties> GetDatabaseAccountAsync(bool forceRefresh = false)
        {
#nullable disable  // Needed because AsyncCache does not have nullable enabled
            return await this.databaseAccountCache.GetAsync(
                              key: string.Empty,
                              obsoleteValue: null,
                              singleValueInitFunc: () => GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                                  this.defaultEndpoint,
                                  this.GetEffectivePreferredLocations(),
                                  this.connectionPolicy.AccountInitializationCustomEndpoints,
                                  this.GetDatabaseAccountAsync,
                                  this.cancellationTokenSource.Token),
                              cancellationToken: this.cancellationTokenSource.Token,
                              forceRefresh: forceRefresh);
#nullable enable
        }

        /// <summary>
        /// If the account is currently refreshing or the last refresh occurred less than the minimum time
        /// just return. This is used to avoid refreshing to often and preventing to much pressure on the gateway.
        /// </summary>
        private bool SkipRefresh(bool forceRefresh)
        {
            TimeSpan timeSinceLastRefresh = DateTime.UtcNow - this.LastBackgroundRefreshUtc;
            return (this.isAccountRefreshInProgress || this.MinTimeBetweenAccountRefresh > timeSinceLastRefresh)
                && !forceRefresh;
        }

        public IList<string> GetEffectivePreferredLocations()
        {
            if (this.connectionPolicy.PreferredLocations != null && this.connectionPolicy.PreferredLocations.Count > 0)
            {
                return this.connectionPolicy.PreferredLocations;
            }

            return this.connectionPolicy.PreferredLocations?.Count > 0 ? 
                this.connectionPolicy.PreferredLocations : this.locationCache.EffectivePreferredLocations;
        }

        public Uri ResolveThinClientEndpoint(DocumentServiceRequest request)
        {
            return this.locationCache.ResolveThinClientEndpoint(request, request.IsReadOnlyRequest);
        }

        /// <summary>
        /// Wires the thin client HTTP/2 <see cref="CosmosHttpClient"/> used by the connectivity probe. Must run
        /// before the first topology refresh. Never trips client construction: if the probe client cannot be
        /// created it is left null and <see cref="IsProxyEndpointHealthy"/> fails closed, so all traffic uses
        /// Gateway V1 until a probe client is wired and an endpoint is confirmed healthy.
        /// </summary>
        public void SetThinClientHttpClient(CosmosHttpClient httpClient)
        {
            if (httpClient == null)
            {
                return;
            }

            try
            {
                this.thinClientProbeClient ??= new EndpointProbeClient(httpClient);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning(
                    "Failed to wire thin client connectivity-probe client; thin client routing will stay on Gateway V1. Exception: {0}",
                    ex.Message);
            }
        }

        /// <summary>
        /// Runs one connectivity-probe cycle against the thin client regional endpoints from the most recent
        /// topology refresh, populating the per-endpoint success cache. No-op when no probe client is wired or
        /// the account advertises no thin client read locations. Only endpoints not yet cached healthy are
        /// probed. Never throws; probe failures must not fail topology refresh.
        /// </summary>
        public async Task RunThinClientProbeCycleAsync()
        {
            EndpointProbeClient? probeClient = this.thinClientProbeClient;
            if (probeClient == null || !this.locationCache.HasThinClientReadLocations)
            {
                return;
            }

            try
            {
                HashSet<Uri> endpoints = this.locationCache.GetThinClientRegionalEndpoints();
                if (endpoints.Count == 0)
                {
                    return;
                }

                await probeClient.RunProbeCycleAsync(endpoints, this.cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning(
                    "Thin client probe cycle threw; ignoring to protect topology refresh. Exception: {0}",
                    ex.Message);
            }
        }
    }
}
