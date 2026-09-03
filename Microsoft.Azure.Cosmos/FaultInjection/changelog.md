## Release notes

This project is in beta. The API and functionality may change when the project is updated.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="unreleased-faultinjection"/> Unreleased

#### Features Added
- [#6004](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6004) RetryWith: Adds `RetryWith` (HTTP 449) server error injection support for Gateway mode
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) FaultInjectionRule: Adds `GetInjectionRate` and `SetInjectionRate` so the injection rate of a server error rule can be changed at runtime, without recreating the rule or the client

#### Breaking Changes
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) FaultInjectionServerErrorResult: The public constructor now throws `ArgumentOutOfRangeException` when the injection rate is outside `(0, 1]`; previously out-of-range values were accepted and silently misbehaved. Use `FaultInjectionRule.Disable()` instead of a rate of 0

#### Bugs Fixed
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) InjectionRate: Fixes `WithInjectionRate`, `WithThreshold` and the `FaultInjectionServerErrorResult` / `FaultInjectionConnectionErrorResult` constructors accepting `double.NaN`, which silently caused a server error rule to be applied to every matching request (and a connection error rule to never fire) instead of being rejected as out of range
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) InjectionRate: Fixes the `ArgumentOutOfRangeException` thrown by `WithInjectionRate` reporting the validation message as its `ParamName`
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) InjectionRate: Fixes `ConnectionDelay` server error rules ignoring their configured injection rate and always injecting on every matching request
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) FaultInjectionRule: Fixes a race where `SetInjectionRate`, `Enable` or `Disable` called while a rule was being registered with a client could be dropped, leaving the rule at a rate or state different from the one reported
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) FaultInjectionRule: Fixes the rule hit limit being exceeded under concurrent requests, and `GetHitCountDetails` never counting past 1 per operation type
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) FaultInjectionRule: Fixes `ToString` throwing a `NullReferenceException` for rules built without an explicit endpoint

#### Other Changes
- [#6103](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6103) FaultInjectionRule: Formats `ToString` output with the invariant culture and reports the live rate as `effectiveInjectionRate`

### <a name="1.0.0-beta.1"/> [1.0.0-beta.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.FaultInjection/1.0.0-beta.1) - 2026-04-30

#### Features Added
- [#4867](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4867) FaultInjection: Adds method to add FaultInjection using CosmosClientBuilder
- [#4989](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4989) Metadata Requests: Adds Metadata request support for FaultInjection
- [#5264](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5264) ThinClient Compatibility: Adds compatibility with Thin Client Proxy
- [#5510](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5510) Unauthorized Errors: Adds Unauthorized status codes
- [#5677](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5677) FaultInjection: Adds XML documentation, stylecop.json, and updates test packages
- [#5679](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5679) FaultInjection: Adds comprehensive unit test coverage

#### Bugs Fixed
- [#5676](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5676) FaultInjection: Fixes naming typos and XML documentation
- [#5675](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5675) FaultInjection: Fixes critical bugs for release 2
- [#5678](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5678) FaultInjection: Refactors code quality improvements

### <a name="1.0.0-beta.0"/> [1.0.0-beta.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.FaultInjection/1.0.0-beta.0) - 2024-11-15

#### Features Added

- Support for fault injection in the Cosmos SDK.
- Support for fault injection in Direct Mode.
- Support for fault injection in Gateway Mode.
