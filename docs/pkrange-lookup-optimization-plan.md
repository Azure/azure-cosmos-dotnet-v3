# PartitionKeyRange Lookup Optimization Plan

## Problem Statement

`CollectionRoutingMap` uses `List<Range<string>>.BinarySearch` with ordinal string comparison for partition key range lookups. At 50K ranges with 128-bit hex EPKs (32 chars), this means ~16 string comparisons per lookup and ~35-40 MB memory per cached collection. We want to optimize for high-partition-count collections while preserving correctness for all EPK formats.

## Scope: Single-Document CRUD Priority

The primary optimization target is the **single-document CRUD hot path**:
```
Request → AddressResolver.ResolvePartitionKeyRangeIdentity()
        → routingMap.GetRangeByEffectivePartitionKey(epk)
```

This means `GetRangeByEffectivePartitionKey` is the critical method. `GetOverlappingRanges` (used by queries, change feed, feed ranges) is a secondary concern.

## Design Approach: Layered Optimization (String-First, Numeric Fast-Path)

The hex string approach must remain the **primary representation** because:
- Non-hash partitioning (V1/range) uses variable-length EPK strings
- Hierarchical partition keys produce composite multi-segment EPKs
- Wire format, cross-SDK compatibility, and diagnostics all depend on string EPKs
- Most collections have <1000 ranges where strings are fast enough

Optimizations are additive layers that don't replace the string foundation. They are ordered by risk/impact ratio.

## Todos

### Phase 1: Zero-Risk String-Level Improvements

All changes are in `Microsoft.Azure.Cosmos/src/Routing/CollectionRoutingMap.cs`.

---

#### 1. pre-extracted-string-array

**What**: Add a `string[]` field containing just the MinInclusive boundary strings, extracted once at construction time.

**File**: `CollectionRoutingMap.cs`

**Current state** (lines 28-29):
```csharp
private readonly List<PartitionKeyRange> orderedPartitionKeyRanges;
private readonly List<Range<string>> orderedRanges;
```

**Change — Add new field** (after line 29):
```csharp
private readonly string[] sortedMinBoundaries;
```

**Change — Populate in constructor** (after line 52, after `orderedRanges` is built):
```csharp
this.sortedMinBoundaries = new string[orderedPartitionKeyRanges.Count];
for (int i = 0; i < orderedPartitionKeyRanges.Count; i++)
{
    this.sortedMinBoundaries[i] = this.orderedRanges[i].Min;
}
```

**Why**: `Array.BinarySearch<string>(string[], string, StringComparer)` is more JIT-friendly than `List<Range<string>>.BinarySearch(Range<string>, IComparer<Range<string>>)`. The array has contiguous string references (better cache locality) and avoids virtual dispatch through the `IComparer<Range<string>>` interface to reach the inner `string.CompareOrdinal`.

**Risk**: None — additive field, no behavior change yet. This is consumed by tasks 2 and 3.

---

#### 2. zero-alloc-point-lookup

**What**: Rewrite `GetRangeByEffectivePartitionKey` to use `sortedMinBoundaries` with a zero-allocation binary search.

**File**: `CollectionRoutingMap.cs`

**Current code** (lines 165-189):
```csharp
public PartitionKeyRange GetRangeByEffectivePartitionKey(string effectivePartitionKeyValue)
{
    if (string.CompareOrdinal(effectivePartitionKeyValue, 
        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey) >= 0)
    {
        throw new ArgumentException("effectivePartitionKeyValue");
    }

    if (string.CompareOrdinal(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, 
        effectivePartitionKeyValue) == 0)
    {
        return this.orderedPartitionKeyRanges[0];
    }

    int index = this.orderedRanges.BinarySearch(
        new Range<string>(effectivePartitionKeyValue, effectivePartitionKeyValue, true, true),  // ← ALLOCATION
        Range<string>.MinComparer.Instance);                                                     // ← IComparer dispatch

    if (index < 0)
    {
        index = ~index - 1;
        Debug.Assert(index >= 0);
        Debug.Assert(this.orderedRanges[index].Contains(effectivePartitionKeyValue));
    }

    return this.orderedPartitionKeyRanges[index];
}
```

