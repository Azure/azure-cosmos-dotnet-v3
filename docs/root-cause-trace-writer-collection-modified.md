# Root cause: `InvalidOperationException: Collection was modified` in `TraceWriter` during cross-region read hedging

| Field | Value |
|---|---|
| Symptom | `System.InvalidOperationException: Collection was modified; enumeration operation may not execute.` thrown from `TraceWriter.TraceJsonWriter.WriteJsonUriArrayWithDuplicatesCounted` |
| Surface call site | `CosmosOpenTelemetryExtensions.LogCosmosDiagnostics` → `TraceWriter.TraceJsonWriter.WriteTrace` → `Visit(ClientSideRequestStatisticsTraceDatum)` |
| Reporter | Internal livesite report (account and container identifiers redacted) |
| Affected SDK window | Surfaced in 3.56 → 3.58 with cross-region read hedging enabled; class of bug is pre-existing |
| Severity | Customer-observable exception in diagnostics serialization (can be re-thrown into the caller depending on host) |

## TL;DR

`ClientSideRequestStatisticsTraceDatum` exposes `ContactedReplicas`, `FailedReplicas`, and `RegionsContacted` as **raw mutable** `List<T>` / `HashSet<T>` instances. Three sibling collections on the same class (`EndpointToAddressResolutionStatistics`, `StoreResponseStatisticsList`, `HttpResponseStatisticsList`) already return a defensive snapshot under a lock; these three do not. Under cross-region read hedging, the Direct-package store-reader paths populate the raw collections from one thread while ResourceStack's OpenTelemetry pipeline serializes the diagnostics tree from another thread. The serializer's `foreach` walks straight into a `List<T>.Enumerator.MoveNext()` that observes a version mismatch and throws.

## Stack trace (from the report)

