//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    /// (Gone) without a recognized substatus rather than 410/1008. The Gateway only bypasses its own
    /// address cache when the SDK sends the x-ms-force-refresh header, so if the SDK does not force a
    /// refresh on the generic 410 the client stays pinned to stale addresses.
    ///
    /// Two DIFFERENT recovery axes are involved:
    ///   * ADDRESSES — a physical partition migration can move the replicas backing an EXISTING partition
    ///     key range. The range identity stays valid and only its addresses go stale, so re-resolving the
    ///     addresses is sufficient.
    ///   * RANGE TOPOLOGY — a split or a merge changes WHICH partition key ranges exist, so the collection
    ///     routing map has to be re-resolved before the addresses of the replacement range can be resolved
    ///     at all. This repo exposes no distinct merge-completion substatus.
    ///   * A generic 410/0 does not say which of the two occurred, which is exactly why both axes are worth
    ///     investigating. It does not by itself establish which refresh policy Direct should apply.
    ///
    /// The client-side refresh branch runs when either <c>forceRefreshPartitionAddresses</c> OR
    /// <see cref="DocumentServiceRequest.ForceCollectionRoutingMapRefresh"/> is set. After that branch
    /// updates the cache it emits a "ForceAddressRefresh" diagnostics block. The marker therefore proves
    /// that the shared branch ran; it does NOT identify which flag drove it or prove that the
    /// <c>x-ms-force-refresh</c> header was sent.
    ///
    /// This test reproduces a MATRIX of 410 substatuses on a Direct-mode ReadItem and records, per
    /// substatus, whether CosmosDiagnostics contains that "ForceAddressRefresh" branch marker. The observed
    /// marker presence is PINNED as a baseline assertion, so this rig also serves as a regression guard.
    ///
    /// H1 OUTCOME on the pinned Microsoft.Azure.Cosmos.Direct 3.43.1: UNRESOLVED. Both generic rows (0 and
    /// 21005) emitted the shared branch marker. Among the three recognized substatus rows, only 1008
    /// (CompletingPartitionMigration) emitted it; 1007 (CompletingSplit) and 1002
    /// (PartitionKeyRangeGone) did not. Because either refresh flag can produce the marker, the matrix does
    /// not establish whether Direct requested an address-force header, a collection routing-map refresh,
    /// or both.
    ///
    /// IMPORTANT — scope and limitations (see the work-item public spec, section 6):
    ///   * This rig runs against a HEALTHY account. Fault injection makes the SDK observe a 410, but the
    ///     cached addresses (SDK and Gateway) are valid the whole time, so the operation recovers on the
    ///     post-injection attempt REGARDLESS of refresh flags. "Recovery" is therefore NOT a refresh-policy
    ///     signal: the Recovered and Notes columns are REPORTED for context and are never asserted. Only
    ///     the ForceAddressRefresh-marker column is pinned.
    ///   * No real split, merge, physical migration, or range removal is exercised. The 1002 row names the
    ///     range-no-longer-exists condition by substatus SEMANTICS only; its injected response is not
    ///     evidence that a range was actually gone.
    ///   * Every marker column is an operation-level SUBSTRING observation over the whole diagnostics
    ///     string, not a per-attempt measurement, so a marker cannot be attributed to a specific retry.
    ///   * The "No change to cache" marker appears only when a refresh-branch trace record reports identical
    ///     addresses. A false value is ambiguous on its own: no record was emitted, or one was emitted with
    ///     different addresses (reported as "Original" / "New"). Read it together with the
    ///     ForceAddressRefresh-marker column.
    ///   * Consequently this test cannot decide H1 (which refresh flag Direct set), H2 (whether the Gateway
    ///     returned stale addresses despite a forced address refresh), or H3 (stale routing-map recovery).
    ///     Those require outbound-header telemetry, Direct-side evidence, or a controlled real migration.
    ///   * The decisive refresh-policy logic lives in the closed-source Microsoft.Azure.Cosmos.Direct
    ///     binary, so the marker baseline is valid only for the Direct version this test runs against.
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

        // Shared category strings so the matrix rows, the report table and the verdict text cannot drift
        // apart. Every non-generic category is a RECOGNIZED substatus; they are deliberately kept
        // distinct because they describe different conditions and do not behave alike.
        private const string GenericCategory = "generic";
        private const string SplitInProgressCategory = "split-in-progress";
        private const string PhysicalMigrationCategory = "physical-migration";
        private const string PartitionKeyRangeGoneCategory = "pkrange-gone";

        private string connectionString;
        private CosmosSystemTextJsonSerializer serializer;

        private CosmosClient client;
        private Database database;
        private Container container;

        private CosmosClient fiClient;

        public TestContext TestContext { get; set; }

        /// <summary>
        /// One row of the substatus matrix. <see cref="Category"/> groups the substatus so the verdict can
        /// contrast the generic-410 cases (the customer scenario) against the recognized
        /// split / physical-migration / range-gone substatus baselines.
        /// </summary>
        private sealed class SubStatusCase
        {
            public SubStatusCase(int subStatusCode, string label, string category, bool expectedForceAddressRefreshMarker)
            {
                this.SubStatusCode = subStatusCode;
                this.Label = label;
                this.Category = category;
                this.ExpectedForceAddressRefreshMarker = expectedForceAddressRefreshMarker;
            }

            public int SubStatusCode { get; }
            public string Label { get; }
            public string Category { get; }

            /// <summary>
            /// Behavior observed against Microsoft.Azure.Cosmos.Direct 3.43.1 and pinned here so that a
            /// change in the closed-source refresh-branch behavior is caught by this test rather than
            /// silently changing the reported table. If this assertion fails, Direct's behavior moved —
            /// re-run the investigation and update the baseline deliberately.
            /// </summary>
            public bool ExpectedForceAddressRefreshMarker { get; }
        }

        private sealed class CaseResult
        {
            public SubStatusCase Case { get; set; }
            public long HitCount { get; set; }
            public bool ForceAddressRefreshMarkerObserved { get; set; }
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
        /// whether diagnostics contain the shared refresh-branch marker. Emits a verdict table contrasting the
        /// generic-410 rows (0, 21005) against the three recognized substatus rows, which are reported
        /// separately because they describe different conditions: 1007 CompletingSplit
        /// (<c>split-in-progress</c>, range topology changes), 1008 CompletingPartitionMigration
        /// (<c>physical-migration</c>, addresses move while the range survives) and 1002
        /// PartitionKeyRangeGone (<c>pkrange-gone</c>, injected for its substatus semantics only). Only
        /// the ForceAddressRefresh-marker column is asserted; Recovered and Notes are reported for context.
        /// </summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Repro: which 410s emit the shared address-refresh branch marker?")]
        public async Task AddressRefresh_RefreshBranchMarkerOnGone_SubStatusMatrix()
        {
            // The customer scenario surfaces as a generic 410 (0 / 21005 ServerGenerated410), which does not
            // say whether a physical migration or a range topology change occurred. The three recognized
            // substatus rows are the contrast baselines and they do NOT behave alike: against the pinned
            // Direct 3.43.1, only 1008 (CompletingPartitionMigration — replica addresses move while the
            // range itself survives) emitted the marker. 1007 (CompletingSplit — range topology
            // changes) and 1002 (PartitionKeyRangeGone — injected for its substatus semantics; no range was
            // actually removed in this healthy-account run) did NOT. The two generic rows emitted the marker.
            //
            // The final constructor argument pins presence of the "ForceAddressRefresh" diagnostics marker
            // observed against Direct 3.43.1. The marker comes from a shared branch entered by either refresh
            // flag, so it must not be interpreted as proof that x-ms-force-refresh was sent.
            List<SubStatusCase> matrix = new List<SubStatusCase>
            {
                new SubStatusCase(0, "Generic 410 / SubStatus 0", GenericCategory, expectedForceAddressRefreshMarker: true),
                new SubStatusCase(21005, "ServerGenerated410 / SubStatus 21005", GenericCategory, expectedForceAddressRefreshMarker: true),
                new SubStatusCase(1007, "CompletingSplit / SubStatus 1007", SplitInProgressCategory, expectedForceAddressRefreshMarker: false),
                new SubStatusCase(1008, "CompletingPartitionMigration / SubStatus 1008", PhysicalMigrationCategory, expectedForceAddressRefreshMarker: true),
                new SubStatusCase(1002, "PartitionKeyRangeGone / SubStatus 1002", PartitionKeyRangeGoneCategory, expectedForceAddressRefreshMarker: false),
            };

            List<CaseResult> results = new List<CaseResult>();
            foreach (SubStatusCase substatusCase in matrix)
            {
                results.Add(await this.RunSingleSubStatusCaseAsync(substatusCase));
            }

            string directVersion = typeof(Microsoft.Azure.Documents.StoreResponse).Assembly.GetName().Version?.ToString() ?? "unknown";
            string report = this.BuildReport(results, directVersion);

            this.TestContext.WriteLine(report);

            // Invariant assertions: every row must actually have exercised the injected path.
            foreach (CaseResult result in results)
            {
                Assert.IsTrue(
                    result.HitCount >= 1,
                    $"Fault was not injected for {result.Case.Label} (hit count {result.HitCount}); the matrix row did not exercise the intended path.");
            }

            // Regression assertions: pin the diagnostics-marker presence observed against Direct 3.43.1.
            // H1 remains unresolved because the marker does not identify which refresh flag drove the shared
            // branch. Update the baseline deliberately after re-running the investigation if this fails.
            List<string> drifted = results
                .Where(result => result.ForceAddressRefreshMarkerObserved != result.Case.ExpectedForceAddressRefreshMarker)
                .Select(result => $"  {result.Case.Label}: expected ForceAddressRefresh marker={result.Case.ExpectedForceAddressRefreshMarker}, observed={result.ForceAddressRefreshMarkerObserved}")
                .ToList();

            Assert.AreEqual(
                0,
                drifted.Count,
                $"Direct refresh-branch marker behavior drifted from the pinned {directVersion} baseline:{Environment.NewLine}{string.Join(Environment.NewLine, drifted)}{Environment.NewLine}{report}");
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
                result.ForceAddressRefreshMarkerObserved = diagnostics.Contains(ForceAddressRefreshMarker, StringComparison.Ordinal);
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
            sb.AppendLine("=== Address refresh-branch diagnostics matrix (H1 investigation) ===");
            sb.AppendLine($"Microsoft.Azure.Cosmos.Direct version under test: {directVersion}");
            sb.AppendLine("Signal = a 'ForceAddressRefresh' block in CosmosDiagnostics proves the shared client refresh");
            sb.AppendLine("branch ran. Either refresh flag can enter that branch, so the marker does NOT prove that the");
            sb.AppendLine("x-ms-force-refresh header was sent. Every marker column is an operation-level substring");
            sb.AppendLine("observation over the whole diagnostics string, not a per-attempt measurement. 'NoChangeToCache'");
            sb.AppendLine("appears only when a refresh record reports identical addresses. False is ambiguous alone:");
            sb.AppendLine("no record was emitted, or one reported different addresses ('Original' / 'New'). Read it with");
            sb.AppendLine("'RefreshBranchMarker'. 'Recovered' and 'Notes' are reported for context");
            sb.AppendLine("only (healthy account; no real split, physical migration or range removal) and are never asserted.");
            sb.AppendLine();
            sb.AppendLine("| SubStatus | Category | HitCount | RefreshBranchMarker | NoChangeToCache | AddrResolution | Recovered | Notes |");
            sb.AppendLine("|-----------|----------|----------|---------------------|-----------------|----------------|-----------|-------|");
            foreach (CaseResult r in results)
            {
                sb.AppendLine($"| {r.Case.SubStatusCode} ({r.Case.Label}) | {r.Case.Category} | {r.HitCount} | {r.ForceAddressRefreshMarkerObserved} | {r.NoChangeToCacheObserved} | {r.AddressResolutionObserved} | {r.Recovered} | {r.Notes} |");
            }

            sb.AppendLine();
            sb.AppendLine(this.BuildVerdict(results));
            return sb.ToString();
        }

        /// <summary>
        /// Produces an H1 interpretation by contrasting the generic-410 rows against the recognized
        /// split / physical-migration / range-gone substatus rows. It reads only diagnostics-marker
        /// presence and produces report text; it is not assertion-bearing.
        /// </summary>
        private string BuildVerdict(List<CaseResult> results)
        {
            bool anyGenericMarkerObserved = false;
            bool allGenericMarkersAbsent = true;
            bool anyRecognizedSubStatusMarkerObserved = false;
            foreach (CaseResult r in results)
            {
                if (r.Case.Category == GenericCategory)
                {
                    anyGenericMarkerObserved |= r.ForceAddressRefreshMarkerObserved;
                    allGenericMarkersAbsent &= !r.ForceAddressRefreshMarkerObserved;
                }
                else
                {
                    // Every non-generic row is a recognized substatus (split-in-progress,
                    // physical-migration, pkrange-gone). Testing for "not generic" rather than enumerating
                    // categories keeps a newly added category from being silently dropped here.
                    anyRecognizedSubStatusMarkerObserved |= r.ForceAddressRefreshMarkerObserved;
                }
            }

            if (allGenericMarkersAbsent && anyRecognizedSubStatusMarkerObserved)
            {
                return "VERDICT (H1 UNRESOLVED): the generic 410 cases did not emit the shared refresh-branch marker, " +
                       "while at least one recognized substatus (split-in-progress / physical-migration / pkrange-gone) " +
                       "did. This is consistent with a generic-410 refresh gap, but marker absence does not directly " +
                       "observe either service header and the concurrent-update guard can suppress the record. Inspect " +
                       "the Direct decision or capture outbound address-feed headers.";
            }

            if (anyGenericMarkerObserved)
            {
                return "VERDICT (H1 UNRESOLVED): at least one generic 410 case emitted the ForceAddressRefresh marker, " +
                       "which proves that the shared refresh branch ran. That branch can be entered by an address-force " +
                       "request or by ForceCollectionRoutingMapRefresh, so the marker does not establish that " +
                       "x-ms-force-refresh was sent or resolve the routing-map axis. Inspect the Direct decision or " +
                       "capture outbound address-feed headers.";
            }

            return "VERDICT (INCONCLUSIVE): no refresh-branch marker was observed for any case, including the recognized " +
                   "split-in-progress / physical-migration / pkrange-gone baselines. Re-check the matrix wiring (was the " +
                   "410 actually injected on the data path?) before drawing conclusions.";
        }
    }
}
