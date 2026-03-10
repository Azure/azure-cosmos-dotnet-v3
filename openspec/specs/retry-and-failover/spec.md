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

## Requirements

### Requirement: ClientRetryPolicy Status Code Handling

The `ClientRetryPolicy` SHALL evaluate the response status code and substatus to determine whether a request is eligible for retry and which failover action to take.

**Status code reference table:**

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

#### WriteForbidden (403/3)

**When** the SDK receives HTTP 403 with substatus 3 (WriteForbidden) on a write request, **then** the SDK **SHALL** mark the partition for failover, refresh the location cache, and retry the request on the next available region.

#### DatabaseAccountNotFound (403/51)

**When** the SDK receives HTTP 403 with substatus 51 (DatabaseAccountNotFound) on a read request or a multi-master write request, **then** the SDK **SHALL** fail over to the next preferred location and retry the request.

#### Region Add/Remove (403/1008)

**When** the SDK receives HTTP 403 with substatus 1008, **then** the SDK **SHALL** mark the region as unavailable for 5 minutes and retry the request on the next available region.

#### ReadSessionNotAvailable (404/1002)

**When** the SDK receives HTTP 404 with substatus 1002 (ReadSessionNotAvailable) under session consistency, **then** the SDK **SHALL** retry on the write region (single-master) or the next preferred region (multi-master).

#### RequestTimeout (408)

**When** the SDK receives HTTP 408 (RequestTimeout) **and** the request is PPAF/PPCB eligible, **then** the SDK **SHALL** mark the partition as unavailable and retry the request on the next available region.

#### LeaseNotFound (410/1022)

**When** the SDK receives HTTP 410 with substatus 1022 (LeaseNotFound), **then** the SDK **SHALL** retry the request on the next available region regardless of request type.

#### SystemResourceUnavailable (429/3092)

**When** the SDK receives HTTP 429 with substatus 3092 (SystemResourceUnavailable) on a multi-master write request, **then** the SDK **SHALL** treat the response as HTTP 503 and mark the partition as unavailable.

#### Throttling (429 — Other Substatus)

**When** the SDK receives HTTP 429 with any substatus other than 3092, **then** the SDK **SHALL** delegate retry handling to the `ResourceThrottleRetryPolicy`.

#### InternalServerError (500)

**When** the SDK receives HTTP 500 on a read request, **then** the SDK **SHALL** retry the request on the next available region.

**If** the SDK receives HTTP 500 on a write request, **then** the SDK **SHALL NOT** retry the request.

#### ServiceUnavailable (503)

**When** the SDK receives HTTP 503, **then** the SDK **SHALL** mark the partition via PPAF/PPCB and retry the request if additional regions are available.

#### HttpRequestException (Gateway/DNS Failure)

**When** the SDK catches an `HttpRequestException` (e.g., gateway or DNS failure), **then** the SDK **SHALL** mark the partition as unavailable and retry the request on the next available region.

#### OperationCanceledException

**When** the SDK catches an `OperationCanceledException`, **then** the SDK **SHALL** mark the partition via PPAF/PPCB for future requests. The current request **may** not be retried.

### Requirement: ClientRetryPolicy Limits

The `ClientRetryPolicy` SHALL enforce upper bounds on retry attempts to prevent unbounded retry loops.

**Retry limit reference table:**

| Parameter | Value |
|-----------|-------|
| Max failover retries | 120 |
| Retry interval | 1 second between failover retries |
| Max session token retries (single-master) | 1 |
| Max session token retries (multi-master) | Number of configured endpoints |
| Max service unavailable retries | 1 |

#### Max Failover Retries

**While** performing cross-region failover retries, the SDK **SHALL** retry at most 120 times with a 1-second interval between retries.

#### Session Token Retry Limit — Single-Master

**Where** the account is configured as single-master, **when** a session token mismatch occurs, **then** the SDK **SHALL** retry at most 1 time on the write (primary) region.

#### Session Token Retry Limit — Multi-Master

**Where** the account is configured as multi-master, **when** a session token mismatch occurs, **then** the SDK **SHALL** retry on each configured endpoint in order, up to `endpoints.Count` times.

#### Service Unavailable Retry Limit

**When** the SDK encounters a service unavailable (503) response, **then** the SDK **SHALL** retry at most 1 time before propagating the failure.

### Requirement: ResourceThrottleRetryPolicy

The `ResourceThrottleRetryPolicy` SHALL handle HTTP 429 (rate-limited) responses by retrying with server-specified delays up to configurable limits.

**Throttle retry parameter reference table:**

| Parameter | Default |
|-----------|---------|
| Max retry attempts | 9 |
| Max cumulative wait time | 60 seconds |
| Retry delay source | `x-ms-retry-after` response header |
| Fallback delay (if header = 0) | 5 seconds |
| Backoff strategy | Configurable factor (default = 1, no escalation) |