```
System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
   at System.Collections.Generic.List`1.Enumerator.MoveNext()
   at Microsoft.Azure.Cosmos.Tracing.TraceWriter.TraceDatumJsonWriter.WriteJsonUriArrayWithDuplicatesCounted(String propertyName, IReadOnlyList`1 uris)
   at Microsoft.Azure.Cosmos.Tracing.TraceWriter.TraceDatumJsonWriter.Visit(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
   at Microsoft.Azure.Cosmos.Tracing.TraceWriter.WriteTraceDatum(IJsonWriter writer, Object value)
   at Microsoft.Azure.Cosmos.Tracing.TraceWriter.TraceJsonWriter.WriteTrace(IJsonWriter writer, ITrace trace, Boolean isRootTrace)
   …
   at Microsoft.WindowsAzure.ResourceStack.RegionalStore.Cosmos.Extensions.CosmosOpenTelemetryExtensions.LogCosmosDiagnostics(...)
   at Microsoft.WindowsAzure.ResourceStack.RegionalStore.Cosmos.Extensions.CosmosOpenTelemetryExtensions.TraceCosmosDbRequestWithOpenTelemetry[T,U,V](...)
   at Microsoft.WindowsAzure.ResourceStack.RegionalStore.Cosmos.CosmosContainer`1.QueryNextPageAsync[T](...)
```

The inner-most frame is `List<T>.Enumerator.MoveNext()`. That is the canonical "list mutated during enumeration" failure mode.

## Root cause

### The asymmetric design in `ClientSideRequestStatisticsTraceDatum`

[Microsoft.Azure.Cosmos/src/Tracing/TraceData/ClientSideRequestStatisticsTraceDatum.cs](../Microsoft.Azure.Cosmos/src/Tracing/TraceData/ClientSideRequestStatisticsTraceDatum.cs) holds six collections of diagnostic data. Three of them already serialize safely; three of them do not:

| Property | Backing | Read pattern | Safe? |
|---|---|---|---|
| `EndpointToAddressResolutionStatistics` (L52) | `Dictionary` + `lock` + cached `shallowCopyOf…` | snapshot under lock | ✅ |
| `StoreResponseStatisticsList` (L80) | `List` + `lock` + cached `shallowCopyOf…` | snapshot under lock | ✅ |
| `HttpResponseStatisticsList` (L97) | `List` + `lock` + cached `shallowCopyOf…` | snapshot under lock | ✅ |
| **`ContactedReplicas`** (L70) | `public List<TransportAddressUri> { get; set; }` | raw reference, no lock | ❌ |
| **`FailedReplicas`** (L72) | `public HashSet<TransportAddressUri> { get; }` | raw reference, no lock | ❌ |
| **`RegionsContacted`** (L74) | `public HashSet<(string, Uri)> { get; }` | raw reference, no lock | ❌ |

The three raw properties exist because the contract they satisfy is `Microsoft.Azure.Documents.IClientSideRequestStatistics` (shipped from the Direct package). The Direct package's store-reader paths mutate those collections by directly calling `requestStats.ContactedReplicas.Add(uri)`, `requestStats.FailedReplicas.Add(uri)`, and `requestStats.RegionsContacted.Add((region, uri))` — there is no setter method we can intercept and no lock the V3 SDK can guarantee around those writes.

### Why this fires under cross-region read hedging

Cross-region read hedging fires multiple parallel store-reader calls per logical operation. All of them share the **same** `ClientSideRequestStatisticsTraceDatum` instance (because they belong to the same `ITrace`). The sequence that produces the exception:

1. Operation begins. A shared `ClientSideRequestStatisticsTraceDatum` is created and attached to the trace.
2. Hedging dispatches readers to multiple regions. Each reader, on completion, mutates the shared `ContactedReplicas` / `FailedReplicas` / `RegionsContacted` from a thread-pool thread inside the Direct package.
3. ResourceStack's OpenTelemetry helper (`CosmosOpenTelemetryExtensions.LogCosmosDiagnostics`) is invoked on the orchestrating thread before all hedged readers have necessarily completed. It calls `TraceWriter.TraceJsonWriter.WriteTrace`, which eventually calls `Visit(ClientSideRequestStatisticsTraceDatum)`.
4. The visitor `foreach`es `clientSideRequestStatisticsTraceDatum.ContactedReplicas` to count duplicates ([WriteJsonUriArrayWithDuplicatesCounted](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceJsonWriter.cs#L549)).
5. A still-active hedging reader calls `Add(...)` on the same `List<T>`. `List<T>` increments its `_version` field on every structural mutation, and `List<T>.Enumerator.MoveNext()` checks `_version` on every step.
6. Mismatch → `InvalidOperationException`.

### Vulnerable read sites in the SDK

All sites enumerate the three raw collections without taking a snapshot or a lock:

- [TraceWriter.TraceJsonWriter.cs:202](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceJsonWriter.cs#L202) — `ContactedReplicas` (this exception's site)
- [TraceWriter.TraceJsonWriter.cs:204](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceJsonWriter.cs#L204) — `RegionsContacted`
- [TraceWriter.TraceJsonWriter.cs:205](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceJsonWriter.cs#L205) — `FailedReplicas`
- [TraceWriter.TraceTextWriter.cs:345](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceTextWriter.cs#L345), [L366](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceTextWriter.cs#L366), [L372](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceTextWriter.cs#L372) — same three fields, text writer
- [TraceSummary.cs:80](../Microsoft.Azure.Cosmos/src/Tracing/TraceSummary.cs#L80) — `regionContactedInternal.UnionWith(...RegionsContacted)`
- [TraceData/SummaryDiagnostics.cs:67](../Microsoft.Azure.Cosmos/src/Tracing/TraceData/SummaryDiagnostics.cs#L67) — `foreach … in regionsContacted`

The aggregators (`TraceSummary`, `SummaryDiagnostics`) run during diagnostics conversion when a customer touches `Diagnostics.ToString()`, `GetContactedRegions()`, or when OTel logs the trace — the same hedging window applies.

### Family of bugs

This is structurally the same as the `ArgumentNullException: request` hedging race fixed in 3.57.1 — same shared `ClientSideRequestStatisticsTraceDatum`, different field. Whenever the SDK leaks a raw mutable collection across the boundary between Direct-package writers and v3 readers, it has the potential to throw under hedging.

## Fix approach

The mutating threads live in the Direct package; we cannot wrap their writes in a lock from v3. The fix is therefore applied at the **read site**: every place that enumerates one of the three unsafe collections takes a **defensive snapshot with retry on transient concurrency exceptions** before iterating. The snapshot copies into a fresh array, retrying up to five times if the indexer or enumerator raises one of the transient exceptions (`InvalidOperationException`, `ArgumentException`, `IndexOutOfRangeException`) that indicate the source mutated mid-copy. After max retries the helper returns an empty snapshot, so diagnostics serialization can fail soft (drop the property) instead of bubbling an exception out of `Diagnostics.ToString()` and into customer code.

### Why not change the property types?

- `ContactedReplicas` / `FailedReplicas` / `RegionsContacted` satisfy `IClientSideRequestStatistics` (Direct package) and are mutated from outside this assembly. Changing the types breaks the cross-package contract and the existing `public List<…>` / `public HashSet<…>` shape that downstream code (including test/perf code in this repo) relies on.
- The Direct-package writers do not call back into v3, so we cannot intercept their `Add` calls. A `SynchronizedCollection<T>`-style wrapper would still be `Add`-able from the Direct package, but it would change the runtime type returned by the property.

A defensive snapshot at the v3-side read boundary is the smallest surgical change that closes the hedging race without breaking the existing contract.

### Why not just `try / catch` around the serializer?

`try { foreach } catch (InvalidOperationException) { /* swallow */ }` would leak a partially-written JSON property (open array, missing array-end) and corrupt the output. The snapshot pattern produces a complete, well-formed JSON document every time.

### Why not just lock on the property getter?

The getter returns the raw `List<T>` / `HashSet<T>` reference, so any lock taken inside the getter is released before the consumer iterates. Locking is only useful if the same lock is held during mutation — which we cannot do from this side of the package boundary.

## Changes in this PR

### 1. New helper — defensive collection snapshot with retry

[Microsoft.Azure.Cosmos/src/Tracing/TraceData/ConcurrentCollectionSnapshot.cs](../Microsoft.Azure.Cosmos/src/Tracing/TraceData/ConcurrentCollectionSnapshot.cs)

- `SnapshotList<T>(IReadOnlyList<T>)` — copies by index, tolerating `Count` shrinkage and item-slot races, retrying up to 5 times.
- `SnapshotCollection<T>(IEnumerable<T>)` — copies by enumerator, retrying up to 5 times on transient concurrency exceptions.
- Returns `Array.Empty<T>()` on null input or after max retries, so callers can always iterate the result safely.

### 2. Snapshot at every vulnerable read site

| File | Change |
|---|---|
| [TraceWriter.TraceJsonWriter.cs](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceJsonWriter.cs) | `Visit(ClientSideRequestStatisticsTraceDatum)` snapshots `ContactedReplicas` / `RegionsContacted` / `FailedReplicas` into local `IReadOnlyList`s before the existing helpers iterate them. |
| [TraceWriter.TraceTextWriter.cs](../Microsoft.Azure.Cosmos/src/Tracing/TraceWriter.TraceTextWriter.cs) | `Visit(ClientSideRequestStatisticsTraceDatum)` snapshots `ContactedReplicas` once (reused for the "Contacted Replicas" and "Regions Contacted" sections, which both read it today) and `FailedReplicas` before iteration. |
| [TraceSummary.cs](../Microsoft.Azure.Cosmos/src/Tracing/TraceSummary.cs) | `UpdateRegionContacted` snapshots `RegionsContacted` before `UnionWith`. |
| [TraceData/SummaryDiagnostics.cs](../Microsoft.Azure.Cosmos/src/Tracing/TraceData/SummaryDiagnostics.cs) | `AggregateRegionsContacted` snapshots its input before the `foreach`. |

The properties themselves and the JSON/text wire format are unchanged. In the common case there is no observable behavior change other than the elimination of the race. The one exception is the rare worst case where a collection mutates on every one of the five copy attempts: the affected property then falls back to an empty snapshot (it is dropped from the serialized output, and can momentarily under-report `GetContactedRegions()`) rather than throwing. That fallback is logged at verbose level via `DefaultTrace` so the drop is diagnosable.

### 3. Regression tests

[Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Tracing/ClientSideRequestStatisticsTraceDatumTests.cs](../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Tracing/ClientSideRequestStatisticsTraceDatumTests.cs) adds two stress tests:

- `ConcurrentSerializationOfMutableCollectionsDoesNotThrow` — background thread continuously `Add`s and periodically `Clear`s `ContactedReplicas`, `FailedReplicas`, `RegionsContacted`; main thread serializes the trace hundreds of times via `CosmosTraceDiagnostics.ToString()`. Reproduces the original `InvalidOperationException` reliably without the fix.
- `ConcurrentTraceSummaryAggregationDoesNotThrow` — same race against `TraceSummary.UpdateRegionContacted` / `UnionWith`.

Both tests have a 20s timeout, mirroring the existing `ConcurrentUpdate*` tests in the same file.

### 4. Changelog

Added to [changelog.md](../changelog.md) under `### Unreleased` → `Bugs Fixed`. Customer-facing language because the exception is observable to anyone who upgrades and enables OTel logging with hedging.

## Verification

| Step | Status |
|---|---|
| Build the v3 SDK | passes (no diagnostics in the touched files) |
| New stress tests pass | expected to pass with the fix; verified to throw before the fix |
| Existing `ConcurrentUpdate*` tests pass | unchanged behavior |
| Existing baseline tests (`TraceWriterBaselineTests`, `DuplicateContactedReplicasTests`) | unchanged JSON output |

## Out of scope (deliberate, follow-up candidates)

- The pre-existing typo in `TraceTextWriter.cs:372` where the "Regions Contacted" section iterates `ContactedReplicas` rather than `RegionsContacted` is preserved as-is. Fixing it changes text-writer output and warrants its own PR with baseline-test updates.
- `ClientCollectionCache.cs:244` stores `response.RequestStats.RegionsContacted` (the raw `HashSet`) into `TelemetryInformation.RegionsContactedList`, which is later iterated on telemetry timer threads ([ClientTelemetryHelper.GetContactedRegions](../Microsoft.Azure.Cosmos/src/Telemetry/ClientTelemetryHelper.cs#L111)). The same class of race exists there. The current report is for the trace-writer path; a follow-up should snapshot at the `ClientCollectionCache` assignment too.
- `PartitionKeyRangeCache.GetRoutingMapForCollectionAsync` ([PartitionKeyRangeCache.cs#L226](../Microsoft.Azure.Cosmos/src/Routing/PartitionKeyRangeCache.cs#L226)) eagerly evaluates `string.Join(", ", response.RequestStats.RegionsContacted)` for a `DefaultTrace.TraceInformation` log, enumerating the same raw `RegionsContacted` `HashSet`. If `RequestStats` is a shared hedged datum still being mutated by a Direct-package reader, this can throw the identical `InvalidOperationException` on a metadata read path (broader blast radius than diagnostics serialization). Deferred to a follow-up: snapshot via `ConcurrentCollectionSnapshot.SnapshotCollection` (or build the log argument lazily) once we confirm whether change-feed / PKRange reads run under hedging with a shared, still-mutating datum.
- A longer-term cleanup is to deprecate the public mutable property shape in favor of `AddContactedReplica` / `AddFailedReplica` / `AddRegionContacted` methods on `IClientSideRequestStatistics` (cross-package change to the Direct package). That is the only way to make these collections internally thread-safe end-to-end.
