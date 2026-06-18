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

## Results (representative run)

```
threshold(model)=150ms  budget=8  fastPrimary=5ms  slowPrimary=300ms  secondary=5ms
----------------------------------------------------------------------------------------------------------------------
S1 Healthy primary, HEDGING OFF  | p50  15.2ms  p99   16.2ms  max  16.4ms | primarySends 5000  hedgeFires    0  secondarySends    0 | budgetExhausted    0  maxSecondaryInFlight 0 (cap 8)
S1 Healthy primary, HEDGING ON   | p50  15.3ms  p99   16.3ms  max  17.4ms | primarySends 5000  hedgeFires    0  secondarySends    0 | budgetExhausted    0  maxSecondaryInFlight 0 (cap 8)

S2 10% slow-primary tail, HEDGING OFF | p50 15.3ms  p99 309.8ms  max 314.3ms | primarySends 5000  hedgeFires   0  secondarySends   0 | budgetExhausted   0  maxSecondaryInFlight 0 (cap 8)
S2 10% slow-primary tail, HEDGING ON  | p50 15.3ms  p99 170.8ms  max 179.7ms | primarySends 5000  hedgeFires 500  secondarySends 500 | budgetExhausted   0  maxSecondaryInFlight 3 (cap 8)

S3 All-slow concurrent burst, HEDGING ON | p50 312.3ms  p99 313.8ms  max 318.7ms | primarySends 2000  hedgeFires 8  secondarySends 8 | budgetExhausted 1992  maxSecondaryInFlight 8 (cap 8)
----------------------------------------------------------------------------------------------------------------------
```

## Conclusions

**1. No negative impact on healthy reads (the common case).**
S1 — when the primary is healthy, `HEDGING ON` and `HEDGING OFF` are within noise
(p50 15.3 vs 15.2 ms, p99 16.3 vs 16.2 ms). **Zero** hedges fire and **zero**
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
S2 — `HEDGING ON` cuts p99 from **309.8 ms → 170.8 ms** (~45 %). This is the whole
point of broadening to refresh reads: a degraded primary inflates the tail on
refresh reads just as it does on cold start, and the hedge recovers it.

## Mapping to the production guarantees

| Concern | Safeguard exercised | Result |
| --- | --- | --- |
| Extra latency on healthy reads | 1.5 s threshold gates the hedge | S1: identical p50/p99, 0 hedges |
| Gateway bombardment under load | per-client budget (`Wait(0)`, default 8) | S3: ≤ 8 secondary in flight from a 2000-read burst |
| Per-read amplification | one-hedge-per-operation latch + threshold | S2: 1 secondary per slow read, 0 per healthy read |
| Tail latency on slow primary | cross-region hedge | S2: p99 309.8 → 170.8 ms |

These match the safeguards described in `docs/PPAF_Metadata_Hedging_ColdStart_Design.md`
§5.11 (budget) and §5.9 (threshold), now validated for the broadened
(cold-start **and** refresh) eligibility surface.

## Caveat

This is an in-process model of the decision logic, not a live-account
measurement. It is the right tool for the fan-out / tail questions this change
raises; a full live-account validation against a real degraded region remains a
pre-GA step in the rollout plan (design §12).
