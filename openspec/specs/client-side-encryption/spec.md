# Client-Side Encryption

## Purpose

Client-side encryption enables encrypting sensitive item properties before they are sent to the Cosmos DB service, ensuring data is encrypted at rest and in transit with customer-managed keys. The encryption extensions are delivered as separate NuGet packages (`Microsoft.Azure.Cosmos.Encryption` and `Microsoft.Azure.Cosmos.Encryption.Custom`) that wrap the core SDK client.

## Public API Surface

### Client Encryption Key Management

```csharp
// Create a client encryption key
ClientEncryptionKeyResponse response = await database.CreateClientEncryptionKeyAsync(
    new ClientEncryptionKeyProperties(
        id: "myKey",
        encryptionAlgorithm: DataEncryptionAlgorithm.AeadAes256CbcHmacSha256,
        wrappedDataEncryptionKey: wrappedKeyBytes,
        encryptionKeyWrapMetadata: new EncryptionKeyWrapMetadata(
            type: "akv",
            name: "myKeyVaultKey",
            value: "https://myvault.vault.azure.net/keys/myKey/version")));

// Read key properties
ClientEncryptionKeyProperties keyProps = await database.GetClientEncryptionKey("myKey").ReadAsync();

// Rewrap (rotate) key
await database.GetClientEncryptionKey("myKey").ReplaceAsync(updatedProperties);
```

### Encryption Policy Configuration

```csharp
ContainerProperties containerProps = new ContainerProperties("myContainer", "/pk")
{
    ClientEncryptionPolicy = new ClientEncryptionPolicy(
        new List<ClientEncryptionIncludedPath>
        {
            new ClientEncryptionIncludedPath
            {
                Path = "/sensitiveProperty",
                ClientEncryptionKeyId = "myKey",
                EncryptionType = EncryptionType.Deterministic,
                EncryptionAlgorithm = DataEncryptionAlgorithm.AeadAes256CbcHmacSha256
            }
        })
};
```

### Extension Package Registration

```csharp
// Microsoft.Azure.Cosmos.Encryption (Azure Key Vault)
CosmosClient encryptionClient = cosmosClient.WithEncryption(
    keyEncryptionKeyResolver,
    KeyEncryptionKeyResolverName.AzureKeyVault);

// Microsoft.Azure.Cosmos.Encryption.Custom (custom provider)
CosmosClient customEncClient = cosmosClient.WithEncryption(
    new MyCustomEncryptionKeyWrapProvider());
```

## Requirements

### Requirement: Client Encryption Key Management

The SDK SHALL support creating and managing client encryption keys (CEKs) in the Cosmos DB account.

#### Create encryption key

**When** `database.CreateClientEncryptionKeyAsync(properties)` is called with valid key properties, the SDK SHALL create a client encryption key in the database with the key material wrapped using the specified key wrap provider.

#### Read encryption key

**When** `database.GetClientEncryptionKey(keyId).ReadAsync()` is called for an existing client encryption key, the SDK SHALL return the key properties (metadata only, not raw key material).

#### Replace (rewrap) encryption key

**When** `database.GetClientEncryptionKey(keyId).ReplaceAsync(updatedProperties)` is called, the SDK SHALL rewrap the data encryption key with the new key wrap metadata, enabling key rotation without re-encrypting data.

### Requirement: Encryption Policy

The SDK SHALL support defining encryption policies on containers to specify which properties are encrypted.

#### Define encryption policy

**Where** `ContainerProperties.ClientEncryptionPolicy` is configured with `ClientEncryptionIncludedPath` entries, **when** the container is created, the SDK SHALL encrypt the specified property paths on write and decrypt them on read.

#### Deterministic encryption

**Where** `EncryptionType = "Deterministic"` is set for a path, **when** the same value is encrypted multiple times, the SDK SHALL produce the same ciphertext, enabling equality queries on the encrypted property.

#### Randomized encryption

**Where** `EncryptionType = "Randomized"` is set for a path, **when** the same value is encrypted multiple times, the SDK SHALL produce different ciphertext each time. Equality queries on the encrypted property SHALL NOT be supported.

### Requirement: Key Wrap Providers

The SDK SHALL support pluggable key wrap providers for wrapping/unwrapping data encryption keys.

#### Azure Key Vault provider

**Where** `EncryptionKeyWrapMetadata` is configured with type `"akv"` and an Azure Key Vault key URL, **when** the encryption key is used, the SDK SHALL use Azure Key Vault to wrap/unwrap the data encryption key.

#### Custom key wrap provider

**Where** a custom `EncryptionKeyWrapProvider` implementation is registered, the SDK SHALL use the custom provider for all key wrap/unwrap operations.

### Requirement: Transparent Encryption/Decryption

The SDK SHALL transparently encrypt and decrypt properties without requiring application code changes for standard CRUD operations.

#### Transparent encryption on write

**While** a container has an encryption policy configured, **when** `Container.CreateItemAsync(item)` is called, the SDK SHALL automatically encrypt the specified properties before sending to the service.

#### Transparent decryption on read

**While** a container has encrypted properties, **when** `Container.ReadItemAsync<T>(id, pk)` is called, the SDK SHALL automatically decrypt the encrypted properties in the returned item.

### Requirement: Encryption with Cosmos Client Extensions

The SDK SHALL provide encryption through separate extension packages.

#### Microsoft.Azure.Cosmos.Encryption package

**Where** the `Microsoft.Azure.Cosmos.Encryption` NuGet package is referenced, **when** `cosmosClient.WithEncryption(keyEncryptionKeyResolver, KeyEncryptionKeyResolverName.AzureKeyVault)` is called, the SDK SHALL configure the client for client-side encryption with Azure Key Vault.

#### Microsoft.Azure.Cosmos.Encryption.Custom package

**Where** the `Microsoft.Azure.Cosmos.Encryption.Custom` NuGet package is referenced, **when** a custom `EncryptionKeyWrapProvider` is configured, the SDK SHALL support encryption with custom key management.

## Interactions

- **CRUD Operations**: Encryption is transparent for typed CRUD APIs. See `crud-operations` spec.
- **Query**: Deterministic encryption supports equality filters in queries. Randomized encryption does not. See `query-and-linq` spec.
- **Serialization**: Encryption wraps the configured serializer — items are serialized first, then encrypted properties are replaced with ciphertext. See `serialization` spec.
- **Change Feed**: Change feed results are automatically decrypted if the encryption client is used.

## References

- Source: `Microsoft.Azure.Cosmos/src/Resource/ClientEncryptionKey/ClientEncryptionKey.cs`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Settings/ClientEncryptionPolicy.cs`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Settings/ClientEncryptionIncludedPath.cs`
- Source: `Microsoft.Azure.Cosmos.Encryption/src/` — Azure Key Vault encryption extension
- Source: `Microsoft.Azure.Cosmos.Encryption.Custom/src/` — Custom encryption extension