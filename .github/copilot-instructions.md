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
  - **🚫 HARD RULE: NEVER push directly to `master` — NO EXCEPTIONS.** Always create a feature branch and submit a pull request. This rule cannot be overridden.
  - Keep changes minimal and focused; prefer small, targeted edits and follow existing code style.
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

If anything here is unclear or you want the file to include additional examples (specific files, common refactor targets, or typical PR reviewers), tell me what to add and I will iterate.