**Replacement**:
```csharp
public PartitionKeyRange GetRangeByEffectivePartitionKey(string effectivePartitionKeyValue)
{
    if (string.CompareOrdinal(effectivePartitionKeyValue, 
        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey) >= 0)
    {
        throw new ArgumentException("effectivePartitionKeyValue");
    }

    if (string.CompareOrdinal(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, 
        effectivePartitionKeyValue) == 0)
    {
        return this.orderedPartitionKeyRanges[0];
    }

    // Zero-allocation binary search on the flat string array.
    // Array.BinarySearch uses StringComparer.Ordinal directly — no wrapper object needed.
    int index = Array.BinarySearch(
        this.sortedMinBoundaries,
        effectivePartitionKeyValue,
        StringComparer.Ordinal);

    if (index < 0)
    {
        // ~index is the insertion point; the containing range is one before that
        index = ~index - 1;
        Debug.Assert(index >= 0);
        Debug.Assert(this.orderedRanges[index].Contains(effectivePartitionKeyValue));
    }

    return this.orderedPartitionKeyRanges[index];
}
```

**What this eliminates**:
- `new Range<string>(epk, epk, true, true)` — heap allocation of a Range object per call
- `IComparer<Range<string>>` virtual dispatch — `MinComparer.Compare` internally does `string.CompareOrdinal(left.Min, right.Min)`, but goes through interface → class → extract `.Min` → compare. Now it's `StringComparer.Ordinal.Compare(string, string)` directly
- The `Range<string>.MinComparer` only compares `.Min` fields anyway, which is exactly what the flat `string[]` stores

**Debug.Assert preservation**: The `orderedRanges[index].Contains()` check stays for debug builds — it verifies the found range actually contains the EPK (catches binary search bugs).

**LengthAware comparer note**: `GetRangeByEffectivePartitionKey` currently uses `Range<string>.MinComparer.Instance` (NOT the length-aware comparer from `this.comparers`). This is intentional — point EPK lookups always use standard ordinal comparison. The length-aware comparer is only used in `GetOverlappingRanges`. So this change preserves existing behavior exactly.

---

#### 3. reduce-overlapping-range-allocations

**What**: Optimize `GetOverlappingRanges` to avoid unnecessary allocations in the common single-range case.

**File**: `CollectionRoutingMap.cs`

**Current code** (lines 119-163):
```csharp
public IReadOnlyList<PartitionKeyRange> GetOverlappingRanges(Range<string> range)
{
    return this.GetOverlappingRanges(new[] { range });  // ← array allocation for single range
}

public IReadOnlyList<PartitionKeyRange> GetOverlappingRanges(IReadOnlyList<Range<string>> providedPartitionKeyRanges)
{
    // ...
    SortedList<string, PartitionKeyRange> partitionRanges = new SortedList<string, PartitionKeyRange>();
    // ↑ SortedList is unnecessary: results come from a sorted index in index order, 
    //   and the SortedList just deduplicates by Min key — but ranges are non-overlapping 
    //   so index order = sorted order with no duplicates within a single provided range.
    
    foreach (Range<string> providedRange in providedPartitionKeyRanges)
    {
        int minIndex = this.orderedRanges.BinarySearch(providedRange, this.comparers.MinComparer);
        // ...
        int maxIndex = this.orderedRanges.BinarySearch(providedRange, this.comparers.MaxComparer);
        // ...
        for (int i = minIndex; i <= maxIndex; ++i)
        {
            if (Range<string>.CheckOverlapping(this.orderedRanges[i], providedRange))
            {
                partitionRanges[this.orderedRanges[i].Min] = this.OrderedPartitionKeyRanges[i];
            }
        }
    }
    return new ReadOnlyCollection<PartitionKeyRange>(partitionRanges.Values);  // ← wraps again
}
```

**Problems**:
1. `new[] { range }` allocates an array for the single-range overload (line 121)  
2. `SortedList<string, PartitionKeyRange>` — has O(k log k) insertion, but results are already in sorted order from the index. Its only value is deduplication across multiple provided ranges, which is rare.
3. `new ReadOnlyCollection<>` wraps the result — an extra allocation

