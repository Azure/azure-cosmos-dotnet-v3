//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
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

    /// <summary>
    /// REAL end-to-end fault-injection validation harness for PR #5923 (metadata hedging).
    /// NOT a CI test — gated behind RUN_HEDGE_FI=1 and a live multi-region account.
    ///
    /// Unlike the two earlier simulated harnesses (MetadataHedgingScenarioHarness /
    /// MetadataHedgingStressHarness), which fake latency with Task.Delay and call
    /// MetadataHedgingStrategy.ExecuteAsync directly, this harness:
    ///   * Drives REAL SDK operations (ReadItemAsync, forced PartitionKeyRange refreshes) so
    ///     the strategy is exercised through its real call sites in ClientCollectionCache /
    ///     PartitionKeyRangeCache.
    ///   * Injects the slow hub at the REAL transport layer via Microsoft.Azure.Cosmos.
    ///     FaultInjection: a region-scoped ResponseDelay on the hub's Gateway metadata
    ///     operations (MetadataContainer + MetadataPartitionKeyRange). Secondary regions
    ///     stay healthy so a hedge can win.
    ///   * A/B compares the PR opt-in ON (AZURE_COSMOS_METADATA_HEDGING_ENABLED=true) vs
    ///     OFF (=false, i.e. main's behaviour) with the SAME fault injection + workload.
    ///   * Cross-checks hedge counts against the SDK meter Azure.Cosmos.Client.MetadataHedging.
    ///
    /// What is real here: the transport delays (fault injection), the SDK read path, the live
    /// region topology, the strategy decision/cancellation/budget logic. What remains for a
    /// pre-GA step: a real degraded-Azure-region test (no synthetic delay at all).
    ///
    /// Test/docs-only; modifies no Microsoft.Azure.Cosmos/src/** code.
    /// </summary>
    [TestClass]
    [TestCategory("StressHarness")]
    public sealed class MetadataHedgingFaultInjectionHarness
    {
        private const string MetadataHedgingEnvVar = "AZURE_COSMOS_METADATA_HEDGING_ENABLED";
        private const string MeterName = "Azure.Cosmos.Client.MetadataHedging";

        private static readonly string[] PreferredRegions = new[]
        {
            "West US 2", "East US", "South Central US", "Central US", "North Central US",
        };

        private static string OutDir =>
            Environment.GetEnvironmentVariable("HEDGE_FI_OUTDIR")
            ?? Path.Combine(Path.GetTempPath(), "hedge-fi");

        private string connectionString;
        private string hubRegion;
        private TestCommon.CosmosSystemTextJsonSerializer serializer;

        [TestMethod]
        [Owner("nalutripician")]
        public async Task RunFaultInjectionHedgingAsync()
        {
            if (Environment.GetEnvironmentVariable("RUN_HEDGE_FI") != "1")
            {
                Assert.Inconclusive("Set RUN_HEDGE_FI=1 (and COSMOSDB_MULTI_REGION) to run the FI hedging harness.");
                return;
            }

            this.connectionString = TestCommon.GetConnectionString();
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Inconclusive("COSMOSDB_MULTI_REGION is required for the FI hedging harness.");
                return;
            }

            Directory.CreateDirectory(OutDir);

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            this.serializer = new TestCommon.CosmosSystemTextJsonSerializer(jsonOptions);

            // Mode knobs. "compressed" (default) uses a 3s hub delay so OFF is ~4s/op and the
            // run is feasible; "realtime" uses a true 8s delay (no compression); "probe" is tiny.
            string mode = (Environment.GetEnvironmentVariable("HEDGE_FI_MODE") ?? "compressed").ToLowerInvariant();
            (TimeSpan hubDelay, int coldClients, int lowRefresh, int satRounds, int containerCount, int mixedOps) cfg = mode switch
            {
                "probe" => (TimeSpan.FromSeconds(3), 3, 6, 2, 10, 12),
                "realtime" => (TimeSpan.FromSeconds(8), 4, 10, 2, 12, 16),
                _ => (TimeSpan.FromSeconds(3), 8, 30, 4, 12, 60),
            };

            Console.WriteLine($"[fi-harness] mode={mode} hubDelay={cfg.hubDelay.TotalSeconds}s outDir={OutDir}");

            // ---- Seed / topology + extra containers via a plain (no-FI) client ----
            using (CosmosClient setupClient = this.NewClient(faultInjector: null, hedgingOptIn: null))
            {
                await TestCommon.GetOrCreateMultiRegionFIDatabaseAndContainersAsync(setupClient);
                await this.EnsureExtraContainersAsync(setupClient, cfg.containerCount);
                Uri writeEndpoint = setupClient.DocumentClient.GlobalEndpointManager.WriteEndpoints.First();
                this.hubRegion = setupClient.DocumentClient.GlobalEndpointManager.GetLocation(writeEndpoint);
                IReadOnlyList<Uri> reads = setupClient.DocumentClient.GlobalEndpointManager.ReadEndpoints;
                Console.WriteLine($"[fi-harness] hub(write) region = {this.hubRegion}; {reads.Count} read regions:");
                foreach (Uri u in reads)
                {
                    Console.WriteLine($"    {setupClient.DocumentClient.GlobalEndpointManager.GetLocation(u),-18} {u}");
                }
                Assert.IsTrue(reads.Count >= 2, "Need >=2 read regions for hedging.");
            }

            List<ScenarioRow> rows = new List<ScenarioRow>();
            ConcurrentBag<OpRow> opRows = new ConcurrentBag<OpRow>();

            foreach (bool on in new[] { false, true })
            {
                string arm = on ? "PR(ON)" : "main(OFF)";
                Console.WriteLine($"\n========== ARM {arm} ==========");

                // 1) COLD START end-to-end: fresh client per sample, rule enabled, time the FIRST ReadItemAsync.
                await this.RunColdStartAsync(arm, on, cfg.hubDelay, cfg.coldClients, rows, opRows);

                // 2) STEADY-STATE metadata refresh — LOW contention: sequential, so each forced
                //    refresh is a DISTINCT (non-coalesced) metadata read that can hedge.
                await this.RunRefreshLowAsync(arm, on, cfg.hubDelay, cfg.lowRefresh, rows, opRows);

                // 3) STEADY-STATE metadata refresh — SATURATING storm across many distinct containers
                //    (> budget 8) so the per-client budget is genuinely exhausted (BudgetExhausted > 0).
                await this.RunRefreshSaturatingAsync(arm, on, cfg.hubDelay, cfg.satRounds, cfg.containerCount, rows, opRows);

                // 4) MIXED end-to-end workload: warm reads (untouched) + refresh-bearing ops. Shows
                //    end-to-end p50 ~unchanged (warm reads dominate) while the refresh tail improves.
                await this.RunMixedWorkloadAsync(arm, on, cfg.hubDelay, cfg.mixedOps, concurrency: 20, rows, opRows);
            }

            WriteScenarioCsv(rows);
            WriteOpCsv(opRows);
            PrintSummary(rows);
            Console.WriteLine($"[fi-harness] DONE. CSVs in {OutDir}");
        }

        // ---------------------------------------------------------------------------------

        private async Task RunColdStartAsync(string arm, bool on, TimeSpan hubDelay, int samples,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows)
        {
            List<double> lat = new List<double>();
            long fires = 0;
            for (int i = 0; i < samples; i++)
            {
                FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
                FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
                FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

                using HedgeMeter meter = new HedgeMeter();
                using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
                Container container = client.GetContainer(TestCommon.FaultInjectionDatabaseName, TestCommon.FaultInjectionContainerName);

                c.Enable();
                p.Enable();

                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    // First op on a fresh client → populates Collection + PkRange caches (hedge-eligible)
                    // under the slow hub. This is a genuine end-to-end ReadItemAsync.
                    await container.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
                }
                catch (CosmosException ex)
                {
                    Console.WriteLine($"  [cold {arm}] sample {i} non-fatal: {ex.StatusCode}");
                }
                sw.Stop();
                lat.Add(sw.Elapsed.TotalMilliseconds);
                fires += meter.Fires;
                opRows.Add(new OpRow { Arm = arm, Scenario = "coldstart", Regime = "single", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = meter.Fires > 0 });
            }
            rows.Add(ScenarioRow.From(arm, on, "coldstart", "single", lat, fires, budgetExhausted: 0, maxConcSecondary: -1));
            Console.WriteLine($"  [cold {arm}] samples={samples} fires={fires} p50={Pct(lat,0.5):F0}ms p99={Pct(lat,0.99):F0}ms");
        }

        private async Task RunRefreshLowAsync(string arm, bool on, TimeSpan hubDelay, int count,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows)
        {
            FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
            FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

            using HedgeMeter meter = new HedgeMeter();
            using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
            Container container = client.GetContainer(TestCommon.FaultInjectionDatabaseName, TestCommon.FaultInjectionContainerName);

            // Warm caches BEFORE enabling the rule, so the warm-up isn't itself delayed.
            await container.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
            string rid = await ((ContainerInternal)container).GetCachedRIDAsync(CancellationToken.None);
            PartitionKeyRangeCache pkCache = await client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            c.Enable();
            p.Enable();
            meter.Reset();

            List<double> lat = new List<double>();
            int failures = 0;
            for (int i = 0; i < count; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    // forceRefresh:true → a real PartitionKeyRange ReadFeed metadata read through
                    // MetadataHedgingStrategy.ExecuteAsync. Sequential so each is a distinct (non-coalesced) read.
                    await pkCache.TryGetOverlappingRangesAsync(rid, FeedRangeEpk.FullRange.Range, NoOpTrace.Singleton, forceRefresh: true);
                    sw.Stop();
                    lat.Add(sw.Elapsed.TotalMilliseconds);
                    opRows.Add(new OpRow { Arm = arm, Scenario = "refresh", Regime = "low", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = false });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    failures++;
                    lat.Add(sw.Elapsed.TotalMilliseconds);
                    LogFailureSample(arm, "low", ex);
                }
            }

            ScenarioRow row = ScenarioRow.From(arm, on, "refresh", "low", lat, meter.Fires, meter.BudgetExhausted, maxConcSecondary: -1);
            row.Failures = failures;
            rows.Add(row);
            Console.WriteLine($"  [refresh {arm}/low] n={lat.Count} fires={meter.Fires} budgetExh={meter.BudgetExhausted} failures={failures} " +
                $"p50={Pct(lat,0.5):F0}ms p99={Pct(lat,0.99):F0}ms");
        }

        private async Task RunRefreshSaturatingAsync(string arm, bool on, TimeSpan hubDelay, int rounds, int containerCount,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows)
        {
            FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
            FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

            using HedgeMeter meter = new HedgeMeter();
            using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
            PartitionKeyRangeCache pkCache = await client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            // Warm all distinct containers BEFORE enabling the rule.
            List<string> rids = new List<string>();
            for (int i = 0; i < containerCount; i++)
            {
                Container ci = client.GetContainer(TestCommon.FaultInjectionDatabaseName, ExtraContainerId(i));
                await ci.ReadItemAsync<FaultInjectionTestObject>("k", new PartitionKey("k"));
                rids.Add(await ((ContainerInternal)ci).GetCachedRIDAsync(CancellationToken.None));
            }

            c.Enable();
            p.Enable();
            meter.Reset();

            ConcurrentBag<double> lat = new ConcurrentBag<double>();
            int failures = 0;
            for (int round = 0; round < rounds; round++)
            {
                // Fire all distinct containers concurrently → containerCount (> budget 8) simultaneous
                // distinct metadata reads, so the per-client hedge budget is genuinely exhausted.
                List<Task> tasks = rids.Select(rid => Task.Run(async () =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        await pkCache.TryGetOverlappingRangesAsync(rid, FeedRangeEpk.FullRange.Range, NoOpTrace.Singleton, forceRefresh: true);
                        sw.Stop();
                        lat.Add(sw.Elapsed.TotalMilliseconds);
                        opRows.Add(new OpRow { Arm = arm, Scenario = "refresh", Regime = "saturating", LatencyMs = sw.Elapsed.TotalMilliseconds, Hedged = false });
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

            ScenarioRow row = ScenarioRow.From(arm, on, "refresh", "saturating", lat.ToList(), meter.Fires, meter.BudgetExhausted, maxConcSecondary: -1);
            row.Failures = failures;
            rows.Add(row);
            Console.WriteLine($"  [refresh {arm}/saturating] n={lat.Count} fires={meter.Fires} budgetExh={meter.BudgetExhausted} failures={failures} " +
                $"p50={Pct(lat.ToList(),0.5):F0}ms p99={Pct(lat.ToList(),0.99):F0}ms");
        }

        private async Task RunMixedWorkloadAsync(string arm, bool on, TimeSpan hubDelay, int ops, int concurrency,
            List<ScenarioRow> rows, ConcurrentBag<OpRow> opRows)
        {
            FaultInjectionRule c = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataContainer);
            FaultInjectionRule p = this.HubMetadataDelayRule(hubDelay, FaultInjectionOperationType.MetadataPartitionKeyRange);
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { c, p });

            using HedgeMeter meter = new HedgeMeter();
            using CosmosClient client = this.NewClient(injector, hedgingOptIn: on);
            Container container = client.GetContainer(TestCommon.FaultInjectionDatabaseName, TestCommon.FaultInjectionContainerName);
            await container.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
            string rid = await ((ContainerInternal)container).GetCachedRIDAsync(CancellationToken.None);
            PartitionKeyRangeCache pkCache = await client.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            c.Enable();
            p.Enable();
            meter.Reset();

            ConcurrentBag<double> all = new ConcurrentBag<double>();
            ConcurrentBag<double> refreshOnly = new ConcurrentBag<double>();
            SemaphoreSlim gate = new SemaphoreSlim(concurrency, concurrency);
            Random rng = new Random(7);
            List<Task> tasks = new List<Task>();
            string[] ids = new[] { "testId", "testId2", "testId3", "testId4" };
            string[] pks = new[] { "pk", "pk2", "pk3", "pk4" };

            for (int i = 0; i < ops; i++)
            {
                bool isRefresh = rng.NextDouble() < 0.30; // ~30% refresh-bearing
                int idx = rng.Next(ids.Length);
                await gate.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        if (isRefresh)
                        {
                            await pkCache.TryGetOverlappingRangesAsync(rid, FeedRangeEpk.FullRange.Range, NoOpTrace.Singleton, forceRefresh: true);
                        }
                        else
                        {
                            // warm read — hits cached metadata, should NOT be delayed
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

            rows.Add(ScenarioRow.From(arm, on, "mixed", "end-to-end", all.ToList(), meter.Fires, meter.BudgetExhausted, maxConcSecondary: -1));
            rows.Add(ScenarioRow.From(arm, on, "mixed", "refresh-subset", refreshOnly.ToList(), meter.Fires, meter.BudgetExhausted, maxConcSecondary: -1));
            Console.WriteLine($"  [mixed {arm}] e2e n={all.Count} p50={Pct(all.ToList(),0.5):F0} p99={Pct(all.ToList(),0.99):F0} | " +
                $"refresh n={refreshOnly.Count} p50={Pct(refreshOnly.ToList(),0.5):F0} p99={Pct(refreshOnly.ToList(),0.99):F0} | fires={meter.Fires} budgetExh={meter.BudgetExhausted}");
        }

        // ---------------------------------------------------------------------------------

        private static int failureSamplesLogged;

        private static void LogFailureSample(string arm, string regime, Exception ex)
        {
            // Surface only the first few distinct failures to keep the log readable. Budget-exhausted
            // primary-only ops on a hard-degraded hub can legitimately error/time out — that is the
            // bounded-amplification trade-off being measured, not a harness bug.
            if (Interlocked.Increment(ref failureSamplesLogged) <= 6)
            {
                string status = ex is CosmosException ce ? ce.StatusCode.ToString()
                    : ex is Microsoft.Azure.Documents.DocumentClientException dce ? dce.StatusCode?.ToString() ?? "?"
                    : "n/a";
                Console.WriteLine($"      [failure {arm}/{regime}] {ex.GetType().Name} status={status}: {ex.Message.Split('\n')[0]}");
            }
        }

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
                // Let the new containers replicate to all regions before reads target them.
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

        private CosmosClient NewClient(FaultInjector faultInjector, bool? hedgingOptIn)
        {
            if (hedgingOptIn.HasValue)
            {
                Environment.SetEnvironmentVariable(MetadataHedgingEnvVar, hedgingOptIn.Value ? "true" : "false");
            }
            else
            {
                Environment.SetEnvironmentVariable(MetadataHedgingEnvVar, null);
            }

            CosmosClientOptions options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway, // metadata path; matches the hedged ops
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
            sb.AppendLine("arm,hedgingOn,scenario,regime,n,meterFires,budgetExhausted,failures,p50Ms,p95Ms,p99Ms,maxMs");
            foreach (ScenarioRow r in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    r.Arm, r.HedgingOn ? "1" : "0", r.Scenario, r.Regime, r.N.ToString(),
                    r.MeterFires.ToString(), r.BudgetExhausted.ToString(), r.Failures.ToString(),
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

        private static void PrintSummary(List<ScenarioRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===================== FI HEDGING SUMMARY (PR vs main) =====================");
            foreach (IGrouping<string, ScenarioRow> g in rows.GroupBy(r => r.Scenario + "/" + r.Regime))
            {
                sb.AppendLine($"--- {g.Key} ---");
                foreach (ScenarioRow r in g.OrderBy(x => x.Arm))
                {
                    sb.AppendLine($"  {r.Arm,-10} n={r.N,4} fires={r.MeterFires,4} budgetExh={r.BudgetExhausted,4} fail={r.Failures,3} " +
                        $"p50={r.P50,8:F0} p95={r.P95,8:F0} p99={r.P99,8:F0}");
                }
            }
            sb.AppendLine("==========================================================================");
            Console.WriteLine(sb.ToString());
            File.WriteAllText(Path.Combine(OutDir, "summary.txt"), sb.ToString());
        }

        private sealed class HedgeMeter : IDisposable
        {
            private readonly MeterListener listener;
            private long fires;
            private long budgetExhausted;

            public long Fires => Volatile.Read(ref this.fires);
            public long BudgetExhausted => Volatile.Read(ref this.budgetExhausted);

            public HedgeMeter()
            {
                this.listener = new MeterListener
                {
                    InstrumentPublished = (inst, l) =>
                    {
                        if (inst.Meter.Name == MeterName)
                        {
                            l.EnableMeasurementEvents(inst);
                        }
                    },
                };
                this.listener.SetMeasurementEventCallback<long>((inst, measurement, tags, state) =>
                {
                    if (inst.Name.EndsWith("fires", StringComparison.Ordinal))
                    {
                        Interlocked.Add(ref this.fires, measurement);
                    }
                    else if (inst.Name.EndsWith("budget_exhausted", StringComparison.Ordinal))
                    {
                        Interlocked.Add(ref this.budgetExhausted, measurement);
                    }
                });
                this.listener.Start();
            }

            public void Reset()
            {
                Volatile.Write(ref this.fires, 0);
                Volatile.Write(ref this.budgetExhausted, 0);
            }

            public void Dispose() => this.listener?.Dispose();
        }

        private sealed class ScenarioRow
        {
            public string Arm;
            public bool HedgingOn;
            public string Scenario;
            public string Regime;
            public int N;
            public long MeterFires;
            public long BudgetExhausted;
            public int Failures;
            public int MaxConcSecondary;
            public double P50;
            public double P95;
            public double P99;
            public double MaxMs;

            public static ScenarioRow From(string arm, bool on, string scenario, string regime,
                List<double> lat, long fires, long budgetExhausted, int maxConcSecondary)
            {
                return new ScenarioRow
                {
                    Arm = arm,
                    HedgingOn = on,
                    Scenario = scenario,
                    Regime = regime,
                    N = lat.Count,
                    MeterFires = fires,
                    BudgetExhausted = budgetExhausted,
                    MaxConcSecondary = maxConcSecondary,
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
