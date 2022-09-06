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

        public Container LeaseContainer = null;

        public async Task ChangeFeedTestInit(string leaseContainerPk = "/id")
        {
            await base.TestInit(customizeClientBuilder: (builder) => builder.WithContentResponseOnWrite(false));

            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: "leases", partitionKeyPath: leaseContainerPk),
                cancellationToken: this.cancellationToken);

            this.LeaseContainer = response;
        }
    }
}
