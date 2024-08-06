//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class PatchItemWithCustomSerializationTests : BaseCosmosClientHelper
    {
        CosmosClient cosmosClient = null;

        [TestInitialize]
        public void TestInitialize()
        {
            CosmosClientOptions cosmosOptions = new CosmosClientOptions()
            {
                AllowBulkExecution = true,
                SerializerOptions = new CosmosSerializationOptions()
            };

            cosmosOptions.SerializerOptions.PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase;

            this.cosmosClient = TestCommon.CreateCosmosClient(cosmosOptions);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();

            this.cosmosClient?.Dispose();
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task PatchItemPropertyNamingPolicyTestAsync()
        {
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            Container container = await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id"));

            string id = Guid.NewGuid().ToString();

            ToDoActivity toDoActivity = new ()
            { 
                Id = id,
                Name = default,
                Description = default
            };

            PartitionKey partitionKey = new(toDoActivity.Id);

            ItemResponse<ToDoActivity> upsertResponse = await container.UpsertItemAsync<ToDoActivity>(item: toDoActivity, partitionKey: partitionKey);

            Assert.IsNotNull(upsertResponse);
            Assert.AreEqual(expected: HttpStatusCode.Created, actual: upsertResponse.StatusCode);
            Assert.AreEqual(expected: id, actual: upsertResponse.Resource.Id);
            Assert.AreEqual(expected: default, actual: upsertResponse.Resource.Name);
            Assert.AreEqual(expected: default, actual: upsertResponse.Resource.Description);

            List<PatchOperation> patchOperations = new ()
            {
                PatchOperation.Add($"/{nameof(ToDoActivity.Name)}", "updated name"),
                PatchOperation.Replace($"/{nameof(ToDoActivity.Description)}", "updated description"),
            };

            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(async() =>
            {
                ItemResponse<ToDoActivity> patchResponse = await container.PatchItemAsync<ToDoActivity>(
                    id: toDoActivity.Id,
                    partitionKey: partitionKey,
                    patchOperations: patchOperations);

                Assert.IsNotNull(patchResponse);
                Assert.AreEqual(expected: HttpStatusCode.OK, actual: patchResponse.StatusCode);
                Assert.AreEqual(expected: id, actual: patchResponse.Resource.Id);
                Assert.AreEqual(expected: "updated name", actual: patchResponse.Resource.Name);
                Assert.AreEqual(expected: "updated description", actual: patchResponse.Resource.Description);
            });
                

            Assert.IsNotNull(cosmosException);
            Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: cosmosException.StatusCode);
        }

        public class ToDoActivity
        {
            public string Id { get; set; }

            public string Name { get; set; }
            
            public string Description { get; set; }
        }
    }
}
