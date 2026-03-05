## ADDED Requirements

### Requirement: Single environment variable gates all optimization layers
The system SHALL use a single environment variable `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` to enable or disable all caching and prefetch layers (resolved-client cache, async prefetch, proactive background refresh).

#### Scenario: Env var not set — all layers disabled
- **WHEN** `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` is not set in the environment
- **THEN** all behavior SHALL be identical to the current codebase: no resolved-client cache, no async prefetch, no proactive background refresh

#### Scenario: Env var set to true — all layers enabled
- **WHEN** `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` is set to `true` (case-insensitive)
- **THEN** all caching and prefetch layers SHALL be active

#### Scenario: Env var set to false or invalid — all layers disabled
- **WHEN** `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` is set to `false`, empty, or any value that does not parse as `true`
- **THEN** all behavior SHALL be identical to the env-var-not-set case

### Requirement: Env var read at EncryptionCosmosClient construction time
The environment variable SHALL be read once during `EncryptionCosmosClient` construction and the result cached for the client's lifetime. Subsequent changes to the environment variable SHALL NOT affect an already-constructed client.

#### Scenario: Env var read once at startup
- **WHEN** `EncryptionCosmosClient` is constructed
- **THEN** the env var SHALL be read via the SDK's `ConfigurationManager` pattern (or `Environment.GetEnvironmentVariable`) and the boolean result stored as a readonly field

#### Scenario: Env var change after construction has no effect
- **WHEN** the environment variable is changed after `EncryptionCosmosClient` is constructed
- **THEN** the existing client instance SHALL continue using the value read at construction time

### Requirement: No public API changes
There SHALL be no new public classes, methods, properties, or parameters exposed. All optimization layers SHALL be entirely internal, activated only by the environment variable.

#### Scenario: Public API surface unchanged
- **WHEN** the encryption package is built with optimizations enabled
- **THEN** the public API contract (as captured in the contracts file) SHALL be identical to the current version
