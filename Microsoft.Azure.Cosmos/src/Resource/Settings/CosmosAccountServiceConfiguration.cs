//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    internal class CosmosAccountServiceConfiguration : IServiceConfigurationReader
    {
        private Func<Task<CosmosAccountSettings>> accountSettingsTaskFunc { get; }

        internal CosmosAccountSettings AccountSettings { get; private set; }

        public CosmosAccountServiceConfiguration(Func<Task<CosmosAccountSettings>> accountSettingsTaskFunc)
        {
            if (accountSettingsTaskFunc == null)
            {
                throw new ArgumentNullException(nameof(accountSettingsTaskFunc));
            }

            this.accountSettingsTaskFunc = accountSettingsTaskFunc;
        }

        public IDictionary<string, object> QueryEngineConfiguration => this.AccountSettings.QueryEngineConfiuration;

        public string DatabaseAccountId => throw new NotImplementedException();

        public Uri DatabaseAccountApiEndpoint => throw new NotImplementedException();

        public ReplicationPolicy UserReplicationPolicy => this.AccountSettings.ReplicationPolicy;

        public ReplicationPolicy SystemReplicationPolicy => this.AccountSettings.SystemReplicationPolicy;

        public ConsistencyLevel DefaultConsistencyLevel => this.AccountSettings.ConsistencySetting.DefaultConsistencyLevel;

        public ReadPolicy ReadPolicy => this.AccountSettings.ReadPolicy;

        public string PrimaryMasterKey => throw new NotImplementedException();

        public string SecondaryMasterKey => throw new NotImplementedException();

        public string PrimaryReadonlyMasterKey => throw new NotImplementedException();

        public string SecondaryReadonlyMasterKey => throw new NotImplementedException();

        public string ResourceSeedKey => throw new NotImplementedException();

        public bool EnableAuthorization => true;

        public string SubscriptionId => throw new NotImplementedException();

        public async Task InitializeAsync()
        {
            if (this.AccountSettings == null)
            {
                this.AccountSettings = await accountSettingsTaskFunc();
            }
        }
    }
}
