# Cross-Region Hedging

## Purpose

The cross-region hedging availability strategy allows the SDK to send redundant parallel requests to multiple regions when the primary region is slow. This reduces tail latency and improves availability during regional degradation. When configured, the SDK fires hedge requests to secondary regions after a configurable threshold, returning the first final response.

## Public API Surface

### AvailabilityStrategy Factory

```csharp
public abstract class AvailabilityStrategy
{
    // Create a cross-region hedging strategy
    public static AvailabilityStrategy CrossRegionHedgingStrategy(
        TimeSpan threshold,
        TimeSpan? thresholdStep = null,
        bool enableMultiWriteRegionHedge = false);

    // Disable hedging for a specific request (overrides client-level)
    public static AvailabilityStrategy DisabledStrategy();
}
```

### Configuration

**Client-level:**
```csharp
CosmosClientOptions options = new CosmosClientOptions
{
    ApplicationPreferredRegions = new List<string> { "East US", "West US", "Central US" },
    AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
        threshold: TimeSpan.FromMilliseconds(1500),
        thresholdStep: TimeSpan.FromMilliseconds(1000))
};
```

**Request-level override:**
```csharp
ItemRequestOptions requestOptions = new ItemRequestOptions
{
    AvailabilityStrategy = AvailabilityStrategy.DisabledStrategy() // Disable for this request
};
```

## Behavioral Invariants

### Eligibility

Hedging is **disabled** (request sent directly) when ANY of these conditions are true:

1. **Non-Document resource type** — Only `ResourceType.Document` operations qualify. Database, container, stored procedure, and other metadata operations are never hedged.
2. **Write operations without multi-master** — Read operations are always eligible. Write operations require `enableMultiWriteRegionHedge = true` AND the account must support multiple write locations.
3. **Single region** — If `GlobalEndpointManager.ReadEndpoints.Count == 1`, there's nowhere to hedge.
4. **No preferred regions** — If neither `ApplicationRegion` nor `ApplicationPreferredRegions` is configured, hedging cannot determine target regions.

### Resolution Priority

Request-level strategy takes precedence over client-level:
1. `RequestOptions.AvailabilityStrategy` (if non-null)
2. `CosmosClientOptions.AvailabilityStrategy` (fallback)

Setting `DisabledStrategy()` on a request disables hedging for that request even if client-level hedging is configured.

### Request Timing and Ordering

Given `N` regions in `ApplicationPreferredRegions`, `threshold = T`, `thresholdStep = S`:

| Request | Fired At | Target Region | Retry Scope |
|---------|----------|--------------|-------------|
| Primary (0) | T = 0 | 1st preferred region | Full cross-region retries |
| Hedge 1 | T = threshold | 2nd preferred region | Local retries only |
| Hedge 2 | T = threshold + thresholdStep | 3rd preferred region | Local retries only |
| Hedge N-1 | T = threshold + (N-2) × thresholdStep | Nth preferred region | Local retries only |

**Example** (3 regions, threshold=100ms, step=50ms):
- T=0ms: Send to East US (primary)
- T=100ms: No response → send hedge to West US
- T=150ms: No response → send hedge to Central US
- First final response wins

### Primary vs Hedge Request Differences

| Aspect | Primary (request 0) | Hedge (request 1+) |
|--------|---------------------|---------------------|
| `ExcludeRegions` | None | All regions except target |
| Cross-region retry | ✅ Full `ClientRetryPolicy` retry | ❌ Local retries only |
| Handler pipeline | Independent instance | Independent instance |
| Cancellation | Cancelled when any hedge returns final | Cancelled when any other returns final |

### Cancellation Semantics

1. All requests (primary + hedges) share a linked `CancellationTokenSource`.
2. When ANY request returns a **final** response, `hedgeRequestsCancellationTokenSource.Cancel()` is called.
3. All in-flight requests observe cancellation immediately through the linked token.
4. The request body is cloned once at the start and shared across all hedged requests.
5. Each cloned request is disposed in a `finally` block after execution.

### Final vs Non-Final Responses

**Final results** (returned immediately, cancels other in-flight requests):
- All 1xx, 2xx, 3xx status codes
- 400 (Bad Request)
- 401 (Unauthorized)
- 404/0 (Not Found — document truly absent)
- 405 (Method Not Allowed)
- 409 (Conflict)
- 412 (Precondition Failed)
- 413 (Request Entity Too Large)

**Non-final results** (transient — continue waiting for other responses):
- 408 (Request Timeout)
- 404/1002 (Session Not Available)
- 429 (Too Many Requests)
- 500 (Internal Server Error)
- 503 (Service Unavailable)

When a non-final result arrives and there are still pending requests, the SDK skips any remaining threshold wait and immediately fires the next hedge.

### Thread Safety

1. Each hedged request traverses its own independent handler pipeline instance.
2. The linked `CancellationTokenSource` is the synchronization mechanism between parallel requests.
3. Response selection uses `Task.WhenAny` semantics — the first task to complete with a final result wins.
4. Race condition fix (#5613): All requests receive the linked CTS token (not the application's token), preventing abandoned tasks from accessing disposed request objects.

## Configuration

### Parameters

| Parameter | Type | Validation | Default |
|-----------|------|-----------|---------|
| `threshold` | `TimeSpan` | Must be > `TimeSpan.Zero` | Required |
| `thresholdStep` | `TimeSpan?` | Must be > `TimeSpan.Zero` if provided | `null` (no additional hedges) |
| `enableMultiWriteRegionHedge` | `bool` | — | `false` |

### SDK Default Strategy (Internal)

For PPAF-enabled clients, the SDK applies a default hedging strategy:
- Threshold: `min(1000ms, RequestTimeout / 2)`
- ThresholdStep: 500ms
- This is internal and not configurable by customers.

## Edge Cases

1. **ThresholdStep = null**: Only the primary and one hedge request are sent (at `threshold`). No additional hedges.
2. **All responses non-final**: The SDK returns the last response received after all regions are exhausted.
3. **Request body cloning**: The request `Content` stream is cloned to a `CloneableStream` once. All hedges share this clone.
4. **Disposed client**: `RequestInvokerHandler` validates client state before hedging. Disposed clients return error immediately.

## Interactions

- **Handler Pipeline**: Hedging executes in `RequestInvokerHandler` (position #1). Each hedge traverses its own pipeline. See `handler-pipeline` spec.
- **Retry Policies**: Primary requests get full `ClientRetryPolicy` behavior (cross-region retries). Hedged requests use `ExcludeRegions` to restrict to local retries only. See `retry-and-failover` spec.
- **PPAF**: SDK-default hedging strategy is applied automatically for PPAF-enabled clients with reduced thresholds.

## References

- Source: `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/AvailabilityStrategy.cs`
- Source: `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/CrossRegionHedgingAvailabilityStrategy.cs`
- Source: `Microsoft.Azure.Cosmos/src/Handler/RequestInvokerHandler.cs`
- Design: `docs/Cross Region Request Hedging.md`
- Fix: PR #5613 (race condition in hedge cancellation)
