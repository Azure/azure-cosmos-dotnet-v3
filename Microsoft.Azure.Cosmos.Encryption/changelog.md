Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
- First preview of client-side encryption feature.