#### Retry Decision

**When** the SDK receives HTTP 429, **then** the SDK **SHALL** retry **if** `currentAttempt < maxAttempts AND cumulativeDelay + nextDelay ≤ maxWaitTime`.

**If** the cumulative delay would exceed `maxWaitTime` or the current attempt has reached `maxAttempts`, **then** the SDK **SHALL NOT** retry and **SHALL** propagate the 429 response to the caller.

#### Retry Delay Selection

**When** the SDK retries a throttled request, **then** the SDK **SHALL** use the delay specified in the `x-ms-retry-after` response header.

**If** the `x-ms-retry-after` header value is 0, **then** the SDK **SHALL** use a fallback delay of 5 seconds.

#### Backoff Escalation

**Where** a backoff factor is configured, **then** the SDK **SHALL** multiply the retry delay by the configured factor on each successive attempt. The default factor is 1 (no escalation).

### Requirement: Per-Partition Automatic Failover (PPAF)

The SDK **SHALL** support partition-level failover for single-master write accounts so that when a specific partition is unavailable, write requests are routed to read regions instead of failing entirely.

**PPAF trigger reference table:**

| Trigger | Status Code | Behavior |
|---------|------------|----------|
| Write 503 | ServiceUnavailable | Mark partition unavailable; retry on read region |
| Write 408 | RequestTimeout | Mark partition unavailable; retry on read region |
| Eligibility | `!canUseMultipleWriteLocations && !request.IsReadOnly && PPAFEnabled` | |

#### PPAF Eligibility

**Where** the account does not use multiple write locations (`!canUseMultipleWriteLocations`) **and** the request is not read-only (`!request.IsReadOnly`) **and** PPAF is enabled, **then** the SDK **SHALL** evaluate the request for partition-level failover.

#### Write ServiceUnavailable under PPAF

**When** a PPAF-eligible write request receives HTTP 503 (ServiceUnavailable), **then** the SDK **SHALL** mark the partition as unavailable and retry the request on a read region.

#### Write RequestTimeout under PPAF

**When** a PPAF-eligible write request receives HTTP 408 (RequestTimeout), **then** the SDK **SHALL** mark the partition as unavailable and retry the request on a read region.

#### Partition Unavailability Tracking

**When** a partition is marked as unavailable under PPAF, **then** the SDK **SHALL** record the unavailability for a duration of 5 seconds (configurable via `AZURE_COSMOS_PPAF_ALLOWED_PARTITION_UNAVAILABILITY_DURATION_IN_SECONDS`).

The unavailability state **SHALL** be stored in a `ConcurrentDictionary<PartitionKeyRange, PartitionKeyRangeFailoverInfo>`.

A background task **SHALL** periodically retry failed partitions to detect recovery.

### Requirement: Per-Partition Circuit Breaker (PPCB)

The SDK **SHALL** extend PPAF logic to multi-master accounts and read operations using circuit breaker thresholds.

**PPCB trigger reference table:**

| Trigger | Applies To | Behavior |
|---------|-----------|----------|
| Read 503/500/408 | Read requests | Increment failure counter; trip circuit after threshold |
| Write 503/408 (multi-master) | Multi-master writes | Same circuit breaker logic |
| Failback interval | — | 5 minutes (configurable via `AZURE_COSMOS_PPCB_STALE_PARTITION_UNAVAILABILITY_REFRESH_INTERVAL_IN_SECONDS`) |

#### Read Failure Circuit Breaker

**When** a read request receives HTTP 503, 500, or 408, **then** the SDK **SHALL** increment the failure counter for the affected partition. **If** the failure count exceeds the configured threshold, **then** the SDK **SHALL** trip the circuit breaker and route subsequent requests to an alternate region.

#### Multi-Master Write Circuit Breaker

**When** a multi-master write request receives HTTP 503 or 408, **then** the SDK **SHALL** apply the same circuit breaker logic as read failure handling.

#### PPCB Failback

**While** a partition circuit breaker is tripped, the SDK **SHALL** attempt failback after a 5-minute interval (configurable via `AZURE_COSMOS_PPCB_STALE_PARTITION_UNAVAILABILITY_REFRESH_INTERVAL_IN_SECONDS`).

### Requirement: Session Token Retry (404/1002)

The SDK **SHALL** retry requests that fail with HTTP 404 substatus 1002 (ReadSessionNotAvailable) using a strategy determined by the account's write configuration.

**Session token retry strategy reference table:**

| Config | Strategy | Max Retries |
|--------|----------|-------------|
| Multi-master | Retry on each configured endpoint in order | `endpoints.Count` |
| Single-master | Retry on write (primary) region only | 1 |
| Single-master (after 1st retry) | Add `x-ms-should-process-only-in-hub-region=true` header | Subsequent attempts |

