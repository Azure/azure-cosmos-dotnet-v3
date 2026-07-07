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
    /// End-to-end tests for DTX behavior against a live DTX-enabled account.
    ///
    /// Two purposes:
    /// 1. SDK end-to-end correctness against the real coordinator + backend.
    /// 2. DTC / backend contract verification -- these are the only tests that detect a silent
    ///    change in coordinator behavior.
    ///
    /// To run locally:
    ///     set COSMOS_DTX_ENDPOINT=https://your-account.documents.azure.com:443/
    ///     set COSMOS_DTX_KEY=your-master-key
    ///     dotnet test --filter "FullyQualifiedName~DistributedTransactionE2ETests"
    ///
    /// Remove the [Ignore] attribute before running.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [Ignore("DTX endpoint not yet available in emulator. Remove to run locally with env vars.")]
    public class DistributedTransactionE2ETests
    {
        private const string DatabaseId = "DtxE2ETestDb";
        private const string ContainerId = "DtxE2ETestContainer";
        private const string PartitionKeyPath = "/pk";

        private CosmosClient client;
        private Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT");
            string key = Environment.GetEnvironmentVariable("COSMOS_DTX_KEY");

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Assert.Inconclusive(
                    "COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY environment variables must be set.");
            }

            this.client = new CosmosClient(
                endpoint,
                key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = ConsistencyLevel.Session
                });

            Database database = (await this.client.CreateDatabaseIfNotExistsAsync(DatabaseId)).Database;
            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath));
            this.container = containerResponse.Container;
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

        // ─── Session token: SDK end-to-end correctness ─────────────────────────

        // Verifies DTC commits return per-op session tokens in canonical '{pkRangeId}:{lsn}'
        // format on success, and that SDK FromJson accepts what real DTC actually emits.
        [TestMethod]
        public async Task DtxWrite_CommitReturnsCanonicalSessionTokens()
        {
            string pk = $"canonical-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "canonical-token-test" };

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"DTX commit should succeed. Got: {response.StatusCode}");
            Assert.IsTrue(response.Count > 0, "Response should have at least one operation result.");

            string sessionToken = response[0].SessionToken;
            Assert.IsFalse(string.IsNullOrWhiteSpace(sessionToken),
                "Session token should be present after a successful DTC commit.");

            int colonIndex = sessionToken.IndexOf(':');
            Assert.IsTrue(colonIndex > 0 && colonIndex < sessionToken.Length - 1,
                $"Session token '{sessionToken}' should be in canonical '{{pkRangeId}}:{{lsn}}' format.");
        }

        // After a DTX write, a point read of the same item succeeds.
        // Session consistency proves tokens were merged into the SessionContainer.
        [TestMethod]
        public async Task DtxWrite_ThenPointRead_Succeeds()
        {
            string pk = $"read-after-write-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "read-after-dtx-write" };

            DistributedTransactionResponse dtxResponse = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(dtxResponse.IsSuccessStatusCode,
                $"DTX commit failed: {dtxResponse.StatusCode}");

            try
            {
                ItemResponse<dynamic> readResponse = await this.container.ReadItemAsync<dynamic>(
                    id,
                    new PartitionKey(pk),
                    new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Session });

                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound && ex.SubStatusCode == 1002)
            {
                Assert.Fail(
                    "Got ReadSessionNotAvailable (404/1002) on Session read. " +
                    "Session tokens from DTX write were NOT merged into the session container.");
            }
        }

        // DTX read after DTX write succeeds via the shared SessionContainer.
        [TestMethod]
        public async Task DtxWrite_ThenDtxRead_Succeeds()
        {
            string pk = $"write-then-read-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "dtx-read-after-write" };

            DistributedTransactionResponse writeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(writeResponse.IsSuccessStatusCode,
                $"DTX write failed: {writeResponse.StatusCode}");

            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"DTX read failed: {readResponse.StatusCode}");
            Assert.IsTrue(readResponse.Count > 0, "DTX read should return at least one operation result.");
            Assert.AreEqual(HttpStatusCode.OK, readResponse[0].StatusCode,
                $"DTX read op should be 200 OK, got {readResponse[0].StatusCode}");
        }

        // ─── Session token: DTC / backend contract probes ──────────────────────

        // DTC returns one canonical session token per operation in multi-op commits.
        [TestMethod]
        public async Task DtxWrite_MultiOp_AllOpsReturnTokens()
        {
            string pk = $"multi-op-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            var doc1 = new { id = id1, pk, value = "multi-op-1" };
            var doc2 = new { id = id2, pk, value = "multi-op-2" };

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id1, doc1)
                .CreateItem(this.container, new PartitionKey(pk), id2, doc2)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode, $"DTX commit failed: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);

            for (int i = 0; i < response.Count; i++)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(response[i].SessionToken),
                    $"Op[{i}] should carry its own session token (DTC must not collapse per-op tokens).");
            }
        }

        // DTX read-only transactions must return session tokens for each operation.
        [TestMethod]
        public async Task DtxRead_OnlyResponse_ReturnsSessionTokens()
        {
            string pk = $"read-only-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "read-only-probe" };

            // Seed via a regular point write so the session container starts populated.
            await this.container.CreateItemAsync(doc, new PartitionKey(pk));

            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"DTX read failed: {readResponse.StatusCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(readResponse[0].SessionToken),
                "DTC must return a session token on read-only DTX responses so the SDK can merge it.");
        }

        // DTC + backend behavior when the client supplies a future-LSN session token.
        [TestMethod]
        public async Task DtxRead_WithFutureSessionToken_BehavesPredictably()
        {
            string pk = $"future-tok-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "future-tok-probe" };

            ItemResponse<dynamic> seed = await this.container.CreateItemAsync<dynamic>(doc, new PartitionKey(pk));
            string seedToken = seed.Headers.Session;
            Assert.IsFalse(string.IsNullOrWhiteSpace(seedToken),
                "Seed write must return a session token to fabricate a future-LSN variant.");

            // Fabricate a future-LSN token by bumping the LSN segment by a large delta.
            int colon = seedToken.IndexOf(':');
            Assert.IsTrue(colon > 0, $"Seed session token '{seedToken}' is not in canonical format.");
            string pkRangeId = seedToken.Substring(0, colon);
            string lsnSegment = seedToken.Substring(colon + 1);
            int hashIdx = lsnSegment.IndexOf('#');
            string globalLsnStr = hashIdx > 0 ? lsnSegment.Substring(0, hashIdx) : lsnSegment;
            if (!long.TryParse(globalLsnStr, out long lsn))
            {
                Assert.Fail($"Could not parse LSN from '{lsnSegment}'.");
            }
            string futureToken = $"{pkRangeId}:{lsn + 1_000_000}";

            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(
                    this.container,
                    new PartitionKey(pk),
                    id,
                    new DistributedTransactionRequestOptions { SessionToken = futureToken })
                .CommitTransactionAsync(CancellationToken.None);

            // Acceptable: OK (blocked until catch-up) or 404/1002 (ReadSessionNotAvailable)
            Assert.IsTrue(
                readResponse.IsSuccessStatusCode || (int)readResponse.StatusCode == 404,
                $"Future-LSN session token produced unexpected status {readResponse.StatusCode}.");
        }

        // Pins down DTC's session-token emission on failed transactions.
        [TestMethod]
        public async Task DtxCommit_FailedTransaction_TokenEmissionBehavior()
        {
            string pk = $"fail-tok-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "to-be-conflicted" };

            // Create the item once, then attempt a second Create with the same id to force 409.
            await this.container.CreateItemAsync(doc, new PartitionKey(pk));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsFalse(response.IsSuccessStatusCode,
                "Re-creating an existing item via DTX must fail (expected 409 on the op).");

            // DTC contract (verified 2026-06-13): failed transactions return empty session tokens.
            string token = response.Count > 0 ? response[0].SessionToken : null;
            Console.WriteLine(
                $"[CONTRACT] DTC failed-commit envelope: StatusCode={response.StatusCode}, " +
                $"Op[0].StatusCode={(response.Count > 0 ? response[0].StatusCode.ToString() : "<none>")}, " +
                $"Op[0].SessionToken='{token ?? "<null>"}'");

            // DTC does NOT emit session tokens on failed transactions.
            // The SDK silently skips merge via the IsNullOrEmpty check.
            Assert.IsTrue(string.IsNullOrEmpty(token),
                "DTC contract: failed transactions return empty session tokens. " +
                "If this assertion fails, the contract has changed and the SDK can now merge tokens on failures.");
        }

        // ─── A2 contract verification: compound (multi-partition) token ─────────

        // Verifies that the coordinator correctly handles a DTX write spanning multiple
        // partitions (different PKs). The SDK resolves a per-partition session token for each
        // operation (or none when that partition has no token yet) rather than substituting a
        // compound collection-wide token, and the coordinator returns each op's per-partition token.
        [TestMethod]
        public async Task DtxWrite_MultiPartition_PerPartitionTokensReturnedByCoordinator()
        {
            // Use distinct partition keys to target different physical partitions.
            string pk1 = $"mp-a-{Guid.NewGuid():N}";
            string pk2 = $"mp-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            var doc1 = new { id = id1, pk = pk1, value = "partition-A" };
            var doc2 = new { id = id2, pk = pk2, value = "partition-B" };

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk1), id1, doc1)
                .CreateItem(this.container, new PartitionKey(pk2), id2, doc2)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Multi-partition DTX commit should succeed. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);

            // Each op should return its own per-partition session token.
            string token0 = response[0].SessionToken;
            string token1 = response[1].SessionToken;
            Assert.IsFalse(string.IsNullOrWhiteSpace(token0),
                "Op[0] (partition A) must return a session token.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(token1),
                "Op[1] (partition B) must return a session token.");

            Console.WriteLine(
                $"[A2 CONTRACT] Multi-partition tokens: Op[0]='{token0}', Op[1]='{token1}'");
        }

        // After a multi-partition DTX write, a subsequent Session-level point read of each
        // item succeeds — proving the SDK merged per-op tokens correctly into the SessionContainer.
        [TestMethod]
        public async Task DtxWrite_MultiPartition_ThenPointReads_Succeed()
        {
            string pk1 = $"mp-read-a-{Guid.NewGuid():N}";
            string pk2 = $"mp-read-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            var doc1 = new { id = id1, pk = pk1, value = "read-mp-A" };
            var doc2 = new { id = id2, pk = pk2, value = "read-mp-B" };

            DistributedTransactionResponse dtxResponse = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk1), id1, doc1)
                .CreateItem(this.container, new PartitionKey(pk2), id2, doc2)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(dtxResponse.IsSuccessStatusCode,
                $"Multi-partition DTX commit failed: {dtxResponse.StatusCode}");

            // Both reads must succeed at Session consistency (no 404/1002).
            ItemResponse<dynamic> read1 = await this.container.ReadItemAsync<dynamic>(
                id1, new PartitionKey(pk1),
                new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Session });
            Assert.AreEqual(HttpStatusCode.OK, read1.StatusCode,
                "Point read of item in partition A failed after DTX write.");

            ItemResponse<dynamic> read2 = await this.container.ReadItemAsync<dynamic>(
                id2, new PartitionKey(pk2),
                new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Session });
            Assert.AreEqual(HttpStatusCode.OK, read2.StatusCode,
                "Point read of item in partition B failed after DTX write.");
        }

        // ─── Multi-container session token tests ────────────────────────────────

        // DTX write spanning two containers: proves session tokens are merged per-collection
        // and subsequent reads of both containers succeed.
        [TestMethod]
        public async Task DtxWrite_MultiContainer_SessionTokensMergedPerCollection()
        {
            // Create a second container.
            string container2Id = $"DtxE2E-container2-{Guid.NewGuid():N}";
            Database database = this.client.GetDatabase(DatabaseId);
            ContainerResponse container2Response = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(container2Id, PartitionKeyPath));
            Container container2 = container2Response.Container;

            try
            {
                string pk1 = $"mc-a-{Guid.NewGuid():N}";
                string pk2 = $"mc-b-{Guid.NewGuid():N}";
                string id1 = Guid.NewGuid().ToString();
                string id2 = Guid.NewGuid().ToString();
                var doc1 = new { id = id1, pk = pk1, value = "container-1" };
                var doc2 = new { id = id2, pk = pk2, value = "container-2" };

                DistributedTransactionResponse response = await this.client
                    .CreateDistributedWriteTransaction()
                    .CreateItem(this.container, new PartitionKey(pk1), id1, doc1)
                    .CreateItem(container2, new PartitionKey(pk2), id2, doc2)
                    .CommitTransactionAsync(CancellationToken.None);

                Assert.IsTrue(response.IsSuccessStatusCode,
                    $"Multi-container DTX commit should succeed. Got: {response.StatusCode}");
                Assert.AreEqual(2, response.Count);

                // Both ops must have session tokens.
                Assert.IsFalse(string.IsNullOrWhiteSpace(response[0].SessionToken),
                    "Op[0] (container1) must carry a session token.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(response[1].SessionToken),
                    "Op[1] (container2) must carry a session token.");

                // Verify reads succeed at Session level — proves tokens merged into each container's session.
                ItemResponse<dynamic> read1 = await this.container.ReadItemAsync<dynamic>(
                    id1, new PartitionKey(pk1),
                    new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Session });
                Assert.AreEqual(HttpStatusCode.OK, read1.StatusCode);

                ItemResponse<dynamic> read2 = await container2.ReadItemAsync<dynamic>(
                    id2, new PartitionKey(pk2),
                    new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Session });
                Assert.AreEqual(HttpStatusCode.OK, read2.StatusCode);
            }
            finally
            {
                await container2.DeleteContainerAsync();
            }
        }

        // ─── Multi-database session token tests ─────────────────────────────────

        // DTX write spanning two databases: tokens from each database/collection merge
        // independently into the SessionContainer.
        [TestMethod]
        public async Task DtxWrite_MultiDatabase_SessionTokensMergedPerDatabase()
        {
            string db2Id = $"DtxE2E-db2-{Guid.NewGuid():N}";
            Database database2 = (await this.client.CreateDatabaseIfNotExistsAsync(db2Id)).Database;

            try
            {
                ContainerResponse container2Response = await database2.CreateContainerIfNotExistsAsync(
                    new ContainerProperties($"container-{Guid.NewGuid():N}", PartitionKeyPath));
                Container container2 = container2Response.Container;

                string pk1 = $"mdb-a-{Guid.NewGuid():N}";
                string pk2 = $"mdb-b-{Guid.NewGuid():N}";
                string id1 = Guid.NewGuid().ToString();
                string id2 = Guid.NewGuid().ToString();
                var doc1 = new { id = id1, pk = pk1, value = "db1-item" };
                var doc2 = new { id = id2, pk = pk2, value = "db2-item" };

                DistributedTransactionResponse response = await this.client
                    .CreateDistributedWriteTransaction()
                    .CreateItem(this.container, new PartitionKey(pk1), id1, doc1)
                    .CreateItem(container2, new PartitionKey(pk2), id2, doc2)
                    .CommitTransactionAsync(CancellationToken.None);

                Assert.IsTrue(response.IsSuccessStatusCode,
                    $"Multi-database DTX commit should succeed. Got: {response.StatusCode}");
                Assert.AreEqual(2, response.Count);

                Assert.IsFalse(string.IsNullOrWhiteSpace(response[0].SessionToken),
                    "Op[0] (db1) must have a session token.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(response[1].SessionToken),
                    "Op[1] (db2) must have a session token.");

                // Verify point reads succeed.
                ItemResponse<dynamic> read1 = await this.container.ReadItemAsync<dynamic>(
                    id1, new PartitionKey(pk1),
                    new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Session });
                Assert.AreEqual(HttpStatusCode.OK, read1.StatusCode);

                ItemResponse<dynamic> read2 = await container2.ReadItemAsync<dynamic>(
                    id2, new PartitionKey(pk2),
                    new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Session });
                Assert.AreEqual(HttpStatusCode.OK, read2.StatusCode);
            }
            finally
            {
                await database2.DeleteAsync();
            }
        }

        // ─── Round-trip: DTX result token → DTX request options ─────────────────

        // Verifies that a session token obtained from a DTX write result can be passed into
        // a subsequent DTX read via DistributedTransactionRequestOptions.SessionToken.
        [TestMethod]
        public async Task DtxWrite_TokenRoundTrip_IntoSubsequentDtxRead()
        {
            string pk = $"roundtrip-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "round-trip-test" };

            DistributedTransactionResponse writeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(writeResponse.IsSuccessStatusCode,
                $"DTX write failed: {writeResponse.StatusCode}");
            string writeToken = writeResponse[0].SessionToken;
            Assert.IsFalse(string.IsNullOrWhiteSpace(writeToken),
                "Write result must have a session token for round-trip.");

            // Use the write token as an explicit session token on a DTX read.
            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(
                    this.container,
                    new PartitionKey(pk),
                    id,
                    new DistributedTransactionRequestOptions { SessionToken = writeToken })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"DTX read with round-tripped session token should succeed. Got: {readResponse.StatusCode}");
            Assert.AreEqual(HttpStatusCode.OK, readResponse[0].StatusCode,
                "Read op with the write's session token should return 200 OK.");
        }

        // ─── Per-partition token round-trip ─────────────────────────────────────

        // After a multi-partition write, a second DTX read auto-resolves the per-partition
        // token for each op from the SessionContainer and verifies the read succeeds.
        // This proves the auto-resolution path (A2) sends a valid per-partition token.
        [TestMethod]
        public async Task DtxWrite_MultiPartition_AutoResolvedToken_ReadSucceeds()
        {
            string pk1 = $"ar-a-{Guid.NewGuid():N}";
            string pk2 = $"ar-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            var doc1 = new { id = id1, pk = pk1, value = "auto-resolve-A" };
            var doc2 = new { id = id2, pk = pk2, value = "auto-resolve-B" };

            // Write two items to different partitions.
            DistributedTransactionResponse writeResponse = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk1), id1, doc1)
                .CreateItem(this.container, new PartitionKey(pk2), id2, doc2)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(writeResponse.IsSuccessStatusCode,
                $"Multi-partition write failed: {writeResponse.StatusCode}");

            // DTX read WITHOUT explicit session token — relies on auto-resolution from SessionContainer.
            // The SDK resolves each op's per-partition token (falling back to the compound token only
            // if the routing map is unavailable). The coordinator must accept it for both ops.
            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk1), id1)
                .ReadItem(this.container, new PartitionKey(pk2), id2)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(readResponse.IsSuccessStatusCode,
                $"DTX read with auto-resolved per-partition token should succeed. Got: {readResponse.StatusCode}");
            Assert.AreEqual(2, readResponse.Count);
            Assert.AreEqual(HttpStatusCode.OK, readResponse[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, readResponse[1].StatusCode);
        }

        // ─── Bad / malformed token behavior ─────────────────────────────────────

        // Verifies that a DTX read with an invalid (fabricated) pkRangeId token
        // either succeeds (coordinator ignores unknown range) or fails gracefully.
        [TestMethod]
        public async Task DtxRead_WithInvalidPkRangeId_GracefulBehavior()
        {
            string pk = $"bad-range-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "bad-range-probe" };

            // Seed the item so the read has something to find.
            await this.container.CreateItemAsync(doc, new PartitionKey(pk));

            // Fabricate a token with a non-existent pkRangeId.
            string invalidToken = "99999:42";

            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(
                    this.container,
                    new PartitionKey(pk),
                    id,
                    new DistributedTransactionRequestOptions { SessionToken = invalidToken })
                .CommitTransactionAsync(CancellationToken.None);

            // Acceptable: coordinator ignores the unknown range (200), blocks (timeout),
            // or returns 404/1002. Not acceptable: 500 or unhandled exception.
            Console.WriteLine(
                $"[CONTRACT] Invalid-pkRangeId: StatusCode={readResponse.StatusCode}");
            Assert.IsTrue(
                readResponse.IsSuccessStatusCode ||
                (int)readResponse.StatusCode == 404 ||
                (int)readResponse.StatusCode == 408,
                $"Invalid pkRangeId should not cause 500. Got: {readResponse.StatusCode}");
        }

        // Verifies that a completely garbled session token (not in pkRangeId:lsn format)
        // is handled gracefully — the coordinator or SDK should not crash.
        [TestMethod]
        public async Task DtxRead_WithGarbledToken_DoesNotCrash()
        {
            string pk = $"garbled-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();
            var doc = new { id, pk, value = "garbled-probe" };

            await this.container.CreateItemAsync(doc, new PartitionKey(pk));

            // Completely non-parseable token.
            string garbledToken = "not-a-valid-session-token!!!";

            DistributedTransactionResponse readResponse = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(
                    this.container,
                    new PartitionKey(pk),
                    id,
                    new DistributedTransactionRequestOptions { SessionToken = garbledToken })
                .CommitTransactionAsync(CancellationToken.None);

            // DTC may reject or ignore the bad token. We just verify it doesn't crash the SDK.
            Console.WriteLine(
                $"[CONTRACT] Garbled token: StatusCode={readResponse.StatusCode}");
            Assert.IsTrue(
                readResponse.IsSuccessStatusCode ||
                (int)readResponse.StatusCode == 400 ||
                (int)readResponse.StatusCode == 404 ||
                (int)readResponse.StatusCode == 408,
                $"Garbled session token should fail gracefully. Got: {readResponse.StatusCode}");
        }
    }
}
