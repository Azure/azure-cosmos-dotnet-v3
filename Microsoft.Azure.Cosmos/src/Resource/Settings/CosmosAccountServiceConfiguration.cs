//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    internal class CosmosAccountServiceConfiguration : IServiceConfigurationReader, IDisposable
    {
        private readonly Func<Task<AccountProperties>> accountPropertiesTaskFunc;
        private readonly int refreshIntervalInSeconds;
        private readonly object refreshLock = new object();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        
        private AccountProperties accountProperties;
        private bool isBackgroundRefreshActive = false;
        private Task backgroundRefreshTask;

        internal AccountProperties AccountProperties
        {
            get
            {
                lock (this.refreshLock)
                {
                    return this.accountProperties;
                }
            }
            private set
            {
                lock (this.refreshLock)
                {
                    this.accountProperties = value;
                }
            }
        }

        /// <summary>
        /// Event that is raised when account properties are refreshed and PPAF enablement status changes
        /// </summary>
        internal event Action<bool?> OnEnablePartitionLevelFailoverChanged;

        public CosmosAccountServiceConfiguration(Func<Task<AccountProperties>> accountPropertiesTaskFunc)
        {
            if (accountPropertiesTaskFunc == null)
            {
                throw new ArgumentNullException(nameof(accountPropertiesTaskFunc));
            }

            this.accountPropertiesTaskFunc = accountPropertiesTaskFunc;
            this.refreshIntervalInSeconds = ConfigurationManager.GetAccountPropertiesRefreshIntervalInSeconds(300); // Default 5 minutes
        }

        public IDictionary<string, object> QueryEngineConfiguration => this.AccountProperties.QueryEngineConfiguration;

        public string DatabaseAccountId => throw new NotImplementedException();

        public Uri DatabaseAccountApiEndpoint => throw new NotImplementedException();

        public ReplicationPolicy UserReplicationPolicy => this.AccountProperties.ReplicationPolicy;

        public ReplicationPolicy SystemReplicationPolicy => this.AccountProperties.SystemReplicationPolicy;

        public Documents.ConsistencyLevel DefaultConsistencyLevel => (Documents.ConsistencyLevel)this.AccountProperties.Consistency.DefaultConsistencyLevel;

        public ReadPolicy ReadPolicy => this.AccountProperties.ReadPolicy;

        public string PrimaryMasterKey => throw new NotImplementedException();

        public string SecondaryMasterKey => throw new NotImplementedException();

        public string PrimaryReadonlyMasterKey => throw new NotImplementedException();

        public string SecondaryReadonlyMasterKey => throw new NotImplementedException();

        public string ResourceSeedKey => throw new NotImplementedException();

        public bool EnableAuthorization => true;

        public string SubscriptionId => throw new NotImplementedException();

        public async Task InitializeAsync()
        {
            if (this.AccountProperties == null)
            {
                this.AccountProperties = await this.accountPropertiesTaskFunc();
                this.InitializeBackgroundRefresh();
            }
        }

        /// <summary>
        /// Initializes and starts the background account properties refresh task
        /// </summary>
        private void InitializeBackgroundRefresh()
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (this.isBackgroundRefreshActive)
            {
                return;
            }

            lock (this.refreshLock)
            {
                if (this.isBackgroundRefreshActive)
                {
                    return;
                }

                this.isBackgroundRefreshActive = true;
            }

            try
            {
                this.backgroundRefreshTask = this.InitiateAccountPropertiesRefreshLoopAsync();
            }
            catch
            {
                this.isBackgroundRefreshActive = false;
                throw;
            }
        }

        /// <summary>
        /// Runs a continuous loop with a delay to refresh the account properties periodically.
        /// The loop will break when a cancellation is requested.
        /// </summary>
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async Task InitiateAccountPropertiesRefreshLoopAsync()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(this.refreshIntervalInSeconds),
                        this.cancellationTokenSource.Token);

                    if (this.cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    DefaultTrace.TraceInformation("CosmosAccountServiceConfiguration: Refreshing account properties.");
                    await this.RefreshAccountPropertiesAsync();
                }
                catch (Exception ex)
                {
                    if (this.cancellationTokenSource.IsCancellationRequested && (ex is OperationCanceledException || ex is ObjectDisposedException))
                    {
                        break;
                    }

                    DefaultTrace.TraceCritical("CosmosAccountServiceConfiguration: Failed to refresh account properties. Exception: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Refreshes the account properties and notifies listeners if EnablePartitionLevelFailover changes
        /// </summary>
        private async Task RefreshAccountPropertiesAsync()
        {
            try
            {
                bool? previousEnablePartitionLevelFailover = this.AccountProperties?.EnablePartitionLevelFailover;
                AccountProperties newProperties = await this.accountPropertiesTaskFunc();
                
                bool? newEnablePartitionLevelFailover = newProperties?.EnablePartitionLevelFailover;

                // Check if PPAF enablement status has changed
                if (previousEnablePartitionLevelFailover != newEnablePartitionLevelFailover)
                {
                    DefaultTrace.TraceInformation(
                        "CosmosAccountServiceConfiguration: EnablePartitionLevelFailover changed from {0} to {1}",
                        previousEnablePartitionLevelFailover,
                        newEnablePartitionLevelFailover);

                    // Update the properties first
                    this.AccountProperties = newProperties;

                    // Notify listeners about the change
                    this.OnEnablePartitionLevelFailoverChanged?.Invoke(newEnablePartitionLevelFailover);
                }
                else
                {
                    // Update properties even if PPAF status didn't change, as other properties might have changed
                    this.AccountProperties = newProperties;
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("CosmosAccountServiceConfiguration: Error refreshing account properties: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Disposes the background refresh task and cancellation token
        /// </summary>
        public void Dispose()
        {
            this.cancellationTokenSource?.Cancel();
            this.cancellationTokenSource?.Dispose();

            try
            {
                this.backgroundRefreshTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("CosmosAccountServiceConfiguration: Error disposing background refresh task: {0}", ex.Message);
            }
        }
    }
}