#### Multi-Master Session Token Retry

**Where** the account is configured as multi-master, **when** the SDK receives HTTP 404 with substatus 1002, **then** the SDK **SHALL** retry the request on each configured endpoint in order, up to `endpoints.Count` retries.

#### Single-Master Session Token Retry

**Where** the account is configured as single-master, **when** the SDK receives HTTP 404 with substatus 1002, **then** the SDK **SHALL** retry the request on the write (primary) region only, with a maximum of 1 retry.

#### Single-Master Hub Region Header

**Where** the account is configured as single-master, **when** the first session token retry has been attempted, **then** the SDK **SHALL** add the header `x-ms-should-process-only-in-hub-region=true` to subsequent retry attempts.

### Requirement: HTTP Timeout Policies

The SDK **SHALL** wrap HTTP requests in timeout policies that control per-attempt timeouts and retry behavior.

**HTTP timeout policy reference table:**

| Policy | Attempt Timeouts | Use Case |
|--------|-----------------|----------|
| `HttpTimeoutPolicyDefault` | 65s / 65s / 65s (1s delay between) | Gateway mode data-plane |
| `HttpTimeoutPolicyForPartitionFailover` | 6s / 6s / 10s | PPAF-enabled requests |
| `HttpTimeoutPolicyControlPlaneRetriableHotPath` | 0.5s / 5s / 65s (1s delay) | Metadata reads on hot path (query plan, addresses) |
| `HttpTimeoutPolicyControlPlaneRead` | 5s / 10s / 65s | Metadata reads (initialization, account info) |
| `HttpTimeoutPolicyNoRetry` | 65s (no retry) | Client telemetry |

#### Default Gateway Timeout

**Where** a request uses gateway mode for data-plane operations, **then** the SDK **SHALL** apply `HttpTimeoutPolicyDefault` with attempt timeouts of 65s / 65s / 65s and a 1-second delay between attempts.

#### Partition Failover Timeout

**Where** a request is PPAF-enabled, **then** the SDK **SHALL** apply `HttpTimeoutPolicyForPartitionFailover` with attempt timeouts of 6s / 6s / 10s.

#### Control Plane Hot Path Timeout

**Where** a request is a metadata read on the hot path (e.g., query plan, addresses), **then** the SDK **SHALL** apply `HttpTimeoutPolicyControlPlaneRetriableHotPath` with attempt timeouts of 0.5s / 5s / 65s and a 1-second delay between attempts.

#### Control Plane Read Timeout

**Where** a request is a metadata read for initialization or account info, **then** the SDK **SHALL** apply `HttpTimeoutPolicyControlPlaneRead` with attempt timeouts of 5s / 10s / 65s.

#### No Retry Timeout

**Where** a request is a client telemetry call, **then** the SDK **SHALL** apply `HttpTimeoutPolicyNoRetry` with a single 65-second timeout and no retry.

### Requirement: CancellationToken Handling

The SDK **SHALL** respect `CancellationToken` propagation through retry loops while maintaining partition health tracking.

#### Retry Loop Cancellation

**When** a `CancellationToken` passed to a public API is cancelled, **then** the SDK **SHALL** stop any active retry loop at the earliest opportunity.

#### ResourceThrottleRetryPolicy Cancellation

**While** `ResourceThrottleRetryPolicy` accepts a `CancellationToken`, the SDK **SHALL NOT** actively check it during retry wait intervals — cancellation **SHALL** only be observed at exception time.

#### OperationCanceledException Partition Marking

**When** `ClientRetryPolicy` catches an `OperationCanceledException`, **then** the SDK **SHALL** convert it to a PPAF/PPCB partition marking for future requests.

### Requirement: Region Failover Logic

The SDK **SHALL** maintain a `LocationCache` that tracks available read and write endpoints and governs region failover behavior.

#### Unavailable Region Handling

**When** a region is marked as unavailable, **then** the SDK **SHALL** move it to the end of the preference list. The unavailability marking **SHALL** expire after 5 minutes.

#### Endpoint Resolution Order

**When** resolving an endpoint for a request, the SDK **SHALL** use the following order: Preferred Locations → Available Write/Read Endpoints → Account-level fallback.

#### Account Information Refresh on Failover

**When** the SDK encounters a failover-triggering error (403/3, 403/51, or 503), **then** `GlobalEndpointManager` **SHALL** refresh account information to update the available regions.

## Configuration

| Parameter | Configurable Via | Default |
|-----------|-----------------|---------|
| Max throttle retries | `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests` | 9 |
| Max throttle wait | `CosmosClientOptions.MaxRetryWaitTimeOnRateLimitedRequests` | 60 seconds |
| Unavailable region expiry | Internal | 5 minutes (300s) |
| PPAF partition unavailability | Environment variable | 5 seconds |
| PPCB failback interval | Environment variable | 5 minutes |

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