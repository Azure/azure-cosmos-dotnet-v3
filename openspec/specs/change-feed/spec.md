# Change Feed

## Purpose

Change feed provides an ordered stream of changes (creates, updates, and optionally deletes) made to items in a container. It supports both pull-based iteration and push-based distributed processing.

## Requirements

### Requirement: Change Feed Iterator
The SDK SHALL provide a pull-based iterator for reading changes from a container.

#### Read all changes from beginning
**When** `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental)` is called on a container with existing items, the SDK shall return all historical creates and updates in order per partition.

#### Read changes from now
**When** `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.Now())` is called, the SDK shall return only changes occurring after this point.

#### Read changes from specific time
**When** `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.Time(dateTimeUtc))` is called with a UTC timestamp, the SDK shall return changes starting from the first change after that timestamp (exclusive).

#### Resume from continuation token
**When** `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.ContinuationToken(token))` is called with a continuation token from a previous change feed read, the SDK shall resume reading from the saved position.

#### No new changes available
**While** no new changes are available since the last read, **when** `ReadNextAsync()` is called, the SDK shall return a `FeedResponse<T>` with status code 304 (Not Modified) and `HasMoreResults` shall remain true (change feed never completes).

### Requirement: Change Feed Modes
The SDK SHALL support multiple change feed modes with different fidelity levels.

#### Incremental mode (default)
**Where** `ChangeFeedMode.Incremental` (or `LatestVersion`) is used, **when** changes are read, the SDK shall return only the latest version of each item and shall not include deletes.

#### All Versions and Deletes mode
**Where** `ChangeFeedMode.AllVersionsAndDeletes` (or `FullFidelity`) is used, **when** changes are read, the SDK shall return all intermediate versions of each item and include delete events with metadata. The container MUST have a change feed retention policy configured.

### Requirement: Change Feed Processor
The SDK SHALL provide a distributed change feed processing framework with automatic partition assignment and checkpointing.

#### Start processing
**When** `processor.StartAsync()` is called on a processor built with `Container.GetChangeFeedProcessorBuilder<T>(processorName, handler)` and configured with `.WithInstanceName(name).WithLeaseContainer(leaseContainer)`, the SDK shall begin polling for changes across all partitions and distribute work across instances sharing the same processor name.

#### Automatic load balancing
**While** multiple processor instances share the same processor name, **when** a new instance starts or an existing instance stops, the SDK shall automatically rebalance partition leases across running instances.

#### Automatic checkpointing
**When** a `ChangesHandler<T>` delegate returns without exception, the SDK shall automatically update the checkpoint in the lease container.

#### Manual checkpointing
**Where** a processor is built with `GetChangeFeedProcessorBuilderWithManualCheckpoint<T>`, **when** the delegate is invoked, the SDK shall provide a `checkpointAsync` function that allows the application to control when checkpoints are saved.

#### Handler failure
**If** a `ChangesHandler<T>` delegate throws an exception, **then** the SDK shall retry the change batch from the last checkpoint and shall not advance the lease.

### Requirement: Change Feed Processor Configuration
The SDK SHALL support configuring processor timing and behavior.

#### Poll interval
**Where** `.WithPollInterval(TimeSpan.FromSeconds(5))` is configured, **when** no new changes are available, the SDK shall wait 5 seconds before polling again (default: 1 second).

#### Max items per trigger
**Where** `.WithMaxItems(100)` is configured, **when** changes are available, the SDK shall pass at most 100 items per delegate invocation.

#### Lease configuration
**Where** custom lease timing is configured via `.WithLeaseConfiguration(acquireInterval, expirationInterval, renewInterval)`, **when** the processor runs, the SDK shall acquire leases at `acquireInterval` (default: 13s), renew at `renewInterval` (default: 10s), and expire leases after `expirationInterval` (default: 60s).

#### Start from beginning
**Where** `.WithStartFromBeginning()` is configured, **when** the processor starts for the first time (no existing leases), the SDK shall begin processing from the earliest available changes.

### Requirement: Change Feed Estimator
The SDK SHALL provide a mechanism to estimate the number of pending changes.

#### Estimate remaining work
**When** `estimator.ReadNextAsync()` is called on an estimator built with `Container.GetChangeFeedEstimator(processorName, leaseContainer)`, the SDK shall return a `FeedResponse<ChangeFeedProcessorState>` where each item contains the estimated lag per partition.

### Requirement: Change Feed with FeedRange
The SDK SHALL support reading changes from a specific subset of partitions.

#### Scoped to FeedRange
**When** `ChangeFeedStartFrom.Beginning(feedRange)` is used with a `FeedRange` obtained from `Container.GetFeedRangesAsync()`, the SDK shall return only changes within that partition range.

### Requirement: Change Feed Ordering
The SDK SHALL guarantee ordering within a logical partition.

#### Within-partition ordering
**When** changes to items with the same partition key are read via change feed, the SDK shall return changes in the order they were committed.

#### Cross-partition ordering
**When** changes across different partitions are read, the SDK shall guarantee ordering only within each partition, not across partitions.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedIteratorCore.cs` — iterator implementation
- `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedStartFrom.cs` — start position types
- `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedMode.cs` — mode selection
- `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/ChangeFeedProcessor.cs` — processor interface
- `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/ChangeFeedProcessorBuilder.cs` — builder
- `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/LeaseManagement/` — lease management
