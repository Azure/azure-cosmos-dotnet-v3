//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
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
    /// Worst-case stress harness for PR #5923 (metadata hedging). NOT a CI unit test —
    /// gated behind the RUN_HEDGE_STRESS=1 environment variable. It drives the REAL
    /// <see cref="MetadataHedgingStrategy.ExecuteAsync"/> against a REAL
    /// <see cref="GlobalEndpointManager"/> built from a live multi-region account, while
    /// injecting a degraded-hub transport (5-10s gateway delay + PartitionKeyRange-Gone)
    /// through the strategy's own <c>sendToEndpoint</c> delegate.
    ///
    /// Models: SDK goes healthy -> hub (write) region gateway slow on all calls; reads
    /// return PartitionKeyRange-Gone which spawns PkRange ReadFeed refresh reads. Tracks
    /// per-region call counts, low vs high partition counts (100 / 10k / 50k), and how
    /// many PkRange refresh reads are spawned and hedged. Emits CSVs for graphing.
    ///
    /// This file ADDS a test harness only; it does not modify any PR/src code.
    /// </summary>
    [TestClass]
    public sealed class MetadataHedgingStressHarness
    {
        // Hub (write/first-preferred) region for the nalu-new account.
        private const string HubRegion = "West US 2";

        private static readonly string[] PreferredRegions = new[]
        {
            "West US 2",       // hub / write
            "East US",
            "South Central US",
            "Central US",
            "North Central US",
        };

        private static readonly string OutDir =
            Environment.GetEnvironmentVariable("HEDGE_STRESS_OUTDIR")
            ?? Path.Combine(Path.GetTempPath(), "hedge-stress");

        [TestMethod]
        [TestCategory("StressHarness")]
        public async Task RunMetadataHedgingStressAsync()
        {
            if (Environment.GetEnvironmentVariable("RUN_HEDGE_STRESS") != "1")
            {
                Assert.Inconclusive("Set RUN_HEDGE_STRESS=1 to run the metadata-hedging stress harness.");
                return;
            }

            Directory.CreateDirectory(OutDir);
            string connectionString =
                Environment.GetEnvironmentVariable("COSMOSDB_MULTI_REGION")
                ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
            Assert.IsNotNull(connectionString, "COSMOSDB_MULTI_REGION env var is required.");

            Console.WriteLine($"[harness] output dir: {OutDir}");

            using CosmosClient client = new CosmosClient(
                connectionString,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationPreferredRegions = PreferredRegions,
                    LimitToEndpoint = false,
                });

            // Force account read so GlobalEndpointManager populates regional read endpoints.
            await client.ReadAccountAsync();
            IGlobalEndpointManager gem = client.DocumentClient.GlobalEndpointManager;

            IReadOnlyList<Uri> readEndpoints = gem.ReadEndpoints;
            Console.WriteLine($"[harness] live read endpoints ({readEndpoints.Count}):");
            foreach (Uri u in readEndpoints)
            {
                Console.WriteLine($"    {gem.GetLocation(u),-18} {u}");
            }
            Assert.IsTrue(readEndpoints.Count >= 2,
                $"Need >=2 read regions for hedging; account exposes {readEndpoints.Count}. Add regions and retry.");

            // ---- Authoritative hedge telemetry via the SDK's own meter ----
            using HedgeMeterCollector meter = new HedgeMeterCollector();

            List<ScenarioResult> all = new List<ScenarioResult>();

            // Phase A: healthy baseline (hub fast) — proves no amplification when healthy.
            all.Add(await this.RunScenarioAsync(
                gem, meter,
                label: "Healthy-baseline",
                partitions: 2000,
                hedgingOn: true,
                degraded: false,
                thresholdMs: 150,
                hubDelayMinMs: 5, hubDelayMaxMs: 30,
                secondaryMinMs: 5, secondaryMaxMs: 20,
                concurrency: 200));

            // Phase B: degraded hub, time-compressed 10x (threshold 150ms models prod 1.5s,
            // hub delay 0.5-1.0s models 5-10s). Ratios + structural bounds are preserved.
            int[] partitionCounts = new[] { 100, 10000, 50000 };
            foreach (int p in partitionCounts)
            {
                foreach (bool on in new[] { true, false })
                {
                    all.Add(await this.RunScenarioAsync(
                        gem, meter,
                        label: $"Degraded-P{p}-{(on ? "HEDGE_ON" : "HEDGE_OFF")}",
                        partitions: p,
                        hedgingOn: on,
                        degraded: true,
                        thresholdMs: 150,
                        hubDelayMinMs: 500, hubDelayMaxMs: 1000,
                        secondaryMinMs: 5, secondaryMaxMs: 20,
                        concurrency: 400));
                }
            }

            // Phase C: one LOW-partition run at TRUE production timing (1.5s threshold,
            // 5-10s hub delay) to demonstrate real wall-clock behaviour end-to-end.
            all.Add(await this.RunScenarioAsync(
                gem, meter,
                label: "Degraded-P100-REALTIME-HEDGE_ON",
                partitions: 100,
                hedgingOn: true,
                degraded: true,
                thresholdMs: 1500,
                hubDelayMinMs: 5000, hubDelayMaxMs: 10000,
                secondaryMinMs: 20, secondaryMaxMs: 60,
                concurrency: 32));

            this.WriteScenarioCsv(all);
            this.WriteRegionCsv(all);
            this.PrintSummary(all);

            Console.WriteLine($"[harness] DONE. CSVs in {OutDir}");
        }

        private async Task<ScenarioResult> RunScenarioAsync(
            IGlobalEndpointManager gem,
            HedgeMeterCollector meter,
            string label,
            int partitions,
            bool hedgingOn,
            bool degraded,
            double thresholdMs,
            int hubDelayMinMs, int hubDelayMaxMs,
            int secondaryMinMs, int secondaryMaxMs,
            int concurrency)
        {
            Console.WriteLine($"[scenario] {label}: partitions={partitions} on={hedgingOn} degraded={degraded} " +
                $"threshold={thresholdMs}ms hub={hubDelayMinMs}-{hubDelayMaxMs}ms conc={concurrency}");

            ScenarioResult result = new ScenarioResult
            {
                Label = label,
                Partitions = partitions,
                HedgingOn = hedgingOn,
                Degraded = degraded,
                ThresholdMs = thresholdMs,
            };

            meter.Reset();

            // REAL strategy. customerOptIn:true force-enables (independent of account PPAF);
            // customerOptIn:false models hedging OFF (always primary-only).
            using MetadataHedgingStrategy strategy = new MetadataHedgingStrategy(
                globalEndpointManager: gem,
                isHedgingDisabledByGateway: () => false,
                isPpafEnabled: () => true,
                customerOptIn: hedgingOn ? (bool?)true : false,
                threshold: TimeSpan.FromMilliseconds(thresholdMs),
                perClientConcurrencyBudget: MetadataHedgingStrategy.DefaultPerClientConcurrencyBudget);

            int inFlightSecondary = 0;
            int maxInFlightSecondary = 0;

            // Injected transport: the strategy hands us the cloned request + target Uri.
            // We classify the target region and simulate the degraded hub vs healthy secondary.
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> send =
                async (req, uri, ct) =>
                {
                    string region = gem.GetLocation(uri) ?? "unknown";
                    result.RecordSend(region);
                    bool isHub = string.Equals(region, HubRegion, StringComparison.OrdinalIgnoreCase);

                    if (isHub && degraded)
                    {
                        // Hub gateway is slow on all metadata calls and returns PartitionKeyRange-Gone.
                        int delay = ThreadSafeRandom.Next(hubDelayMinMs, hubDelayMaxMs);
                        try
                        {
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Losing primary cancelled once the hedge won — return promptly.
                        }
                        return MakeResponse(HttpStatusCode.Gone, subStatus: SubStatusCodes.PartitionKeyRangeGone);
                    }

                    // Healthy region (hub when not degraded, or any secondary).
                    int cur = Interlocked.Increment(ref inFlightSecondary);
                    if (!isHub)
                    {
                        UpdateMax(ref maxInFlightSecondary, cur);
                    }
                    try
                    {
                        int delay = ThreadSafeRandom.Next(secondaryMinMs, secondaryMaxMs);
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        return MakeResponse(HttpStatusCode.OK);
                    }
                    catch (OperationCanceledException)
                    {
                        return MakeResponse(HttpStatusCode.OK);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref inFlightSecondary);
                    }
                };

            Stopwatch wall = Stopwatch.StartNew();
            SemaphoreSlim gate = new SemaphoreSlim(concurrency, concurrency);
            List<Task> tasks = new List<Task>(partitions);

            for (int i = 0; i < partitions; i++)
            {
                await gate.WaitAsync().ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Each PartitionKeyRange-Gone event spawns one PkRange ReadFeed refresh read.
                        DocumentServiceRequest request = DocumentServiceRequest.Create(
                            OperationType.ReadFeed,
                            ResourceType.PartitionKeyRange,
                            AuthorizationTokenType.PrimaryMasterKey);

                        MetadataHedgingContext ctx = new MetadataHedgingContext
                        {
                            IsColdStart = false,        // steady-state refresh read (worst case for amplification)
                            IsFirstReadFeedPage = true, // PkRange ReadFeed first page is eligible
                        };

                        Stopwatch opSw = Stopwatch.StartNew();
                        MetadataHedgingResult r = await strategy.ExecuteAsync(
                            request, send, ctx, NoOpTrace.Singleton, CancellationToken.None).ConfigureAwait(false);
                        opSw.Stop();

                        result.RecordOp(
                            latencyMs: opSw.Elapsed.TotalMilliseconds,
                            hedgeFired: r.HedgeFired,
                            winningRegion: r.WinningRegion ?? "unknown",
                            skipReason: r.Diagnostics.SkipReason.ToString());
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            wall.Stop();

            result.WallMs = wall.Elapsed.TotalMilliseconds;
            result.MaxConcurrentSecondary = Volatile.Read(ref maxInFlightSecondary);
            result.MeterFires = meter.Fires;
            result.MeterHedgeWins = meter.HedgeWins;
            result.MeterBudgetExhausted = meter.BudgetExhausted;
            result.Compute();

            Console.WriteLine(
                $"    -> ops={result.TotalOps} hedgeFired={result.HedgeFiredCount} " +
                $"budgetExhausted={result.BudgetExhaustedCount} meterFires={result.MeterFires} " +
                $"maxSecInFlight={result.MaxConcurrentSecondary} p50={result.P50:F1}ms p99={result.P99:F1}ms " +
                $"wall={result.WallMs / 1000:F1}s");

            return result;
        }

        private static void UpdateMax(ref int max, int candidate)
        {
            int snapshot;
            while (candidate > (snapshot = Volatile.Read(ref max)))
            {
                Interlocked.CompareExchange(ref max, candidate, snapshot);
            }
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

        private void WriteScenarioCsv(List<ScenarioResult> all)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("label,partitions,hedgingOn,degraded,thresholdMs,totalOps,hedgeFired,budgetExhausted,primaryWins,meterFires,maxConcurrentSecondary,p50Ms,p95Ms,p99Ms,maxMs,wallSec");
            foreach (ScenarioResult r in all)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    r.Label,
                    r.Partitions.ToString(),
                    r.HedgingOn ? "1" : "0",
                    r.Degraded ? "1" : "0",
                    r.ThresholdMs.ToString("F0"),
                    r.TotalOps.ToString(),
                    r.HedgeFiredCount.ToString(),
                    r.BudgetExhaustedCount.ToString(),
                    r.PrimaryWinCount.ToString(),
                    r.MeterFires.ToString(),
                    r.MaxConcurrentSecondary.ToString(),
                    r.P50.ToString("F2"),
                    r.P95.ToString("F2"),
                    r.P99.ToString("F2"),
                    r.MaxMs.ToString("F2"),
                    (r.WallMs / 1000).ToString("F2"),
                }));
            }
            File.WriteAllText(Path.Combine(OutDir, "scenarios.csv"), sb.ToString());
        }

        private void WriteRegionCsv(List<ScenarioResult> all)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("label,region,sendCount,winCount");
            foreach (ScenarioResult r in all)
            {
                HashSet<string> regions = new HashSet<string>(r.SendsByRegion.Keys);
                regions.UnionWith(r.WinsByRegion.Keys);
                foreach (string region in regions.OrderBy(x => x))
                {
                    r.SendsByRegion.TryGetValue(region, out long sends);
                    r.WinsByRegion.TryGetValue(region, out long wins);
                    sb.AppendLine($"{r.Label},{region},{sends},{wins}");
                }
            }
            File.WriteAllText(Path.Combine(OutDir, "regions.csv"), sb.ToString());
        }

        private void PrintSummary(List<ScenarioResult> all)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================= STRESS SUMMARY =================");
            foreach (ScenarioResult r in all)
            {
                sb.AppendLine(
                    $"{r.Label,-34} ops={r.TotalOps,6} hedged={r.HedgeFiredCount,6} " +
                    $"budgetExh={r.BudgetExhaustedCount,6} maxSecInFlight={r.MaxConcurrentSecondary,3} " +
                    $"p50={r.P50,7:F1} p99={r.P99,8:F1} wall={r.WallMs / 1000,6:F1}s");
                foreach (KeyValuePair<string, long> kv in r.SendsByRegion.OrderByDescending(x => x.Value))
                {
                    sb.AppendLine($"        sends {kv.Key,-18} {kv.Value}   (wins {(r.WinsByRegion.TryGetValue(kv.Key, out long w) ? w : 0)})");
                }
            }
            sb.AppendLine("=================================================");
            Console.WriteLine(sb.ToString());
            File.WriteAllText(Path.Combine(OutDir, "summary.txt"), sb.ToString());
        }

        private sealed class ScenarioResult
        {
            private readonly ConcurrentBag<double> latencies = new ConcurrentBag<double>();

            public string Label;
            public int Partitions;
            public bool HedgingOn;
            public bool Degraded;
            public double ThresholdMs;
            public double WallMs;
            public int MaxConcurrentSecondary;
            public long MeterFires;
            public long MeterHedgeWins;
            public long MeterBudgetExhausted;

            public ConcurrentDictionary<string, long> SendsByRegion { get; } = new ConcurrentDictionary<string, long>();
            public ConcurrentDictionary<string, long> WinsByRegion { get; } = new ConcurrentDictionary<string, long>();

            private long hedgeFired;
            private long budgetExhausted;
            private long primaryWins;

            public int TotalOps { get; private set; }
            public long HedgeFiredCount => Volatile.Read(ref this.hedgeFired);
            public long BudgetExhaustedCount => Volatile.Read(ref this.budgetExhausted);
            public long PrimaryWinCount => Volatile.Read(ref this.primaryWins);
            public double P50 { get; private set; }
            public double P95 { get; private set; }
            public double P99 { get; private set; }
            public double MaxMs { get; private set; }

            public void RecordSend(string region)
            {
                this.SendsByRegion.AddOrUpdate(region, 1, (_, v) => v + 1);
            }

            public void RecordOp(double latencyMs, bool hedgeFired, string winningRegion, string skipReason)
            {
                this.latencies.Add(latencyMs);
                if (hedgeFired)
                {
                    Interlocked.Increment(ref this.hedgeFired);
                }
                if (string.Equals(skipReason, "BudgetExhausted", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref this.budgetExhausted);
                }
                this.WinsByRegion.AddOrUpdate(winningRegion, 1, (_, v) => v + 1);
            }

            public void Compute()
            {
                double[] sorted = this.latencies.ToArray();
                Array.Sort(sorted);
                this.TotalOps = sorted.Length;
                this.P50 = Percentile(sorted, 0.50);
                this.P95 = Percentile(sorted, 0.95);
                this.P99 = Percentile(sorted, 0.99);
                this.MaxMs = sorted.Length > 0 ? sorted[sorted.Length - 1] : 0;

                long pw = this.TotalOps - this.HedgeFiredCount - this.BudgetExhaustedCount;
                Volatile.Write(ref this.primaryWins, pw < 0 ? 0 : pw);
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
        }

        /// <summary>
        /// Listens to the SDK's real metadata-hedging meter
        /// (<c>Azure.Cosmos.Client.MetadataHedging</c>) for authoritative hedge counts.
        /// </summary>
        private sealed class HedgeMeterCollector : IDisposable
        {
            private const string MeterName = "Azure.Cosmos.Client.MetadataHedging";
            private readonly MeterListener listener;
            private long fires;
            private long hedgeWins;
            private long budgetExhausted;

            public long Fires => Volatile.Read(ref this.fires);
            public long HedgeWins => Volatile.Read(ref this.hedgeWins);
            public long BudgetExhausted => Volatile.Read(ref this.budgetExhausted);

            public HedgeMeterCollector()
            {
                this.listener = new MeterListener
                {
                    InstrumentPublished = (instrument, l) =>
                    {
                        if (instrument.Meter.Name == MeterName)
                        {
                            l.EnableMeasurementEvents(instrument);
                        }
                    },
                };
                this.listener.SetMeasurementEventCallback<long>((inst, measurement, tags, state) =>
                {
                    if (inst.Name.EndsWith("fires", StringComparison.Ordinal))
                    {
                        Interlocked.Add(ref this.fires, measurement);
                    }
                    else if (inst.Name.EndsWith("hedge_wins", StringComparison.Ordinal))
                    {
                        Interlocked.Add(ref this.hedgeWins, measurement);
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
                Volatile.Write(ref this.hedgeWins, 0);
                Volatile.Write(ref this.budgetExhausted, 0);
            }

            public void Dispose() => this.listener?.Dispose();
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
