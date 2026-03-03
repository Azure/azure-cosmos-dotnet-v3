# Cross-Region Hedging

## Purpose

Cross-region request hedging improves read latency during regional degradation by sending parallel requests to multiple regions and returning the first successful response.

## Requirements

### Requirement: Availability Strategy Configuration
The SDK SHALL provide a configurable availability strategy for cross-region hedging.

#### Scenario: Enable hedging via client options
- GIVEN `CosmosClientOptions.AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(threshold: TimeSpan.FromMilliseconds(1500), thresholdStep: TimeSpan.FromMilliseconds(500))`
- AND `ApplicationPreferredRegions` is set with multiple regions
- WHEN requests are made through this client
- THEN hedging behavior is active for eligible operations

#### Scenario: Disabled strategy
- GIVEN `AvailabilityStrategy.DisabledStrategy()` or no strategy configured
- WHEN requests are made
- THEN no hedging occurs and requests go to a single region

#### Scenario: Per-request override
- GIVEN a client-level availability strategy is configured
- WHEN `RequestOptions.AvailabilityStrategy` is set on a specific request
- THEN the per-request strategy overrides the client-level strategy for that request

#### Scenario: No preferred regions
- GIVEN `ApplicationPreferredRegions` is NOT set
- WHEN an availability strategy is configured
- THEN the SDK SHALL NOT apply any hedging behavior

### Requirement: Hedging Trigger
The SDK SHALL initiate hedged requests after the configured threshold elapses without a response.

#### Scenario: Primary responds before threshold
- GIVEN threshold is 1500ms
- WHEN the primary region responds within 1500ms
- THEN no hedged request is sent
- AND the primary response is returned

#### Scenario: Primary exceeds threshold
- GIVEN threshold is 1500ms
- WHEN the primary region has not responded after 1500ms
- THEN a hedged request is sent to the next region in `ApplicationPreferredRegions`

#### Scenario: Subsequent hedges at thresholdStep intervals
- GIVEN threshold is 1500ms and thresholdStep is 500ms
- WHEN neither primary nor first hedge has responded
- THEN a second hedge is sent at 2000ms (threshold + thresholdStep)
- AND a third hedge is sent at 2500ms (threshold + 2 * thresholdStep)
- AND hedging continues until all preferred regions are tried

### Requirement: Response Selection
The SDK SHALL return the first final response received from any region.

#### Scenario: Final status codes
- GIVEN hedged requests are in flight to multiple regions
- WHEN any region returns a status code in {1xx, 2xx, 3xx, 400, 401, 404/0, 409, 405, 412, 413}
- THEN that response is considered final and returned to the caller
- AND outstanding hedged requests are cancelled

#### Scenario: Non-final responses
- GIVEN all regions return non-final status codes (e.g., transient 5xx)
- WHEN the last hedged request completes
- THEN the last response received is returned to the caller

### Requirement: Hedging Scope
The SDK SHALL apply hedging based on operation type.

#### Scenario: Read operations
- GIVEN hedging is configured
- WHEN a read operation is performed (ReadItem, Query, ReadMany, ChangeFeed)
- THEN hedging is applied

#### Scenario: Write operations (single-master)
- GIVEN a single-master account
- WHEN a write operation is performed
- THEN hedging sends requests to read regions (not the write region)
- AND this enables reads from read replicas during write region degradation

#### Scenario: Write operations (multi-master)
- GIVEN a multi-master account AND `enableMultiWriteRegionHedge = true`
- WHEN a write operation is performed
- THEN hedging sends requests to other write regions
- AND the application is responsible for handling potential conflicts

### Requirement: Independent Retry Per Hedge
The SDK SHALL ensure each hedged request has its own independent retry policy.

#### Scenario: Isolated retry chains
- GIVEN a hedged request to Region 2 encounters a transient error
- WHEN the retry policy evaluates the error
- THEN Region 2's retry operates independently
- AND the hedged request does NOT trigger cross-region retries (no hedging within hedging)

### Requirement: Hedging Diagnostics
The SDK SHALL include hedging metadata in diagnostics output.

#### Scenario: Diagnostics content
- GIVEN a hedged request completes
- WHEN `response.Diagnostics.ToString()` is called
- THEN the output includes hedge configuration (threshold, thresholdStep)
- AND a list of regions that received hedged requests
- AND per-region response details (status code, latency)

### Requirement: Default Hedging Configuration
The SDK SHALL use sensible defaults when hedging is enabled without explicit thresholds.

#### Scenario: Default threshold
- GIVEN `AvailabilityStrategy.CrossRegionHedgingStrategy()` is called with default parameters
- WHEN the SDK computes the threshold
- THEN it uses `min(1000ms, RequestTimeout / 2)` as the threshold
- AND 500ms as the thresholdStep

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/AvailabilityStrategy.cs` — public factory
- `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/CrossRegionHedgingAvailabilityStrategy.cs` — hedging implementation
- `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/DisabledAvailabilityStrategy.cs` — no-op strategy
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — `AvailabilityStrategy` property
- `Microsoft.Azure.Cosmos/src/RequestOptions/RequestOptions.cs` — per-request override
