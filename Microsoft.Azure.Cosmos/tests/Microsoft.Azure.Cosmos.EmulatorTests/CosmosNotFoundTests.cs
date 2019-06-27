//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class CosmosNotFoundTests
    {
        public const string DoesNotExist = "DoesNotExist-69E1BD04-EC99-449B-9365-34DA9F4D4ECE";
        private static CosmosClient client = null;
        private static CosmosSerializer jsonSerializer = new CosmosJsonSerializerCore();

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

            var crossPartitionQueryIterator = container.GetItemQueryStreamIterator(
                "select * from t where true", 
                requestOptions: new QueryRequestOptions() { MaxConcurrency= 2});

            var queryResponse = await crossPartitionQueryIterator.ReadNextAsync();
            Assert.IsNotNull(queryResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, queryResponse.StatusCode);

            var queryIterator = container.GetItemQueryStreamIterator(
                "select * from t where true",
                requestOptions: new QueryRequestOptions()
                    {
                        MaxConcurrency = 1,
                        PartitionKey = new Cosmos.PartitionKey("testpk"),
                });

            this.VerifyQueryNotFoundResponse(await queryIterator.ReadNextAsync());

            var crossPartitionQueryIterator2 = container.GetItemQueryStreamIterator(
                "select * from t where true", 
                requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

            this.VerifyQueryNotFoundResponse(await crossPartitionQueryIterator2.ReadNextAsync());

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
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReadStreamAsync());

            ContainerProperties containerSettings = new ContainerProperties(id: DoesNotExist, partitionKeyPath: "/pk");
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReplaceStreamAsync(containerSettings));
            this.VerifyNotFoundResponse(await doesNotExistContainer.DeleteStreamAsync());

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
                Stream create = jsonSerializer.ToStream<dynamic>(randomItem);
                this.VerifyNotFoundResponse(await container.CreateItemStreamAsync(create, new PartitionKey(randomItem.pk)));

                var queryIterator = container.GetItemQueryStreamIterator(
                    "select * from t where true", 
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

                this.VerifyQueryNotFoundResponse(await queryIterator.ReadNextAsync());

                var feedIterator = container.GetItemQueryStreamIterator();
                this.VerifyNotFoundResponse(await feedIterator.ReadNextAsync());

                dynamic randomUpsertItem = new { id = DoesNotExist, pk = DoesNotExist, status = 42 };
                Stream upsert = jsonSerializer.ToStream<dynamic>(randomUpsertItem);
                this.VerifyNotFoundResponse(await container.UpsertItemStreamAsync(
                    partitionKey: new Cosmos.PartitionKey(randomUpsertItem.pk),
                    streamPayload: upsert));
            }

            this.VerifyNotFoundResponse(await container.ReadItemStreamAsync(partitionKey: new Cosmos.PartitionKey(DoesNotExist), id: DoesNotExist));
            this.VerifyNotFoundResponse(await container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(DoesNotExist), id: DoesNotExist));

            dynamic randomReplaceItem = new { id = "test", pk = "doesnotexist", status = 42 };
            Stream replace = jsonSerializer.ToStream<dynamic>(randomReplaceItem);
            this.VerifyNotFoundResponse(await container.ReplaceItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(randomReplaceItem.pk),
                id: randomReplaceItem.id,
                streamPayload: replace));
        }

        private void VerifyQueryNotFoundResponse(ResponseMessage response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        private void VerifyNotFoundResponse(ResponseMessage response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}