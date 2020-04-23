//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests.ChangeFeed
{
    using System;
    using System.Threading.Tasks;

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
            ContainerResponse response = await this.database.CreateContainerAsync(
                new CosmosContainerProperties(id: "monitored", partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            this.Container = response;


            response = await this.database.CreateContainerAsync(
                new CosmosContainerProperties(id: "leases", partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);

            this.LeaseContainer = response;
        }
    }
}
