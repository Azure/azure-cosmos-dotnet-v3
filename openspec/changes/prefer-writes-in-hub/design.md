## Context

The Azure Cosmos DB .NET SDK v3 routes requests to regions based on `ApplicationPreferredRegions` (or `ApplicationRegion`). For write operations:

- **Single-write accounts**: writes always go to the first entry in `AccountProperties.WritableRegions` (the hub). Preferred regions affect only reads and failover ordering.
- **Multi-write accounts** (`UseMultipleWriteLocations = true`): document-level writes go to the first preferred region that is available, while metadata writes go to the hub.

The existing `GetHubUri()` method in `LocationCache` already resolves the hub region endpoint (first entry in `AvailableWriteLocations`). The routing logic in `ResolveServiceEndpoint` decides between the hub-first path and the preferred-region path based on write-location capability. We need to introduce a third path that deliberately routes writes to the hub even when the client could use multiple write locations.

Key classes:
- `CosmosClientOptions` — public options bag, owns `ApplicationPreferredRegions`
- `CosmosClientBuilder` — fluent builder wrapping `CosmosClientOptions`
- `ConnectionPolicy` — internal configuration consumed by `DocumentClient`
- `GlobalEndpointManager` — delegates to `LocationCache`
- `LocationCache` — owns endpoint resolution logic

## Goals / Non-Goals

**Goals:**
- Provide a simple boolean opt-in (`PreferWritesInHub`) that routes write operations to the hub region.
- Use the preferred-regions list as a fallback if the hub endpoint is unavailable.
- Keep the feature fully backward-compatible (default `false` = no behavior change).
- Work correctly for both single-write and multi-write accounts.

**Non-Goals:**
- Per-request override of write routing (this is a client-level setting only).
- Changing read-request routing behavior — reads continue to follow preferred regions.
- Supporting a configurable "preferred write region" different from the hub — the hub is always the first write region reported by the account.

## Decisions

### 1. Boolean property on CosmosClientOptions

**Decision**: Add `public bool PreferWritesInHub { get; set; }` on `CosmosClientOptions`.

**Rationale**: A simple boolean is the lowest-friction API. Users who want this behavior enable one flag; the SDK does the rest. An alternative (a separate `ApplicationPreferredWriteRegions` list) was considered but adds complexity without clear benefit—users who want the hub already know they want the first write region, not a custom list.

### 2. Routing logic in LocationCache.ResolveServiceEndpoint

**Decision**: When `PreferWritesInHub` is enabled and the request is a write operation, resolve to the hub endpoint (`AvailableWriteLocations[0]`) at `locationIndex == 0`. If the hub is marked unavailable (exists in `locationUnavailablityInfoByEndpoint`), fall through to the normal preferred-region write endpoint resolution.

**Rationale**: This reuses the existing `GetHubUri()` helper and the existing unavailability tracking. No new data structures are needed. Failover is automatic because we simply delegate to the existing path.

### 3. Threading the flag through the stack

**Decision**: Pass the flag from `CosmosClientOptions` → `ConnectionPolicy.PreferWritesInHub` → `GlobalEndpointManager` constructor → `LocationCache` constructor (new `bool preferWritesInHub` parameter stored as a readonly field).

**Rationale**: This follows the existing pattern used for `useMultipleWriteLocations` and other boolean flags. Each layer just stores and forwards.

### 4. Validation rules

**Decision**: `PreferWritesInHub = true` is incompatible with `LimitToEndpoint = true` (since `LimitToEndpoint` pins all requests to a single endpoint, routing preferences are moot). Throw `ArgumentException` during validation if both are set.

**Rationale**: Prevents silent misconfiguration. Follows the same pattern as the `ApplicationPreferredRegions` + `LimitToEndpoint` validation.

## Risks / Trade-offs

- **[Risk] Hub endpoint is down** → Mitigation: Automatic fallback to preferred-region list. The unavailability tracking already exists in `LocationCache`; we just need to check it before committing to the hub endpoint.
- **[Risk] Subtle interaction with `UseMultipleWriteLocations`** → Mitigation: `PreferWritesInHub` takes precedence for routing, but multi-write semantics for retry and failover remain unchanged. Document this clearly.
- **[Trade-off] Client-level only** → This cannot be toggled per-request. Accepted because per-request routing adds significant complexity and the primary use case is a global policy decision.
