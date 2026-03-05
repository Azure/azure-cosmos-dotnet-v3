# Change Feed

## Purpose

The Azure Cosmos DB change feed provides an ordered log of changes (creates, updates, and optionally deletes) to items in a container. The .NET SDK supports two consumption patterns: a low-level `FeedIterator`-based pull model for fine-grained control, and a high-level `ChangeFeedProcessor` framework for distributed, resilient consumption with automatic lease management and partition balancing.

## Public API Surface

### Change Feed Modes

| Mode | Header | Returns | Requirements |
|------|--------|---------|-------------|
| **Incremental** (LatestVersion) | `A-IM: Incremental` | Latest state of items (creates + updates) | Default; no special container config |
| **AllVersionsAndDeletes** (FullFidelity) | `A-IM: FullFidelityFeed` | All intermediate versions + deletes within retention window | Container must have `ChangeFeedPolicy` with retention; forces Gateway mode |

### ChangeFeedStartFrom Options

| Factory Method | Behavior |
|---------------|----------|
| `ChangeFeedStartFrom.Beginning()` | Start from container creation; catch all historical changes |
| `ChangeFeedStartFrom.Now()` | Start from current instant; only future changes |
| `ChangeFeedStartFrom.Time(DateTime utcTime)` | Start from specific UTC timestamp (exclusive); `DateTime.Kind` must be `Utc` |
| `ChangeFeedStartFrom.ContinuationToken(string)` | Resume from a saved checkpoint token |

All options except `ContinuationToken` accept an optional `FeedRange` parameter for partition-specific reading.

### FeedIterator-Based Consumption

```csharp
FeedIterator<T> iterator = container.GetChangeFeedIterator<T>(
    ChangeFeedStartFrom.Now(),
    ChangeFeedMode.LatestVersion);

while (iterator.HasMoreResults)
{
    FeedResponse<T> response = await iterator.ReadNextAsync();
    if (response.StatusCode == HttpStatusCode.NotModified)
    {
        // No changes — save continuation token, sleep, retry later
        string token = response.Headers.ContinuationToken;
        await Task.Delay(pollInterval);
        continue;
    }
    foreach (T item in response) { /* process change */ }
}
```

### ChangeFeedProcessor

```csharp
ChangeFeedProcessor processor = container
    .GetChangeFeedProcessorBuilder<MyItem>("processorName", HandleChangesAsync)
    .WithInstanceName("host-1")
    .WithLeaseContainer(leaseContainer)
    .WithPollInterval(TimeSpan.FromSeconds(5))
    .WithStartFromBeginning()
    .WithErrorNotification(HandleErrorAsync)
    .Build();

await processor.StartAsync();
// ... runs until stopped
await processor.StopAsync();
```

## Behavioral Invariants

### Change Feed Modes

1. **Incremental mode** returns only the latest version of each item. Intermediate updates between polls are collapsed into the final state.
2. **AllVersionsAndDeletes mode** returns all intermediate versions and deletes within the configured retention window. Reading beyond the retention window returns 400 Bad Request.
3. **AllVersionsAndDeletes forces Gateway mode** for split-handling logic, regardless of `CosmosClientOptions.ConnectionMode`.

### FeedIterator Semantics

1. **304 Not Modified**: Indicates no changes since last checkpoint. The response is empty but includes a continuation token for resumption.
2. **Continuation tokens are always available** in response headers, even on 304 responses.
3. **Transactional grouping**: Items committed in the same transaction are returned together in the same page, even if this exceeds `PageSizeHint`.
4. **`PageSizeHint`** is a hint, not a guarantee. Pages may contain fewer or more items than requested.
5. **Ordering**: Changes within a single partition are ordered by logical sequence number (LSN). No ordering guarantee across partitions.

### ChangeFeedProcessor

1. **Lease container required**: Must be a Cosmos container (shared or dedicated). Partition key should be `/id`.
2. **Instance name required**: Identifies this host/pod in a distributed processor cluster. Must be unique per instance.
3. **Automatic partition balancing**: The processor distributes partitions evenly across instances. When instances are added or removed, partitions are rebalanced automatically.
4. **Auto-checkpointing**: The default `ChangeFeedHandler<T>` delegate auto-checkpoints after successful completion. Use `ChangeFeedHandlerWithManualCheckpoint<T>` for explicit checkpointing.
5. **Error handling**: Unhandled exceptions in the delegate pause processing for that partition. The `WithErrorNotification` callback is invoked. Processing retries after `PollInterval`.
6. **Lease expiration**: Leases expire after `LeaseExpirationInterval` (default 60s) if not renewed. Expired leases are redistributed to other instances.
7. **`WithStartFromBeginning()` is prohibited** with `AllVersionsAndDeletes` mode.

### Lease Management

1. Leases are stored as documents in the lease container with optimistic concurrency via ETags.
2. Each lease tracks: partition range, owner instance, continuation token, last timestamp.
3. **Lease renewal** occurs every `LeaseRenewInterval` (default 17s).
4. **Lease acquisition** checked every `LeaseAcquireInterval` (default 13s).
5. **Partition splits** are handled automatically: new leases are created for split ranges.

## Configuration

### ChangeFeedRequestOptions

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `PageSizeHint` | `int?` | `null` (server default) | Batch size hint; transaction-aware |

### ChangeFeedProcessor Timing Options

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `PollInterval` | 5 seconds | Delay between empty polls |
| `LeaseAcquireInterval` | 13 seconds | How often to check for unowned leases |
| `LeaseExpirationInterval` | 60 seconds | Lease validity window |
| `LeaseRenewInterval` | 17 seconds | How often to refresh held leases |

## Error Handling

| Exception | Trigger | Recovery |
|-----------|---------|----------|
| `MalformedChangeFeedContinuationTokenException` | Invalid/corrupted continuation token | Restart from `Beginning` or `Now` |
| `LeaseLostException` | Lease expired or stolen during processing | Automatic — partition reassigned |
| `FeedRangeGoneException` | Partition split/merge during iteration | Automatic — ranges refreshed |
| Delegate exceptions | Unhandled exception in user code | Partition paused; retried after poll interval |

## Interactions

- **Handler Pipeline**: Change feed requests flow through the full pipeline with `IsPartitionKeyRangeHandlerRequired = true`. See `handler-pipeline` spec.
- **Retry Policies**: Change feed page fetches are retried per `retry-and-failover` spec.
- **Partition Keys**: Change feed can be scoped to a `FeedRange` (physical partition). See `partition-keys` spec.
- **Serialization**: `FeedIterator<T>` uses the container's serializer. See `serialization` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/ChangeFeed/`
- Source: `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/`
- Source: `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedMode.cs`
- Source: `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedStartFrom.cs`
