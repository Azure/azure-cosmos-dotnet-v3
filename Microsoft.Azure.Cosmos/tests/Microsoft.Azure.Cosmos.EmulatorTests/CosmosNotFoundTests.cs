//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosNotFoundTests
    {
        public const string DoesNotExist = "DoesNotExist-69E1BD04-EC99-449B-9365-34DA9F4D4ECE";
        private static CosmosClient client = null;

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            CosmosNotFoundTests.client = TestCommon.CreateCosmosClient();
        }

        [TestMethod]
        public async Task ValidateDatabaseNotFoundResponse()
        {
            Database database = CosmosNotFoundTests.client.GetDatabase(DoesNotExist);
            this.VerifyNotFoundResponse(await database.ReadStreamAsync());
            this.VerifyNotFoundResponse(await database.DeleteStreamAsync());
        }

        [TestMethod]
        public async Task ValidateContainerNotFoundResponse()
        {
            Database dbDoesNotExist = CosmosNotFoundTests.client.GetDatabase(DoesNotExist);
            await this.ContainerOperations(database: dbDoesNotExist, dbNotExist: true);

            Database dbExists = await client.CreateDatabaseAsync("NotFoundTest" + Guid.NewGuid().ToString());
            await this.ContainerOperations(database: dbExists, dbNotExist: false);
        }

        [TestMethod]
        public async Task ValidateQueryNotFoundResponse()
        {
            Database db = await CosmosNotFoundTests.client.CreateDatabaseAsync("NotFoundTest" + Guid.NewGuid().ToString());
            Container container = await db.CreateContainerAsync("NotFoundTest" + Guid.NewGuid().ToString(), "/pk", 500);

            dynamic randomItem = new { id = "test", pk = "testpk" };
            await container.CreateItemAsync(randomItem);

            await container.DeleteContainerAsync();

            {
                // Querying after delete should be a gone exception even after the retry.
                FeedIterator crossPartitionQueryIterator = container.GetItemQueryStreamIterator(
                    "select * from t where true",
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

                await this.VerifyQueryNotFoundResponse(crossPartitionQueryIterator);
            }

            {
                // Also try with partition key.
                FeedIterator queryIterator = container.GetItemQueryStreamIterator(
                    "select * from t where true",
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxConcurrency = 1,
                        PartitionKey = new Cosmos.PartitionKey("testpk"),
                    });

                await this.VerifyQueryNotFoundResponse(queryIterator);
            }

            {
                // Recreate the collection with the same name on a different client.
                CosmosClient newClient = TestCommon.CreateCosmosClient();
                Database db2 = newClient.GetDatabase(db.Id);
                Container container2 = await db2.CreateContainerAsync(
                    id: container.Id,
                    partitionKeyPath: "/pk",
                    throughput: 500);
                await container2.CreateItemAsync(randomItem);

                FeedIterator queryIterator = container.GetItemQueryStreamIterator(
                    "select * from t where true",
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

                ResponseMessage response = await queryIterator.ReadNextAsync();
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            await db.DeleteAsync();
        }

        private async Task ContainerOperations(Database database, bool dbNotExist)
        {
            // Create should fail if the database does not exist
            if (dbNotExist)
            {
                ContainerProperties newcontainerSettings = new ContainerProperties(id: DoesNotExist, partitionKeyPath: "/pk");
                this.VerifyNotFoundResponse(await database.CreateContainerStreamAsync(newcontainerSettings, throughput: 500));
            }

            Container doesNotExistContainer = database.GetContainer(DoesNotExist);
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReadContainerStreamAsync());

            ContainerProperties containerSettings = new ContainerProperties(id: DoesNotExist, partitionKeyPath: "/pk");
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReplaceContainerStreamAsync(containerSettings));
            this.VerifyNotFoundResponse(await doesNotExistContainer.DeleteContainerStreamAsync());

            // Validate Child resources
            await this.ItemOperations(doesNotExistContainer, true);

            // The database exists create a container and validate it's children
            if (!dbNotExist)
            {
                Container containerExists = await database.CreateContainerAsync(
                    id: "NotFoundTest" + Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                await this.ItemOperations(containerExists, false);
            }
        }

        private async Task ItemOperations(Container container, bool containerNotExist)
        {
            if (containerNotExist)
            {
                dynamic randomItem = new { id = "test", pk = "doesnotexist" };
                Stream create = TestCommon.SerializerCore.ToStream<dynamic>(randomItem);
                this.VerifyNotFoundResponse(await container.CreateItemStreamAsync(create, new PartitionKey(randomItem.pk)));

                FeedIterator queryIterator = container.GetItemQueryStreamIterator(
                    "select * from t where true",
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

                this.VerifyNotFoundResponse(await queryIterator.ReadNextAsync());

                FeedIterator feedIterator = container.GetItemQueryStreamIterator();
                this.VerifyNotFoundResponse(await feedIterator.ReadNextAsync());

                dynamic randomUpsertItem = new { id = DoesNotExist, pk = DoesNotExist, status = 42 };
                Stream upsert = TestCommon.SerializerCore.ToStream<dynamic>(randomUpsertItem);
                this.VerifyNotFoundResponse(await container.UpsertItemStreamAsync(
                    partitionKey: new Cosmos.PartitionKey(randomUpsertItem.pk),
                    streamPayload: upsert));
            }

            this.VerifyNotFoundResponse(await container.ReadItemStreamAsync(partitionKey: new Cosmos.PartitionKey(DoesNotExist), id: DoesNotExist));
            this.VerifyNotFoundResponse(await container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(DoesNotExist), id: DoesNotExist));

            dynamic randomReplaceItem = new { id = "test", pk = "doesnotexist", status = 42 };
            Stream replace = TestCommon.SerializerCore.ToStream<dynamic>(randomReplaceItem);
            this.VerifyNotFoundResponse(await container.ReplaceItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(randomReplaceItem.pk),
                id: randomReplaceItem.id,
                streamPayload: replace));
        }

        private async Task VerifyQueryNotFoundResponse(FeedIterator iterator)
        {
            ResponseMessage response = await iterator.ReadNextAsync();
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.IsFalse(iterator.HasMoreResults);
        }

        private void VerifyNotFoundResponse(ResponseMessage response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}