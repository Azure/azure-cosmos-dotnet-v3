## ADDED Requirements

### Requirement: Resolved IKeyEncryptionKey cached per CMK URL
The system SHALL maintain a `ConcurrentDictionary<string, IKeyEncryptionKey>` keyed by `encryptionKeyId` (the CMK URL) that caches the `CryptographyClient` returned by `IKeyEncryptionKeyResolver.Resolve()`.

#### Scenario: First resolve for a CMK URL
- **WHEN** `UnwrapKey` (or prefetch) is called for a CMK URL not yet in the resolved-client cache
- **THEN** the system SHALL call `Resolve(keyId)` (or `ResolveAsync(keyId)` on the async path), store the returned `IKeyEncryptionKey` in the cache, and use it for the `UnwrapKey` call

#### Scenario: Subsequent resolve for the same CMK URL
- **WHEN** `UnwrapKey` is called for a CMK URL that is already in the resolved-client cache
- **THEN** the system SHALL skip the `Resolve()` call and use the cached `IKeyEncryptionKey` directly for the `UnwrapKey` call

#### Scenario: Key Vault calls halved on true cache miss
- **WHEN** a PDEK miss triggers `UnwrapKey` with a warm resolved-client cache
- **THEN** only one Key Vault HTTP call SHALL be made (`UnwrapKey` POST) instead of two (`Resolve` GET + `UnwrapKey` POST)

### Requirement: No secret material in resolved-client cache
The `IKeyEncryptionKey` object SHALL contain only the Key Vault URL, key name, key version, and HTTP pipeline configuration. It SHALL NOT contain any private key material.

#### Scenario: Cache contents are non-secret
- **WHEN** the resolved-client cache is inspected
- **THEN** each entry SHALL be a `CryptographyClient` (or equivalent) containing only the CMK URL and auth pipeline reference — no RSA private key bytes

### Requirement: Cache invalidation on CMK URL change
The resolved-client cache entry SHALL be invalidated when the CMK URL changes (key rotation where the CEK is rewrapped to a different CMK).

#### Scenario: CMK URL changes after rewrap
- **WHEN** `ClientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value` returns a different CMK URL than the one cached
- **THEN** the system SHALL call `Resolve()` with the new URL and update the cache entry
