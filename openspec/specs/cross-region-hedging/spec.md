# Cross-Region Hedging

## Purpose

Cross-region request hedging improves read latency during regional degradation by sending parallel requests to multiple regions and returning the first successful response.

## Requirements

### Requirement: Availability Strategy Configuration
The SDK SHALL provide a configurable availability strategy for cross-region hedging.

#### Enable hedging via client options
**Where** `CosmosClientOptions.AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(threshold: TimeSpan.FromMilliseconds(1500), thresholdStep: TimeSpan.FromMilliseconds(500))` and `ApplicationPreferredRegions` is set with multiple regions, **when** requests are made through this client, the SDK shall activate hedging behavior for eligible operations.

#### Disabled strategy
**Where** `AvailabilityStrategy.DisabledStrategy()` is configured or no strategy is set, **when** requests are made, the SDK shall not perform hedging and shall send requests to a single region.

#### Per-request override
**Where** a client-level availability strategy is configured, **when** `RequestOptions.AvailabilityStrategy` is set on a specific request, the SDK shall use the per-request strategy instead of the client-level strategy for that request.

#### No preferred regions
**If** `ApplicationPreferredRegions` is NOT set when an availability strategy is configured, **then** the SDK shall not apply any hedging behavior.

### Requirement: Hedging Trigger
The SDK SHALL initiate hedged requests after the configured threshold elapses without a response.

#### Primary responds before threshold
**While** the hedging threshold is 1500ms, **when** the primary region responds within 1500ms, the SDK shall return the primary response without sending any hedged request.

#### Primary exceeds threshold
**While** the hedging threshold is 1500ms, **when** the primary region has not responded after 1500ms, the SDK shall send a hedged request to the next region in `ApplicationPreferredRegions`.

#### Subsequent hedges at thresholdStep intervals
**While** the hedging threshold is 1500ms and thresholdStep is 500ms, **when** neither primary nor previous hedged requests have responded, the SDK shall send additional hedged requests at thresholdStep intervals (at 2000ms, 2500ms, and so on) until all preferred regions are tried.

### Requirement: Response Selection
The SDK SHALL return the first final response received from any region.

#### Final status codes
**When** any region returns a status code in {1xx, 2xx, 3xx, 400, 401, 404/0, 409, 405, 412, 413} during hedged requests, the SDK shall consider that response final, return it to the caller, and cancel outstanding hedged requests.

#### Non-final responses
**If** all regions return non-final status codes (e.g., transient 5xx) during hedged requests, **then** the SDK shall return the last response received to the caller.

### Requirement: Hedging Scope
The SDK SHALL apply hedging based on operation type.

#### Read operations
**Where** hedging is configured, **when** a read operation is performed (ReadItem, Query, ReadMany, ChangeFeed), the SDK shall apply hedging.

#### Write operations (single-master)
**While** operating against a single-master account with hedging configured, **when** a write operation is performed, the SDK shall send hedged requests to read regions (not the write region), enabling reads from read replicas during write region degradation.

#### Write operations (multi-master)
**While** operating against a multi-master account with `enableMultiWriteRegionHedge = true`, **when** a write operation is performed, the SDK shall send hedged requests to other write regions. The application is responsible for handling potential conflicts.

### Requirement: Independent Retry Per Hedge
The SDK SHALL ensure each hedged request has its own independent retry policy.

#### Isolated retry chains
**If** a hedged request to a secondary region encounters a transient error, **then** the SDK shall retry independently for that region without triggering cross-region retries (no hedging within hedging).

### Requirement: Hedging Diagnostics
The SDK SHALL include hedging metadata in diagnostics output.

#### Diagnostics content
**When** a hedged request completes and `response.Diagnostics.ToString()` is called, the SDK shall include hedge configuration (threshold, thresholdStep), a list of regions that received hedged requests, and per-region response details (status code, latency) in the output.

### Requirement: Default Hedging Configuration
The SDK SHALL use sensible defaults when hedging is enabled without explicit thresholds.

#### Default threshold
**Where** `AvailabilityStrategy.CrossRegionHedgingStrategy()` is called with default parameters, the SDK shall use `min(1000ms, RequestTimeout / 2)` as the threshold and 500ms as the thresholdStep.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/AvailabilityStrategy.cs` — public factory
- `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/CrossRegionHedgingAvailabilityStrategy.cs` — hedging implementation
- `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/DisabledAvailabilityStrategy.cs` — no-op strategy
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — `AvailabilityStrategy` property
- `Microsoft.Azure.Cosmos/src/RequestOptions/RequestOptions.cs` — per-request override
