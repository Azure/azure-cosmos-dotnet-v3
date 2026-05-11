## Release notes

This project is in beta. The API and functionality may change when the project is updated.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="1.0.0-beta.1"/> [1.0.0-beta.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.FaultInjection/1.0.0-beta.1) - 2026-04-30

#### Added
- [#4867](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4867) FaultInjection: Adds method to add FaultInjection using CosmosClientBuilder
- [#4989](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4989) Metadata Requests: Adds Metadata request support for FaultInjection
- [#5264](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5264) ThinClient Compatibility: Adds compatibility with Thin Client Proxy
- [#5510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5510) Unauthorized Errors: Adds Unauthorized status codes
- [#5677](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5677) FaultInjection: Adds XML documentation, stylecop.json, and updates test packages
- [#5679](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5679) FaultInjection: Adds comprehensive unit test coverage

#### Fixed
- [#5676](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5676) FaultInjection: Fixes naming typos and XML documentation
- [#5675](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5675) FaultInjection: Fixes critical bugs for release 2
- [#5678](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5678) FaultInjection: Refactors code quality improvements

### <a name="1.0.0-beta.0"/> [1.0.0-beta.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.FaultInjection/1.0.0-beta.0) - 2024-11-15

#### Added

- Support for fault injection in the Cosmos SDK.
- Support for fault injection in Direct Mode.
- Support for fault injection in Gateway Mode.
