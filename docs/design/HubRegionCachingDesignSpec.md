# Hub Region Caching for Single-Master Accounts

## Design Specification

**Version:** 2.0  
**Author:** Cosmos DB SDK Team  
**Branch:** `users/kundadebdatta/hubregioncaching_on_retry`  
**Last Updated:** April 2026

---

## 1. Overview

This document describes the hub region caching mechanism implemented in the Azure Cosmos DB .NET SDK. The feature optimizes operations in single-master accounts by caching the discovered hub region endpoint during the 403/3 (WriteForbidden) discovery chain, so that subsequent requests to the same partition can skip the discovery chain entirely and route directly to the cached hub.

### 1.1 Problem Statement

In single-master Cosmos DB accounts, when a client sends a request to a non-hub (satellite) region and encounters session consistency issues (404/1002 ReadSessionNotAvailable), the SDK must discover the hub region. This discovery involves the service returning `403 Forbidden` with substatus `3` (WriteForbidden) from non-hub regions until the actual hub is found. This discovery process involves multiple round-trips and adds latency to every request targeting a partition whose hub region is unknown.

### 1.2 Solution

After 2 consecutive 404/1002 errors on a single-master account, set the `x-ms-cosmos-hub-region-processing-only` header and let the service-side 403/3 discovery chain find the hub. The `GlobalPartitionEndpointManagerCore` caches each failed location during discovery (via `TryMarkEndpointUnavailableForPartitionKeyRange`), building a failover chain in `PartitionKeyRangeToLocationForWrite`. On subsequent requests that hit 2× 404/1002 for the same partition, `TryAddPartitionLevelLocationOverride` finds the existing cache entry and routes directly to the discovered hub — skipping the 403/3 discovery chain entirely.

This mechanism works for **both PPAF (Per-Partition Automatic Failover) and non-PPAF accounts** by reusing the same `PartitionKeyRangeToLocationForWrite` cache and failover infrastructure.

---

## 2. Existing Behavior vs. New Behavior

### 2.1 Existing Behavior (Before This Change)

On a single-master account, when a 404/1002 (ReadSessionNotAvailable) is received:

| Step | What Happens |
|------|-------------|
| 1. Request sent to preferred region | Returns 404/1002 |
| 2. Retry on write region (index 0) | Returns 404/1002 |
| 3. **No more retries** | SDK returns the 404/1002 to the caller |

**Problems:**
- The SDK gave up after 2 attempts (preferred region + write region) with no way to discover the actual hub region that holds the latest session state.
- Every request to a partition in a satellite region paid the same penalty — there was no caching of discovered regions.
- Hub region discovery via the `x-ms-cosmos-hub-region-processing-only` header was set after the 1st 404/1002 and only applied in narrow scenarios.
- Non-PPAF accounts could not leverage the partition-level failover cache at all; only PPAF-enabled accounts had cache-based routing.

### 2.2 New Behavior (After This Change)

| Step | What Happens |
|------|-------------|
| 1. Request sent to preferred region | Returns 404/1002 |
| 2. Retry on write region (index 0); also checks partition cache for previously discovered hub | Returns 404/1002 |
| 3. **Hub header set** (`addHubRegionProcessingOnlyHeader = true`); retry with `x-ms-cosmos-hub-region-processing-only` header | SDK starts round-robin across regions |
| 4a. Non-hub region receives header | Returns 403/3 (WriteForbidden) → SDK calls `TryMarkEndpointUnavailableForPartitionKeyRange` which advances the failover cache to the next region |
| 4b. Hub region receives header | Returns 200 OK → partition is now in the cache |
| 4c. Hub region receives header but returns 404/1002 | The hub is the source of truth — if it also returns 404/1002, the document genuinely does not exist in the current session. SDK returns `NoRetry` (no further retry attempts). |
| 5. **Subsequent request** hits 2× 404/1002 for same partition | `TryAddPartitionLevelLocationOverride(request, checkHubRegionOverrideInCache: true)` finds cached hub → routes directly, **skipping 403/3 chain** |

**Key improvements:**
- The SDK no longer gives up after 2× 404/1002 — it continues with hub region discovery.
- The partition-level failover cache (`PartitionKeyRangeToLocationForWrite`) is populated during the 403/3 discovery chain, not just on PPAF accounts.
- Subsequent requests for the same partition skip the entire 403/3 chain by reading the cached hub.
- Non-PPAF accounts now have the same cache-based routing that PPAF accounts have, activated by the `checkHubRegionOverrideInCache` / `IsHubRegionRoutingActive` flag.

### 2.3 Comparison Summary

