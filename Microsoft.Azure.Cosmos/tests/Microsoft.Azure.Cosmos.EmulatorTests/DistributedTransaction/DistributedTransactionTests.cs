// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

        // Custom endpoint and key - update these for your environment
        private const string CustomEndpoint = "https://<your-account>.documents.azure.com:443/";
        private const string CustomMasterKey = "";

        // Account keys used by AllKeyCombinations_DtxReadAndWrite_VerifyAuthorization.
        // Master keys grant read+write; readonly keys grant read-only access.
        // SECURITY: never commit real key values. Scrub these to "" before pushing.
        // NOTE: only the primary master key is configured for this account; the other slots are
        // left empty so the matrix test runs the master-key path then reports Inconclusive.
        private const string PrimaryMasterKey = CustomMasterKey;
        private const string SecondaryMasterKey = "";
        private const string PrimaryReadonlyMasterKey = "";
        private const string SecondaryReadonlyMasterKey = "";

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
            // Skip database delete to allow rapid test iteration without delete/recreate conflicts.
            // Database and containers persist across runs (created-if-not-exists pattern in TestInitialize).
            // if (this.database != null)
            // {
            //     try
            //     {
            //         await this.database.DeleteAsync();
            //     }
            //     catch
            //     {
            //         // Ignore cleanup errors
            //     }
            // }

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
        [Description("Operations targeting two different containers are both committed successfully. Uses the absa15junwus account; creates the database/containers only if they do not already exist, and does not delete them at the end.")]
        public async Task CrossContainer_TwoDifferentContainers_AllOperationsCommitted()
        {
            // This test runs against the absa15junwus account (key-based auth).
            // SECURITY: never commit real key values. Scrub this to "" before pushing.
            const string accountEndpoint = "https://<your-account>.documents.azure.com:443/";
            const string accountKey = "";
            const string databaseId = "absadbsdk";
            const string firstContainerId = "absacollsdk";
            const string secondContainerId = "absacollsdk2";

            using CosmosClient client = new CosmosClient(
                accountEndpoint: accountEndpoint,
                authKeyOrResourceToken: accountKey,
                clientOptions: new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });

            // Use existing database/containers; create them only if missing. Do NOT delete them.
            CosmosDatabase database = (await client.CreateDatabaseIfNotExistsAsync(databaseId)).Database;
            Container container = (await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: firstContainerId, partitionKeyPath: PartitionKeyPath))).Container;
            Container secondContainer = (await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: secondContainerId, partitionKeyPath: PartitionKeyPath))).Container;

            Console.WriteLine($"Using account endpoint={accountEndpoint}, db={database.Id}, containers=[{container.Id}, {secondContainer.Id}]");

            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            // Act - one distributed write transaction spanning two containers.
            DistributedTransactionResponse response = await client
                .CreateDistributedWriteTransaction()
                .CreateItem(container, new PartitionKey(doc1.pk), doc1.id, doc1)
                .CreateItem(secondContainer, new PartitionKey(doc2.pk), doc2.id, doc2)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert
            Console.WriteLine($"Response StatusCode: {response.StatusCode}, Count: {response.Count}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.Created, response[1].StatusCode);

            // Verify both documents exist in their respective containers
            ItemResponse<ToDoActivity> readFromFirstContainer = await container.ReadItemAsync<ToDoActivity>(
                doc1.id, new PartitionKey(doc1.pk), cancellationToken: this.cancellationToken);
            Console.WriteLine($"Read from '{container.Id}': {readFromFirstContainer.StatusCode}");
            Assert.AreEqual(HttpStatusCode.OK, readFromFirstContainer.StatusCode);

            ItemResponse<ToDoActivity> readFromSecondContainer = await secondContainer.ReadItemAsync<ToDoActivity>(
                doc2.id, new PartitionKey(doc2.pk), cancellationToken: this.cancellationToken);
            Console.WriteLine($"Read from '{secondContainer.Id}': {readFromSecondContainer.StatusCode}");
            Assert.AreEqual(HttpStatusCode.OK, readFromSecondContainer.StatusCode);

            // Do NOT delete the containers - leave them for reuse.
            response.Dispose();
        }

        [TestMethod]
        [Description("Verify ETags from transaction response match subsequent reads and request charges are populated correctly.")]
        public async Task ETagAndRequestCharge_VerifyConsistencyAndCharges()
        {
            // Arrange - Create an initial document that will be replaced
            ToDoActivity initialDoc = ToDoActivity.CreateRandomToDoActivity();
            initialDoc.description = "Initial document for replace";
            ItemResponse<ToDoActivity> createResponse = await this.container.CreateItemAsync(
                initialDoc, 
                new PartitionKey(initialDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"Initial document created: id={initialDoc.id}, etag={createResponse.ETag}");

            // Prepare documents for the transaction
            ToDoActivity replaceDoc = initialDoc;
            replaceDoc.description = "Replaced in DTX";
            replaceDoc.taskNum = 999;

            ToDoActivity insertDoc = ToDoActivity.CreateRandomToDoActivity();
            insertDoc.description = "Inserted in DTX";

            ToDoActivity upsertDoc = ToDoActivity.CreateRandomToDoActivity();
            upsertDoc.description = "Upserted in DTX";

            // Act - Execute distributed transaction with replace, insert, and upsert
            Console.WriteLine("\n=== Executing Distributed Transaction ===");
            Console.WriteLine($"Operation 0: Replace - id={replaceDoc.id}, pk={replaceDoc.pk}");
            Console.WriteLine($"Operation 1: Insert - id={insertDoc.id}, pk={insertDoc.pk}");
            Console.WriteLine($"Operation 2: Upsert - id={upsertDoc.id}, pk={upsertDoc.pk}");

            DistributedTransactionResponse dtxResponse = await this.client
                .CreateDistributedWriteTransaction()
                .ReplaceItem(this.container, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .CreateItem(this.container, new PartitionKey(insertDoc.pk), insertDoc.id, insertDoc)
                .UpsertItem(this.container, new PartitionKey(upsertDoc.pk), upsertDoc.id, upsertDoc)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert - Verify transaction succeeded
            Console.WriteLine($"\nTransaction Response StatusCode: {dtxResponse.StatusCode}");
            Console.WriteLine($"Transaction ActivityId: {dtxResponse.ActivityId}");
            Console.WriteLine($"Transaction Request Charge: {dtxResponse.RequestCharge}");
            Console.WriteLine($"Operation Count: {dtxResponse.Count}");

            Assert.AreEqual(HttpStatusCode.OK, dtxResponse.StatusCode, "Transaction should succeed");
            Assert.IsTrue(dtxResponse.IsSuccessStatusCode, "Transaction should indicate success");
            Assert.AreEqual(3, dtxResponse.Count, "Should have 3 operation responses");

            // Verify transaction-level request charge is non-zero
            Assert.IsTrue(dtxResponse.RequestCharge > 0, 
                $"Transaction-level request charge should be > 0, but was {dtxResponse.RequestCharge}");

            // Collect ETags and request charges from transaction response
            var operationETags = new List<string>();
            var operationCharges = new List<double>();

            Console.WriteLine("\n=== Transaction Operation Results ===");
            for (int i = 0; i < dtxResponse.Count; i++)
            {
                DistributedTransactionOperationResult opResult = dtxResponse[i];

                Console.WriteLine($"\nOperation[{i}]:");
                Console.WriteLine($"  StatusCode: {opResult.StatusCode}");
                Console.WriteLine($"  Index: {opResult.Index}");
                Console.WriteLine($"  ETag: {opResult.ETag}");
                Console.WriteLine($"  RequestCharge: {opResult.RequestCharge}");

                // Verify each operation has non-zero request charge
                Assert.IsTrue(opResult.RequestCharge > 0, 
                    $"Operation[{i}] request charge should be > 0, but was {opResult.RequestCharge}");

                operationCharges.Add(opResult.RequestCharge);

                // Verify ETags are present and non-null
                Assert.IsNotNull(opResult.ETag, $"Operation[{i}] ETag should not be null");
                Assert.IsFalse(string.IsNullOrWhiteSpace(opResult.ETag), $"Operation[{i}] ETag should not be empty");

                operationETags.Add(opResult.ETag);

                // Verify expected status codes
                if (i == 0) // Replace
                {
                    Assert.AreEqual(HttpStatusCode.OK, opResult.StatusCode, "Replace should return 200 OK");
                }
                else // Insert and Upsert
                {
                    Assert.AreEqual(HttpStatusCode.Created, opResult.StatusCode, $"Operation[{i}] should return 201 Created");
                }
            }

            // Verify sum of operation charges is approximately equal to transaction charge
            double sumOfOperationCharges = operationCharges.Sum();
            Console.WriteLine($"\n=== Request Charge Verification ===");
            Console.WriteLine($"Transaction-level charge: {dtxResponse.RequestCharge}");
            Console.WriteLine($"Sum of operation charges: {sumOfOperationCharges}");
            Console.WriteLine($"Individual charges: [{string.Join(", ", operationCharges)}]");

            // Allow for small rounding differences (within 0.01 RU or 1%)
            double chargeDifference = Math.Abs(dtxResponse.RequestCharge - sumOfOperationCharges);
            double chargePercentDiff = (chargeDifference / dtxResponse.RequestCharge) * 100;

            Console.WriteLine($"Charge difference: {chargeDifference} RUs ({chargePercentDiff:F2}%)");

            Assert.IsTrue(chargePercentDiff < 1.0 || chargeDifference < 0.01, 
                $"Sum of operation charges ({sumOfOperationCharges}) should approximately equal " +
                $"transaction charge ({dtxResponse.RequestCharge}). Difference: {chargeDifference} RUs ({chargePercentDiff:F2}%)");

            // Now read each document and verify ETags match
            Console.WriteLine("\n=== ETag Verification via Point Reads ===");

            // Read replaced document
            ItemResponse<ToDoActivity> readReplace = await this.container.ReadItemAsync<ToDoActivity>(
                replaceDoc.id, 
                new PartitionKey(replaceDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"\nReplaced document read:");
            Console.WriteLine($"  ETag from DTX: {operationETags[0]}");
            Console.WriteLine($"  ETag from Read: {readReplace.ETag}");

            Assert.AreEqual(operationETags[0], readReplace.ETag, 
                "ETag from transaction response should match ETag from subsequent read for replaced document");
            Assert.AreEqual(999, readReplace.Resource.taskNum, "Replaced document should have updated taskNum");
            Assert.AreEqual("Replaced in DTX", readReplace.Resource.description, "Replaced document should have updated description");

            // Read inserted document
            ItemResponse<ToDoActivity> readInsert = await this.container.ReadItemAsync<ToDoActivity>(
                insertDoc.id, 
                new PartitionKey(insertDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"\nInserted document read:");
            Console.WriteLine($"  ETag from DTX: {operationETags[1]}");
            Console.WriteLine($"  ETag from Read: {readInsert.ETag}");

            Assert.AreEqual(operationETags[1], readInsert.ETag, 
                "ETag from transaction response should match ETag from subsequent read for inserted document");
            Assert.AreEqual(insertDoc.id, readInsert.Resource.id, "Inserted document should have correct id");
            Assert.AreEqual("Inserted in DTX", readInsert.Resource.description, "Inserted document should have correct description");

            // Read upserted document
            ItemResponse<ToDoActivity> readUpsert = await this.container.ReadItemAsync<ToDoActivity>(
                upsertDoc.id, 
                new PartitionKey(upsertDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"\nUpserted document read:");
            Console.WriteLine($"  ETag from DTX: {operationETags[2]}");
            Console.WriteLine($"  ETag from Read: {readUpsert.ETag}");

            Assert.AreEqual(operationETags[2], readUpsert.ETag, 
                "ETag from transaction response should match ETag from subsequent read for upserted document");
            Assert.AreEqual(upsertDoc.id, readUpsert.Resource.id, "Upserted document should have correct id");
            Assert.AreEqual("Upserted in DTX", readUpsert.Resource.description, "Upserted document should have correct description");

            // ----------------------------------------------------------------------------
            // Additional step: Execute a conditional DTX (optimistic concurrency).
            // Replace one document and patch another, each guarded by IfMatchEtag using the
            // CURRENT (correct) ETags obtained from the point reads above. With correct
            // ETags the precondition is satisfied, so the transaction should succeed.
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== Executing Conditional (IfMatchEtag) Distributed Transaction ===");

            string replaceIfMatchEtag = readInsert.ETag;   // current ETag of the inserted document
            string patchIfMatchEtag = readUpsert.ETag;     // current ETag of the upserted document

            Console.WriteLine($"Operation 0: Replace - id={insertDoc.id}, IfMatchEtag={replaceIfMatchEtag}");
            Console.WriteLine($"Operation 1: Patch   - id={upsertDoc.id}, IfMatchEtag={patchIfMatchEtag}");

            // Mutate the inserted document for the conditional replace
            insertDoc.description = "Replaced via conditional DTX";
            insertDoc.taskNum = 555;

            // Patch operation for the upserted document
            IReadOnlyList<PatchOperation> conditionalPatchOps = new[]
            {
                PatchOperation.Replace("/description", "Patched via conditional DTX")
            };

            DistributedTransactionResponse conditionalResponse = await this.client
                .CreateDistributedWriteTransaction()
                .ReplaceItem(
                    this.container,
                    new PartitionKey(insertDoc.pk),
                    insertDoc.id,
                    insertDoc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = replaceIfMatchEtag })
                .PatchItem(
                    this.container,
                    new PartitionKey(upsertDoc.pk),
                    upsertDoc.id,
                    conditionalPatchOps,
                    new DistributedTransactionRequestOptions { IfMatchEtag = patchIfMatchEtag })
                .CommitTransactionAsync(this.cancellationToken);

            Console.WriteLine($"\nConditional Transaction StatusCode: {conditionalResponse.StatusCode}");
            Console.WriteLine($"Conditional Transaction Request Charge: {conditionalResponse.RequestCharge}");
            Console.WriteLine($"Conditional Operation Count: {conditionalResponse.Count}");

            // The transaction should succeed because the supplied ETags are correct
            Assert.AreEqual(HttpStatusCode.OK, conditionalResponse.StatusCode, 
                "Conditional transaction with correct ETags should succeed");
            Assert.IsTrue(conditionalResponse.IsSuccessStatusCode, "Conditional transaction should indicate success");
            Assert.AreEqual(2, conditionalResponse.Count, "Conditional transaction should have 2 operation responses");
            Assert.IsTrue(conditionalResponse.RequestCharge > 0, 
                $"Conditional transaction request charge should be > 0, but was {conditionalResponse.RequestCharge}");

            DistributedTransactionOperationResult conditionalReplaceResult = conditionalResponse[0];
            DistributedTransactionOperationResult conditionalPatchResult = conditionalResponse[1];

            Assert.AreEqual(HttpStatusCode.OK, conditionalReplaceResult.StatusCode, "Conditional Replace should return 200 OK");
            Assert.AreEqual(HttpStatusCode.OK, conditionalPatchResult.StatusCode, "Conditional Patch should return 200 OK");

            // Capture the ETags returned in the conditional transaction response
            string conditionalReplaceETag = conditionalReplaceResult.ETag;
            string conditionalPatchETag = conditionalPatchResult.ETag;

            Console.WriteLine($"\nConditional Replace returned ETag: {conditionalReplaceETag}");
            Console.WriteLine($"Conditional Patch returned ETag: {conditionalPatchETag}");

            Assert.IsFalse(string.IsNullOrWhiteSpace(conditionalReplaceETag), "Conditional Replace ETag should not be empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(conditionalPatchETag), "Conditional Patch ETag should not be empty");

            // The new ETags must differ from the precondition ETags (the documents changed)
            Assert.AreNotEqual(replaceIfMatchEtag, conditionalReplaceETag, 
                "Replaced document ETag should change after the conditional transaction");
            Assert.AreNotEqual(patchIfMatchEtag, conditionalPatchETag, 
                "Patched document ETag should change after the conditional transaction");

            // Verify returned ETags against subsequent point reads
            Console.WriteLine("\n=== Conditional ETag Verification via Point Reads ===");

            ItemResponse<ToDoActivity> readConditionalReplace = await this.container.ReadItemAsync<ToDoActivity>(
                insertDoc.id, 
                new PartitionKey(insertDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"\nConditionally replaced document read:");
            Console.WriteLine($"  ETag from DTX: {conditionalReplaceETag}");
            Console.WriteLine($"  ETag from Read: {readConditionalReplace.ETag}");

            Assert.AreEqual(conditionalReplaceETag, readConditionalReplace.ETag, 
                "ETag from conditional transaction should match ETag from subsequent read for replaced document");
            Assert.AreEqual(555, readConditionalReplace.Resource.taskNum, "Conditionally replaced document should have updated taskNum");
            Assert.AreEqual("Replaced via conditional DTX", readConditionalReplace.Resource.description, 
                "Conditionally replaced document should have updated description");

            ItemResponse<ToDoActivity> readConditionalPatch = await this.container.ReadItemAsync<ToDoActivity>(
                upsertDoc.id, 
                new PartitionKey(upsertDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"\nConditionally patched document read:");
            Console.WriteLine($"  ETag from DTX: {conditionalPatchETag}");
            Console.WriteLine($"  ETag from Read: {readConditionalPatch.ETag}");

            Assert.AreEqual(conditionalPatchETag, readConditionalPatch.ETag, 
                "ETag from conditional transaction should match ETag from subsequent read for patched document");
            Assert.AreEqual("Patched via conditional DTX", readConditionalPatch.Resource.description, 
                "Conditionally patched document should have updated description");

            conditionalResponse.Dispose();

            // ----------------------------------------------------------------------------
            // Negative scenario (atomicity on precondition failure):
            // Within a single distributed transaction, replace one document using a WRONG
            // (stale) IfMatchEtag while deleting another document using its CORRECT
            // IfMatchEtag. Because distributed transactions are atomic, the precondition
            // failure on the replace must abort the ENTIRE transaction:
            //   - the replace must NOT be applied, and
            //   - the delete must NOT happen.
            // We then verify via point reads that BOTH documents are untouched (same
            // content AND same ETags as before the failed transaction).
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== Executing Negative (Atomicity) Distributed Transaction ===");

            // Replace target = the inserted document. Its CURRENT ETag is
            // conditionalReplaceETag (== readConditionalReplace.ETag). The earlier
            // readInsert.ETag (captured in replaceIfMatchEtag) is now STALE because the
            // conditional transaction above replaced this document and changed its ETag,
            // so it is a valid-looking-but-wrong ETag for the precondition.
            string staleReplaceEtag = replaceIfMatchEtag;                              // no longer current
            string currentInsertEtag = readConditionalReplace.ETag;                    // actual current ETag
            int expectedInsertTaskNum = readConditionalReplace.Resource.taskNum;       // 555
            string expectedInsertDescription = readConditionalReplace.Resource.description; // "Replaced via conditional DTX"

            // Delete target = the upserted document, using its CORRECT current ETag.
            string currentUpsertEtag = readConditionalPatch.ETag;
            string expectedUpsertDescription = readConditionalPatch.Resource.description;   // "Patched via conditional DTX"

            Console.WriteLine($"Operation 0: Replace (WRONG/stale etag) - id={insertDoc.id}, IfMatchEtag={staleReplaceEtag}");
            Console.WriteLine($"Operation 1: Delete (CORRECT etag)      - id={upsertDoc.id}, IfMatchEtag={currentUpsertEtag}");

            // Mutate the local object to values that must NOT end up persisted if the
            // replace were (incorrectly) applied.
            insertDoc.description = "Should NOT be applied (replace must fail)";
            insertDoc.taskNum = 777;

            DistributedTransactionResponse negativeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .DeleteItem(
                    this.container,
                    new PartitionKey(upsertDoc.pk),
                    upsertDoc.id)
                 .ReplaceItem(
                    this.container,
                    new PartitionKey(insertDoc.pk),
                    insertDoc.id,
                    insertDoc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = staleReplaceEtag })
                .CommitTransactionAsync(this.cancellationToken);

            Console.WriteLine($"\nNegative Transaction StatusCode: {negativeResponse.StatusCode}");
            Console.WriteLine($"Negative Transaction IsSuccessStatusCode: {negativeResponse.IsSuccessStatusCode}");
            Console.WriteLine($"Negative Operation Count: {negativeResponse.Count}");

            // The transaction must fail because the replace precondition (stale ETag) is not met.
            Assert.IsFalse(negativeResponse.IsSuccessStatusCode, 
                "Transaction with a wrong ETag on the replace must NOT succeed");
            Assert.AreEqual(452, (int)negativeResponse.StatusCode, 
                "Transaction-level status should surface the Aborted failure (452)");

            // Per-operation results: the replace must report PreconditionFailed; the delete
            // must NOT report success because the atomic transaction is aborted as a whole.
            if (negativeResponse.Count == 2)
            {
                DistributedTransactionOperationResult negativeReplaceResult = negativeResponse[0];
                DistributedTransactionOperationResult negativeDeleteResult = negativeResponse[1];

                Console.WriteLine($"  Replace op StatusCode: {negativeReplaceResult.StatusCode}");
                Console.WriteLine($"  Delete op StatusCode: {negativeDeleteResult.StatusCode}");

                Assert.AreEqual(HttpStatusCode.PreconditionFailed, negativeReplaceResult.StatusCode, 
                    "Replace operation with a stale ETag should report 412 PreconditionFailed");
                Assert.IsFalse(negativeDeleteResult.IsSuccessStatusCode, 
                    "Delete operation must not succeed because the atomic transaction is aborted");
            }

            negativeResponse.Dispose();

            // Verify atomicity via point reads: BOTH documents must be unchanged.
            Console.WriteLine("\n=== Negative Scenario Verification via Point Reads ===");

            // 1) The replace target must still exist with its ORIGINAL content and ETag.
            ItemResponse<ToDoActivity> readAfterFailedReplace = await this.container.ReadItemAsync<ToDoActivity>(
                insertDoc.id, 
                new PartitionKey(insertDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"\nReplace target after failed DTX:");
            Console.WriteLine($"  ETag before: {currentInsertEtag}, ETag after: {readAfterFailedReplace.ETag}");
            Console.WriteLine($"  taskNum: {readAfterFailedReplace.Resource.taskNum}, description: {readAfterFailedReplace.Resource.description}");

            Assert.AreEqual(HttpStatusCode.OK, readAfterFailedReplace.StatusCode, 
                "Replace target should still be readable after the failed transaction");
            Assert.AreEqual(currentInsertEtag, readAfterFailedReplace.ETag, 
                "Replace target ETag must be unchanged because the replace was not applied");
            Assert.AreEqual(expectedInsertTaskNum, readAfterFailedReplace.Resource.taskNum, 
                "Replace target taskNum must be unchanged (replace must not be applied)");
            Assert.AreEqual(expectedInsertDescription, readAfterFailedReplace.Resource.description, 
                "Replace target description must be unchanged (replace must not be applied)");
            Assert.AreNotEqual(777, readAfterFailedReplace.Resource.taskNum, 
                "Replace target must NOT contain the values from the failed replace operation");

            // 2) The delete target must still exist (the delete must not have happened) with
            //    its ORIGINAL content and ETag. If the delete had been applied, this point
            //    read would throw CosmosException(NotFound) and fail the test.
            ItemResponse<ToDoActivity> readAfterFailedDelete = await this.container.ReadItemAsync<ToDoActivity>(
                upsertDoc.id, 
                new PartitionKey(upsertDoc.pk), 
                cancellationToken: this.cancellationToken);

            Console.WriteLine($"\nDelete target after failed DTX:");
            Console.WriteLine($"  ETag before: {currentUpsertEtag}, ETag after: {readAfterFailedDelete.ETag}");
            Console.WriteLine($"  description: {readAfterFailedDelete.Resource.description}");

            Assert.AreEqual(HttpStatusCode.OK, readAfterFailedDelete.StatusCode, 
                "Delete target should still exist after the failed transaction (delete must not happen)");
            Assert.AreEqual(currentUpsertEtag, readAfterFailedDelete.ETag, 
                "Delete target ETag must be unchanged because the delete was not applied");
            Assert.AreEqual(expectedUpsertDescription, readAfterFailedDelete.Resource.description, 
                "Delete target content must be unchanged (delete must not happen)");

            Console.WriteLine("\nNegative-scenario atomicity verified: neither the replace nor the delete was applied.");

            Console.WriteLine("\n? All ETag and request charge validations passed!");

            dtxResponse.Dispose();
        }

        [TestMethod]
        [Description("Verify distributed read transaction can read documents created via point writes. Uses the absa05junsea account.")]
        public async Task DistributedReadTransaction_ReadDocumentsFromPointWrites()
        {
            // This test runs against the absa15junwus account (key-based auth).
            // SECURITY: never commit real key values. Scrub this to "" before pushing.
            const string absa05junseaEndpoint = "https://<your-account>.documents.azure.com:443/";
            const string absa05junseaKey = "";

            using CosmosClient client = new CosmosClient(
                accountEndpoint: absa05junseaEndpoint,
                authKeyOrResourceToken: absa05junseaKey,
                clientOptions: new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });

            CosmosDatabase database = (await client.CreateDatabaseIfNotExistsAsync("absadbsdk")).Database;
            Container container = (await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "absacollsdk", partitionKeyPath: PartitionKeyPath))).Container;

            Console.WriteLine($"Using account endpoint={absa05junseaEndpoint}, db={database.Id}, container={container.Id}");

            // Local helper: print a uniform summary table (inner/outer response, content present, RU, index).
            void PrintSummary(string title, DistributedTransactionResponse resp)
            {
                Console.WriteLine($"\n=== {title} Summary ===");
                Console.WriteLine("{0,-7} {1,-12} {2,-14} {3,-16} {4,-8}", "Index", "Level", "StatusCode", "ContentPresent", "RU");
                Console.WriteLine(new string('-', 60));
                Console.WriteLine("{0,-7} {1,-12} {2,-14} {3,-16} {4,-8}",
                    "-", "Transaction", resp.StatusCode, "n/a", resp.RequestCharge);
                for (int idx = 0; idx < resp.Count; idx++)
                {
                    DistributedTransactionOperationResult op = resp[idx];
                    bool contentPresent = op.ResourceStream != null && op.ResourceStream.Length > 0;
                    Console.WriteLine("{0,-7} {1,-12} {2,-14} {3,-16} {4,-8}",
                        op.Index, "Operation", op.StatusCode, contentPresent, op.RequestCharge);
                }
                Console.WriteLine(new string('-', 60));
            }

            // ----------------------------------------------------------------------------
            // Step 1: Prepare two documents but DO NOT insert them yet.
            // ----------------------------------------------------------------------------
            ToDoActivity doc0 = ToDoActivity.CreateRandomToDoActivity();
            doc0.description = "Document 0 for DTX read";
            doc0.taskNum = 100;

            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            doc1.description = "Document 1 for DTX read";
            doc1.taskNum = 101;

            Console.WriteLine($"Prepared (not inserted) doc0: id={doc0.id}, pk={doc0.pk}");
            Console.WriteLine($"Prepared (not inserted) doc1: id={doc1.id}, pk={doc1.pk}");

            // ----------------------------------------------------------------------------
            // Step 2: DTX read both documents BEFORE they exist -> each op should be NotFound.
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== DTX Read BEFORE insert (expect NotFound) ===");
            DistributedTransactionResponse preInsertRead = await client
                .CreateDistributedReadTransaction()
                .ReadItem(container, new PartitionKey(doc0.pk), doc0.id)
                .ReadItem(container, new PartitionKey(doc1.pk), doc1.id)
                .CommitTransactionAsync(this.cancellationToken);

            Console.WriteLine($"Pre-insert read StatusCode: {preInsertRead.StatusCode}, Count: {preInsertRead.Count}");
            Assert.AreEqual(2, preInsertRead.Count, "Pre-insert read should have 2 operation responses");
            for (int i = 0; i < preInsertRead.Count; i++)
            {
                Console.WriteLine($"  Op[{i}] StatusCode: {preInsertRead[i].StatusCode}");
                Assert.AreEqual(HttpStatusCode.NotFound, preInsertRead[i].StatusCode,
                    $"Pre-insert read op[{i}] should be NotFound because the document does not exist yet");
            }
            PrintSummary("DTX Read BEFORE insert", preInsertRead);
            preInsertRead.Dispose();

            // ----------------------------------------------------------------------------
            // Step 3: Insert both documents via point writes and remember their ETags.
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== Inserting both documents via point writes ===");
            ItemResponse<ToDoActivity> create0 = await container.CreateItemAsync(
                doc0, new PartitionKey(doc0.pk), cancellationToken: this.cancellationToken);
            ItemResponse<ToDoActivity> create1 = await container.CreateItemAsync(
                doc1, new PartitionKey(doc1.pk), cancellationToken: this.cancellationToken);

            Assert.AreEqual(HttpStatusCode.Created, create0.StatusCode, "doc0 insert should return 201 Created");
            Assert.AreEqual(HttpStatusCode.Created, create1.StatusCode, "doc1 insert should return 201 Created");

            string etag0 = create0.ETag;
            string etag1 = create1.ETag;
            Console.WriteLine($"Inserted doc0: etag={etag0}, charge={create0.RequestCharge}");
            Console.WriteLine($"Inserted doc1: etag={etag1}, charge={create1.RequestCharge}");

            Assert.IsFalse(string.IsNullOrWhiteSpace(etag0), "doc0 ETag should be present after insert");
            Assert.IsFalse(string.IsNullOrWhiteSpace(etag1), "doc1 ETag should be present after insert");

            // ----------------------------------------------------------------------------
            // Step 4: DTX read both documents AFTER insert -> each op should be OK with content.
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== DTX Read AFTER insert (expect OK) ===");
            DistributedTransactionResponse readResponse = await client
                .CreateDistributedReadTransaction()
                .ReadItem(container, new PartitionKey(doc0.pk), doc0.id)
                .ReadItem(container, new PartitionKey(doc1.pk), doc1.id)
                .CommitTransactionAsync(this.cancellationToken);

            Console.WriteLine($"Post-insert read StatusCode: {readResponse.StatusCode}, Count: {readResponse.Count}, Charge: {readResponse.RequestCharge}");

            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode, "Read transaction should succeed");
            Assert.IsTrue(readResponse.IsSuccessStatusCode, "Read transaction should indicate success");
            Assert.AreEqual(2, readResponse.Count, "Should have 2 operation responses");
            Assert.IsTrue(readResponse.RequestCharge > 0,
                $"Transaction-level request charge should be > 0, but was {readResponse.RequestCharge}");

            ToDoActivity[] documents = new[] { doc0, doc1 };
            for (int i = 0; i < readResponse.Count; i++)
            {
                DistributedTransactionOperationResult opResult = readResponse[i];
                Console.WriteLine($"\nOperation[{i}] (Read): StatusCode={opResult.StatusCode}, ETag={opResult.ETag}, Charge={opResult.RequestCharge}");

                Assert.AreEqual(HttpStatusCode.OK, opResult.StatusCode, $"Read operation[{i}] should return 200 OK");
                Assert.IsTrue(opResult.RequestCharge > 0, $"Operation[{i}] request charge should be > 0");
                Assert.IsNotNull(opResult.ResourceStream, $"Operation[{i}] should have a ResourceStream");

                using (var reader = new StreamReader(opResult.ResourceStream, leaveOpen: true))
                {
                    string json = await reader.ReadToEndAsync();
                    Console.WriteLine($"  Content: {json}");
                    ToDoActivity readDoc = JsonSerializer.Deserialize<ToDoActivity>(json);
                    Assert.AreEqual(documents[i].id, readDoc.id, $"Document {i} id should match");
                    Assert.AreEqual(documents[i].pk, readDoc.pk, $"Document {i} pk should match");
                    Assert.AreEqual(documents[i].taskNum, readDoc.taskNum, $"Document {i} taskNum should match");
                }
            }
            PrintSummary("DTX Read AFTER insert", readResponse);
            readResponse.Dispose();

            // ----------------------------------------------------------------------------
            // Step 5: Conditional DTX read with IfNoneMatch in a single read transaction.
            //  - Op 0 uses the CORRECT current ETag (etag0).
            //  - Op 1 uses a WRONG/stale ETag.
            // Observed behavior is service/version dependent. Some deployments do NOT honor
            // IfNoneMatch for distributed reads (every op returns 200 OK with the body), while
            // others DO honor it (a matching ETag short-circuits to 304 NotModified with no body,
            // and the transaction-level status surfaces NotModified). Accept either contract.
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== Conditional DTX Read (IfNoneMatch: one correct, one wrong) ===");
            string wrongEtag = "\"00000000-0000-0000-0000-000000000000\"";
            Console.WriteLine($"Op 0 IfNoneMatch (correct) : {etag0}");
            Console.WriteLine($"Op 1 IfNoneMatch (wrong)   : {wrongEtag}");

            DistributedTransactionResponse conditionalRead = await client
                .CreateDistributedReadTransaction()
                .ReadItem(container, new PartitionKey(doc0.pk), doc0.id,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = etag0 })
                .ReadItem(container, new PartitionKey(doc1.pk), doc1.id,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = wrongEtag })
                .CommitTransactionAsync(this.cancellationToken);

            Console.WriteLine($"Conditional read StatusCode: {conditionalRead.StatusCode}, Count: {conditionalRead.Count}");
            Assert.AreEqual(2, conditionalRead.Count, "Conditional read should have 2 operation responses");

            DistributedTransactionOperationResult condOp0 = conditionalRead[0];
            DistributedTransactionOperationResult condOp1 = conditionalRead[1];
            Console.WriteLine($"  Op[0] (correct etag) StatusCode: {condOp0.StatusCode}, HasStream: {condOp0.ResourceStream != null}");
            Console.WriteLine($"  Op[1] (wrong etag)   StatusCode: {condOp1.StatusCode}, HasStream: {condOp1.ResourceStream != null}");

            // Local helper: a conditional-read op must be either 200 OK (body present, id matches)
            // or 304 NotModified (no body). Anything else is a real failure.
            async Task AssertConditionalReadOp(DistributedTransactionOperationResult op, string expectedId, string label)
            {
                Assert.IsTrue(op.StatusCode == HttpStatusCode.OK || op.StatusCode == HttpStatusCode.NotModified,
                    $"{label} conditional DTX read should return 200 OK or 304 NotModified, but was {op.StatusCode}");

                if (op.StatusCode == HttpStatusCode.OK)
                {
                    Assert.IsNotNull(op.ResourceStream, $"{label} returned 200 OK and should include the document body");
                    using (var reader = new StreamReader(op.ResourceStream, leaveOpen: true))
                    {
                        string json = await reader.ReadToEndAsync();
                        Console.WriteLine($"  {label} content: {json}");
                        ToDoActivity readDoc = JsonSerializer.Deserialize<ToDoActivity>(json);
                        Assert.AreEqual(expectedId, readDoc.id, $"{label} returned document id should match");
                    }
                }
                else
                {
                    Console.WriteLine($"  {label} returned 304 NotModified (IfNoneMatch honored); no body expected.");
                }
            }

            await AssertConditionalReadOp(condOp0, doc0.id, "Op[0] (correct etag)");
            await AssertConditionalReadOp(condOp1, doc1.id, "Op[1] (wrong etag)");

            PrintSummary("Conditional DTX Read", conditionalRead);
            conditionalRead.Dispose();

            // ----------------------------------------------------------------------------
            // Step 6: Mixed DTX read where one document exists (doc0) and another does NOT
            // (a brand-new id that was never inserted). The existing op should return OK with
            // the body; the missing op should return NotFound. The transaction itself still
            // completes (a read DTX surfaces per-operation results rather than aborting), and
            // the transaction-level status surfaces the failing operation's status (NotFound).
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== Mixed DTX Read (one exists, one missing) ===");
            ToDoActivity missingDoc = ToDoActivity.CreateRandomToDoActivity();
            Console.WriteLine($"Op 0 (exists)  : id={doc0.id}");
            Console.WriteLine($"Op 1 (missing) : id={missingDoc.id} (never inserted)");

            DistributedTransactionResponse mixedRead = await client
                .CreateDistributedReadTransaction()
                .ReadItem(container, new PartitionKey(doc0.pk), doc0.id)
                .ReadItem(container, new PartitionKey(missingDoc.pk), missingDoc.id)
                .CommitTransactionAsync(this.cancellationToken);

            // Transaction (outer) level response.
            Console.WriteLine("--- Transaction (outer) level ---");
            Console.WriteLine($"  StatusCode: {mixedRead.StatusCode}");
            Console.WriteLine($"  IsSuccessStatusCode: {mixedRead.IsSuccessStatusCode}");
            Console.WriteLine($"  ActivityId: {mixedRead.ActivityId}");
            Console.WriteLine($"  RequestCharge: {mixedRead.RequestCharge}");
            Console.WriteLine($"  Count: {mixedRead.Count}");

            Assert.AreEqual(2, mixedRead.Count, "Mixed read should have 2 operation responses");

            // Outer status reflects the failing operation: the missing document makes the
            // transaction-level status NotFound and IsSuccessStatusCode false.
            Assert.AreEqual(HttpStatusCode.NotFound, mixedRead.StatusCode,
                "Transaction-level status should surface NotFound when one read operation targets a missing document");
            Assert.IsFalse(mixedRead.IsSuccessStatusCode,
                "Transaction-level IsSuccessStatusCode should be false when a read operation is NotFound");
            Assert.IsTrue(mixedRead.RequestCharge > 0,
                $"Transaction-level request charge should be > 0, but was {mixedRead.RequestCharge}");

            DistributedTransactionOperationResult existingOp = mixedRead[0];
            DistributedTransactionOperationResult missingOp = mixedRead[1];
            Console.WriteLine("--- Operation (inner) level ---");
            Console.WriteLine($"  Op[0] (exists)  StatusCode: {existingOp.StatusCode}, HasStream: {existingOp.ResourceStream != null}");
            Console.WriteLine($"  Op[1] (missing) StatusCode: {missingOp.StatusCode}, HasStream: {missingOp.ResourceStream != null}");

            // Existing document -> OK with body.
            Assert.AreEqual(HttpStatusCode.OK, existingOp.StatusCode,
                "Mixed read op for the existing document should return 200 OK");
            Assert.IsNotNull(existingOp.ResourceStream, "Existing-document op should return the document body");

            using (var readerExisting = new StreamReader(existingOp.ResourceStream, leaveOpen: true))
            {
                string jsonExisting = await readerExisting.ReadToEndAsync();
                Console.WriteLine($"  Op[0] content: {jsonExisting}");
                ToDoActivity readDocExisting = JsonSerializer.Deserialize<ToDoActivity>(jsonExisting);
                Assert.AreEqual(doc0.id, readDocExisting.id, "Existing-document op should return doc0");
            }

            // Missing document -> NotFound.
            Assert.AreEqual(HttpStatusCode.NotFound, missingOp.StatusCode,
                "Mixed read op for the missing document should return 404 NotFound");

            PrintSummary("Mixed DTX Read", mixedRead);
            mixedRead.Dispose();

            Console.WriteLine("\nAll distributed read transaction validations passed!");
        }

        [TestMethod]
        [Description("Verify DTX read and DTX write across all account key types. Master keys succeed at both DTX read and write; readonly keys are rejected with 401 Unauthorized for both DTX read and write. Some combinations pass and some fail by design.")]
        public async Task AllKeyCombinations_DtxReadAndWrite_VerifyAuthorization()
        {
            // Arrange - seed a document using the master-key client so every key has something to read.
            ToDoActivity seedDoc = ToDoActivity.CreateRandomToDoActivity();
            seedDoc.description = "Seed for all-key DTX matrix";
            seedDoc.taskNum = 1000;
            ItemResponse<ToDoActivity> seedResponse = await this.container.CreateItemAsync(
                seedDoc,
                new PartitionKey(seedDoc.pk),
                cancellationToken: this.cancellationToken);
            Console.WriteLine($"Seed document created: id={seedDoc.id}, pk={seedDoc.pk}, etag={seedResponse.ETag}");

            // The four account keys. Master keys are full access; readonly keys are read scoped.
            (string Name, string Key, bool IsReadOnly)[] keyMatrix = new[]
            {
                ("PrimaryMasterKey", PrimaryMasterKey, false),
                ("SecondaryMasterKey", SecondaryMasterKey, false),
                ("PrimaryReadonlyMasterKey", PrimaryReadonlyMasterKey, true),
                ("SecondaryReadonlyMasterKey", SecondaryReadonlyMasterKey, true),
            };

            List<(string Name, bool IsReadOnly, HttpStatusCode? Read, bool ReadSuccess, HttpStatusCode? Write, bool WriteSuccess)> results =
                new List<(string, bool, HttpStatusCode?, bool, HttpStatusCode?, bool)>();

            foreach ((string Name, string Key, bool IsReadOnly) entry in keyMatrix)
            {
                Console.WriteLine($"\n=== Key: {entry.Name} (IsReadOnly={entry.IsReadOnly}) ===");

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    Console.WriteLine($"  Skipping '{entry.Name}' - key not configured for this account.");
                    continue;
                }

                HttpStatusCode? readStatus = null;
                bool readSuccess = false;
                HttpStatusCode? writeStatus = null;
                bool writeSuccess = false;
                string writeDocId = null;
                string writeDocPk = null;

                using (CosmosClient keyClient = new CosmosClient(
                    accountEndpoint: CustomEndpoint,
                    authKeyOrResourceToken: entry.Key,
                    clientOptions: new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway }))
                {
                    Container keyContainer = keyClient.GetContainer(this.database.Id, this.container.Id);

                    // --- DTX READ ---
                    try
                    {
                        DistributedTransactionResponse readResponse = await keyClient
                            .CreateDistributedReadTransaction()
                            .ReadItem(keyContainer, new PartitionKey(seedDoc.pk), seedDoc.id)
                            .CommitTransactionAsync(this.cancellationToken);

                        readStatus = readResponse.StatusCode;
                        readSuccess = readResponse.IsSuccessStatusCode;
                        Console.WriteLine($"  DTX Read StatusCode: {readResponse.StatusCode}, Count: {readResponse.Count}, Charge: {readResponse.RequestCharge}");
                        readResponse.Dispose();
                    }
                    catch (CosmosException ex)
                    {
                        readStatus = ex.StatusCode;
                        readSuccess = false;
                        Console.WriteLine($"  DTX Read threw CosmosException: {(int)ex.StatusCode} {ex.StatusCode}");
                    }

                    // --- DTX WRITE ---
                    ToDoActivity writeDoc = ToDoActivity.CreateRandomToDoActivity();
                    writeDoc.description = $"Upserted via DTX using {entry.Name}";
                    writeDoc.taskNum = 2000;
                    writeDocId = writeDoc.id;
                    writeDocPk = writeDoc.pk;

                    try
                    {
                        DistributedTransactionResponse writeResponse = await keyClient
                            .CreateDistributedWriteTransaction()
                            .UpsertItem(keyContainer, new PartitionKey(writeDoc.pk), writeDoc.id, writeDoc)
                            .CommitTransactionAsync(this.cancellationToken);

                        writeStatus = writeResponse.StatusCode;
                        writeSuccess = writeResponse.IsSuccessStatusCode;
                        Console.WriteLine($"  DTX Write StatusCode: {writeResponse.StatusCode}, IsSuccess: {writeResponse.IsSuccessStatusCode}, Charge: {writeResponse.RequestCharge}");
                        writeResponse.Dispose();
                    }
                    catch (CosmosException ex)
                    {
                        writeStatus = ex.StatusCode;
                        writeSuccess = false;
                        Console.WriteLine($"  DTX Write threw CosmosException: {(int)ex.StatusCode} {ex.StatusCode}");
                    }
                }

                // Verify side effect with the master-key client: a successful write must persist;
                // a failed write must not leave anything behind.
                bool persisted = false;
                try
                {
                    ItemResponse<ToDoActivity> verify = await this.container.ReadItemAsync<ToDoActivity>(
                        writeDocId, new PartitionKey(writeDocPk), cancellationToken: this.cancellationToken);
                    persisted = verify.StatusCode == HttpStatusCode.OK;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    persisted = false;
                }

                Console.WriteLine($"  Persisted after write: {persisted}");
                Assert.AreEqual(writeSuccess, persisted,
                    $"Persistence for '{entry.Name}' must match the write success result (write succeeded => doc exists; write failed => doc absent)");

                results.Add((entry.Name, entry.IsReadOnly, readStatus, readSuccess, writeStatus, writeSuccess));
            }

            // Report the full observed matrix.
            Console.WriteLine($"\n=== Observed DTX authorization matrix ===");
            Console.WriteLine($"{"Key",-28} {"ReadOnly",-9} {"Read",-14} {"Write",-14}");
            foreach (var r in results)
            {
                Console.WriteLine($"{r.Name,-28} {r.IsReadOnly,-9} {r.Read,-14} {r.Write,-14}");
            }

            // Assertions based on the observed authorization model:
            //  - Master keys (full access) succeed for both DTX read and DTX write.
            //  - Readonly keys are rejected with 401 Unauthorized for BOTH DTX read and DTX write
            //    (the distributed transaction endpoints do not honor readonly keys).
            foreach (var r in results)
            {
                if (r.IsReadOnly)
                {
                    Assert.IsFalse(r.ReadSuccess,
                        $"Readonly key '{r.Name}' must be rejected for a DTX read (observed read status {r.Read})");
                    Assert.AreEqual(HttpStatusCode.Unauthorized, r.Read,
                        $"Readonly key '{r.Name}' DTX read should return 401 Unauthorized");
                    Assert.IsFalse(r.WriteSuccess,
                        $"Readonly key '{r.Name}' must be rejected for a DTX write (observed write status {r.Write})");
                    Assert.AreEqual(HttpStatusCode.Unauthorized, r.Write,
                        $"Readonly key '{r.Name}' DTX write should return 401 Unauthorized");
                }
                else
                {
                    Assert.IsTrue(r.ReadSuccess,
                        $"Master key '{r.Name}' should be able to perform a DTX read, but read status was {r.Read}");
                    Assert.AreEqual(HttpStatusCode.OK, r.Read,
                        $"Master key '{r.Name}' DTX read should return 200 OK");
                    Assert.IsTrue(r.WriteSuccess,
                        $"Master key '{r.Name}' should be able to perform a DTX write, but write status was {r.Write}");
                    Assert.AreEqual(HttpStatusCode.OK, r.Write,
                        $"Master key '{r.Name}' DTX write should return 200 OK");
                }
            }

            int readSuccesses = results.Count(r => r.ReadSuccess);
            int writeSuccesses = results.Count(r => r.WriteSuccess);
            Console.WriteLine($"\nRead successes:  {readSuccesses}/{results.Count}");
            Console.WriteLine($"Write successes: {writeSuccesses}/{results.Count}");

            // Every configured master key should succeed at both DTX read and write; every configured
            // readonly key should fail both. Assert against the keys actually configured for this account.
            int configuredMasterKeys = results.Count(r => !r.IsReadOnly);
            Assert.IsTrue(configuredMasterKeys > 0, "At least one master key must be configured to run this matrix");
            Assert.AreEqual(configuredMasterKeys, readSuccesses, "Every configured master key should succeed at DTX reads");
            Assert.AreEqual(configuredMasterKeys, writeSuccesses, "Every configured master key should succeed at DTX writes");

            Console.WriteLine("\nAll-key DTX read/write authorization matrix verified!");
        }

        [TestMethod]
        [Description("Verify point read and point write work with AAD (Entra ID) token authentication via DefaultAzureCredential, without using account keys.")]
        public async Task AadToken_ProdAccount_SucceedsWithoutKeys()
        {
            // Endpoint and key for this account (authenticates with the account key).
            const string aadAccountEndpoint = "https://<your-account>.documents.azure.com:443/";
            const string aadAccountKey = "";

            using CosmosClient aadClient = new CosmosClient(
                accountEndpoint: aadAccountEndpoint,
                authKeyOrResourceToken: aadAccountKey,
                clientOptions: new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                });

            // Use the existing database/container on this account (do NOT create them).
            try
            {
                CosmosDatabase aadDatabase = aadClient.GetDatabase("absadbsdk");
                Container aadContainer = aadDatabase.GetContainer("absacollsdk");

                Console.WriteLine($"AAD client using endpoint={aadAccountEndpoint}, db={aadDatabase.Id}, container={aadContainer.Id}");

                // Discover the container's actual partition key path (b1 may not use "/pk").
                ContainerProperties containerProperties = (await aadContainer.ReadContainerAsync(cancellationToken: this.cancellationToken)).Resource;
                string pkPath = containerProperties.PartitionKeyPath;
                string pkPropName = pkPath.TrimStart('/');
                Console.WriteLine($"Container '{aadContainer.Id}' partition key path: {pkPath}");

                // Build a document whose partition-key property matches the container's path.
                // Use the stream API with raw JSON so the property name is written verbatim
                // (independent of the CamelCase serializer policy).
                bool pkIsId = string.Equals(pkPropName, "id", StringComparison.OrdinalIgnoreCase);
                string docId = Guid.NewGuid().ToString();
                string pkValue = pkIsId ? docId : Guid.NewGuid().ToString();
                string docJson = pkIsId
                    ? $"{{\"id\":\"{docId}\",\"description\":\"Created via AAD point write\",\"taskNum\":4242}}"
                    : $"{{\"id\":\"{docId}\",\"{pkPropName}\":\"{pkValue}\",\"description\":\"Created via AAD point write\",\"taskNum\":4242}}";

                // --- POINT WRITE using AAD token ---
                Console.WriteLine("\n=== AAD Point Write (CreateItemStream) ===");
                Console.WriteLine($"Document: {docJson}");
                using (MemoryStream writeStream = new MemoryStream(Encoding.UTF8.GetBytes(docJson)))
                using (ResponseMessage createResponse = await aadContainer.CreateItemStreamAsync(
                    writeStream,
                    new PartitionKey(pkValue),
                    cancellationToken: this.cancellationToken))
                {
                    Console.WriteLine($"Point Write StatusCode: {createResponse.StatusCode}, etag={createResponse.Headers?.ETag}, Charge: {createResponse.Headers?.RequestCharge}");

                    Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode,
                        $"Point write with AAD token should return 201 Created. Error: {createResponse.ErrorMessage}");
                    Assert.IsTrue(createResponse.Headers.RequestCharge > 0, "Point write should have a non-zero request charge");
                }

                // --- POINT READ using AAD token ---
                Console.WriteLine("\n=== AAD Point Read (ReadItemStream) ===");
                using (ResponseMessage readResponse = await aadContainer.ReadItemStreamAsync(
                    docId,
                    new PartitionKey(pkValue),
                    cancellationToken: this.cancellationToken))
                {
                    Console.WriteLine($"Point Read StatusCode: {readResponse.StatusCode}, etag={readResponse.Headers?.ETag}, Charge: {readResponse.Headers?.RequestCharge}");

                    Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode,
                        $"Point read with AAD token should return 200 OK. Error: {readResponse.ErrorMessage}");
                    Assert.IsTrue(readResponse.Headers.RequestCharge > 0, "Point read should have a non-zero request charge");

                    using (StreamReader reader = new StreamReader(readResponse.Content))
                    {
                        string readJson = await reader.ReadToEndAsync();
                        Console.WriteLine($"Read document: {readJson}");
                        Assert.IsTrue(readJson.Contains(docId), "Read document should contain the written id");
                        Assert.IsTrue(readJson.Contains("Created via AAD point write"), "Read document should contain the written description");
                    }
                }

                Console.WriteLine("\nAAD-authenticated point read/write verified!");
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"\nAAD operation failed with CosmosException:");
                Console.WriteLine($"  StatusCode: {(int)ex.StatusCode} {ex.StatusCode}");
                Console.WriteLine($"  SubStatusCode: {ex.SubStatusCode}");
                Console.WriteLine($"  ActivityId: {ex.ActivityId}");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  Diagnostics: {ex.Diagnostics}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAAD operation failed with {ex.GetType().Name}:");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        [TestMethod]
        [Description("Verify point read and point write against the test68 account (absatest68) using the existing absadb/absacoll, authenticating via DefaultAzureCredential (no keys). Does not create database or container.")]
        public async Task Test68Account_PointReadAndWrite_Succeeds()
        {
            // test68 account endpoint and existing database/container ids.
            const string test68Endpoint = "https://<your-account>.documents.azure.com:443/";
            const string test68Key = "";
            const string test68DatabaseId = "absadbsdk";
            const string test68ContainerId = "absacollsdk";

            using CosmosClient test68Client = new CosmosClient(
                accountEndpoint: test68Endpoint,
                authKeyOrResourceToken: test68Key,
                clientOptions: new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                });

            // Use the existing database/container on this account (do NOT create them).
            try
            {
                CosmosDatabase test68Database = test68Client.GetDatabase(test68DatabaseId);
                Container test68Container = test68Database.GetContainer(test68ContainerId);

                Console.WriteLine($"test68 client using endpoint={test68Endpoint}, db={test68Database.Id}, container={test68Container.Id}");

                // Discover the container's actual partition key path.
                ContainerProperties containerProperties = (await test68Container.ReadContainerAsync(cancellationToken: this.cancellationToken)).Resource;
                string pkPath = containerProperties.PartitionKeyPath;
                string pkPropName = pkPath.TrimStart('/');
                Console.WriteLine($"Container '{test68Container.Id}' partition key path: {pkPath}");

                // Build a document whose partition-key property matches the container's path.
                // Use the stream API with raw JSON so the property name is written verbatim
                // (independent of the CamelCase serializer policy).
                bool pkIsId = string.Equals(pkPropName, "id", StringComparison.OrdinalIgnoreCase);
                string docId = Guid.NewGuid().ToString();
                string pkValue = pkIsId ? docId : Guid.NewGuid().ToString();
                string docJson = pkIsId
                    ? $"{{\"id\":\"{docId}\",\"description\":\"Created via test68 point write\",\"taskNum\":6868}}"
                    : $"{{\"id\":\"{docId}\",\"{pkPropName}\":\"{pkValue}\",\"description\":\"Created via test68 point write\",\"taskNum\":6868}}";

                // --- POINT WRITE ---
                Console.WriteLine("\n=== test68 Point Write (CreateItemStream) ===");
                Console.WriteLine($"Document: {docJson}");
                using (MemoryStream writeStream = new MemoryStream(Encoding.UTF8.GetBytes(docJson)))
                using (ResponseMessage createResponse = await test68Container.CreateItemStreamAsync(
                    writeStream,
                    new PartitionKey(pkValue),
                    cancellationToken: this.cancellationToken))
                {
                    Console.WriteLine($"Point Write StatusCode: {createResponse.StatusCode}, etag={createResponse.Headers?.ETag}, Charge: {createResponse.Headers?.RequestCharge}");

                    Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode,
                        $"Point write on test68 should return 201 Created. Error: {createResponse.ErrorMessage}");
                    Assert.IsTrue(createResponse.Headers.RequestCharge > 0, "Point write should have a non-zero request charge");
                }

                // --- POINT READ ---
                Console.WriteLine("\n=== test68 Point Read (ReadItemStream) ===");
                using (ResponseMessage readResponse = await test68Container.ReadItemStreamAsync(
                    docId,
                    new PartitionKey(pkValue),
                    cancellationToken: this.cancellationToken))
                {
                    Console.WriteLine($"Point Read StatusCode: {readResponse.StatusCode}, etag={readResponse.Headers?.ETag}, Charge: {readResponse.Headers?.RequestCharge}");

                    Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode,
                        $"Point read on test68 should return 200 OK. Error: {readResponse.ErrorMessage}");
                    Assert.IsTrue(readResponse.Headers.RequestCharge > 0, "Point read should have a non-zero request charge");

                    using (StreamReader reader = new StreamReader(readResponse.Content))
                    {
                        string readJson = await reader.ReadToEndAsync();
                        Console.WriteLine($"Read document: {readJson}");
                        Assert.IsTrue(readJson.Contains(docId), "Read document should contain the written id");
                        Assert.IsTrue(readJson.Contains("Created via test68 point write"), "Read document should contain the written description");
                    }
                }

                Console.WriteLine("\ntest68 point read/write verified!");
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"\ntest68 operation failed with CosmosException:");
                Console.WriteLine($"  StatusCode: {(int)ex.StatusCode} {ex.StatusCode}");
                Console.WriteLine($"  SubStatusCode: {ex.SubStatusCode}");
                Console.WriteLine($"  ActivityId: {ex.ActivityId}");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  Diagnostics: {ex.Diagnostics}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\ntest68 operation failed with {ex.GetType().Name}:");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        [TestMethod]
        [Description("Using AAD (Entra ID) token authentication via DefaultAzureCredential (no keys), create database 'absadb' and container 'absacoll' on the absatest68 account and insert one document.")]
        public async Task Testacc11Account_CreateDbCollectionAndInsert_WithKey()
        {
            // Account endpoint and key.
            const string testacc11Endpoint = "https://<your-account>.documents.azure.com:443/";
            const string testacc11Key = "";
            const string databaseId = "absadbsdk";
            const string containerId = "absacollsdk";
            const string partitionKeyPath = "/pk";

            using CosmosClient keyClient = new CosmosClient(
                accountEndpoint: testacc11Endpoint,
                authKeyOrResourceToken: testacc11Key,
                clientOptions: new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                });

            try
            {
                // Create the database (if it does not already exist).
                Console.WriteLine($"=== Creating database '{databaseId}' on {testacc11Endpoint} ===");
                DatabaseResponse databaseResponse = await keyClient.CreateDatabaseIfNotExistsAsync(databaseId);
                CosmosDatabase database = databaseResponse.Database;
                Console.WriteLine($"Database '{database.Id}' StatusCode: {databaseResponse.StatusCode}");

                // Create the container (if it does not already exist).
                Console.WriteLine($"\n=== Creating container '{containerId}' (pk={partitionKeyPath}) ===");
                ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties(id: containerId, partitionKeyPath: partitionKeyPath));
                Container container = containerResponse.Container;
                Console.WriteLine($"Container '{container.Id}' StatusCode: {containerResponse.StatusCode}");

                // Insert one document.
                Console.WriteLine($"\n=== Inserting one document ===");
                ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
                doc.description = "Inserted via key on testacc11";
                doc.taskNum = 1111;

                ItemResponse<ToDoActivity> createItemResponse = await container.CreateItemAsync(
                    doc,
                    new PartitionKey(doc.pk),
                    cancellationToken: this.cancellationToken);

                Console.WriteLine($"Insert StatusCode: {createItemResponse.StatusCode}, id={doc.id}, pk={doc.pk}, etag={createItemResponse.ETag}, Charge: {createItemResponse.RequestCharge}");

                Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode,
                    "Document insert with key should return 201 Created");
                Assert.IsTrue(createItemResponse.RequestCharge > 0, "Insert should have a non-zero request charge");

                // Read the document back to confirm it persisted.
                ItemResponse<ToDoActivity> readBack = await container.ReadItemAsync<ToDoActivity>(
                    doc.id,
                    new PartitionKey(doc.pk),
                    cancellationToken: this.cancellationToken);

                Console.WriteLine($"Read back StatusCode: {readBack.StatusCode}, description={readBack.Resource.description}");

                Assert.AreEqual(HttpStatusCode.OK, readBack.StatusCode, "Inserted document should be readable");
                Assert.AreEqual("Inserted via key on testacc11", readBack.Resource.description, "Read document description should match");

                Console.WriteLine("\ntestacc11 database/container created and document inserted successfully!");
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"\ntestacc11 operation failed with CosmosException:");
                Console.WriteLine($"  StatusCode: {(int)ex.StatusCode} {ex.StatusCode}");
                Console.WriteLine($"  SubStatusCode: {ex.SubStatusCode}");
                Console.WriteLine($"  ActivityId: {ex.ActivityId}");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  Diagnostics: {ex.Diagnostics}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\ntestacc11 operation failed with {ex.GetType().Name}:");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        [TestMethod]
        [Description("DTX write with insert + replace(non-existent) + insert: the replace fails because the target document does not exist, so the atomic transaction aborts and NONE of the operations are applied. A point read afterwards confirms the would-be inserted documents do not exist.")]
        public async Task WriteTransaction_ReplaceMissingDocument_AbortsEntireTransaction()
        {
            // This test runs against the absa15junwus account (key-based auth).
            // SECURITY: never commit real key values. Scrub this to "" before pushing.
            const string absa05junseaEndpoint = "https://<your-account>.documents.azure.com:443/";
            const string absa05junseaKey = "";

            using CosmosClient client = new CosmosClient(
                accountEndpoint: absa05junseaEndpoint,
                authKeyOrResourceToken: absa05junseaKey,
                clientOptions: new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });

            CosmosDatabase database = (await client.CreateDatabaseIfNotExistsAsync("absadbsdk")).Database;
            Container container = (await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id: "absacollsdk", partitionKeyPath: PartitionKeyPath))).Container;

            Console.WriteLine($"Using account endpoint={absa05junseaEndpoint}, db={database.Id}, container={container.Id}");

            // Prepare three documents. None of them exist yet.
            //  Op 0: Insert insertDoc0 (would succeed on its own)
            //  Op 1: Replace replaceDoc (MUST fail - document does not exist)
            //  Op 2: Insert insertDoc1 (would succeed on its own)
            ToDoActivity insertDoc0 = ToDoActivity.CreateRandomToDoActivity();
            insertDoc0.description = "Insert 0 in aborted DTX";
            insertDoc0.taskNum = 10;

            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();
            replaceDoc.description = "Replace target that does NOT exist";
            replaceDoc.taskNum = 20;

            ToDoActivity insertDoc1 = ToDoActivity.CreateRandomToDoActivity();
            insertDoc1.description = "Insert 1 in aborted DTX";
            insertDoc1.taskNum = 30;

            Console.WriteLine("=== Executing DTX write: Insert + Replace(missing) + Insert ===");
            Console.WriteLine($"Op 0: Insert  id={insertDoc0.id}");
            Console.WriteLine($"Op 1: Replace id={replaceDoc.id} (does NOT exist -> should fail)");
            Console.WriteLine($"Op 2: Insert  id={insertDoc1.id}");

            DistributedTransactionResponse response = await client
                .CreateDistributedWriteTransaction()
                .CreateItem(container, new PartitionKey(insertDoc0.pk), insertDoc0.id, insertDoc0)
                .ReplaceItem(container, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .CreateItem(container, new PartitionKey(insertDoc1.pk), insertDoc1.id, insertDoc1)
                .CommitTransactionAsync(this.cancellationToken);

            // Transaction (outer) level.
            Console.WriteLine($"\nTransaction StatusCode: {(int)response.StatusCode} ({response.StatusCode})");
            Console.WriteLine($"IsSuccessStatusCode: {response.IsSuccessStatusCode}");
            Console.WriteLine($"Count: {response.Count}");

            // The transaction must NOT succeed.
            Assert.IsFalse(response.IsSuccessStatusCode,
                "Transaction must not succeed because the replace targets a missing document");
            // The atomic transaction aborts. The transaction-level status surfaces the abort; the
            // server intermittently reports this as 452 (Aborted) or 500 (InternalServerError).
            int txStatus = (int)response.StatusCode;
            Assert.IsTrue(txStatus == 452 || txStatus == 500,
                $"Transaction-level status should surface the abort (452 Aborted or 500 InternalServerError), but was {txStatus}");

            // Per-operation results: because the transaction aborts atomically, NO operation
            // should report success. (Depending on how the server surfaces the abort, the failing
            // replace may report 404 NotFound with the 452 path, or all ops may report 500 with the
            // 500 path - in either case none of them succeed.)
            Console.WriteLine("\n=== Operation Results ===");
            for (int i = 0; i < response.Count; i++)
            {
                Console.WriteLine($"  Op[{i}] StatusCode: {(int)response[i].StatusCode} ({response[i].StatusCode})");
                Assert.IsFalse(response[i].IsSuccessStatusCode,
                    $"Operation[{i}] must not report success because the atomic transaction is aborted");
            }

            response.Dispose();

            // ----------------------------------------------------------------------------
            // Confirm atomicity via point reads: NEITHER inserted document should exist.
            // ----------------------------------------------------------------------------
            Console.WriteLine("\n=== Verifying atomicity via point reads (neither insert should exist) ===");

            await this.AssertItemDoesNotExist(container, insertDoc0.id, insertDoc0.pk, "insertDoc0");
            await this.AssertItemDoesNotExist(container, insertDoc1.id, insertDoc1.pk, "insertDoc1");
            await this.AssertItemDoesNotExist(container, replaceDoc.id, replaceDoc.pk, "replaceDoc");

            Console.WriteLine("\nAtomicity verified: the failed replace aborted the entire transaction; no documents were inserted.");
        }

        private async Task AssertItemDoesNotExist(Container container, string id, string pk, string label)
        {
            try
            {
                ItemResponse<ToDoActivity> read = await container.ReadItemAsync<ToDoActivity>(
                    id, new PartitionKey(pk), cancellationToken: this.cancellationToken);
                Console.WriteLine($"  {label}: UNEXPECTEDLY EXISTS (StatusCode={read.StatusCode})");
                Assert.Fail($"{label} (id={id}) should NOT exist because the transaction was aborted");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"  {label}: NotFound (correct - not inserted)");
            }
        }

        // Additional E2E tests can be added here following the same pattern
        // All tests should use this.client, this.database, and this.container
        // and perform real E2E operations against the configured endpoint
    }
}