# Metadata Hedging — Benchmark & Amplification Analysis (PR #5923)

This branch (`nalutripician/metadata-hedging-benchmark`) carries the benchmark
harness and results backing the decision to **extend metadata hedging beyond
cold start** to steady-state refresh reads. It is intentionally kept **off** the
feature branch / PR.

## Why a simulation harness

The behaviour we need to validate — "does broadening to refresh reads hurt
latency or bombard the Gateway?" — is a property of the
`MetadataHedgingStrategy.ExecuteAsync` *decision logic* (threshold race, budget
semaphore, one-hedge-per-operation latch), not of any specific Cosmos account.
Reproducing it against a live multi-region account is non-deterministic
(depends on real region health) and not available in CI. The harness in
`MetadataHedgingBenchmark/` therefore re-implements that exact control flow and
measures it deterministically:

- Per-client budget acquired with a non-blocking `SemaphoreSlim.Wait(TimeSpan.Zero)`
  **before** the primary is sent, held for the whole eligible operation, released
  in a `finally`.
- A hedge fires only if the primary has not produced an acceptable response
  within the threshold (`Task.WhenAny(primary, timer)`), or fast-fails first.
- At most one hedge per logical operation.
- Ineligible reads never touch the budget and never reach a secondary region.

Wall-clock latencies are compressed 10× (threshold 150 ms models the production
1.5 s); only the *ratios* matter for the fan-out and tail conclusions.

## How to run

```bash
cd Microsoft.Azure.Cosmos.Samples/Tools/MetadataHedgingBenchmark
dotnet run -c Release
```

## Scenarios

- **S1** — healthy primary, steady-state reads (does turning hedging ON cost anything?).
- **S2** — 10 % slow-primary tail (does the hedge recover the tail, and how many secondary requests does it cost?).
- **S3** — 2000 simultaneous all-slow reads (is the secondary fan-out bounded by the budget?).
- **S4 / S5** — **warm-path verification.** A client-lifetime workload (per cache key:
  1 cold read + 9 recurring refresh reads) run under *both* cold-start-only and
  warm-enabled eligibility, so the cost of opening hedging to refresh reads is
  measured directly. S4 = 10 % slow refreshes (normal churn); S5 = 50 % slow
  refreshes (region brownout).

## Results (representative run)

```
threshold(model)=150ms  budget=8  fastPrimary=5ms  slowPrimary=300ms  secondary=5ms
----------------------------------------------------------------------------------------------------------------------
S1 Healthy primary, HEDGING OFF  | p50  15.1ms  p99   16.3ms | primarySends 5000  hedgeFires    0  secondarySends    0 | maxSecondaryInFlight 0
S1 Healthy primary, HEDGING ON   | p50  15.2ms  p99   16.4ms | primarySends 5000  hedgeFires    0  secondarySends    0 | maxSecondaryInFlight 0

S2 10% slow-primary tail, HEDGING OFF | p50 15.3ms  p99 311.5ms | primarySends 5000  hedgeFires   0  secondarySends   0 | maxSecondaryInFlight 0
S2 10% slow-primary tail, HEDGING ON  | p50 15.4ms  p99 174.7ms | primarySends 5000  hedgeFires 500  secondarySends 500 | maxSecondaryInFlight 3 (cap 8)

S3 All-slow concurrent burst, HEDGING ON | p50 329.5ms  p99 334.0ms | primarySends 2000  hedgeFires 8  secondarySends 8 | budgetExhausted 1992  maxSecondaryInFlight 8 (cap 8)

S4 Lifetime churn, 10% slow refreshes  | COLD-ONLY: secondarySends    0  refresh-p99 310.7ms || WARM-ON: secondarySends  450  refresh-p99 171.9ms  maxSecondaryInFlight 1 (cap 8) | extra secondary  450 over 4500 refreshes
S5 Region brownout, 50% slow refreshes | COLD-ONLY: secondarySends    0  refresh-p99 313.5ms || WARM-ON: secondarySends 2250  refresh-p99 179.1ms  maxSecondaryInFlight 4 (cap 8) | extra secondary 2250 over 4500 refreshes
----------------------------------------------------------------------------------------------------------------------
```

## Conclusions