| Aspect | Existing Behavior | New Behavior |
|--------|-------------------|-------------|
| Max retries on 404/1002 (single-master) | 2 (then give up) | 2 + hub discovery chain |
| Hub header trigger | After 1st 404/1002 | After 2nd 404/1002 |
| 403/3 on read path with hub header | Not handled | Retries to continue discovery |
| Non-PPAF cache routing | Not available | Available via `checkHubRegionOverrideInCache` |
| Partition-level cache on warm path | Only PPAF accounts | Both PPAF and non-PPAF |
| `GatewayStoreModel` PKRange resolution | Only when PPAF enabled | Also when hub header is present |

---

## 3. Architecture

### 3.1 Component Interaction

The hub region caching integrates into the existing partition-level failover infrastructure. When hub region discovery is active (indicated by the `x-ms-cosmos-hub-region-processing-only` header), the same `PartitionKeyRangeToLocationForWrite` cache and `TryRouteRequestForPartitionLevelOverride` method used by PPAF are reused — gated by the `addHubRegionOverrideFromCache` / `isHubRegionDiscoveryActive` flags.

### 3.2 Key Classes and Methods

| Class | Method | Responsibility |
|-------|--------|----------------|
| `ClientRetryPolicy` | `OnBeforeSendRequest()` | Sets `ShouldProcessOnlyInHubRegion` header when `addHubRegionProcessingOnlyHeader` is true |
| `ClientRetryPolicy` | `ShouldRetryOnSessionNotAvailable()` | After 2× 404/1002: sets hub header flag, checks cache via `TryAddPartitionLevelLocationOverride`, falls back to region cycling |
| `ClientRetryPolicy` | `ShouldRetryInternal()` (403/3 path) | Read path with hub header: retries via `TryMarkEndpointUnavailableForPartitionKeyRange`; Write path: normal retry |
| `GatewayStoreModel` | `InvokeAsync()` | When `IsHubRegionRoutingActive`, resolves PKRange and calls `TryAddPartitionLevelLocationOverride` (even on non-PPAF accounts) |
| `GlobalPartitionEndpointManagerCore` | `TryAddPartitionLevelLocationOverride()` | When `addHubRegionOverrideFromCache = true`, routes to cached hub from `PartitionKeyRangeToLocationForWrite` |
| `GlobalPartitionEndpointManagerCore` | `TryMarkEndpointUnavailableForPartitionKeyRange()` | During 403/3 chain, advances the failover cache to the next region |
| `GlobalPartitionEndpointManagerCore` | `IsHubRegionRoutingActive()` | Static check: returns true if request has the hub region header and is a read-only request |
| `GlobalPartitionEndpointManagerCore` | `IsRequestEligibleForPartitionOrHubRegionFailover()` | Eligibility gate: allows entry when PPAF, circuit breaker, **or** `checkHubRegionOverrideInCache` is true |

---

## 4. Detailed Flow

### 4.1 Hub Region Discovery Trigger

Hub region discovery is triggered after **2 consecutive 404/1002 (ReadSessionNotAvailable)** errors in a single-master account. On the 2nd 404/1002, `ShouldRetryOnSessionNotAvailable()` sets `addHubRegionProcessingOnlyHeader = true` and checks the partition cache (warm path) before falling back to region cycling (cold path).

### 4.2 Warm Path: Cache Hit

Before falling back to the region-cycling discovery chain, the SDK checks:

```
if (this.partitionKeyRangeLocationCache.TryAddPartitionLevelLocationOverride(request, checkHubRegionOverrideInCache: true))
{
    // Cache hit — route directly to the previously discovered hub region.
    // No 403/3 chain needed.
}
```

This is the fast path for subsequent requests to a partition whose hub was already discovered.

### 4.3 Cold Path: 403/3 Discovery Chain

When the cache is empty (first request for this partition), the SDK enters the 403/3 discovery chain:

1. `OnBeforeSendRequest()` attaches the `x-ms-cosmos-hub-region-processing-only` header
2. `GatewayStoreModel` detects the header via `IsHubRegionRoutingActive()`, resolves the PKRange, and calls `TryAddPartitionLevelLocationOverride(request, isHubRegionRoutingActive)` to route to any existing cache entry
3. Non-hub regions return 403/3 → `TryMarkEndpointUnavailableForPartitionKeyRange()` advances the failover cache
4. The hub region returns 200 OK → the cache entry now points to the correct hub

### 4.4 Cache Storage

The hub region is stored in the `PartitionKeyRangeToLocationForWrite` dictionary (the same dictionary used by PPAF):

```
Lazy<ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>> PartitionKeyRangeToLocationForWrite
```

### 4.5 Using Cached Hub Region

Subsequent requests that trigger 2× 404/1002 check the cache via:

```
TryAddPartitionLevelLocationOverride(request, checkHubRegionOverrideInCache: true)
```