**Replacement for single-range overload** (fast path):
```csharp
public IReadOnlyList<PartitionKeyRange> GetOverlappingRanges(Range<string> range)
{
    // Fast path for single range (the common case from PartitionKeyRangeCache):
    // skip the array allocation and SortedList deduplication overhead.
    int minIndex = this.orderedRanges.BinarySearch(range, this.comparers.MinComparer);
    if (minIndex < 0)
    {
        minIndex = Math.Max(0, (~minIndex) - 1);
    }

    int maxIndex = this.orderedRanges.BinarySearch(range, this.comparers.MaxComparer);
    if (maxIndex < 0)
    {
        maxIndex = Math.Min(this.orderedPartitionKeyRanges.Count - 1, ~maxIndex);
    }

    // Collect results directly into a list (no SortedList needed for single range —
    // index order is already sorted and non-overlapping ranges can't produce duplicates)
    List<PartitionKeyRange> results = new List<PartitionKeyRange>(maxIndex - minIndex + 1);
    for (int i = minIndex; i <= maxIndex; ++i)
    {
        if (Range<string>.CheckOverlapping(this.orderedRanges[i], range))
        {
            results.Add(this.orderedPartitionKeyRanges[i]);
        }
    }

    return results;
}
```

**Multi-range overload stays mostly the same** but uses `List` instead of `SortedList`:
```csharp
public IReadOnlyList<PartitionKeyRange> GetOverlappingRanges(IReadOnlyList<Range<string>> providedPartitionKeyRanges)
{
    if (providedPartitionKeyRanges == null)
    {
        throw new ArgumentNullException(nameof(providedPartitionKeyRanges));
    }

    // For a single range, use the optimized overload
    if (providedPartitionKeyRanges.Count == 1)
    {
        return this.GetOverlappingRanges(providedPartitionKeyRanges[0]);
    }

    // For multiple ranges: use a list with deduplication via tracking the last added index.
    // Since both the provided ranges and the index are sorted, we can deduplicate by tracking
    // the highest index added so far.
    List<PartitionKeyRange> results = new List<PartitionKeyRange>();
    int lastAddedIndex = -1;

    foreach (Range<string> providedRange in providedPartitionKeyRanges)
    {
        int minIndex = this.orderedRanges.BinarySearch(providedRange, this.comparers.MinComparer);
        if (minIndex < 0)
        {
            minIndex = Math.Max(0, (~minIndex) - 1);
        }

        int maxIndex = this.orderedRanges.BinarySearch(providedRange, this.comparers.MaxComparer);
        if (maxIndex < 0)
        {
            maxIndex = Math.Min(this.orderedPartitionKeyRanges.Count - 1, ~maxIndex);
        }

        // Skip indices we've already added (handles overlap between provided ranges)
        int startIndex = Math.Max(minIndex, lastAddedIndex + 1);

        for (int i = startIndex; i <= maxIndex; ++i)
        {
            if (Range<string>.CheckOverlapping(this.orderedRanges[i], providedRange))
            {
                results.Add(this.orderedPartitionKeyRanges[i]);
                lastAddedIndex = i;
            }
        }
    }

    return results;
}
```

**What this eliminates**:
- Single-range path: `new[] { range }` array, `SortedList<>`, `ReadOnlyCollection<>` wrapper = **3 allocations → 1** (just the result `List`)
- Multi-range path: `SortedList<>` with O(k log k) insertion → `List` with O(k) append + index tracking for deduplication
- `ReadOnlyCollection<>` wrapper → return `List<>` directly (implements `IReadOnlyList<>`)

**Correctness note for multi-range dedup**: The current code uses `SortedList` keyed by `orderedRanges[i].Min` for dedup. This works because partition ranges are non-overlapping with unique Min values. The replacement uses `lastAddedIndex` tracking which achieves the same dedup — if two provided ranges overlap the same partition range index `i`, `startIndex = Math.Max(minIndex, lastAddedIndex + 1)` skips it. This requires the provided ranges to be sorted (which callers guarantee — `IRoutingMapProviderExtensions.TryGetOverlappingRangesAsync` uses a `SortedSet<Range<string>>` upstream at line 85).

---

### Summary of Phase 1 changes

