//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// AddressCache implementation for client SDK. Supports cross region address routing based on 
    /// avaialbility and preference list.
    /// </summary>
    /// Marking it as non-sealed in order to unit test it using Moq framework
    internal class GlobalEndpointManager : IDisposable
    {
        private static int defaultbackgroundRefreshLocationTimeIntervalInMS = 5 * 60 * 1000;

        private const string BackgroundRefreshLocationTimeIntervalInMS = "BackgroundRefreshLocationTimeIntervalInMS";
        private int backgroundRefreshLocationTimeIntervalInMS = defaultbackgroundRefreshLocationTimeIntervalInMS;
        private readonly LocationCache locationCache;
        private readonly Uri defaultEndpoint;
        private readonly ConnectionPolicy connectionPolicy;
        private readonly IDocumentClientInternal owner;
        private readonly object refreshLock;
        private bool isRefreshing;
        private bool isDisposed;

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

            this.isRefreshing = false;
            this.isDisposed = false;
            this.refreshLock = new object();
#if !(NETSTANDARD15 || NETSTANDARD16)
            string backgroundRefreshLocationTimeIntervalInMSConfig = System.Configuration.ConfigurationManager.AppSettings[GlobalEndpointManager.BackgroundRefreshLocationTimeIntervalInMS];
            if (!string.IsNullOrEmpty(backgroundRefreshLocationTimeIntervalInMSConfig))
            {
                if (!int.TryParse(backgroundRefreshLocationTimeIntervalInMSConfig, out this.backgroundRefreshLocationTimeIntervalInMS))
                {
                    this.backgroundRefreshLocationTimeIntervalInMS = GlobalEndpointManager.defaultbackgroundRefreshLocationTimeIntervalInMS;
                }
            }
#endif
        }

        public ReadOnlyCollection<Uri> ReadEndpoints
        {
            get
            {
                return this.locationCache.ReadEndpoints;
            }
        }

        public ReadOnlyCollection<Uri> WriteEndpoints
        {
            get
            {
                return this.locationCache.WriteEndpoints;
            }
        }

        public async static Task<CosmosAccountSettings> GetDatabaseAccountFromAnyLocationsAsync(
            Uri defaultEndpoint, IList<string> locations, Func<Uri, Task<CosmosAccountSettings>> getDatabaseAccountFn)
        {
            try
            {
                CosmosAccountSettings databaseAccount = await getDatabaseAccountFn(defaultEndpoint);
                return databaseAccount;
            }
            catch (Exception e)
            {
                DefaultTrace.TraceInformation("Fail to reach global gateway {0}, {1}", defaultEndpoint, e.ToString());
                if (locations.Count == 0)
                    throw;
            }

            for (int index = 0; index < locations.Count; index++)
            {
                try
                {
                    CosmosAccountSettings databaseAccount = await getDatabaseAccountFn(LocationHelper.GetLocationEndpoint(defaultEndpoint, locations[index]));
                    return databaseAccount;
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceInformation("Fail to reach location {0}, {1}", locations[index], e.ToString());
                    if (index == locations.Count - 1)
                    {
                        // if is the last one, throw exception
                        throw;
                    }
                }
            }

            // we should never reach here. Make compiler happy
            throw new Exception();
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
            DefaultTrace.TraceInformation("Marking endpoint {0} unavailable for read", endpoint);

            this.locationCache.MarkEndpointUnavailableForRead(endpoint);
        }

        public void MarkEndpointUnavailableForWrite(Uri endpoint)
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
            this.isDisposed = true;
        }

        public async Task RefreshLocationAsync(CosmosAccountSettings databaseAccount)
        {
            lock (this.refreshLock)
            {
                if (this.isRefreshing) return;

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

        private async Task RefreshLocationPrivateAsync(CosmosAccountSettings databaseAccount)
        {
            DefaultTrace.TraceInformation("RefreshLocationAsync() refreshing locations");

            if (databaseAccount != null)
            {
                this.locationCache.OnDatabaseAccountRead(databaseAccount);
            }

            bool canRefreshInBackground = false;
            if (this.locationCache.ShouldRefreshEndpoints(out canRefreshInBackground))
            {
                if (databaseAccount == null && !canRefreshInBackground)
                {
                    databaseAccount = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(this.defaultEndpoint, this.connectionPolicy.PreferredLocations, this.GetDatabaseAccountAsync);

                    this.locationCache.OnDatabaseAccountRead(databaseAccount);
                }

                this.StartRefreshLocationTimerAsync();
            }
            else
            {
                this.isRefreshing = false;
            }
        }

        private async void StartRefreshLocationTimerAsync()
        {
            if (this.isDisposed)
            {
                return;
            }

            try
            {
                await Task.Delay(this.backgroundRefreshLocationTimeIntervalInMS);

                DefaultTrace.TraceInformation("StartRefreshLocationTimerAsync() - Invoking refresh");

                CosmosAccountSettings databaseAccount = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(this.defaultEndpoint, this.connectionPolicy.PreferredLocations, this.GetDatabaseAccountAsync);

                await this.RefreshLocationPrivateAsync(databaseAccount);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceCritical("StartRefreshLocationTimerAsync() - Unable to refresh database account from any location. Exception: {0}", ex.ToString());
                this.StartRefreshLocationTimerAsync();
            }
        }

        private Task<CosmosAccountSettings> GetDatabaseAccountAsync(Uri serviceEndpoint)
        {
            return this.owner.GetDatabaseAccountInternalAsync(serviceEndpoint);
        }

        private void OnPreferenceChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.locationCache.OnLocationPreferenceChanged(new ReadOnlyCollection<string>(
                this.connectionPolicy.PreferredLocations));
        }
    }
}
