//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for resolving the service settings.
    /// Implementations:
    /// GatewayServiceConfigurationReader: Client implementation.
    /// </summary>
    internal interface IServiceConfigurationReader
    {
        string DatabaseAccountId { get; }

        Uri DatabaseAccountApiEndpoint { get; }

        ReplicationPolicy UserReplicationPolicy { get; }

        ReplicationPolicy SystemReplicationPolicy { get; }

        ConsistencyLevel DefaultConsistencyLevel { get; }

        ReadPolicy ReadPolicy {get;}

        string PrimaryMasterKey { get; }
        string SecondaryMasterKey { get; }

        string PrimaryReadonlyMasterKey { get; }

        string SecondaryReadonlyMasterKey { get; }

        string ResourceSeedKey { get; }

        string SubscriptionId { get; }

        Task InitializeAsync();
    }
}
