# PKRange Lookup Optimization — Pre-Optimization Baseline

Tracking issue: [kirankumarkolli/ThinClient#1](https://github.com/kirankumarkolli/ThinClient/issues/1)

Related micro-benchmark (in-process routing map only): `CollectionRoutingMapBenchmark`.

This document records the **pre-optimization** baseline for the end-to-end Direct-mode
point-read hot path that issue #1's routing-map lookup optimization is expected to improve.
It exists so the impact of that optimization can be quantified against a committed baseline
rather than a re-run "before" number that may drift as unrelated changes land on master.

## Scope

The benchmark in scope is `DirectModeRoutingBenchmark.ReadItemStream`, defined at
`Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/Benchmarks/DirectModeRoutingBenchmark.cs`.

It exercises:

- A real, unmodified `CosmosClient` constructed with `ConnectionMode.Direct`.
- A 17,329-range routing map loaded from the committed
  `Data/shared_conversations_pkranges.tsv` dataset (real production-shape distribution
  from a v2-hash collection — fixed 32-char hex boundaries).
- A deterministic 1,024-entry random PK pool (seed=42) that is cycled per iteration so the
  routing-map lookup sees varied effective partition keys.
- The full `ReadItemStreamAsync` request pipeline from `Container` down to (but not through)
  the RNTBD transport.

It does **not** exercise: TCP/sockets, IMDS probes, the emulator, a live account, or any
real network I/O. Gateway HTTP is mocked at the `HttpMessageHandler` seam
(`Mocks/PkRangeMetadataHandler.cs`) and RNTBD is mocked at the `TransportClient` seam
(`Mocks/DirectStubTransport.cs`). The `GatewayAddressCache` is pre-warmed during
`[GlobalSetup]` so the measured iteration issues **zero** gateway HTTP calls and **exactly
one** transport invocation.

## Why this benchmark matters for issue #1

`CollectionRoutingMapBenchmark` isolates `routingMap.GetRangeByEffectivePartitionKey` at
the micro level (~45–69 ns per lookup at 50K ranges, varying by the string-vs-numeric
fast path). That's a useful lower-bound signal but it does not answer the question
"how much does this save the SDK's actual hot path?". `DirectModeRoutingBenchmark` does:
it measures the fraction of a ~58 µs end-to-end `ReadItemStreamAsync` that is dominated by
(a) routing-map lookup, (b) address resolution, and (c) the surrounding per-call
allocations. Issue #1's optimization should visibly move the µs and — more importantly —
the per-operation allocation counter in this benchmark.

## Baseline result

Full `DefaultJob` BenchmarkDotNet run of `DirectModeRoutingBenchmark.ReadItemStream`:

```
BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.26200.8246), VM=Hyper-V
Unknown processor
.NET SDK=10.0.102
  [Host]     : .NET 6.0.36 (6.0.3624.51421), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.36 (6.0.3624.51421), X64 RyuJIT AVX2
```

| Method         | Mean     | Error    | StdDev   | Gen0   | Allocated |
|--------------- |---------:|---------:|---------:|-------:|----------:|
| ReadItemStream | 57.74 μs | 0.724 μs | 0.642 μs | 0.9766 |  26.41 KB |

14 measured iterations, 1 outlier removed, 99.9% CI margin = 1.25% of mean. See
`BenchmarkDotNet.Artifacts/results/` for the full HTML/CSV reports.

> **VM caveat:** BDN emits an environment warning because this run was on a Hyper-V VM.
> Virtualization adds noise but does not invalidate the *delta* between a pre- and
> post-optimization run on the same host. Post-optimization comparison runs must be
> executed on the same machine, with the same `dotnet` SDK, on a quiet system.

## How to reproduce

```pwsh
# From the bench-direct-pointread worktree root
dotnet build .\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Performance.Tests\Microsoft.Azure.Cosmos.Performance.Tests.csproj -c Release
dotnet run --project .\Microsoft.Azure.Cosmos\tests\Microsoft.Azure.Cosmos.Performance.Tests\Microsoft.Azure.Cosmos.Performance.Tests.csproj -c Release --no-build -- --filter "*DirectModeRoutingBenchmark*"
```

Run time is ~1 minute for the default job. The `--verify-stage4` entry point in
`Program.cs` is a fast (~1 s) smoke check that the harness is still wired correctly
without paying for a full BDN measurement cycle; run it whenever the handler or transport
mocks change.

## What the optimization should move

Issue #1 changes the routing-map lookup to avoid a repeated string comparison / hex-parse
on every `GetRangeByEffectivePartitionKey` call at large range counts. At 17,329 ranges,
the per-iteration routing-map lookup is well inside the hot path. A successful change
should show:

- **Lower Mean µs** — the micro-benchmark suggests ~15–20 ns of savings per lookup at this
  scale; measurable but small as a fraction of 58 µs. Larger savings would be a red flag
  that something else moved.
- **Lower or equal Allocated bytes** — the optimization's explicit goal is allocation
  reduction. The 26.41 KB/op baseline is the allocation budget to chase. Any regression
  here is a blocker.
- **Unchanged Gen0/Gen1 counts** outside of noise. Allocations that drop out of the hot
  path should appear as a reduction in Gen0 collects per 1000 ops.

## Related files

- `Data/shared_conversations_pkranges.tsv` — source dataset (17,329 PKRanges).
- `Data/PkRangeRoutingFactory.cs` — dataset loader and PK-pool generator.
- `Mocks/PkRangeMetadataHandler.cs` — `/` + `/dbs` + `/colls` + `/pkranges` + `/addresses`
  gateway mock.
- `Mocks/DirectStubTransport.cs` — RNTBD seam mock that delegates to
  `MockRequestHelper.GetStoreResponse`.
- `Benchmarks/DirectModeRoutingBenchmark.cs` — the benchmark class itself.
- `docs/bench-direct-pointread-spike-findings.md` — non-obvious JSON/env gotchas
  discovered while building this harness.
