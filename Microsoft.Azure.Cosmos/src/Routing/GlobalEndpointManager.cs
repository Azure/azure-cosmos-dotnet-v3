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
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly LocationCache locationCache;
        private readonly Uri defaultEndpoint;
        private readonly ConnectionPolicy connectionPolicy;
        private readonly IDocumentClientInternal owner;
        private readonly object refreshLock;
        private readonly AsyncCache<string, AccountProperties> databaseAccountCache;
        private readonly int backgroundRefreshLocationTimeIntervalInMS = GlobalEndpointManager.DefaultBackgroundRefreshLocationTimeIntervalInMS;
        private bool isRefreshing;

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
            this.databaseAccountCache = new AsyncCache<string, AccountProperties>();

            this.connectionPolicy.PreferenceChanged += this.OnPreferenceChanged;

            this.isRefreshing = false;
            this.refreshLock = new object();
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
        }

        public ReadOnlyCollection<Uri> ReadEndpoints => this.locationCache.ReadEndpoints;

        public ReadOnlyCollection<Uri> WriteEndpoints => this.locationCache.WriteEndpoints;

        public int PreferredLocationCount => this.connectionPolicy.PreferredLocations != null ? this.connectionPolicy.PreferredLocations.Count : 0;

        /// <summary>
        /// This will get the account information.
        /// It will try the global endpoint first. 
        /// If no response in 5 seconds it will create 2 additional tasks
        /// The 2 additional tasks will go through all the preferred regions in parallel
        /// It will return the first success and stop the parallel tasks.
        /// </summary>
        public static Task<AccountProperties> GetDatabaseAccountFromAnyLocationsAsync(
            Uri defaultEndpoint,
            IList<string>? locations,
            Func<Uri, Task<AccountProperties>> getDatabaseAccountFn)
        {
            GetAccountPropertiesHelper threadSafeGetAccountHelper = new GetAccountPropertiesHelper(
               defaultEndpoint,
               locations?.GetEnumerator(),
               getDatabaseAccountFn);

            return threadSafeGetAccountHelper.GetAccountPropertiesAsync();
        }

        /// <summary>
        /// This is a helper class to 
        /// </summary>
        private class GetAccountPropertiesHelper
        {
            private readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
            private readonly Uri DefaultEndpoint;
            private readonly IEnumerator<string>? Locations;
            private readonly Func<Uri, Task<AccountProperties>> GetDatabaseAccountFn;
            private AccountProperties? AccountProperties = null;
            private Exception? NonRetriableException = null;
            private Exception? LastTransientException = null;

            public GetAccountPropertiesHelper(
                Uri defaultEndpoint,
                IEnumerator<string>? locations,
                Func<Uri, Task<AccountProperties>> getDatabaseAccountFn)
            {
                this.DefaultEndpoint = defaultEndpoint;
                this.Locations = locations;
                this.GetDatabaseAccountFn = getDatabaseAccountFn;
            }

            public async Task<AccountProperties> GetAccountPropertiesAsync()
            {
                // If there are no preferred regions then just wait for the global endpoint results
                if (this.Locations == null)
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
                    throw this.NonRetriableException;
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
                        throw this.NonRetriableException;
                    }

                    tasksToWaitOn.Remove(completedTask);
                }

                if (this.LastTransientException == null)
                {
                    throw new ArgumentException("Account properties and NonRetriableException are null and there is no LastTransientException.");
                }

                throw this.LastTransientException;
            }

            private async Task<AccountProperties> GetOnlyGlobalEndpointAsync()
            {
                if (this.Locations != null)
                {
                    throw new ArgumentException("GetOnlyGlobalEndpointAsync should only be called if there are no other regions");
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

                if (this.LastTransientException != null)
                {
                    throw this.LastTransientException;
                }

                throw new ArgumentException("The account properties and exceptions are null");
            }

            /// <summary>
            /// This is done in a thread safe way to allow multiple tasks to iterate over the 
            /// list of locations.
            /// </summary>
            private async Task TryGetAccountPropertiesFromAllLocationsAsync()
            {
                while (this.TryMoveNextLocationThreadSafe(
                        out string? location))
                {
                    if (location == null)
                    {
                        DefaultTrace.TraceCritical("location is null for TryMoveNextLocationThreadSafe");
                        return;
                    }

                    await this.TryGetAccountPropertiesFromRegionalEndpointsAsync(location);
                }
            }

            private bool TryMoveNextLocationThreadSafe(
                out string? location)
            {
                if (this.CancellationTokenSource.IsCancellationRequested
                    || this.Locations == null)
                {
                    location = null;
                    return false;
                }

                lock (this.Locations)
                {
                    if (!this.Locations.MoveNext())
                    {
                        location = null;
                        return false;
                    }

                    location = this.Locations.Current;
                    return true;
                }
            }

            private Task TryGetAccountPropertiesFromRegionalEndpointsAsync(string location)
            {
                return this.GetAndUpdateAccountPropertiesAsync(
                    LocationHelper.GetLocationEndpoint(this.DefaultEndpoint, location));
            }

            private async Task GetAndUpdateAccountPropertiesAsync(Uri endpoint)
            {
                try
                {
                    if (this.CancellationTokenSource.IsCancellationRequested)
                    {
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
                    DefaultTrace.TraceInformation("Fail to reach gateway endpoint {0}, {1}", endpoint, e.ToString());
                    if (GetAccountPropertiesHelper.IsNonRetriableException(e))
                    {
                        DefaultTrace.TraceInformation("Exception is not retriable");
                        this.CancellationTokenSource.Cancel();
                        this.NonRetriableException = e;
                    }
                    else
                    {
                        this.LastTransientException = e;
                    }
                }
            }

            private static bool IsNonRetriableException(Exception exception)
            {
                if (exception is DocumentClientException dce && dce.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return true;
                }

                return false;
            }
        }

        public virtual Uri ResolveServiceEndpoint(DocumentServiceRequest request)
        {
            return this.locationCache.ResolveServiceEndpoint(request);
        }

        /// <summary>
        /// Returns location corresponding to the endpoint
        /// </summary>
        /// <param name="endpoint"></param>
        public string GetLocation(Uri endpoint)
        {
            return this.locationCache.GetLocation(endpoint);
        }

        public virtual void MarkEndpointUnavailableForRead(Uri endpoint)
        {
            DefaultTrace.TraceInformation("Marking endpoint {0} unavailable for read", endpoint);

            this.locationCache.MarkEndpointUnavailableForRead(endpoint);
        }

        public virtual void MarkEndpointUnavailableForWrite(Uri endpoint)
        {
            DefaultTrace.TraceInformation("Marking endpoint {0} unavailable for Write", endpoint);

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

        public virtual async Task RefreshLocationAsync(AccountProperties databaseAccount, bool forceRefresh = false)
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (forceRefresh)
            {
                AccountProperties refreshedDatabaseAccount = await this.RefreshDatabaseAccountInternalAsync();

                this.locationCache.OnDatabaseAccountRead(refreshedDatabaseAccount);
                return;
            }

            lock (this.refreshLock)
            {
                if (this.isRefreshing)
                {
                    return;
                }

                this.isRefreshing = true;
            }

            try
            {
                await this.RefreshLocationPrivateAsync(databaseAccount);
            }
            catch
            {
                this.isRefreshing = false;
                throw;
            }
        }

        private async Task RefreshLocationPrivateAsync(AccountProperties databaseAccount)
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            DefaultTrace.TraceInformation("RefreshLocationAsync() refreshing locations");

            if (databaseAccount != null)
            {
                this.locationCache.OnDatabaseAccountRead(databaseAccount);
            }

            if (this.locationCache.ShouldRefreshEndpoints(out bool canRefreshInBackground))
            {
                if (databaseAccount == null && !canRefreshInBackground)
                {
                    databaseAccount = await this.RefreshDatabaseAccountInternalAsync();

                    this.locationCache.OnDatabaseAccountRead(databaseAccount);
                }

                this.StartRefreshLocationTimer();
            }
            else
            {
                this.isRefreshing = false;
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void StartRefreshLocationTimer()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(this.backgroundRefreshLocationTimeIntervalInMS, this.cancellationTokenSource.Token);

                DefaultTrace.TraceInformation("StartRefreshLocationTimerAsync() - Invoking refresh");

                AccountProperties databaseAccount = await this.RefreshDatabaseAccountInternalAsync();

                await this.RefreshLocationPrivateAsync(databaseAccount);
            }
            catch (Exception ex)
            {
                if (this.cancellationTokenSource.IsCancellationRequested && (ex is TaskCanceledException || ex is ObjectDisposedException))
                {
                    return;
                }

                DefaultTrace.TraceCritical("StartRefreshLocationTimerAsync() - Unable to refresh database account from any location. Exception: {0}", ex.ToString());

                this.StartRefreshLocationTimer();
            }
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

        private Task<AccountProperties> RefreshDatabaseAccountInternalAsync()
        {
#nullable disable // Needed because AsyncCache does not have nullable enabled
            return this.databaseAccountCache.GetAsync(
                key: string.Empty,
                obsoleteValue: null,
                singleValueInitFunc: () => GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                    this.defaultEndpoint,
                    this.connectionPolicy.PreferredLocations,
                    this.GetDatabaseAccountAsync),
                cancellationToken: this.cancellationTokenSource.Token,
                forceRefresh: true);
#nullable enable
        }
    }
}
