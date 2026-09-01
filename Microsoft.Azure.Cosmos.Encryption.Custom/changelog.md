Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="1.1.0-preview01"/> [1.1.0-preview01](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.1.0-preview01) - Unreleased

#### Added
- [#4766](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4766), [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds a `net8.0` target with opt-in System.Text.Json stream processing. Newtonsoft remains the default and `netstandard2.0` consumers are unaffected.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds asynchronous disposal for stream-backed `DecryptableItem` instances and feed pages so pooled plaintext buffers can be returned promptly.
- [#5423](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5423) Adds `CosmosDataEncryptionKeyProvider.Initialize(Container)`.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds optional distributed caching for wrapped DEK properties through `CosmosDataEncryptionKeyProvider.Create`, provider disposal, and cache-failure diagnostics. Raw DEK material remains process-local.

#### Fixes
- `EncryptableItem` create, replace, and upsert operations now preserve successful responses when content-on-write is disabled instead of dereferencing the absent response body.

#### Updates
- Stable builds depend on `Microsoft.Azure.Cosmos` `3.60.0` or later; preview builds retain `3.41.0-preview.0`.
- [#4753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4753), [#4819](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4819), [#5418](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5418) Updates the MDE cryptography dependency and removes unused direct package references.

### <a name="1.0.0-preview07"/> [1.0.0-preview07](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview07) - 2024-06-12

#### Fixes 
- [#4546](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4546) Updates package reference Microsoft.Azure.Cosmos to version 3.41.0-preview and 3.40.0 for preview and stable version support.

### <a name="1.0.0-preview06"/> [1.0.0-preview06](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview06) - 2023-06-28

#### Fixes 
- [#3956](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3956) Updates package reference Microsoft.Azure.Cosmos to version 3.35.1-preview.

### <a name="1.0.0-preview05"/> [1.0.0-preview05](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview05) - 2023-04-27

#### Fixes 
- [#3809](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3809) Adds api FetchDataEncryptionKeyWithoutRawKeyAsync and FetchDataEncryptionKey to get DEK without and with raw key respectively.

### <a name="1.0.0-preview04"/> [1.0.0-preview04](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview04) - 2022-08-16

#### Fixes 
- [#3386](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3386) Fixes custom serializer issue with DataEncryptionKeyContainer operations.

### <a name="1.0.0-preview03"/> [1.0.0-preview03](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview03) - 2022-04-15
- [#3145](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3145) Adds dependency on latest Microsoft.Azure.Cosmos preview (3.26.0-preview).

### <a name="1.0.0-preview02"/> [1.0.0-preview02](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview02) - 2021-10-29

#### Fixes 
- [#2834](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2834) Adds fix for deserialization issue for invalid date type.


### <a name="1.0.0-preview"/> [1.0.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview) - 2021-10-20
- First preview of custom client-side encryption feature. See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
