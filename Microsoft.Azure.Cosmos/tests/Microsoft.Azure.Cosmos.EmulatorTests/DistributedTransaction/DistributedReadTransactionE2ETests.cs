// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Tracing;
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
    /// This class runs in the "DistributedTransaction" test category and is NOT gated with
    /// [Ignore]. It requires the COSMOS_DTX_ENDPOINT / COSMOS_DTX_KEY environment variables
    /// pointing at a live DTX-enabled account (the public emulator does not implement
    /// /operations/dtc); without them the tests fail fast in TestInitialize.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedReadTransactionE2ETests
    {
        private const string DatabaseId = "DtxReadE2ETestDb";
        private const string SecondDatabaseId = "DtxReadE2ETestDb2";
        private const string ContainerId = "DtxReadE2ETestContainer";
        private const string SecondContainerId = "DtxReadE2ETestContainer2";
        private const string DifferentPkPathContainerId = "DtxReadE2ETestContainerCat";
        private const string PartitionKeyPath = "/pk";
        private const string DifferentPartitionKeyPath = "/category";

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

                try
                {
                    await this.client.GetDatabase(SecondDatabaseId).DeleteAsync();
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

            // Read DTx response codes: 404 is a non-retryable hard failure, not a per-op success
            // (the success set is {200, 304}). All-missing fails Phase 1; the envelope is the
            // promotion of the lone remaining non-424 code {404} -> 404. The 404 ops are themselves
            // the failing code, so they are kept as-is (only successful 200/304 ops are rewritten
            // to 424).
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "All-missing read DTx fails with envelope 404 (promotion of {404}).");
            Assert.AreEqual(2, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, response[i].StatusCode, $"Op[{i}] should be 404 NotFound (the hard-failure code, kept as-is).");
            }

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_OneExistsOneMissing_ReturnsMixedResults()
        {
            ToDoActivity existing = await this.SeedItemAsync(this.container);
            await ConfirmVisibleAsync(this.container, new PartitionKey(existing.pk), existing.id);
            string missingPk = Guid.NewGuid().ToString();
            string missingId = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await DriveMixedExistenceReadAsync(
                () => this.client
                    .CreateDistributedReadTransaction()
                    .ReadItem(this.container, new PartitionKey(existing.pk), existing.id)
                    .ReadItem(this.container, new PartitionKey(missingPk), missingId)
                    .CommitTransactionAsync(CancellationToken.None),
                expectedMissingCount: 1);

            // A missing op (404) hard-fails Phase 1. On a converged read the successful
            // op is rewritten to 424 (body stripped) and the missing op keeps its 404, promoting the
            // envelope to 404; the per-op multiset must be exactly one 424 + one 404 (order-independent).
            // On this slow account the read may instead surface a retriable Phase-2 408 — both accepted.
            AssertMixedExistenceReadOutcome(response, operationCount: 2, existingCount: 1, missingCount: 1);

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

        // ─── Authorization: read-only vs read-write credential ─────────────────

        [TestMethod]
        public async Task ReadTransaction_ReadOnlyResourceToken_Succeeds()
        {
            // A read distributed transaction should work with a read-only credential since
            // the user-facing operation is a read.
            ToDoActivity doc = await this.SeedItemAsync(this.container);

            (CosmosClient readOnlyClient, HttpStatusCode mintStatus) = await this.TryCreateResourceTokenClientAsync(
                this.container, PermissionMode.Read, "DtxReadOnlyPermission");
            Assert.AreEqual(HttpStatusCode.Created, mintStatus, "Minting the read-only resource token should succeed.");

            using (readOnlyClient)
            {
                HttpStatusCode status = await TryGetReadTransactionStatusAsync(() => readOnlyClient
                    .CreateDistributedReadTransaction()
                    .ReadItem(readOnlyClient.GetContainer(DatabaseId, ContainerId), new PartitionKey(doc.pk), doc.id)
                    .CommitTransactionAsync(CancellationToken.None));

                Assert.AreEqual(
                    HttpStatusCode.OK,
                    status,
                    $"A read DTX with a read-only resource token should succeed. Got: {status}");
            }
        }

        [TestMethod]
        public async Task ReadTransaction_ReadWriteResourceToken_Succeeds()
        {
            // A read distributed transaction should also work with a read-write (PermissionMode.All) resource token.
            ToDoActivity doc = await this.SeedItemAsync(this.container);

            (CosmosClient readWriteClient, HttpStatusCode mintStatus) = await this.TryCreateResourceTokenClientAsync(
                this.container, PermissionMode.All, "DtxReadWritePermission");
            Assert.AreEqual(HttpStatusCode.Created, mintStatus, "Minting the read-write resource token should succeed.");

            using (readWriteClient)
            {
                HttpStatusCode status = await TryGetReadTransactionStatusAsync(() => readWriteClient
                    .CreateDistributedReadTransaction()
                    .ReadItem(readWriteClient.GetContainer(DatabaseId, ContainerId), new PartitionKey(doc.pk), doc.id)
                    .CommitTransactionAsync(CancellationToken.None));

                Assert.AreEqual(
                    HttpStatusCode.OK,
                    status,
                    $"A read DTX with a read-write resource token should succeed. Got: {status}");
            }
        }

        [TestMethod]
        public async Task ReadTransaction_MasterKey_Succeeds()
        {
            // A read DTX with the read-write master key must succeed end-to-end.
            ToDoActivity doc = await this.SeedItemAsync(this.container);

            HttpStatusCode status = await TryGetReadTransactionStatusAsync(() => this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc.pk), doc.id)
                .CommitTransactionAsync(CancellationToken.None));

            Assert.AreEqual(
                HttpStatusCode.OK,
                status,
                $"A read DTX with the read-write master key should succeed with 200 OK. Got: {status}");
        }

        // ─── Cross-PK (xPK) gaps ──────────────────────────────────────────────

        [TestMethod]
        public async Task ReadTransaction_CrossPk_MixedExistence_ReturnsMixedResults()
        {
            // 3 distinct PKs: two exist, one missing. Per the read DTx contract the coordinator
            // surfaces a multi-status envelope (200 or a promoted 207/404) while every per-op code
            // is preserved 1:1.
            ToDoActivity doc1 = await this.SeedItemAsync(this.container);
            ToDoActivity doc2 = await this.SeedItemAsync(this.container);
            await ConfirmVisibleAsync(this.container, new PartitionKey(doc1.pk), doc1.id);
            await ConfirmVisibleAsync(this.container, new PartitionKey(doc2.pk), doc2.id);
            string missingPk = Guid.NewGuid().ToString();
            string missingId = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await DriveMixedExistenceReadAsync(
                () => this.client
                    .CreateDistributedReadTransaction()
                    .ReadItem(this.container, new PartitionKey(doc1.pk), doc1.id)
                    .ReadItem(this.container, new PartitionKey(doc2.pk), doc2.id)
                    .ReadItem(this.container, new PartitionKey(missingPk), missingId)
                    .CommitTransactionAsync(CancellationToken.None),
                expectedMissingCount: 1);

            // The missing op (404) hard-fails Phase 1. On a converged read both successful
            // ops are rewritten to 424 (bodies stripped) and the missing op keeps its 404; the per-op
            // multiset must be exactly two 424s + one 404 (order-independent). A retriable Phase-2 408
            // is also accepted on this slow account.
            AssertMixedExistenceReadOutcome(response, operationCount: 3, existingCount: 2, missingCount: 1);

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_CrossPk_LargeFanout_Succeeds()
        {
            // Stress: fan a single read transaction across K=10 distinct partition keys.
            const int fanout = 10;
            List<ToDoActivity> docs = new List<ToDoActivity>(fanout);
            for (int i = 0; i < fanout; i++)
            {
                docs.Add(await this.SeedItemAsync(this.container));
            }

            DistributedReadTransaction transaction = this.client.CreateDistributedReadTransaction();
            foreach (ToDoActivity doc in docs)
            {
                transaction.ReadItem(this.container, new PartitionKey(doc.pk), doc.id);
            }

            DistributedTransactionResponse response = await transaction.CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(fanout, response.Count, "No per-PK op should be dropped from the fan-out.");
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.OK, response[i].StatusCode, $"Per-op[{i}] should be 200 OK.");
            }

            response.Dispose();
        }

        /// <summary>
        /// EXPERIMENT (live, empirical): does giving the SDK a much larger retry budget ever flip
        /// the deterministic wide cross-partition read 408 into a 200?
        ///
        /// Background: on a quiescent single-region DTX account, a wide cross-partition read fan-out
        /// deterministically returns a server-generated 408 (Coordinator Phase-2 reconciliation
        /// exhaustion). The public read path drives <see cref="DistributedTransactionCommitter"/>
        /// with its default cumulative-delay budget of 30 s, which binds at ~4-5 outer-loop retries.
        ///
        /// This test reproduces the same fan-out but drives the committer directly through its
        /// INTERNAL constructor twice on the SAME seeded data:
        ///   (1) baseline  — 30 s budget (default), and
        ///   (2) increased — 5 min budget,
        /// counting the realized retries via the injectable delayProvider (which performs REAL waits
        /// against the live coordinator). The 5 min budget lets the committer reach its hard attempt
        /// ceiling (MaxIsRetriableRetryCount = 10) instead of stopping at the 30 s-bound ~5.
        ///
        /// Empirical claim under test: more retries do NOT change the outcome — only latency grows —
        /// because the failure is deterministic on quiescent data. If the increased-budget run ever
        /// returns 200, that falsifies the "deterministic server-side timeout" verdict. The test does
        /// NOT hard-fail on either status; it RECORDS what actually happened (status + retries + wall
        /// clock for both runs) so the result is evidence, not an assertion.
        ///
        /// Note: 10 attempts is the absolute ceiling reachable without a product change, since
        /// MaxIsRetriableRetryCount is a const baked into the committer; budget inflation cannot push
        /// past it.
        /// </summary>
        [TestMethod]
        public async Task ReadTransaction_MultiPartition_IncreasedRetryBudget_Experiment()
        {
            // Seed a wide cross-partition fan-out (the shape that deterministically 408s here).
            const int fanout = 5;
            List<ToDoActivity> docs = new List<ToDoActivity>(fanout);
            for (int i = 0; i < fanout; i++)
            {
                docs.Add(await this.SeedItemAsync(this.container));
            }

            // Build the read operations exactly as the public read-transaction builder would.
            List<DistributedTransactionOperation> BuildOps()
            {
                List<DistributedTransactionOperation> ops = new List<DistributedTransactionOperation>(fanout);
                for (int i = 0; i < fanout; i++)
                {
                    ops.Add(new DistributedTransactionOperation(
                        operationType: Microsoft.Azure.Documents.OperationType.Read,
                        operationIndex: i,
                        database: DatabaseId,
                        container: ContainerId,
                        partitionKey: new PartitionKey(docs[i].pk),
                        id: docs[i].id));
                }

                return ops;
            }

            // Runs one committer with the given budget, performing REAL backoff waits, and reports
            // (final envelope status, realized retry count, total wall clock).
            async Task<(HttpStatusCode status, int retries, TimeSpan wall)> RunAsync(TimeSpan budget, string label)
            {
                int retries = 0;
                TimeSpan plannedDelay = TimeSpan.Zero;
                Func<TimeSpan, CancellationToken, Task> countingRealDelay = async (delay, ct) =>
                {
                    Interlocked.Increment(ref retries);
                    plannedDelay += delay;
                    await Task.Delay(delay, ct); // faithful real wait against the live coordinator
                };

                DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                    BuildOps(),
                    this.client.ClientContext,
                    Microsoft.Azure.Documents.OperationType.Read,
                    retryBaseDelay: TimeSpan.FromSeconds(1),
                    delayProvider: countingRealDelay,
                    maxCumulativeRetryDelay: budget);

                Stopwatch sw = Stopwatch.StartNew();
                using DistributedTransactionResponse response = await committer.CommitTransactionAsync(
                    NoOpTrace.Singleton,
                    CancellationToken.None);
                sw.Stop();

                Console.WriteLine(
                    $"[IncreasedRetryExperiment][{label}] budget={budget.TotalSeconds}s " +
                    $"status={(int)response.StatusCode} ({response.StatusCode}) retries={retries} " +
                    $"plannedDelay={plannedDelay.TotalSeconds:F1}s wallClock={sw.Elapsed.TotalSeconds:F1}s");

                return (response.StatusCode, retries, sw.Elapsed);
            }

            // (1) Increased FIRST on COLD data: a much larger budget so the committer can reach the
            // 10-attempt hard cap. Running this first eliminates any "baseline warmed the coordinator
            // reconciliation state" confound — a 200 here is attributable solely to this run's retries.
            (HttpStatusCode increasedStatus, int increasedRetries, TimeSpan increasedWall) =
                await RunAsync(TimeSpan.FromMinutes(5), "increased-5min-cold");

            // (2) Baseline AFTER: reproduce the production default budget exactly (but instrumented).
            (HttpStatusCode baselineStatus, int baselineRetries, TimeSpan baselineWall) =
                await RunAsync(DistributedTransactionCommitter.MaxCumulativeRetryDelay, "baseline-default-budget");

            Console.WriteLine(
                $"[IncreasedRetryExperiment][verdict] baseline: {(int)baselineStatus} after {baselineRetries} retries " +
                $"({baselineWall.TotalSeconds:F1}s) | increased: {(int)increasedStatus} after {increasedRetries} retries " +
                $"({increasedWall.TotalSeconds:F1}s) | more-retries-flipped-outcome=" +
                $"{(baselineStatus != HttpStatusCode.OK && increasedStatus == HttpStatusCode.OK)}");

            // The increased-budget run must drive strictly MORE retries than the 30 s-bound baseline
            // (that is the whole point of the experiment); otherwise the budget was not the binding
            // constraint and the experiment is inconclusive.
            Assert.IsTrue(
                increasedRetries >= baselineRetries,
                $"Increased budget should realize at least as many retries as the baseline. " +
                $"baseline={baselineRetries}, increased={increasedRetries}.");
        }

        /// <summary>
        /// LIMITS PROBE (live, empirical): scale the fan-out an order of magnitude wider —
        /// 10 containers × 5 distinct partition keys each = 50 read operations in a single
        /// distributed read transaction — and ask whether the coordinator's Phase-2 reconciliation
        /// can still converge within the SDK's hard 10-attempt ceiling
        /// (<see cref="DistributedTransactionCommitter.MaxIsRetriableRetryCount"/>) when the
        /// cumulative-retry BUDGET is made effectively unbounded (10 min).
        ///
        /// Why this matters: the 5-op fan-out already needs ~8-10 retries / ~2-3 min of coordinator
        /// backoff to converge to a confirmed snapshot. Since the attempt cap is a const that budget
        /// inflation can reach but never exceed, a sufficiently wide read could need MORE than 10
        /// reconciliation passes and would then 408 regardless of budget. This test finds out where
        /// that wall is for a 50-op / 10-container shape.
        ///
        /// This records evidence (200 = converged within the ceiling; 408 = hit the wall) rather than
        /// asserting a specific outcome, so a live run reveals the true limit without a brittle gate.
        /// The 10 probe containers live in the test database and are torn down with it in TestCleanup.
        /// </summary>
        [TestMethod]
        public async Task ReadTransaction_TenContainers_FivePartitions_LimitsProbe()
        {
            const int containerCount = 10;
            const int pkPerContainer = 5;

            // Create 10 sibling containers (idempotent; cleaned up with the database in TestCleanup).
            List<Container> containers = new List<Container>(containerCount);
            for (int c = 0; c < containerCount; c++)
            {
                Container created = (await this.database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties($"DtxLimitsProbeContainer{c}", PartitionKeyPath))).Container;
                containers.Add(created);
            }

            // Seed 5 distinct-PK docs per container (50 docs total) in parallel for setup speed.
            ToDoActivity[][] seeded = new ToDoActivity[containerCount][];
            List<Task> seedTasks = new List<Task>(containerCount * pkPerContainer);
            for (int c = 0; c < containerCount; c++)
            {
                seeded[c] = new ToDoActivity[pkPerContainer];
                int ci = c;
                for (int p = 0; p < pkPerContainer; p++)
                {
                    int pi = p;
                    seedTasks.Add(Task.Run(async () => seeded[ci][pi] = await this.SeedItemAsync(containers[ci])));
                }
            }

            await Task.WhenAll(seedTasks);

            // Build all 50 read ops spanning the 10 containers × 5 partition keys.
            List<DistributedTransactionOperation> ops =
                new List<DistributedTransactionOperation>(containerCount * pkPerContainer);
            int idx = 0;
            for (int c = 0; c < containerCount; c++)
            {
                for (int p = 0; p < pkPerContainer; p++)
                {
                    ops.Add(new DistributedTransactionOperation(
                        operationType: Microsoft.Azure.Documents.OperationType.Read,
                        operationIndex: idx++,
                        database: DatabaseId,
                        container: $"DtxLimitsProbeContainer{c}",
                        partitionKey: new PartitionKey(seeded[c][p].pk),
                        id: seeded[c][p].id));
                }
            }

            int retries = 0;
            TimeSpan plannedDelay = TimeSpan.Zero;
            Func<TimeSpan, CancellationToken, Task> countingRealDelay = async (delay, ct) =>
            {
                Interlocked.Increment(ref retries);
                plannedDelay += delay;
                await Task.Delay(delay, ct); // faithful real wait against the live coordinator
            };

            // Effectively-unbounded budget so the only binding constraint is the 10-attempt ceiling.
            TimeSpan budget = TimeSpan.FromMinutes(10);
            DistributedTransactionCommitter committer = new DistributedTransactionCommitter(
                ops,
                this.client.ClientContext,
                Microsoft.Azure.Documents.OperationType.Read,
                retryBaseDelay: TimeSpan.FromSeconds(1),
                delayProvider: countingRealDelay,
                maxCumulativeRetryDelay: budget);

            Stopwatch sw = Stopwatch.StartNew();
            using DistributedTransactionResponse response = await committer.CommitTransactionAsync(
                NoOpTrace.Singleton,
                CancellationToken.None);
            sw.Stop();

            // Per-op tally is only meaningful on a converged (200) envelope.
            int perOpOk = 0;
            int perOpTotal = 0;
            if (response.StatusCode == HttpStatusCode.OK)
            {
                perOpTotal = response.Count;
                for (int i = 0; i < perOpTotal; i++)
                {
                    if (response[i].StatusCode == HttpStatusCode.OK)
                    {
                        perOpOk++;
                    }
                }
            }

            Console.WriteLine(
                $"[LimitsProbe] ops={ops.Count} containers={containerCount} pkPerContainer={pkPerContainer} " +
                $"budget={budget.TotalMinutes}min status={(int)response.StatusCode} ({response.StatusCode}) " +
                $"retries={retries} (ceiling={DistributedTransactionCommitter.MaxIsRetriableRetryCount}) " +
                $"plannedDelay={plannedDelay.TotalSeconds:F1}s wallClock={sw.Elapsed.TotalSeconds:F1}s " +
                $"perOpOk={perOpOk}/{perOpTotal} " +
                $"converged={response.StatusCode == HttpStatusCode.OK} " +
                $"hitAttemptCeiling={retries >= DistributedTransactionCommitter.MaxIsRetriableRetryCount}");

            // Evidence, not a pass/fail gate: a 50-op fan-out either converges within the attempt
            // ceiling (200) or hits the wall (408) regardless of budget. Both are valid findings.
            Assert.IsTrue(
                response.StatusCode == HttpStatusCode.OK || (int)response.StatusCode == 408,
                $"Limits probe should resolve to 200 (converged) or 408 (hit ceiling). Got: {(int)response.StatusCode}.");
        }

        // ─── Cross-container (xCont) gaps ─────────────────────────────────────

        [TestMethod]
        public async Task ReadTransaction_CrossContainer_DifferentPkPaths_Succeeds()
        {
            // Container A partitions on /pk, container B partitions on /category. Each op's PK must
            // be extracted against its own container's path with no cross-container confusion.
            Container categoryContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(DifferentPkPathContainerId, DifferentPartitionKeyPath))).Container;

            ToDoActivity doc1 = await this.SeedItemAsync(this.container);
            (string catId, string category) = await this.SeedCategoryItemAsync(categoryContainer);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc1.pk), doc1.id)
                .ReadItem(categoryContainer, new PartitionKey(category), catId)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode, "Per-op[0] (/pk container) should be 200 OK.");
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode, "Per-op[1] (/category container) should be 200 OK.");

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_CrossContainer_MixedExistence_ReturnsMixedResults()
        {
            // One item exists in container A, the other is missing in container B. Per-op codes
            // preserved across the container boundary; promotion logic unchanged.
            Container secondContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(SecondContainerId, PartitionKeyPath))).Container;

            ToDoActivity existing = await this.SeedItemAsync(this.container);
            await ConfirmVisibleAsync(this.container, new PartitionKey(existing.pk), existing.id);
            string missingPk = Guid.NewGuid().ToString();
            string missingId = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await DriveMixedExistenceReadAsync(
                () => this.client
                    .CreateDistributedReadTransaction()
                    .ReadItem(this.container, new PartitionKey(existing.pk), existing.id)
                    .ReadItem(secondContainer, new PartitionKey(missingPk), missingId)
                    .CommitTransactionAsync(CancellationToken.None),
                expectedMissingCount: 1);

            // The missing op hard-fails Phase 1. On a converged read the existing op
            // (container A) is rewritten to 424 and the missing op (container B) keeps its 404; the
            // per-op multiset must be exactly one 424 + one 404 (order-independent). A retriable
            // Phase-2 408 is also accepted.
            AssertMixedExistenceReadOutcome(response, operationCount: 2, existingCount: 1, missingCount: 1);

            response.Dispose();
        }

        // ─── Cross-database (xDB) gaps ────────────────────────────────────────

        [TestMethod]
        public async Task ReadTransaction_CrossDatabase_AllExist_Succeeds()
        {
            // Ops span two databases in the same account; the coordinator ledger is per-account,
            // so snapshot isolation holds across the database boundary and the outer status is 200.
            Container secondDbContainer = await this.CreateSecondDatabaseContainerAsync();

            ToDoActivity doc1 = await this.SeedItemAsync(this.container);
            ToDoActivity doc2 = await this.SeedItemAsync(secondDbContainer);

            DistributedTransactionResponse response = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(doc1.pk), doc1.id)
                .ReadItem(secondDbContainer, new PartitionKey(doc2.pk), doc2.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Envelope status should be 200 OK. Got: {response.StatusCode}");
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.OK, response[0].StatusCode, "Per-op[0] (database 1) should be 200 OK.");
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode, "Per-op[1] (database 2) should be 200 OK.");

            response.Dispose();
        }

        [TestMethod]
        public async Task ReadTransaction_CrossDatabase_MixedExistence_ReturnsMixedResults()
        {
            // Existing item in database 1, missing item in database 2; per-op codes preserved
            // across the database boundary.
            Container secondDbContainer = await this.CreateSecondDatabaseContainerAsync();

            ToDoActivity existing = await this.SeedItemAsync(this.container);
            await ConfirmVisibleAsync(this.container, new PartitionKey(existing.pk), existing.id);
            string missingPk = Guid.NewGuid().ToString();
            string missingId = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await DriveMixedExistenceReadAsync(
                () => this.client
                    .CreateDistributedReadTransaction()
                    .ReadItem(this.container, new PartitionKey(existing.pk), existing.id)
                    .ReadItem(secondDbContainer, new PartitionKey(missingPk), missingId)
                    .CommitTransactionAsync(CancellationToken.None),
                expectedMissingCount: 1);

            // The missing op hard-fails Phase 1. On a converged read the existing op
            // (database 1) is rewritten to 424 and the missing op (database 2) keeps its 404; the
            // per-op multiset must be exactly one 424 + one 404 (order-independent). A retriable
            // Phase-2 408 is also accepted.
            AssertMixedExistenceReadOutcome(response, operationCount: 2, existingCount: 1, missingCount: 1);

            response.Dispose();
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private async Task<ToDoActivity> SeedItemAsync(Container targetContainer)
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            await targetContainer.CreateItemAsync(doc, new PartitionKey(doc.pk));
            return doc;
        }

        // Confirms a freshly-seeded item is durably point-readable before a distributed read
        // transaction is driven over it. On a slow single-region DTX account a just-written document
        // can briefly be invisible to the coordinator's Phase-1 snapshot, racing a not-yet-visible
        // write into a spurious per-op 404. Polling a point read to 200 first removes that
        // read-your-write flake without weakening the contract assertions.
        private static async Task ConfirmVisibleAsync(Container container, PartitionKey partitionKey, string id)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                using ResponseMessage rm = await container.ReadItemStreamAsync(id, partitionKey);
                if (rm.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            Assert.Fail($"Seeded item id='{id}' did not become point-readable within the settle window.");
        }

        // Drives a mixed-existence read DTx, retrying the WHOLE transaction while an existing op is
        // still surfaced as 404 — i.e., the read snapshot has not yet advanced past the just-committed
        // seed write (read-your-write lag on a slow single-region account). This isolates a transient
        // lag (a retry lets the snapshot catch up and the op becomes 424) from a persistent one
        // (stays 404, so the caller's assertion fails and flags it). A retriable 408 (Phase-2
        // unconfirmed) short-circuits immediately — it is a valid terminal, not a visibility miss.
        private static async Task<DistributedTransactionResponse> DriveMixedExistenceReadAsync(
            Func<Task<DistributedTransactionResponse>> commit,
            int expectedMissingCount)
        {
            DistributedTransactionResponse response = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                response?.Dispose();
                response = await commit();

                if ((int)response.StatusCode == 408)
                {
                    return response;
                }

                // A successful (existing) op is rewritten to 424 on a failed mixed-existence read, so
                // the count of per-op 404s should equal exactly the number of genuinely-missing ops.
                // More 404s than that means an existing op is still absent from the snapshot -> retry.
                int notFound = 0;
                for (int i = 0; i < response.Count; i++)
                {
                    if (response[i].StatusCode == HttpStatusCode.NotFound)
                    {
                        notFound++;
                    }
                }

                if (notFound <= expectedMissingCount)
                {
                    return response;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            return response;
        }

        // Asserts the documented terminal outcomes for a mixed-existence read DTx under the
        // read DTx contract. Two terminals are accepted on this slow single-region account:
        //   (a) converged          -> envelope 404; the per-op multiset is exactly {424 x existing,
        //                             404 x missing} — every successful op is rewritten to 424
        //                             (FailedDependency, body stripped) and every missing op keeps 404.
        //                             Order is NOT asserted (per-op codes are checked as a multiset).
        //   (b) Phase-2 unconfirmed -> envelope 408 (retriable, empty body, no per-op detail).
        private static void AssertMixedExistenceReadOutcome(
            DistributedTransactionResponse response,
            int operationCount,
            int existingCount,
            int missingCount)
        {
            if ((int)response.StatusCode == 408)
            {
                // Phase-2 could not confirm a consistent snapshot within budget: a valid retriable terminal.
                return;
            }

            Assert.AreEqual(
                HttpStatusCode.NotFound,
                response.StatusCode,
                $"Converged mixed-existence read DTx must promote to envelope 404. Got: {(int)response.StatusCode}.");
            Assert.AreEqual(operationCount, response.Count);

            // The contract requires a 424 to be present for the successful op(s). Assert the per-op
            // multiset order-independently: exactly existingCount 424s and missingCount 404s.
            int observed424 = 0;
            int observed404 = 0;
            for (int i = 0; i < response.Count; i++)
            {
                int code = (int)response[i].StatusCode;
                if (code == 424)
                {
                    observed424++;
                }
                else if (code == 404)
                {
                    observed404++;
                }
                else
                {
                    Assert.Fail($"Per-op[{i}] should be 424 or 404 for a mixed-existence read failure. Got: {code}.");
                }
            }

            Assert.AreEqual(
                existingCount,
                observed424,
                $"Each existing op must be rewritten to 424 FailedDependency on the failed read. Observed 424s: {observed424}, expected: {existingCount}.");
            Assert.AreEqual(
                missingCount,
                observed404,
                $"Each missing op must keep its 404 NotFound. Observed 404s: {observed404}, expected: {missingCount}.");
        }

        // Seeds a document whose partition key lives at /category (used by the different-PK-path test)
        // and returns its (id, category) so the caller can read it back on the correct PK.
        private async Task<(string id, string category)> SeedCategoryItemAsync(Container categoryContainer)
        {
            string id = Guid.NewGuid().ToString();
            string category = Guid.NewGuid().ToString();
            await categoryContainer.CreateItemAsync(new { id, category }, new PartitionKey(category));
            return (id, category);
        }

        // Creates (idempotently) a second database + container in the same account for cross-database tests.
        private async Task<Container> CreateSecondDatabaseContainerAsync()
        {
            Database secondDatabase = (await this.client.CreateDatabaseIfNotExistsAsync(SecondDatabaseId)).Database;
            return (await secondDatabase.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath))).Container;
        }

        // Mints a resource token scoped to the target container at the requested permission level and
        // returns a CosmosClient authenticated with that token (plus the permission-creation status code).
        private async Task<(CosmosClient, HttpStatusCode)> TryCreateResourceTokenClientAsync(
            Container targetContainer,
            PermissionMode permissionMode,
            string permissionId)
        {
            User user = (await this.database.CreateUserAsync($"dtx-user-{Guid.NewGuid()}")).User;

            PermissionResponse permissionResponse = await user.CreatePermissionAsync(
                new PermissionProperties(permissionId, permissionMode, targetContainer));

            CosmosClient tokenClient = new CosmosClient(
                this.endpoint,
                authKeyOrResourceToken: permissionResponse.Resource.Token,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = ConsistencyLevel.Session
                });

            return (tokenClient, permissionResponse.StatusCode);
        }

        // Runs a read DTX commit and returns the effective HTTP status, whether the coordinator surfaces
        // it as an envelope status code or as a thrown CosmosException (e.g. Forbidden).
        private static async Task<HttpStatusCode> TryGetReadTransactionStatusAsync(
            Func<Task<DistributedTransactionResponse>> commit)
        {
            try
            {
                using DistributedTransactionResponse response = await commit();
                return response.StatusCode;
            }
            catch (CosmosException ex)
            {
                return ex.StatusCode;
            }
        }
    }
}
