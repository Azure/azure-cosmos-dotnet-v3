## Context

PPAF (Partition-Level Failover) is an account-level feature that, when enabled, triggers automatic hedging of read requests with a default threshold of `Min(1000ms, RequestTimeout/2)` and a step of 500ms. The hedging is implemented via `CrossRegionHedgingAvailabilityStrategy`, which is either explicitly configured by the customer on `CosmosClientOptions.AvailabilityStrategy` or automatically created by the SDK in `DocumentClient.InitializePartitionLevelFailoverWithDefaultHedging()`.

**Current hedging decision flow:**

1. `RequestInvokerHandler` resolves the active `AvailabilityStrategy`: request-level override → client-level → null.
2. If a strategy is present and `Enabled()`, requests are dispatched through `CrossRegionHedgingAvailabilityStrategy.ExecuteAvailabilityStrategyAsync()`.
3. The strategy decides per-request whether to hedge based on resource type (Document only), operation type (reads always, writes only if multi-write enabled), and available regions.

**Account properties refresh:**

- `GlobalEndpointManager.RefreshDatabaseAccountInternalAsync()` periodically fetches `AccountProperties` from the Gateway.
- The `OnEnablePartitionLevelFailoverConfigChanged` event fires when PPAF status changes, triggering `DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh()` to dynamically enable or disable default hedging.

**Problem:** There is no mechanism for on-call engineers to temporarily disable hedging for a PPAF account without rolling back PPAF entirely. Rolling back PPAF is expensive and disrupts other benefits. A Gateway-controlled flag is needed as a targeted escape hatch.

## Goals / Non-Goals

**Goals:**

- Add a new boolean account property (`disableCrossRegionalHedging`) read from Gateway responses.
- When the flag is `true`, disable all hedging (both SDK-default PPAF hedging and explicit customer-configured hedging) for that account.
- When the flag is `false` or absent, preserve existing hedging behavior.
- Support dynamic toggling: as clients observe refreshed account properties, hedging state updates accordingly.
- Keep the feature entirely internal — no new public API surface.

**Non-Goals:**

- Changing customer-authored hedging strategies or their configuration shape.
- Modifying PPAF enablement or onboarding flows.
- Supporting non-PPAF accounts with this flag (for the immediate term).
- Exposing the flag to end users or making it configurable from the SDK.

## Decisions

### 1. Property location: `AccountProperties` with `JsonExtensionData` fallback

**Decision:** Add a strongly-typed `bool?` property `DisableCrossRegionalHedging` to `AccountProperties` with a `[JsonProperty]` attribute mapped to the Gateway JSON key `"disableCrossRegionalHedging"`.

**Rationale:** This is consistent with how `EnablePartitionLevelFailover` is already modeled (Line 249 of `AccountProperties.cs`). A strongly-typed property provides compile-time safety and discoverability. The `[JsonExtensionData] AdditionalProperties` dictionary exists as a fallback for unknown fields, but relying on it would lose type safety and require manual parsing.

**Alternative considered:** Reading from `AdditionalProperties` at evaluation time. Rejected because it introduces fragile string-keyed lookups and inconsistency with the existing pattern for account-level flags.

### 2. Evaluation point: `GlobalEndpointManager` account-refresh callback

**Decision:** Evaluate the `disableCrossRegionalHedging` flag in `DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh()` — the same method that already handles dynamic PPAF enable/disable based on account-property changes.

**Rationale:** This method is invoked whenever `GlobalEndpointManager` detects a change in PPAF-related account properties. Adding the hedging-disable check here ensures the flag is evaluated on every account-properties refresh, supporting dynamic toggling. It also consolidates all PPAF-related hedging logic in one place.

**Alternative considered:** Evaluating the flag per-request in `RequestInvokerHandler`. Rejected because it would require propagating account properties into the hot path and adds unnecessary per-request overhead. The account-refresh callback runs infrequently and already handles strategy assignment.

### 3. Enforcement mechanism: Strategy nullification / replacement

**Decision:** When the flag is `true`, set `ConnectionPolicy.AvailabilityStrategy` to `null` (or a `DisabledAvailabilityStrategy` sentinel) to disable hedging. When the flag is toggled back to `false`, re-evaluate and restore the appropriate strategy (explicit customer config or PPAF default).

**Rationale:** `RequestInvokerHandler` already treats a null strategy as "no hedging" (Line 97-98: `strategy != null && strategy.Enabled()`). Using the existing null-check path avoids introducing new conditional logic in the request hot path. The `DisabledAvailabilityStrategy` subclass already exists for explicit opt-out scenarios, though null assignment is simpler.

**Alternative considered:** Adding an `IsDisabledByGateway` flag to `CrossRegionHedgingAvailabilityStrategy` and checking it in `Enabled()`. Rejected because it would couple the strategy to Gateway concerns and requires changes to the strategy hierarchy.

### 4. Precedence: Gateway flag overrides all hedging configuration

**Decision:** When the Gateway flag is `true`, it overrides both SDK-default PPAF hedging AND any explicit customer-configured `AvailabilityStrategy`. The precedence order becomes:

1. Gateway `disableCrossRegionalHedging = true` → hedging OFF (highest priority)
2. Request-level `AvailabilityStrategy` override
3. Client-level `AvailabilityStrategy` (explicit customer config)
4. PPAF default hedging (if PPAF enabled, no explicit config)

**Rationale:** The flag is an operational safety valve. If on-call determines hedging is causing issues, it must be a hard kill switch regardless of what the customer configured. This prevents scenarios where explicit customer hedging bypasses the mitigation.

### 5. State tracking: Store flag and original strategy reference

**Decision:** Introduce internal fields in `DocumentClient`:
- `bool disableCrossRegionalHedgingFlag` — cached value of the Gateway flag.
- `AvailabilityStrategy customerConfiguredStrategy` — the customer's original explicit strategy (if any), stored before nullification so it can be restored when the flag is toggled back.

**Rationale:** The SDK must be able to restore the correct hedging behavior when the flag is turned off. Without storing the original strategy, the SDK cannot distinguish between "customer never set a strategy" and "strategy was removed by the flag."

## Risks / Trade-offs

- **[Risk] Flag latency** — The flag takes effect only after the next account-properties refresh (default interval is ~5 minutes). → **Mitigation:** On-call can force a faster refresh by restarting the client or waiting for the next refresh cycle. The existing `GlobalEndpointManager` refresh interval is sufficient for non-emergency toggles.

- **[Risk] Customer confusion if explicit hedging silently disabled** — If a customer configured explicit hedging and on-call disables it via the flag, the customer may observe unexpected behavior. → **Mitigation:** The flag is intended as a temporary operational tool. On-call should communicate with the customer. SDK diagnostics/traces should log when hedging is disabled by the Gateway flag.

- **[Risk] Strategy restoration correctness** — When restoring hedging after the flag is toggled off, the SDK must correctly reconstruct the PPAF default strategy or restore the customer's explicit strategy. → **Mitigation:** Store the original strategy reference before nullification. Unit-test the toggle cycle (enable → disable → re-enable).

- **[Trade-off] Non-PPAF accounts ignored** — The flag is only evaluated for PPAF accounts. A future extension could support non-PPAF accounts, but this adds complexity without current demand.
