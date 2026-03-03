# Change Feed

## Purpose

Change feed provides an ordered stream of changes (creates, updates, and optionally deletes) made to items in a container. It supports both pull-based iteration and push-based distributed processing.

## Requirements

### Requirement: Change Feed Iterator
The SDK SHALL provide a pull-based iterator for reading changes from a container.

#### Scenario: Read all changes from beginning
- GIVEN a container with existing items
- WHEN `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental)` is called
- THEN all historical creates and updates are returned in order per partition

#### Scenario: Read changes from now
- GIVEN a container
- WHEN `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.Now())` is called
- THEN only changes occurring after this point are returned

#### Scenario: Read changes from specific time
- GIVEN a UTC timestamp
- WHEN `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.Time(dateTimeUtc))` is called
- THEN changes are returned starting from the first change after that timestamp (exclusive)

#### Scenario: Resume from continuation token
- GIVEN a continuation token from a previous change feed read
- WHEN `Container.GetChangeFeedIterator<T>(ChangeFeedStartFrom.ContinuationToken(token))` is called
- THEN reading resumes from the saved position

#### Scenario: No new changes available
- GIVEN no new changes since the last read
- WHEN `ReadNextAsync()` is called
- THEN a `FeedResponse<T>` is returned with status code 304 (Not Modified)
- AND `HasMoreResults` remains true (change feed never completes)

### Requirement: Change Feed Modes
The SDK SHALL support multiple change feed modes with different fidelity levels.

#### Scenario: Incremental mode (default)
- GIVEN `ChangeFeedMode.Incremental` (or `LatestVersion`)
- WHEN changes are read
- THEN only the latest version of each item is returned
- AND deletes are NOT included

#### Scenario: All Versions and Deletes mode
- GIVEN `ChangeFeedMode.AllVersionsAndDeletes` (or `FullFidelity`)
- WHEN changes are read
- THEN all intermediate versions of each item are returned
- AND delete events are included with metadata
- AND the container MUST have a change feed retention policy configured

### Requirement: Change Feed Processor
The SDK SHALL provide a distributed change feed processing framework with automatic partition assignment and checkpointing.

#### Scenario: Start processing
- GIVEN a processor built with `Container.GetChangeFeedProcessorBuilder<T>(processorName, handler)`
- AND configured with `.WithInstanceName(name).WithLeaseContainer(leaseContainer)`
- WHEN `processor.StartAsync()` is called
- THEN the processor begins polling for changes across all partitions
- AND distributes work across instances sharing the same processor name

#### Scenario: Automatic load balancing
- GIVEN multiple processor instances with the same processor name
- WHEN a new instance starts or an existing instance stops
- THEN partition leases are automatically rebalanced across running instances

#### Scenario: Automatic checkpointing
- GIVEN a `ChangesHandler<T>` delegate that completes successfully
- WHEN the delegate returns without exception
- THEN the checkpoint is automatically updated in the lease container

#### Scenario: Manual checkpointing
- GIVEN a processor built with `GetChangeFeedProcessorBuilderWithManualCheckpoint<T>`
- WHEN the delegate is invoked with a `checkpointAsync` function
- THEN the application controls when checkpoints are saved by calling `checkpointAsync()`

#### Scenario: Handler failure
- GIVEN a `ChangesHandler<T>` delegate that throws an exception
- WHEN the exception propagates
- THEN the change batch is retried from the last checkpoint
- AND the lease is not advanced

### Requirement: Change Feed Processor Configuration
The SDK SHALL support configuring processor timing and behavior.

#### Scenario: Poll interval
- GIVEN `.WithPollInterval(TimeSpan.FromSeconds(5))` is configured
- WHEN no new changes are available
- THEN the processor waits 5 seconds before polling again (default: 1 second)

#### Scenario: Max items per trigger
- GIVEN `.WithMaxItems(100)` is configured
- WHEN changes are available
- THEN at most 100 items are passed per delegate invocation

#### Scenario: Lease configuration
- GIVEN custom lease timing via `.WithLeaseConfiguration(acquireInterval, expirationInterval, renewInterval)`
- WHEN the processor runs
- THEN leases are acquired at `acquireInterval` (default: 13s), renewed at `renewInterval` (default: 10s), and expire after `expirationInterval` (default: 60s)

#### Scenario: Start from beginning
- GIVEN `.WithStartFromBeginning()` is configured
- WHEN the processor starts for the first time (no existing leases)
- THEN processing begins from the earliest available changes

### Requirement: Change Feed Estimator
The SDK SHALL provide a mechanism to estimate the number of pending changes.

#### Scenario: Estimate remaining work
- GIVEN an estimator built with `Container.GetChangeFeedEstimator(processorName, leaseContainer)`
- WHEN `estimator.ReadNextAsync()` is called
- THEN a `FeedResponse<ChangeFeedProcessorState>` is returned
- AND each item contains the estimated lag per partition

### Requirement: Change Feed with FeedRange
The SDK SHALL support reading changes from a specific subset of partitions.

#### Scenario: Scoped to FeedRange
- GIVEN a `FeedRange` obtained from `Container.GetFeedRangesAsync()`
- WHEN `ChangeFeedStartFrom.Beginning(feedRange)` is used
- THEN only changes within that partition range are returned

### Requirement: Change Feed Ordering
The SDK SHALL guarantee ordering within a logical partition.

#### Scenario: Within-partition ordering
- GIVEN multiple changes to items with the same partition key
- WHEN changes are read via change feed
- THEN changes appear in the order they were committed

#### Scenario: Cross-partition ordering
- GIVEN changes across different partitions
- WHEN changes are read
- THEN ordering is guaranteed only within each partition, not across partitions

## Key Source Files
- `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedIteratorCore.cs` — iterator implementation
- `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedStartFrom.cs` — start position types
- `Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedMode.cs` — mode selection
- `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/ChangeFeedProcessor.cs` — processor interface
- `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/ChangeFeedProcessorBuilder.cs` — builder
- `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/LeaseManagement/` — lease management
