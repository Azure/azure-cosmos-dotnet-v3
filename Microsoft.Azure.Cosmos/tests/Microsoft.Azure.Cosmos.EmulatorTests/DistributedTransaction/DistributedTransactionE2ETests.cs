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
                Assert.Fail(
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
    }
}
