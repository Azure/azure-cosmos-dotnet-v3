//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils.TestCommon;
    using ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel;
    using Database = Microsoft.Azure.Cosmos.Database;
    using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
    using Trace = Microsoft.Azure.Cosmos.Tracing.Trace;

    /// <summary>
    /// REAL end-to-end fault-injection validation harness for the SIMPLIFIED metadata hedging
    /// PR #5999 (v2 of issue #5917). NOT a CI test — gated behind RUN_HEDGE_FI=1 and a live
    /// multi-region account.
    ///
    /// Same methodology as the original PR #5923 harness — inject a real transport-layer delay
    /// on the hub region's Gateway metadata ops and drive REAL SDK reads (cold-start
    /// ReadItemAsync, forced PartitionKeyRange refreshes) so the strategy is exercised through its
    /// real call sites in ClientCollectionCache / PartitionKeyRangeCache — adapted to the v2 API:
    ///
    ///   * v2 has NO per-client concurrency budget (the SemaphoreSlim(8) was removed). So under a
    ///     saturating / fast-fail brownout EVERY eligible slow read hedges — the fan-out is bounded
    ///     by the slow-read rate + one-hedge-per-op, NOT capped at a budget. This harness measures
    ///     that honestly (there is no "budget-exhausted" fallback to report).
    ///   * v2 has NO OpenTelemetry meter. Hedge counts come from the PR's own per-request trace
    ///     datum ("Metadata Hedge" → HedgeFired / HedgeWon / WinningRegion), captured by passing a
    ///     real root Trace into the forced refresh (for cold start, parsed from ItemResponse
    ///     diagnostics).
    ///   * The v2 correctness invariant — "the primary is authoritative" — is observed via the
    ///     winning-region attribution: a hedge only WINS from a secondary when the hub is genuinely
    ///     degraded; a healthy (fast) hub always wins, and the hedge never overrides it.
    ///
    /// A/B compares ON (AZURE_COSMOS_METADATA_HEDGING_ENABLED=true) vs OFF (=false) with the SAME
    /// fault injection + workload. Test/docs-only; modifies no Microsoft.Azure.Cosmos/src/** code.
    /// </summary>
    [TestClass]
    [TestCategory("StressHarness")]
    public sealed class MetadataHedgingV2FaultInjectionHarness
    {
        private const string MetadataHedgingEnvVar = "AZURE_COSMOS_METADATA_HEDGING_ENABLED";

        private static readonly string[] PreferredRegions = new[]
        {
            "West US 2", "East US", "South Central US", "Central US", "North Central US",
        };

        private static string OutDir =>
            Environment.GetEnvironmentVariable("HEDGE_FI_OUTDIR")
            ?? Path.Combine(Path.GetTempPath(), "hedge-fi-v2");

        private string connectionString;
        private string hubRegion;
        private TestCommon.CosmosSystemTextJsonSerializer serializer;

        [TestMethod]
        [Owner("nalutripician")]
        public async Task RunV2FaultInjectionHedgingAsync()
        {
            if (Environment.GetEnvironmentVariable("RUN_HEDGE_FI") != "1")
            {
                Assert.Inconclusive("Set RUN_HEDGE_FI=1 (and COSMOSDB_MULTI_REGION) to run the v2 metadata-hedging FI harness.");
                return;
            }

            this.connectionString = TestCommon.GetConnectionString();
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Inconclusive("COSMOSDB_MULTI_REGION is required for the v2 metadata-hedging FI harness.");
                return;
            }

            Directory.CreateDirectory(OutDir);

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            this.serializer = new TestCommon.CosmosSystemTextJsonSerializer(jsonOptions);

            string mode = (Environment.GetEnvironmentVariable("HEDGE_FI_MODE") ?? "compressed").ToLowerInvariant();
            (TimeSpan hubDelay, int coldClients, int lowRefresh, int satRounds, int containerCount, int mixedOps) cfg = mode switch
            {
                "probe" => (TimeSpan.FromSeconds(3), 3, 6, 2, 10, 12),
                "realtime" => (TimeSpan.FromSeconds(8), 4, 10, 2, 12, 16),
                _ => (TimeSpan.FromSeconds(3), 8, 30, 4, 12, 60),
            };

            Console.WriteLine($"[v2-fi-harness] mode={mode} hubDelay={cfg.hubDelay.TotalSeconds}s outDir={OutDir}");

            using (CosmosClient setupClient = this.NewClient(faultInjector: null, hedgingOptIn: null))
            {
                await TestCommon.GetOrCreateMultiRegionFIDatabaseAndContainersAsync(setupClient);
                await this.EnsureExtraContainersAsync(setupClient, cfg.containerCount);
                Uri writeEndpoint = setupClient.DocumentClient.GlobalEndpointManager.WriteEndpoints.First();
                this.hubRegion = setupClient.DocumentClient.GlobalEndpointManager.GetLocation(writeEndpoint);
                IReadOnlyList<Uri> reads = setupClient.DocumentClient.GlobalEndpointManager.ReadEndpoints;
                Console.WriteLine($"[v2-fi-harness] hub(write) region = {this.hubRegion}; {reads.Count} read regions:");
                foreach (Uri u in reads)
                {
                    Console.WriteLine($"    {setupClient.DocumentClient.GlobalEndpointManager.GetLocation(u),-18} {u}");
                }
                Assert.IsTrue(reads.Count >= 2, "Need >=2 read regions for hedging.");
            }

            List<ScenarioRow> rows = new List<ScenarioRow>();
            ConcurrentBag<OpRow> opRows = new ConcurrentBag<OpRow>();
            ConcurrentDictionary<string, long> winRegions = new ConcurrentDictionary<string, long>();

            foreach (bool on in new[] { false, true })
            {
                string arm = on ? "PR(ON)" : "main(OFF)";
                Console.WriteLine($"\n========== ARM {arm} ==========");

                // 1) COLD START end-to-end (fresh client, first ReadItemAsync populates both caches).
                await this.RunColdStartAsync(arm, on, cfg.hubDelay, cfg.coldClients, rows, opRows);

                // 2) STEADY-STATE refresh — LOW contention (sequential, distinct non-coalesced reads).
                await this.RunRefreshLowAsync(arm, on, cfg.hubDelay, cfg.lowRefresh, rows, opRows, winRegions);

                // 3) STEADY-STATE refresh — SATURATING storm (many distinct containers concurrent).
                //    v2 has no budget, so EVERY slow read hedges (fan-out == concurrent slow reads).
                await this.RunRefreshSaturatingAsync(arm, on, cfg.hubDelay, cfg.satRounds, cfg.containerCount, rows, opRows, winRegions);

                // 4) MIXED end-to-end (70% warm reads + 30% refresh) — honest end-to-end framing.
                await this.RunMixedWorkloadAsync(arm, on, cfg.hubDelay, cfg.mixedOps, concurrency: 20, rows, opRows);

                // 5) PRIMARY-BROWNOUT FAST-FAIL soak (hub 503, no delay → every primary fast-fails and
                //    hedges immediately). v2: no budget → every op hedges; fan-out == wave width.
                await this.RunFastFailBrownoutAsync(arm, on, cfg.satRounds, cfg.containerCount, rows, opRows, winRegions);
            }

            WriteScenarioCsv(rows);
            WriteOpCsv(opRows);
            WriteWinRegionCsv(winRegions);
            PrintSummary(rows);
            Console.WriteLine($"[v2-fi-harness] DONE. CSVs in {OutDir}");
        }

        // ---------------------------------------------------------------------------------

        private async Task RunColdStartAsync(string arm, bool on, TimeSpan hubDelay, int samples,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows)
        {
            List<double> lat = new List<double>();
            long hedged = 0;
            for (int i = 0; i < samples; i++)
            {
                FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
                FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
                FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

                using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
                Container container = client.GetContainer(TestCommon.FaultInjectionDatabaseName, TestCommon.FaultInjectionContainerName);

                c.Enable();
                p.Enable();

                Stopwatch sw = Stopwatch.StartNew();
                bool opHedged = false;
                try
                {
                    ItemResponse<FaultInjectionTestObject> resp =
                        await container.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
                    opHedged = DiagnosticsShowHedge(resp.Diagnostics);
                }
                catch (CosmosException ex)
                {
                    Console.WriteLine($"  [cold {arm}] sample {i} non-fatal: {ex.StatusCode}");
                }
                sw.Stop();
                lat.Add(sw.Elapsed.TotalMilliseconds);
                if (opHedged)
                {
                    hedged++;
                }
                opRows.Add(new OpRow { Arm = arm, Scenario = "coldstart", Regime = "single", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = opHedged });
            }
            rows.Add(ScenarioRow.From(arm, on, "coldstart", "single", lat, hedged, hedgeWon: hedged));
            Console.WriteLine($"  [cold {arm}] samples={samples} hedged={hedged} p50={Pct(lat,0.5):F0}ms p99={Pct(lat,0.99):F0}ms");
        }

        private async Task RunRefreshLowAsync(string arm, bool on, TimeSpan hubDelay, int count,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows, ConcurrentDictionary<string, long> winRegions)
        {
            FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
            FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

            using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
            Container container = client.GetContainer(TestCommon.FaultInjectionDatabaseName, TestCommon.FaultInjectionContainerName);
            await container.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
            string rid = await ((ContainerInternal)container).GetCachedRIDAsync(CancellationToken.None);
            PartitionKeyRangeCache pkCache = await client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            c.Enable();
            p.Enable();

            List<double> lat = new List<double>();
            long fired = 0, won = 0;
            for (int i = 0; i < count; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                (bool f, bool w, string region) = await ForceRefreshWithHedgeAsync(pkCache, rid);
                sw.Stop();
                lat.Add(sw.Elapsed.TotalMilliseconds);
                if (f) { fired++; }
                if (w) { won++; RecordWin(winRegions, arm, "low", region); }
                opRows.Add(new OpRow { Arm = arm, Scenario = "refresh", Regime = "low", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = f });
            }

            rows.Add(ScenarioRow.From(arm, on, "refresh", "low", lat, fired, won));
            Console.WriteLine($"  [refresh {arm}/low] n={lat.Count} hedgeFired={fired} hedgeWon={won} " +
                $"p50={Pct(lat,0.5):F0}ms p99={Pct(lat,0.99):F0}ms");
        }

        private async Task RunRefreshSaturatingAsync(string arm, bool on, TimeSpan hubDelay, int rounds, int containerCount,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows, ConcurrentDictionary<string, long> winRegions)
        {
            FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
            FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

            using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
            PartitionKeyRangeCache pkCache = await client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            List<string> rids = new List<string>();
            for (int i = 0; i < containerCount; i++)
            {
                Container ci = client.GetContainer(TestCommon.FaultInjectionDatabaseName, ExtraContainerId(i));
                await ci.ReadItemAsync<FaultInjectionTestObject>("k", new PartitionKey("k"));
                rids.Add(await ((ContainerInternal)ci).GetCachedRIDAsync(CancellationToken.None));
            }

            c.Enable();
            p.Enable();

            ConcurrentBag<double> lat = new ConcurrentBag<double>();
            long fired = 0, won = 0;
            int failures = 0;
            for (int round = 0; round < rounds; round++)
            {
                // One simultaneous burst of all distinct containers (> the old budget of 8).
                List<Task> tasks = rids.Select(rid => Task.Run(async () =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        (bool f, bool w, string region) = await ForceRefreshWithHedgeAsync(pkCache, rid);
                        sw.Stop();
                        lat.Add(sw.Elapsed.TotalMilliseconds);
                        if (f) { Interlocked.Increment(ref fired); }
                        if (w) { Interlocked.Increment(ref won); RecordWin(winRegions, arm, "saturating", region); }
                        opRows.Add(new OpRow { Arm = arm, Scenario = "refresh", Regime = "saturating", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = f });
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        Interlocked.Increment(ref failures);
                        lat.Add(sw.Elapsed.TotalMilliseconds);
                        LogFailureSample(arm, "saturating", ex);
                    }
                })).ToList();
                await Task.WhenAll(tasks);
            }

            ScenarioRow row = ScenarioRow.From(arm, on, "refresh", "saturating", lat.ToList(), fired, won);
            row.Failures = failures;
            rows.Add(row);
            Console.WriteLine($"  [refresh {arm}/saturating] n={lat.Count} hedgeFired={fired} hedgeWon={won} failures={failures} " +
                $"p50={Pct(lat.ToList(),0.5):F0}ms p99={Pct(lat.ToList(),0.99):F0}ms");
        }

        private async Task RunMixedWorkloadAsync(string arm, bool on, TimeSpan hubDelay, int ops, int concurrency,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows)
        {
            FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
            FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

            using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
            Container container = client.GetContainer(TestCommon.FaultInjectionDatabaseName, TestCommon.FaultInjectionContainerName);
            await container.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
            string rid = await ((ContainerInternal)container).GetCachedRIDAsync(CancellationToken.None);
            PartitionKeyRangeCache pkCache = await client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            c.Enable();
            p.Enable();

            ConcurrentBag<double> all = new ConcurrentBag<double>();
            ConcurrentBag<double> refreshOnly = new ConcurrentBag<double>();
            long fired = 0, won = 0;
            SemaphoreSlim gate = new SemaphoreSlim(concurrency, concurrency);
            Random rng = new Random(7);
            List<Task> tasks = new List<Task>();
            string[] ids = new[] { "testId", "testId2", "testId3", "testId4" };
            string[] pks = new[] { "pk", "pk2", "pk3", "pk4" };

            for (int i = 0; i < ops; i++)
            {
                bool isRefresh = rng.NextDouble() < 0.30;
                int idx = rng.Next(ids.Length);
                await gate.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        if (isRefresh)
                        {
                            (bool f, bool w, _) = await ForceRefreshWithHedgeAsync(pkCache, rid);
                            if (f) { Interlocked.Increment(ref fired); }
                            if (w) { Interlocked.Increment(ref won); }
                        }
                        else
                        {
                            await container.ReadItemAsync<FaultInjectionTestObject>(ids[idx], new PartitionKey(pks[idx]));
                        }
                        sw.Stop();
                        all.Add(sw.Elapsed.TotalMilliseconds);
                        if (isRefresh)
                        {
                            refreshOnly.Add(sw.Elapsed.TotalMilliseconds);
                        }
                        opRows.Add(new OpRow { Arm = arm, Scenario = "mixed", Regime = isRefresh ? "refresh" : "warm", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = false });
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        all.Add(sw.Elapsed.TotalMilliseconds);
                        if (isRefresh)
                        {
                            refreshOnly.Add(sw.Elapsed.TotalMilliseconds);
                        }
                        LogFailureSample(arm, "mixed", ex);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));
            }
            await Task.WhenAll(tasks);

            rows.Add(ScenarioRow.From(arm, on, "mixed", "end-to-end", all.ToList(), fired, won));
            rows.Add(ScenarioRow.From(arm, on, "mixed", "refresh-subset", refreshOnly.ToList(), fired, won));
            Console.WriteLine($"  [mixed {arm}] e2e n={all.Count} p50={Pct(all.ToList(),0.5):F0} p99={Pct(all.ToList(),0.99):F0} | " +
                $"refresh n={refreshOnly.Count} p50={Pct(refreshOnly.ToList(),0.5):F0} p99={Pct(refreshOnly.ToList(),0.99):F0} | hedgeFired={fired} hedgeWon={won}");
        }

        private async Task RunFastFailBrownoutAsync(string arm, bool on, int rounds, int containerCount,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows, ConcurrentDictionary<string, long> winRegions)
        {
            FaultInjectionRule c = this.HubMetadata503Rule(FaultInjectionOperationType.MetadataContainer);
            FaultInjectionRule p = this.HubMetadata503Rule(FaultInjectionOperationType.MetadataPartitionKeyRange);
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

            using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
            PartitionKeyRangeCache pkCache = await client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            List<string> rids = new List<string>();
            for (int i = 0; i < containerCount; i++)
            {
                Container ci = client.GetContainer(TestCommon.FaultInjectionDatabaseName, ExtraContainerId(i));
                await ci.ReadItemAsync<FaultInjectionTestObject>("k", new PartitionKey("k"));
                rids.Add(await ((ContainerInternal)ci).GetCachedRIDAsync(CancellationToken.None));
            }

            c.Enable();
            p.Enable();

            ConcurrentBag<double> lat = new ConcurrentBag<double>();
            long fired = 0, won = 0;
            int failures = 0;
            for (int round = 0; round < rounds; round++)
            {
                List<Task> tasks = rids.Select(rid => Task.Run(async () =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        (bool f, bool w, string region) = await ForceRefreshWithHedgeAsync(pkCache, rid);
                        sw.Stop();
                        lat.Add(sw.Elapsed.TotalMilliseconds);
                        if (f) { Interlocked.Increment(ref fired); }
                        if (w) { Interlocked.Increment(ref won); RecordWin(winRegions, arm, "fastfail", region); }
                        opRows.Add(new OpRow { Arm = arm, Scenario = "fastfail", Regime = "brownout", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = f });
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        Interlocked.Increment(ref failures);
                        lat.Add(sw.Elapsed.TotalMilliseconds);
                        LogFailureSample(arm, "fastfail", ex);
                    }
                })).ToList();
                await Task.WhenAll(tasks);
            }

            ScenarioRow row = ScenarioRow.From(arm, on, "fastfail", "brownout", lat.ToList(), fired, won);
            row.Failures = failures;
            rows.Add(row);
            Console.WriteLine($"  [fastfail {arm}/brownout] n={lat.Count} hedgeFired={fired} hedgeWon={won} failures={failures} " +
                $"p50={Pct(lat.ToList(),0.5):F0}ms p99={Pct(lat.ToList(),0.99):F0}ms");
        }

        // ---------------------------------------------------------------------------------

        // Forced PkRange refresh that captures the PR's own "Metadata Hedge" trace datum, and retries
        // the transient thread-safety race in the FaultInjection TEST framework
        // (FaultInjectionServerErrorResultInternal.IsApplicable enumerates a per-rule history list
        // while a concurrent request appends to it) — unrelated to the hedging code under test.
        private static async Task<(bool fired, bool won, string winningRegion)> ForceRefreshWithHedgeAsync(
            PartitionKeyRangeCache pkCache, string rid)
        {
            for (int attempt = 0; ; attempt++)
            {
                using Trace trace = Trace.GetRootTrace("v2-hedge-probe");
                try
                {
                    await pkCache.TryGetOverlappingRangesAsync(
                        rid, FeedRangeEpk.FullRange.Range, trace, forceRefresh: true);
                    trace.SetWalkingStateRecursively();
                    return ExtractHedge(trace);
                }
                catch (InvalidOperationException ex)
                    when (attempt < 5 && ex.Message.Contains("Collection was modified", StringComparison.Ordinal))
                {
                    await Task.Delay(5).ConfigureAwait(false);
                }
            }
        }

        // Walks the trace tree for the PR's "Metadata Hedge" datum:
        //   "HedgeFired=True; HedgeWon=True; WinningRegion=East US"
        private static (bool fired, bool won, string winningRegion) ExtractHedge(ITrace root)
        {
            Stack<ITrace> stack = new Stack<ITrace>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                ITrace t = stack.Pop();
                if (t.Data != null && t.Data.TryGetValue(MetadataHedgingStrategy.TraceDatumKey, out object val))
                {
                    string s = val?.ToString() ?? string.Empty;
                    bool fired = s.Contains("HedgeFired=True", StringComparison.OrdinalIgnoreCase);
                    bool won = s.Contains("HedgeWon=True", StringComparison.OrdinalIgnoreCase);
                    string region = null;
                    int idx = s.IndexOf("WinningRegion=", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        region = s.Substring(idx + "WinningRegion=".Length).Trim();
                    }
                    return (fired, won, region);
                }

                if (t.Children != null)
                {
                    foreach (ITrace child in t.Children)
                    {
                        stack.Push(child);
                    }
                }
            }
            return (false, false, null);
        }

        private static bool DiagnosticsShowHedge(CosmosDiagnostics diagnostics)
        {
            string s = diagnostics?.ToString();
            return s != null
                && s.Contains(MetadataHedgingStrategy.TraceDatumKey, StringComparison.Ordinal)
                && s.Contains("HedgeFired=True", StringComparison.OrdinalIgnoreCase);
        }

        private static void RecordWin(ConcurrentDictionary<string, long> winRegions, string arm, string regime, string region)
        {
            string key = $"{arm}|{regime}|{region ?? "unknown"}";
            winRegions.AddOrUpdate(key, 1, (_, v) => v + 1);
        }

        private static void LogFailureSample(string arm, string regime, Exception ex)
        {
            int n = Interlocked.Increment(ref failureSamplesLogged);
            if (n <= 6)
            {
                string status = ex is CosmosException ce ? ce.StatusCode.ToString()
                    : ex is Microsoft.Azure.Documents.DocumentClientException dce ? dce.StatusCode?.ToString() ?? "?"
                    : "n/a";
                Console.WriteLine($"      [failure {arm}/{regime}] {ex.GetType().Name} status={status}: {ex.Message.Split('\n')[0]}");
            }
        }

        private static int failureSamplesLogged;

        private static string ExtraContainerId(int i) => $"fihedge{i}";

        private async Task EnsureExtraContainersAsync(CosmosClient client, int count)
        {
            Database database = client.GetDatabase(TestCommon.FaultInjectionDatabaseName);
            bool anyCreated = false;
            for (int i = 0; i < count; i++)
            {
                ContainerResponse resp = await database.CreateContainerIfNotExistsAsync(
                    id: ExtraContainerId(i),
                    partitionKeyPath: "/pk",
                    throughput: 400);
                if (resp.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    anyCreated = true;
                    await resp.Container.CreateItemAsync(new FaultInjectionTestObject { Id = "k", Pk = "k" });
                }
            }
            if (anyCreated)
            {
                await Task.Delay(60000);
            }
        }

        private FaultInjectionRule HubMetadataDelayRule(TimeSpan delay, FaultInjectionOperationType opType)
        {
            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: $"hub-delay-{opType}-{Guid.NewGuid()}",
                condition: new FaultInjectionConditionBuilder()
                    .WithRegion(this.hubRegion)
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .WithOperationType(opType)
                    .Build(),
                result: FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                    .WithDelay(delay)
                    .WithTimes(int.MaxValue)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(30))
                .Build();
            rule.Disable();
            return rule;
        }

        // 503 (ServiceUnavailable, NO delay) on the hub metadata path — a regional failure, so the
        // primary fast-fails and the strategy fires the hedge IMMEDIATELY (skips the 1.5s threshold).
        private FaultInjectionRule HubMetadata503Rule(FaultInjectionOperationType opType)
        {
            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: $"hub-503-{opType}-{Guid.NewGuid()}",
                condition: new FaultInjectionConditionBuilder()
                    .WithRegion(this.hubRegion)
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .WithOperationType(opType)
                    .Build(),
                result: FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                    .WithTimes(int.MaxValue)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(30))
                .Build();
            rule.Disable();
            return rule;
        }

        private CosmosClient NewClient(FaultInjector faultInjector, bool? hedgingOptIn)
        {
            Environment.SetEnvironmentVariable(MetadataHedgingEnvVar, hedgingOptIn.HasValue ? (hedgingOptIn.Value ? "true" : "false") : null);

            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = PreferredRegions,
                ConsistencyLevel = ConsistencyLevel.Session,
                Serializer = this.serializer,
            };

            return faultInjector == null
                ? new CosmosClient(this.connectionString, options)
                : new CosmosClient(this.connectionString, faultInjector.GetFaultInjectionClientOptions(options));
        }

        private static double Pct(List<double> vals, double p)
        {
            if (vals.Count == 0)
            {
                return 0;
            }
            double[] s = vals.ToArray();
            Array.Sort(s);
            int idx = (int)Math.Ceiling(p * s.Length) - 1;
            idx = Math.Max(0, Math.Min(s.Length - 1, idx));
            return s[idx];
        }

        private static void WriteScenarioCsv(List<ScenarioRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("arm,hedgingOn,scenario,regime,n,hedgeFired,hedgeWon,failures,p50Ms,p95Ms,p99Ms,maxMs");
            foreach (ScenarioRow r in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    r.Arm, r.HedgingOn ? "1" : "0", r.Scenario, r.Regime, r.N.ToString(),
                    r.HedgeFired.ToString(), r.HedgeWon.ToString(), r.Failures.ToString(),
                    r.P50.ToString("F2"), r.P95.ToString("F2"), r.P99.ToString("F2"), r.MaxMs.ToString("F2"),
                }));
            }
            File.WriteAllText(Path.Combine(OutDir, "fi_scenarios.csv"), sb.ToString());
        }

        private static void WriteOpCsv(ConcurrentBag<OpRow> ops)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("arm,scenario,regime,latencyMs,hedged");
            foreach (OpRow o in ops)
            {
                sb.AppendLine($"{o.Arm},{o.Scenario},{o.Regime},{o.LatencyMs.ToString("F2", CultureInfo.InvariantCulture)},{(o.Hedged ? 1 : 0)}");
            }
            File.WriteAllText(Path.Combine(OutDir, "fi_ops.csv"), sb.ToString());
        }

        private static void WriteWinRegionCsv(ConcurrentDictionary<string, long> winRegions)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("arm,regime,winningRegion,count");
            foreach (KeyValuePair<string, long> kv in winRegions.OrderBy(x => x.Key))
            {
                string[] parts = kv.Key.Split('|');
                sb.AppendLine($"{parts[0]},{parts[1]},{parts[2]},{kv.Value}");
            }
            File.WriteAllText(Path.Combine(OutDir, "fi_winregions.csv"), sb.ToString());
        }

        private static void PrintSummary(List<ScenarioRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=============== v2 FI HEDGING SUMMARY (PR #5999, PR vs main) ===============");
            foreach (IGrouping<string, ScenarioRow> g in rows.GroupBy(r => r.Scenario + "/" + r.Regime))
            {
                sb.AppendLine($"--- {g.Key} ---");
                foreach (ScenarioRow r in g.OrderBy(x => x.Arm))
                {
                    sb.AppendLine($"  {r.Arm,-10} n={r.N,4} hedgeFired={r.HedgeFired,4} hedgeWon={r.HedgeWon,4} fail={r.Failures,3} " +
                        $"p50={r.P50,8:F0} p95={r.P95,8:F0} p99={r.P99,8:F0}");
                }
            }
            sb.AppendLine("===========================================================================");
            Console.WriteLine(sb.ToString());
            File.WriteAllText(Path.Combine(OutDir, "summary.txt"), sb.ToString());
        }

        private sealed class ScenarioRow
        {
            public string Arm;
            public bool HedgingOn;
            public string Scenario;
            public string Regime;
            public int N;
            public long HedgeFired;
            public long HedgeWon;
            public int Failures;
            public double P50;
            public double P95;
            public double P99;
            public double MaxMs;

            public static ScenarioRow From(string arm, bool on, string scenario, string regime,
                List<double> lat, long fired, long hedgeWon)
            {
                return new ScenarioRow
                {
                    Arm = arm,
                    HedgingOn = on,
                    Scenario = scenario,
                    Regime = regime,
                    N = lat.Count,
                    HedgeFired = fired,
                    HedgeWon = hedgeWon,
                    P50 = Pct(lat, 0.50),
                    P95 = Pct(lat, 0.95),
                    P99 = Pct(lat, 0.99),
                    MaxMs = lat.Count > 0 ? lat.Max() : 0,
                };
            }
        }

        private sealed class OpRow
        {
            public string Arm;
            public string Scenario;
            public string Regime;
            public double LatencyMs;
            public bool Hedged;
        }
    }
}
