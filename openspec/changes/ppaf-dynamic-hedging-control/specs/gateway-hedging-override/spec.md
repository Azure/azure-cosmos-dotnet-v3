## ADDED Requirements

### Requirement: Gateway account property for hedging control
The `AccountProperties` model SHALL include a nullable boolean property `DisableCrossRegionalHedging` deserialized from the Gateway JSON key `"disableCrossRegionalHedging"`. The property SHALL default to `null` when absent from the Gateway response.

#### Scenario: Gateway response includes the flag set to true
- **WHEN** the Gateway account-properties response contains `"disableCrossRegionalHedging": true`
- **THEN** the `AccountProperties.DisableCrossRegionalHedging` property SHALL be `true`

#### Scenario: Gateway response includes the flag set to false
- **WHEN** the Gateway account-properties response contains `"disableCrossRegionalHedging": false`
- **THEN** the `AccountProperties.DisableCrossRegionalHedging` property SHALL be `false`

#### Scenario: Gateway response does not include the flag
- **WHEN** the Gateway account-properties response does not contain the `"disableCrossRegionalHedging"` key
- **THEN** the `AccountProperties.DisableCrossRegionalHedging` property SHALL be `null`

---

### Requirement: Gateway flag disables all hedging when true
When the Gateway flag `disableCrossRegionalHedging` is `true`, the SDK SHALL disable all hedging for PPAF-enabled accounts regardless of any explicit or implicit hedging configuration.

#### Scenario: PPAF account with default hedging and flag set to true
- **WHEN** the account has PPAF enabled with SDK-default hedging active
- **AND** the Gateway flag `disableCrossRegionalHedging` is `true`
- **THEN** the SDK SHALL disable hedging
- **AND** requests SHALL NOT be hedged across regions

#### Scenario: PPAF account with explicit customer hedging and flag set to true
- **WHEN** the account has PPAF enabled
- **AND** the customer has configured an explicit `AvailabilityStrategy` via `CosmosClientOptions`
- **AND** the Gateway flag `disableCrossRegionalHedging` is `true`
- **THEN** the SDK SHALL disable hedging
- **AND** the explicit customer strategy SHALL NOT be executed

#### Scenario: PPAF account with request-level hedging override and flag set to true
- **WHEN** the account has PPAF enabled
- **AND** a request has a per-request `AvailabilityStrategy` override set in `RequestOptions`
- **AND** the Gateway flag `disableCrossRegionalHedging` is `true`
- **THEN** the SDK SHALL disable hedging for that request
- **AND** the request-level strategy SHALL NOT be executed

---

### Requirement: Existing behavior preserved when flag is false or absent
When the Gateway flag `disableCrossRegionalHedging` is `false` or absent from the account-properties response, the SDK SHALL preserve existing hedging behavior without any change.

#### Scenario: PPAF account with flag set to false and no explicit hedging
- **WHEN** the account has PPAF enabled
- **AND** the Gateway flag `disableCrossRegionalHedging` is `false`
- **AND** no explicit customer hedging configuration is set
- **THEN** the SDK SHALL enable the default PPAF hedging strategy with threshold `Min(1000ms, RequestTimeout/2)` and step `500ms`

#### Scenario: PPAF account with flag absent and explicit hedging configured
- **WHEN** the account has PPAF enabled
- **AND** the Gateway flag `disableCrossRegionalHedging` is absent from the response
- **AND** the customer has configured an explicit `AvailabilityStrategy`
- **THEN** the SDK SHALL honor the customer's explicit hedging configuration

#### Scenario: PPAF account with flag set to false and explicit hedging configured
- **WHEN** the account has PPAF enabled
- **AND** the Gateway flag `disableCrossRegionalHedging` is `false`
- **AND** the customer has configured an explicit `AvailabilityStrategy`
- **THEN** the SDK SHALL honor the customer's explicit hedging configuration

---

### Requirement: Dynamic toggling via account-properties refresh
The SDK SHALL evaluate the Gateway flag on each account-properties refresh cycle and dynamically enable or disable hedging as the flag value changes, without requiring client restart.

