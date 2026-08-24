## ADDED Requirements

### Requirement: Detect whether hedging started for an operation

The `CosmosDiagnostics` class SHALL expose a public virtual method
`HedgingStarted()` returning `bool`. The method SHALL return `true` when
the SDK dispatched at least one hedge-arm request for the operation, and
`false` otherwise.

#### Scenario: Non-hedged single-region request

- **WHEN** an operation completes without any hedge arm being dispatched
- **THEN** `response.Diagnostics.HedgingStarted()` SHALL return `false`

#### Scenario: Hedged request where a hedge arm was dispatched

- **WHEN** an operation activated cross-region hedging AND at least one
  hedge arm dispatch was issued before a final result arrived
- **THEN** `response.Diagnostics.HedgingStarted()` SHALL return `true`

#### Scenario: Hedged-eligible request where the primary won under threshold

- **WHEN** the cross-region hedging availability strategy is configured AND
  the primary region produced a final response before the first hedge
  threshold elapsed
- **THEN** no hedge arm was dispatched AND
  `response.Diagnostics.HedgingStarted()` SHALL return `false`

#### Scenario: Default implementation returns false

- **WHEN** a custom subclass of `CosmosDiagnostics` does not override
  `HedgingStarted()`
- **THEN** the default implementation SHALL return `false`

#### Scenario: O(1) accessor

- **WHEN** `HedgingStarted()` is called on any `CosmosDiagnostics` instance
- **THEN** the method SHALL return in O(1) time without walking the trace
  tree, allocating memory, or performing string operations

### Requirement: Enumerate dispatched regions with reason

The `CosmosDiagnostics` class SHALL expose a public virtual method
`GetRequestedRegions()` returning `IReadOnlyList<RequestedRegion>`. The
returned list SHALL contain one entry for every region the SDK dispatched
to during the operation, in observed dispatch order, each tagged with the
reason the SDK chose that region.

#### Scenario: Single-region happy path

- **WHEN** an operation runs end-to-end against the primary region with
  no retries
- **THEN** `GetRequestedRegions()` SHALL contain exactly one entry tagged
  `RequestedRegionReason.Initial`

#### Scenario: Same-region retry

- **WHEN** the SDK retries an operation against the same region under the
  operation-retry policy
- **THEN** `GetRequestedRegions()` SHALL contain at least two entries: the
  initial dispatch (`Initial`) followed by one or more retries tagged
  `RequestedRegionReason.OperationRetry`

#### Scenario: Region failover retry

- **WHEN** the SDK retries an operation against a different preferred
  region after deciding to fail over
- **THEN** the subsequent dispatch SHALL be recorded with reason
  `RequestedRegionReason.RegionFailover`

#### Scenario: Hedge arm dispatch

- **WHEN** the cross-region hedging strategy dispatches a hedge arm for
  this operation
- **THEN** that dispatch SHALL be recorded with reason
  `RequestedRegionReason.Hedging`

#### Scenario: Default implementation returns empty list

- **WHEN** a custom subclass of `CosmosDiagnostics` does not override
  `GetRequestedRegions()`
- **THEN** the default implementation SHALL return an empty
  `IReadOnlyList<RequestedRegion>`

#### Scenario: Snapshot semantics

- **WHEN** `GetRequestedRegions()` is called concurrently with the request
  pipeline still appending entries
- **THEN** the returned list SHALL be a stable snapshot taken under the
  internal lock AND subsequent mutations SHALL NOT be observed through
  that snapshot

### Requirement: Enumerate responded regions

The `CosmosDiagnostics` class SHALL expose a public virtual method
`GetRespondedRegions()` returning `IReadOnlyList<string>`. The returned
list SHALL contain the name of every region that produced a response
during the operation, in arrival order, with duplicates allowed.

#### Scenario: Single-region happy path

- **WHEN** an operation completes with one response from the primary
  region
- **THEN** `GetRespondedRegions()` SHALL contain `["{primary}"]`

#### Scenario: Hedge arm wins

