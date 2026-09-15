//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    internal class CosmosAccountServiceConfiguration : IServiceConfigurationReaderVnext
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

        public bool EnableNRegionSynchronousCommit => this.AccountProperties.EnableNRegionSynchronousCommit;

        public async Task InitializeAsync()
        {
            if (this.AccountProperties == null)
            {
                this.AccountProperties = await this.accountPropertiesTaskFunc();

                // Defensive guard (issue #4671): the account read is expected to either return account
                // properties or throw. If it completes with a null result, consuming AccountProperties
                // downstream (for example DocumentClient.InitializeGatewayConfigurationReaderAsync reading
                // accountProperties.EnableMultipleWriteLocations) would throw a bare, undiagnosable
                // NullReferenceException during client initialization. Fail with an actionable exception
                // instead. This stays a non-terminal initialization failure, so the initialization cache
                // re-initializes on the next request (the client self-heals).
                if (this.AccountProperties == null)
                {
                    DefaultTrace.TraceCritical(
                        "CosmosAccountServiceConfiguration: gateway account initialization returned null AccountProperties.");

                    throw new InvalidOperationException(
                        "Azure Cosmos DB client initialization failed: reading the database account from the " +
                        "gateway returned no account properties (AccountProperties was null). This is unexpected " +
                        "and most commonly indicates a version mismatch between the Microsoft.Azure.Cosmos and " +
                        "Microsoft.Azure.Cosmos.Direct packages loaded at runtime; ensure both resolve to matching, " +
                        "compatible versions. Initialization will be retried on the next request.");
                }
            }
        }
    }
}
