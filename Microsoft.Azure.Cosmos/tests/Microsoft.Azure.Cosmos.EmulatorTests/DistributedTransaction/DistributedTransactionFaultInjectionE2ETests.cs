// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// HYBRID fault-injection end-to-end tests for Distributed Transactions (DTX/DTC).
    ///
    /// The pattern these tests validate is the genuine fault-injection contract: a fault is
    /// injected at the transport layer (below <c>RetryHandler</c>) for the first N attempts via the
    /// FaultInjection <c>ChaosInterceptor</c>; once the rule's hit-limit (<see
    /// cref="FaultInjectionServerErrorResultBuilder.WithTimes(int)"/>) is exhausted the interceptor
    /// stops firing and the SDK's transport retry policy (<c>ClientRetryPolicy</c>) re-sends the
    /// request — which now flows through to the REAL transaction Coordinator and (for a retriable
    /// error) ultimately succeeds. In other words: MOCK the error, then let the RETRY go REAL.
    ///
    /// This is the only layer that exercises the SDK's <b>inner</b> transport retry loop for DTX
    /// (the committer's outer loop only retries when the response body carries
    /// <c>isRetriable:true</c>, which the empty-body retriable envelope codes do not — see
    /// <c>DistributedTransactionCommitter</c>). Whether a given injected server-error type is
    /// retried-then-resent by the Gateway-DTX inner loop is asserted by the test outcome itself
    /// (hit-count &gt;= N AND ultimate success), never assumed.
    ///
    /// To run locally:
    ///     set COSMOS_DTX_ENDPOINT=https://your-account.documents.azure.com:443/
    ///     set COSMOS_DTX_KEY=your-master-key
    ///     dotnet test --filter "FullyQualifiedName~DistributedTransactionFaultInjectionE2ETests"
    ///
    /// This class runs in the "DistributedTransaction" test category and is NOT gated with
    /// [Ignore]. It requires the COSMOS_DTX_ENDPOINT / COSMOS_DTX_KEY environment variables
    /// pointing at a live DTX-enabled account (the public emulator does not implement
    /// /operations/dtc, and a live DTX-enabled Coordinator is required to observe the
    /// retry-then-real-success behaviour); without them the tests fail fast in TestInitialize.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedTransactionFaultInjectionE2ETests
    {
        private const string DatabaseId = "DtxFaultInjectionE2ETestDb";
        private const string ContainerId = "DtxFaultInjectionE2ETestContainer";
        private const string PartitionKeyPath = "/pk";

        private CosmosClient bootstrapClient;
        private string endpoint;
        private string key;
        private Database database;
        private Container bootstrapContainer;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.endpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT");
            this.key = Environment.GetEnvironmentVariable("COSMOS_DTX_KEY");

            if (string.IsNullOrWhiteSpace(this.endpoint) || string.IsNullOrWhiteSpace(this.key))
            {
                Assert.Fail("COSMOS_DTX_ENDPOINT and COSMOS_DTX_KEY environment variables must be set.");
            }

            // A non-fault-injected bootstrap client used only to create the database/container and to
            // seed items. Each test builds its own fault-injected client from the same endpoint+key.
            this.bootstrapClient = new CosmosClient(
                this.endpoint,
                this.key,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = ConsistencyLevel.Session
                });

            this.database = (await this.bootstrapClient.CreateDatabaseIfNotExistsAsync(DatabaseId)).Database;
            this.bootstrapContainer = (await this.database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath))).Container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.bootstrapClient != null)
            {
                try
                {
                    await this.bootstrapClient.GetDatabase(DatabaseId).DeleteAsync();
                }
                catch { /* ignore */ }

                this.bootstrapClient.Dispose();
            }
        }

        // ─── Hybrid: inject N times → retry reaches the real Coordinator → success ──────────

        /// <summary>
        /// WRITE DTx hybrid: inject a single retriable server error on the
        /// <see cref="FaultInjectionOperationType.DistributedTransactionWriteBatch"/> request, then
        /// assert the SDK retried (rule hit at least once) AND the commit ultimately succeeded
        /// against the real Coordinator. Data-driven over every retriable transport error type so
        /// the run itself reveals exactly which ones the Gateway-DTX inner loop retries-then-resends.
        /// </summary>
        // Note: the Gateway fault-injection path does not support RetryWith (449) or Gone (410) —
        // those codes are only emitted on the Direct-mode path — so they are intentionally excluded
        // from these Gateway DTX rows.
        [DataTestMethod]
        [DataRow(FaultInjectionServerErrorType.ServiceUnavailable)]
        [DataRow(FaultInjectionServerErrorType.TooManyRequests)]
        [DataRow(FaultInjectionServerErrorType.Timeout)]
        public async Task WriteTransaction_RetriableServerError_RetriesThenSucceedsAgainstRealCoordinator(
            FaultInjectionServerErrorType errorType)
        {
            string pk = $"fi-write-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            string ruleId = $"dtx-write-{errorType}-{Guid.NewGuid():N}";
            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition: new FaultInjectionConditionBuilder()
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .WithOperationType(FaultInjectionOperationType.DistributedTransactionWriteBatch)
                    .Build(),
                result: FaultInjectionResultBuilder.GetResultBuilder(errorType)
                    .WithTimes(1)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            using CosmosClient fiClient = this.CreateFaultInjectedClient(rule, out _);
            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "fi-create" })
                .CommitTransactionAsync(CancellationToken.None);

            // The fault MUST have fired (proves the request was intercepted before the Coordinator)...
            Assert.IsTrue(
                rule.GetHitCount() >= 1,
                $"[{errorType}] FaultInjection rule should have been hit at least once. Hit count: {rule.GetHitCount()}.");

            // ...and the SDK retry MUST have flowed through to the real Coordinator and committed.
            Assert.IsTrue(
                response.IsSuccessStatusCode,
                $"[{errorType}] After the injected fault was exhausted, the retried commit should " +
                $"succeed against the real Coordinator. Got: {response.StatusCode} (hit count {rule.GetHitCount()}).");
            Assert.IsTrue(response.Count > 0);
            Assert.IsTrue(
                response[0].IsSuccessStatusCode,
                $"[{errorType}] Per-op[0] should succeed. Got: {response[0].StatusCode}.");

            response.Dispose();
        }

        /// <summary>
        /// READ DTx hybrid: inject a single retriable server error on the
        /// <see cref="FaultInjectionOperationType.DistributedTransactionReadBatch"/> request, then
        /// assert the SDK retried AND the read transaction ultimately returned 200 from the real
        /// Coordinator.
        /// </summary>
        [DataTestMethod]
        [DataRow(FaultInjectionServerErrorType.ServiceUnavailable)]
        [DataRow(FaultInjectionServerErrorType.TooManyRequests)]
        [DataRow(FaultInjectionServerErrorType.Timeout)]
        public async Task ReadTransaction_RetriableServerError_RetriesThenSucceedsAgainstRealCoordinator(
            FaultInjectionServerErrorType errorType)
        {
            ToDoActivity doc = await this.SeedItemAsync(this.bootstrapContainer);

            string ruleId = $"dtx-read-{errorType}-{Guid.NewGuid():N}";
            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition: new FaultInjectionConditionBuilder()
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .WithOperationType(FaultInjectionOperationType.DistributedTransactionReadBatch)
                    .Build(),
                result: FaultInjectionResultBuilder.GetResultBuilder(errorType)
                    .WithTimes(1)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            using CosmosClient fiClient = this.CreateFaultInjectedClient(rule, out _);
            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedReadTransaction()
                .ReadItem(fiContainer, new PartitionKey(doc.pk), doc.id)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(
                rule.GetHitCount() >= 1,
                $"[{errorType}] FaultInjection rule should have been hit at least once. Hit count: {rule.GetHitCount()}.");

            Assert.AreEqual(
                HttpStatusCode.OK,
                response.StatusCode,
                $"[{errorType}] After the injected fault was exhausted, the retried read transaction " +
                $"should return 200 from the real Coordinator. Got: {response.StatusCode} (hit count {rule.GetHitCount()}).");
            Assert.IsTrue(response.Count > 0);
            Assert.AreEqual(
                HttpStatusCode.OK,
                response[0].StatusCode,
                $"[{errorType}] Per-op[0] should be 200 OK. Got: {response[0].StatusCode}.");

            response.Dispose();
        }

        // ─── Multi-attempt: inject N>1 times → SDK keeps retrying → eventual success ─────────

        /// <summary>
        /// WRITE DTx: inject a retriable error for the first THREE attempts. The SDK's inner retry
        /// loop must re-send more than once and still reach the real Coordinator, proving the retry
        /// budget tolerates more than a single transient fault.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_RetriableServerErrorThreeTimes_StillRetriesThenSucceeds()
        {
            string pk = $"fi-write-3x-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            const int injectTimes = 3;
            string ruleId = $"dtx-write-3x-{Guid.NewGuid():N}";
            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition: new FaultInjectionConditionBuilder()
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .WithOperationType(FaultInjectionOperationType.DistributedTransactionWriteBatch)
                    .Build(),
                result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                    .WithTimes(injectTimes)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            using CosmosClient fiClient = this.CreateFaultInjectedClient(rule, out _);
            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "fi-create-3x" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(
                rule.GetHitCount() >= injectTimes,
                $"The rule should have been hit at least {injectTimes} times (the SDK must retry past " +
                $"each transient fault). Hit count: {rule.GetHitCount()}.");
            Assert.IsTrue(
                response.IsSuccessStatusCode,
                $"After {injectTimes} transient faults the retried commit should still succeed. " +
                $"Got: {response.StatusCode} (hit count {rule.GetHitCount()}).");

            response.Dispose();
        }

        // ─── Toggle: a disabled rule must not interfere; enabling it re-arms the fault ──────

        /// <summary>
        /// A rule created in the disabled state must not fault the first commit (clean baseline);
        /// enabling it then arms exactly one fault for the next commit, which is retried-then-served
        /// by the real Coordinator. Proves enable/disable gating works for DTX requests.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_DisabledRule_NoFault_ThenEnabled_FaultsOnceThenSucceeds()
        {
            string ruleId = $"dtx-write-toggle-{Guid.NewGuid():N}";
            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition: new FaultInjectionConditionBuilder()
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .WithOperationType(FaultInjectionOperationType.DistributedTransactionWriteBatch)
                    .Build(),
                result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                    .WithTimes(1)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            rule.Disable();

            using CosmosClient fiClient = this.CreateFaultInjectedClient(rule, out _);
            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);

            // Baseline commit with the rule disabled — no fault, no retry needed.
            string pk1 = $"fi-toggle-off-{Guid.NewGuid():N}";
            string id1 = Guid.NewGuid().ToString();
            DistributedTransactionResponse baseline = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk1), id1, new { id = id1, pk = pk1, value = "baseline" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(0, rule.GetHitCount(), "A disabled rule must not fire.");
            Assert.IsTrue(baseline.IsSuccessStatusCode, $"Baseline commit should succeed. Got: {baseline.StatusCode}.");
            baseline.Dispose();

            // Now arm the rule and commit again — exactly one fault, then real success.
            rule.Enable();
            string pk2 = $"fi-toggle-on-{Guid.NewGuid():N}";
            string id2 = Guid.NewGuid().ToString();
            DistributedTransactionResponse faulted = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk2), id2, new { id = id2, pk = pk2, value = "armed" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(rule.GetHitCount() >= 1, $"An enabled rule should fire. Hit count: {rule.GetHitCount()}.");
            Assert.IsTrue(
                faulted.IsSuccessStatusCode,
                $"After the single injected fault the retried commit should succeed. Got: {faulted.StatusCode}.");
            faulted.Dispose();
        }

        // ─── Synthesized coordinator responses (RetriableCoordinatorResponse injector) ──────
        //
        // Unlike the hybrid tests above (which inject a transport error and let the REAL Coordinator
        // serve the retry), these tests use the RetriableCoordinatorResponse error type to inject a
        // COMPLETE, contract-shaped Coordinator response — envelope status/sub-status + body
        // isRetriable + per-operation results — so each documented DTC outcome can be reproduced
        // deterministically. Terminal scenarios suppress the service request entirely, so they do
        // not depend on a live Coordinator at all; retriable scenarios inject for the first attempt(s)
        // and let the real Coordinator serve the successful retry.
        //
        // Assertions target the FINAL response state (status, IsRetriable, per-op results), which is
        // robust regardless of how the inner transport retry loop interleaves.

        /// <summary>
        /// Terminal non-retriable envelope (400 / DtcRetryOperationsMismatch). The committer must
        /// surface it as-is and NOT retry (isRetriable=false). Service request suppressed.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_Terminal400Mismatch_SurfacedWithoutRetry()
        {
            FaultInjectionDistributedTransactionResponse coordinatorResponse =
                new FaultInjectionDistributedTransactionResponse(
                    statusCode: 400,
                    subStatusCode: 5422,
                    isRetriable: false);

            using CosmosClient fiClient = this.CreateDtcFaultInjectedClient(
                FaultInjectionOperationType.DistributedTransactionWriteBatch,
                coordinatorResponse,
                suppressServiceRequest: true,
                rule: out FaultInjectionRule rule);

            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);
            string pk = $"fi-400-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)400, response.StatusCode, "The terminal 400 envelope must surface unchanged.");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.IsFalse(response.IsRetriable, "A non-retriable coordinator response must not be flagged retriable.");
            Assert.AreEqual(
                1,
                rule.GetHitCount(),
                $"A non-retriable (400) response must be injected exactly once — no inner or outer retry. Hit count: {rule.GetHitCount()}.");
            response.Dispose();
        }

        /// <summary>
        /// Terminal aborted envelope (452 Transaction Aborted, no sub-status) with a per-operation
        /// 453 DtcOperationRolledBack result. The envelope surfaces as 452 and the per-op result as 453.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_Aborted452_WithRolledBackOperation_SurfacedWithoutRetry()
        {
            FaultInjectionDistributedTransactionResponse coordinatorResponse =
                new FaultInjectionDistributedTransactionResponse(
                    statusCode: 452,
                    subStatusCode: 0,
                    isRetriable: false,
                    retryAfter: null,
                    operationResults: new List<FaultInjectionDistributedTransactionOperationResult>
                    {
                        new FaultInjectionDistributedTransactionOperationResult(index: 0, statusCode: 453, subStatusCode: 5415),
                    });

            using CosmosClient fiClient = this.CreateDtcFaultInjectedClient(
                FaultInjectionOperationType.DistributedTransactionWriteBatch,
                coordinatorResponse,
                suppressServiceRequest: true,
                rule: out FaultInjectionRule rule);

            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);
            string pk = $"fi-452-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)452, response.StatusCode, "The aborted 452 envelope must surface unchanged.");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.IsFalse(response.IsRetriable);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual((HttpStatusCode)453, response[0].StatusCode, "The rolled-back operation must surface as 453.");
            Assert.AreEqual(
                1,
                rule.GetHitCount(),
                $"A non-retriable (452) response must be injected exactly once — no retry. Hit count: {rule.GetHitCount()}.");
            response.Dispose();
        }

        /// <summary>
        /// 207 MultiStatus promotion: the envelope is promoted to the first per-operation error code,
        /// excluding 424 FailedDependency. Here ops are [200, 424, 412] → envelope promotes to 412.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_MultiStatus207_PromotesFirstNonDependencyError()
        {
            FaultInjectionDistributedTransactionResponse coordinatorResponse =
                new FaultInjectionDistributedTransactionResponse(
                    statusCode: 207,
                    subStatusCode: 0,
                    isRetriable: false,
                    retryAfter: null,
                    operationResults: new List<FaultInjectionDistributedTransactionOperationResult>
                    {
                        new FaultInjectionDistributedTransactionOperationResult(index: 0, statusCode: 200),
                        new FaultInjectionDistributedTransactionOperationResult(index: 1, statusCode: 424),
                        new FaultInjectionDistributedTransactionOperationResult(index: 2, statusCode: 412),
                    });

            using CosmosClient fiClient = this.CreateDtcFaultInjectedClient(
                FaultInjectionOperationType.DistributedTransactionWriteBatch,
                coordinatorResponse,
                suppressServiceRequest: true,
                rule: out FaultInjectionRule rule);

            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);
            string pk = $"fi-207-{Guid.NewGuid():N}";

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), Guid.NewGuid().ToString(), new { id = "a", pk, value = "v" })
                .CreateItem(fiContainer, new PartitionKey(pk), Guid.NewGuid().ToString(), new { id = "b", pk, value = "v" })
                .CreateItem(fiContainer, new PartitionKey(pk), Guid.NewGuid().ToString(), new { id = "c", pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(
                (HttpStatusCode)412,
                response.StatusCode,
                "The envelope must be promoted to the first non-424 per-operation error (412), not 424.");
            Assert.AreEqual(3, response.Count);
            Assert.AreEqual((HttpStatusCode)200, response[0].StatusCode);
            Assert.AreEqual((HttpStatusCode)424, response[1].StatusCode, "The 424 FailedDependency must still surface per-op.");
            Assert.AreEqual((HttpStatusCode)412, response[2].StatusCode);
            Assert.AreEqual(
                1,
                rule.GetHitCount(),
                $"A 207 multi-status response must be injected exactly once — no retry. Hit count: {rule.GetHitCount()}.");
            response.Dispose();
        }

        /// <summary>
        /// Bodyless inner-loop retry budgets. A coordinator envelope with NO body (Content == null) is
        /// owned by ClientRetryPolicy's inner loop, which retries it up to a fixed budget and then
        /// surfaces it. With the service request suppressed every attempt is re-injected, so the rule
        /// hit count pins the EXACT inner-loop budget: 408 and 449/5352 share MaxDtxRetryCount (10
        /// retries -> 11 hits); 500/5411-5413 use MaxDtxInfraFailureRetryCount (9 retries -> 10 hits).
        /// The surfaced response reports IsRetriable=false because there is no body to carry the flag.
        /// This is the live, exact-count proof that the inner loop owns bodyless retriable envelopes.
        /// </summary>
        [DataTestMethod]
        [Timeout(120000)]
        [DataRow(408, 0, 11, DisplayName = "408/0 stuck - inner loop retries 10x then surfaces (11 hits)")]
        [DataRow(449, 5352, 11, DisplayName = "449/5352 coordinator race - inner loop retries 10x then surfaces (11 hits)")]
        [DataRow(500, 5411, 10, DisplayName = "500/5411 ledger failure - inner loop retries 9x then surfaces (10 hits)")]
        [DataRow(500, 5412, 10, DisplayName = "500/5412 account config failure - inner loop retries 9x then surfaces (10 hits)")]
        [DataRow(500, 5413, 10, DisplayName = "500/5413 dispatch failure - inner loop retries 9x then surfaces (10 hits)")]
        public async Task WriteTransaction_BodylessRetriableEnvelope_InnerLoopExhaustsBudgetThenSurfaces(
            int statusCode,
            int subStatusCode,
            int expectedHits)
        {
            FaultInjectionDistributedTransactionResponse coordinatorResponse =
                new FaultInjectionDistributedTransactionResponse(
                    statusCode: statusCode,
                    subStatusCode: subStatusCode,
                    isRetriable: false,
                    retryAfter: TimeSpan.FromMilliseconds(1), // keep the coordinator-retriable backoff tiny (ignored by the infra-failure branch)
                    operationResults: null,
                    emptyBody: true);

            using CosmosClient fiClient = this.CreateDtcFaultInjectedClient(
                FaultInjectionOperationType.DistributedTransactionWriteBatch,
                coordinatorResponse,
                suppressServiceRequest: true,
                rule: out FaultInjectionRule rule);

            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);
            string pk = $"fi-bodyless-{statusCode}-{subStatusCode}-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(
                (HttpStatusCode)statusCode,
                response.StatusCode,
                $"The bodyless {statusCode}/{subStatusCode} envelope must surface unchanged after the inner loop exhausts its budget.");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.IsFalse(
                response.IsRetriable,
                $"A bodyless {statusCode}/{subStatusCode} response carries no isRetriable flag, so the surfaced response must report IsRetriable=false.");
            Assert.AreEqual(
                expectedHits,
                rule.GetHitCount(),
                $"ClientRetryPolicy must retry the bodyless {statusCode}/{subStatusCode} envelope exactly to its inner-loop budget. Expected {expectedHits} hits, got {rule.GetHitCount()}.");
            response.Dispose();
        }

        /// <summary>
        /// A bodyless 429/3200 (RU budget exceeded) is classified coordinator-retriable by ClientRetryPolicy
        /// and retried via ResourceThrottleRetryPolicy (honoring Retry-After up to the throttle budget). With
        /// the service request suppressed, every attempt re-injects the 429, so the rule is hit more than once
        /// before the throttle budget is exhausted and the 429 finally surfaces. Regression guard for the fix
        /// to issue #5975 (the throttle deferral previously did not re-send on the DTX commit path).
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task WriteTransaction_Bodyless429_ThrottlePolicyRetriesThenSurfaces()
        {
            FaultInjectionDistributedTransactionResponse coordinatorResponse =
                new FaultInjectionDistributedTransactionResponse(
                    statusCode: 429,
                    subStatusCode: 3200, // RUBudgetExceeded
                    isRetriable: false,
                    retryAfter: TimeSpan.FromMilliseconds(1),
                    operationResults: null,
                    emptyBody: true);

            using CosmosClient fiClient = this.CreateDtcFaultInjectedClient(
                FaultInjectionOperationType.DistributedTransactionWriteBatch,
                coordinatorResponse,
                suppressServiceRequest: true,
                rule: out FaultInjectionRule rule);

            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);
            string pk = $"fi-bodyless-429-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);


            Assert.AreEqual(
                (HttpStatusCode)429,
                response.StatusCode,
                "The bodyless 429/3200 surfaces once the throttle budget is exhausted.");
            Assert.IsTrue(
                rule.GetHitCount() >= 2,
                $"429/3200 must be retried by ResourceThrottleRetryPolicy (ClientRetryPolicy defers to it), so the " +
                $"rule must be hit more than once before the 429 surfaces. Hit count: {rule.GetHitCount()}.");
            response.Dispose();
        }

        /// <summary>
        /// A bodyless 452/5421 (aborted) is terminal: neither the inner ClientRetryPolicy nor the outer
        /// committer loop retries it, so it surfaces at exactly one hit.
        /// </summary>
        [TestMethod]
        public async Task WriteTransaction_Bodyless452Aborted_SurfacedWithoutRetry()
        {
            FaultInjectionDistributedTransactionResponse coordinatorResponse =
                new FaultInjectionDistributedTransactionResponse(
                    statusCode: 452,
                    subStatusCode: 5421,
                    isRetriable: false,
                    retryAfter: null,
                    operationResults: null,
                    emptyBody: true);

            using CosmosClient fiClient = this.CreateDtcFaultInjectedClient(
                FaultInjectionOperationType.DistributedTransactionWriteBatch,
                coordinatorResponse,
                suppressServiceRequest: true,
                rule: out FaultInjectionRule rule);

            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);
            string pk = $"fi-bodyless-452-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)452, response.StatusCode, "The aborted 452 envelope must surface unchanged.");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.IsFalse(response.IsRetriable);
            Assert.AreEqual(
                1,
                rule.GetHitCount(),
                $"452/5421 is terminal - it must be injected exactly once with no inner or outer retry. Hit count: {rule.GetHitCount()}.");
            response.Dispose();
        }

        /// <summary>
        /// Bodyless inner-loop retry that reaches the REAL Coordinator: inject a bodyless retriable
        /// envelope for the first attempt only (WithTimes(1), service request NOT suppressed). The inner
        /// ClientRetryPolicy retries, the rule is then exhausted, and the retry flows through to the real
        /// Coordinator and commits - mirroring the hybrid transport-fault "mock then real" pattern.
        /// </summary>
        [DataTestMethod]
        [Timeout(120000)]
        [DataRow(408, 0, DisplayName = "408/0 stuck - bodyless, inner retry reaches real Coordinator")]
        [DataRow(449, 5352, DisplayName = "449/5352 coordinator race - bodyless, inner retry reaches real Coordinator")]
        [DataRow(500, 5411, DisplayName = "500/5411 ledger failure - bodyless, inner retry reaches real Coordinator")]
        public async Task WriteTransaction_BodylessRetriableEnvelope_InnerRetryReachesRealCoordinator(
            int statusCode,
            int subStatusCode)
        {
            FaultInjectionDistributedTransactionResponse coordinatorResponse =
                new FaultInjectionDistributedTransactionResponse(
                    statusCode: statusCode,
                    subStatusCode: subStatusCode,
                    isRetriable: false,
                    retryAfter: TimeSpan.FromMilliseconds(1),
                    operationResults: null,
                    emptyBody: true);

            using CosmosClient fiClient = this.CreateDtcFaultInjectedClient(
                FaultInjectionOperationType.DistributedTransactionWriteBatch,
                coordinatorResponse,
                suppressServiceRequest: false,
                times: 1,
                rule: out FaultInjectionRule rule);

            Container fiContainer = fiClient.GetContainer(DatabaseId, ContainerId);
            string pk = $"fi-bodyless-real-{statusCode}-{subStatusCode}-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            DistributedTransactionResponse response = await fiClient
                .CreateDistributedWriteTransaction()
                .CreateItem(fiContainer, new PartitionKey(pk), id, new { id, pk, value = "v" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(
                rule.GetHitCount() >= 1,
                $"[{statusCode}/{subStatusCode}] The bodyless fault must have fired at least once. Hit count: {rule.GetHitCount()}.");
            Assert.IsTrue(
                response.IsSuccessStatusCode,
                $"[{statusCode}/{subStatusCode}] After the bodyless fault was exhausted, the inner-loop retry should reach the real Coordinator and commit. Got: {response.StatusCode} (hit count {rule.GetHitCount()}).");
            response.Dispose();
        }

        // Note: outer-loop retry-budget EXHAUSTION (an always-retriable response retried up to the
        // attempt/delay cap) is asserted deterministically — with an injected delayProvider and no real
        // sleeping — by DistributedTransactionCommitterTests.CommitTransaction_ExhaustsCumulativeDelayBudget_
        // ReturnsLastResponse and _ExhaustsIsRetriableRetryBudget_ReturnsLastResponse. It is intentionally
        // NOT reproduced here because the live pipeline would sleep the real exponential backoff (tens of
        // seconds) and could not pin an exact attempt count.

        // ─── Helpers ───────────────────────────────────────────────────────────────────────

        // Builds a CosmosClient (endpoint + master key, Gateway/Session) wired with the supplied
        // fault-injection rule so DTX requests are intercepted by the ChaosInterceptor.
        private CosmosClient CreateFaultInjectedClient(FaultInjectionRule rule, out FaultInjector faultInjector)
        {
            faultInjector = new FaultInjector(new List<FaultInjectionRule> { rule });

            CosmosClientOptions options = faultInjector.GetFaultInjectionClientOptions(
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = ConsistencyLevel.Session
                });

            return new CosmosClient(this.endpoint, this.key, options);
        }

        // Builds a fault-injected client whose rule injects a complete, synthesized DTC coordinator
        // response (via RetriableCoordinatorResponse) for the given distributed-transaction operation
        // type. When suppressServiceRequest is true the real Coordinator is never contacted.
        private CosmosClient CreateDtcFaultInjectedClient(
            FaultInjectionOperationType operationType,
            FaultInjectionDistributedTransactionResponse coordinatorResponse,
            bool suppressServiceRequest,
            out FaultInjectionRule rule,
            int times = 0)
        {
            FaultInjectionServerErrorResultBuilder resultBuilder = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.RetriableCoordinatorResponse)
                .WithSuppressServiceRequest(suppressServiceRequest)
                .WithDistributedTransactionResponse(coordinatorResponse);

            if (times > 0)
            {
                resultBuilder = resultBuilder.WithTimes(times);
            }

            rule = new FaultInjectionRuleBuilder(
                id: $"dtc-coord-{operationType}-{Guid.NewGuid():N}",
                condition: new FaultInjectionConditionBuilder()
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .WithOperationType(operationType)
                    .Build(),
                result: resultBuilder.Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            return this.CreateFaultInjectedClient(rule, out _);
        }

        private async Task<ToDoActivity> SeedItemAsync(Container targetContainer)
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            await targetContainer.CreateItemAsync(doc, new PartitionKey(doc.pk));
            return doc;
        }
    }
}
