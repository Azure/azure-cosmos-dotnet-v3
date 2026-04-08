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

### Requirement: ExcludeRegions takes precedence over PreferWritesInHub
When `PreferWritesInHub` is enabled but the hub region is listed in the per-request `RequestOptions.ExcludeRegions`, the SDK SHALL skip the hub and fall back to the preferred-region-ordered write endpoint resolution.

#### Scenario: Hub region excluded via ExcludeRegions
- **WHEN** `PreferWritesInHub` is `true` and the client sends a write operation with `RequestOptions.ExcludeRegions` containing the hub region
- **THEN** the SDK SHALL NOT route the write to the hub region and SHALL resolve the write endpoint using the preferred regions list, excluding the hub

#### Scenario: ExcludeRegions does not contain the hub region
- **WHEN** `PreferWritesInHub` is `true` and the client sends a write operation with `RequestOptions.ExcludeRegions` that does not contain the hub region
- **THEN** the SDK SHALL route the write to the hub region as normal

### Requirement: PreferWritesInHub is orthogonal to Partition-level Circuit Breaker (PPCB)
When both `PreferWritesInHub` and PPCB are enabled, they SHALL operate independently. `PreferWritesInHub` selects the hub as the target write region at the account level, while PPCB monitors partition health within that region at the partition-key-range level.

#### Scenario: PPCB triggers partition-level failover in hub region
- **WHEN** `PreferWritesInHub` is `true` and PPCB detects a failing partition in the hub region
- **THEN** PPCB SHALL trigger partition-level failover for that partition to the next available region, independently of `PreferWritesInHub`

### Requirement: PreferWritesInHub works with ApplicationRegion
When `PreferWritesInHub` is enabled and `ApplicationRegion` (singular) is used instead of `ApplicationPreferredRegions`, the SDK SHALL route writes to the hub region and use the proximity-generated preferred regions list as the fallback.

#### Scenario: ApplicationRegion with PreferWritesInHub
- **WHEN** `PreferWritesInHub` is `true` and `ApplicationRegion` is set (generating a proximity-ordered preferred regions list)
- **THEN** the SDK SHALL route write operations to the hub region, and SHALL use the proximity-generated preferred regions list as the fallback ordering if the hub is unavailable

### Requirement: Default behavior is unchanged
The `PreferWritesInHub` option SHALL default to `false`, preserving existing routing behavior for all existing users.

#### Scenario: Default value
- **WHEN** a `CosmosClientOptions` instance is created without setting `PreferWritesInHub`
- **THEN** `PreferWritesInHub` SHALL be `false` and write routing SHALL follow the existing preferred-region logic
