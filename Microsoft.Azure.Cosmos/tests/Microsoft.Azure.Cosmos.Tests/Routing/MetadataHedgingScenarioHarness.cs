//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.Routing.MetadataHedgingStrategy;

    /// <summary>
    /// Phased PR-vs-main scenario harness for PR #5923 (metadata hedging). NOT a CI unit
    /// test — gated behind RUN_HEDGE_SCENARIOS=1. It drives the REAL
    /// <see cref="MetadataHedgingStrategy.ExecuteAsync"/> against a REAL
    /// <see cref="GlobalEndpointManager"/> from a live 5-region account.
    ///
    /// Two arms per scenario:
    ///   * "PR"   = real metadata hedging strategy (customerOptIn=true).
    ///   * "main" = primary-only metadata reads (customerOptIn=false) — exactly main's
    ///              behaviour, which has no metadata hedging at all.
    ///
    /// End-to-end read latency = a shared "normal hedging" data-read step (identical on
    /// BOTH arms — models the existing data-plane availability strategy) + a metadata
    /// refresh step (the ONLY differentiator: PR hedges it, main does not). Metadata
    /// refreshes are spawned by (a) cold-start cache population and (b) 410 PartitionKey
    /// Range-Gone during the degraded phase.
    ///
    /// Phases over a scenario timeline: COLDSTART (good|bad) -> OK -> DEGRADED.
    ///
    /// This file ADDS a harness only; it does not modify any PR/src code.
    /// </summary>
    [TestClass]
    public sealed class MetadataHedgingScenarioHarness
    {
        private const string HubRegion = "West US 2";

        private static readonly string[] PreferredRegions = new[]
        {
            "West US 2", "East US", "South Central US", "Central US", "North Central US",
        };

        private static readonly string OutDir =
            Environment.GetEnvironmentVariable("HEDGE_SCENARIO_OUTDIR")
            ?? Path.Combine(Path.GetTempPath(), "hedge-scenarios");

        // Time-compressed 10x: threshold 150ms models prod 1.5s; hub-slow 0.5-1.0s models
        // 5-10s; healthy latencies are small. Only ratios / structural bounds matter.
        private const double ThresholdMs = 150;
        private const int HubSlowMinMs = 500;
        private const int HubSlowMaxMs = 1000;
        private const int FastMinMs = 5;
        private const int FastMaxMs = 25;

        // Shared "normal hedging" (data-plane availability strategy) model — identical on
        // both arms. The data read races the hub; if the hub is slow past this threshold a
        // secondary serves it. So a slow hub never blocks the DATA read by more than this.
        private const double NormalHedgeDataThresholdMs = 100;

        private enum Phase { ColdStart, Ok, Degraded }

        [TestMethod]
        [TestCategory("StressHarness")]
        public async Task RunMetadataHedgingScenariosAsync()
        {
            if (Environment.GetEnvironmentVariable("RUN_HEDGE_SCENARIOS") != "1")
            {
                Assert.Inconclusive("Set RUN_HEDGE_SCENARIOS=1 to run the metadata-hedging scenario harness.");
                return;
            }

            Directory.CreateDirectory(OutDir);
            string connectionString =
                Environment.GetEnvironmentVariable("COSMOSDB_MULTI_REGION")
                ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
            Assert.IsNotNull(connectionString, "COSMOSDB_MULTI_REGION env var is required.");

            using CosmosClient client = new CosmosClient(
                connectionString,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationPreferredRegions = PreferredRegions,
                });
            await client.ReadAccountAsync();
            IGlobalEndpointManager gem = client.DocumentClient.GlobalEndpointManager;

            Console.WriteLine($"[harness] live read endpoints ({gem.ReadEndpoints.Count}):");
            foreach (Uri u in gem.ReadEndpoints)
            {
                Console.WriteLine($"    {gem.GetLocation(u),-18} {u}");
            }
            Assert.IsTrue(gem.ReadEndpoints.Count >= 2, "Need >=2 read regions.");

            List<OpRecord> ops = new List<OpRecord>();
            List<PhaseSummary> phases = new List<PhaseSummary>();
            ConcurrentDictionary<string, long> regionSends = new ConcurrentDictionary<string, long>();

            // Scenario 1 — completely healthy.
            await this.RunScenarioAsync(gem, "S1-Healthy",
                BuildSchedule(coldBad: false, includeOk: true, includeDegraded: false),
                ops, phases, regionSends);

            // Scenario 2 — bad cold start, then healthy.
            await this.RunScenarioAsync(gem, "S2-BadColdStart",
                BuildSchedule(coldBad: true, includeOk: true, includeDegraded: false),
                ops, phases, regionSends);

            // Scenario 3 — bad cold start -> ok -> degraded (hub delay + 410 PkRange-Gone).
            await this.RunScenarioAsync(gem, "S3-BadCold+Degraded",
                BuildSchedule(coldBad: true, includeOk: true, includeDegraded: true),
                ops, phases, regionSends);

            // Scenario 4 — healthy cold start -> ok -> degraded.
            await this.RunScenarioAsync(gem, "S4-GoodCold+Degraded",
                BuildSchedule(coldBad: false, includeOk: true, includeDegraded: true),
                ops, phases, regionSends);

            WriteOpsCsv(ops);
            WritePhaseCsv(phases);
            WriteRegionCsv(regionSends);
            PrintSummary(phases);
            Console.WriteLine($"[harness] DONE. CSVs in {OutDir}");
        }

        private static List<(Phase phase, bool coldMeta, bool gone)> BuildSchedule(
            bool coldBad, bool includeOk, bool includeDegraded)
        {
            // coldBad is consumed by the per-op latency model (hub slow during ColdStart).
            List<(Phase, bool, bool)> sched = new List<(Phase, bool, bool)>();

            // Cold start: a burst of first reads, each populating collection + pkrange caches.
            const int coldBurst = 200;
            for (int i = 0; i < coldBurst; i++)
            {
                sched.Add((Phase.ColdStart, true, false));
            }

            if (includeOk)
            {
                const int okReads = 1500;
                for (int i = 0; i < okReads; i++)
                {
                    sched.Add((Phase.Ok, false, false));
                }
            }

            if (includeDegraded)
            {
                const int degradedReads = 3000;
                Random rng = new Random(12345);
                for (int i = 0; i < degradedReads; i++)
                {
                    // ~30% of reads hit 410 PartitionKeyRange-Gone -> spawn a PkRange refresh.
                    bool gone = rng.NextDouble() < 0.30;
                    sched.Add((Phase.Degraded, false, gone));
                }
            }

            return sched;
        }

        private async Task RunScenarioAsync(
            IGlobalEndpointManager gem,
            string scenario,
            List<(Phase phase, bool coldMeta, bool gone)> schedule,
            List<OpRecord> ops,
            List<PhaseSummary> phases,
            ConcurrentDictionary<string, long> regionSends)
        {
            bool coldBad = scenario.Contains("BadCold");
            foreach (string arm in new[] { "main", "PR" })
            {
                using MetadataHedgingStrategy strategy = new MetadataHedgingStrategy(
                    globalEndpointManager: gem,
                    isHedgingDisabledByGateway: () => false,
                    isPpafEnabled: () => true,
                    customerOptIn: arm == "PR" ? (bool?)true : false,
                    threshold: TimeSpan.FromMilliseconds(ThresholdMs),
                    perClientConcurrencyBudget: MetadataHedgingStrategy.DefaultPerClientConcurrencyBudget);

                ConcurrentBag<OpRecord> local = new ConcurrentBag<OpRecord>();
                SemaphoreSlim gate = new SemaphoreSlim(50, 50);
                int index = 0;
                List<Task> tasks = new List<Task>(schedule.Count);

                foreach ((Phase phase, bool coldMeta, bool gone) in schedule)
                {
                    int opIndex = Interlocked.Increment(ref index);
                    await gate.WaitAsync().ConfigureAwait(false);
                    Phase ph = phase;
                    bool needsColdMeta = coldMeta;
                    bool needsGone = gone;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            OpRecord rec = await this.RunOneOpAsync(
                                gem, strategy, scenario, arm, ph, opIndex,
                                needsColdMeta, needsGone, coldBad, regionSends).ConfigureAwait(false);
                            local.Add(rec);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                lock (ops)
                {
                    ops.AddRange(local);
                }

                foreach (Phase ph in Enum.GetValues(typeof(Phase)).Cast<Phase>())
                {
                    List<OpRecord> phaseOps = local.Where(o => o.Phase == ph.ToString()).ToList();
                    if (phaseOps.Count == 0)
                    {
                        continue;
                    }
                    double[] lat = phaseOps.Select(o => o.LatencyMs).OrderBy(x => x).ToArray();
                    phases.Add(new PhaseSummary
                    {
                        Scenario = scenario,
                        Arm = arm,
                        Phase = ph.ToString(),
                        Ops = phaseOps.Count,
                        MetaRefreshes = phaseOps.Count(o => o.MetaRefresh),
                        Hedges = phaseOps.Count(o => o.HedgeFired),
                        BudgetExhausted = phaseOps.Count(o => o.SkipReason == "BudgetExhausted"),
                        P50 = Percentile(lat, 0.50),
                        P99 = Percentile(lat, 0.99),
                        MaxMs = lat[lat.Length - 1],
                    });
                }

                Console.WriteLine($"[{scenario}/{arm}] ops={local.Count} " +
                    $"hedges={local.Count(o => o.HedgeFired)} metaRefreshes={local.Count(o => o.MetaRefresh)}");
            }
        }

        private async Task<OpRecord> RunOneOpAsync(
            IGlobalEndpointManager gem,
            MetadataHedgingStrategy strategy,
            string scenario,
            string arm,
            Phase phase,
            int opIndex,
            bool needsColdMeta,
            bool needsGone,
            bool coldBad,
            ConcurrentDictionary<string, long> regionSends)
        {
            bool hubSlow = (phase == Phase.Degraded) || (phase == Phase.ColdStart && coldBad);

            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> send =
                async (req, uri, ct) =>
                {
                    string region = gem.GetLocation(uri) ?? "unknown";
                    regionSends.AddOrUpdate($"{scenario}|{arm}|{region}", 1, (_, v) => v + 1);
                    bool isHub = string.Equals(region, HubRegion, StringComparison.OrdinalIgnoreCase);

                    if (isHub && hubSlow)
                    {
                        try
                        {
                            await Task.Delay(ThreadSafeRandom.Next(HubSlowMinMs, HubSlowMaxMs), ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        return MakeResponse(HttpStatusCode.Gone, SubStatusCodes.PartitionKeyRangeGone);
                    }

                    try
                    {
                        await Task.Delay(ThreadSafeRandom.Next(FastMinMs, FastMaxMs), ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    return MakeResponse(HttpStatusCode.OK);
                };

            Stopwatch sw = Stopwatch.StartNew();
            bool hedgeFired = false;
            string winningRegion = HubRegion;
            string skipReason = "None";
            bool metaRefresh = false;

            // ---- Metadata step (the ONLY PR-vs-main differentiator) ----
            // Cold start populates the collection + pkrange caches; a 410 PartitionKeyRange
            // -Gone in the degraded phase forces a pkrange cache refresh. Both flow through
            // the real strategy (PR hedges; main = primary-only).
            if (needsColdMeta)
            {
                metaRefresh = true;
                (bool f1, string w1, string s1) = await this.MetaReadAsync(
                    strategy, gem, send, ResourceType.Collection, OperationType.Read, isColdStart: true);
                (bool f2, string w2, string s2) = await this.MetaReadAsync(
                    strategy, gem, send, ResourceType.PartitionKeyRange, OperationType.ReadFeed, isColdStart: true);
                hedgeFired = f1 || f2;
                winningRegion = f2 ? w2 : w1;
                skipReason = (s1 == "BudgetExhausted" || s2 == "BudgetExhausted") ? "BudgetExhausted" : "None";
            }
            else if (needsGone)
            {
                metaRefresh = true;
                (hedgeFired, winningRegion, skipReason) = await this.MetaReadAsync(
                    strategy, gem, send, ResourceType.PartitionKeyRange, OperationType.ReadFeed, isColdStart: false);
            }

            // ---- Data-read step with shared "normal hedging" (identical on both arms) ----
            // Healthy: hub serves fast. Slow hub: a secondary serves after the data-hedge
            // threshold, so the data read is never blocked by the slow hub beyond it.
            double dataMs;
            if (hubSlow)
            {
                dataMs = NormalHedgeDataThresholdMs + ThreadSafeRandom.Next(FastMinMs, FastMaxMs);
            }
            else
            {
                dataMs = ThreadSafeRandom.Next(FastMinMs, FastMaxMs);
            }
            await Task.Delay((int)dataMs).ConfigureAwait(false);

            sw.Stop();
            return new OpRecord
            {
                Scenario = scenario,
                Arm = arm,
                Phase = phase.ToString(),
                OpIndex = opIndex,
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                MetaRefresh = metaRefresh,
                HedgeFired = hedgeFired,
                WinningRegion = winningRegion,
                SkipReason = skipReason,
            };
        }

        private async Task<(bool hedgeFired, string winningRegion, string skipReason)> MetaReadAsync(
            MetadataHedgingStrategy strategy,
            IGlobalEndpointManager gem,
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> send,
            ResourceType resourceType,
            OperationType operationType,
            bool isColdStart)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType, resourceType, AuthorizationTokenType.PrimaryMasterKey);
            MetadataHedgingContext ctx = new MetadataHedgingContext
            {
                IsColdStart = isColdStart,
                IsFirstReadFeedPage = true,
            };
            MetadataHedgingResult r = await strategy.ExecuteAsync(
                request, send, ctx, NoOpTrace.Singleton, CancellationToken.None).ConfigureAwait(false);
            return (r.HedgeFired, r.WinningRegion ?? "unknown", r.Diagnostics.SkipReason.ToString());
        }

        private static DocumentServiceResponse MakeResponse(HttpStatusCode status, SubStatusCodes subStatus = SubStatusCodes.Unknown)
        {
            StoreResponseNameValueCollection headers = new StoreResponseNameValueCollection();
            if (subStatus != SubStatusCodes.Unknown)
            {
                headers.Add(WFConstants.BackendHeaders.SubStatus, ((int)subStatus).ToString(CultureInfo.InvariantCulture));
            }
            return new DocumentServiceResponse(Stream.Null, headers, status);
        }

        private static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 0)
            {
                return 0;
            }
            int idx = (int)Math.Ceiling(p * sorted.Length) - 1;
            idx = Math.Max(0, Math.Min(sorted.Length - 1, idx));
            return sorted[idx];
        }

        private static void WriteOpsCsv(List<OpRecord> ops)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("scenario,arm,phase,opIndex,latencyMs,metaRefresh,hedgeFired,winningRegion,skipReason");
            foreach (OpRecord o in ops)
            {
                sb.Append(o.Scenario).Append(',').Append(o.Arm).Append(',').Append(o.Phase).Append(',')
                  .Append(o.OpIndex.ToString()).Append(',').Append(o.LatencyMs.ToString("F2")).Append(',')
                  .Append(o.MetaRefresh ? "1" : "0").Append(',').Append(o.HedgeFired ? "1" : "0").Append(',')
                  .Append(o.WinningRegion).Append(',').Append(o.SkipReason).Append('\n');
            }
            File.WriteAllText(Path.Combine(OutDir, "ops.csv"), sb.ToString());
        }

        private static void WritePhaseCsv(List<PhaseSummary> phases)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("scenario,arm,phase,ops,metaRefreshes,hedges,budgetExhausted,p50Ms,p99Ms,maxMs");
            foreach (PhaseSummary p in phases)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    p.Scenario, p.Arm, p.Phase, p.Ops.ToString(), p.MetaRefreshes.ToString(),
                    p.Hedges.ToString(), p.BudgetExhausted.ToString(),
                    p.P50.ToString("F2"), p.P99.ToString("F2"), p.MaxMs.ToString("F2"),
                }));
            }
            File.WriteAllText(Path.Combine(OutDir, "phase_summary.csv"), sb.ToString());
        }

        private static void WriteRegionCsv(ConcurrentDictionary<string, long> regionSends)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("scenario,arm,region,sends");
            foreach (KeyValuePair<string, long> kv in regionSends.OrderBy(x => x.Key))
            {
                string[] parts = kv.Key.Split('|');
                sb.AppendLine($"{parts[0]},{parts[1]},{parts[2]},{kv.Value}");
            }
            File.WriteAllText(Path.Combine(OutDir, "regions.csv"), sb.ToString());
        }

        private static void PrintSummary(List<PhaseSummary> phases)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================= SCENARIO SUMMARY (PR vs main) =================");
            foreach (IGrouping<string, PhaseSummary> g in phases.GroupBy(p => p.Scenario))
            {
                sb.AppendLine($"--- {g.Key} ---");
                foreach (PhaseSummary p in g.OrderBy(x => x.Phase).ThenBy(x => x.Arm))
                {
                    sb.AppendLine(
                        $"  {p.Phase,-10} {p.Arm,-4} ops={p.Ops,5} metaRefresh={p.MetaRefreshes,5} " +
                        $"hedges={p.Hedges,5} budgetExh={p.BudgetExhausted,5} " +
                        $"p50={p.P50,8:F1} p99={p.P99,9:F1}");
                }
            }
            sb.AppendLine("===============================================================");
            Console.WriteLine(sb.ToString());
            File.WriteAllText(Path.Combine(OutDir, "summary.txt"), sb.ToString());
        }

        private sealed class OpRecord
        {
            public string Scenario;
            public string Arm;
            public string Phase;
            public int OpIndex;
            public double LatencyMs;
            public bool MetaRefresh;
            public bool HedgeFired;
            public string WinningRegion;
            public string SkipReason;
        }

        private sealed class PhaseSummary
        {
            public string Scenario;
            public string Arm;
            public string Phase;
            public int Ops;
            public int MetaRefreshes;
            public int Hedges;
            public int BudgetExhausted;
            public double P50;
            public double P99;
            public double MaxMs;
        }

        private static class ThreadSafeRandom
        {
            [ThreadStatic]
            private static Random local;

            public static int Next(int minInclusive, int maxInclusive)
            {
                local ??= new Random(Guid.NewGuid().GetHashCode());
                return local.Next(minInclusive, maxInclusive + 1);
            }
        }
    }
}
