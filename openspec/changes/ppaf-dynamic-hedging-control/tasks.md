## 1. Account Properties Model

- [ ] 1.1 Add `DisableCrossRegionalHedging` nullable bool property to `AccountProperties.cs` with `[JsonProperty("disableCrossRegionalHedging")]` attribute, following the same pattern as `EnablePartitionLevelFailover`
- [ ] 1.2 Add unit tests for `AccountProperties` deserialization: flag present as `true`, flag present as `false`, and flag absent from JSON

## 2. DocumentClient State Tracking

- [ ] 2.1 Add internal field `disableCrossRegionalHedgingFlag` (bool, default false) to `DocumentClient` to cache the current Gateway flag value
- [ ] 2.2 Add internal field to store the customer's original explicit `AvailabilityStrategy` reference (if any) so it can be restored when the flag is toggled back to false

## 3. Hedging Evaluation in Account-Refresh Callback

- [ ] 3.1 Modify `GlobalEndpointManager.RefreshDatabaseAccountInternalAsync()` (or the existing PPAF-change callback) to propagate the `DisableCrossRegionalHedging` value to `DocumentClient`
- [ ] 3.2 Modify `DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh()` to evaluate the Gateway flag: when `true`, store the current strategy and set `ConnectionPolicy.AvailabilityStrategy` to null/disabled; when `false` or absent, restore the appropriate strategy
- [ ] 3.3 Ensure the flag is also evaluated during initial client setup in `DocumentClient.InitializePartitionLevelFailoverWithDefaultHedging()` — if the flag is `true` at initialization time, do not enable default hedging

## 4. Request-Level Enforcement

- [ ] 4.1 Update `RequestInvokerHandler` hedging resolution logic to check the Gateway disable flag before evaluating request-level or client-level `AvailabilityStrategy` overrides, ensuring the flag takes absolute precedence when `true`

## 5. Diagnostics and Tracing

- [ ] 5.1 Add a trace/diagnostic log entry when hedging is disabled due to the Gateway flag, including the flag value and the action taken (e.g., "Hedging disabled by Gateway account property disableCrossRegionalHedging=true")
- [ ] 5.2 Add a trace/diagnostic log entry when hedging is re-enabled after the flag is toggled back to false

## 6. Unit Tests

- [ ] 6.1 Test: PPAF account with default hedging — flag `true` disables hedging, flag toggled to `false` re-enables default hedging
- [ ] 6.2 Test: PPAF account with explicit customer hedging — flag `true` disables hedging, flag toggled to `false` restores customer strategy
- [ ] 6.3 Test: PPAF account with request-level hedging override — flag `true` prevents request-level strategy execution
- [ ] 6.4 Test: Non-PPAF account — flag `true` does not affect explicit customer hedging
- [ ] 6.5 Test: Flag absent from account properties — existing behavior unchanged
- [ ] 6.6 Test: Dynamic toggle cycle — enable → disable → re-enable with correct strategy restoration

## 7. Integration Verification

- [ ] 7.1 Validate end-to-end with mocked Gateway responses containing the `disableCrossRegionalHedging` flag in integration/emulator tests
- [ ] 7.2 Verify no public API surface changes — confirm `DisableCrossRegionalHedging` is internal only and not exposed on `CosmosClientOptions`, `RequestOptions`, or related types
