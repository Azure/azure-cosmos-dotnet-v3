// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Cosmos.PartitionKey;
    using CosmosDatabase = Microsoft.Azure.Cosmos.Database;

    /// <summary>
    /// End-to-end emulator tests for Distributed Transactions.
    /// These tests run against the actual Cosmos DB service with distributed transaction support enabled.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedTransactionTests
    {
        private const string PartitionKeyPath = "/pk";

        // Custom endpoint and key - set via environment variables:
        // COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY
        private static readonly string CustomEndpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT") 
            ?? "https://localhost:8081";
        private static readonly string CustomMasterKey = Environment.GetEnvironmentVariable("COSMOS_DTX_KEY") 
            ?? "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="; // Local emulator key

        private CosmosClient client;
        private CosmosDatabase database;
        private Container container;
        private readonly CancellationToken cancellationToken = CancellationToken.None;

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

            string databaseId = $"absadbsdk";
            DatabaseResponse dbResponse = await this.client.CreateDatabaseIfNotExistsAsync(databaseId);
            this.database = dbResponse.Database;

            string containerId = $"absacollsdk";
            ContainerResponse containerResponse = await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: containerId, partitionKeyPath: PartitionKeyPath));
            this.container = containerResponse.Container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                try
                {
                    await this.database.DeleteAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            this.client?.Dispose();
        }

        // Happy path scenarios

        [TestMethod]
        [Description("Two creates against the same container both return 201 Created.")]
        public async Task CreateItems_SameContainer_AllReturnCreatedStatus()
        {
            // Arrange
            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            // Act
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(doc1.pk), doc1.id, doc1)
                .CreateItem(this.container, new PartitionKey(doc2.pk), doc2.id, doc2)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert
            Console.WriteLine($"Response StatusCode: {response.StatusCode}");
            Console.WriteLine($"Response ActivityId: {response.ActivityId}");
            Console.WriteLine($"Response Count: {response.Count}");
            Console.WriteLine($"Response ErrorMessage: {response.ErrorMessage}");
            Console.WriteLine($"Response Diagnostics: {response.Diagnostics}");

            // Print details of each operation result
            for (int i = 0; i < response.Count; i++)
            {
                Console.WriteLine($"Operation[{i}]: StatusCode={response[i].StatusCode}, Index={response[i].Index}");
                if (response[i].StatusCode != HttpStatusCode.Created)
                {
                    Console.WriteLine($"  Error details for operation {i}: SubStatusCode={response[i].SubStatusCode}");
                }
            }

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Expected OK but got {response.StatusCode}. Error: {response.ErrorMessage}");
            Assert.IsTrue(response.IsSuccessStatusCode, "Response should indicate success");
            Assert.AreEqual(2, response.Count, "Should have 2 operation responses");
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode, "First operation should return Created");
            Assert.AreEqual(HttpStatusCode.Created, response[1].StatusCode, "Second operation should return Created");

            // Verify documents were actually created by reading them back
            ItemResponse<ToDoActivity> readResponse1 = await this.container.ReadItemAsync<ToDoActivity>(
                doc1.id,
                new PartitionKey(doc1.pk),
                cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, readResponse1.StatusCode);
            Assert.AreEqual(doc1.id, readResponse1.Resource.id);

            ItemResponse<ToDoActivity> readResponse2 = await this.container.ReadItemAsync<ToDoActivity>(
                doc2.id,
                new PartitionKey(doc2.pk),
                cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, readResponse2.StatusCode);
            Assert.AreEqual(doc2.id, readResponse2.Resource.id);

            response.Dispose();
        }

        [TestMethod]
        [Description("Create, Replace, and Delete operations are all committed successfully.")]
        public async Task MixedOperations_AllOperationsCommitted()
        {
            // Arrange - create a document first that we can replace and delete
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity deleteDoc = ToDoActivity.CreateRandomToDoActivity();

            // Pre-create the docs that will be replaced and deleted
            await this.container.CreateItemAsync(replaceDoc, new PartitionKey(replaceDoc.pk), cancellationToken: this.cancellationToken);
            await this.container.CreateItemAsync(deleteDoc, new PartitionKey(deleteDoc.pk), cancellationToken: this.cancellationToken);

            // Modify replaceDoc for the replace operation
            replaceDoc.taskNum = 999;
            replaceDoc.description = "Updated in transaction";

            // Act
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .ReplaceItem(this.container, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .DeleteItem(this.container, new PartitionKey(deleteDoc.pk), deleteDoc.id)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert
            Console.WriteLine($"Response StatusCode: {response.StatusCode}");
            Console.WriteLine($"Response Count: {response.Count}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(3, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode, "Create should return 201");
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode, "Replace should return 200");
            Assert.AreEqual(HttpStatusCode.NoContent, response[2].StatusCode, "Delete should return 204");

            // Verify the create worked
            ItemResponse<ToDoActivity> readCreate = await this.container.ReadItemAsync<ToDoActivity>(
                createDoc.id, new PartitionKey(createDoc.pk), cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, readCreate.StatusCode);

            // Verify the replace worked
            ItemResponse<ToDoActivity> readReplace = await this.container.ReadItemAsync<ToDoActivity>(
                replaceDoc.id, new PartitionKey(replaceDoc.pk), cancellationToken: this.cancellationToken);
            Assert.AreEqual(999, readReplace.Resource.taskNum);

            // Verify the delete worked
            try
            {
                await this.container.ReadItemAsync<ToDoActivity>(
                    deleteDoc.id, new PartitionKey(deleteDoc.pk), cancellationToken: this.cancellationToken);
                Assert.Fail("Document should have been deleted");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Expected
            }

            response.Dispose();
        }

        [TestMethod]
        [Description("Upsert operations work correctly in distributed transactions.")]
        public async Task UpsertItem_IncludedInTransaction_SuccessfullyCommitted()
        {
            // Arrange
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity upsertDoc = ToDoActivity.CreateRandomToDoActivity();

            // Act
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .UpsertItem(this.container, new PartitionKey(upsertDoc.pk), upsertDoc.id, upsertDoc)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert
            Console.WriteLine($"Response StatusCode: {response.StatusCode}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode, "Create should return 201");
            Assert.AreEqual(HttpStatusCode.Created, response[1].StatusCode, "Upsert (insert) should return 201");

            // Verify both documents exist
            ItemResponse<ToDoActivity> readCreate = await this.container.ReadItemAsync<ToDoActivity>(
                createDoc.id, new PartitionKey(createDoc.pk), cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, readCreate.StatusCode);

            ItemResponse<ToDoActivity> readUpsert = await this.container.ReadItemAsync<ToDoActivity>(
                upsertDoc.id, new PartitionKey(upsertDoc.pk), cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, readUpsert.StatusCode);
            Assert.AreEqual(upsertDoc.id, readUpsert.Resource.id);

            response.Dispose();
        }

        [TestMethod]
        [Description("Patch operation works correctly in distributed transactions.")]
        public async Task PatchItem_WithAddOperation_SuccessfullyCommitted()
        {
            // Arrange - create a document first that we can patch
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity patchDoc = ToDoActivity.CreateRandomToDoActivity();
            patchDoc.description = "Original description";

            await this.container.CreateItemAsync(patchDoc, new PartitionKey(patchDoc.pk), cancellationToken: this.cancellationToken);

            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Replace("/description", "Patched in transaction") };

            // Act
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .PatchItem(this.container, new PartitionKey(patchDoc.pk), patchDoc.id, patchOps)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert
            Console.WriteLine($"Response StatusCode: {response.StatusCode}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode, "Create should return 201");
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode, "Patch should return 200");

            // Verify the patch worked
            ItemResponse<ToDoActivity> readPatched = await this.container.ReadItemAsync<ToDoActivity>(
                patchDoc.id, new PartitionKey(patchDoc.pk), cancellationToken: this.cancellationToken);
            Assert.AreEqual("Patched in transaction", readPatched.Resource.description);

            response.Dispose();
        }

        [TestMethod]
        [Description("Operations targeting two different containers are both committed successfully.")]
        public async Task CrossContainer_TwoDifferentContainers_AllOperationsCommitted()
        {
            // Arrange - create a second container
            string secondContainerId = $"DtxSecondContainer_{Guid.NewGuid():N}";
            ContainerResponse secondContainerResponse = await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: secondContainerId, partitionKeyPath: PartitionKeyPath));

            Container secondContainer = secondContainerResponse.Container;

            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            // Act
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(doc1.pk), doc1.id, doc1)
                .CreateItem(secondContainer, new PartitionKey(doc2.pk), doc2.id, doc2)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert
            Console.WriteLine($"Response StatusCode: {response.StatusCode}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.Created, response[1].StatusCode);

            // Verify both documents exist in their respective containers
            ItemResponse<ToDoActivity> readFromFirstContainer = await this.container.ReadItemAsync<ToDoActivity>(
                doc1.id, new PartitionKey(doc1.pk), cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, readFromFirstContainer.StatusCode);

            ItemResponse<ToDoActivity> readFromSecondContainer = await secondContainer.ReadItemAsync<ToDoActivity>(
                doc2.id, new PartitionKey(doc2.pk), cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, readFromSecondContainer.StatusCode);

            // Cleanup second container
            try
            {
                await secondContainer.DeleteContainerAsync(cancellationToken: this.cancellationToken);
            }
            catch
            {
                // Ignore cleanup errors
            }

            response.Dispose();
        }

        // Additional E2E tests can be added here following the same pattern
        // All tests should use this.client, this.database, and this.container
        // and perform real E2E operations against the configured endpoint
    }
}
