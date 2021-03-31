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
    internal class GlobalEndpointManager : IDisposable
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
        /// This gets the account information
        /// 
        /// Source Task        
        /// Creates Task 1,2 ->                |    Task 1                         |    Task 2          |                 
        ///                                    | Global endpoint -> 10 sec -> fail | Timer wait 5 sec   |
        /// Waits for Any on (Task1, Task2)    | still waiting on response         | Timer is done      |
        /// Creates Task3, Task4                                                                        |     Task 3                                |     Task 4                                 |   
        ///                                                                                             | 1st preferred location -> 10 sec -> fail  | 2nd preferred location -> 2 sec -> success |
        ///                                                                                             | still waiting on response                 | returns success                            |
        /// Waits for Any on (Task1, Task3, Task 4). Task 4 is done return the account information.
        /// Other tasks log the exception or just ignore the response
        /// </summary>
        public static async Task<AccountProperties> GetDatabaseAccountFromAnyLocationsAsync(
            Uri defaultEndpoint,
            IList<string> locations,
            Func<Uri, Task<AccountProperties>> getDatabaseAccountFn)
        {
            Task<(AccountProperties? accountProperties, Exception? globalException)> globalEndpointTask = ThreadSafeGetAccountHelper.TryGetAccountPropertiesAsync(
                defaultEndpoint,
                getDatabaseAccountFn);

            // If no preferred regions are set then just await the global endpoint task.
            if (locations == null || !locations.Any())
            {
                (AccountProperties? globalAccountProperties, Exception? globalException) = await globalEndpointTask;
                if (globalAccountProperties != null)
                {
                    return globalAccountProperties;
                }

                if (globalException == null)
                {
                    throw new ArgumentException("The account properties and exception are both null");
                }

                throw globalException;
            }

            // Start a timer to start secondary requests in parallel.
            Task timerTask = Task.Delay(TimeSpan.FromSeconds(5));
            await Task.WhenAny(globalEndpointTask, timerTask);

            // If the global endpoint finishes first return the results or throw the exception if it is not retriable
            bool isGlobalEndpointTaskRunning = true;
            if (globalEndpointTask.IsCompleted)
            {
                (AccountProperties? globalAccountProperties, Exception? globalException) = await globalEndpointTask;
                if (globalAccountProperties != null)
                {
                    return globalAccountProperties;
                }

                if (globalException == null)
                {
                    throw new ArgumentException("The global account properties and exception are both null");
                }

                if (IsNonRetriableException(globalException))
                {
                    throw globalException;
                }

                isGlobalEndpointTaskRunning = false;
            }

            DefaultTrace.TraceInformation("GlobalEndpoint did not respond within 5 seconds. Trying local regions. {0}", DateTime.UtcNow);
            // The timer task completed first. Try going to the preferred region list in 2 tasks to reduce latency.
            // The reason to do 2 tasks in parallel is it not possible to determine which region the global endpoint
            // is pointing to. By having 2 tasks it if 1 of the tasks gets stuck on the same region the other task will continue with the other regions.
            HashSet<Task<(AccountProperties? accountProperties, Exception? exception)>> tasksToWaitOn = new HashSet<Task<(AccountProperties?, Exception?)>>();

            // It's possible the globalEndpointTask completed between now and the previous check
            // so use a flag to make sure it is included if it was not previously checked.
            if (isGlobalEndpointTaskRunning)
            {
                tasksToWaitOn.Add(globalEndpointTask);
            }

            ThreadSafeGetAccountHelper threadSafeGetAccountHelper = new ThreadSafeGetAccountHelper(
                defaultEndpoint,
                locations.GetEnumerator(),
                getDatabaseAccountFn);

            // This creates two thread safe tasks to go to through the list of preferred regions
            tasksToWaitOn.Add(threadSafeGetAccountHelper.TryGetAccountPropertiesFromAllLocationsAsync());
            tasksToWaitOn.Add(threadSafeGetAccountHelper.TryGetAccountPropertiesFromAllLocationsAsync());

            Exception? lastException = null;
            while (tasksToWaitOn.Any())
            {
                Task<(AccountProperties? accountProperties, Exception? exception)> completedTask = await Task.WhenAny(tasksToWaitOn);
                (AccountProperties? accountProperties, Exception? exception) = completedTask.Result;
                if (accountProperties != null)
                {
                    return accountProperties;
                }

                if (exception == null)
                {
                    throw new ArgumentException("The account properties and exception are both null");
                }

                if (IsNonRetriableException(exception))
                {
                    throw exception;
                }

                lastException = exception;
                tasksToWaitOn.Remove(completedTask);
            }

            if (lastException == null)
            {
                throw new ArgumentException("None of the tasks completed successfully and there is no last exception to throw");
            }

            throw lastException;
        }

        private class ThreadSafeGetAccountHelper
        {
            private readonly Uri DefaultEndpoint;
            private readonly IEnumerator<string> Locations;
            private readonly Func<Uri, Task<AccountProperties>> GetDatabaseAccountFn;
            private AccountProperties? AccountProperties = null;

            public ThreadSafeGetAccountHelper(
                Uri defaultEndpoint,
                IEnumerator<string> locations,
                Func<Uri, Task<AccountProperties>> getDatabaseAccountFn)
            {
                this.DefaultEndpoint = defaultEndpoint;
                this.Locations = locations;
                this.GetDatabaseAccountFn = getDatabaseAccountFn;
            }

            public async Task<(AccountProperties?, Exception?)> TryGetAccountPropertiesFromAllLocationsAsync()
            {
                Exception? lastException = null;
                while (this.TryMoveNextLocationThreadSafe(
                        out string? location))
                {
                    if (this.AccountProperties != null)
                    {
                        return (this.AccountProperties, null);
                    }

                    if (location == null)
                    {
                        throw new ArgumentNullException(nameof(location));
                    }

                    (AccountProperties? accountProperties, Exception? exception) = await this.TryGetAccountPropertiesFromRegionalEndpointsAsync(location);
                    if (accountProperties != null)
                    {
                        this.AccountProperties = accountProperties;
                        return (accountProperties, null);
                    }

                    if (exception == null)
                    {
                        throw new ArgumentException("Account properties and exception are null");
                    }

                    lastException = exception;
                }

                if (lastException == null)
                {
                    lastException = new Exception("No locations left to get account properties from.");
                }

                return (null, lastException);
            }

            private bool TryMoveNextLocationThreadSafe(
                out string? location)
            {
                if (this.AccountProperties != null)
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

            private Task<(AccountProperties?, Exception?)> TryGetAccountPropertiesFromRegionalEndpointsAsync(string location)
            {
                return TryGetAccountPropertiesAsync(
                    LocationHelper.GetLocationEndpoint(this.DefaultEndpoint, location),
                    this.GetDatabaseAccountFn);
            }

            public static async Task<(AccountProperties?, Exception?)> TryGetAccountPropertiesAsync(
                Uri endpoint,
                Func<Uri, Task<AccountProperties>> getDatabaseAccountFn)
            {
                try
                {
                    AccountProperties databaseAccount = await getDatabaseAccountFn(endpoint);
                    return (databaseAccount, null);
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceInformation(DateTime.UtcNow + "Fail to reach location {0}, {1}", endpoint, e.ToString());
                    return (null, e);
                }
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

        public void MarkEndpointUnavailableForRead(Uri endpoint)
        {
            DefaultTrace.TraceInformation(DateTime.UtcNow + "Marking endpoint {0} unavailable for read", endpoint);

            this.locationCache.MarkEndpointUnavailableForRead(endpoint);
        }

        public void MarkEndpointUnavailableForWrite(Uri endpoint)
        {
            DefaultTrace.TraceInformation(DateTime.UtcNow + "Marking endpoint {0} unavailable for Write", endpoint);

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

        public async Task RefreshLocationAsync(AccountProperties databaseAccount, bool forceRefresh = false)
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

        private static bool IsNonRetriableException(Exception exception)
        {
            if (exception is DocumentClientException dce && dce.StatusCode == HttpStatusCode.Unauthorized)
            {
                return true;
            }

            return false;
        }
    }
}
