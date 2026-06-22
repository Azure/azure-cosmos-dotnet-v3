//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace MetadataHedgingBenchmark
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// In-process simulation harness for the PR #5923 metadata-hedging
    /// amplification / latency analysis. This is NOT a live-account benchmark —
    /// it faithfully models <c>MetadataHedgingStrategy.ExecuteAsync</c>'s
    /// decision logic so the secondary-region fan-out (the "do not bombard the
    /// Gateway" concern) and the latency-tail effect can be measured
    /// deterministically and quickly.
    ///
    /// Modeled invariants (mirrors the production control flow):
    ///   1. Per-client concurrency budget acquired with a non-blocking
    ///      <c>SemaphoreSlim.Wait(TimeSpan.Zero)</c> BEFORE the primary is sent;
    ///      the permit is held for the whole eligible operation (primary + any
    ///      hedge + loser cleanup) and released in a finally.
    ///   2. A hedge fires only if the primary has not produced an acceptable
    ///      response within the threshold (Task.WhenAny(primary, timer)), or the
    ///      primary fast-fails before the threshold.
    ///   3. At most one hedge per logical operation.
    ///   4. Ineligible reads (hedging disabled / single region / unsupported
    ///      type) never touch the budget and never send to a secondary region.
    ///
    /// Wall-clock latencies are compressed 10x vs. production (threshold 150&#160;ms
    /// models 1.5&#160;s) to keep runs fast; only the ratios matter for the
    /// fan-out and tail conclusions.
    /// </summary>
    internal sealed class HedgeSimulator
    {
        private readonly TimeSpan threshold;
        private readonly SemaphoreSlim budget;
        private readonly int budgetCapacity;

        private long primarySends;
        private long secondarySends;
        private long hedgeFires;
        private long budgetExhausted;
        private long eligibleOps;
        private long ineligibleOps;

        private int currentPermits;
        private int maxPermits;
        private int currentSecondaryInFlight;
        private int maxSecondaryInFlight;

        public HedgeSimulator(TimeSpan threshold, int budgetCapacity)
        {
            this.threshold = threshold;
            this.budgetCapacity = budgetCapacity;
            this.budget = new SemaphoreSlim(budgetCapacity, budgetCapacity);
        }

        public long PrimarySends => Interlocked.Read(ref this.primarySends);

        public long SecondarySends => Interlocked.Read(ref this.secondarySends);

        public long HedgeFires => Interlocked.Read(ref this.hedgeFires);

        public long BudgetExhausted => Interlocked.Read(ref this.budgetExhausted);

        public long EligibleOps => Interlocked.Read(ref this.eligibleOps);

        public long IneligibleOps => Interlocked.Read(ref this.ineligibleOps);

        public int MaxPermitsHeld => Volatile.Read(ref this.maxPermits);

        public int MaxSecondaryInFlight => Volatile.Read(ref this.maxSecondaryInFlight);

        /// <summary>Returns the measured winner latency in milliseconds.</summary>
        public async Task<double> ExecuteAsync(OperationSpec op)
        {
            Stopwatch sw = Stopwatch.StartNew();

            // Ineligible: hedging disabled, single-region, unsupported type, etc.
            // Never touches the budget, never sends to a secondary region.
            if (!op.Eligible)
            {
                Interlocked.Increment(ref this.ineligibleOps);
                await this.SendPrimaryAsync(op).ConfigureAwait(false);
                return sw.Elapsed.TotalMilliseconds;
            }

            // Step 2: non-blocking budget check. Exhausted -> primary-only.
            if (!this.budget.Wait(TimeSpan.Zero))
            {
                Interlocked.Increment(ref this.budgetExhausted);
                await this.SendPrimaryAsync(op).ConfigureAwait(false);
                return sw.Elapsed.TotalMilliseconds;
            }

            this.EnterPermit();
            try
            {
                Interlocked.Increment(ref this.eligibleOps);

                Task primaryTask = this.SendPrimaryAsync(op);
                Task timer = Task.Delay(this.threshold);
                Task firstCompleted = await Task.WhenAny(primaryTask, timer).ConfigureAwait(false);

                // 5a. Primary genuinely won before threshold -> no hedge.
                if (firstCompleted == primaryTask && op.PrimaryAcceptable)
                {
                    return sw.Elapsed.TotalMilliseconds;
                }

                // 5b. Threshold elapsed (or primary fast-failed) -> fire one hedge.
                Interlocked.Increment(ref this.hedgeFires);
                Task secondaryTask = this.SendSecondaryAsync(op);

                // First acceptable winner determines the observed latency.
                await Task.WhenAny(primaryTask, secondaryTask).ConfigureAwait(false);
                double winnerMs = sw.Elapsed.TotalMilliseconds;

                // Loser cleanup (background in production) — awaited here so the
                // permit is not released until both branches settle, exactly as
                // BackgroundCleanupAsync holds the slot.
                await Task.WhenAll(primaryTask, secondaryTask).ConfigureAwait(false);
                return winnerMs;
            }
            finally
            {
                this.ExitPermit();
                this.budget.Release();
            }
        }

        private async Task SendPrimaryAsync(OperationSpec op)
        {
            Interlocked.Increment(ref this.primarySends);
            await Task.Delay(op.PrimaryLatency).ConfigureAwait(false);
        }

        private async Task SendSecondaryAsync(OperationSpec op)
        {
            Interlocked.Increment(ref this.secondarySends);
            this.EnterSecondary();
            try
            {
                await Task.Delay(op.SecondaryLatency).ConfigureAwait(false);
            }
            finally
            {
                this.ExitSecondary();
            }
        }

        private void EnterPermit()
        {
            int held = Interlocked.Increment(ref this.currentPermits);
            this.Bump(ref this.maxPermits, held);
        }

        private void ExitPermit() => Interlocked.Decrement(ref this.currentPermits);

        private void EnterSecondary()
        {
            int n = Interlocked.Increment(ref this.currentSecondaryInFlight);
            this.Bump(ref this.maxSecondaryInFlight, n);
        }

        private void ExitSecondary() => Interlocked.Decrement(ref this.currentSecondaryInFlight);

        private void Bump(ref int target, int candidate)
        {
            int snapshot;
            while (candidate > (snapshot = Volatile.Read(ref target)))
            {
                if (Interlocked.CompareExchange(ref target, candidate, snapshot) == snapshot)
                {
                    return;
                }
            }
        }
    }

    internal readonly struct OperationSpec
    {
        public OperationSpec(bool eligible, TimeSpan primaryLatency, TimeSpan secondaryLatency, bool primaryAcceptable)
        {
            this.Eligible = eligible;
            this.PrimaryLatency = primaryLatency;
            this.SecondaryLatency = secondaryLatency;
            this.PrimaryAcceptable = primaryAcceptable;
        }

        public bool Eligible { get; }

        public TimeSpan PrimaryLatency { get; }

        public TimeSpan SecondaryLatency { get; }

        public bool PrimaryAcceptable { get; }
    }

    internal static class Program
    {
        // 10x-compressed model of production timings.
        private static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(150);   // models 1.5 s
        private static readonly TimeSpan FastPrimary = TimeSpan.FromMilliseconds(5);    // models ~50 ms
        private static readonly TimeSpan SlowPrimary = TimeSpan.FromMilliseconds(300);  // models ~3 s degraded
        private static readonly TimeSpan HealthySecondary = TimeSpan.FromMilliseconds(5); // models ~50 ms

        private const int BudgetCapacity = 8;

        public static async Task Main()
        {
            Console.WriteLine("Metadata Hedging — in-process amplification / latency simulation (PR #5923)");
            Console.WriteLine($"threshold(model)={Threshold.TotalMilliseconds}ms  budget={BudgetCapacity}  " +
                              $"fastPrimary={FastPrimary.TotalMilliseconds}ms  slowPrimary={SlowPrimary.TotalMilliseconds}ms  " +
                              $"secondary={HealthySecondary.TotalMilliseconds}ms");
            Console.WriteLine(new string('-', 118));

            // Scenario 1 — healthy primary, modest steady-state concurrency.
            await RunScenarioAsync(
                "S1 Healthy primary, HEDGING OFF",
                count: 5000, maxConcurrency: 8, slowFraction: 0.0,
                eligible: false);

            await RunScenarioAsync(
                "S1 Healthy primary, HEDGING ON ",
                count: 5000, maxConcurrency: 8, slowFraction: 0.0,
                eligible: true);

            Console.WriteLine();

            // Scenario 2 — degraded-primary tail (10% of reads are slow) at modest
            // steady-state concurrency (within budget) so the hedge can fire.
            await RunScenarioAsync(
                "S2 10% slow-primary tail, HEDGING OFF",
                count: 5000, maxConcurrency: 8, slowFraction: 0.10,
                eligible: false);

            await RunScenarioAsync(
                "S2 10% slow-primary tail, HEDGING ON ",
                count: 5000, maxConcurrency: 8, slowFraction: 0.10,
                eligible: true);

            Console.WriteLine();

            // Scenario 3 — worst-case startup burst: every read is slow and all
            // arrive at once. Proves the secondary fan-out is bounded by the budget.
            await RunScenarioAsync(
                "S3 All-slow concurrent burst, HEDGING ON",
                count: 2000, maxConcurrency: 2000, slowFraction: 1.0,
                eligible: true);

            Console.WriteLine();

            // Scenario 4 (warm-path verification) — client-lifetime workload where
            // each cache key is read many times: one cold-start read + recurring
            // refresh reads (410/Gone, forceRefresh, splits). This is the genuinely
            // NEW exposure of opening hedging to the warm path: refreshes RECUR, so
            // the rate of potential hedges over a client's life is higher than
            // cold-start-only. We contrast the two eligibility policies on the SAME
            // workload to quantify exactly what the warm path costs.
            await RunWarmComparisonAsync(
                "S4 Lifetime churn, 10% slow refreshes",
                keys: 500, readsPerKey: 10, slowRefreshFraction: 0.10, maxConcurrency: 8);

            await RunWarmComparisonAsync(
                "S5 Region brownout, 50% slow refreshes",
                keys: 500, readsPerKey: 10, slowRefreshFraction: 0.50, maxConcurrency: 8);

            Console.WriteLine(new string('-', 118));
            Console.WriteLine("Done.");
        }

        /// <summary>
        /// Runs the SAME lifetime workload (per key: 1 cold read + N-1 refresh reads)
        /// twice — once under cold-start-only eligibility (refreshes never hedge) and
        /// once under the broadened warm-enabled eligibility (slow refreshes hedge) —
        /// so the warm-path delta (extra secondary requests, refresh-tail latency) is
        /// directly observable. Cold reads are healthy (fast primary) in both runs.
        /// </summary>
        private static async Task RunWarmComparisonAsync(
            string name, int keys, int readsPerKey, double slowRefreshFraction, int maxConcurrency)
        {
            (long secCold, double p99Cold, long hedgeCold, int maxInflCold) =
                await RunLifetimeAsync(keys, readsPerKey, slowRefreshFraction, maxConcurrency, warmEligible: false);
            (long secWarm, double p99Warm, long hedgeWarm, int maxInflWarm) =
                await RunLifetimeAsync(keys, readsPerKey, slowRefreshFraction, maxConcurrency, warmEligible: true);

            int refreshReads = keys * (readsPerKey - 1);
            Console.WriteLine(
                $"{name,-40} | COLD-ONLY: secondarySends {secCold,5}  refresh-p99 {p99Cold,7:F1}ms" +
                $"   ||   WARM-ON: secondarySends {secWarm,5}  refresh-p99 {p99Warm,7:F1}ms  " +
                $"maxSecondaryInFlight {maxInflWarm,3} (cap {BudgetCapacity}) | " +
                $"extra secondary {secWarm - secCold,5} over {refreshReads,5} refreshes");
        }

        private static async Task<(long secondarySends, double refreshP99, long hedgeFires, int maxInflight)> RunLifetimeAsync(
            int keys, int readsPerKey, double slowRefreshFraction, int maxConcurrency, bool warmEligible)
        {
            HedgeSimulator sim = new HedgeSimulator(Threshold, BudgetCapacity);
            ConcurrentBag<double> refreshLatencies = new ConcurrentBag<double>();
            int slowEvery = slowRefreshFraction <= 0 ? int.MaxValue : Math.Max(1, (int)Math.Round(1.0 / slowRefreshFraction));

            using SemaphoreSlim gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            List<Task> tasks = new List<Task>(keys * readsPerKey);
            int refreshIndex = 0;

            for (int k = 0; k < keys; k++)
            {
                for (int r = 0; r < readsPerKey; r++)
                {
                    bool isCold = r == 0;
                    bool isSlow = !isCold && (Interlocked.Increment(ref refreshIndex) % slowEvery == 0);

                    // Cold reads are always eligible; refresh reads are eligible only
                    // under the warm-enabled policy.
                    OperationSpec op = new OperationSpec(
                        eligible: isCold || warmEligible,
                        primaryLatency: isSlow ? SlowPrimary : FastPrimary,
                        secondaryLatency: HealthySecondary,
                        primaryAcceptable: true);

                    await gate.WaitAsync().ConfigureAwait(false);
                    bool recordRefresh = !isCold;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            double ms = await sim.ExecuteAsync(op).ConfigureAwait(false);
                            if (recordRefresh)
                            {
                                refreshLatencies.Add(ms);
                            }
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            double[] sorted = refreshLatencies.OrderBy(x => x).ToArray();
            return (sim.SecondarySends, Percentile(sorted, 99), sim.HedgeFires, sim.MaxSecondaryInFlight);
        }

        private static async Task RunScenarioAsync(
            string name, int count, int maxConcurrency, double slowFraction, bool eligible)
        {
            HedgeSimulator sim = new HedgeSimulator(Threshold, BudgetCapacity);
            ConcurrentBag<double> latencies = new ConcurrentBag<double>();

            // Deterministic slow/fast assignment for reproducibility.
            int slowEvery = slowFraction <= 0 ? int.MaxValue : Math.Max(1, (int)Math.Round(1.0 / slowFraction));

            using SemaphoreSlim gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            Stopwatch wall = Stopwatch.StartNew();

            List<Task> tasks = new List<Task>(count);
            for (int i = 0; i < count; i++)
            {
                bool isSlow = slowFraction >= 1.0 || (slowFraction > 0 && (i % slowEvery == 0));
                OperationSpec op = new OperationSpec(
                    eligible: eligible,
                    primaryLatency: isSlow ? SlowPrimary : FastPrimary,
                    secondaryLatency: HealthySecondary,
                    primaryAcceptable: true);

                await gate.WaitAsync().ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        double ms = await sim.ExecuteAsync(op).ConfigureAwait(false);
                        latencies.Add(ms);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            wall.Stop();

            double[] sorted = latencies.OrderBy(x => x).ToArray();
            double p50 = Percentile(sorted, 50);
            double p99 = Percentile(sorted, 99);
            double max = sorted.Length == 0 ? 0 : sorted[^1];

            Console.WriteLine(
                $"{name,-40} | p50 {p50,6:F1}ms  p99 {p99,7:F1}ms  max {max,7:F1}ms | " +
                $"primarySends {sim.PrimarySends,5}  hedgeFires {sim.HedgeFires,5}  secondarySends {sim.SecondarySends,5} | " +
                $"budgetExhausted {sim.BudgetExhausted,5}  maxSecondaryInFlight {sim.MaxSecondaryInFlight,3} (cap {BudgetCapacity})");
        }

        private static double Percentile(double[] sorted, double pct)
        {
            if (sorted.Length == 0)
            {
                return 0;
            }

            double rank = (pct / 100.0) * (sorted.Length - 1);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            if (lo == hi)
            {
                return sorted[lo];
            }

            double frac = rank - lo;
            return (sorted[lo] * (1 - frac)) + (sorted[hi] * frac);
        }
    }
}
