# Client-Side Encryption

## Purpose

Client-side encryption enables encrypting sensitive item properties before they are sent to the Cosmos DB service, ensuring data is encrypted at rest and in transit with customer-managed keys.

## Requirements

### Requirement: Client Encryption Key Management
The SDK SHALL support creating and managing client encryption keys (CEKs) in the Cosmos DB account.

#### Scenario: Create encryption key
- GIVEN a database
- WHEN `database.CreateClientEncryptionKeyAsync(new ClientEncryptionKeyProperties(keyId, algorithm, wrappedDataEncryptionKey, encryptionKeyWrapMetadata))` is called
- THEN a client encryption key is created in the database
- AND the key material is wrapped (encrypted) using the specified key wrap provider

#### Scenario: Read encryption key
- GIVEN an existing client encryption key
- WHEN `database.GetClientEncryptionKey(keyId).ReadAsync()` is called
- THEN the key properties are returned (metadata only, not raw key material)

#### Scenario: Replace (rewrap) encryption key
- GIVEN an existing client encryption key
- WHEN `database.GetClientEncryptionKey(keyId).ReplaceAsync(updatedProperties)` is called
- THEN the key is rewrapped with the new key wrap metadata (key rotation)

### Requirement: Encryption Policy
The SDK SHALL support defining encryption policies on containers to specify which properties are encrypted.

#### Scenario: Define encryption policy
- GIVEN `ContainerProperties.ClientEncryptionPolicy` with a list of `ClientEncryptionIncludedPath` entries
- WHEN the container is created
- THEN the specified property paths are encrypted on write and decrypted on read

#### Scenario: Encryption path configuration
- GIVEN a `ClientEncryptionIncludedPath` with `Path`, `ClientEncryptionKeyId`, `EncryptionType` (Deterministic or Randomized), and `EncryptionAlgorithm`
- WHEN items are written to the container
- THEN the property at the specified path is encrypted using the referenced CEK

#### Scenario: Deterministic encryption
- GIVEN `EncryptionType = "Deterministic"` for a path
- WHEN the same value is encrypted multiple times
- THEN the same ciphertext is produced
- AND equality queries on the encrypted property are supported

#### Scenario: Randomized encryption
- GIVEN `EncryptionType = "Randomized"` for a path
- WHEN the same value is encrypted multiple times
- THEN different ciphertext is produced each time
- AND equality queries on the encrypted property are NOT supported

### Requirement: Key Wrap Providers
The SDK SHALL support pluggable key wrap providers for wrapping/unwrapping data encryption keys.

#### Scenario: Azure Key Vault provider
- GIVEN `EncryptionKeyWrapMetadata` with type `"akv"` and an Azure Key Vault key URL
- WHEN the encryption key is used
- THEN the SDK uses Azure Key Vault to wrap/unwrap the data encryption key

#### Scenario: Custom key wrap provider
- GIVEN a custom `EncryptionKeyWrapProvider` implementation
- WHEN registered with the encryption container
- THEN the custom provider is used for all key wrap/unwrap operations

### Requirement: Transparent Encryption/Decryption
The SDK SHALL transparently encrypt and decrypt properties without requiring application code changes for standard CRUD operations.

#### Scenario: Transparent encryption on write
- GIVEN a container with an encryption policy
- WHEN `Container.CreateItemAsync(item)` is called
- THEN encrypted properties are automatically encrypted before sending to the service

#### Scenario: Transparent decryption on read
- GIVEN a container with encrypted properties
- WHEN `Container.ReadItemAsync<T>(id, pk)` is called
- THEN encrypted properties are automatically decrypted in the returned item

### Requirement: Encryption with Cosmos Client Extensions
The SDK SHALL provide encryption through separate extension packages.

#### Scenario: Microsoft.Azure.Cosmos.Encryption package
- GIVEN the `Microsoft.Azure.Cosmos.Encryption` NuGet package is referenced
- WHEN `cosmosClient.WithEncryption(keyEncryptionKeyResolver, KeyEncryptionKeyResolverName.AzureKeyVault)` is called
- THEN the client is configured for client-side encryption with Azure Key Vault

#### Scenario: Microsoft.Azure.Cosmos.Encryption.Custom package
- GIVEN the `Microsoft.Azure.Cosmos.Encryption.Custom` NuGet package is referenced
- WHEN a custom `EncryptionKeyWrapProvider` is configured
- THEN the client supports encryption with custom key management

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Resource/ClientEncryptionKey/ClientEncryptionKey.cs` — key management API
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ClientEncryptionPolicy.cs` — encryption policy definition
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ClientEncryptionIncludedPath.cs` — encrypted path config
- `Microsoft.Azure.Cosmos.Encryption/src/` — Azure Key Vault encryption extension
- `Microsoft.Azure.Cosmos.Encryption.Custom/src/` — custom encryption extension
