// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// End-to-end emulator tests for Distributed Transactions.
    /// These tests run against the actual Cosmos DB Emulator.
    /// </summary>
    [TestClass]
    public class DistributedTransactionTest
    {
        private const string PartitionKeyPath = "/pk";

        // Custom endpoint and key - update these for your environment
        private const string CustomEndpoint = "https://swvyadtc-southeastasia.sql.cosmos.windows-int.net:443/";
        private const string CustomMasterKey = "<master-key>";

        private CosmosClient client;
        private Database database;
        private Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.client = new CosmosClient(
                accountEndpoint: CustomEndpoint,
                authKeyOrResourceToken: CustomMasterKey,
                clientOptions: new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway
                });

            string databaseId = "DtxTestDb";
            DatabaseResponse dbResponse = await this.client.CreateDatabaseIfNotExistsAsync(databaseId);
            this.database = dbResponse.Database;

            string containerId = "DtxTestContainer";
            ContainerResponse containerResponse = await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: containerId, partitionKeyPath: PartitionKeyPath));
            this.container = containerResponse.Container;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }

            this.client?.Dispose();
        }

        [TestMethod]
        [Description("E2E happy path test: Create multiple items in a distributed transaction")]
        public async Task DistributedTransaction_CreateMultipleItems_Succeeds()
        {
            // Arrange
            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            // Act
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc1.pk), doc1)
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc2.pk), doc2)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            Console.WriteLine($"Response StatusCode: {response.StatusCode}");
            Console.WriteLine($"Response Count: {response.Count}");
            Console.WriteLine($"Response ActivityId: {response.ActivityId}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Expected OK but got {response.StatusCode}");
            Assert.IsTrue(response.IsSuccessStatusCode, "Response should indicate success");
            Assert.AreEqual(2, response.Count, "Should have 2 operation responses");

            // Verify individual operation results
            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];
                Console.WriteLine($"Operation {i}: StatusCode={result.StatusCode}, ETag={result.ETag}");
                
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode, $"Operation {i} should return Created");
                Assert.IsNotNull(result.ETag, $"Operation {i} should have an ETag");
            }

            // Verify documents were actually created by reading them back
            ItemResponse<ToDoActivity> readResponse1 = await this.container.ReadItemAsync<ToDoActivity>(
                doc1.id,
                new PartitionKey(doc1.pk));
            Assert.AreEqual(HttpStatusCode.OK, readResponse1.StatusCode);
            Assert.AreEqual(doc1.id, readResponse1.Resource.id);

            ItemResponse<ToDoActivity> readResponse2 = await this.container.ReadItemAsync<ToDoActivity>(
                doc2.id,
                new PartitionKey(doc2.pk));
            Assert.AreEqual(HttpStatusCode.OK, readResponse2.StatusCode);
            Assert.AreEqual(doc2.id, readResponse2.Resource.id);

            response.Dispose();
        }
    }
}
