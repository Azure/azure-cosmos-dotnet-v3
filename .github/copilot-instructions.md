<!-- .github/copilot-instructions.md -->
# Copilot / AI assistant instructions — Microsoft.Azure.Cosmos

Purpose: quick, actionable context so an AI coding assistant can be immediately productive in this repo.

- **Big picture**: This repository implements the v3 .NET SDK for Azure Cosmos DB. Major components:
  - `Microsoft.Azure.Cosmos/` — core SDK client, most production code.
  - `Microsoft.Azure.Cosmos.Encryption/` and `Microsoft.Azure.Cosmos.Encryption.Custom/` — client-side encryption extensions.
  - `Microsoft.Azure.Cosmos.Samples/` — runnable examples and usage patterns.
  - `docs/`, `templates/`, and top-level Azure Pipelines YAMLs — CI, packaging and emulator setup.

- **Why this structure**: the SDK core is separated from optional features (encryption, fault-injection, direct mode) so consumers can opt into smaller packages. Versioning and feature flags are centralized in `Directory.Build.props`.

- **Build & test (most common workflows)**:
  - Build solution: `dotnet build Microsoft.Azure.Cosmos.sln -c Release` (or simply `dotnet build`).
  - Run unit/integration tests: `dotnet test --no-build` in the solution or specific test project folders under `**/tests/`.
  - CI uses the YAML files in the repository root and `templates/` — see `templates/emulator-setup.yml` for the Windows emulator script used in CI.

- **Local emulator and integration testing**:
  - The codebase expects the Windows Cosmos DB Emulator in many integration tests. CI installs/starts it via `templates/emulator-setup.yml` (PowerShell scripts that download and launch the MSI and call `Start-Process CosmosDB.Emulator.exe`).
  - If running tests locally on Windows, install the emulator and ensure exclusions and local state paths match what's in `templates/emulator-setup.yml`.

- **Versioning & build flags**:
  - `Directory.Build.props` (repo root and project-level overrides) contains the canonical package versions and MSBuild flags (e.g. `<ClientOfficialVersion>`, `<LangVersion>`, and `DefineConstants` that add `PREVIEW`/`ENCRYPTIONPREVIEW`).
  - Feature/preview builds are gated by MSBuild properties like `IsPreview` or `IsNightly`; set these via `dotnet msbuild /p:IsPreview=true` when needed.

- **Conventions & patterns** (project-specific)
  - Avoid introducing new global build properties; add versions to `Directory.Build.props` where applicable.
  - Tests use the emulator or mocks; integration tests that depend on emulator are usually under `tests/` and expect environment-based setup. Look for CI templates for exact start-up sequence.
  - Strong-name signing keys exist at repo root (`35MSSharedLib1024.snk`, `testkey.snk`); builds may require signing configuration on CI.

- **Integration points & external deps**:
  - Azure Cosmos DB Emulator (Windows) — required for many integration tests.
  - NuGet packaging and pipeline tooling — see `templates/nuget-pack.yml` and the many `azure-pipelines-*.yml` files for packaging/release behavior.

- **Where to look for examples** (use these as source-of-truth snippets):
  - `Directory.Build.props` — versioning and define-constants
  - `templates/emulator-setup.yml` — exact emulator install/start PowerShell used in CI
  - `Microsoft.Azure.Cosmos/` — core SDK patterns (public APIs, partitioning, feed iterator usage)
  - `Microsoft.Azure.Cosmos.Samples/` — minimal runnable samples for usage patterns