- **WHEN** the hedge arm to a secondary region produces a final response
  before the primary
- **THEN** `GetRespondedRegions()` SHALL contain at least the secondary
  region name (and SHALL also contain the primary if it produced any
  observable response before cancellation)

#### Scenario: Duplicates are preserved

- **WHEN** the same region produces multiple responses for one operation
  (e.g., the gateway response followed by replica responses)
- **THEN** every response SHALL be recorded; the list SHALL preserve
  arrival order

#### Scenario: Default implementation returns empty list

- **WHEN** a custom subclass of `CosmosDiagnostics` does not override
  `GetRespondedRegions()`
- **THEN** the default implementation SHALL return an empty
  `IReadOnlyList<string>`

### Requirement: `RequestedRegion` value type

The SDK SHALL expose a public readonly struct `RequestedRegion` with
properties `RegionName` (string) and `Reason` (`RequestedRegionReason`),
implementing `IEquatable<RequestedRegion>`, with case-insensitive name
equality, a `GetHashCode` consistent with `Equals`, `==`/`!=` operators,
and a `ToString` of the form `"{regionName}:{reason}"`.

#### Scenario: Equality is case-insensitive on region name

- **WHEN** two `RequestedRegion` values have the same reason and region
  names that differ only in casing
- **THEN** `Equals`, `==`, and `GetHashCode` SHALL agree that the two
  values are equal

#### Scenario: Null region name is rejected

- **WHEN** a caller constructs `RequestedRegion(null, anyReason)`
- **THEN** the constructor SHALL throw `ArgumentNullException`

### Requirement: `RequestedRegionReason` enumeration

The SDK SHALL expose a public `enum : byte` named `RequestedRegionReason`
with values `Unknown`, `Initial`, `OperationRetry`, `RegionFailover`,
`Hedging`, `CircuitBreakerProbe`, and `TransportRetry`. `Unknown` SHALL be
the underlying-zero value and SHALL serve as the default sentinel for
`default(RequestedRegionReason)` and `default(RequestedRegion).Reason`. The
SDK SHALL NOT emit `Unknown` from a real dispatch.
`CircuitBreakerProbe` and `TransportRetry` SHALL be reserved for future use
and MAY not be populated by every SDK version; consumers SHALL treat the
enum as non-exhaustive.

#### Scenario: Reserved values do not appear in v1 .NET

- **WHEN** an operation completes on the v1 .NET implementation
- **THEN** no entry in `GetRequestedRegions()` SHALL have reason
  `CircuitBreakerProbe` or `TransportRetry`

#### Scenario: `default(RequestedRegion)` is observably distinct from a real dispatch

- **WHEN** a caller inspects `default(RequestedRegion)`
- **THEN** `Reason` SHALL be `RequestedRegionReason.Unknown`
- **AND** the SDK SHALL NOT produce a `RequestedRegion` with reason
  `Unknown` from any real dispatch path

### Requirement: Diagnostics JSON shape is preserved

The new hedging-detection state SHALL be held off the trace tree and SHALL
NOT introduce a new `TraceDatum` or otherwise modify the JSON shape
produced by `CosmosDiagnostics.ToString()`.

#### Scenario: ToString output is unchanged for non-hedged paths

- **WHEN** an operation completes without hedging on a release that adds
  the hedging detection API
- **THEN** the JSON returned by `Diagnostics.ToString()` SHALL be the same
  as before the API was added (apart from incidental version stamps)

### Requirement: Backwards compatibility for `CosmosDiagnostics` subclasses

A pre-existing customer subclass of `CosmosDiagnostics` that does not
override any of the new virtual methods SHALL continue to compile and run
without throwing when the new methods are invoked.

#### Scenario: Legacy subclass returns safe defaults

- **WHEN** a customer subclass of `CosmosDiagnostics` that predates the
  new API is exercised
- **THEN** `HedgingStarted()` SHALL return `false`,
  `GetRequestedRegions()` SHALL return an empty list, and
  `GetRespondedRegions()` SHALL return an empty list
