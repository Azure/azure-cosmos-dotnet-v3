//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using CosmosSystemTextJsonSerializer = Utils.TestCommon.CosmosSystemTextJsonSerializer;

    /// <summary>
    /// Investigation repro for the "AddressRefresh forceRefresh after partition migration" work item.
    ///
    /// Background: two live-site incidents were traced to a customer app continuing to route to OLD
    /// partitions after a partition migration completed. AddressRefresh calls were observed in the app
    /// diagnostics, but the suspicion (Fabian Meiswinkel) is that those refreshes were issued with
    /// forceRefresh = false, because — long after the migration — the backend returned a generic 410
    /// (Gone) without a migration substatus (1008) rather than 410/1008. The Gateway only bypasses its
    /// own address cache when the SDK sends the x-ms-force-refresh header, so if the SDK does not force
    /// a refresh on the generic 410 the client stays pinned to stale addresses.
    ///
    /// This test reproduces a MATRIX of 410 substatuses on a Direct-mode ReadItem and records, per
    /// substatus, whether the SDK issued a FORCED address refresh (observable in CosmosDiagnostics as a
    /// "ForceAddressRefresh" block). The output is a verdict table mapping each substatus to H1
    /// (SDK does not force on a generic 410).
    ///
    /// IMPORTANT — scope and limitations (see the work-item public spec, section 6):
    ///   * This rig runs against a HEALTHY account. Fault injection makes the SDK observe a 410, but the
    ///     cached addresses (SDK and Gateway) are valid the whole time, so the operation recovers on the
    ///     post-injection attempt REGARDLESS of forceRefresh. "Recovery" is therefore NOT a force-refresh
    ///     signal and is only recorded for context.
    ///   * Consequently this test can answer H1 only (does the SDK ISSUE a forced refresh on a given 410
    ///     substatus?). It cannot decide H2 (Gateway returned stale addresses despite force-refresh) or
    ///     H3 (stale routing-map recovery) — those require the real-incident server-side diagnostics or a
    ///     controlled real migration.
    ///   * The decisive force-refresh decision lives in the closed-source Microsoft.Azure.Cosmos.Direct
    ///     binary, so the verdict is valid only for the Direct version this test runs against. That
    ///     version is recorded in the output (see SE-6).
    ///
    /// Requires a live multi-region account via the COSMOSDB_MULTI_REGION environment variable; it cannot
    /// run on the emulator. It is intentionally not part of the default CI gate.
    /// </summary>
    [TestClass]
    public class AddressRefreshForceRefreshPostMigrationTests
    {
        private const int Timeout = 120000;

        private const string ForceAddressRefreshMarker = "ForceAddressRefresh";
        private const string NoChangeToCacheMarker = "No change to cache";
        private const string AddressResolutionMarker = "AddressResolutionStatistics";

        private string connectionString;
        private CosmosSystemTextJsonSerializer serializer;

        private CosmosClient client;
        private Database database;
        private Container container;

        private CosmosClient fiClient;

        public TestContext TestContext { get; set; }

        /// <summary>
        /// One row of the substatus matrix. <see cref="Category"/> groups the substatus so the verdict can
        /// contrast the generic-410 cases (the customer scenario) against the migration baselines.
        /// </summary>
        private sealed class SubStatusCase
        {
            public SubStatusCase(int subStatusCode, string label, string category)
            {
                this.SubStatusCode = subStatusCode;
                this.Label = label;
                this.Category = category;
            }

            public int SubStatusCode { get; }
            public string Label { get; }
            public string Category { get; }
        }

        private sealed class CaseResult
        {
            public SubStatusCase Case { get; set; }
            public long HitCount { get; set; }
            public bool ForcedAddressRefreshObserved { get; set; }
            public bool NoChangeToCacheObserved { get; set; }
            public bool AddressResolutionObserved { get; set; }
            public bool Recovered { get; set; }
            public string Notes { get; set; }
        }

        [TestInitialize]
        public async Task Initialize()
        {
            // Tests use a live account with multi-region enabled.
            this.connectionString = TestCommon.GetConnectionString();

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Inconclusive("Set environment variable COSMOSDB_MULTI_REGION to run the AddressRefresh force-refresh repro.");
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            this.serializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                Serializer = this.serializer,
            };

            this.client = new CosmosClient(this.connectionString, cosmosClientOptions);
            (this.database, this.container) = await TestCommon.GetOrCreateMultiRegionFIDatabaseAndContainersAsync(this.client);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.client?.Dispose();
            this.fiClient?.Dispose();
        }

        /// <summary>
        /// Injects a 410 (Gone) on a Direct-mode ReadItem for each substatus in the matrix and records
        /// whether the SDK issued a forced address refresh. Emits a verdict table contrasting the
        /// generic-410 cases (0, 21005) against the migration baselines (1007, 1008) and PartitionKeyRangeGone (1002).
        /// </summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Repro: does the SDK force an address refresh on a generic 410 after migration? (H1)")]
        public async Task AddressRefresh_ForceRefreshOnGone_SubStatusMatrix()
        {
            // The customer scenario surfaces as a generic 410 (0 / 21005 ServerGenerated410). The migration
            // substatuses (1007 CompletingSplit, 1008 CompletingPartitionMigration) and 1002
            // PartitionKeyRangeGone are the contrast baselines that are expected to force a refresh today.
            List<SubStatusCase> matrix = new List<SubStatusCase>
            {
                new SubStatusCase(0, "Generic 410 / SubStatus 0", "generic"),
                new SubStatusCase(21005, "ServerGenerated410 / SubStatus 21005", "generic"),
                new SubStatusCase(1007, "CompletingSplit / SubStatus 1007", "migration"),
                new SubStatusCase(1008, "CompletingPartitionMigration / SubStatus 1008", "migration"),
                new SubStatusCase(1002, "PartitionKeyRangeGone / SubStatus 1002", "pkrange-gone"),
            };

            List<CaseResult> results = new List<CaseResult>();
            foreach (SubStatusCase substatusCase in matrix)
            {
                results.Add(await this.RunSingleSubStatusCaseAsync(substatusCase));
            }

            string directVersion = typeof(Microsoft.Azure.Documents.StoreResponse).Assembly.GetName().Version?.ToString() ?? "unknown";
            string report = this.BuildReport(results, directVersion);

            this.TestContext.WriteLine(report);

            // Invariant assertions only — the H1 verdict itself is REPORTED, not asserted, because the
            // "expected" behavior on a generic 410 is exactly what this investigation is determining.
            foreach (CaseResult result in results)
            {
                Assert.IsTrue(
                    result.HitCount >= 1,
                    $"Fault was not injected for {result.Case.Label} (hit count {result.HitCount}); the matrix row did not exercise the intended path.");
            }
        }

        private async Task<CaseResult> RunSingleSubStatusCaseAsync(SubStatusCase substatusCase)
        {
            string id = "addrRefreshTestId-" + Guid.NewGuid().ToString();
            string pk = "addrRefreshTestPk-" + Guid.NewGuid().ToString();

            TestCommon.FaultInjectionTestObject createdItem = new TestCommon.FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            // Seed the item and warm the address cache with a non-fault-injected client so the subsequent
            // injected 410 exercises the Gone retry / address-refresh path rather than a cold lookup.
            await this.container.CreateItemAsync(createdItem);
            await this.container.ReadItemAsync<TestCommon.FaultInjectionTestObject>(id, new PartitionKey(pk));

            string ruleId = "addrRefreshGoneRule-" + substatusCase.SubStatusCode + "-" + Guid.NewGuid().ToString();
            FaultInjectionRule goneRule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithConnectionType(FaultInjectionConnectionType.Direct)
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    new FaultInjectionCustomServerErrorResultBuilder(
                        statusCode: (int)System.Net.HttpStatusCode.Gone,
                        subStatusCode: substatusCase.SubStatusCode)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(2))
                .Build();

            goneRule.Disable();

            CaseResult result = new CaseResult { Case = substatusCase };

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { goneRule });
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer,
                };

                this.fiClient = new CosmosClient(this.connectionString, cosmosClientOptions);
                Container fiContainer = this.fiClient
                    .GetDatabase(TestCommon.FaultInjectionDatabaseName)
                    .GetContainer(TestCommon.FaultInjectionContainerName);

                // Warm the address cache on the fault-injection client too, so the injected 410 below is the
                // first event that could trigger a FORCED refresh (and not a cold-cache population).
                await fiContainer.ReadItemAsync<TestCommon.FaultInjectionTestObject>(id, new PartitionKey(pk));

                goneRule.Enable();

                string diagnostics;
                try
                {
                    ItemResponse<TestCommon.FaultInjectionTestObject> response =
                        await fiContainer.ReadItemAsync<TestCommon.FaultInjectionTestObject>(id, new PartitionKey(pk));
                    diagnostics = response.Diagnostics.ToString();
                    result.Recovered = (int)response.StatusCode < 400;
                }
                catch (CosmosException ex)
                {
                    // The generic-410 case may surface to the caller if the SDK does not transparently
                    // recover; capture its diagnostics too.
                    diagnostics = ex.Diagnostics?.ToString() ?? string.Empty;
                    result.Recovered = false;
                    result.Notes = $"Surfaced CosmosException {(int)ex.StatusCode}/{ex.SubStatusCode}.";
                }

                result.HitCount = goneRule.GetHitCount();
                result.AddressResolutionObserved = diagnostics.Contains(AddressResolutionMarker, StringComparison.Ordinal);
                result.ForcedAddressRefreshObserved = diagnostics.Contains(ForceAddressRefreshMarker, StringComparison.Ordinal);
                result.NoChangeToCacheObserved = diagnostics.Contains(NoChangeToCacheMarker, StringComparison.Ordinal);
            }
            finally
            {
                goneRule.Disable();
                this.fiClient?.Dispose();
                this.fiClient = null;
                try
                {
                    await this.container.DeleteItemAsync<TestCommon.FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // best effort cleanup
                }
            }

            return result;
        }

        private string BuildReport(List<CaseResult> results, string directVersion)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== AddressRefresh force-refresh substatus matrix (H1 repro) ===");
            sb.AppendLine($"Microsoft.Azure.Cosmos.Direct version under test: {directVersion}");
            sb.AppendLine("Signal = a 'ForceAddressRefresh' block in CosmosDiagnostics implies the SDK issued a forced");
            sb.AppendLine("address refresh tied to the injected 410. 'Recovered' is context only (healthy account).");
            sb.AppendLine();
            sb.AppendLine("| SubStatus | Category | HitCount | ForcedRefresh | NoChangeToCache | AddrResolution | Recovered | Notes |");
            sb.AppendLine("|-----------|----------|----------|---------------|-----------------|----------------|-----------|-------|");
            foreach (CaseResult r in results)
            {
                sb.AppendLine($"| {r.Case.SubStatusCode} ({r.Case.Label}) | {r.Case.Category} | {r.HitCount} | {r.ForcedAddressRefreshObserved} | {r.NoChangeToCacheObserved} | {r.AddressResolutionObserved} | {r.Recovered} | {r.Notes} |");
            }

            sb.AppendLine();
            sb.AppendLine(this.BuildVerdict(results));
            return sb.ToString();
        }

        /// <summary>
        /// Produces a heuristic H1 verdict by contrasting the generic-410 rows against the migration rows.
        /// </summary>
        private string BuildVerdict(List<CaseResult> results)
        {
            bool anyGenericForced = false;
            bool allGenericNotForced = true;
            bool anyMigrationForced = false;
            foreach (CaseResult r in results)
            {
                if (r.Case.Category == "generic")
                {
                    anyGenericForced |= r.ForcedAddressRefreshObserved;
                    allGenericNotForced &= !r.ForcedAddressRefreshObserved;
                }
                else if (r.Case.Category == "migration")
                {
                    anyMigrationForced |= r.ForcedAddressRefreshObserved;
                }
            }

            if (allGenericNotForced && anyMigrationForced)
            {
                return "VERDICT (H1 SUPPORTED): the generic 410 cases did NOT trigger a forced address refresh, " +
                       "while the migration substatuses did. This matches the reported gap — a generic post-migration " +
                       "410 leaves the SDK serving cached (stale) addresses. Route a fix to the Direct binary / Gateway owners.";
            }

            if (anyGenericForced)
            {
                return "VERDICT (H1 NOT SUPPORTED for the forced cases): at least one generic 410 case DID trigger a forced " +
                       "address refresh. If customers still hit old partitions, the gap is more likely H2 (Gateway returned " +
                       "stale addresses despite force-refresh — check the 'No change to cache' column) or H3 (stale routing map). " +
                       "Those require the real-incident server-side diagnostics; this FaultInjection rig cannot decide them.";
            }

            return "VERDICT (INCONCLUSIVE): no forced refresh was observed for any case, including the migration baselines. " +
                   "Re-check the matrix wiring (was the 410 actually injected on the data path?) before drawing conclusions.";
        }
    }
}
