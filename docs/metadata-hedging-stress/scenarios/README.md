# Metadata Hedging — Phased PR-vs-main Scenarios (PR #5923)

> **⚠️ SUPERSEDED.** Like the parent harness, this one *simulates* latency with `Task.Delay`
> and calls `MetadataHedgingStrategy.ExecuteAsync` directly. Its "−65 % p50" headline is a
> **refresh-subset** number (end-to-end p50 is unchanged). For the authoritative **real
> fault-injection** validation, see [`../faultinjection/README.md`](../faultinjection/README.md).
> Kept here for history.

Companion to [`../README.md`](../README.md). These artifacts compare the **PR (metadata
hedging)** against **main (no metadata hedging)** across four phased timelines requested
for review. Both arms run the **exact same workload** against the same live 5-region
account; the **only** difference is whether metadata-cache reads can hedge.

> Stress/validation artifacts only. The harness ADDS a test
> (`Microsoft.Azure.Cosmos.Tests/Routing/MetadataHedgingScenarioHarness.cs`); **no PR/src
> code was modified.**

## Arms
* **PR** — the real `MetadataHedgingStrategy` (`customerOptIn=true`).
* **main** — metadata reads go **primary-only** (`customerOptIn=false`), exactly main's
  behaviour (main has no metadata hedging at all).

The real `MetadataHedgingStrategy.ExecuteAsync` drives the metadata step against the real
`GlobalEndpointManager` of a live `CosmosClient` (5 regions: West US 2 hub · East US ·
South Central US · Central US · North Central US).

## End-to-end read model
`read latency = data-read step + metadata-refresh step`

* **data-read step** — a shared model of the existing **data-plane "normal hedging"**
  (availability strategy): a slow hub never blocks the data read beyond the data-hedge
  threshold (a secondary serves it). **Identical on both arms**, so it cancels out of the
  PR-vs-main comparison and only provides realistic context.
* **metadata-refresh step** — the **only** differentiator. A refresh is spawned by
  (a) cold-start cache population (Collection `Read` + `PartitionKeyRange` `ReadFeed`) and
  (b) `410 PartitionKeyRange-Gone` during the degraded phase. On **main** it is primary-only
  (stalls on the slow hub); on **PR** it hedges to a secondary.

**Timing** is time-compressed 10× (threshold 150 ms models prod 1.5 s; hub-slow 0.5–1.0 s
models 5–10 s). Concurrency = 50 (a realistic thundering-herd). Cold-start burst = 200
first reads; OK period = 1,500 reads; degraded = 3,000 reads with ~30 % hitting
`410 PartitionKeyRange-Gone`.

## Scenarios
1. **S1 Completely healthy** — hub fast throughout.
2. **S2 Bad cold start** — hub slow during cold start, then healthy.
3. **S3 Bad cold start → OK → degraded** (hub delay + `410 PkRange-Gone`).
4. **S4 Good cold start → OK → degraded.**

## Results — refresh-bearing reads (the reads that actually do a metadata refresh)

| Scenario / phase | main p50 | PR p50 | Δ p50 | main p99 | PR p99 |
| --- | ---: | ---: | ---: | ---: | ---: |
| S2 bad cold start | 1,622 ms | 1,167 ms | **−28 %** | 2,084 ms | 1,972 ms |
| S3 degraded (bad cold) | 875 ms | 310 ms | **−65 %** | 1,125 ms | 1,113 ms |
| S4 degraded (good cold) | 883 ms | 309 ms | **−65 %** | 1,127 ms | 1,112 ms |
| S1 healthy cold start | 68 ms | 64 ms | ~0 | 99 ms | 103 ms |

(Times are 10×-compressed; multiply by ~10 for the production-scale equivalent.)

### Graphs
* `s1_refresh_latency_pr_vs_main.png` — refresh-bearing read latency, PR vs main (headline).
* `s2_timeline_s3.png` — S3 timeline (cold → OK → degraded): main's high stall band vs PR's
  recovered band.
* `s3_calls_per_region.png` — per-region metadata calls: main hits only the hub; PR adds
  bounded hedges to **one** secondary (East US); the other three regions are untouched.
* `s4_hedges_vs_budget.png` — bounded amplification: hedged vs budget-exhausted per phase.
* `s5_healthy_no_amplification.png` — S1: PR == main, 0 hedges, 0 secondary calls.

## Conclusions
1. **Healthy (S1):** PR is identical to main — **0 hedges, 0 secondary calls**.
2. **Bad cold start (S2):** PR cuts the median cold-start metadata latency ~28 % even under a
   50-way thundering herd; the per-client budget caps how many of the simultaneous cold
   reads hedge (the rest fall back to primary-only — bounded amplification).
3. **Degraded + 410 PkRange-Gone (S3/S4):** on **main**, every read that hits PkRange-Gone
   stalls on the slow hub's metadata refresh (median ~0.9 s, i.e. ~9 s at production scale)
   even with data-plane normal hedging, because the routing map must be refreshed first.
   **PR cuts that median ~65 %**; the residual p99 is the budget-exhausted fallback (by
   design — the budget bounds secondary load, so the gateway is never flooded).
4. **Region fan-out** is a single secondary (East US), bounded by the budget, never spread
   across all five regions.

## Reproduce
```powershell
$env:RUN_HEDGE_SCENARIOS = "1"
$env:COSMOSDB_MULTI_REGION = "<connection string to a 4-5 region account>"
$env:HEDGE_SCENARIO_OUTDIR = "<output dir>"
dotnet test -c Release --filter "FullyQualifiedName~MetadataHedgingScenarioHarness"
python plot_scenarios.py <output dir>
```
