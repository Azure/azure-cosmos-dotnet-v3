# Changelog

Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

- [#1258](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1258) Renamed CosmosClientOptions.SerializerOptions to CosmosClientOptions.DefaultSerializerOptions

### Fixed

## <a name="4.0.0-preview3"/> [4.0.0-preview3](https://www.nuget.org/packages/Azure.Cosmos/4.0.0-preview3) - 2020-01-09

### Fixed

- [#1144](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1144) Newtonsoft.Json dependency needed for internal dependencies


## <a name="4.0.0-preview2"/> [4.0.0-preview2](https://www.nuget.org/packages/Azure.Cosmos/4.0.0-preview2) - 2020-01-07

### Added

- [#853](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/853) ORDER BY Arrays and Object support.
- [#877](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/877) Query diagnostics now contains client side request diagnostics information
- [#934](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/934) Preserved the ordering of projections in a GROUP BY query.
- [#952](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/952) ORDER BY Undefined and Mixed Type ORDER BY support
- [#1072](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1072) and [#1110](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1110)  Public contract renames, using Azure.ETag, new Azure.Cosmos.Serialization namespace
- [#1119](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1119) System.Text.Json as default serialization mechanism.

### Fixed

- [#1118](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/1118) Key already present exception when using custom retry policies on Core pipeline

## <a name="4.0.0-preview"/> [4.0.0-preview](https://www.nuget.org/packages/Azure.Cosmos/4.0.0-preview) - 2019-10-31

Initial preview release of the new 4.0.0 SDK that aligns with [Azure.Core for .NET](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/README.md).

### Key differences with [V3](https://github.com/Azure/azure-cosmos-dotnet-v3/)

* [System.Text.Json](https://docs.microsoft.com/dotnet/standard/serialization/system-text-json-overview) used as default serialization mechanism instead of Newtonsoft.Json.
* Queries now support `await foreach` through `IAsyncEnumerable` available in C# 8 for streams and types. This replaces the `FeedIterator` class in the v3 SDK.
    * For Typed queries async enumeration is supported at the [item level](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/samples/Response.md#iterating-over-asyncpageable-using-await-foreach), or at the Page level with [AsPages](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/samples/Response.md#iterating-over-asyncpageable-pages).
    * For stream queries, async enumeration can be done directly over the [Response](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/samples/Response.md).
* Point stream operations return [Response](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/samples/Response.md) instead of `ResponseMessage`.
* Point type operations still return `ItemResponse<T>` but the only promoted property is the `Session` to be used on session consistency, all other headers need to be read from the response (see below).
* Accessing headers can be done using the [Headers](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/samples/Response.md#accessing-http-response-propreties) property in the responses.
* Diagnostics are not part of the operation return types but emitted to the `Azure.Cosmos` scope and can be [listened to](https://azuresdkdocs.blob.core.windows.net/$web/dotnet/Azure.Core/1.0.0/api/Azure.Core.Diagnostics/Azure.Core.Diagnostics.AzureEventSourceListener.html) by enabling logging as part of the [CosmosClientOptions.Diagnostics.IsLoggingEnabled](https://azuresdkdocs.blob.core.windows.net/$web/dotnet/Azure.Core/1.0.0/api/Azure.Core/Azure.Core.DiagnosticsOptions.html).
* [CosmosClientOptions](./Microsoft.Azure.Cosmos/azuredata/CosmosClientOptions.cs) supports `Azure.Core.Pipeline` and allows for [policy customization](https://azuresdkdocs.blob.core.windows.net/$web/dotnet/Azure.Core/1.0.0/api/Azure.Core.Pipeline/Azure.Core.Pipeline.HttpPipelinePolicy.html).
* `CosmosException` is still the type used for Exceptions but it inherits from [RequestFailedException](https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Azure.Core/samples/Response.md#handling-exceptions).

## Release & Retirement dates
Microsoft provides notification at least **12 months** in advance of retiring an SDK in order to smooth the transition to a newer/supported version.

New features and functionality and optimizations are only added to the current SDK, as such it is recommended that you always upgrade to the latest SDK version as early as possible. 

Any requests to Azure Cosmos DB using a retired SDK are rejected by the service.

| Version | Release Date | Retirement Date |
| --- | --- | --- |
| [4.0.0-preview](#4.0.0-preview) |October 31, 2019 |--- |