If a cache entry exists, the request is routed directly to the cached hub endpoint, skipping the 403/3 discovery chain entirely.

---

## 5. PPAF vs. Non-PPAF Account Support

### 5.1 How It Works for Both

The hub region caching reuses the PPAF infrastructure (`PartitionKeyRangeToLocationForWrite`, `TryRouteRequestForPartitionLevelOverride`, etc.) but is activated independently:

| Gate | PPAF Account | Non-PPAF Account |
|------|-------------|-----------------|
| `IsRequestEligibleForPerPartitionAutomaticFailover()` | ✅ true | ❌ false |
| `checkHubRegionOverrideInCache` (from `ClientRetryPolicy`) | ✅ true (after 2× 404/1002) | ✅ true (after 2× 404/1002) |
| `IsHubRegionRoutingActive()` (from `GatewayStoreModel`) | ✅ true (header present) | ✅ true (header present) |
| `IsRequestEligibleForPartitionOrHubRegionFailover()` | ✅ passes (PPAF flag OR `checkHubRegionOverrideInCache`) | ✅ passes (`checkHubRegionOverrideInCache` alone) |

### 5.2 Key Code Paths

**`IsRequestEligibleForPartitionOrHubRegionFailover`** — the eligibility gate that allows non-PPAF accounts to use the cache:
```csharp
if (!this.IsPartitionLevelAutomaticFailoverEnabled()     // false for non-PPAF
   && !this.IsPartitionLevelCircuitBreakerEnabled()       // may be false
   && !checkHubRegionOverrideInCache)                     // true → passes!
{
    return false;
}
```

**`GatewayStoreModel.InvokeAsync`** — resolves PKRange and calls override for non-PPAF accounts:
```csharp
bool isHubRegionRoutingActive = GlobalPartitionEndpointManagerCore.IsHubRegionRoutingActive(request);
if ((isPPAFEnabled || this.isThinClientEnabled || isHubRegionRoutingActive) ...)
{
    // Resolves PKRange and calls TryAddPartitionLevelLocationOverride
    // This now fires for non-PPAF accounts when the hub header is present
}
```

### 5.3 Circuit Breaker Interaction

Hub region routing is explicitly excluded from the circuit breaker path to prevent conflicts:
```csharp
// IsRequestEligibleForPartitionLevelCircuitBreaker
return this.isPartitionLevelCircuitBreakerEnabled == 1
    && !GlobalPartitionEndpointManagerCore.IsHubRegionRoutingActive(request)  // excluded
    && ...;
```

---

## 6. Performance Considerations

### 6.1 Latency

- **First request (cold cache):** Hub discovery adds 1-3 extra round-trips (403/3 chain)
- **Subsequent requests (warm cache):** Direct routing to cached hub — no extra round-trips

### 6.2 Network Traffic

- **With Caching:** First request pays the discovery cost; all subsequent requests for the same partition go directly to the hub
- **Without Caching:** Every request that hits 404/1002 would either fail (existing behavior) or repeat the full 403/3 chain

---

## 7. Cache Invalidation

### 7.1 Invalidation Scenarios

