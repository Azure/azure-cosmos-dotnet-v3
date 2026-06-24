# Metadata Hedging — Worst-Case Stress Test (PR #5923)

> **⚠️ SUPERSEDED.** This harness *simulates* latency with `Task.Delay` constants and calls
> `MetadataHedgingStrategy.ExecuteAsync` directly (only the region topology is live). For the
> authoritative **real fault-injection** validation — real transport delays through the real
> SDK read path, with end-to-end *and* refresh-subset latency reported — see
> [`faultinjection/README.md`](./faultinjection/README.md). Kept here for history.

This folder holds the artifacts for a **worst-case stress test** of the metadata
hedging feature added in PR #5923. It validates the behaviour reviewers care about:
when the SDK goes from healthy → **the hub (write) region's gateway has a 5–10 s delay
on all calls (especially metadata)** and reads return **PartitionKeyRange‑Gone** (which
spawns `PartitionKeyRange` ReadFeed *refresh* reads), the hedging code stays sound and
**does not bombard the gateway**, even with **10k–50k partitions**.

> These are stress/validation artifacts only. The harness ADDS a test
> (`Microsoft.Azure.Cosmos.Tests/Routing/MetadataHedgingStressHarness.cs`); **no PR/src
> code was modified.** Results reflect the current PR state.

## Methodology

The harness drives the **real** `MetadataHedgingStrategy.ExecuteAsync` — the actual PR
code — against a **real** `GlobalEndpointManager` obtained from a live `CosmosClient`
connected to a **5‑region** account (`nalu-new`):

```
West US 2 (hub/write) · East US · South Central US · Central US · North Central US
```

The strategy exposes a `sendToEndpoint` delegate (its transport seam). The harness injects
a degraded‑hub transport through it:

* **Hub region (West US 2):** 5–10 s delay on every metadata call, then returns
  `410 Gone` / `PartitionKeyRangeGone`.
* **Secondary regions:** fast, healthy `200 OK`.

Each **PartitionKeyRange‑Gone event = one `PartitionKeyRange` ReadFeed refresh read**
driven through `ExecuteAsync` with a fresh context (`IsColdStart=false`,
`IsFirstReadFeedPage=true` — the steady‑state refresh path, the worst case for
amplification). Hedge counts are cross‑checked against the SDK's **own meter**
(`Azure.Cosmos.Client.MetadataHedging`) via a `MeterListener` — `meterFires` matches the
measured `hedgeFired` exactly in every scenario.

**Timing.** The three high‑partition runs (100 / 10k / 50k) are **time‑compressed 10×**
(threshold 150 ms models the production 1.5 s; hub delay 0.5–1.0 s models 5–10 s) so a
50,000‑op storm finishes in ~90 s. Only ratios and structural bounds matter — the same
methodology already accepted on the benchmark branch. One extra run
(`Degraded-P100-REALTIME`) uses the **true production timing** (1.5 s threshold,
5–10 s hub) to show real wall‑clock behaviour.

## Results

See `scenarios.csv`, `regions.csv`, `summary.txt`.

| Scenario | ops (PkRange refreshes) | hedged (= meter fires) | budget‑exhausted | max concurrent secondary | p50 | p99 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Healthy baseline (hub fast) | 2,000 | **0** | 1,920 | **0** | 16 ms | 32 ms |
| Degraded P=100 (ON) | 100 | 8 | 92 | **8** | 729 ms | 992 ms |
| Degraded P=10,000 (ON) | 10,000 | 800 | 9,200 | **8** | 734 ms | 1,002 ms |
| Degraded P=50,000 (ON) | 50,000 | 4,054 | 45,946 | **8** | 737 ms | 1,001 ms |
| **P=100 REAL timing (1.5 s / 5–10 s)** | 100 | 56 | 44 | **8** | **1.57 s** | 9.91 s |

### Graphs
* `g1_calls_per_region.png` — per‑region metadata calls. The hub takes every primary send;
  hedge fan‑out concentrates on the **2nd preferred region (East US)**; the other three
  regions are never touched.
* `g2_spawned_vs_hedged.png` — PkRange refreshes spawned vs hedged vs budget‑exhausted.
* `g3_realtime_latency_win.png` — production‑timing latency: median recovered to **1.57 s**
  vs **5–10 s** with hedging off.
* `g4_secondary_inflight_cap.png` — **the key graph**: max concurrent secondary requests
  stays **= 8** (the per‑client budget) whether 100 or **50,000** partitions storm at once.
* `g5_healthy_no_amplification.png` — healthy hub ⇒ **zero** hedges, **zero** secondary calls.

## Conclusions

1. **No amplification when healthy.** A healthy hub wins before the 1.5 s threshold ⇒ 0
   hedges, 0 secondary calls (g5).
2. **The gateway is not bombarded — even at 50k partitions.** Concurrent secondary fan‑out
   is hard‑bounded by the per‑client budget (**max 8 in flight**) regardless of storm size
   (g4). Total secondary requests equal the number actually hedged; the rest fall back to
   primary‑only (`BudgetExhausted`). This is the intended bounded‑amplification trade‑off.
3. **Real tail recovery.** At production timing the median PkRange refresh recovered to
   **1.57 s** (hedge wins from East US) vs the 5–10 s degraded hub (g3). The
   budget‑exhausted remainder intentionally falls back to primary‑only rather than
   exceeding the budget — so the p99 tail equals the hub delay by design, not a defect.
4. **Hedge target is deterministic & single.** All hedges go to exactly one region (the 2nd
   preferred), never fanning out across all 5 (g1).

## Reproduce

```powershell
$env:RUN_HEDGE_STRESS = "1"
$env:COSMOSDB_MULTI_REGION = "<connection string to a 4–5 region account>"
$env:HEDGE_STRESS_OUTDIR = "<output dir>"
dotnet test -c Release --filter "FullyQualifiedName~MetadataHedgingStressHarness"
python plot_hedge_stress.py <output dir>
```
