## Why

PPAF-enabled Cosmos DB accounts automatically enable hedging with a 1-second threshold to fast-track read-region failover. However, production incidents have shown that implicit hedging of long-running queries can cause unexpected exceptions (e.g., ArgumentException in CallStore). Rolling back PPAF entirely to disable hedging is operationally expensive and disrupts other PPAF benefits, so a targeted, service-side escape hatch is needed to let on-call engineers dynamically disable hedging without customer intervention.

## What Changes

- Introduce a new Gateway account property (`disableCrossRegionalHedging`) that the SDK reads from account-property responses.
- When the flag is `true`, hedging is disabled for the PPAF account regardless of any explicit or implicit hedging configuration.
- When the flag is `false` or absent, existing hedging behavior is preserved (explicit customer config honored; PPAF defaults applied if no explicit config).
- The SDK evaluates the flag dynamically on every account-properties refresh, enabling on-call toggle without customer code changes.
- The flag is honored for any account where the customer has not opted out via `CosmosClientOptions.DisablePartitionLevelFailover` (surfaced internally as `DisablePartitionLevelFailoverClientLevelOverride`).

## Capabilities

### New Capabilities
- `gateway-hedging-override`: Reads a new Gateway account property flag and enforces it as the highest-precedence control over PPAF hedging behavior, supporting dynamic enable/disable at the SDK layer.

### Modified Capabilities
<!-- No existing spec-level capabilities are being modified. The underlying hedging and PPAF plumbing remain unchanged;
     only the precedence evaluation gains a new top-level check. -->

## Impact

- **SDK Client layer** (`DocumentClient` / `CosmosClient` internals): hedging-decision logic must incorporate a new precedence check against the Gateway flag before evaluating explicit or default hedging configuration.
- **Account properties model**: new property deserialized from the Gateway response (`AccountProperties` or equivalent DTO).
- **Gateway / service dependency**: the flag is surfaced by the Cosmos DB Gateway; the SDK consumes it read-only.
- **No public API surface changes**: the feature is invisible to end users; no new `CosmosClientOptions` or request-options properties are exposed.
- **Testing**: unit tests for precedence rules; integration tests validating dynamic toggle via mocked account-property responses.

## Cross-SDK Parity

- **.NET (this PR).** Implements the gateway-driven `disableCrossRegionalHedging` flag in the `Microsoft.Azure.Cosmos` v3 SDK.
- **Java.** As of the date of this PR, `azure-sdk-for-java` does not surface this flag. Java already has the GEM `perPartitionAutomaticFailoverConfigModifier` callback plumbing (`GlobalEndpointManager.java`), so mirroring this knob is additive rather than new infrastructure. A tracking issue should be filed against `Azure/azure-sdk-for-java` so an operator flipping the gateway flag gets consistent behavior across .NET and Java clients on the same account.
- **Python.** Out of scope. Python's `azure-cosmos` SDK has no SDK-default PPAF hedging today (default `availability_strategy=False`), so the per-request override path that motivates this knob does not exist.

