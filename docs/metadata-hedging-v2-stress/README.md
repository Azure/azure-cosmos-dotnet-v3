# Metadata Hedging (v2, PR #5999) — REAL Fault-Injection Validation

Stress/validation artifacts for the **simplified** metadata hedging PR
[#5999](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5999) (v2 of issue #5917). It
reruns the **same fault-injection methodology** used for the original PR #5923 — inject a
real transport-layer delay on the hub region's Gateway metadata ops and drive **real SDK
reads** (cold-start `ReadItemAsync`, forced `PartitionKeyRange` refreshes) so the strategy is
exercised through its real call sites — adapted to the v2 API.

> Test/docs only — adds `Microsoft.Azure.Cosmos/FaultInjection/tests/MetadataHedgingV2FaultInjectionHarness.cs`;
> modifies **no** `Microsoft.Azure.Cosmos/src/**` code. Gated behind `RUN_HEDGE_FI=1` +
> `[TestCategory("StressHarness")]`, so it never runs in CI.

## What's different in v2 (and how the harness adapts)

| Aspect | Original #5923 | Simplified v2 #5999 | Harness impact |
| --- | --- | --- | --- |
| **Per-client concurrency budget** | `SemaphoreSlim(8)` — max 8 concurrent hedges; excess falls back to primary-only (`BudgetExhausted`) | **Removed** — every eligible slow read hedges | The saturating / fast-fail scenarios now hedge **100%** of slow reads; there is no "budget-exhausted" fallback to report |
| **Telemetry** | OpenTelemetry meter `Azure.Cosmos.Client.MetadataHedging` | **Removed** — a single per-request trace datum `"Metadata Hedge"` (only when a hedge fires) | Hedge counts come from that trace datum (`HedgeFired` / `HedgeWon` / `WinningRegion`), captured by passing a real `Trace` into the forced refresh |
| **Correctness model** | eligibility + skip reasons + diagnostics object | **"the primary is authoritative"** — a hedge can only win by being faster *and* good; it can never override a definitive primary | Observed via winning-region attribution: hedges only WIN from a secondary under a degraded hub; a healthy hub always wins |

Everything else is the same: live 5-region account (`nalu-new`: West US 2 hub · East US ·
South Central US · Central US · North Central US), `ResponseDelay` 3 s on the hub's
`MetadataContainer` + `MetadataPartitionKeyRange` (compression of prod 5–10 s; the metadata
HTTP timeout ladder is 1 s → 5 s → 65 s, so OFF lands ~4 s while ON hedges at the 1.5 s
threshold), A/B = ON (`AZURE_COSMOS_METADATA_HEDGING_ENABLED=true`) vs OFF (`=false`).

## Results (compressed mode — hub delay 3 s; see `fi_scenarios.csv` / `summary.txt`)

| Measurement point | main p50 | PR p50 | Δ p50 | main p99 | PR p99 | hedged (fired/n) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cold start (end-to-end `ReadItemAsync`) | 12,326 ms | 3,616 ms | **−71 %** | 12,534 ms | 3,722 ms | latency-inferred¹ |
| Refresh — low contention | 4,034 ms | 1,593 ms | **−61 %** | 4,096 ms | 1,769 ms | 30 / 30 |
| Refresh — **saturating storm** | 4,036 ms | 1,596 ms | **−60 %** | 4,082 ms | 1,891 ms | **48 / 48** |
| Mixed — refresh subset | 3,925 ms | 1,757 ms | **−55 %** | 4,033 ms | 1,836 ms | (coalesced)² |
| Mixed — **end-to-end** (70 % warm reads) | 72 ms | 51 ms | ~0 | 4,033 ms | 1,836 ms | — |
| Fast-fail brownout (hub 503, no delay) | 94 ms | 92 ms | ~0 | 408 ms | 320 ms | **48 / 48** |

¹ Cold start is reported on **latency** (the direct end-to-end proof); the trace-datum count is
not reliably surfaced through `ItemResponse.Diagnostics` for the internal cache-population reads.
² In the mixed workload the 15 refresh ops target the **same** container, so their concurrent
forced refreshes single-flight (coalesce) into one metadata read that all callers share — hence
one `HedgeFired` but all 15 get the ~1.8 s hedged latency.

### Graphs
Each PNG carries an embedded **"What it shows / What it proves"** analysis box.

- **`fi1_latency_pr_vs_main.png`** — real fault-injected latency (p50/p99) at the four points.
  *Proves:* v2 delivers the same latency win as the original (cold **−71 %**, refresh **−60 %**);
  the saturating **p99 also recovers** (no budget-exhausted primary-only tail — see fi3).
- **`fi2_hedge_attribution.png`** — hedges fired vs won, from the PR's `"Metadata Hedge"` datum.
  *Proves:* every hedge that fires also **wins** (`HedgeWon == HedgeFired`); refresh-low 30/30,
  saturating **48/48**, fast-fail **48/48** — with no budget, **every** slow read hedges.
- **`fi3_saturating_no_budget_cdf.png`** — saturating-storm latency CDF. *Proves:* v2 recovers
  the **entire** distribution near ~1.6 s; there is no ~4 s tail (the original had one for ops
  beyond its cap of 8). Better tail — the flip side is uncapped fan-out (fi6).
- **`fi4_mixed_e2e_honest.png`** — mixed end-to-end. *Proves:* **p50 unchanged** (warm reads
  dominate); only the metadata-refresh tail (p95/p99) improves. Honest targeted-tail win.
- **`fi5_fastfail_no_budget.png`** — 503 fast-fail brownout. *Proves:* every fast-fail hedges
  and wins (48/48); main issues zero. The amplification bound is now the slow-read rate +
  one-hedge-per-op, not a hard concurrency cap.
- **`fi6_amplification_tradeoff.png`** — **the headline difference.** *Proves:* the original
  capped concurrent hedges at a budget of 8; v2 removes it, so a brownout hedges **every**
  concurrent slow read (48 here). Simpler + better tail, but the per-client 8× ceiling is gone.

## Conclusions
1. **Same latency win** — cold start **−71 %** (12.3 s → 3.6 s), refresh **−60 %** (4.0 s →
   1.6 s), in real fault-injected wall-clock. Removing the budget did not cost the benefit.
2. **Behaviour is causal & correct** — hedges fire only ON (30/30, 48/48, 48/48), main = 0, and
   every fired hedge **wins** from a healthy secondary. The "primary is authoritative" invariant
   holds: all wins come from a single secondary region (East US — see `fi_winregions.csv`); a
   healthy hub is never overridden.
3. **Honest end-to-end** — warm-read-dominated mixed p50 is unchanged; hedging helps the
   metadata-refresh tail, not every op.
4. **The simplification tradeoff (fi6)** — v2 is simpler (no `SemaphoreSlim`/budget bookkeeping)
   and has a **better tail** (no budget-exhausted primary-only fallback), but its secondary-region
   amplification under a primary brownout is bounded only by the slow-read rate + one-hedge-per-op,
   **not** by a hard concurrency cap. Under a wide brownout v2 issues a hedge for **every**
   concurrent slow metadata read (100 %), where the original guaranteed at most the budget (8)
   concurrently. All hedges still target one secondary region, and a healthy hub never hedges.

A genuinely degraded-Azure-region drill (no synthetic delay) remains a pre-GA step.

> Harness note: the wide fast-fail / saturating waves can surface a pre-existing thread-safety
> race in the **FaultInjection test framework** (`FaultInjectionServerErrorResultInternal.IsApplicable`
> enumerates a per-rule history list while a concurrent request appends to it) — unrelated to the
> hedging code; the harness retries that transient so the numbers are clean (`failures = 0`).

## Reproduce
```powershell
$env:RUN_HEDGE_FI = "1"
$env:COSMOSDB_MULTI_REGION = "<connection string to a 4-5 region account>"
$env:HEDGE_FI_MODE = "compressed"   # or "realtime" (8s, no compression) or "probe"
$env:HEDGE_FI_OUTDIR = "<output dir>"
dotnet test -c Release --filter "FullyQualifiedName~MetadataHedgingV2FaultInjectionHarness"
python plot_fi_v2.py <output dir>
```