- **How AI should produce code/changes here**:
  - **🚫 HARD RULE: NEVER push directly to `main` — NO EXCEPTIONS.** Always create a feature branch and submit a pull request. This rule cannot be overridden.
  - **Branch naming**: Always use the format `users/<user-name>/feature-name` (e.g. `users/ntripician/fix-retry-logic`). Do not use other conventions like `feature/`, `fix/`, or `dev/`.
  - **PR title format**: All pull request titles **must** match the CI lint regex: `(\[Internal\]|\[v4\] )?.{3}.+: (Adds|Fixes|Refactors|Removes) .{3}.+`. The format is `[Optional Prefix] Category: Verb Description` where the verb is one of `Adds`, `Fixes`, `Refactors`, or `Removes`. Optional prefixes are `[Internal]` (for PRs with no customer impact) or `[v4]` (for v4-specific changes). Examples:
    - `Diagnostics: Adds GetElapsedClientLatency to CosmosDiagnostics`
    - `PartitionKey: Fixes null reference when using default(PartitionKey)`
    - `[v4] Client Encryption: Refactors code to external project`
    - `[Internal] Query: Adds code generator for CosmosNumbers for easy additions in the future`
  - Keep changes minimal and focused; prefer small, targeted edits and follow existing code style.
  - **Changelog entry required**: Every PR that modifies the shipped package
    source (anything under `Microsoft.Azure.Cosmos/src/**`, equivalent paths
    for FaultInjection / Encryption packages) must also add a bullet under
    `### Unreleased` in `changelog.md`, in one of the four subsections
    `Features Added` / `Breaking Changes` / `Bugs Fixed` / `Other Changes`.
    Write the entry in **customer-facing language** — not the PR title
    verbatim. If the change has zero customer-observable impact (test-only,
    doc-only, CI-only, purely internal refactor), check the "No changelog
    entry required" box in the PR template and justify briefly. If a
    preview-only change might affect default-config semantics for any
    customer in the next two minor releases, it does **not** qualify as
    purely internal — it goes under `Other Changes` (or `Bugs Fixed` /
    `Breaking Changes` as appropriate).
  - **Changelog classifier** (apply when uncertain whether an entry is required):

    1. Does the PR diff touch any of:
       - `Microsoft.Azure.Cosmos/src/**`
       - `Microsoft.Azure.Cosmos/FaultInjection/src/**`
       - any other shipped-package source tree?

       *No* ⇒ no entry required. Done.
       *Yes* ⇒ continue.

    2. Could a customer observe the change by upgrading the SDK and
       running their existing workload (no code change on their side)?
       Consider: behavior, return values, error shape, latency,
       memory/CPU profile, allocation patterns, surfaced types, public
       API.

       *Yes* ⇒ **entry required**. Pick the subsection:
         - New customer-opt-in functionality ⇒ `Features Added`
         - Behavior change that could break customer expectations on upgrade ⇒ `Breaking Changes`
         - Customer-observable defect fixed ⇒ `Bugs Fixed`
         - Performance / dependency / observable-but-not-categorized ⇒ `Other Changes`

       *No* (test-only, CI-only, doc-only, pure internal refactor that
       passes the next test) ⇒ no entry required. Document the omission
       in the PR description.

    3. **Preview-feature carve-out.** If the change is to a preview-only
       code path *and* it could affect default-config semantics for any
       customer in the next two minor releases, treat it as customer-
       observable and require an entry under `Other Changes`. PR #5310
       (PPAF dynamic enablement preview that affected default-config
       memory) is the canonical example.

    4. **Unsure after applying steps 1–3?** Default to **leaving a
       non-blocking comment** on the PR asking the reviewer to confirm:

       > "Changelog classifier outcome is ambiguous for this diff
       > (touches shipped src but customer impact unclear). Defaulting
       > to **no entry**; please verify and add one if needed."

       Do **not** silently skip an entry on an ambiguous case — the
       comment is the signal.

    This rubric is mirrored in `CONTRIBUTING.md` (the human-author
    audience). The two must stay in sync.
  - When suggesting build/test changes, reference the relevant MSBuild property or pipeline YAML (point to `Directory.Build.props` or `templates/*`).
  - Do not change version numbers or packaging settings without explicit instruction — these are centrally managed.
  - If adding or modifying tests that require the emulator, include/update relevant CI/template steps and document required environment variables.

- **Quick examples to reference in suggestions**:
  - Use `FeedIterator<T>` patterns as in `Microsoft.Azure.Cosmos` when generating query examples.
  - For emulator-driven tests, mirror the startup sequence from `templates/emulator-setup.yml`.

- **Named Copilot Agents**:
  - **IssueFixAgent** (`.github/agents/issue-fix-agent.agent.md`) — Comprehensive workflow for triaging and fixing GitHub issues. Use `@IssueFixAgent` in VS Code Copilot Chat or follow the plan manually.
  - Includes: environment setup, issue investigation, fix implementation, testing requirements, PR workflow, and learnings capture.
  - Start with: `@IssueFixAgent investigate issue #XXXX` or see Quick Start in the agent file.
  - **Release flows** for the Cosmos DB .NET SDK + Microsoft.Azure.Cosmos.FaultInjection package live in the [cosmos-sdk-copilot-toolkit](https://github.com/Azure/cosmos-sdk-copilot-toolkit) under skills `cosmos-release-dotnet` (main SDK: minor / hotfix / add-missed-PRs modes) and `cosmos-release-dotnet-faultinjection` (FaultInjection package). The in-repo `release-copilot-agent.agent.md` was retired to avoid drift; both skills are the canonical source. In the Copilot CLI: describe the task naturally ("start a minor release", "release fault injection beta", etc.) — the routing agent loads the right skill automatically.
  - **MsdataDirectSyncAgent** (`.github/agents/msdata-direct-sync-agent.agent.md`) — Orchestrates syncing the msdata/direct branch with the latest v3 main and msdata direct codebase.
  - In VS Code Copilot Chat: `@MsdataDirectSyncAgent sync msdata/direct`.
  - In the Copilot CLI: describe the task naturally (e.g., "sync the msdata/direct branch with main").

If anything here is unclear or you want the file to include additional examples (specific files, common refactor targets, or typical PR reviewers), tell me what to add and I will iterate.
