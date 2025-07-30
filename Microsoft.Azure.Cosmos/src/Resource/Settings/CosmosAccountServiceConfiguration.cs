//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class CosmosAccountServiceConfiguration : IServiceConfigurationReader
    {
        private Func<Task<AccountProperties>> accountPropertiesTaskFunc { get; }

        internal AccountProperties AccountProperties { get; private set; }

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
            }
        }

        /// <summary>
        /// Updates the account properties and notifies listeners if EnablePartitionLevelFailover changes
        /// This method is called by GlobalEndpointManager when account properties are refreshed
        /// </summary>
        internal void UpdateAccountProperties(AccountProperties newProperties)
        {
            if (newProperties == null)
            {
                return;
            }

            bool? previousEnablePartitionLevelFailover = this.AccountProperties?.EnablePartitionLevelFailover;
            bool? newEnablePartitionLevelFailover = newProperties.EnablePartitionLevelFailover;

            // Update the properties first
            this.AccountProperties = newProperties;

            // Check if PPAF enablement status has changed and notify listeners
            if (previousEnablePartitionLevelFailover != newEnablePartitionLevelFailover)
            {
                this.OnEnablePartitionLevelFailoverChanged?.Invoke(newEnablePartitionLevelFailover);
            }
        }
    }
}
