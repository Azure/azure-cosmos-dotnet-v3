# Client-Side Encryption

## Purpose

Client-side encryption enables encrypting sensitive item properties before they are sent to the Cosmos DB service, ensuring data is encrypted at rest and in transit with customer-managed keys.

## Requirements

### Requirement: Client Encryption Key Management
The SDK SHALL support creating and managing client encryption keys (CEKs) in the Cosmos DB account.

#### Create encryption key
**When** `database.CreateClientEncryptionKeyAsync(new ClientEncryptionKeyProperties(keyId, algorithm, wrappedDataEncryptionKey, encryptionKeyWrapMetadata))` is called, the SDK shall create a client encryption key in the database with the key material wrapped (encrypted) using the specified key wrap provider.

#### Read encryption key
**When** `database.GetClientEncryptionKey(keyId).ReadAsync()` is called for an existing client encryption key, the SDK shall return the key properties (metadata only, not raw key material).

#### Replace (rewrap) encryption key
**When** `database.GetClientEncryptionKey(keyId).ReplaceAsync(updatedProperties)` is called for an existing client encryption key, the SDK shall rewrap the key with the new key wrap metadata (key rotation).

### Requirement: Encryption Policy
The SDK SHALL support defining encryption policies on containers to specify which properties are encrypted.

#### Define encryption policy
**Where** `ContainerProperties.ClientEncryptionPolicy` is configured with a list of `ClientEncryptionIncludedPath` entries, **when** the container is created, the SDK shall encrypt the specified property paths on write and decrypt them on read.

#### Encryption path configuration
**Where** a `ClientEncryptionIncludedPath` is configured with `Path`, `ClientEncryptionKeyId`, `EncryptionType` (Deterministic or Randomized), and `EncryptionAlgorithm`, **when** items are written to the container, the SDK shall encrypt the property at the specified path using the referenced CEK.

#### Deterministic encryption
**Where** `EncryptionType = "Deterministic"` is set for a path, **when** the same value is encrypted multiple times, the SDK shall produce the same ciphertext and support equality queries on the encrypted property.

#### Randomized encryption
**Where** `EncryptionType = "Randomized"` is set for a path, **when** the same value is encrypted multiple times, the SDK shall produce different ciphertext each time and shall NOT support equality queries on the encrypted property.

### Requirement: Key Wrap Providers
The SDK SHALL support pluggable key wrap providers for wrapping/unwrapping data encryption keys.

#### Azure Key Vault provider
**Where** `EncryptionKeyWrapMetadata` is configured with type `"akv"` and an Azure Key Vault key URL, **when** the encryption key is used, the SDK shall use Azure Key Vault to wrap/unwrap the data encryption key.

#### Custom key wrap provider
**Where** a custom `EncryptionKeyWrapProvider` implementation is registered with the encryption container, the SDK shall use the custom provider for all key wrap/unwrap operations.

### Requirement: Transparent Encryption/Decryption
The SDK SHALL transparently encrypt and decrypt properties without requiring application code changes for standard CRUD operations.

#### Transparent encryption on write
**While** a container has an encryption policy configured, **when** `Container.CreateItemAsync(item)` is called, the SDK shall automatically encrypt the specified properties before sending to the service.

#### Transparent decryption on read
**While** a container has encrypted properties, **when** `Container.ReadItemAsync<T>(id, pk)` is called, the SDK shall automatically decrypt the encrypted properties in the returned item.

### Requirement: Encryption with Cosmos Client Extensions
The SDK SHALL provide encryption through separate extension packages.

#### Microsoft.Azure.Cosmos.Encryption package
**Where** the `Microsoft.Azure.Cosmos.Encryption` NuGet package is referenced, **when** `cosmosClient.WithEncryption(keyEncryptionKeyResolver, KeyEncryptionKeyResolverName.AzureKeyVault)` is called, the SDK shall configure the client for client-side encryption with Azure Key Vault.

#### Microsoft.Azure.Cosmos.Encryption.Custom package
**Where** the `Microsoft.Azure.Cosmos.Encryption.Custom` NuGet package is referenced, **when** a custom `EncryptionKeyWrapProvider` is configured, the SDK shall support encryption with custom key management.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Resource/ClientEncryptionKey/ClientEncryptionKey.cs` — key management API
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ClientEncryptionPolicy.cs` — encryption policy definition
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ClientEncryptionIncludedPath.cs` — encrypted path config
- `Microsoft.Azure.Cosmos.Encryption/src/` — Azure Key Vault encryption extension
- `Microsoft.Azure.Cosmos.Encryption.Custom/src/` — custom encryption extension
