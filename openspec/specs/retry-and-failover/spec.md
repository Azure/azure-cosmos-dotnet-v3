# Retry and Failover

## Purpose

The Azure Cosmos DB .NET SDK implements multiple retry policies to handle transient failures, throttling, region failovers, and partition-level unavailability. These policies are layered — each handles a specific class of errors — and are orchestrated by the `RetryHandler` in the handler pipeline. Correct retry behavior is critical for SDK reliability; bugs in retry logic can cause outages or data loss.

## Retry Policy Stack

The `RetryHandler` applies policies in this order:

1. **`ResetSessionTokenRetryPolicy`** — Session token mismatch
2. **`ClientRetryPolicy`** — Cross-region failover and partition failover (PPAF/PPCB)
3. **`ResourceThrottleRetryPolicy`** — Rate limiting (HTTP 429)

Additionally:
- **`MetadataRequestThrottleRetryPolicy`** — For metadata/control-plane requests
- **`HttpTimeoutPolicy` variants** — Transport-level timeout and retry at the HTTP layer
- **`PartitionKeyMismatchRetryPolicy`** — Stale partition key definition cache

## Behavioral Invariants

### ClientRetryPolicy — Status Code Handling

| Status Code | Substatus | Condition | Retry? | Behavior |
|-------------|-----------|-----------|--------|----------|
| 403 | 3 (WriteForbidden) | Write request | ✅ | Endpoint not writable; mark partition for failover, refresh cache |
| 403 | 51 (DatabaseAccountNotFound) | Read or multi-master write | ✅ | Regional endpoint unavailable; fail over to preferred locations |
| 403 | 1008 | Region being added/removed | ✅ | Region marked unavailable for 5 min; retry on next region |
| 404 | 1002 (ReadSessionNotAvailable) | Session consistency | ✅ | Session token mismatch; retry on write region (single-master) or next preferred region (multi-master) |
| 408 | Any | PPAF/PPCB eligible | ✅ | Request timeout; mark partition unavailable, retry next region |
| 410 | 1022 (LeaseNotFound) | Always | ✅ | Partition recreated/moved; retry on next region |
| 429 | 3092 (SystemResourceUnavailable) | Multi-master write only | ⚠️ | Treated as 503; partition marked unavailable |
| 429 | Other | Always | ✅ | Delegates to `ResourceThrottleRetryPolicy` |
| 500 | Any | Read requests only | ✅ | Internal server error on reads; retry next region |
| 503 | Any | Always | ✅ | Service unavailable; PPAF/PPCB marks partition, retry if regions available |
| HttpRequestException | — | Gateway/DNS failure | ✅ | Mark partition unavailable; retry next region |
| OperationCanceledException | — | Cancellation | ⚠️ | PPAF/PPCB marks partition for future requests; current request may not retry |

### ClientRetryPolicy — Limits

| Parameter | Value |
|-----------|-------|
| Max failover retries | 120 |
| Retry interval | 1 second between failover retries |
| Max session token retries (single-master) | 1 |
| Max session token retries (multi-master) | Number of configured endpoints |
| Max service unavailable retries | 1 |

### ResourceThrottleRetryPolicy (HTTP 429)

| Parameter | Default |
|-----------|---------|
| Max retry attempts | 9 |
| Max cumulative wait time | 60 seconds |
| Retry delay source | `x-ms-retry-after` response header |
| Fallback delay (if header = 0) | 5 seconds |
| Backoff strategy | Configurable factor (default = 1, no escalation) |

**Decision logic**: Retry if `currentAttempt < maxAttempts AND cumulativeDelay + nextDelay ≤ maxWaitTime`.

### Per-Partition Automatic Failover (PPAF)

PPAF enables partition-level failover for single-master write accounts. When a specific partition is unavailable, the SDK routes write requests to read regions instead of failing the entire request.

| Trigger | Status Code | Behavior |
|---------|------------|----------|
| Write 503 | ServiceUnavailable | Mark partition unavailable; retry on read region |
| Write 408 | RequestTimeout | Mark partition unavailable; retry on read region |
| Eligibility | `!canUseMultipleWriteLocations && !request.IsReadOnly && PPAFEnabled` | |

**Partition unavailability tracking**:
- Duration: 5 seconds (configurable via `AZURE_COSMOS_PPAF_ALLOWED_PARTITION_UNAVAILABILITY_DURATION_IN_SECONDS`)
- Stored in `ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>`
- Background task periodically retries failed partitions

