## ADDED Requirements

### Requirement: Detect hedging status from diagnostics
The `CosmosDiagnostics` class SHALL expose a public virtual method `IsHedged()` that returns `true` when the availability strategy activated cross-region hedging for the request, and `false` otherwise.

#### Scenario: Non-hedged request returns false
- **WHEN** a request completes without activating the hedging availability strategy (e.g., no availability strategy configured, or the strategy determined hedging was not needed)
- **THEN** `response.Diagnostics.IsHedged()` SHALL return `false`

#### Scenario: Hedged request returns true when primary wins
- **WHEN** a request activates cross-region hedging AND the primary region responds before any hedge request
- **THEN** `response.Diagnostics.IsHedged()` SHALL return `true`

#### Scenario: Hedged request returns true when hedge wins
- **WHEN** a request activates cross-region hedging AND a hedge request to a secondary region responds first
- **THEN** `response.Diagnostics.IsHedged()` SHALL return `true`

#### Scenario: Default implementation returns false
- **WHEN** a custom subclass of `CosmosDiagnostics` does not override `IsHedged()`
- **THEN** the default implementation SHALL return `false`

#### Scenario: No performance impact on non-hedged path
- **WHEN** `IsHedged()` is called on a response that was not hedged
- **THEN** the method SHALL return in O(1) time without walking the diagnostics trace tree, performing string operations, or allocating memory

### Requirement: Retrieve hedged regions from diagnostics
The `CosmosDiagnostics` class SHALL expose a public virtual method `GetHedgedRegions()` that returns all regions that received hedge requests, in the order they were dispatched.

#### Scenario: Returns all hedged regions when primary wins
- **WHEN** a request was hedged across regions ["East US", "West US"] AND the primary region "East US" responded first
- **THEN** `response.Diagnostics.GetHedgedRegions()` SHALL return a list containing `["East US"]` (only the primary was dispatched before a result arrived)

#### Scenario: Returns all hedged regions when hedge wins
- **WHEN** a request was hedged across regions ["East US", "West US", "North Europe"] AND the hedge to "West US" responded first after both "East US" and "West US" were dispatched
- **THEN** `response.Diagnostics.GetHedgedRegions()` SHALL return a list containing `["East US", "West US"]`

#### Scenario: Returns all regions when all hedges dispatched
- **WHEN** a request was hedged AND all available regions ["East US", "West US", "North Europe"] were dispatched before any responded
- **THEN** `response.Diagnostics.GetHedgedRegions()` SHALL return a list containing `["East US", "West US", "North Europe"]`

#### Scenario: Returns empty list when not hedged
- **WHEN** a request was not hedged
- **THEN** `response.Diagnostics.GetHedgedRegions()` SHALL return an empty `IReadOnlyList<string>`

#### Scenario: Default implementation returns empty list
- **WHEN** a custom subclass of `CosmosDiagnostics` does not override `GetHedgedRegions()`
- **THEN** the default implementation SHALL return an empty `IReadOnlyList<string>`

#### Scenario: No performance impact
- **WHEN** `GetHedgedRegions()` is called
- **THEN** the method SHALL return in O(1) time without walking the diagnostics trace tree, performing string operations, or allocating memory

### Requirement: Hedging flag set by availability strategy
The `CrossRegionHedgingAvailabilityStrategy` SHALL set the hedging detection fields on the response diagnostics before returning the response, ensuring the public APIs reflect the correct hedging state.

#### Scenario: Flag set when hedging activates and primary region responds first
- **WHEN** hedging is activated AND the primary region (request 0) returns a final result before any hedge timer fires
- **THEN** the response diagnostics SHALL have `IsHedged()` return `true` AND `GetHedgedRegions()` return the list of regions that were dispatched (at minimum the primary region)

#### Scenario: Flag set when hedge region responds first
- **WHEN** hedging is activated AND a hedge request to a secondary region returns a final result first
- **THEN** the response diagnostics SHALL have `IsHedged()` return `true` AND `GetHedgedRegions()` return all regions that had requests dispatched up to that point

#### Scenario: Flag not set when hedging strategy skips
- **WHEN** the hedging strategy determines the request should not be hedged (e.g., non-document resource type, single region)
- **THEN** the response diagnostics SHALL have `IsHedged()` return `false` AND `GetHedgedRegions()` return an empty list

### Requirement: Hedging detection available on exceptions
When a `CosmosException` or `CosmosOperationCanceledException` is thrown from a hedged request, the `Diagnostics` property on the exception SHALL support the same `IsHedged()` and `GetHedgedRegions()` methods.

#### Scenario: Exception from hedged request reflects hedging state
- **WHEN** a hedged request results in a `CosmosException`
- **THEN** `exception.Diagnostics.IsHedged()` SHALL return `true` if hedging was activated

#### Scenario: Cancellation exception from hedged request
- **WHEN** a hedged request is cancelled via the application-provided cancellation token
- **THEN** the `CosmosOperationCanceledException.Diagnostics.IsHedged()` SHALL return `true` if hedging was activated before cancellation
