# Changelog

Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

### Fixed

## <a name="4.0.0-preview"/> [4.0.0-preview](https://www.nuget.org/packages/Azure.Cosmos/4.0.0-preview) - 2019-10-31

Initial preview release of the new 4.0.0 SDK that aligns with [Azure SDKs](https://azure.github.io/azure-sdk/).

### Key differences with [V3](https://github.com/Azure/azure-cosmos-dotnet-v3/)

* Queries now support `await foreach` through `IAsyncEnumerable` available in C# 8 for streams and types
* Paging on queries is supported through the `AsPages` method on the `AsyncPageable` query type. [Pages](https://docs.microsoft.com/dotnet/api/azure.page-1?view=azure-dotnet-preview) can be iterated with `await foreach`.
* `FeedIterator` is no longer available nor public.
* Point stream operations return [Response](https://docs.microsoft.com/dotnet/api/azure.response?view=azure-dotnet-preview) instead of `ResponseMessage`.
* Diagnostics are not part of the operation return types but emitted to the `Azure.Cosmos` scope and can be [listened to](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlistener?view=netframework-4.8).
* [CosmosClientOptions](./Microsoft.Azure.Cosmos/azuredata/CosmosClientOptions.cs) supports `Azure.Core.Pipeline` and allows for [policy customization](https://docs.microsoft.com/dotnet/api/azure.core.clientoptions.addpolicy?view=azure-dotnet-preview#Azure_Core_ClientOptions_AddPolicy_Azure_Core_Pipeline_HttpPipelinePolicy_Azure_Core_HttpPipelinePosition_).

## Release & Retirement dates
Microsoft provides notification at least **12 months** in advance of retiring an SDK in order to smooth the transition to a newer/supported version.

New features and functionality and optimizations are only added to the current SDK, as such it is recommended that you always upgrade to the latest SDK version as early as possible. 

Any requests to Azure Cosmos DB using a retired SDK are rejected by the service.

| Version | Release Date | Retirement Date |
| --- | --- | --- |
| [4.0.0-preview](#4.0.0-preview) |October 31, 2019 |--- |
