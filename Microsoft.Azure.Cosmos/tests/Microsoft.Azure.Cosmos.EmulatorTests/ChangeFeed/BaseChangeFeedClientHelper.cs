//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;

    public class BaseChangeFeedClientHelper : BaseCosmosClientHelper
    {
        public static int ChangeFeedSetupTime = 1000;
        public static int ChangeFeedCleanupTime = 5000;

        public CosmosContainer Container = null;
        public CosmosContainer LeaseContainer = null;

        public async Task ChangeFeedTestInit()
        {
            await base.TestInit();
            string PartitionKey = "/id";
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: "monitored", partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            this.Container = response;


            response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: "leases", partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);

            this.LeaseContainer = response;
        }
    }
}
