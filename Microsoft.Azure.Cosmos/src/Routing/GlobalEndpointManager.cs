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
    using Microsoft.Azure.Documents;

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
        private readonly AsyncCache<string, AccountProperties> databaseAccountCache = new AsyncCache<string, AccountProperties>();
        private readonly TimeSpan MinTimeBetweenAccountRefresh = TimeSpan.FromSeconds(15);
        private readonly int backgroundRefreshLocationTimeIntervalInMS = GlobalEndpointManager.DefaultBackgroundRefreshLocationTimeIntervalInMS;
        private readonly object backgroundAccountRefreshLock = new object();
        private readonly object isAccountRefreshInProgressLock = new object();
        private bool isAccountRefreshInProgress = false;
        private bool isBackgroundAccountRefreshActive = false;
        private DateTime LastBackgroundRefreshUtc = DateTime.MinValue;

        public GlobalEndpointManager(IDocumentClientInternal owner, ConnectionPolicy connectionPolicy)
        {
            this.locationCache = new LocationCache(
                new ReadOnlyCollection<string>(connectionPolicy.PreferredLocations),
                owner.ServiceEndpoint,
                connectionPolicy.EnableEndpointDiscovery,
                connectionPolicy.MaxConnectionLimit,
                connectionPolicy.UseMultipleWriteLocations);

            this.owner = owner;
            this.defaultEndpoint = owner.ServiceEndpoint;
            this.connectionPolicy = connectionPolicy;

            this.connectionPolicy.PreferenceChanged += this.OnPreferenceChanged;

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
                        this.CancellationTokenSource.Cancel();
                    }
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceInformation("GlobalEndpointManager: Fail to reach gateway endpoint {0}, {1}", endpoint, e.ToString());
                    if (GetAccountPropertiesHelper.IsNonRetriableException(e))
                    {
                        DefaultTrace.TraceInformation("GlobalEndpointManager: Exception is not retriable");
                        this.CancellationTokenSource.Cancel();
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
                if (Interlocked.Increment(ref this.disposeCounter) == 1)
                {
                    this.CancellationTokenSource?.Cancel();
                    this.CancellationTokenSource?.Dispose();
                }
            }
        }

        public virtual Uri ResolveServiceEndpoint(DocumentServiceRequest request)
        {
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
            if (!this.cancellationTokenSource.IsCancellationRequested)
            {
                // This can cause task canceled exceptions if the user disposes of the object while awaiting an async call.
                this.cancellationTokenSource.Cancel();
                // The background timer task can hit a ObjectDisposedException but it's an async background task
                // that is never awaited on so it will not be thrown back to the caller.
                this.cancellationTokenSource.Dispose();
            }
        }

        public virtual void InitializeAccountPropertiesAndStartBackgroundRefresh(AccountProperties databaseAccount)
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            this.locationCache.OnDatabaseAccountRead(databaseAccount);

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
                
                DefaultTrace.TraceCritical("GlobalEndpointManager: StartLocationBackgroundRefreshWithTimer() - Unable to refresh database account from any serviceEndpoint. Exception: {0}", ex.ToString());
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

                this.locationCache.OnDatabaseAccountRead(accountProperties);

            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Failed to refresh database account with exception: {0}. Activity Id: '{1}'",
                    ex,
                    System.Diagnostics.Trace.CorrelationManager.ActivityId);
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
    }
}
