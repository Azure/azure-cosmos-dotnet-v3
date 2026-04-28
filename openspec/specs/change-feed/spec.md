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
await processor.StopAsync();
```

## Requirements

### Requirement: Change Feed Modes

The SDK SHALL support two change feed modes with distinct behavior.

#### Incremental mode

**When** `ChangeFeedMode.LatestVersion` is used, the SDK SHALL return only the latest version of each item. Intermediate updates between polls SHALL be collapsed into the final state.

#### AllVersionsAndDeletes mode

**When** `ChangeFeedMode.AllVersionsAndDeletes` is used, the SDK SHALL return all intermediate versions and deletes within the configured retention window.

#### AllVersionsAndDeletes retention boundary

**If** a read in AllVersionsAndDeletes mode attempts to read beyond the retention window, the SDK SHALL return 400 Bad Request.

#### AllVersionsAndDeletes forces Gateway mode

**When** AllVersionsAndDeletes mode is used, the SDK SHALL force Gateway mode for split-handling logic, regardless of `CosmosClientOptions.ConnectionMode`.

### Requirement: FeedIterator Semantics

The SDK SHALL provide change feed results through the FeedIterator pattern with specific guarantees.

#### 304 Not Modified

**When** no changes exist since the last checkpoint, the SDK SHALL return a response with status code 304 (Not Modified) and an empty result set. Continuation tokens SHALL still be available in the response headers.

#### Transactional grouping

**When** items are committed in the same transaction, the SDK SHALL return them together in the same page, even if this exceeds `PageSizeHint`.

#### Ordering guarantee

**When** reading changes within a single partition, the SDK SHALL return changes ordered by logical sequence number (LSN). No ordering guarantee SHALL be provided across partitions.

#### PageSizeHint semantics

**Where** `ChangeFeedRequestOptions.PageSizeHint` is set, the SDK SHALL treat it as a hint, not a guarantee. Pages MAY contain fewer or more items than requested.

### Requirement: ChangeFeedProcessor

The SDK SHALL provide a high-level processor framework for distributed change feed consumption.

#### Lease container requirement

**When** building a ChangeFeedProcessor, a lease container SHALL be required. The lease container partition key SHOULD be `/id`.

#### Instance name requirement

**When** building a ChangeFeedProcessor, an instance name SHALL be required. It SHALL be unique per instance in a distributed processor cluster.

#### Automatic partition balancing

**When** instances are added or removed from a processor cluster, the SDK SHALL automatically rebalance partition ownership evenly across instances.

#### Auto-checkpointing

**When** using the default `ChangeFeedHandler<T>` delegate, the SDK SHALL auto-checkpoint after successful completion of each batch. For explicit checkpointing, `ChangeFeedHandlerWithManualCheckpoint<T>` SHALL be used.

#### Error handling

**When** an unhandled exception occurs in the user delegate, the SDK SHALL pause processing for that partition, invoke the `WithErrorNotification` callback, and retry after `PollInterval`.

#### Lease expiration

**If** a lease is not renewed within `LeaseExpirationInterval` (default 60s), the SDK SHALL treat it as expired and redistribute it to other instances.

#### StartFromBeginning with AllVersionsAndDeletes

**When** `WithStartFromBeginning()` is used with `AllVersionsAndDeletes` mode, the SDK SHALL prohibit this combination.

### Requirement: Lease Management

The SDK SHALL manage leases with optimistic concurrency.

#### Lease storage

**When** leases are created, the SDK SHALL store them as documents in the lease container with optimistic concurrency via ETags. Each lease SHALL track: partition range, owner instance, continuation token, and last timestamp.

#### Lease renewal

**While** a processor is running, the SDK SHALL renew held leases every `LeaseRenewInterval` (default 17s).

#### Lease acquisition

**While** a processor is running, the SDK SHALL check for unowned leases every `LeaseAcquireInterval` (default 13s).

#### Partition split handling

**When** a physical partition splits, the SDK SHALL automatically create new leases for the split ranges.

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