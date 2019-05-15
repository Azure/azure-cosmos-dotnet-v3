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
        private static CosmosJsonSerializer jsonSerializer = new CosmosDefaultJsonSerializer();

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            CosmosNotFoundTests.client = TestCommon.CreateCosmosClient();
        }

        [TestMethod]
        public async Task ValidateDatabaseNotFoundResponse()
        {
            CosmosClient client = CosmosNotFoundTests.client;

            CosmosDatabase database = client.Databases[DoesNotExist];
            this.VerifyNotFoundResponse(await database.ReadStreamAsync());
            this.VerifyNotFoundResponse(await database.DeleteStreamAsync());
        }

        [TestMethod]
        public async Task ValidateContainerNotFoundResponse()
        {
            CosmosClient client = CosmosNotFoundTests.client;
            CosmosDatabase dbDoesNotExist = client.Databases[DoesNotExist];
            await this.ContainerOperations(database: dbDoesNotExist, dbNotExist: true);

            CosmosDatabase dbExists = await client.Databases.CreateDatabaseAsync("NotFoundTest" + Guid.NewGuid().ToString());
            await this.ContainerOperations(database: dbExists, dbNotExist: false);
        }

        private async Task ContainerOperations(CosmosDatabase database, bool dbNotExist)
        {
            // Create should fail if the database does not exist
            if (dbNotExist)
            {
                Stream create = jsonSerializer.ToStream<CosmosContainerSettings>(new CosmosContainerSettings(id: DoesNotExist, partitionKeyPath: "/pk"));
                this.VerifyNotFoundResponse(await database.Containers.CreateContainerStreamAsync(create, throughput: 500));
            }

            CosmosContainer doesNotExistContainer = database.Containers[DoesNotExist];
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReadStreamAsync());

            Stream replace = jsonSerializer.ToStream<CosmosContainerSettings>(new CosmosContainerSettings(id: DoesNotExist, partitionKeyPath: "/pk"));
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReplaceStreamAsync(replace));
            this.VerifyNotFoundResponse(await doesNotExistContainer.DeleteStreamAsync());

            // Validate Child resources
            await this.ItemOperations(doesNotExistContainer, true);

            // The database exists create a container and validate it's children
            if (!dbNotExist)
            {
                CosmosContainer containerExists = await database.Containers.CreateContainerAsync(
                    id: "NotFoundTest" + Guid.NewGuid().ToString(), 
                    partitionKeyPath: "/pk");

                await this.ItemOperations(containerExists, false);
            }
        }

        private async Task ItemOperations(CosmosContainer container, bool containerNotExist)
        {
            CosmosItems cosmosItems = container.Items;
            if (containerNotExist)
            {
                dynamic randomItem = new { id = "test", pk = "doesnotexist" };
                Stream create = jsonSerializer.ToStream<dynamic>(randomItem);
                this.VerifyNotFoundResponse(await container.Items.CreateItemStreamAsync(randomItem.pk, create));

                FeedIterator queryIterator = cosmosItems.CreateItemQueryAsStream("select * from t where true", maxConcurrency: 2);
                this.VerifyNotFoundResponse(await queryIterator.FetchNextSetAsync());

                FeedIterator feedIterator = cosmosItems.GetItemStreamIterator();
                this.VerifyNotFoundResponse(await feedIterator.FetchNextSetAsync());

                dynamic randomUpsertItem = new { id = DoesNotExist, pk = DoesNotExist, status = 42 };
                Stream upsert = jsonSerializer.ToStream<dynamic>(randomUpsertItem);
                this.VerifyNotFoundResponse(await cosmosItems.UpsertItemStreamAsync(
                    partitionKey: randomUpsertItem.pk,
                    streamPayload: upsert));
            }

            this.VerifyNotFoundResponse(await cosmosItems.ReadItemStreamAsync(partitionKey: DoesNotExist, id: DoesNotExist));
            this.VerifyNotFoundResponse(await cosmosItems.DeleteItemStreamAsync(partitionKey: DoesNotExist, id: DoesNotExist));

            dynamic randomReplaceItem = new { id = "test", pk = "doesnotexist", status = 42 };
            Stream replace = jsonSerializer.ToStream<dynamic>(randomReplaceItem);
            this.VerifyNotFoundResponse(await cosmosItems.ReplaceItemStreamAsync(
                partitionKey: randomReplaceItem.pk, 
                id: randomReplaceItem.id, 
                streamPayload: replace));
        }

        private void VerifyNotFoundResponse(CosmosResponseMessage response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
