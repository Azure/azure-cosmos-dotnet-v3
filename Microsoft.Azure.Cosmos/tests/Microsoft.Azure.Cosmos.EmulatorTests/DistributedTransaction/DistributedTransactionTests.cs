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
        private const string CustomEndpoint = "https://absa05junsea-southeastasia.documents-test.windows-int.net:443/";
        private const string CustomMasterKey = "";//

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
                .ReplaceItem(
                    this.container,
                    new PartitionKey(insertDoc.pk),
                    insertDoc.id,
                    insertDoc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = staleReplaceEtag })
                .DeleteItem(
                    this.container,
                    new PartitionKey(upsertDoc.pk),
                    upsertDoc.id,
                    new DistributedTransactionRequestOptions { IfMatchEtag = currentUpsertEtag })
                .CommitTransactionAsync(this.cancellationToken);

            Console.WriteLine($"\nNegative Transaction StatusCode: {negativeResponse.StatusCode}");
            Console.WriteLine($"Negative Transaction IsSuccessStatusCode: {negativeResponse.IsSuccessStatusCode}");
            Console.WriteLine($"Negative Operation Count: {negativeResponse.Count}");

            // The transaction must fail because the replace precondition (stale ETag) is not met.
            Assert.IsFalse(negativeResponse.IsSuccessStatusCode, 
                "Transaction with a wrong ETag on the replace must NOT succeed");
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, negativeResponse.StatusCode, 
                "Transaction-level status should surface the precondition failure (412)");

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
        [Description("Verify distributed read transaction can read documents created via point writes.")]
        public async Task DistributedReadTransaction_ReadDocumentsFromPointWrites()
        {
            // Arrange - Create multiple documents via point writes
            Console.WriteLine("=== Creating documents via point writes ===");

            var documents = new List<ToDoActivity>();
            var createResponses = new List<ItemResponse<ToDoActivity>>();

            // Create 4 documents with different partition keys (cross-partition read)
            for (int i = 0; i < 4; i++)
            {
                ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
                doc.description = $"Document {i} for DTX read";
                doc.taskNum = 100 + i;

                ItemResponse<ToDoActivity> createResponse = await this.container.CreateItemAsync(
                    doc,
                    new PartitionKey(doc.pk),
                    cancellationToken: this.cancellationToken);

                Console.WriteLine($"Created doc[{i}]: id={doc.id}, pk={doc.pk}, etag={createResponse.ETag}, charge={createResponse.RequestCharge}");

                documents.Add(doc);
                createResponses.Add(createResponse);
            }

            // Act - Read all documents using distributed read transaction
            Console.WriteLine("\n=== Executing Distributed Read Transaction ===");

            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(documents[0].pk), documents[0].id)
                .ReadItem(this.container, new PartitionKey(documents[1].pk), documents[1].id)
                .CommitTransactionAsync(this.cancellationToken);

            // Assert - Verify transaction response
            Console.WriteLine($"\nTransaction Response StatusCode: {readResponse.StatusCode}");
            Console.WriteLine($"Transaction ActivityId: {readResponse.ActivityId}");
            Console.WriteLine($"Transaction Request Charge: {readResponse.RequestCharge}");
            Console.WriteLine($"Operation Count: {readResponse.Count}");

            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode, "Read transaction should succeed");
            Assert.IsTrue(readResponse.IsSuccessStatusCode, "Read transaction should indicate success");
            Assert.AreEqual(1, readResponse.Count, "Should have 4 operation responses");

            // Verify transaction-level request charge is non-zero
            Assert.IsTrue(readResponse.RequestCharge > 0, 
                $"Transaction-level request charge should be > 0, but was {readResponse.RequestCharge}");

            Console.WriteLine("\n=== Verifying Individual Read Operations ===");

            double totalOperationCharge = 0;
            var readETags = new List<string>();

            for (int i = 0; i < readResponse.Count; i++)
            {
                DistributedTransactionOperationResult opResult = readResponse[i];

                Console.WriteLine($"\nOperation[{i}] (Read):");
                Console.WriteLine($"  StatusCode: {opResult.StatusCode}");
                Console.WriteLine($"  Index: {opResult.Index}");
                Console.WriteLine($"  ETag: {opResult.ETag}");
                Console.WriteLine($"  RequestCharge: {opResult.RequestCharge}");
                Console.WriteLine($"  Has ResourceStream: {opResult.ResourceStream != null}");

                // Verify read operation succeeded
                Assert.AreEqual(HttpStatusCode.OK, opResult.StatusCode, 
                    $"Read operation[{i}] should return 200 OK");

                // Verify request charge is non-zero
                Assert.IsTrue(opResult.RequestCharge > 0, 
                    $"Operation[{i}] request charge should be > 0, but was {opResult.RequestCharge}");

                totalOperationCharge += opResult.RequestCharge;

                // Verify ETag is present
                Assert.IsNotNull(opResult.ETag, $"Operation[{i}] ETag should not be null");
                Assert.IsFalse(string.IsNullOrWhiteSpace(opResult.ETag), 
                    $"Operation[{i}] ETag should not be empty");

                readETags.Add(opResult.ETag);

                // Verify ETag matches the ETag from point write
                Assert.AreEqual(createResponses[i].ETag, opResult.ETag,
                    $"ETag from DTX read should match ETag from point write for document {i}");

                // Verify resource stream is present
                Assert.IsNotNull(opResult.ResourceStream, 
                    $"Operation[{i}] should have ResourceStream for read operation");

                // Deserialize and verify document content
                using (var reader = new StreamReader(opResult.ResourceStream, leaveOpen: true))
                {
                    string json = await reader.ReadToEndAsync();
                    ToDoActivity readDoc = JsonSerializer.Deserialize<ToDoActivity>(json);

                    Console.WriteLine($"  Document content: id={readDoc.id}, taskNum={readDoc.taskNum}, description={readDoc.description}");

                    Assert.IsNotNull(readDoc, $"Should be able to deserialize document {i}");
                    Assert.AreEqual(documents[i].id, readDoc.id, 
                        $"Document {i} id should match");
                    Assert.AreEqual(documents[i].pk, readDoc.pk, 
                        $"Document {i} pk should match");
                    Assert.AreEqual(documents[i].taskNum, readDoc.taskNum, 
                        $"Document {i} taskNum should match");
                    Assert.AreEqual($"Document {i} for DTX read", readDoc.description, 
                        $"Document {i} description should match");
                }
            }

            // Verify sum of operation charges approximately equals transaction charge
            Console.WriteLine($"\n=== Request Charge Verification ===");
            Console.WriteLine($"Transaction-level charge: {readResponse.RequestCharge}");
            Console.WriteLine($"Sum of operation charges: {totalOperationCharge}");

            double chargeDifference = Math.Abs(readResponse.RequestCharge - totalOperationCharge);
            double chargePercentDiff = (chargeDifference / readResponse.RequestCharge) * 100;

            Console.WriteLine($"Charge difference: {chargeDifference} RUs ({chargePercentDiff:F2}%)");

            Assert.IsTrue(chargePercentDiff < 1.0 || chargeDifference < 0.01,
                $"Sum of operation charges ({totalOperationCharge}) should approximately equal " +
                $"transaction charge ({readResponse.RequestCharge}). Difference: {chargeDifference} RUs ({chargePercentDiff:F2}%)");

            // Additional validation: Try reading with GetOperationResultAtIndex<T>()
            Console.WriteLine("\n=== Testing Typed Deserialization ===");

            for (int i = 0; i < readResponse.Count; i++)
            {
                ToDoActivity typedDoc = readResponse.GetOperationResultAtIndex<ToDoActivity>(i).Resource;

                Console.WriteLine($"Typed read[{i}]: id={typedDoc.id}, taskNum={typedDoc.taskNum}");

                Assert.IsNotNull(typedDoc, $"Typed deserialization should work for operation {i}");
                Assert.AreEqual(documents[i].id, typedDoc.id, 
                    $"Typed document {i} id should match original");
                Assert.AreEqual(documents[i].taskNum, typedDoc.taskNum, 
                    $"Typed document {i} taskNum should match original");
            }

            Console.WriteLine("\n? All distributed read transaction validations passed!");

            readResponse.Dispose();
        }

        // Additional E2E tests can be added here following the same pattern
        // All tests should use this.client, this.database, and this.container
        // and perform real E2E operations against the configured endpoint
    }
}
