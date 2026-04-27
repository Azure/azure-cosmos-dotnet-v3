## ADDED Requirements

### Requirement: CosmosClientOptions exposes PreferWritesInHub property
`CosmosClientOptions` SHALL expose a public `bool PreferWritesInHub` property that defaults to `false`.

#### Scenario: Property exists and defaults to false
- **WHEN** a new `CosmosClientOptions` instance is created
- **THEN** `PreferWritesInHub` SHALL be `false`

#### Scenario: Property can be set to true
- **WHEN** a user sets `PreferWritesInHub = true` on `CosmosClientOptions`
- **THEN** the value SHALL be persisted and used during client initialization

### Requirement: CosmosClientBuilder exposes WithPreferWritesInHub method
`CosmosClientBuilder` SHALL expose a public `WithPreferWritesInHub()` method that sets `PreferWritesInHub` to `true` on the underlying `CosmosClientOptions`.

#### Scenario: Builder method sets the option
- **WHEN** `WithPreferWritesInHub()` is called on a `CosmosClientBuilder`
- **THEN** the built `CosmosClientOptions` SHALL have `PreferWritesInHub` set to `true`

### Requirement: PreferWritesInHub is incompatible with LimitToEndpoint
The SDK SHALL throw an `ArgumentException` if both `PreferWritesInHub` is `true` and `LimitToEndpoint` is `true`.

#### Scenario: Validation rejects conflicting options
- **WHEN** `PreferWritesInHub` is `true` and `LimitToEndpoint` is `true`
- **THEN** the SDK SHALL throw an `ArgumentException` during client initialization with a message indicating the conflict

### Requirement: PreferWritesInHub is visible in diagnostics
The SDK SHALL include the `PreferWritesInHub` setting in `ClientConfigurationTraceDatum` so the configuration is visible in diagnostics output.

#### Scenario: Diagnostics reflect PreferWritesInHub when enabled
- **WHEN** `PreferWritesInHub` is `true` and the client emits diagnostics
- **THEN** the diagnostics output SHALL include `PreferWritesInHub` with a value of `true`

#### Scenario: Diagnostics reflect PreferWritesInHub when disabled
- **WHEN** `PreferWritesInHub` is `false` (default) and the client emits diagnostics
- **THEN** the diagnostics output SHALL include `PreferWritesInHub` with a value of `false`