| Task | Allocations eliminated per call | CPU improvement | Lines changed | Status |
|------|-------------------------------|----------------|---------------|--------|
| pre-extracted-string-array | — (prep) | — | +5 (new field + init) | ✅ Done |
| zero-alloc-point-lookup | 1 heap object (`Range<string>`) | Removes IComparer dispatch | ~5 lines in method | ✅ Done |
| reduce-overlapping-allocs (single) | 3 objects (array, SortedList, ReadOnlyCollection) | Removes SortedList O(k log k) → O(k) | New single-range overload | Pending (query/CF path only) |
| reduce-overlapping-allocs (multi) | 2 objects (SortedList, ReadOnlyCollection) | SortedList → List + index tracking | Rewrite multi-range method | Pending (query/CF path only) |

**Benchmark results (Phase 1, completed tasks)**:
| Partitions | NEW (ns/op) | OLD (ns/op) | Speedup | Alloc reduction |
|-----------|------------|------------|---------|-----------------|
| 100 | 35.0 | 67.8 | 1.94× | 40 B → 0 B |
| 1,000 | 81.4 | 123.5 | 1.52× | 40 B → 0 B |
| 10,000 | 123.1 | 181.1 | 1.47× | 40 B → 0 B |
| 50,000 | 180.2 | 247.4 | 1.37× | 40 B → 0 B |

**Total risk**: Low. All changes are internal to `CollectionRoutingMap`, preserve the same API signatures and semantics, and can be validated by existing unit tests (`CollectionRoutingMapTest.cs`).

---

## Prioritized Roadmap (Single-Document CRUD Focus)

The single-doc CRUD hot path is `AddressResolver → GetRangeByEffectivePartitionKey`. Tasks are re-prioritized by their impact on this path:

### Next: Phase 2 — Numeric Fast-Path for Point Lookup (High Impact)

These directly speed up `GetRangeByEffectivePartitionKey`:

5. **uint128-boundary-index** — Add an optional parallel `UInt128[]` boundary array that is populated **only** when all EPK strings are valid 128-bit hex (detected at construction time). Point lookups use numeric comparison first, falling back to string binary search otherwise. Keeps string arrays as source of truth.

6. **two-level-bucket-index** — For the UInt128 fast path, add a 256-entry lookup table indexed by the first byte of the hash. Narrows binary search window from 50K to ~195 entries (8 comparisons instead of 16). Only activated alongside the UInt128 path.

### Then: Phase 3 — Construction & Cache Refresh (Medium Impact)

Affects single-doc CRUD indirectly — faster cache refresh means less latency on partition splits and failovers:

7. **incremental-combine-optimization** — In `TryCombine`, avoid full re-sort of all ranges. Instead, merge-insert the delta ranges into the existing sorted array. O(delta × log N) instead of O(N log N) for small deltas (typical changefeed updates).

8. **lazy-pkrange-deserialization** — Separate the compact routing index (boundaries + IDs) from full `PartitionKeyRange` objects. Only deserialize the full PKR when needed for request routing. Reduces steady-state memory from ~35 MB to ~5 MB for 50K ranges.

### Lower Priority: Query/ChangeFeed/Batch Path

These do NOT affect single-doc CRUD. Implement when query or bulk perf is the focus:

3. **reduce-overlapping-allocs** — Optimize `GetOverlappingRanges` allocations (query, change feed, feed range path only).

4. **extend-benchmark-to-50k** — Extend `CollectionRoutingMapBenchmark` with 50K params and memory/construction benchmarks.

9. **batch-epk-resolution** — Batch EPK resolution for ReadMany/bulk operations.

## Dependencies

- Phase 2 (numeric fast-path) depends on Phase 1 ✅ (completed)
- `two-level-bucket-index` depends on `uint128-boundary-index`
- `lazy-pkrange-deser` depends on `uint128-boundary-index`
- All other tasks are independent

## Notes

- All changes must preserve the `LengthAwareRangeComparer` path for hierarchical PK support
- The `Range<string>` type is in `Microsoft.Azure.Documents.Routing` (referenced assembly) — we can't modify it, only wrap/avoid it
- Existing `UInt128` type in the SDK (`UInt128.cs`) can be reused for the numeric path
- String-based `Range<string>` APIs must remain the public contract; numeric path is internal optimization only
