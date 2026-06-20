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
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// End-to-end happy-path tests for <see cref="DistributedWriteTransaction"/> against a live
    /// DTX-enabled account, exercising the full account-endpoint + master-key gateway pipeline
    /// (no mock handler, no emulator interception).
    ///
    /// These tests pin the Prepare + Commit (2PC) success contract across the four routing
    /// topologies enumerated in the Happy Path spec (§1.2): single PK, cross-PK, cross-container,
    /// and cross-database. They also cover §1.3 session-token round-tripping (write result →
    /// follow-up read option).
    ///
    /// To run locally:
    ///     set COSMOS_DTX_ENDPOINT=https://your-account.documents.azure.com:443/
    ///     set COSMOS_DTX_KEY=your-master-key
    ///     dotnet test --filter "FullyQualifiedName~DistributedWriteTransactionE2ETests"
    ///
    /// This class runs in the "DistributedTransaction" test category and is NOT gated with
    /// [Ignore]. It requires the COSMOS_DTX_ENDPOINT / COSMOS_DTX_KEY environment variables
    /// pointing at a live DTX-enabled account (the public emulator does not implement
    /// /operations/dtc); without them the tests fail fast in TestInitialize.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedWriteTransactionE2ETests
    {
        private const string DatabaseId = "DtxWriteE2ETestDb";
        private const string SecondDatabaseId = "DtxWriteE2ETestDb2";
        private const string SharedThroughputDatabaseId = "DtxWriteE2ETestDbShared";
        private const string DedicatedThroughputDatabaseId = "DtxWriteE2ETestDbDedicated";
        private const string ContainerId = "DtxWriteE2ETestContainer";
        private const string SecondContainerId = "DtxWriteE2ETestContainer2";
        private const string ThirdContainerId = "DtxWriteE2ETestContainer3";
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

            // Standard DTX auth model: account endpoint + primary master key.
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

                try
                {
                    await this.client.GetDatabase(SecondDatabaseId).DeleteAsync();
                }
                catch { /* ignore */ }

                try
                {
                    await this.client.GetDatabase(SharedThroughputDatabaseId).DeleteAsync();
                }
                catch { /* ignore */ }

                try
                {
                    await this.client.GetDatabase(DedicatedThroughputDatabaseId).DeleteAsync();
                }
                catch { /* ignore */ }

                this.client.Dispose();
            }
        }

        // ─── §1.2.1 Single PK (1PK) ──────────────────────────────────────────────

        /// <summary>
        /// §1.2.1 Prepare + Commit, all ops succeed: multiple write ops on the same partition key
        /// commit atomically; envelope is success and every per-op result is in the success class.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_SinglePk_MultiOp_AllSucceed()
        {
            string pk = $"single-pk-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id1, new { id = id1, pk, value = "a" })
                .CreateItem(this.container, new PartitionKey(pk), id2, new { id = id2, pk, value = "b" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Single-PK multi-op commit should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.IsTrue(response[i].IsSuccessStatusCode,
                    $"Per-op[{i}] should be in the success class. Got: {response[i].StatusCode}");
            }

            response.Dispose();
        }

        /// <summary>
        /// §1.2.1 Primary master key auth: a write DTx authenticated with the account primary
        /// master key is accepted and commits.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_PrimaryMasterKey_Succeeds()
        {
            string pk = $"master-key-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, new { id, pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Write DTx with the primary master key should commit. Got: {response.StatusCode}");
            Assert.IsTrue(response.Count > 0);
            Assert.IsTrue(response[0].IsSuccessStatusCode,
                $"Op[0] should be in the success class. Got: {response[0].StatusCode}");

            response.Dispose();
        }

        // ─── §1.2.2 Cross-PK, single container (xPK) ─────────────────────────────

        /// <summary>
        /// §1.2.2 2PC commit across K PKs: a write DTx whose ops span several partition keys in one
        /// container prepares and commits on every participant.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_CrossPk_2PC_Succeeds()
        {
            DistributedWriteTransaction tx = this.client.CreateDistributedWriteTransaction();

            const int partitionKeyCount = 3;
            for (int i = 0; i < partitionKeyCount; i++)
            {
                string pk = $"xpk-{i}-{Guid.NewGuid():N}";
                string id = Guid.NewGuid().ToString();
                tx.CreateItem(this.container, new PartitionKey(pk), id, new { id, pk, value = i });
            }

            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cross-PK 2PC commit should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(partitionKeyCount, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.IsTrue(response[i].IsSuccessStatusCode,
                    $"Per-op[{i}] should be in the success class. Got: {response[i].StatusCode}");
            }

            response.Dispose();
        }

        /// <summary>
        /// §1.2.2 Large fan-out 2PC (K = 10): the coordinator scales the 2PC cleanly with no
        /// participant left in the Prepared state; the envelope succeeds and every op lands.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_CrossPk_LargeFanout_Succeeds()
        {
            DistributedWriteTransaction tx = this.client.CreateDistributedWriteTransaction();

            const int partitionKeyCount = 10;
            for (int i = 0; i < partitionKeyCount; i++)
            {
                string pk = $"fanout-{i}-{Guid.NewGuid():N}";
                string id = Guid.NewGuid().ToString();
                tx.CreateItem(this.container, new PartitionKey(pk), id, new { id, pk, value = i });
            }

            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Large fan-out 2PC commit should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(partitionKeyCount, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.IsTrue(response[i].IsSuccessStatusCode,
                    $"Per-op[{i}] should be in the success class. Got: {response[i].StatusCode}");
            }

            response.Dispose();
        }

        // ─── §1.2.3 Cross-container (xCont) ──────────────────────────────────────

        /// <summary>
        /// §1.2.3 2PC commit across 2+ containers (same DB): both containers are prepared and
        /// committed atomically.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_CrossContainer_2PC_Succeeds()
        {
            Container secondContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(SecondContainerId, PartitionKeyPath))).Container;

            string pk1 = $"xcont-a-{Guid.NewGuid():N}";
            string pk2 = $"xcont-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk1), id1, new { id = id1, pk = pk1, value = "c1" })
                .CreateItem(secondContainer, new PartitionKey(pk2), id2, new { id = id2, pk = pk2, value = "c2" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cross-container 2PC commit should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.IsTrue(response[0].IsSuccessStatusCode, $"Op[0] should succeed. Got: {response[0].StatusCode}");
            Assert.IsTrue(response[1].IsSuccessStatusCode, $"Op[1] should succeed. Got: {response[1].StatusCode}");

            response.Dispose();
        }

        /// <summary>
        /// §1.2.3 Mix of write op types across the transaction (Create / Replace / Upsert / Delete):
        /// each op's semantics are honored and the 2PC commit is unaffected.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_MixedOpTypes_AllHonored()
        {
            // Seed the items targeted by Replace and Delete so they exist before the transaction.
            string replacePk = $"mix-replace-{Guid.NewGuid():N}";
            string replaceId = Guid.NewGuid().ToString();
            await this.container.CreateItemAsync<object>(
                new { id = replaceId, pk = replacePk, value = "before" }, new PartitionKey(replacePk));

            string deletePk = $"mix-delete-{Guid.NewGuid():N}";
            string deleteId = Guid.NewGuid().ToString();
            await this.container.CreateItemAsync<object>(
                new { id = deleteId, pk = deletePk, value = "to-delete" }, new PartitionKey(deletePk));

            string createPk = $"mix-create-{Guid.NewGuid():N}";
            string createId = Guid.NewGuid().ToString();
            string upsertPk = $"mix-upsert-{Guid.NewGuid():N}";
            string upsertId = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(createPk), createId, new { id = createId, pk = createPk, value = "new" })
                .ReplaceItem(this.container, new PartitionKey(replacePk), replaceId, new { id = replaceId, pk = replacePk, value = "after" })
                .UpsertItem(this.container, new PartitionKey(upsertPk), upsertId, new { id = upsertId, pk = upsertPk, value = "upserted" })
                .DeleteItem(this.container, new PartitionKey(deletePk), deleteId)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Mixed-op-type commit should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(4, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.IsTrue(response[i].IsSuccessStatusCode,
                    $"Per-op[{i}] should be in the success class. Got: {response[i].StatusCode}");
            }

            response.Dispose();
        }

        // ─── §1.2.4 Cross-database (xDB) ─────────────────────────────────────────

        /// <summary>
        /// §1.2.4 2PC commit across 2+ databases (same account): all databases are prepared and
        /// committed atomically under a single coordinator ledger.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_CrossDatabase_2PC_Succeeds()
        {
            Database secondDatabase = (await this.client.CreateDatabaseIfNotExistsAsync(SecondDatabaseId)).Database;
            Container secondContainer = (await secondDatabase.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath))).Container;

            string pk1 = $"xdb-a-{Guid.NewGuid():N}";
            string pk2 = $"xdb-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk1), id1, new { id = id1, pk = pk1, value = "db1" })
                .CreateItem(secondContainer, new PartitionKey(pk2), id2, new { id = id2, pk = pk2, value = "db2" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cross-database 2PC commit should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.IsTrue(response[0].IsSuccessStatusCode, $"Op[0] should succeed. Got: {response[0].StatusCode}");
            Assert.IsTrue(response[1].IsSuccessStatusCode, $"Op[1] should succeed. Got: {response[1].StatusCode}");

            response.Dispose();
        }

        // ─── §1.3 Request Options: session-token round-trip ──────────────────────

        /// <summary>
        /// §1.3 Session token round-tripping: the per-operation session token surfaced on a write
        /// <see cref="DistributedTransactionOperationResult"/> can be fed back through
        /// <see cref="DistributedTransactionRequestOptions.SessionToken"/> on a subsequent read DTx,
        /// validating read-your-own-writes continuity and per-operation session-token plumbing.
        /// </summary>
        [TestMethod]
        public async Task WriteThenRead_SessionToken_RoundTrips()
        {
            string pk = $"session-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse writeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, new { id, pk, value = "written" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(writeResponse.IsSuccessStatusCode,
                $"Seeding write should commit. Got: {writeResponse.StatusCode}");
            Assert.IsTrue(writeResponse.Count > 0);

            string sessionToken = writeResponse[0].SessionToken;
            Assert.IsFalse(string.IsNullOrEmpty(sessionToken),
                "Write op result should surface a non-empty session token for round-tripping.");

            writeResponse.Dispose();

            // Feed the captured token back through the per-operation request options on a read DTx.
            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id,
                    new DistributedTransactionRequestOptions { SessionToken = sessionToken })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"Read with the round-tripped session token should succeed. Got: {readResponse.StatusCode}");
            Assert.IsTrue(readResponse.Count > 0);
            Assert.AreEqual(HttpStatusCode.OK, readResponse[0].StatusCode,
                $"Read-your-own-write op should be 200 OK. Got: {readResponse[0].StatusCode}");

            readResponse.Dispose();
        }

        // ─── §1.2.3 Cross-container with heterogeneous provisioning ───────────────

        /// <summary>
        /// §1.2.3 2PC commit across containers provisioned with <b>different RU throughput</b>:
        /// one container is created with 400 RU/s manual throughput and the other with 1000 RU/s
        /// manual throughput. A cross-container write DTx must still prepare + commit atomically
        /// regardless of the per-container RU provisioning, proving the coordinator does not require
        /// homogeneous throughput across the enlisted resources.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_CrossContainer_DifferentRuProvisioning_Succeeds()
        {
            Container lowRuContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(SecondContainerId, PartitionKeyPath), throughput: 400)).Container;
            Container highRuContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ThirdContainerId, PartitionKeyPath), throughput: 1000)).Container;

            string pk1 = $"ru-low-{Guid.NewGuid():N}";
            string pk2 = $"ru-high-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(lowRuContainer, new PartitionKey(pk1), id1, new { id = id1, pk = pk1, value = "low-ru" })
                .CreateItem(highRuContainer, new PartitionKey(pk2), id2, new { id = id2, pk = pk2, value = "high-ru" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cross-container 2PC across different RU provisioning should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.IsTrue(response[0].IsSuccessStatusCode, $"Op[0] should succeed. Got: {response[0].StatusCode}");
            Assert.IsTrue(response[1].IsSuccessStatusCode, $"Op[1] should succeed. Got: {response[1].StatusCode}");

            response.Dispose();
        }

        // ─── §1.2.4 Cross-database with mixed throughput models ───────────────────

        /// <summary>
        /// §1.2.4 2PC commit across databases that use <b>different throughput models</b>: one
        /// database provisions <b>shared (database-level) throughput</b> consumed by its container,
        /// while the other database hosts a container with its own <b>dedicated (container-level)
        /// throughput</b>. A cross-database write DTx spanning both must prepare + commit atomically,
        /// proving the coordinator tolerates mixed shared/dedicated provisioning models.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_CrossDatabase_MixedThroughputModels_Succeeds()
        {
            // DB1: shared (database-level) throughput; container inherits it (no dedicated RU).
            Database sharedDb = (await this.client.CreateDatabaseIfNotExistsAsync(
                SharedThroughputDatabaseId, throughput: 400)).Database;
            Container sharedContainer = (await sharedDb.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath))).Container;

            // DB2: no shared throughput; container provisions its own dedicated RU.
            Database dedicatedDb = (await this.client.CreateDatabaseIfNotExistsAsync(
                DedicatedThroughputDatabaseId)).Database;
            Container dedicatedContainer = (await dedicatedDb.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath), throughput: 400)).Container;

            string pk1 = $"shared-{Guid.NewGuid():N}";
            string pk2 = $"dedicated-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(sharedContainer, new PartitionKey(pk1), id1, new { id = id1, pk = pk1, value = "shared-db" })
                .CreateItem(dedicatedContainer, new PartitionKey(pk2), id2, new { id = id2, pk = pk2, value = "dedicated-db" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cross-database 2PC across mixed throughput models should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.IsTrue(response[0].IsSuccessStatusCode, $"Op[0] should succeed. Got: {response[0].StatusCode}");
            Assert.IsTrue(response[1].IsSuccessStatusCode, $"Op[1] should succeed. Got: {response[1].StatusCode}");

            response.Dispose();
        }
    }
}
