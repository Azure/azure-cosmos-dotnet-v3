## Why

When using Azure Cosmos DB with geo-replicated accounts, users configure `ApplicationPreferredRegions` to route requests to specific regions for low latency. However, some scenarios require write operations to always go to the **hub region** (the primary/first write region in `AccountProperties.WritableRegions`) for consistency or cost reasons, while reads still follow the preferred region list. Today there is no way to express this intent—writes follow the same preferred region routing as reads. Users want a simple opt-in API that sends writes to the hub region by default, falling back to the preferred regions list only if the hub region is unavailable.

## What Changes

- Add a new `bool PreferWritesInHub` property on `CosmosClientOptions` (default `false`).
- Add a corresponding `WithPreferWritesInHub()` fluent method on `CosmosClientBuilder`.
- Modify write-endpoint resolution in `LocationCache.ResolveServiceEndpoint` so that when `PreferWritesInHub` is enabled, write operations resolve to the hub region endpoint (first entry in `AvailableWriteLocations`) before consulting the preferred-region-ordered write endpoint list.
- If the hub endpoint is marked unavailable, fall back to the existing preferred-region resolution logic, ensuring no loss in availability.
- Thread the new option through `ConnectionPolicy` and `GlobalEndpointManager` into `LocationCache`.
- Add validation in `CosmosClientOptions.ValidateDirectTCPSettings` to ensure `PreferWritesInHub` is not set when `LimitToEndpoint` is `true` (conflicting semantics).

## Capabilities

### New Capabilities
- `prefer-writes-in-hub`: Adds a client-level option to route write requests to the hub (primary write) region, ignoring the preferred regions list for writes while using it as a failover fallback.

### Modified Capabilities
- `client-and-configuration`: The `CosmosClientOptions` and `CosmosClientBuilder` classes gain a new configuration property and builder method.

## Impact

- **Public API surface**: New public property on `CosmosClientOptions`, new public method on `CosmosClientBuilder`.
- **Routing layer**: `LocationCache`, `GlobalEndpointManager`, and `ConnectionPolicy` gain awareness of the new flag.
- **Existing behavior**: No change when `PreferWritesInHub` is `false` (the default). Fully backward-compatible.
- **Tests**: New unit tests for the option validation, endpoint resolution with the flag enabled/disabled, and failover scenarios.
