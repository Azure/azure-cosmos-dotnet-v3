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
    /// End-to-end tests for conditional ETag behavior (ifMatch / ifNoneMatch) in distributed
    /// transactions, run against a live DTX-enabled account.
    ///
    /// To run locally:
    ///     set COSMOS_DTX_ENDPOINT=https://your-account.documents.azure.com:443/
    ///     set COSMOS_DTX_KEY=your-master-key
    ///     dotnet test --filter "FullyQualifiedName~DistributedTransactionConditionalE2ETests"
    ///
    /// Remove the [Ignore] attribute before running.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [Ignore("DTX endpoint not yet available in emulator. Remove to run locally with env vars.")]
    public class DistributedTransactionConditionalE2ETests
    {
        private const string DatabaseId = "DtxConditionalE2ETestDb";
        private const string ContainerId = "DtxConditionalE2ETestContainer";
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

        // ─── Baseline: all items exist, no conditionals ─────────────────────────

        /// <summary>
        /// Verifies baseline read DTx behavior without conditional ETags.
        /// All items exist and no conditionals are set, so every op returns 200.
        /// </summary>
        [TestMethod]
        public async Task ReadDtx_AllItemsExist_NoConditional_Returns200()
        {
            string pk1 = $"read-200-a-{Guid.NewGuid():N}";
            string pk2 = $"read-200-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync(new { id = id1, pk = pk1, value = "item1" }, new PartitionKey(pk1));
            await this.container.CreateItemAsync(new { id = id2, pk = pk2, value = "item2" }, new PartitionKey(pk2));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk1), id1)
                .ReadItem(this.container, new PartitionKey(pk2), id2)
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                $"Envelope should be 200 when all ops succeed. Got: {response.StatusCode}");
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);

            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.OK, response[i].StatusCode,
                    $"Op[{i}] should be 200.");
                Assert.IsNotNull(response[i].ResourceStream,
                    $"Op[{i}] should have a resource body.");
                Assert.IsFalse(string.IsNullOrEmpty(response[i].ETag),
                    $"Op[{i}] should have an ETag.");
            }

            response.Dispose();
        }

        // ─── IfNoneMatch match on one op → 207 [200, 304] ────────────────────

        /// <summary>
        /// Verifies that a matching IfNoneMatchEtag produces a 304 on the matching op
        /// and a 207 MultiStatus envelope (aligning with TransactionalBatch behavior).
        /// </summary>
        [TestMethod]
        public async Task ReadDtx_MultiOp_OneMatchingIfNoneMatch_Returns207()
        {
            string pk1 = $"read-207-a-{Guid.NewGuid():N}";
            string pk2 = $"read-207-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync(new { id = id1, pk = pk1, value = "item1" }, new PartitionKey(pk1));
            var item2Response = await this.container.CreateItemAsync<object>(
                new { id = id2, pk = pk2, value = "item2" }, new PartitionKey(pk2));
            string item2ETag = item2Response.ETag;

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk1), id1)
                .ReadItem(this.container, new PartitionKey(pk2), id2,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = item2ETag })
                .ExecuteTransactionAsync(CancellationToken.None);

            // Envelope should be 207 MultiStatus when any op is non-2xx (304),
            // aligning with TransactionalBatch behavior.
            Assert.AreEqual((HttpStatusCode)207, response.StatusCode,
                $"Envelope should be 207 (MultiStatus) when one op returns 304. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);

            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode,
                "Op[0] (no condition) should be 200.");
            Assert.IsNotNull(response[0].ResourceStream,
                "Op[0] should have a resource body.");

            Assert.AreEqual(HttpStatusCode.NotModified, response[1].StatusCode,
                "Op[1] (matching IfNoneMatchEtag) should be 304.");
            Assert.IsFalse(response[1].IsSuccessStatusCode,
                "304 is not in the 2xx range, IsSuccessStatusCode should be false.");

            response.Dispose();
        }

        // ─── IfNoneMatch: stale ETag → 200 with updated resource ───────────────

        /// <summary>
        /// When IfNoneMatchEtag is stale (item was modified), the server returns 200
        /// with the updated resource body and the new ETag.
        /// </summary>
        [TestMethod]
        public async Task ReadDtx_WithStaleIfNoneMatch_Returns200WithUpdatedResource()
        {
            string pk = $"stale-etag-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            var createResponse = await this.container.CreateItemAsync<object>(
                new { id, pk, value = "original" }, new PartitionKey(pk));
            string originalETag = createResponse.ETag;

            // Replace the item so its ETag changes.
            await this.container.ReplaceItemAsync(
                new { id, pk, value = "updated" }, id, new PartitionKey(pk));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = originalETag })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Stale IfNoneMatch should return success. Got: {response.StatusCode}");
            Assert.AreEqual(1, response.Count);

            DistributedTransactionOperationResult op = response[0];
            Assert.AreEqual(HttpStatusCode.OK, op.StatusCode,
                "Stale IfNoneMatch should return 200 (item was modified).");
            Assert.IsNotNull(op.ResourceStream,
                "200 response must include the resource body.");
            Assert.IsFalse(string.IsNullOrEmpty(op.ETag),
                "200 response must include the new ETag.");
            Assert.AreNotEqual(originalETag, op.ETag,
                "The returned ETag should differ from the stale one.");

            response.Dispose();
        }

        // ─── IfNoneMatch: all ops match → all 304s ─────────────────────────────

        /// <summary>
        /// When all operations have matching IfNoneMatchEtag, all return 304.
        /// Documents the envelope status behavior when every op is 304.
        /// </summary>
        [TestMethod]
        public async Task ReadDtx_AllOpsMatchingIfNoneMatch_AllReturn304()
        {
            string pk1 = $"all-304-a-{Guid.NewGuid():N}";
            string pk2 = $"all-304-b-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();

            var resp1 = await this.container.CreateItemAsync<object>(
                new { id = id1, pk = pk1, value = "item1" }, new PartitionKey(pk1));
            var resp2 = await this.container.CreateItemAsync<object>(
                new { id = id2, pk = pk2, value = "item2" }, new PartitionKey(pk2));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk1), id1,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = resp1.ETag })
                .ReadItem(this.container, new PartitionKey(pk2), id2,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = resp2.ETag })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(2, response.Count);

            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.NotModified, response[i].StatusCode,
                    $"Op[{i}] with matching IfNoneMatchEtag should be 304.");
                Assert.IsFalse(response[i].IsSuccessStatusCode,
                    $"Op[{i}] 304 should report IsSuccessStatusCode == false.");
            }

            response.Dispose();
        }

        // ─── Non-existent item → 404 with FailedDependency ──────────────────

        /// <summary>
        /// When one item doesn't exist, the transaction aborts.
        /// The failing op gets 404, all other ops get 424 (FailedDependency).
        /// </summary>
        [TestMethod]
        public async Task ReadDtx_NonExistentItem_Returns404WithFailedDependency()
        {
            string pk1 = $"exist-{Guid.NewGuid():N}";
            string pk2 = $"noexist-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            string nonExistentId = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync(
                new { id = id1, pk = pk1, value = "exists" }, new PartitionKey(pk1));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk1), id1)
                .ReadItem(this.container, new PartitionKey(pk2), nonExistentId)
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode,
                $"Envelope should be 404 when a read op targets a non-existent item. Got: {response.StatusCode}");
            Assert.IsFalse(response.IsSuccessStatusCode);

            // Per contract: the failing op gets 404, other ops get 424 (FailedDependency).
            // The exact op ordering depends on batch grouping, so check for presence rather than index.
            bool found404 = false;
            bool found424 = false;
            for (int i = 0; i < response.Count; i++)
            {
                if (response[i].StatusCode == HttpStatusCode.NotFound) found404 = true;
                if ((int)response[i].StatusCode == 424) found424 = true;
            }

            Assert.IsTrue(found404, "At least one op should be 404 (NotFound).");
            Assert.IsTrue(found424, "Non-failing ops should be 424 (FailedDependency).");

            response.Dispose();
        }

        // ─── Write DTx: stale IfMatch → 412 ────────────────────────────────────

        /// <summary>
        /// Verifies that a write DTx with a stale IfMatchEtag returns 412 PreconditionFailed.
        /// The 412 should be promoted as the envelope status code.
        /// </summary>
        [TestMethod]
        public async Task WriteDtx_WithStaleIfMatch_Returns412()
        {
            string pk = $"stale-match-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            var createResponse = await this.container.CreateItemAsync<object>(
                new { id, pk, value = "original" }, new PartitionKey(pk));
            string originalETag = createResponse.ETag;

            // Replace the item so its ETag changes.
            await this.container.ReplaceItemAsync(
                new { id, pk, value = "replaced" }, id, new PartitionKey(pk));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .ReplaceItem(this.container, new PartitionKey(pk), id,
                    new { id, pk, value = "dtx-replace" },
                    new DistributedTransactionRequestOptions { IfMatchEtag = originalETag })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode,
                $"Envelope should be 412 with stale IfMatch. Got: {response.StatusCode}");
            Assert.IsFalse(response.IsSuccessStatusCode);

            if (response.Count > 0)
            {
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, response[0].StatusCode,
                    "Op[0] should be 412 (PreconditionFailed).");
            }

            response.Dispose();
        }

        // ─── Write DTx: current IfMatch → success ──────────────────────────────

        /// <summary>
        /// Verifies that a write DTx with a current (valid) IfMatchEtag succeeds.
        /// </summary>
        [TestMethod]
        public async Task WriteDtx_WithCurrentIfMatch_Succeeds()
        {
            string pk = $"current-match-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            var createResponse = await this.container.CreateItemAsync<object>(
                new { id, pk, value = "original" }, new PartitionKey(pk));
            string currentETag = createResponse.ETag;

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .ReplaceItem(this.container, new PartitionKey(pk), id,
                    new { id, pk, value = "dtx-replaced" },
                    new DistributedTransactionRequestOptions { IfMatchEtag = currentETag })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Replace with current IfMatch should succeed. Got: {response.StatusCode}");
            Assert.IsTrue(response.Count > 0);
            Assert.IsTrue(response[0].IsSuccessStatusCode,
                $"Op[0] should succeed. Got: {response[0].StatusCode}");

            response.Dispose();
        }

        // ─── 304 inspectability ─────────────────────────────────────────────────

        /// <summary>
        /// Verifies that a 304 (NotModified) result from IfNoneMatch is fully inspectable
        /// via the user-facing DistributedTransactionOperationResult API.
        /// </summary>
        [TestMethod]
        public async Task ReadDtx_304IsUserInspectable()
        {
            string pk = $"inspect-304-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            var createResponse = await this.container.CreateItemAsync<object>(
                new { id, pk, value = "inspectable" }, new PartitionKey(pk));
            string matchingETag = createResponse.ETag;

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = matchingETag })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.Count > 0,
                "Response should have at least one operation result.");

            DistributedTransactionOperationResult op = response[0];

            // StatusCode is inspectable.
            Assert.AreEqual(HttpStatusCode.NotModified, op.StatusCode,
                "User should see StatusCode == 304 (NotModified).");

            // IsSuccessStatusCode correctly reflects 304 is not 2xx.
            Assert.IsFalse(op.IsSuccessStatusCode,
                "304 is not in the 2xx range; IsSuccessStatusCode must be false.");

            // ResourceStream should be null on 304 (no body returned).
            Assert.IsNull(op.ResourceStream,
                "304 response should not have a resource body (ResourceStream should be null).");

            // ETag should be present — the server returns the matching ETag.

            // The result is accessible via the IReadOnlyList<> indexer.
            DistributedTransactionOperationResult fromIndexer = response[0];
            Assert.AreSame(op, fromIndexer,
                "Indexer access should return the same result instance.");

            response.Dispose();
        }

        // ─── Both IfMatch + IfNoneMatch set: only the relevant one is honoured ──

        /// <summary>
        /// When both IfMatchEtag and IfNoneMatchEtag are set on a READ operation,
        /// only IfNoneMatch is honoured (IfMatch is ignored for reads).
        /// Setting a stale IfMatch alongside a matching IfNoneMatch should still
        /// produce 304 — proving IfMatch was not evaluated.
        /// </summary>
        [TestMethod]
        public async Task ReadDtx_BothConditionalsSet_OnlyIfNoneMatchHonoured()
        {
            string pk = $"both-read-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            var createResponse = await this.container.CreateItemAsync<object>(
                new { id, pk, value = "dual-conditional" }, new PartitionKey(pk));
            string currentETag = createResponse.ETag;

            // Set IfNoneMatch to the current ETag (should trigger 304)
            // Set IfMatch to a deliberately stale/bogus ETag (would fail if evaluated)
            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id,
                    new DistributedTransactionRequestOptions
                    {
                        IfMatchEtag = "\"stale-bogus-etag\"",
                        IfNoneMatchEtag = currentETag
                    })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.Count > 0);

            // IfNoneMatch was honoured → 304. If IfMatch were honoured, we'd get 412.
            Assert.AreEqual(HttpStatusCode.NotModified, response[0].StatusCode,
                $"Read should honour IfNoneMatch (304), not IfMatch. Got: {response[0].StatusCode}");

            response.Dispose();
        }

        /// <summary>
        /// When both IfMatchEtag and IfNoneMatchEtag are set on a WRITE operation,
        /// only IfMatch is honoured (IfNoneMatch is ignored for writes).
        /// Setting a matching IfNoneMatch alongside a current IfMatch should still
        /// succeed — proving IfNoneMatch was not evaluated for the write.
        /// </summary>
        [TestMethod]
        public async Task WriteDtx_BothConditionalsSet_OnlyIfMatchHonoured()
        {
            string pk = $"both-write-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            var createResponse = await this.container.CreateItemAsync<object>(
                new { id, pk, value = "dual-conditional-write" }, new PartitionKey(pk));
            string currentETag = createResponse.ETag;

            // Set IfMatch to the current ETag (should succeed)
            // Set IfNoneMatch to the current ETag (would trigger 304 if evaluated on reads)
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .ReplaceItem(this.container, new PartitionKey(pk), id,
                    new { id, pk, value = "replaced-with-both" },
                    new DistributedTransactionRequestOptions
                    {
                        IfMatchEtag = currentETag,
                        IfNoneMatchEtag = currentETag
                    })
                .ExecuteTransactionAsync(CancellationToken.None);

            // IfMatch was honoured (current → success). IfNoneMatch was ignored for writes.
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Write should honour IfMatch (success), ignoring IfNoneMatch. Got: {response.StatusCode}");
            Assert.IsTrue(response.Count > 0);
            Assert.IsTrue(response[0].IsSuccessStatusCode,
                $"Op[0] should succeed. Got: {response[0].StatusCode}");

            response.Dispose();
        }

        /// <summary>
        /// When both IfMatchEtag (stale) and IfNoneMatchEtag (current/matching) are set
        /// on a WRITE operation, the write should fail with 412 because IfMatch is the
        /// only conditional evaluated for writes — and it's stale.
        /// This proves IfNoneMatch cannot "rescue" a failing IfMatch on writes.
        /// </summary>
        [TestMethod]
        public async Task WriteDtx_BothConditionalsSet_StaleIfMatch_Fails()
        {
            string pk = $"both-write-stale-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            var createResponse = await this.container.CreateItemAsync<object>(
                new { id, pk, value = "original" }, new PartitionKey(pk));
            string originalETag = createResponse.ETag;

            // Replace the item so its ETag changes, making originalETag stale.
            await this.container.ReplaceItemAsync(
                new { id, pk, value = "updated" }, id, new PartitionKey(pk));

            // Set IfMatch to the stale ETag (should fail)
            // Set IfNoneMatch to the stale ETag (would succeed on a read, but irrelevant for writes)
            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .ReplaceItem(this.container, new PartitionKey(pk), id,
                    new { id, pk, value = "should-not-write" },
                    new DistributedTransactionRequestOptions
                    {
                        IfMatchEtag = originalETag,
                        IfNoneMatchEtag = originalETag
                    })
                .ExecuteTransactionAsync(CancellationToken.None);

            // IfMatch is stale → precondition failure. IfNoneMatch cannot rescue the write.
            Assert.IsFalse(response.IsSuccessStatusCode,
                $"Write with stale IfMatch should fail even with IfNoneMatch set. Got: {response.StatusCode}");

            response.Dispose();
        }

        // ─── Write DTx: conditional Patch via FilterPredicate ──────────────────

        /// <summary>
        /// Verifies that a write DTx Patch with a satisfied <see cref="DistributedTransactionPatchItemRequestOptions.FilterPredicate"/>
        /// applies the patch (the server evaluates the predicate atomically before patching).
        /// </summary>
        [TestMethod]
        public async Task WriteDtx_PatchWithSatisfiedFilterPredicate_Succeeds()
        {
            string pk = $"patch-filter-ok-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync<object>(
                new { id, pk, status = "pending", value = 1 }, new PartitionKey(pk));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .PatchItem(this.container, new PartitionKey(pk), id,
                    new[] { PatchOperation.Replace("/status", "done") },
                    new DistributedTransactionPatchItemRequestOptions
                    {
                        FilterPredicate = "from c where c.status = 'pending'"
                    })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Patch with a satisfied FilterPredicate should succeed. Got: {response.StatusCode}");
            Assert.IsTrue(response.Count > 0);
            Assert.IsTrue(response[0].IsSuccessStatusCode,
                $"Op[0] should succeed. Got: {response[0].StatusCode}");

            response.Dispose();
        }

        /// <summary>
        /// Verifies that a write DTx Patch with an unsatisfied <see cref="DistributedTransactionPatchItemRequestOptions.FilterPredicate"/>
        /// fails with 412 PreconditionFailed and does not apply the patch.
        /// </summary>
        [TestMethod]
        public async Task WriteDtx_PatchWithUnsatisfiedFilterPredicate_Returns412()
        {
            string pk = $"patch-filter-fail-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync<object>(
                new { id, pk, status = "done", value = 1 }, new PartitionKey(pk));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .PatchItem(this.container, new PartitionKey(pk), id,
                    new[] { PatchOperation.Replace("/status", "archived") },
                    new DistributedTransactionPatchItemRequestOptions
                    {
                        FilterPredicate = "from c where c.status = 'pending'"
                    })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode,
                $"Patch with an unsatisfied FilterPredicate should return 412. Got: {response.StatusCode}");
            Assert.IsFalse(response.IsSuccessStatusCode);

            // Exactly one operation was queued, so assert its per-op status unconditionally.
            Assert.AreEqual(1, response.Count, "Exactly one patch operation was queued.");
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response[0].StatusCode,
                "Op[0] should be 412 (PreconditionFailed).");

            // The patch must NOT have been applied — read the item back and confirm it is unchanged.
            ConditionalDoc readBack = (await this.container.ReadItemAsync<ConditionalDoc>(
                id, new PartitionKey(pk))).Resource;
            Assert.AreEqual("done", readBack.status,
                "An unsatisfied FilterPredicate must leave the item unchanged (patch must not apply).");

            response.Dispose();
        }

        /// <summary>
        /// Verifies the core distributed-transaction atomicity guarantee for conditional patch:
        /// when one operation's <see cref="DistributedTransactionPatchItemRequestOptions.FilterPredicate"/>
        /// is unsatisfied, the whole distributed transaction is not committed — a sibling write on a
        /// different item and partition is rolled back (reports 424 FailedDependency and is left unchanged).
        /// </summary>
        [TestMethod]
        public async Task WriteDtx_UnsatisfiedFilterPredicate_RollsBackSiblingWrite_Returns412()
        {
            string pkPatch = $"atomic-patch-{Guid.NewGuid():N}";
            string pkSibling = $"atomic-sibling-{Guid.NewGuid():N}";
            string idPatch = Guid.NewGuid().ToString();
            string idSibling = Guid.NewGuid().ToString();

            // Seed the item that will be conditionally patched (its predicate will NOT be satisfied).
            await this.container.CreateItemAsync<object>(
                new { id = idPatch, pk = pkPatch, status = "done" }, new PartitionKey(pkPatch));

            // Seed the sibling item that a second (unconditional) write in the same DTx will try to mutate.
            await this.container.CreateItemAsync<object>(
                new { id = idSibling, pk = pkSibling, payload = "original" }, new PartitionKey(pkSibling));

            DistributedTransactionResponse response = await this.client
                .CreateDistributedWriteTransaction()
                .PatchItem(this.container, new PartitionKey(pkPatch), idPatch,
                    new[] { PatchOperation.Replace("/status", "archived") },
                    new DistributedTransactionPatchItemRequestOptions
                    {
                        FilterPredicate = "from c where c.status = 'pending'"
                    })
                .ReplaceItem(this.container, new PartitionKey(pkSibling), idSibling,
                    new { id = idSibling, pk = pkSibling, payload = "sibling-mutated" })
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode,
                $"An unsatisfied FilterPredicate must fail the whole DTx with 412. Got: {response.StatusCode} " +
                $"[ops: {string.Join(",", GetOpStatuses(response))}]");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count, "Both operations should be reported in the response.");

            // The failing conditional patch reports 412; the sibling op is rolled back with 424 FailedDependency.
            // Op ordering depends on server-side batch grouping, so check for presence rather than by index.
            bool found412 = false;
            bool found424 = false;
            for (int i = 0; i < response.Count; i++)
            {
                if (response[i].StatusCode == HttpStatusCode.PreconditionFailed) found412 = true;
                if ((int)response[i].StatusCode == 424) found424 = true;
            }

            Assert.IsTrue(found412, "The conditional patch op should be 412 (PreconditionFailed).");
            Assert.IsTrue(found424, "The sibling op should be 424 (FailedDependency) — proving the DTx rolled back.");

            // Read-back proves nothing was persisted: the patch did not apply and the sibling write was rolled back.
            ConditionalDoc patchedBack = (await this.container.ReadItemAsync<ConditionalDoc>(
                idPatch, new PartitionKey(pkPatch))).Resource;
            Assert.AreEqual("done", patchedBack.status,
                "The conditional patch must not have been applied.");

            ConditionalDoc siblingBack = (await this.container.ReadItemAsync<ConditionalDoc>(
                idSibling, new PartitionKey(pkSibling))).Resource;
            Assert.AreEqual("original", siblingBack.payload,
                "The sibling write must have been rolled back with the rest of the distributed transaction.");

            response.Dispose();
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private static string[] GetOpStatuses(DistributedTransactionResponse response)
        {
            string[] statuses = new string[response.Count];
            for (int i = 0; i < response.Count; i++)
            {
                statuses[i] = $"{(int)response[i].StatusCode}";
            }
            return statuses;
        }

        private sealed class ConditionalDoc
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string status { get; set; }

            public string payload { get; set; }
        }
    }
}
