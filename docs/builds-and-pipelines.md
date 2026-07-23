# Build & CI pipelines for the Azure Cosmos DB .NET SDK

This is the reference guide for every Azure DevOps pipeline in this repository: what
each one does, when it runs, the reusable templates they share, how tests are organized,
how to add to or troubleshoot them, and the conventions to follow.

- **Audience:** anyone on the team who needs to diagnose a red build, add a test category
  or job, wire up a new pipeline, or understand how a release is produced.
- **Where pipelines run:** all pipelines execute in the Azure DevOps org
  [`cosmos-db-sdk-public`](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public)
  (project `cosmos-db-sdk-public`), which is **separate** from the shared `azure-sdk`
  Azure DevOps org that the other language SDKs use. We own this org, so pipeline/service
  connection/variable changes are ours to make.
- **The YAML lives in the repo root** (`azure-pipelines*.yml`) and in
  [`templates/`](../templates); the ADO pipeline *definitions* point at those files.

> **Quick links:** [Pipeline catalog](#pipeline-catalog) · [PR validation](#pr-validation-azure-pipelinesyml)
> · [Templates](#reusable-templates) · [Test categories](#test-categories--filtering)
> · [Live AAD tests](#live-account-aad-tests-multiregionaad) · [Adding to pipelines](#extending-the-pipelines)
> · [Troubleshooting](#troubleshooting--diagnostics)

---

## Concepts you need first

| Concept | Detail |
| --- | --- |
| **Agent pool** | Almost every job runs on the **`OneES`** 1ES-managed pool (Windows). A few opt into other images via `demands: ImageOverride -equals <image>` (e.g. `dotnet-ubuntu-latest` for the CTL Docker build) or a hosted `VmImage` (macOS/Linux in the cron pipeline). |
| **.NET versions** | Jobs install **.NET 6.0 runtime + .NET 8.0 SDK** via `UseDotNet@2`. `build-internal.yml` also installs the **.NET 9.0 SDK**. Test projects target `net6.0`. |
| **Build flags** | Compile-time feature flags gate different SDK surfaces: `IsPreview=true` (the `-preview` package), `INTERNAL` / friend-assembly + dogfood surface (`build-internal.yml`), `SdkProjectRef` / `OSSProjectRef` (source vs NuGet references for the encryption/benchmark parity builds). Versions and constants are centralized in [`Directory.Build.props`](../Directory.Build.props) — do **not** change them casually. |
| **Emulator** | Windows integration tests need the Cosmos DB emulator, installed/started by [`templates/emulator-setup.yml`](../templates/emulator-setup.yml) (see [Emulator setup](#emulator-setup-emulator-setupyml)). |
| **Test selection** | Everything is driven by MSTest `TestCategory` filters passed to `dotnet test --filter`. See [Test categories](#test-categories--filtering). |
| **Secrets / connection strings** | Live-account connection strings (`COSMOSDB_MULTI_REGION`, `COSMOSDB_MULTIMASTER`, `COSMOSDB_THINCLIENT`, …) and AAD settings are ADO pipeline **variables/variable groups**, injected as env vars — never hard-coded. |

---

## Pipeline catalog

| File | ADO pipeline | Trigger | Purpose |
| --- | --- | --- | --- |
| [`azure-pipelines.yml`](../azure-pipelines.yml) | [dotnet-v3-ci](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=63) | **PR** to `main`/`releases/*` | Primary PR validation (build, unit, emulator, samples, CTL, internal/preview/benchmark, thin client, live AAD). |
| [`azure-pipelines-nightly.yml`](../azure-pipelines-nightly.yml) | [dotnet-v3-nightly](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=75) | **Schedule** 00:00 UTC daily | Produce nightly GA + preview NuGet packages; preview-parity build; **live AAD tests**. |
| [`azure-pipelines-rolling.yml`](../azure-pipelines-rolling.yml) | dotnet-v3-rolling | **Schedule** (weekday hours + weekend every 2h) | Full GA + preview test matrix against live accounts, with hang/crash blame dumps. |
| [`azure-pipelines-cron.yml`](../azure-pipelines-cron.yml) | dotnet-v3-cron | **Schedule** every 6h | Cross-platform run (macOS, Linux, Windows Release + Debug) of the test suite. |
| [`azure-pipelines-functional.yml`](../azure-pipelines-functional.yml) | dotnet-v3-functional | **PR** (path-filtered) | Runs `TestCategory=Functional` tests (excluded from the main PR run). |
| [`azure-pipelines-official.yml`](../azure-pipelines-official.yml) | [dotnet-v3-release](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=65) | Manual (`trigger: none`) | Official release of `Microsoft.Azure.Cosmos`: gate (static + tests + telemetry) → publish NuGet. |
| [`azure-pipelines-encryption-official.yml`](../azure-pipelines-encryption-official.yml) | [dotnet-v3-encryption-release](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=66) | Manual | Release `Microsoft.Azure.Cosmos.Encryption`. |
| [`azure-pipelines-encryption-custom-official.yml`](../azure-pipelines-encryption-custom-official.yml) | [dotnet-v3-encryption-custom-release](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=67) | Manual | Release `Microsoft.Azure.Cosmos.Encryption.Custom`. |
| [`azure-pipelines-faultinjection.yml`](../azure-pipelines-faultinjection.yml) | dotnet-v3-faultinjection | Manual | Release the `Microsoft.Azure.Cosmos.FaultInjection` package (+ integration tests). |
| [`azure-pipelines-ctl-publishing.yml`](../azure-pipelines-ctl-publishing.yml) | [dotnet-v3-ctl-image-publish](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=64) | CI on merge to `main` (implicit) | Build & push the CTL load-test Docker image to ACR. |
| [`azure-pipelines-msdata-direct.yml`](../azure-pipelines-msdata-direct.yml) | dotnet-v3-msdata-direct | **PR** on `msdata/direct*` | PR validation for the `msdata/direct` feature branch. |
| [`azure-pipelines-msdata-aot.yml`](../azure-pipelines-msdata-aot.yml) | dotnet-v3-msdata-aot | Manual | AOT-mode build + NuGet pack (published under an `aot/` blob prefix). |

> **Note on triggers:** most scheduled/release pipelines start with `trigger: none` and `pr: none`
> and are wired to a schedule or run manually. `azure-pipelines-ctl-publishing.yml` omits the
> trigger block entirely, so it uses the ADO default CI trigger (runs on commits to `main`).

---

## PR validation (`azure-pipelines.yml`)

The gate every PR to `main` / `releases/*` must pass. It excludes doc-only changes
(`*.md`, `docs/**`, `contracts/**`). It fans out into these template jobs:

| Template | What it verifies |
| --- | --- |
| [`static-tools.yml`](../templates/static-tools.yml) | Builds with the PREVIEW flag; runs security/compliance scanners (most currently gated off — see the template). |
| [`nuget-pack.yml`](../templates/nuget-pack.yml) | The SDK still packs into a valid NuGet package (`ReleasePackage: false` here — no publish). |
| [`build-ctl.yml`](../templates/build-ctl.yml) | The [CTL runner](../Microsoft.Azure.Cosmos.Samples/Tools/CTL) builds. |
| [`build-samples.yml`](../templates/build-samples.yml) | The [samples](../Microsoft.Azure.Cosmos.Samples/Usage) build. |
| [`build-test.yml`](../templates/build-test.yml) | **The core** — unit tests, emulator tests (4 parallel category buckets), flaky retries, coverage, perf baseline, encryption emulator tests. |
| [`build-internal.yml`](../templates/build-internal.yml) | The solution builds under the `INTERNAL` flag (dogfood / friend-assembly surface). |
| [`build-preview.yml`](../templates/build-preview.yml) | The PREVIEW-flag build + preview unit tests + encryption/benchmark surface-area parity. |
| [`build-benchmark.yml`](../templates/build-benchmark.yml) | The [benchmark tool](../Microsoft.Azure.Cosmos.Samples/Tools/Benchmark) builds (GA + preview). |
| [`build-thinclient.yml`](../templates/build-thinclient.yml) | Thin-client protocol emulator tests. |
| [`build-test-aad.yml`](../templates/build-test-aad.yml) | **Live-account AAD (Entra ID) tests** — see [that section](#live-account-aad-tests-multiregionaad). Self-skips until its service connection is set. |

**External contributors:** the CI won't auto-run for forked PRs; a maintainer starts it with an
`/azp run` comment after confirming the change (see [CONTRIBUTING](../CONTRIBUTING.md#contribution-flow)).

---

## Scheduled pipelines

- **Nightly** (`azure-pipelines-nightly.yml`, 00:00 UTC) — produces two packages from `main`:
  a GA `X.Y.Z-nightly-MMDDYYYY` and a preview `X.Y.Z-preview-nightly-MMDDYYYY` (`X.Y.Z` from
  [`Directory.Build.props`](../Directory.Build.props)), published to the `cosmosdb/csharp/nightly`
  and `cosmosdb/csharp/nightly-preview` blob containers (cleaning prior contents). Also runs a
  preview-parity build and the **live AAD tests** stage.
- **Rolling** (`azure-pipelines-rolling.yml`) — two schedules (weekday 00/05/07/13 UTC; weekend
  every 2h). Runs `build-test.yml` twice (GA and `IsPreview=true`) against the live multi-region
  and multi-master accounts with `--blame-hang --blame-crash` dumps, plus a preview build.
- **Cron** (`azure-pipelines-cron.yml`, every 6h) — cross-platform coverage: macOS and Linux
  (excluding `Windows`-category tests) and Windows Release + Debug.

## Release / official pipelines (manual)

All are `trigger: none`, run by a release owner, set `Packaging.EnableSBOMSigning: true`, and
publish to Azure blob storage via the **`azuresdkpartnerdrops`** service connection (container
`drops`, prefix `cosmosdb/csharp/<version>` or a package-specific prefix).

- **`azure-pipelines-official.yml`** — two stages: **Gate** (`static-tools` → `build-test` →
  a 120-min `TelemetryToService` job running `ClientTelemetryRelease` category) → **Publish**
  (`nuget-pack` with `ReleasePackage: true`, version from `Directory.Build.props`).
- **`azure-pipelines-encryption-official.yml`** / **`-encryption-custom-official.yml`** —
  gate + build/pack/publish for the two encryption satellite packages, using their
  `static-tools-encryption*.yml` and `encryption*-nuget-pack.yml` templates. Published under
  `cosmosdb/csharp/encryption/<version>` and `…/encryption.custom/<version>`.
- **`azure-pipelines-faultinjection.yml`** — gate (`static-tools` + a 120-min FaultInjection
  integration-test job against the multi-region account) → publish via
  [`fault-injection-nuget-pack.yml`](../templates/fault-injection-nuget-pack.yml). A
  `build-fault-injection-test.yml` reference is currently commented out.
- **`azure-pipelines-msdata-aot.yml`** — `static-tools` → `build-internal` → `nuget-pack` with
  an `aot/$(BlobVersion)` prefix to keep AOT artifacts separate.

> **Release runbooks** for the main SDK and the FaultInjection package live in the
> [`cosmos-sdk-copilot-toolkit`](https://github.com/Azure/cosmos-sdk-copilot-toolkit) skills
> `cosmos-release-dotnet` and `cosmos-release-dotnet-faultinjection`. This doc covers the
> pipeline mechanics; the runbooks cover the end-to-end release flow.

## Other pipelines

- **Functional** (`azure-pipelines-functional.yml`, PR) — runs the `TestCategory=Functional`
  tests that the main PR pipeline excludes, path-filtered to `Microsoft.Azure.Cosmos/*`.
- **CTL image publish** (`azure-pipelines-ctl-publishing.yml`, on merge to `main`) — builds the
  `cosmos-dotnet-ctl` Docker image on an Ubuntu image and pushes it to
  `cosmosdotnetsdkctl.azurecr.io` (auth via the `dotnet-cosmos-container-registry-pwd` variable).
- **msdata-direct** (`azure-pipelines-msdata-direct.yml`, PR on `msdata/direct*`) — the feature
  branch's own validation, mirroring the main PR set but using the `*-msdata` template variants
  ([`build-test-msdata.yml`](../templates/build-test-msdata.yml),
  [`build-preview-msdata.yml`](../templates/build-preview-msdata.yml)).

---

## Reusable templates

Templates under [`templates/`](../templates) are the building blocks; pipelines compose them and
pass parameters. Key ones:

### Core test template (`build-test.yml`)

The heart of CI. It defines a set of **parallel jobs** (all on `OneES`):

| Job | Runs |
| --- | --- |
| `Microsoft.Azure.Cosmos.Tests` | Unit tests (no emulator, no endpoint). `retryCountOnTaskFailure: 2`. |
| `…Tests Flaky` | Unit tests filtered to `TestCategory=Flaky`, retried up to 4×. |
| `…Tests Coverage` | Unit tests in Debug with Coverlet → Cobertura report (gated on `IncludeCoverage`). |
| `PerformanceTests` | Builds + runs the micro-benchmarks with `--BaselineValidation` (gated on `IncludePerformance`, 120-min timeout). |
| `EmulatorTests … Client Telemetry, Query, ChangeFeed, ReadFeed, Batch` | **Bucket 1** of emulator tests (`EmulatorPipeline1Arguments`). |
| `EmulatorTests … Others` | **Bucket 2** — everything not in bucket 1 (`EmulatorPipeline2Arguments`). |
| `EmulatorTests … Flaky` | Emulator flaky tests, retried 4×. |
| `Encryption EmulatorTests` / `Encryption.Custom EmulatorTests` | The two encryption emulator suites (gated on `IncludeEncryption`). |
| `EmulatorTests … MultiRegion` | `TestCategory=MultiRegion` against a live multi-region account (`COSMOSDB_MULTI_REGION`). |
| `EmulatorTests … MultiMaster` | `TestCategory=MultiMaster` against a live multi-master account (`COSMOSDB_MULTIMASTER`). |

Buckets 1 and 2 exist purely to **parallelize** the large emulator suite and shorten PR feedback
time. Every emulator job pulls in `emulator-setup.yml` and sets
`AZURE_COSMOS_NON_STREAMING_ORDER_BY_FLAG_DISABLED: true`.

### Emulator setup (`emulator-setup.yml`)

Steps-only template (no parameters) used by every Windows emulator job:

1. Downloads the emulator MSI from `$env:EMULATORMSIURL` and expands it with `lessmsi`
   (from [`tools/`](../tools)) instead of installing it.
2. Adds Windows Defender exclusions for the emulator dirs.
3. Starts it via the `Microsoft.Azure.CosmosDB.Emulator` PowerShell module with:
   `/NoExplorer /NoUI /DisableRateLimiting /PartitionCount=10 /Consistency=Strong /EnablePreview
   /EnableSqlComputeEndpoint /overrides=enablePreviousImageForDeleteInFFCF:true;queryEnableFullText:true;`
   then polls `Get-CosmosDbEmulatorStatus` until **Running** (retries with backoff; fails the job
   if it never starts).

To reproduce locally, install the [same emulator](https://aka.ms/cosmosdb-emulator) and start it
with comparable flags (see [CONTRIBUTING](../CONTRIBUTING.md#usage-of-cosmos-db-emulator-for-running-unit-tests)).

### Packaging & build templates

| Template | Role |
| --- | --- |
| [`nuget-pack.yml`](../templates/nuget-pack.yml) | Build + `dotnet pack` the SDK. When `ReleasePackage: true`: also `.snupkg` symbols, SBOM (`ManifestGeneratorTask@0`), and `AzureFileCopy@6` to the `azuresdkpartnerdrops` `drops` container under `cosmosdb/csharp/<BlobVersion>` (optionally cleaning first). |
| [`static-tools.yml`](../templates/static-tools.yml) | Preview build + security scanners (BinSkim/CredScan/PoliCheck/AntiMalware/ComponentGovernance — several currently gated off with `condition: eq(1,2)`). |
| [`build-internal.yml`](../templates/build-internal.yml) | Builds `Microsoft.Azure.Cosmos.sln` with `DefineConstants="DOCDBCLIENT COSMOSCLIENT NETSTANDARD20 INTERNAL"`; installs .NET 6/8/9. |
| [`build-preview.yml`](../templates/build-preview.yml) | Preview-flag build, preview unit tests (+ flaky retries), and encryption **surface-area parity** builds (preview vs NuGet vs GA, `SdkProjectRef` combos) to catch missing abstract-member overrides. |
| [`build-benchmark.yml`](../templates/build-benchmark.yml) | Builds `CosmosBenchmark.sln` GA (`OSSProjectRef=true`) and preview. |
| [`build-samples.yml`](../templates/build-samples.yml) / [`build-ctl.yml`](../templates/build-ctl.yml) | Build the samples solution / the CTL tool solution. |
| [`build-thinclient.yml`](../templates/build-thinclient.yml) | Emulator tests for the ThinClient protocol; sets `AZURE_COSMOS_THIN_CLIENT_ENABLED: True` and the `COSMOSDB_THINCLIENT*` connection strings. |
| [`build-test-aad.yml`](../templates/build-test-aad.yml) | The live AAD test job — see below. |

---

## Test categories & filtering

Test selection is entirely `dotnet test --filter "TestCategory=…"` expressions. Understanding the
categories is how you predict *where* a test runs.

**Always excluded from normal runs**

- `Quarantine` — disabled everywhere (known-broken / under investigation).
- `Ignore` — never run.
- `Flaky` — split into dedicated jobs with extra retries, so they don't fail the main job.
- `Functional` — excluded from PR/main; run by the **functional** pipeline.

**Routed to their own jobs / pipelines**

| Category | Where it runs |
| --- | --- |
| `MultiRegion` | `build-test.yml` MultiRegion job, live `COSMOSDB_MULTI_REGION` account. |
| `MultiMaster` | `build-test.yml` MultiMaster job, live `COSMOSDB_MULTIMASTER` account. |
| `MultiRegionAad` | `build-test-aad.yml`, live AAD account (see below). |
| `ThinClient` | `build-thinclient.yml`. |
| `ClientTelemetryEmulator` / `Query` / `ReadFeed` / `Batch` / `ChangeFeed` | Emulator **bucket 1**. |
| `ClientTelemetryRelease` | Official release `TelemetryToService` job only. |
| `LongRunning` | Excluded from the fast buckets. |
| everything else | Emulator **bucket 2** ("Others"). |

The two bucket filters live at the top of [`build-test.yml`](../templates/build-test.yml) as
`EmulatorPipeline1Arguments` / `EmulatorPipeline2Arguments`. **If you add a new category that
should run on the emulator, make sure it lands in exactly one bucket** (add it to bucket 1's
inclusion list, or it falls through to bucket 2 automatically). A category that should *not* run
in the general buckets must be excluded from **both** (this is how `MultiRegion`, `MultiRegionAad`,
etc. are kept out).

See [CONTRIBUTING → Tests](../CONTRIBUTING.md#tests) for the unit vs emulator project split.

---

## Live-account AAD tests (`MultiRegionAad`)

These authenticate to a **real, AAD-only** Cosmos DB account using a real Entra token
(data-plane RBAC) — the counterpart to the key-based `MultiRegion` live tests. They live in
`Microsoft.Azure.Cosmos.EmulatorTests` under `[TestCategory("MultiRegionAad")]` and are wired into
the PR and nightly pipelines via [`build-test-aad.yml`](../templates/build-test-aad.yml). The job
**self-skips (stays green)** whenever `AadServiceConnection` is empty, so it is safe to reference
before the connection exists.

### How auth flows

```
ADO service connection (Workload Identity Federation, no secret)
  → AzureCLI@2 logs `az` in as the connection's app-registration identity
    → SDK DefaultAzureCredential → AzureCliCredential acquires a Cosmos data-plane token
```

The tests set `COSMOSDB_MULTI_REGION_AAD` (the account endpoint) and
`COSMOSDB_AAD_USE_DEFAULT_CREDENTIAL=true`; `TestCommon` resolves the credential accordingly and
skips with `Assert.Inconclusive` if endpoint/credentials/resources aren't configured.

### Why an app registration (not the build-agent MI)

Cosmos data-plane RBAC requires the **principal and the account to be in the same tenant**. The
`OneES` build agents' managed identity lives in a **different** tenant than the corp Cosmos test
account, so granting the agent MI a role is rejected (`principal not found in the AAD tenant …`).
The working pattern is a **single-tenant app registration created in the account's tenant**, whose
service principal holds the Cosmos role. (An app-registration federated credential also sidesteps
the corp policy that blocks *managed-identity* federated credentials.)

### The account & identity (current setup)

| Item | Value |
| --- | --- |
| Account | `dotnet-v3-multiregion` (AAD-only), RG `dotnet-v3-ci-pipelines`, sub `chrande-CosmosDB-SDK-CI` |
| DB / container | `AadLiveTestDb` / `AadLiveTestContainer` (`/pk`) — pre-created (data-plane identity can't create them) |
| App registration | `dotnet-v3-aad-ci-app` (single-tenant, in the account's tenant) |
| ADO service connection | `dotnet-v3-aad-ci` (ARM, Workload Identity Federation, saved *without verification*) |
| Data-plane role | Cosmos DB Built-in Data Contributor (`…0002`) on the account → the app SP |
| Control-plane role | **Reader** on RG `dotnet-v3-ci-pipelines` → the app SP (see gotcha below) |

### Setting it up from scratch (runbook)

1. **Pre-create** DB `AadLiveTestDb` + container `AadLiveTestContainer` (`/pk`) on the AAD account.
2. **Create a single-tenant app registration** in the account's tenant (portal → Entra ID → App
   registrations). Requires a Service Tree ID.
3. **Grant its service principal** the Cosmos data-plane role — note this uses the **SP object id**
   (enterprise-application object id), *not* the app-registration object id:
   ```
   az cosmosdb sql role assignment create -a dotnet-v3-multiregion -g dotnet-v3-ci-pipelines \
     --role-definition-id 00000000-0000-0000-0000-000000000002 \
     --principal-id <SP-object-id> --scope "/"
   ```
4. **Create an ADO service connection** (ARM → *App registration or managed identity (manual)* →
   Workload identity federation), referencing the app's client id + tenant + subscription. Copy the
   **Subject identifier** it shows.
5. **Add a federated credential** on the app registration (Certificates & secrets → Federated
   credentials → *Other issuer*): Issuer `https://login.microsoftonline.com/<tenant>/v2.0`,
   Subject = the value from step 4, Audience `api://AzureADTokenExchange`. Then **Save without
   verification** on the ADO connection (the verify probe is control-plane and the app is
   data-plane only — see gotcha).
6. **Grant the SP `Reader`** on the resource group (needs an Owner / User Access Administrator):
   ```
   az role assignment create --assignee-object-id <SP-object-id> \
     --assignee-principal-type ServicePrincipal --role "Reader" \
     --scope "/subscriptions/<sub>/resourceGroups/dotnet-v3-ci-pipelines"
   ```
7. **Set `AadServiceConnection`** (and `AadAccountEndpoint`) in `azure-pipelines.yml` and
   `azure-pipelines-nightly.yml`. The job now runs.

### AAD gotchas (learned the hard way)

- **`Save without verification`** on the service connection: ADO's verify probe calls
  `subscriptions/read` (control-plane); a data-plane-only SP can't, so verification fails even
  though federation works. Saving without verification is correct here.
- **ARM `Reader` is required** even though the tests are data-plane only: `AzureCLI@2` automatically
  runs `az account set --subscription …` after login. Without *some* ARM role, the subscription
  isn't enumerable and that step fails with `The subscription … doesn't exist in cloud 'AzureCloud'`.
  Reader is the minimal fix and does **not** break `AadControlPlaneIsForbiddenAsync` (that test
  exercises the SDK/data-plane, which never touches ARM).
- **Explicit `.csproj`, not a glob:** the AAD job uses a raw `AzureCLI@2` inline `dotnet test`
  (needed for the WIF login), and MSBuild does **not** expand `*.csproj` there
  (`MSB1009: Project file does not exist`). Reference the full project path. (The other templates
  use `DotNetCoreCLI@2`, whose `projects:` input *does* expand the glob.)

---

## Extending the pipelines

**Add a test to an existing project** — just add it with the right `[TestCategory]`; the existing
jobs pick it up. Confirm which bucket/job it will land in via the
[category table](#test-categories--filtering).

**Add a new emulator category** — add it to bucket 1's inclusion list in `build-test.yml` (or let
it fall into bucket 2), and exclude it from both buckets if it needs its own job. Keep the msdata
variant (`build-test-msdata.yml`) in sync if the change should apply there too.

**Add a job that needs a live account** — model it on the MultiRegion/MultiMaster jobs: pass the
connection string as a template parameter, surface it as an env var, and gate the job with a
`condition` so it self-skips when unconfigured (as `build-test-aad.yml` does with
`AadServiceConnection`).

**Add a whole new pipeline** — create `azure-pipelines-<name>.yml` in the repo root, compose the
existing `templates/`, then create the ADO pipeline definition pointing at it. Reuse `OneES`,
`UseDotNet@2` (6 runtime / 8 SDK), and the shared connection strings. **Gotcha:** the 1ES
"new pipeline" wizard only reads YAML from the repo's **default branch** — to test a pipeline whose
YAML is only on a feature branch, either merge the YAML to `main` first or create the definition
against an existing file and repoint it.

**Conventions**

- One reusable template per concern; pipelines stay thin and pass parameters.
- Keep secrets/connection strings in ADO variables/variable groups; inject as env vars.
- Don't touch versions/packaging in `Directory.Build.props` without an explicit reason.
- Test/CI/pipeline-only changes usually need **no changelog entry**
  (see [CONTRIBUTING → Changelog](../CONTRIBUTING.md#changelog-entry)).

---

## Troubleshooting & diagnostics

**Reading a failed PR check.** From the PR, `Details` on the failed check → `View more details on
Azure Pipelines` → the run → the failing job → the failing test. Screenshots and the walk-through
are in [CONTRIBUTING → Test failures](../CONTRIBUTING.md#test-failures).

**Re-running.** Transient failures can be re-run per-check from the **Checks** section (individual
re-run, or re-run all failed). Maintainers can also comment `/azp run <pipeline>` on a PR.

**Common failure signatures**

| Symptom | Likely cause / fix |
| --- | --- |
| `Get-CosmosDbEmulatorStatus` never reaches Running / emulator job times out | Emulator failed to start on the agent — re-run the job; if persistent, check `emulator-setup.yml` and `$env:EMULATORMSIURL`. |
| A single test fails only in one emulator bucket | It's a category-routing issue — check which bucket the category maps to in `build-test.yml`. |
| `TestCategory=Flaky` test red | Flaky jobs already retry 4×; a hard failure there is usually a real regression. |
| Live MultiRegion/MultiMaster job fails to connect | The `COSMOSDB_MULTI_REGION` / `COSMOSDB_MULTIMASTER` variable/account — not your code. |
| `MSB1009: Project file does not exist … *.csproj` | A raw `dotnet test` with a glob (MSBuild doesn't expand it) — use the explicit `.csproj` path. |
| `The subscription … doesn't exist in cloud 'AzureCloud'` after WIF login | The connection identity lacks an ARM role — grant it `Reader` (see [AAD gotchas](#aad-gotchas-learned-the-hard-way)). |
| AAD job **skipped** (green, didn't run) | Expected when `AadServiceConnection` is empty. Set it to run the tests. |
| `AADSTS70025 … no configured federated identity credentials` | The app registration is missing the federated credential matching the connection's issuer/subject. |
| Preview/encryption surface-area parity build fails | A new abstract member under `#if PREVIEW` lacks an unconditional override (e.g. in `EncryptionContainer`) — see `build-preview.yml`. |

**Diagnostic tips**

- The **rolling** pipeline runs with `--blame-hang --blame-crash`; its dumps are the place to look
  for hangs/crashes that don't reproduce locally.
- Live-account issues are almost always environment/config (connection strings, RBAC, tenant), not
  the test code — verify the account and variables before digging into the SDK.
- To reproduce an emulator test locally, start the emulator with the flags in
  [CONTRIBUTING](../CONTRIBUTING.md#usage-of-cosmos-db-emulator-for-running-unit-tests) and run
  `dotnet test --filter "TestCategory=…"` in the test project folder.

---

## Reference

**Service connections**

| Connection | Used by | For |
| --- | --- | --- |
| `azuresdkpartnerdrops` | `nuget-pack.yml`, `encryption*-nuget-pack.yml`, `fault-injection-nuget-pack.yml` | Publish packages to the `drops` blob container. |
| `dotnet-v3-aad-ci` | `build-test-aad.yml` | WIF login for the live AAD tests. |
| `dotnet-cosmos-container-registry-pwd` (variable) | `azure-pipelines-ctl-publishing.yml` | ACR auth for the CTL image push. |

**Key environment variables / secrets**

`COSMOSDB_MULTI_REGION`, `COSMOSDB_MULTIMASTER`, `COSMOSDB_THINCLIENT`,
`COSMOSDB_THINCLIENTSTRONG`, `COSMOSDB_MULTI_REGION_AAD`, `COSMOSDB_AAD_USE_DEFAULT_CREDENTIAL`,
`AZURE_COSMOS_NON_STREAMING_ORDER_BY_FLAG_DISABLED`, `AZURE_COSMOS_THIN_CLIENT_ENABLED`,
`EMULATORMSIURL`, `CLIENT_TELEMETRY_SERVICE_ENDPOINT`.

**Blob storage layout** (`azuresdkpartnerdrops` → `drops`)

- `cosmosdb/csharp/<version>` — official SDK releases
- `cosmosdb/csharp/nightly`, `cosmosdb/csharp/nightly-preview` — nightly
- `cosmosdb/csharp/encryption/<version>`, `cosmosdb/csharp/encryption.custom/<version>` — encryption packages
- `cosmosdb/csharp/aot/<version>` — AOT build
- `cosmosdb/faultInjection/<version>` — FaultInjection package

**Related docs**

- [CONTRIBUTING.md](../CONTRIBUTING.md) — dev setup, tests, PR flow, changelog rules.
- [Directory.Build.props](../Directory.Build.props) — versions and build constants.
- [templates/](../templates) — the pipeline template sources.