### Per-Partition Circuit Breaker (PPCB)

PPCB extends PPAF logic to multi-master accounts and read operations using circuit breaker thresholds.

| Trigger | Applies To | Behavior |
|---------|-----------|----------|
| Read 503/500/408 | Read requests | Increment failure counter; trip circuit after threshold |
| Write 503/408 (multi-master) | Multi-master writes | Same circuit breaker logic |
| Failback interval | — | 5 minutes (configurable via `AZURE_COSMOS_PPCB_STALE_PARTITION_UNAVAILABILITY_REFRESH_INTERVAL_IN_SECONDS`) |

### Session Token Retry (404/1002)

| Config | Strategy | Max Retries |
|--------|----------|-------------|
| Multi-master | Retry on each configured endpoint in order | `endpoints.Count` |
| Single-master | Retry on write (primary) region only | 1 |
| Single-master (after 1st retry) | Add `x-ms-should-process-only-in-hub-region=true` header | Subsequent attempts |

### HTTP Timeout Policies

The SDK wraps HTTP requests in timeout policies that control per-attempt timeouts and retry behavior.

| Policy | Attempt Timeouts | Use Case |
|--------|-----------------|----------|
| `HttpTimeoutPolicyDefault` | 65s / 65s / 65s (1s delay between) | Gateway mode data-plane |
| `HttpTimeoutPolicyForPartitionFailover` | 6s / 6s / 10s | PPAF-enabled requests |
| `HttpTimeoutPolicyControlPlaneRetriableHotPath` | 0.5s / 5s / 65s (1s delay) | Metadata reads on hot path (query plan, addresses) |
| `HttpTimeoutPolicyControlPlaneRead` | 5s / 10s / 65s | Metadata reads (initialization, account info) |
| `HttpTimeoutPolicyNoRetry` | 65s (no retry) | Client telemetry |

### CancellationToken Handling

1. `CancellationToken` passed to public APIs can stop any retry loop at any point.
2. `ResourceThrottleRetryPolicy` accepts `CancellationToken` but does NOT actively check it during retry waits — cancellation is observed at exception time only.
3. `ClientRetryPolicy` converts `OperationCanceledException` to PPAF/PPCB partition marking for future requests.

## Configuration

| Parameter | Configurable Via | Default |
|-----------|-----------------|---------|
| Max throttle retries | `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests` | 9 |
| Max throttle wait | `CosmosClientOptions.MaxRetryWaitTimeOnRateLimitedRequests` | 60 seconds |
| Unavailable region expiry | Internal | 5 minutes (300s) |
| PPAF partition unavailability | Environment variable | 5 seconds |
| PPCB failback interval | Environment variable | 5 minutes |

## Region Failover Logic

1. The SDK maintains a `LocationCache` that tracks available read and write endpoints.
2. When a region is marked unavailable, it's moved to the end of the preference list and expires after 5 minutes.
3. Endpoint resolution order: Preferred Locations → Available Write/Read Endpoints → Account-level fallback.
4. `GlobalEndpointManager` refreshes account information on failover-triggering errors (403/3, 403/51, 503).

## Interactions

- **Handler Pipeline**: `RetryHandler` sits in position #5 in the pipeline. See `handler-pipeline` spec.
- **Hedging**: Hedged requests have independent retry behavior. Primary requests can cross-region retry; hedged requests are local-retry only. See `cross-region-hedging` spec.
- **PPAF/PPCB**: Partition-level failover runs within `ClientRetryPolicy` and coordinates with `GlobalPartitionEndpointManager`.

## References

- Source: `Microsoft.Azure.Cosmos/src/ClientRetryPolicy.cs`
- Source: `Microsoft.Azure.Cosmos/src/ResourceThrottleRetryPolicy.cs`
- Source: `Microsoft.Azure.Cosmos/src/Handler/RetryHandler.cs`
- Source: `Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs`
- Source: `Microsoft.Azure.Cosmos/src/Routing/LocationCache.cs`
- Source: `Microsoft.Azure.Cosmos/src/HttpClient/HttpTimeoutPolicy*.cs`
- Design: `docs/SdkDesign.md` (Retry sections)
- Design: `docs/PerPartitionAutomaticFailoverDesign.md`
