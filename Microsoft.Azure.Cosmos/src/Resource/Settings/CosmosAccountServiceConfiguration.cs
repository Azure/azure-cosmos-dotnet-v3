//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class CosmosAccountServiceConfiguration : IServiceConfigurationReader
    {
        private Func<Task<AccountProperties>> accountPropertiesTaskFunc { get; }

        internal AccountProperties AccountProperties { get; private set; }

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
    }
}