#### Scenario: Flag toggled from false to true during runtime
- **WHEN** the Gateway flag `disableCrossRegionalHedging` was `false` (or absent) at client initialization
- **AND** hedging was active (default or explicit)
- **AND** the Gateway flag is changed to `true`
- **AND** the SDK observes the updated account properties via the next refresh cycle
- **THEN** the SDK SHALL disable hedging

#### Scenario: Flag toggled from true to false during runtime
- **WHEN** the Gateway flag `disableCrossRegionalHedging` was `true` and hedging was disabled
- **AND** the Gateway flag is changed to `false`
- **AND** the SDK observes the updated account properties via the next refresh cycle
- **THEN** the SDK SHALL re-enable hedging using the appropriate strategy
- **AND** if the customer had configured an explicit strategy, that strategy SHALL be restored
- **AND** if no explicit strategy was configured, the SDK-default PPAF hedging strategy SHALL be applied

#### Scenario: Flag toggled from true to false with no prior explicit strategy
- **WHEN** the Gateway flag `disableCrossRegionalHedging` transitions from `true` to `false`
- **AND** the customer did not configure an explicit `AvailabilityStrategy`
- **AND** the account has PPAF enabled
- **THEN** the SDK SHALL re-enable the default PPAF hedging strategy

---

### Requirement: Non-PPAF accounts ignore the flag
The SDK SHALL NOT evaluate or act on the `disableCrossRegionalHedging` flag for accounts that do not have PPAF enabled.

#### Scenario: Non-PPAF account with flag set to true
- **WHEN** the account does NOT have PPAF enabled (`EnablePartitionLevelFailover` is `false` or absent)
- **AND** the Gateway flag `disableCrossRegionalHedging` is `true`
- **THEN** the SDK SHALL ignore the flag
- **AND** any explicit customer hedging configuration SHALL continue to function normally

#### Scenario: Non-PPAF account with explicit hedging and flag set to true
- **WHEN** the account does NOT have PPAF enabled
- **AND** the customer has configured an explicit `AvailabilityStrategy`
- **AND** the Gateway flag `disableCrossRegionalHedging` is `true`
- **THEN** the SDK SHALL NOT disable the customer's explicit hedging strategy

---

### Requirement: Feature is invisible to end users
The Gateway hedging override flag SHALL NOT be exposed through any public SDK API surface. There SHALL be no new public properties on `CosmosClientOptions`, `RequestOptions`, or any other user-facing type related to this flag.

#### Scenario: No public API surface for the flag
- **WHEN** a developer inspects the public API of `CosmosClientOptions`, `ItemRequestOptions`, `QueryRequestOptions`, or `ChangeFeedRequestOptions`
- **THEN** there SHALL be no property or method related to `disableCrossRegionalHedging`

#### Scenario: Diagnostics logging when hedging is disabled by flag
- **WHEN** hedging is disabled due to the Gateway flag being `true`
- **THEN** the SDK SHALL include a trace or diagnostic entry indicating that hedging was disabled by a Gateway account property
- **AND** this information SHALL be available in SDK diagnostics for supportability

---

### Requirement: Precedence rules for hedging evaluation
The SDK SHALL evaluate hedging configuration using the following strict precedence order:
1. Gateway `disableCrossRegionalHedging = true` → hedging OFF (highest priority)
2. If Gateway flag is `false` or absent → evaluate existing rules (request-level override → client-level strategy → PPAF default)

#### Scenario: Gateway flag true takes precedence over all other configuration
- **WHEN** the Gateway flag `disableCrossRegionalHedging` is `true`
- **AND** the customer has configured an explicit `AvailabilityStrategy` at the client level
- **AND** a request has a per-request `AvailabilityStrategy` override
- **THEN** the SDK SHALL disable hedging for that request
- **AND** neither the client-level nor request-level strategy SHALL be executed

#### Scenario: Gateway flag false defers to existing precedence
- **WHEN** the Gateway flag `disableCrossRegionalHedging` is `false`
- **AND** the customer has configured an explicit `AvailabilityStrategy` at the client level
- **AND** a request has a per-request `AvailabilityStrategy` override
- **THEN** the request-level strategy SHALL be used (existing precedence preserved)
