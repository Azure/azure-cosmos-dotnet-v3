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
    /// Remove the [Ignore] attribute before running. The class is gated because the public emulator
    /// does not implement /operations/dtc, and because a live DTX-enabled Coordinator is required to
    /// observe the retry-then-real-success behaviour.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [Ignore("DTX endpoint not yet available in emulator. Remove to run locally with env vars.")]
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
        // Note: the Gateway fault-injection path does not support RetryWith (449) — only the
        // Direct-mode path emits it — so it is intentionally excluded from these Gateway DTX rows.
        [DataTestMethod]
        [DataRow(FaultInjectionServerErrorType.Gone)]
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
        [DataRow(FaultInjectionServerErrorType.Gone)]
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

        private async Task<ToDoActivity> SeedItemAsync(Container targetContainer)
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            await targetContainer.CreateItemAsync(doc, new PartitionKey(doc.pk));
            return doc;
        }
    }
}
