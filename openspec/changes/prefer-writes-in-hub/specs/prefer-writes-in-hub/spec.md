## ADDED Requirements

### Requirement: Write requests are routed to the hub region when PreferWritesInHub is enabled
When `PreferWritesInHub` is set to `true`, the SDK SHALL route all write operations to the hub region (the first region in `AccountProperties.WritableRegions`) as the primary endpoint, regardless of the configured preferred regions list.

#### Scenario: Write routed to hub region
- **WHEN** `PreferWritesInHub` is `true` and the client sends a write operation
- **THEN** the SDK SHALL resolve the write endpoint to the hub region (first entry in `AvailableWriteLocations`)

#### Scenario: Read routing is unaffected
- **WHEN** `PreferWritesInHub` is `true` and the client sends a read operation
- **THEN** the SDK SHALL continue to resolve the read endpoint using the preferred regions list, as if `PreferWritesInHub` were not set

### Requirement: Fallback to preferred regions when hub is unavailable
When `PreferWritesInHub` is enabled but the hub region endpoint is marked unavailable, the SDK SHALL fall back to the preferred-region-ordered write endpoint resolution.

#### Scenario: Hub region unavailable triggers fallback
- **WHEN** `PreferWritesInHub` is `true` and the hub region endpoint is marked unavailable
- **THEN** the SDK SHALL resolve the write endpoint using the preferred regions list ordering, identical to the behavior when `PreferWritesInHub` is `false`

#### Scenario: Hub region becomes available again
- **WHEN** `PreferWritesInHub` is `true` and the hub region endpoint was previously unavailable but has recovered
- **THEN** the SDK SHALL resume routing write operations to the hub region

### Requirement: Default behavior is unchanged
The `PreferWritesInHub` option SHALL default to `false`, preserving existing routing behavior for all existing users.

#### Scenario: Default value
- **WHEN** a `CosmosClientOptions` instance is created without setting `PreferWritesInHub`
- **THEN** `PreferWritesInHub` SHALL be `false` and write routing SHALL follow the existing preferred-region logic
