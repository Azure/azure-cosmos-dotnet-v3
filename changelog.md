# Changelog

Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

### Fixed

## <a name="4.0.0-preview"/> [4.0.0-preview](https://www.nuget.org/packages/Azure.Cosmos/4.0.0-preview) - 2019-10-31

### Added

- [#853](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/853) ORDER BY Arrays and Object support.
- [#877](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/877) Query diagnostics now contains client side request diagnostics information
- [#923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/923) Bulk Support is now public
- [#922](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/922) Included information of bulk support usage in user agent

### Fixed
- [#901](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/901) Fix a bug causing query response to create a new stream for each content call
- [#918](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/918) Fixed serializer being used for Scripts, Permissions, and Conflict related iterators
- [#927](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/927) Fixed query returning partial results instead of error

## Release & Retirement dates
Microsoft provides notification at least **12 months** in advance of retiring an SDK in order to smooth the transition to a newer/supported version.

New features and functionality and optimizations are only added to the current SDK, as such it is recommended that you always upgrade to the latest SDK version as early as possible. 

Any requests to Azure Cosmos DB using a retired SDK are rejected by the service.

<br/>

| Version | Release Date | Retirement Date |
| --- | --- | --- |
| [4.0.0-preview](#4.0.0-preview) |October 31, 2019 |--- |