**1. No negative impact on healthy reads (the common case).**
S1 — when the primary is healthy, `HEDGING ON` and `HEDGING OFF` are within noise
(p50 15.2 vs 15.1 ms, p99 16.4 vs 16.3 ms). **Zero** hedges fire and **zero**
secondary requests are issued, because a healthy primary always wins before the
1.5 s threshold. Broadening eligibility to refresh reads adds no steady-state cost.

**2. The Gateway is not bombarded — secondary fan-out is hard-bounded.**
- S2 — with a 10 % slow-primary tail, the number of secondary (hedge) requests is
  exactly **500 / 5000 = 10 %**: one hedge per slow read, and **none** for the
  90 % of healthy reads. Secondary load is proportional to the *slow-read rate*,
  not the total read rate.
- S3 — a worst-case startup storm of **2000 simultaneous all-slow reads** produces
  only **8** secondary requests (`maxSecondaryInFlight = 8 = budget cap`). The
  other **1992** reads hit `BudgetExhausted` and fall back to primary-only. The
  per-client concurrency budget is the structural ceiling on secondary-region
  pressure regardless of how broad the eligibility surface is.

**3. Real tail-latency win when the primary is actually slow.**
S2 — `HEDGING ON` cuts p99 from **311.5 ms → 174.7 ms** (~44 %). This is the whole
point of broadening to refresh reads: a degraded primary inflates the tail on
refresh reads just as it does on cold start, and the hedge recovers it.

## Warm-path verification (S4 / S5) — the cost of opening to refresh reads

The genuinely **new** exposure of broadening hedging beyond cold start is that
**refresh reads recur** over a client's lifetime (410/Gone, forceRefresh,
partition splits), whereas a cold-start read happens once per cache key. S4/S5
run the *same* client-lifetime workload (per key: 1 cold read + 9 refresh reads)
under both eligibility policies so the warm-path delta is directly measurable.

**There is a quantifiable cost, and it is bounded:**

| Scenario | Cold-only secondary | Warm-on secondary | Extra (over 4500 refreshes) | Max in-flight | Refresh p99 cold→warm |
| --- | --- | --- | --- | --- | --- |
| S4 — 10 % slow refreshes | 0 | 450 | **+450** (= the slow-refresh count) | 1 (cap 8) | 310.7 → 171.9 ms |
| S5 — brownout, 50 % slow | 0 | 2250 | **+2250** (= the slow-refresh count) | 4 (cap 8) | 313.5 → 179.1 ms |

- **The extra secondary requests equal the slow-refresh count exactly** — one hedge
  per slow refresh, **zero** for healthy refreshes. So the warm-path cost scales
  with how degraded the primary is, not with total refresh traffic.
- **Still hard-bounded by the per-client budget**: even at a 50 % slow-refresh
  brownout, concurrent secondary requests peak at **4 ≤ 8 (cap)**. The warm path
  cannot exceed the same ceiling that bounds the cold path.
- **It buys the intended benefit**: refresh-tail p99 drops ~**44–45 %** in both
  cases — the same win cold start gets, now extended to refresh reads.

**Bottom line:** opening to the warm path is *not* free — it adds secondary-region
requests proportional to the slow-refresh rate — but the cost is structurally
capped by the existing concurrency budget, is zero when the primary is healthy,
and is paid only to recover a real refresh-tail latency regression.

## Mapping to the production guarantees

| Concern | Safeguard exercised | Result |
| --- | --- | --- |
| Extra latency on healthy reads | 1.5 s threshold gates the hedge | S1: identical p50/p99, 0 hedges |
| Gateway bombardment under load | per-client budget (`Wait(0)`, default 8) | S3/S5: ≤ 8 secondary in flight |
| Per-read amplification | one-hedge-per-operation latch + threshold | S2/S4: 1 secondary per slow read, 0 per healthy read |
| Warm-path recurrence cost | budget + threshold + healthy-primary skip | S4/S5: extra secondary = slow-refresh count, in-flight ≤ 4 |
| Tail latency on slow primary | cross-region hedge | S2/S4/S5: refresh p99 cut ~44–45 % |

These match the safeguards described in `docs/PPAF_Metadata_Hedging_ColdStart_Design.md`
§5.11 (budget) and §5.9 (threshold), now validated for the broadened
(cold-start **and** refresh) eligibility surface.

## Caveat

This is an in-process model of the decision logic, not a live-account
measurement. It is the right tool for the fan-out / tail questions this change
raises; a full live-account validation against a real degraded region remains a
pre-GA step in the rollout plan (design §12).
