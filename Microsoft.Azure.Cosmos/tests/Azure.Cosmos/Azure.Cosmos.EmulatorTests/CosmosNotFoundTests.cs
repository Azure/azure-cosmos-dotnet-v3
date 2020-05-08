//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

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
            CosmosDatabase database = CosmosNotFoundTests.client.GetDatabase(DoesNotExist);
            this.VerifyNotFoundResponse(await database.ReadStreamAsync());
            this.VerifyNotFoundResponse(await database.DeleteStreamAsync());
        }

        [TestMethod]
        public async Task ValidateContainerNotFoundResponse()
        {
            CosmosDatabase dbDoesNotExist = CosmosNotFoundTests.client.GetDatabase(DoesNotExist);
            await this.ContainerOperations(database: dbDoesNotExist, dbNotExist: true);

            CosmosDatabase dbExists = await client.CreateDatabaseAsync("NotFoundTest" + Guid.NewGuid().ToString());
            await this.ContainerOperations(database: dbExists, dbNotExist: false);
        }

        [TestMethod]
        public async Task ValidateQueryNotFoundResponse()
        {
            CosmosDatabase db = await CosmosNotFoundTests.client.CreateDatabaseAsync("NotFoundTest" + Guid.NewGuid().ToString());
            CosmosContainer container = await db.CreateContainerAsync("NotFoundTest" + Guid.NewGuid().ToString(), "/pk", 500);

            Dictionary<string, object> randomItem = new Dictionary<string, object> () { { "id", "test" }, { "pk", "testpk" } };
            await container.CreateItemAsync(randomItem);

            await container.DeleteContainerAsync();

            IAsyncEnumerable<Response> crossPartitionQueryIterator = container.GetItemQueryStreamResultsAsync(
                "select * from t where true",
                requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

            await this.VerifyQueryNotFoundResponse(crossPartitionQueryIterator);

            IAsyncEnumerable<Response> queryIterator = container.GetItemQueryStreamResultsAsync(
                "select * from t where true",
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = 1,
                    PartitionKey = new Cosmos.PartitionKey("testpk"),
                });

            await this.VerifyQueryNotFoundResponse(queryIterator);

            IAsyncEnumerable<Response> crossPartitionQueryIterator2 = container.GetItemQueryStreamResultsAsync(
                "select * from t where true",
                requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

            await this.VerifyQueryNotFoundResponse(crossPartitionQueryIterator2);

            await db.DeleteAsync();
        }

        private async Task ContainerOperations(CosmosDatabase database, bool dbNotExist)
        {
            // Create should fail if the database does not exist
            if (dbNotExist)
            {
                CosmosContainerProperties newcontainerSettings = new CosmosContainerProperties(id: DoesNotExist, partitionKeyPath: "/pk");
                this.VerifyNotFoundResponse(await database.CreateContainerStreamAsync(newcontainerSettings, throughput: 500));
            }

            CosmosContainer doesNotExistContainer = database.GetContainer(DoesNotExist);
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReadContainerStreamAsync());

            CosmosContainerProperties containerSettings = new CosmosContainerProperties(id: DoesNotExist, partitionKeyPath: "/pk");
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReplaceContainerStreamAsync(containerSettings));
            this.VerifyNotFoundResponse(await doesNotExistContainer.DeleteContainerStreamAsync());

            // Validate Child resources
            await this.ItemOperations(doesNotExistContainer, true);

            // The database exists create a container and validate it's children
            if (!dbNotExist)
            {
                CosmosContainer containerExists = await database.CreateContainerAsync(
                    id: "NotFoundTest" + Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                await this.ItemOperations(containerExists, false);
            }
        }

        private async Task ItemOperations(CosmosContainer container, bool containerNotExist)
        {
            if (containerNotExist)
            {
                dynamic randomItem = new { id = "test", pk = "doesnotexist" };
                Stream create = TestCommon.Serializer.Value.ToStream<dynamic>(randomItem);
                this.VerifyNotFoundResponse(await container.CreateItemStreamAsync(create, new PartitionKey(randomItem.pk)));

                IAsyncEnumerable<Response> queryIterator = container.GetItemQueryStreamResultsAsync(
                    "select * from t where true",
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });


                this.VerifyNotFoundResponse(await queryIterator.GetFirstResponse());

                IAsyncEnumerable<Response> feedIterator = container.GetItemQueryStreamResultsAsync();
                this.VerifyNotFoundResponse(await feedIterator.GetFirstResponse());

                dynamic randomUpsertItem = new { id = DoesNotExist, pk = DoesNotExist, status = 42 };
                Stream upsert = TestCommon.Serializer.Value.ToStream<dynamic>(randomUpsertItem);
                this.VerifyNotFoundResponse(await container.UpsertItemStreamAsync(
                    partitionKey: new Cosmos.PartitionKey(randomUpsertItem.pk),
                    streamPayload: upsert));
            }

            this.VerifyNotFoundResponse(await container.ReadItemStreamAsync(partitionKey: new Cosmos.PartitionKey(DoesNotExist), id: DoesNotExist));
            this.VerifyNotFoundResponse(await container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(DoesNotExist), id: DoesNotExist));

            dynamic randomReplaceItem = new { id = "test", pk = "doesnotexist", status = 42 };
            Stream replace = TestCommon.Serializer.Value.ToStream<dynamic>(randomReplaceItem);
            this.VerifyNotFoundResponse(await container.ReplaceItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(randomReplaceItem.pk),
                id: randomReplaceItem.id,
                streamPayload: replace));
        }

        private async Task VerifyQueryNotFoundResponse(IAsyncEnumerable<Response> iterator)
        {
            // Verify that even if the user ignores the HasMoreResults it still returns the exception
            int iterationCount = 0;
            await foreach(Response response in iterator)
            {
                iterationCount++;
                Assert.IsNotNull(response);
                Assert.AreEqual((int)HttpStatusCode.NotFound, response.Status);
            }

            Assert.IsTrue(iterationCount > 0);
        }

        private void VerifyNotFoundResponse(Response response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.NotFound, response.Status);
        }
    }
}