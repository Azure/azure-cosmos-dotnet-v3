//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;

    public class BaseChangeFeedClientHelper : BaseCosmosClientHelper
    {
        public static int ChangeFeedSetupTime = 1000;
        public static int ChangeFeedCleanupTime = 5000;

        public Container Container = null;
        public Container LeaseContainer = null;

        public async Task ChangeFeedTestInit()
        {
            await base.TestInit();
            string PartitionKey = "/id";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: "monitored", partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            this.Container = response;


            response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);

            this.LeaseContainer = response;
        }
    }
}
