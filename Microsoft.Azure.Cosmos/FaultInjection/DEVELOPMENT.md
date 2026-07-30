# Fault Injection — Development & Maintenance Guide

This guide is for **contributors and maintainers** of the
`Microsoft.Azure.Cosmos.FaultInjection` library. If you are a *consumer* of the
package (writing tests against your own app), start with [README.md](./README.md)
instead.

- [Project layout](#project-layout)
- [How it plugs into the SDK](#how-it-plugs-into-the-sdk)
- [Building](#building)
- [Running the tests](#running-the-tests)
- [Working on / maintaining the project](#working-on--maintaining-the-project)
- [Adding a new fault type](#adding-a-new-fault-type)
- [Changelog rules](#changelog-rules)
- [Release process](#release-process)
- [Common issues & troubleshooting](#common-issues--troubleshooting)

---

## Project layout

Everything lives under `Microsoft.Azure.Cosmos/FaultInjection/`:

```
FaultInjection/
├── README.md                     # Consumer-facing usage guide
├── DEVELOPMENT.md                # This file
├── changelog.md                  # Package release notes
├── LICENSE
├── src/
│   ├── FaultInjection.csproj      # The shipped package project (targets net6)
│   ├── FaultInjection.sln
│   ├── stylecop.json              # StyleCop config (enforced; warnings are errors)
│   ├── FaultInjector.cs           # Public entry point passed to the CosmosClient
│   ├── FaultInjection*Builder.cs  # Public fluent builders (rule/condition/result/endpoint)
│   ├── FaultInjection*Type.cs     # Public enums (server error, connection error, operation, connection)
│   └── implementation/            # Internal wiring (interceptor rules, stores, processors)
│       ├── FaultInjectionRuleProcessor.cs
│       ├── FaultInjectionRuleStore.cs
│       ├── FaultInjectionServerErrorResultInternal.cs
│       ├── FaultInjectionConnectionErrorRule.cs
│       ├── FaultInjectionApplicationContext.cs
│       └── ...
└── tests/
    ├── FaultInjectionTests.csproj
    ├── FaultInjectionUnitTests.cs            # Builder tests (no account)
    ├── FaultInjectionBuilderValidationTests.cs # Builder validation (no account)
    ├── FaultInjectionDirectModeTests.cs      # Integration (live account)
    ├── FaultInjectionGatewayModeTests.cs     # Integration (live account)
    ├── FaultInjectionMetadataTests.cs        # Integration (live account)
    ├── FaultInjectionProxyTests.cs           # Integration (ThinClient proxy)
    └── Utils/TestCommon.cs                   # Shared setup + env-var helpers
```

The **public surface** is the set of `FaultInjection*` types directly under `src/`.
The `src/implementation/` folder contains the internal classes that translate a
public `FaultInjectionRule` into the `IChaosInterceptor` the SDK understands.

## How it plugs into the SDK

The main SDK (`Microsoft.Azure.Cosmos`) exposes internal hook points that this
library implements:

- `Microsoft.Azure.Cosmos/src/FaultInjection/IFaultInjector.cs` — the public
  interface `FaultInjector` implements.
- `Microsoft.Azure.Cosmos/src/FaultInjection/IChaosInterceptorFactory.cs` — the
  internal factory the SDK calls to build the interceptor.
- `CosmosClientOptions.FaultInjector` (public) → sets the internal
  `CosmosClientOptions.ChaosInterceptorFactory`.
- `CosmosClientBuilder.WithFaultInjection(IFaultInjector)` (public) → same wiring
  through the fluent builder.

At runtime, `FaultInjector.GetChaosInterceptorFactory()` returns a
`ChaosInterceptorFactory` (backed by `Microsoft.Azure.Documents.FaultInjection`)
that the SDK invokes on every request to decide whether/how to inject a fault.

By default `FaultInjection.csproj` builds with `SdkProjectRef=True`, which uses a
**`ProjectReference`** to `..\..\src\Microsoft.Azure.Cosmos.csproj` (so you build
against your local SDK changes). When packaging, that switches to a
**`PackageReference`** on the published `Microsoft.Azure.Cosmos` version
(`$(ClientOfficialVersion)` from the root `Directory.Build.props`).

## Building

From the repo root:

```bash
# Build just the fault injection library
dotnet build Microsoft.Azure.Cosmos/FaultInjection/src/FaultInjection.csproj -c Release

# Or open the focused solution
#   Microsoft.Azure.Cosmos/FaultInjection/src/FaultInjection.sln
```

Notes:

- The project targets **`net6`**, has `Nullable` enabled, and sets
  **`TreatWarningsAsErrors=true`** with StyleCop analyzers — a stray warning will
  fail the build. Keep XML docs on public members.
- Release builds are **delay-signed** with `35MSSharedLib1024.snk`; local dev
  builds do not require the private key.

## Running the tests

The test project (`FaultInjection/tests/FaultInjectionTests.csproj`) contains
**both** pure unit tests and **live-account integration tests**. Despite the
`IsEmulatorTest`/`EmulatorFlavor` MSBuild flags on the project, the integration
tests run against a **real, multi-region Cosmos DB account** (multi-region is
required to exercise region-scoped and failover faults), not the emulator.

| Test class | Needs a live account? | Environment variable |
| ---------- | --------------------- | -------------------- |
| `FaultInjectionUnitTests` | No | — |
| `FaultInjectionBuilderValidationTests` | No | — |
| `FaultInjectionDirectModeTests` | Yes (multi-region) | `COSMOSDB_MULTI_REGION` |
| `FaultInjectionGatewayModeTests` | Yes (multi-region) | `COSMOSDB_MULTI_REGION` |
| `FaultInjectionMetadataTests` | Yes (multi-region) | `COSMOSDB_MULTI_REGION` |
| `FaultInjectionProxyTests` | Yes (ThinClient proxy) | `COSMOSDB_THIN_CLIENT` |

`COSMOSDB_MULTI_REGION` and `COSMOSDB_THIN_CLIENT` are **connection strings**.
The integration tests create (if missing) a `faultInjectionDatabase` with
`faultInjectionContainer` / `faultInjectionContainerHTP` on the target account
and reuse them across runs to reduce replication-lag flakiness.

### Run only the tests that need no account

```bash
dotnet test Microsoft.Azure.Cosmos/FaultInjection/tests/FaultInjectionTests.csproj \
  --filter "FullyQualifiedName~FaultInjectionUnitTests|FullyQualifiedName~FaultInjectionBuilderValidationTests"
```

### Run the full suite (integration)

```powershell
$env:COSMOSDB_MULTI_REGION = "<multi-region account connection string>"
$env:COSMOSDB_THIN_CLIENT  = "<thin client / proxy connection string>"   # only for proxy tests

dotnet test Microsoft.Azure.Cosmos/FaultInjection/tests/FaultInjectionTests.csproj -c Release
```

If a required variable is missing, the affected tests fail fast with
`Set environment variable COSMOSDB_MULTI_REGION to run the tests` (or the
`COSMOSDB_THIN_CLIENT` equivalent) rather than silently passing.

### How CI runs them

`azure-pipelines-faultinjection.yml` runs the integration tests on the internal
`OneES` pool with the `COSMOSDB_MULTI_REGION` secret wired in, using
`dotnet test` over `Microsoft.Azure.Cosmos/FaultInjection/tests/*.csproj`.

## Working on / maintaining the project

- **Public API changes** must keep the fluent-builder style consistent
  (`WithXxx(...)` returns the builder; a terminal `Build()` produces the immutable
  result/rule). Validate arguments in the builder (`ArgumentOutOfRangeException`,
  `InvalidOperationException`) close to where they're set.
- **Mode restrictions** are enforced in `FaultInjectionRuleBuilder.Build()`
  (`ValidateDirectConnection` / `ValidateGatewayConnection`). Update these when a
  new fault type is only valid for one connection mode or for metadata operations.
- Keep the **README tables** (server error types, connection error types,
  operation types, properties) in sync with the enums and builders — they are the
  primary consumer reference and drift easily.
- Every public member needs an XML doc comment (StyleCop + warnings-as-errors).

## Adding a new fault type

Rough checklist for adding a new server error type (connection error types are
analogous):

1. Add the value + XML doc (status code) to
   `src/FaultInjectionServerErrorType.cs`.
2. Map it to the actual injected error in the internal implementation
   (`src/implementation/FaultInjectionServerErrorResultInternal.cs` and the
   rule processor).
3. If the fault only applies to Direct **or** Gateway mode, or to (or excludes)
   metadata operations, update the validation in
   `src/FaultInjectionRuleBuilder.cs`.
4. Add unit coverage in `FaultInjectionBuilderValidationTests.cs` and, if it
   needs a live account, integration coverage in the relevant `*ModeTests.cs`.
5. Add the row to the server-error table in [README.md](./README.md).
6. Add a changelog entry (see below).

## Changelog rules

The package has its **own** changelog:
[`Microsoft.Azure.Cosmos/FaultInjection/changelog.md`](./changelog.md). Any PR
that touches `Microsoft.Azure.Cosmos/FaultInjection/src/**` must add a bullet
under the `### Unreleased` section, in one of `Features Added` /
`Breaking Changes` / `Bugs Fixed` / `Other Changes`, written in customer-facing
language and linking the PR. This mirrors the main-SDK rules in the repo-root
[CONTRIBUTING.md](../../CONTRIBUTING.md). Docs/test/CI-only changes do not require
an entry.

PR titles still follow the repo regex, e.g.
`FaultInjection: Adds ServiceUnavailable injection for Gateway mode`.

## Release process

The package version is centrally managed in the **root**
[`Directory.Build.props`](../../Directory.Build.props):

```xml
<FaultInjectionVersion>1.0.0</FaultInjectionVersion>
<FaultInjectionSuffixVersion>beta.1</FaultInjectionSuffixVersion>
```

These combine into the NuGet version (e.g. `1.0.0-beta.1`). High-level steps:

1. **Bump the version** — update `FaultInjectionVersion` and/or
   `FaultInjectionSuffixVersion` in the root `Directory.Build.props`.
2. **Finalize the changelog** — move the `### Unreleased` bullets under a new
   version heading with the release date and a NuGet link, matching the existing
   entries in `changelog.md`.
3. **Run the release pipeline** `azure-pipelines-faultinjection.yml` (it is
   manually triggered — `trigger: none` / `pr: none`). It:
   - runs static analysis (`templates/static-tools.yml`),
   - runs the integration tests on `OneES` with `COSMOSDB_MULTI_REGION`,
   - packs the NuGet + symbols package and stages artifacts via
     `templates/fault-injection-nuget-pack.yml`, publishing to the
     `azuresdkpartnerdrops` blob under
     `cosmosdb/faultInjection/<BlobVersion>` (set the `BlobVersion` variable).
4. Push the resulting `.nupkg` to NuGet.org through the standard partner-drop
   release flow.

> **Canonical runbook:** the detailed, step-by-step release procedure lives in
> the `cosmos-release-dotnet-faultinjection` skill in the
> [cosmos-sdk-copilot-toolkit](https://github.com/Azure/cosmos-sdk-copilot-toolkit).
> In the Copilot CLI you can just describe the task naturally (e.g. "release
> fault injection beta") and the routing agent loads that skill. Prefer the skill
> over hand-running the steps above so the version bump, changelog, pipeline, and
> blob drop stay consistent.

## Common issues & troubleshooting

| Symptom | Cause / fix |
| ------- | ----------- |
| Test fails with `Set environment variable COSMOSDB_MULTI_REGION to run the tests` | An integration test ran without a live multi-region account. Set the `COSMOSDB_MULTI_REGION` connection string (or filter to the unit tests). |
| Proxy test fails with `Set environment variable COSMOSDB_THIN_CLIENT` | `FaultInjectionProxyTests` needs a ThinClient/proxy connection string in `COSMOSDB_THIN_CLIENT`. |
| `InvalidOperationException: Delay is not applicable for server error type ...` | `WithDelay` was called for a non-delay error type. It is only valid for `SendDelay`, `ResponseDelay`, and `ConnectionDelay`. |
| `ArgumentNullException: Argument 'delay' required for server error type ...` on `Build()` | A delay error type (`SendDelay`/`ResponseDelay`/`ConnectionDelay`) was built without calling `WithDelay(...)`. |
| `ArgumentException: Gone error type is not supported for Gateway connection type.` | `Gone` is Direct-mode only. Use a Direct-mode condition or a different error type for Gateway. |
| `ArgumentException: DatabaseAccountNotFound error type is not supported for Direct connection type.` | `DatabaseAccountNotFound` is Gateway-mode only. |
| `ArgumentException: <Type> is not supported for metadata requests.` | Only a subset of server errors is valid for metadata operations in Gateway mode (see README). |
| `ArgumentOutOfRangeException` on `WithInjectionRate` / `WithThreshold` | Rate/threshold must be within `(0, 1]`. |
| `ArgumentOutOfRangeException` on `WithHitLimit` / `WithInterval` | `HitLimit` must be `> 0`; connection-error `Interval` must be greater than zero. |
| Build fails on a warning | The project uses `TreatWarningsAsErrors=true` with StyleCop. Fix the warning or add the required XML doc comment. |
| Faults never fire | Confirm the `FaultInjector` was actually wired in (`WithFaultInjection(...)` on the builder, or `CosmosClientOptions.FaultInjector = ...`), the rule is enabled and its `Duration`/`StartDelay`/`HitLimit` window is active, and the `FaultInjectionCondition` (operation/connection/region/endpoint) matches the requests you are issuing. Use `FaultInjector.GetApplicationContext()` / `GetFaultInjectionRuleId(activityId)` to confirm which rules were applied. |
