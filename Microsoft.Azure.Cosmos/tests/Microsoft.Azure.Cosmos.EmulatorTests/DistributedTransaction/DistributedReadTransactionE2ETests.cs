// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// End-to-end tests for <see cref="DistributedReadTransaction"/> against a live DTX-enabled
    /// account, exercising the full account-endpoint + master-key gateway pipeline (no mock
    /// handler, no emulator interception).
    ///
    /// These tests verify SDK end-to-end correctness against the real transaction coordinator +
    /// backend and pin down the per-operation result contract returned for read transactions.
    ///
    /// To run locally:
    ///     set COSMOS_DTX_ENDPOINT=https://your-account.documents.azure.com:443/
    ///     set COSMOS_DTX_KEY=your-master-key
    ///     dotnet test --filter "FullyQualifiedName~DistributedReadTransactionE2ETests"
    ///
    /// Remove the [Ignore] attribute before running. The class is gated because the public
    /// emulator does not implement /operations/dtc.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [Ignore("DTX endpoint not yet available in emulator. Remove to run locally with env vars.")]
    public class DistributedReadTransactionE2ETests
    {
        private const string DatabaseId = "DtxReadE2ETestDb";
        private const string ContainerId = "DtxReadE2ETestContainer";
        private const string SecondContainerId = "DtxReadE2ETestContainer2";
        private const string PartitionKeyPath = "/pk";

        private CosmosClient client;
        private string endpoint;
        private string key;
        private Database database;
        private Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.endpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT");
            this.key = Environment.GetEnvironmentVariable("COSMOS_DTX_KEY");

            if (string.IsNullOrWhiteSpace(this.endpoint) || string.IsNullOrWhiteSpace(this.key))
            {
                Assert.Fail("COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY environment variables must be set.");
            }

            // Standard DTX auth model: account endpoint + master key.
            this.client = new CosmosClient(
                this.endpoint,
                this.key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = ConsistencyLevel.Session
                });

            this.database = (await this.client.CreateDatabaseIfNotExistsAsync(DatabaseId)).Database;
            this.container = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath))).Container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.client != null)
            {
                try
                {
                    await this.client.GetDatabase(DatabaseId).DeleteAsync();
                }
                catch { /* ignore */ }

                this.client.Dispose();
            }
        }

        // ─── Happy path / routing ──────────────────────────────────────────────

        [TestMethod]
        public async Task ReadTransaction_SingleItem_Succeeds()
        {
            ToDoActivity doc = await this.SeedItemAsync(this.container);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc.pk), doc.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode, "Per-op status should be 200 OK.");

            ToDoActivity read = response.GetOperationResultAtIndex<ToDoActivity>(0).Resource;
            Assert.AreEqual(doc.id, read.id);
            Assert.AreEqual(doc.pk, read.pk);

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_MultiPartition_MultiItem_Succeeds()
        {
            ToDoActivity doc1 = await this.SeedItemAsync(this.container);
            ToDoActivity doc2 = await this.SeedItemAsync(this.container);
            ToDoActivity doc3 = await this.SeedItemAsync(this.container);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc1.pk), doc1.id)
                .ReadItem(this.container, new PartitionKey(doc2.pk), doc2.id)
                .ReadItem(this.container, new PartitionKey(doc3.pk), doc3.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(3, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.OK, response[i].StatusCode, $"Per-op[{i}] should be 200 OK.");
            }

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_CrossContainer_Succeeds()
        {
            Container secondContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(SecondContainerId, PartitionKeyPath))).Container;

            ToDoActivity doc1 = await this.SeedItemAsync(this.container);
            ToDoActivity doc2 = await this.SeedItemAsync(secondContainer);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc1.pk), doc1.id)
                .ReadItem(secondContainer, new PartitionKey(doc2.pk), doc2.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode, "Per-op[0] should be 200 OK.");
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode, "Per-op[1] should be 200 OK.");

            response.Dispose();
        }

        // ─── Result semantics ──────────────────────────────────────────────────

        [TestMethod]
        public async Task ReadTransaction_AllItemsExist_AllReturn200WithBody()
        {
            ToDoActivity doc1 = await this.SeedItemAsync(this.container);
            ToDoActivity doc2 = await this.SeedItemAsync(this.container);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc1.pk), doc1.id)
                .ReadItem(this.container, new PartitionKey(doc2.pk), doc2.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.OK, response[i].StatusCode, $"Per-op[{i}] should be 200 OK.");
                Assert.IsNotNull(response[i].ResourceStream, $"Per-op[{i}] should carry a resource body.");
            }

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_AllItemsMissing_AllReturnNotFound()
        {
            string pk1 = Guid.NewGuid().ToString();
            string pk2 = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk1), Guid.NewGuid().ToString())
                .ReadItem(this.container, new PartitionKey(pk2), Guid.NewGuid().ToString())
                .CommitTransactionAsync(CancellationToken.None);

            // Pin the coordinator contract: envelope is 200 (multi-status), each missing item surfaces as a per-op 404.
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Envelope status should be 200 even when all per-op results are 404.");
            Assert.AreEqual(2, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, response[i].StatusCode, $"Op[{i}] should be 404 NotFound for a missing item.");
            }

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_OneExistsOneMissing_ReturnsMixedResults()
        {
            ToDoActivity existing = await this.SeedItemAsync(this.container);
            string missingPk = Guid.NewGuid().ToString();
            string missingId = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(existing.pk), existing.id)
                .ReadItem(this.container, new PartitionKey(missingPk), missingId)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Envelope status should be 200 even with mixed per-op results.");
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode, "The existing item should be 200 OK at index 0.");
            Assert.AreEqual(HttpStatusCode.NotFound, response[1].StatusCode, "The missing item should be 404 NotFound at index 1.");

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_ReturnsBodyForAllOps()
        {
            ToDoActivity doc1 = await this.SeedItemAsync(this.container);
            ToDoActivity doc2 = await this.SeedItemAsync(this.container);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc1.pk), doc1.id)
                .ReadItem(this.container, new PartitionKey(doc2.pk), doc2.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode, "Per-op[0] should be 200 OK.");
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode, "Per-op[1] should be 200 OK.");

            ToDoActivity read0 = response.GetOperationResultAtIndex<ToDoActivity>(0).Resource;
            ToDoActivity read1 = response.GetOperationResultAtIndex<ToDoActivity>(1).Resource;

            Assert.AreEqual(doc1.id, read0.id);
            Assert.AreEqual(doc2.id, read1.id);

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_NoOps_Throws()
        {
            // Committing a read transaction with zero ReadItem calls must throw
            // InvalidOperationException before any network call.
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                this.client
                    .CreateDistributedReadTransaction()
                    .CommitTransactionAsync(CancellationToken.None));
        }

        // ─── Container resolution failure (real path, before /dtc) ─────────────

        [TestMethod]
        public async Task ReadTransaction_InvalidContainer_ThrowsNotFound()
        {
            Container missingContainer = this.client.GetContainer(DatabaseId, "container-that-does-not-exist");

            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                this.client
                    .CreateDistributedReadTransaction()
                    .ReadItem(missingContainer, new PartitionKey("pk"), "item-id")
                    .CommitTransactionAsync(CancellationToken.None));

            Assert.AreEqual(
                HttpStatusCode.NotFound,
                ex.StatusCode,
                "Resolving an unknown container must fail with NotFound before any /dtc call.");
        }

        // ─── Cross-container with differing indexing policies ──────────────────

        [TestMethod]
        public async Task ReadTransaction_CrossContainer_DifferentIndexingPolicies_Succeeds()
        {
            // Container A: default (consistent) indexing.
            ContainerProperties propsA = new ContainerProperties("DtxReadIdxA", PartitionKeyPath)
            {
                IndexingPolicy = new IndexingPolicy { IndexingMode = IndexingMode.Consistent }
            };
            Container containerA = (await this.database.CreateContainerIfNotExistsAsync(propsA)).Container;

            // Container B: no automatic indexing — a deliberately different policy.
            ContainerProperties propsB = new ContainerProperties("DtxReadIdxB", PartitionKeyPath)
            {
                IndexingPolicy = new IndexingPolicy { Automatic = false, IndexingMode = IndexingMode.None }
            };
            Container containerB = (await this.database.CreateContainerIfNotExistsAsync(propsB)).Container;

            ToDoActivity docA = await this.SeedItemAsync(containerA);
            ToDoActivity docB = await this.SeedItemAsync(containerB);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(containerA, new PartitionKey(docA.pk), docA.id)
                .ReadItem(containerB, new PartitionKey(docB.pk), docB.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode, "Per-op[0] should be 200 OK.");
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode, "Per-op[1] should be 200 OK.");

            response.Dispose();
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private async Task<ToDoActivity> SeedItemAsync(Container targetContainer)
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            await targetContainer.CreateItemAsync(doc, new PartitionKey(doc.pk));
            return doc;
        }
    }
}
