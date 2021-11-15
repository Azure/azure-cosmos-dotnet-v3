Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="1.0.0-previewV18"/> [1.0.0-previewV18](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV18) - 2021-10-29

#### Fixes 
- [#2835](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2835) Adds fix to add encryption header for patch operation.
- [#2727](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2727) Fixes JSON property name of ClientEncryptionKeyProperties to match backend.

### <a name="1.0.0-previewV17"/> [1.0.0-previewV17](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV17) - 2021-10-07

#### Added 
- [#2633](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2633) Adds basic diagnostics information regarding encryption / decryption operation.
- [#2634](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2634) Adds support for PatchItem in Transactional Batch.
- [#2572](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2572) Adds ReadMany API support.

### <a name="1.0.0-previewV16"/> [1.0.0-previewV16](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV16) - 2021-08-30

#### Fixes 
- [#2581](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2581) Change Feed: Fixes change feed decryptableresponse to not throw exceptions on NotModified
- [#2507](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2507) Query: Fixes issue when passing null parameter value in AddParameterAsync

### <a name="1.0.0-previewV15"/> [1.0.0-previewV15](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV15) - 2021-05-18

#### Added 
- [#2475](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2475) Adds integration with latest CosmosDb Preview Package - 3.19.0-preview1 and check for Client Encryption Policy format version.
- [#2449](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2449) Adds validation that partition key paths are not encrypted.
- [#2452](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2452) Adds the implementation for new ChangeFeed APIs.
- [#2448](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2448) Adds client encryption support for patch.
- [#2453](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2453) Removes Plaintext encryption type support.

#### Fixes 
- [#2445](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2445) Fixes query support on encrypted parameters and fixes samples.
- [#2403](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2403) Fixes issue with clients using incorrect/stale Encryption Policy or Encryption Keys from the cache.

### <a name="1.0.0-previewV14"/> [1.0.0-previewV14](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV14) - 2021-04-28

#### Added 
- [#2433](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2433) Adds integration with latest CosmosDb Preview Package - 3.19.0-preview.
- [#2372](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2372) Adds decryption support for ChangeFeed Processor.

### <a name="1.0.0-previewV13"/> [1.0.0-previewV13](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV13) - 2021-03-26

#### Added 
- [#2340](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2340) Adds integration with latest CosmosDb Preview Package - 3.18.0-preview.

### <a name="1.0.0-previewV12"/> [1.0.0-previewV12](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV12) - 2021-03-15

#### Added 
- [#2112](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2112) Adds support for public model using Microsoft Data Encryption (MDE) based encryption via Container Level Encryption Policy for items.

#### Fixes 
- [#2306](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2306) Fixes the response for transactional batch in case of non-success status code.


### <a name="1.0.0-previewV11"/> [1.0.0-previewV11](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV11) - 2021-02-16

#### Fixes 
- [#2219](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2219) Adds integration with Cosmos SDK 3.17.0-preview. Moving to Microsoft.Data.Encryption.Cryptography Nuget package instead of dll dependency.

### <a name="1.0.0-previewV10"/> [1.0.0-previewV10](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-previewV10) - 2021-02-08

#### Fixes 
- [#2171](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2171) Adds support to Migrate Data Encryption Key via Rewrap from Legacy Encryption Algorithm to MDE based Encryption Algorithm.

### <a name="1.0.0-preview9"/> [1.0.0-preview9](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview9) - 2021-01-06

#### Fixes 
- [#2105](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2105) Fixes the nuget generation to include Cryptography DLL.

### <a name="1.0.0-preview8"/> [1.0.0-preview8](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview8) - 2021-01-05

#### Added 
- [#1861](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1861) Adds Support for Microsoft Data Encryption/MDE, its Encryption Algorithm and KeyWrap Provider Services.


### <a name="1.0.0-preview7"/> [1.0.0-preview7](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview7) - 2020-11-19

#### Added 
- [#1836](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1836) Adds overload with QueryDefinition for GetDataEncryptionKeyQueryIterator
- [#1832](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1832) Adds DecryptableItem, a type which allows for lazy decryption & deserialization
- [#1956](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1956) Adds integration with Cosmos SDK 3.15.0-preview


### <a name="1.0.0-preview6"/> [1.0.0-preview6](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview6) - 2020-09-02

#### Fixed
- [#1829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1829) Fixes serializer settings to make it more pass-through

### <a name="1.0.0-preview5"/> [1.0.0-preview5](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview5) - 2020-08-20

#### Fixed
- [#1776](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1776) Fixes etag mismatch exception for ReWrap DEK by refreshing cache
- [#1595](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1595) Fix to ensure input stream is not disposed before executing TransactionalBatch


### <a name="1.0.0-preview4"/> [1.0.0-preview4](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview4) - 2020-05-29

#### Added 
- [#1571](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1571) Add encryption support for transactional batch requests


### <a name="1.0.0-preview3"/> [1.0.0-preview3](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview3) - 2020-05-13

#### Fixed
- [#1510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1510) Fix to not encrypt if EncryptionOptions is null


### <a name="1.0.0-preview2"/> [1.0.0-preview2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview2) - 2020-05-12

#### Added
- [#1465](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1465) Add decryption functionality for change feed iterator and add capability to handle decryption failure

#### Fixed
- [#1499](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1499) Fix dependency to latest SDK preview in Encryption package


### <a name="1.0.0-preview"/> [1.0.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/1.0.0-preview) - 2020-04-12
- First preview of client-side encryption feature. See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.