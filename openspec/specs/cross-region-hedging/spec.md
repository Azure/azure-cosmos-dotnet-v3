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

## Requirements

### Requirement: Eligibility

The SDK **SHALL** disable hedging and send the request directly when any disqualifying condition is met.

#### Non-Document Resource Type

**When** the operation targets a resource type other than `ResourceType.Document` (e.g., Database, Container, StoredProcedure, or other metadata resources), the SDK **SHALL NOT** hedge the request.

#### Write Operations Without Multi-Master

**When** the operation is a write, the SDK **SHALL** hedge the request only **if** `enableMultiWriteRegionHedge = true` **AND** the account supports multiple write locations. **If** either condition is not met, the SDK **SHALL** send the write to the primary region without hedging. Read operations **SHALL** always be eligible for hedging.

#### Single Region

**When** `GlobalEndpointManager.ReadEndpoints.Count == 1`, the SDK **SHALL NOT** hedge the request, as there is no secondary region available.

#### No Preferred Regions

**When** neither `ApplicationRegion` nor `ApplicationPreferredRegions` is configured, the SDK **SHALL NOT** hedge the request, as it cannot determine target regions.

### Requirement: Resolution Priority

The SDK **SHALL** resolve the availability strategy by applying request-level configuration over client-level configuration.

#### Request-Level Override

**When** `RequestOptions.AvailabilityStrategy` is non-null, the SDK **SHALL** use it as the effective strategy, ignoring `CosmosClientOptions.AvailabilityStrategy`.

#### Client-Level Fallback

**When** `RequestOptions.AvailabilityStrategy` is null, the SDK **SHALL** fall back to `CosmosClientOptions.AvailabilityStrategy`.

#### Disabled Strategy Override

**When** `DisabledStrategy()` is set on a request, the SDK **SHALL** disable hedging for that request even if client-level hedging is configured.

### Requirement: Request Timing

The SDK **SHALL** fire hedge requests according to the configured threshold schedule.

#### Hedge Scheduling

**Given** `N` regions in `ApplicationPreferredRegions`, `threshold = T`, and `thresholdStep = S`, the SDK **SHALL** fire requests as follows:

| Request | Fired At | Target Region | Retry Scope |
|---------|----------|--------------|-------------|
| Primary (0) | T = 0 | 1st preferred region | Full cross-region retries |
| Hedge 1 | T = threshold | 2nd preferred region | Local retries only |
| Hedge 2 | T = threshold + thresholdStep | 3rd preferred region | Local retries only |
| Hedge N-1 | T = threshold + (N-2) x thresholdStep | Nth preferred region | Local retries only |

**Example** (3 regions, threshold=100ms, step=50ms):
- T=0ms: Send to East US (primary)
- T=100ms: No response - send hedge to West US
- T=150ms: No response - send hedge to Central US
- First final response wins

#### Primary vs Hedge Differences

The SDK **SHALL** differentiate primary and hedge requests as follows:

| Aspect | Primary (request 0) | Hedge (request 1+) |
|--------|---------------------|---------------------|
| `ExcludeRegions` | None | All regions except target |
| Cross-region retry | Full `ClientRetryPolicy` retry | Local retries only |
| Handler pipeline | Independent instance | Independent instance |
| Cancellation | Cancelled when any hedge returns final | Cancelled when any other returns final |

### Requirement: Cancellation

The SDK **SHALL** cancel all remaining in-flight requests as soon as a final response is received.

#### Linked Cancellation Token

**While** hedged requests are in flight, the SDK **SHALL** link all requests (primary + hedges) through a shared `CancellationTokenSource`.

#### Final Response Triggers Cancellation

**When** any request returns a **final** response, the SDK **SHALL** call `hedgeRequestsCancellationTokenSource.Cancel()` so that all other in-flight requests observe cancellation immediately through the linked token.

#### Request Body Cloning

**Before** dispatching hedge requests, the SDK **SHALL** clone the request `Content` stream to a `CloneableStream` once. All hedges **SHALL** share this clone.

#### Cloned Request Disposal

**After** each hedged request completes execution, the SDK **SHALL** dispose its cloned request in a `finally` block.

### Requirement: Final vs Non-Final Response Classification

The SDK **SHALL** classify every response as either final or non-final to determine whether to return it or continue waiting.

#### Final Responses

**When** a response has any of the following status codes, the SDK **SHALL** treat it as final, return it immediately, and cancel all other in-flight requests:

- All 1xx, 2xx, 3xx status codes
- 400 (Bad Request)
- 401 (Unauthorized)
- 404/0 (Not Found - document truly absent)
- 405 (Method Not Allowed)
- 409 (Conflict)
- 412 (Precondition Failed)
- 413 (Request Entity Too Large)

#### Non-Final Responses

**When** a response has any of the following status codes, the SDK **SHALL** treat it as non-final (transient) and continue waiting for other responses:

- 408 (Request Timeout)
- 404/1002 (Session Not Available)
- 429 (Too Many Requests)
- 500 (Internal Server Error)
- 503 (Service Unavailable)

#### Accelerated Hedge on Non-Final

**When** a non-final result arrives **and** there are still pending requests, the SDK **SHALL** skip any remaining threshold wait and immediately fire the next hedge.

### Requirement: Thread Safety

The SDK **SHALL** ensure safe concurrent execution of hedged requests.

#### Independent Handler Pipelines

**While** processing hedged requests, the SDK **SHALL** route each hedged request through its own independent handler pipeline instance.

#### Cancellation as Synchronization

The SDK **SHALL** use the linked `CancellationTokenSource` as the sole synchronization mechanism between parallel requests.

#### First-Final-Wins Selection

**When** multiple requests complete, the SDK **SHALL** use `Task.WhenAny` semantics - the first task to complete with a final result wins.

#### Linked CTS Token Distribution (Race Condition Fix #5613)

The SDK **SHALL** pass the linked CTS token (not the application's original token) to all requests, preventing abandoned tasks from accessing disposed request objects.

## Configuration

### Parameters

| Parameter | Type | Validation | Default |
|-----------|------|-----------|---------|
| `threshold` | `TimeSpan` | Must be > `TimeSpan.Zero` | Required |
| `thresholdStep` | `TimeSpan?` | Must be > `TimeSpan.Zero` if provided | `null` (no additional hedges) |
| `enableMultiWriteRegionHedge` | `bool` | - | `false` |

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