| Scenario | Trigger | Action |
|----------|---------|--------|
| Hub region changed | 403/3 on cached hub | `TryAddOrUpdatePartitionFailoverInfoAndMoveToNextLocation` cycles to next region; if all exhausted, removes entry |
| All regions exhausted | `TryMoveNextLocation` returns false | Entry removed from `PartitionKeyRangeToLocationForWrite` |
| Collection recreated | Collection RID mismatch | Entry becomes orphaned (new PKRange won't match) |
| Client restart | Process termination | Cache is in-memory, automatically cleared |

### 7.2 Stale Cache Handling

When a 403/3 is received on a previously cached hub, `TryMarkEndpointUnavailableForPartitionKeyRange` advances the failover location to the next region. If all regions are exhausted, the cache entry is removed.

---

## 8. Applicability

### 8.1 Enabled For

- ✅ Single-master accounts (both PPAF and non-PPAF)
- ✅ External customers (`#if !INTERNAL`)
- ✅ Read and write operations that encounter 404/1002 session consistency issues
- ✅ Gateway and Direct connection modes

### 8.2 Not Applicable For

- ❌ Multi-master accounts (`canUseMultipleWriteLocations = true`) — these use region-cycling for 404/1002
- ❌ Internal deployments (`#if INTERNAL`) — uses the existing 2-retry behavior
- ❌ Master resource requests — excluded by `!ReplicatedResourceClient.IsMasterResource(request.ResourceType)`

### 8.3 Preprocessor Guards

The hub header logic in `ClientRetryPolicy.ShouldRetryOnSessionNotAvailable()` and `OnBeforeSendRequest()` is gated by `#if !INTERNAL`. Internal builds retain the existing 2-retry-then-stop behavior.

---

## 9. Existing vs. New Behavior: Performance Impact

### 9.1 Existing Behavior (Without Hub Region Caching)

Every request to a partition in a satellite region that hits 404/1002:

```
Request → 404/1002 → Retry on write region → 404/1002 → FAIL (returned to caller)
```

### 9.2 New Behavior: Cold Cache (First Discovery)

```
Request → 404/1002 → Retry on write region → 404/1002
  → Hub header set → 403/3 (non-hub) → 403/3 (non-hub) → 200 OK (hub found, cached)
```

### 9.3 New Behavior: Warm Cache (Subsequent Requests)

```
Request → 404/1002 → Retry on write region → 404/1002
  → Hub header set → Cache hit! → Route to cached hub → 200 OK
```

### 9.4 Expected Improvement

| Metric | Existing Behavior | New Behavior (Cold) | New Behavior (Warm) |
|--------|-------------------|---------------------|---------------------|
| Outcome on 2× 404/1002 | Fail (returned to caller) | Success (after discovery) | Success (direct from cache) |
| Round-trips per request | 2 (then fail) | 3-6 (discovery chain) | 3 (cache hit) |
| 403/3 responses | N/A (fails before discovery) | Per first request per partition | None (cached) |
| P99 Latency (subsequent) | N/A (request fails) | 50-100ms | 50-100ms |

---

## 10. Testing

### 10.1 Unit Test Scenarios (`ClientRetryPolicyTests.cs`)

1. **Hub header after 2× 404/1002 (single-master vs multi-master):** `ClientRetryPolicy_After404With1002_AddsHubHeaderOnlyForSingleMaster` — Verifies hub header is added only for single-master after 2× 404/1002 (3rd attempt), never for multi-master
2. **Full hub caching flow:** `ClientRetryPolicy_After404With1002Twice_Then403_3_ThenSuccess_CachesHub_AndSubsequentRequestReusesCache` — End-to-end: 2× 404/1002 → hub header → 403/3 (no cache build) → success (cache populated) → subsequent request reuses cache
3. **Cache isolation:** Normal requests without hub header do NOT get routed through the hub cache (prevents bombarding the hub)

### 10.2 Unit Test Scenarios (`LocationCacheTests.cs`)

4. **Retry count change:** `ValidateRetryOnSessionNotAvailableWithDisableMultipleWriteLocations` — Updated to expect 3 attempts (was 2) with success on 3rd attempt when hub header is active

### 10.3 Integration Test Scenarios (`CosmosItemIntegrationTests.cs`)

5. **Hub header injection (Gateway):** `ReadItemAsync_ShouldAddHubHeader_OnRetryAfter_404_1002` — Uses `HttpClientHandlerHelper` to intercept HTTP requests and verify hub header appears on 3rd request after 2× 404/1002
6. **Full 403/3 flow (Gateway):** `ReadItemAsync_HubRegionDiscovery_FullFlow_With403_3_Retry` — Simulates 2× 404/1002 → 403/3 → success; verifies hub header persists through 403/3 retries and request succeeds

---

## 11. Future Enhancements

1. **TTL-based cache expiration:** Add time-based invalidation for long-running clients
2. **Proactive cache warming:** Pre-populate cache during client initialization
3. **Metrics and telemetry:** Add counters for cache hit/miss rates
4. **Cross-partition optimization:** Share hub discovery across partitions in same region
5. **Direct mode support:** Integration tests for Direct connection mode (currently scaffolded but disabled)

---

## 12. References

- [ClientRetryPolicy.cs](../../Microsoft.Azure.Cosmos/src/ClientRetryPolicy.cs) - Retry logic, hub header injection, and 404/1002 → hub discovery trigger
- [GlobalPartitionEndpointManagerCore.cs](../../Microsoft.Azure.Cosmos/src/Routing/GlobalPartitionEndpointManagerCore.cs) - Cache storage, routing, `IsHubRegionRoutingActive`, eligibility gates
- [GlobalPartitionEndpointManager.cs](../../Microsoft.Azure.Cosmos/src/Routing/GlobalPartitionEndpointManager.cs) - Abstract interface with `checkHubRegionOverrideInCache` parameter
- [GatewayStoreModel.cs](../../Microsoft.Azure.Cosmos/src/GatewayStoreModel.cs) - PKRange resolution and override for hub region routing on non-PPAF accounts
- [ClientRetryPolicyTests.cs](../../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/ClientRetryPolicyTests.cs) - Unit tests for hub header and caching flow
- [LocationCacheTests.cs](../../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/LocationCacheTests.cs) - Retry count validation
- [CosmosItemIntegrationTests.cs](../../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosItemIntegrationTests.cs) - Integration tests with HTTP interception