# Hub Region Caching for Single-Master Write Accounts

## Design Specification

**Version:** 1.0  
**Author:** Cosmos DB SDK Team  
**Branch:** `users/kundadebdatta/hubregioncaching_on_success`  
**Last Updated:** April 2026

---

## 1. Overview

This document describes the hub region caching mechanism implemented in the Azure Cosmos DB .NET SDK. The feature optimizes write operations in single-master accounts by caching the discovered hub region endpoint after successful hub region discovery, eliminating redundant 403/3 (WriteForbidden) discovery cycles for subsequent requests to the same partition.

### 1.1 Problem Statement

In single-master Cosmos DB accounts, when a client sends a write request to a non-hub (satellite) region, the service returns a `403 Forbidden` with substatus `3` (WriteForbidden). The client must then discover and route to the actual hub region that can accept writes. This discovery process involves multiple round-trips and adds latency to every write operation targeting a partition whose hub region is unknown.

### 1.2 Solution

Cache the hub region endpoint **only after a successful response (HTTP 200)** is received during hub region discovery. This ensures we only cache verified hub regions, avoiding stale or incorrect cache entries.

---

## 2. Core Design Principles

### 2.1 Cache on Success Only

**Key Principle:** The hub region is cached **only when we receive a successful response**, confirming that the region is actually the hub for that partition.

### 2.2 Why Cache on Success?

| Approach | Pros | Cons |
|----------|------|------|
| Cache on 403/3 | Earlier caching | May cache incorrect region; race conditions |
| **Cache on Success** | Guaranteed correct hub | Slight delay in caching (after full discovery) |

Caching on success ensures:
- **Correctness:** The cached endpoint is verified as the actual hub
- **Consistency:** No stale entries from partial discovery
- **Simplicity:** Single point of truth for cache population

---

## 3. Architecture

### 3.1 Component Interaction

### 3.2 Key Classes and Methods

| Class | Method | Responsibility |
|-------|--------|----------------|
| `ClientRetryPolicy` | `OnBeforeSendRequest()` | Sets `ShouldProcessOnlyInHubRegion` header; routes to cached hub if available |
| `ClientRetryPolicy` | `OnAfterSendRequest()` | Triggers hub caching on successful response |
| `ClientRetryPolicy` | `ShouldRetryOnSessionNotAvailable()` | Initiates hub discovery after 2× 404/1002 |
| `GlobalPartitionEndpointManagerCore` | `TryAddHubRegionOverrideOnSuccess()` | Caches the verified hub region |
| `GlobalPartitionEndpointManagerCore` | `TryAddPartitionLevelLocationOverride()` | Routes requests to cached hub |
| `GlobalPartitionEndpointManagerCore` | `IsHubRegionRoutingActive()` | Checks if request is in hub discovery flow |

---

## 4. Detailed Flow

### 4.1 Hub Region Discovery Trigger

Hub region discovery is triggered after **2 consecutive 404/1002 (ReadSessionNotAvailable)** errors in a single-master account:

### 4.2 Request Header Injection

When hub discovery is active, the `ShouldProcessOnlyInHubRegion` header is added:

### 4.3 Caching on Success

The hub region is cached **only after a successful response**:

### 4.4 Cache Storage

The hub region is stored in the `PartitionKeyRangeToLocationForWrite` dictionary:

### 4.5 Using Cached Hub Region

Subsequent requests check the cache before routing:

---

## 5. Performance Considerations

### 5.1 Latency

- **Initial Write:** Involves hub discovery, higher latency
- **Subsequent Writes:** Lower latency,directed to cached hub

### 5.2 Network Traffic

- **With Caching:** Reduced redundancy, lower overall traffic
- **Without Caching:** Higher traffic due to repeated discovery requests

---

## 6. Cache Invalidation

### 6.1 Invalidation Scenarios

| Scenario | Trigger | Action |
|----------|---------|--------|
| Hub region changed | 403/3 on cached hub | Remove stale entry, restart discovery |
| Collection recreated | Collection RID mismatch | Entry becomes orphaned (no match) |
| Client restart | Process termination | Cache is in-memory, automatically cleared |

### 6.2 Stale Cache Handling

When a 403/3 is received on a previously cached hub:

---

## 7. Applicability

### 7.1 Enabled For

- ✅ Single-master write accounts
- ✅ External customers (`#if !INTERNAL`)
- ✅ Write operations with session consistency issues

### 7.2 Not Applicable For

- ❌ Multi-master accounts (`canUseMultipleWriteLocations = true`)
- ❌ Internal deployments (`#if INTERNAL`)
- ❌ Read-only operations (uses different routing logic)

### 7.3 Preprocessor Guards

---

## 8. Performance Impact

### 8.1 Before (Without Caching)

Every write to a partition in a satellite region:

### 8.2 After (With Caching)

First write discovers hub, subsequent writes route directly:

### 8.3 Expected Improvement

| Metric | Without Caching | With Caching |
|--------|-----------------|--------------|
| P99 Latency (subsequent writes) | 200-500ms | 50-100ms |
| Round-trips per write | 2-5 | 1 |
| 403/3 responses | Per request | First request only |

---

## 9. Testing Considerations

### 9.1 Unit Test Scenarios

1. **Cache population:** Verify hub is cached only on 200 OK
2. **Cache hit:** Verify subsequent requests route to cached hub
3. **Cache miss:** Verify discovery flow when cache is empty
4. **Cache invalidation:** Verify stale entries are removed on 403/3
5. **Multi-master exclusion:** Verify caching is disabled for multi-master

### 9.2 Integration Test Scenarios

1. Failover during hub discovery
2. Hub region change after caching
3. Collection recreation with same name
4. Concurrent requests during discovery

---

## 10. Future Enhancements

1. **TTL-based cache expiration:** Add time-based invalidation for long-running clients
2. **Proactive cache warming:** Pre-populate cache during client initialization
3. **Metrics and telemetry:** Add counters for cache hit/miss rates
4. **Cross-partition optimization:** Share hub discovery across partitions in same region

---

## 11. References

- `ClientRetryPolicy.cs` - Retry logic and header injection
- `GlobalPartitionEndpointManagerCore.cs` - Cache storage and routing
- `GlobalPartitionEndpointManager.cs` - Abstract interface definition
- Azure Cosmos DB Documentation: [Consistency levels](https://docs.microsoft.com/azure/cosmos-db/consistency-levels)