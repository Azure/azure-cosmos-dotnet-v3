# Metadata Hedging — REAL Fault-Injection Validation (PR #5923)

**This is the authoritative validation for PR #5923.** It supersedes the two earlier
simulated harnesses (`../README.md` and `../scenarios/README.md`), which faked latency
with `Task.Delay` and called `MetadataHedgingStrategy.ExecuteAsync` directly.

> Test/docs only — adds `Microsoft.Azure.Cosmos/FaultInjection/tests/MetadataHedgingFaultInjectionHarness.cs`;
> modifies **no** `Microsoft.Azure.Cosmos/src/**` code.

## What is real here (and what is not)

**Real:**
- **Transport-layer delays** are injected with the SDK's own **Fault Injection** framework
  (`Microsoft.Azure.Cosmos.FaultInjection`): a region-scoped `ResponseDelay` on the **hub
  (West US 2)** Gateway metadata operations (`MetadataContainer` + `MetadataPartitionKeyRange`).
  Secondary regions get no rule, so a hedge can win.
- **Real SDK read path.** The harness drives real operations — cold-start `ReadItemAsync`
  on a fresh client (populates the Collection + PkRange caches) and forced
  `PartitionKeyRangeCache.TryGetOverlappingRangesAsync(..., forceRefresh: true)` (a real
  `PartitionKeyRange` ReadFeed) — so the strategy is exercised through its **real call
  sites** in `ClientCollectionCache` / `PartitionKeyRangeCache`, not by calling
  `ExecuteAsync` directly.
- **Real end-to-end latency** of those SDK operations (wall-clock `Stopwatch`).
- **Live 5-region topology** (`nalu-new`): West US 2 (hub) · East US · South Central US ·
  Central US · North Central US.
- **Authoritative hedge counts** from the SDK meter `Azure.Cosmos.Client.MetadataHedging`
  via a `MeterListener` (not harness-side counting).

**Not real / remaining for pre-GA (design §12):** the slow hub is a *synthetic* injected
delay, not a genuinely degraded Azure region; a real degraded-region drill is still the
final step. The 3 s hub delay is a feasibility compression of the production 5–10 s; a
`realtime` mode (8 s, no compression) is included and behaves identically in ratio.

## A/B arms (same fault injection, same workload, same account)
- **PR (ON)** — `AZURE_COSMOS_METADATA_HEDGING_ENABLED=true`.
- **main (OFF)** — `AZURE_COSMOS_METADATA_HEDGING_ENABLED=false` (main's behaviour: no
  metadata hedging).

The HTTP metadata timeout ladder is **1 s → 5 s → 65 s**, so a 3 s hub `ResponseDelay`
makes the hub's first attempt time out (~1 s) and the second return real data (~3 s) →
OFF lands at ~4 s; ON dispatches a hedge to a healthy secondary at the 1.5 s threshold.

## Results (compressed mode — hub delay 3 s; see `fi_scenarios.csv` / `summary.txt`)

| Measurement point | main p50 | PR p50 | Δ p50 | main p99 | PR p99 | meter fires (PR / main) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cold start (end-to-end `ReadItemAsync`) | 12,463 ms | 3,693 ms | **−70 %** | 12,622 ms | 3,750 ms | 16 / 0 |
| Refresh — low contention | 4,033 ms | 1,592 ms | **−61 %** | 4,047 ms | 1,762 ms | 30 / 0 |
| Refresh — saturating storm | 4,034 ms | 1,604 ms | **−60 %** | 4,105 ms | 4,057 ms | 32 / 0 |
| Mixed — refresh subset | 3,921 ms | 1,859 ms | **−53 %** | 4,045 ms | 1,923 ms | 1 / 0 |
| Mixed — **end-to-end** (70 % warm reads) | 48 ms | 34 ms | ~0 | 4,045 ms | 1,923 ms | 1 / 0 |

### Graphs
- `fi1_latency_pr_vs_main.png` — headline p50/p99, PR vs main, at the four points.
- `fi2_meter_crosscheck.png` — hedges fire **only** with the PR; main = 0 (SDK meter).
- `fi3_saturating_budget_cdf.png` — latency CDF of the saturating storm: ~67 % of PR ops
  recover at ~1.6 s (hedged), the **budget-exhausted ~33 % (16/48)** fall back to
  primary-only and match main at ~4 s. This is the per-client budget (8) bound, observed.
- `fi4_mixed_e2e_honest.png` — mixed workload end-to-end: **p50 unchanged** (warm reads
  dominate), only the metadata-refresh tail (p95/p99) improves.

## Conclusions
1. **Behaviour actually changes** — ON fires hedges (meter-confirmed: 16/30/32), OFF fires
   **zero**. The A/B is wired correctly to the PR opt-in.
2. **Real latency win on metadata reads** — cold start **−70 %** (12.5 s → 3.7 s), refresh
   **−60 %** (4.0 s → 1.6 s) in real fault-injected wall-clock.
3. **Honest end-to-end framing** — for a mixed workload dominated by warm reads, the
   **end-to-end p50 is unchanged**; hedging helps the **metadata-refresh tail**, not every
   operation. The earlier simulated "−65 % p50" was a refresh-subset number, reproduced
   here (`−53 %` to `−61 %` on the refresh subset) but **not** an end-to-end p50 claim.
4. **Budget bound is real and observed** — under a saturating storm of 12 distinct
   containers, exactly the budget's worth hedge (32 fired) and the remainder
   (**16 budget-exhausted**) fall back to primary-only — the gateway is not flooded.
5. **Region fan-out** is a single secondary (East US); the other three regions are untouched.

## Why these numbers differ from the simulated harness
- The simulated harness's headline "−65 % p50" was computed over a **filtered 31 %
  refresh-bearing subset**; its end-to-end p50 was unchanged. This harness reports **both**
  end-to-end and the refresh subset, and labels which is which.
- The favorable simulated number came from a **low-contention** path. Here both regimes are
  shown: low contention (full −61 %) and a **saturating storm** where the median win is
  preserved for hedged ops but ~⅓ are budget-bounded — so the aggregate win shrinks, as it
  must.
- Latencies here are **real injected transport delays through the real SDK read path**, not
  `Task.Delay` constants.

## Reproduce
```powershell
$env:RUN_HEDGE_FI = "1"
$env:COSMOSDB_MULTI_REGION = "<connection string to a 4-5 region account>"
$env:HEDGE_FI_MODE = "compressed"   # or "realtime" (8s, no compression) or "probe"
$env:HEDGE_FI_OUTDIR = "<output dir>"
dotnet test -c Release --filter "FullyQualifiedName~MetadataHedgingFaultInjectionHarness"
python plot_fi.py <output dir>
```
The test is `Assert.Inconclusive` without `RUN_HEDGE_FI=1` and a live `COSMOSDB_MULTI_REGION`,
and carries `[TestCategory("StressHarness")]`, so it never runs in CI.
