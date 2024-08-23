//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.CosmosAvailabilityStrategyTests;

    public class MultiRegionSetupHelpers
    {
        private const string dbName = "availabilityStrategyTestDb";
        private const string containerName = "availabilityStrategyTestContainer";
        private const string changeFeedContainerName = "availabilityStrategyTestChangeFeedContainer";

        public static async Task<(Database, Container, Container)> GetOrCreateMultiRegionDatabaseAndContainers(CosmosClient client)
        {
            Database database;
            Container container;
            Container changeFeedContainer;

            DatabaseResponse db = await client.CreateDatabaseIfNotExistsAsync(
                id: MultiRegionSetupHelpers.dbName,
                throughput: 400);
            database = db.Database;

            if (db.StatusCode == HttpStatusCode.Created)
            {
                container = await database.CreateContainerIfNotExistsAsync(
                    id: MultiRegionSetupHelpers.containerName,
                    partitionKeyPath: "/pk",
                    throughput: 400);
                changeFeedContainer = await database.CreateContainerIfNotExistsAsync(
                    id: MultiRegionSetupHelpers.changeFeedContainerName,
                    partitionKeyPath: "/partitionKey",
                    throughput: 400);

                List<Task> tasks = new List<Task>()
                {
                    container.CreateItemAsync<AvailabilityStrategyTestObject>(
                        new AvailabilityStrategyTestObject { Id = "testId", Pk = "pk" }),
                    container.CreateItemAsync<AvailabilityStrategyTestObject>(
                        new AvailabilityStrategyTestObject { Id = "testId2", Pk = "pk2" }),
                    container.CreateItemAsync<AvailabilityStrategyTestObject>(
                        new AvailabilityStrategyTestObject { Id = "testId3", Pk = "pk3" }),
                    container.CreateItemAsync<AvailabilityStrategyTestObject>(
                        new AvailabilityStrategyTestObject { Id = "testId4", Pk = "pk4" })
                };

                await Task.WhenAll(tasks);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(60000);

                return (database, container, changeFeedContainer);
            }

            container = database.GetContainer(MultiRegionSetupHelpers.containerName);
            changeFeedContainer = database.GetContainer(MultiRegionSetupHelpers.changeFeedContainerName);

            return (database, container, changeFeedContainer);
        }
    }
}
