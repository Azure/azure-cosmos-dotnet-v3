## Context

The Azure Cosmos DB .NET SDK v3 supports cross-region hedging via `CrossRegionHedgingAvailabilityStrategy`. When the primary request exceeds a latency threshold, the SDK sends parallel "hedge" requests to additional regions and returns the first successful response.

Currently, hedging metadata is embedded in the diagnostics trace tree using `AddOrUpdateDatum` with keys `"Hedge Context"`, `"Hedge Config"`, and `"Response Region"`. This data is only accessible by calling `Diagnostics.ToString()` (which materializes the full JSON trace) and parsing the output string. This approach is:

1. **Expensive** — `ToString()` walks the entire trace tree and serializes to JSON
2. **Fragile** — consumers must know the exact key names and JSON structure
3. **Invisible** — there is no discoverable API surface; users must read SDK source to learn the pattern

The `CosmosDiagnostics` abstract class already exposes fast O(1) metadata methods (`GetClientElapsedTime()`, `GetFailedRequestCount()`, `GetStartTimeUtc()`). Adding hedging detection follows this established pattern.

## Goals / Non-Goals

**Goals:**
- Expose a zero-allocation, O(1) API to detect whether a response resulted from hedging
- Expose all regions that received hedge requests, matching the data already present in the diagnostics trace
- Maintain full backward compatibility (virtual methods with safe defaults)
- Follow established patterns in `CosmosDiagnostics` for consistency

**Non-Goals:**
- Exposing the full hedge configuration (threshold, step, regions list) through public API — this is already available in diagnostics JSON for debugging
- Changing the existing diagnostics trace data structure or serialization
- Adding hedging detection to `CosmosException` (it already carries `CosmosDiagnostics` which will have the new methods)
- Supporting non-hedging availability strategies (e.g., future retry strategies) — those can be added later

## Decisions

### Decision 1: Methods on `CosmosDiagnostics` rather than `ResponseMessage`

**Choice**: Add `IsHedged()` and `GetHedgedRegions()` as virtual methods on `CosmosDiagnostics`.

**Rationale**: `CosmosDiagnostics` is the established home for request metadata (`GetClientElapsedTime()`, `GetContactedRegions()`, etc.). Placing hedging info here keeps the API surface consistent and means it's automatically available through `CosmosException.Diagnostics` as well — no separate exception handling path needed.

**Alternative considered**: Properties on `ResponseMessage`. Rejected because `ResponseMessage` would need internal wiring from the hedging strategy, and the pattern doesn't match — `ResponseMessage` delegates metadata to `Diagnostics`.

### Decision 2: Boolean flag + list field on `CosmosTraceDiagnostics` (not trace Data dictionary lookup)

**Choice**: Add `internal bool isHedged` and `internal IReadOnlyList<string> hedgedRegions` fields directly to `CosmosTraceDiagnostics`. The hedging strategy sets these fields at the same call site where it already calls `AddOrUpdateDatum`.

**Rationale**: Accessing `ITrace.Data` requires calling `SetWalkingStateRecursively()` first (which locks and walks the entire trace tree). A direct field on the diagnostics object is a true O(1) read with zero side effects — no locking, no tree walking, no dictionary lookup. The hedged regions list mirrors the `"Hedge Context"` datum already stored in the trace data, giving users the same information through a first-class API.

**Alternative considered**: Dictionary lookup on `ITrace.Data["Hedge Context"]`. Rejected due to `SetWalkingStateRecursively()` requirement making it not truly O(1) and violating the "no performance impact" constraint.

### Decision 3: Virtual methods with default implementations (not abstract)

**Choice**: `IsHedged()` returns `false` and `GetHedgedRegions()` returns an empty `IReadOnlyList<string>` by default.

**Rationale**: `CosmosDiagnostics` is `public abstract` — any new abstract member would be a breaking change for anyone who subclasses it (including mock frameworks in tests). Virtual methods with safe defaults preserve backward compatibility, following the exact same pattern used by `GetClientElapsedTime()`, `GetFailedRequestCount()`, and `GetStartTimeUtc()`.

### Decision 4: Method names `IsHedged()` and `GetHedgedRegions()`

**Choice**: `bool IsHedged()` — matches the semantic question "was this request hedged?" `IReadOnlyList<string> GetHedgedRegions()` — returns all regions that received hedge requests, in the order they were dispatched. This mirrors the `"Hedge Context"` datum already stored in the diagnostics trace, making the same information available through a first-class, strongly-typed API.

**Rationale**: Consistent with existing naming (`GetClientElapsedTime`, `GetContactedRegions`, `GetFailedRequestCount`). Using methods rather than properties matches the established convention in `CosmosDiagnostics` where all accessors are methods. Returning all hedged regions (rather than just the winning region) gives users the full picture needed for observability — they can see exactly which regions were involved and correlate with latency data.

## Risks / Trade-offs

**[Risk] Internal cast in hedging strategy** → The hedging strategy already casts `Diagnostics` to `CosmosTraceDiagnostics` to access the trace. Setting the new fields uses the same cast — no additional coupling introduced.

**[Risk] Flag not set on non-hedging code paths** → By design. The flag defaults to `false`/empty list, and only the hedging strategy sets it. If new availability strategies are added, they would need to set the flag independently. Mitigation: document this contract in the internal field comments.

**[Trade-off] Two separate methods vs. a composite return type** → Two simple methods are easier to use in conditional checks (`if (diagnostics.IsHedged())`) than unpacking a composite object. Returning all hedged regions as `IReadOnlyList<string>` provides the full observability picture. If richer hedging metadata is needed later (e.g., per-region latency), a `GetHedgingInfo()` method returning a structured type can be added without breaking these methods.

**[Trade-off] `GetHedgedRegions()` returns an empty list for non-hedged requests** → This makes it clearly tied to hedging and avoids null-checking. The existing `GetContactedRegions()` already covers the general "which region(s) were used" question.
