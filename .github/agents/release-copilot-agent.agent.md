# Release Copilot Agent
## Azure Cosmos DB .NET SDK (azure-cosmos-dotnet-v3)

---

## Quick Start Prompt

**For a minor version release:**
```
@ReleaseCopilotAgent start minor
```

**For a hotfix release:**
```
@ReleaseCopilotAgent start hotfix
```

**What the agent will do:**
1. Ask whether to run in **Minor Mode** or **Hotfix Mode**
2. Verify environment setup (.NET SDK, `gh` CLI, GenAPI tool)
3. Determine current and target versions from `Directory.Build.props`
4. Generate changelog entries from merged PRs (filtering out `[Internal]`)
5. Bump version numbers following versioning rules
6. Build the SDK and generate API contract files via GenAPI
7. Create a release PR with API diff in the description
8. Ensure that a full test suite passes, including contract enforcement tests.
9. Guide through post-merge pipeline queuing and NuGet publish

---

## 0. Environment Setup

### 0.1 Prerequisites

| Tool | Purpose | Required |
|------|---------|----------|
| **.NET SDK** | Build the SDK, produce release DLLs | ✅ Yes |
| **GitHub CLI (`gh`)** | PR creation, branch management | ✅ Yes |
| **GitHub MCP Server** | List PRs, search code, create PRs | ✅ Yes |
| **GenAPI tool** | Generate API contract files | ✅ Yes (bundled in `tools/GenAPI/`) |
| **Windows OS** | GenAPI.exe is Windows-only | ✅ Yes |

### 0.2 Verify Environment

Run the following checks before proceeding:

```powershell
# Verify .NET SDK
dotnet --version

# Verify GitHub CLI
gh auth status

# Verify GenAPI tool exists
Test-Path "tools/GenAPI/GenAPI.exe"
```

> **⚠️ GenAPI.exe is Windows-only.** This agent must be run on a Windows machine.

---

## 1. Mode Selection

When the agent is invoked, ask the user:

> **Which release mode would you like to run?**
> 1. **Minor Mode** — Minor release (GA + Preview)
> 2. **Hotfix Mode** — Patch release on an existing release branch (GA + Preview)

Based on the selection, proceed to the corresponding section below.

---

## 2. Minor Mode (Minor Release)

### 2.1 Determine Current Versions

Read `Directory.Build.props` at the repository root to determine current versions:

```xml
<ClientOfficialVersion>X.Y.Z</ClientOfficialVersion>
<ClientPreviewVersion>X.Y+1.0</ClientPreviewVersion>
<ClientPreviewSuffixVersion>preview.Z</ClientPreviewSuffixVersion>
```

**Versioning rules** (enforced by `ContractEnforcementTests.cs`):
- Preview minor version = Official minor version + 1
- Preview suffix number = Official patch/build version
- Preview suffix format: `preview.N`

Example: If releasing `3.58.0`:
- `ClientOfficialVersion` → `3.58.0`
- `ClientPreviewVersion` → `3.59.0`
- `ClientPreviewSuffixVersion` → `preview.0`

**Ask the user to confirm the target release version.**

### 2.2 Determine the Previous Release Version

Read `Directory.Build.props` on the `master` branch — the current `ClientOfficialVersion` value represents the last released version. Use this to determine which PRs to include in the changelog.

### 2.3 Generate Changelog

#### 2.3.1 List Merged PRs Since Last Release

Use the GitHub MCP server or `gh` CLI to list all merged PRs to `master` since the last release:

```powershell
# Find the merge commit date of the last release PR, or the date of the last release tag
gh pr list --repo Azure/azure-cosmos-dotnet-v3 --state merged --base master --limit 200 --json number,title,mergedAt
```

Or use the GitHub MCP `list_pull_requests` tool to fetch merged PRs.

#### 2.3.2 Filter and Categorize

- **Exclude** PRs with `[Internal]` prefix in the title
- **Exclude** PRs with `[v4]` prefix (different SDK)
- **Categorize** by the verb in the PR title (per the PR lint regex `(\[Internal\]|\[v4\] )?.{3}.+: (Adds|Fixes|Refactors|Removes) .{3}.+`):
  - `Adds` → **Added** section
  - `Fixes` → **Fixed** section
  - `Removes` → **Removed** section
  - `Refactors` → **Fixed** section (or a separate section if preferred)

#### 2.3.3 Format Changelog Entries

Follow the existing format in `changelog.md`:

```markdown
### <a name="X.Y.Z"/> [X.Y.Z](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/X.Y.Z) - YYYY-M-DD

#### Added
- [PR#](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/PR#) PR Title

#### Fixed
- [PR#](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/PR#) PR Title
```

Also create the preview header (empty or with preview-specific entries):

```markdown
### <a name="X.Y+1.0-preview.Z"/> [X.Y+1.0-preview.Z](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/X.Y+1.0-preview.Z) - YYYY-M-DD
```

Insert both entries at the top of the "Release notes" section in `changelog.md`, after line 16 (after the format/versioning note), with the preview entry first, then the GA entry.

**Present the generated changelog to the user for review before committing.**

### 2.4 Bump SDK Version

Update `Directory.Build.props` at the repository root:

```xml
<ClientOfficialVersion>NEW_GA_VERSION</ClientOfficialVersion>
<ClientPreviewVersion>NEW_PREVIEW_VERSION</ClientPreviewVersion>
<ClientPreviewSuffixVersion>preview.PATCH</ClientPreviewSuffixVersion>
```

### 2.5 Generate API Contract Files

#### 2.5.1 Build the SDK

```powershell
# GA build
dotnet build Microsoft.Azure.Cosmos\src -c Release

# Preview build
dotnet build Microsoft.Azure.Cosmos\src /p:IsPreview=true -c Release
```

#### 2.5.2 Run GenAPI

```powershell
# GA contract
tools\GenAPI\GenAPI.exe -assembly:"Microsoft.Azure.Cosmos\src\bin\Release\netstandard2.0\Microsoft.Azure.Cosmos.Client.dll" -apiOnly -s:text -out:"Microsoft.Azure.Cosmos\contracts\API_X.Y.Z.txt"

# Preview contract
tools\GenAPI\GenAPI.exe -assembly:"Microsoft.Azure.Cosmos\src\bin\Release\netstandard2.0\Microsoft.Azure.Cosmos.Client.dll" -apiOnly -s:text -out:"Microsoft.Azure.Cosmos\contracts\API_X.Y+1.0-preview.Z.txt"
```

> **Note:** After building with `/p:IsPreview=true`, the DLL will include preview APIs. Run GenAPI against that build for the preview contract file.

#### 2.5.3 Generate API Diff

```powershell
# Compare GA contract with previous GA version
git diff --no-index "Microsoft.Azure.Cosmos\contracts\API_PREV.txt" "Microsoft.Azure.Cosmos\contracts\API_NEW.txt"

# Compare preview contract with previous preview version
git diff --no-index "Microsoft.Azure.Cosmos\contracts\API_PREV_PREVIEW.txt" "Microsoft.Azure.Cosmos\contracts\API_NEW_PREVIEW.txt"
```

**Capture the diff output — it must be included in the PR description.**

### 2.6 Create Release PR

#### 2.6.1 Create Feature Branch

```powershell
git checkout -b release/X.Y.Z
```

> **🚫 NEVER push directly to `master`.** Always create a feature branch and PR.

#### 2.6.2 Commit Changes

Stage and commit all changes:
- `Directory.Build.props` (version bump)
- `changelog.md` (new entries)
- `Microsoft.Azure.Cosmos\contracts\API_X.Y.Z.txt` (GA contract)
- `Microsoft.Azure.Cosmos\contracts\API_X.Y+1.0-preview.Z.txt` (preview contract)

```powershell
git add Directory.Build.props changelog.md Microsoft.Azure.Cosmos\contracts\
git commit -m "Release: Bumps version to X.Y.Z

- Updates ClientOfficialVersion to X.Y.Z
- Updates ClientPreviewVersion to X.Y+1.0
- Updates ClientPreviewSuffixVersion to preview.Z
- Adds changelog entries for X.Y.Z
- Adds API contract files for X.Y.Z and X.Y+1.0-preview.Z"
```

#### 2.6.3 Create PR

```powershell
gh pr create --title "Release: Adds version X.Y.Z" --body "## Release X.Y.Z

### Version Changes
- ClientOfficialVersion: PREV → X.Y.Z
- ClientPreviewVersion: PREV → X.Y+1.0
- ClientPreviewSuffixVersion: PREV → preview.Z

### Changelog
<paste generated changelog entries>

### API Contract Diff (GA)
\`\`\`diff
<paste GA API diff>
\`\`\`

### API Contract Diff (Preview)
\`\`\`diff
<paste preview API diff>
\`\`\`

### Checklist
- [ ] Changelog entries reviewed by team
- [ ] API contract diff reviewed by Kiran and Kirill
- [ ] Preview APIs reviewed (email sent to azurecosmossdkdotnet@microsoft.com)
- [ ] Kiran sign-off obtained
"
```

### 2.7 Post-PR Checklist

After creating the PR, remind the user:

1. **Ping the team** to review changelog entries for accuracy
2. **Send email** to `azurecosmossdkdotnet@microsoft.com` to review preview APIs (include link to the PR)
3. **Kiran sign-off** is required for all SDK releases
4. **Should this release bump the Recommended Version?** (Check if there are critical fixes or important availability improvements — if yes, update the `## Recommended version` section in `changelog.md`)

### 2.8 Post-Merge Steps (Guidance Only)

After the PR is approved and merged:

1. **Create release branch:**
   ```powershell
   git checkout master && git pull
   git checkout -b releases/X.Y.Z
   git push origin releases/X.Y.Z
   ```

2. **Create GitHub Release:**
   - Go to https://github.com/Azure/azure-cosmos-dotnet-v3/releases
   - Tag: `X.Y.Z`, Target: `releases/X.Y.Z`
   - Body: Copy changelog notes
   - For preview: Check "This is a pre-release"

3. **Metadata XML update:**
   - Fork `azure-docs-sdk-dotnet` repo
   - Update version in the metadata file
   - Submit PR

---

## 3. Hotfix Mode

### 3.1 Select PRs for Hotfix

Ask the user:
> **Which PR(s) should be included in this hotfix?**
> Please provide the PR number(s).

Fetch details for each PR using the GitHub MCP server.

### 3.2 Identify Release Branch

Determine the latest release branch to hotfix:

```powershell
git branch -r --list "origin/releases/3.*" | Sort-Object | Select-Object -Last 5
```

Ask the user to confirm which release branch to hotfix. The new hotfix branch will bump the patch version (e.g., `releases/3.57.0` → `releases/3.57.1`).

### 3.3 Determine Hotfix Version

Based on the selected release branch:
- If hotfixing `releases/3.57.0`, the new version is `3.57.1`
- Update versioning accordingly:
  - `ClientOfficialVersion` → `3.57.1`
  - `ClientPreviewVersion` → `3.58.0` (minor + 1)
  - `ClientPreviewSuffixVersion` → `preview.1` (suffix = patch)

### 3.4 Create Hotfix Branch

```powershell
git fetch origin
git checkout -b releases/X.Y.Z+1 origin/releases/X.Y.Z
git push origin releases/X.Y.Z+1
```

### 3.5 Cherry-Pick PRs

For each PR selected by the user:

```powershell
# Get the merge commit SHA for the PR
gh pr view <PR_NUMBER> --json mergeCommit --jq '.mergeCommit.oid'

# Create a working branch for cherry-picks
git checkout -b hotfix/X.Y.Z+1 releases/X.Y.Z+1

# Cherry-pick each commit
git cherry-pick <MERGE_COMMIT_SHA>
```

If there are conflicts, notify the user and assist with resolution.

### 3.6 Version Bump on Hotfix Branch

Update `Directory.Build.props` on the hotfix branch with the new patch version (same rules as Minor Mode).

### 3.7 Changelog Update

Add hotfix entries to `changelog.md` on the hotfix branch. Only include the PRs being cherry-picked:

```markdown
### <a name="X.Y.Z+1"/> [X.Y.Z+1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/X.Y.Z+1) - YYYY-M-DD

#### Fixed
- [PR#](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/PR#) PR Title
```

### 3.8 Generate API Contract Files

Both GA and preview contract files must be generated for the hotfix.

#### 3.8.1 Build the SDK

```powershell
# GA build
dotnet build Microsoft.Azure.Cosmos\src -c Release

# Preview build
dotnet build Microsoft.Azure.Cosmos\src /p:IsPreview=true -c Release
```

#### 3.8.2 Run GenAPI

```powershell
# GA contract
tools\GenAPI\GenAPI.exe -assembly:"Microsoft.Azure.Cosmos\src\bin\Release\netstandard2.0\Microsoft.Azure.Cosmos.Client.dll" -apiOnly -s:text -out:"Microsoft.Azure.Cosmos\contracts\API_X.Y.Z+1.txt"

# Rebuild with preview flag, then generate preview contract
tools\GenAPI\GenAPI.exe -assembly:"Microsoft.Azure.Cosmos\src\bin\Release\netstandard2.0\Microsoft.Azure.Cosmos.Client.dll" -apiOnly -s:text -out:"Microsoft.Azure.Cosmos\contracts\API_X.Y+1.0-preview.Z+1.txt"
```

> **Note:** Build with `/p:IsPreview=true` before running GenAPI for the preview contract so the DLL includes preview APIs.

#### 3.8.3 Generate API Diff

```powershell
# Compare GA contract with previous GA version
git diff --no-index "Microsoft.Azure.Cosmos\contracts\API_X.Y.Z.txt" "Microsoft.Azure.Cosmos\contracts\API_X.Y.Z+1.txt"

# Compare preview contract with previous preview version
git diff --no-index "Microsoft.Azure.Cosmos\contracts\API_PREV_PREVIEW.txt" "Microsoft.Azure.Cosmos\contracts\API_X.Y+1.0-preview.Z+1.txt"
```

**Capture both diffs — they must be included in the PR description.**

### 3.9 Ensure Master Sync

**Important:** Contract file changes and changelog updates must also be reflected on `master`.

1. Create a separate PR targeting `master` that includes:
   - The new GA API contract file (`API_X.Y.Z+1.txt`)
   - The new preview API contract file (`API_X.Y+1.0-preview.Z+1.txt`)
   - Changelog entries for the hotfix version
2. This ensures `master` stays in sync with released versions

### 3.10 Create Hotfix PR

Create a PR from the working branch targeting the hotfix release branch:

```powershell
gh pr create --base releases/X.Y.Z+1 --title "Hotfix: Adds version X.Y.Z+1" --body "## Hotfix X.Y.Z+1

### Cherry-picked PRs
- #PR1: Title
- #PR2: Title

### Version Changes
- ClientOfficialVersion: X.Y.Z → X.Y.Z+1
- ClientPreviewVersion: PREV → X.Y+1.0
- ClientPreviewSuffixVersion: PREV → preview.Z+1

### API Contract Diff (GA)
\`\`\`diff
<paste GA API diff>
\`\`\`

### API Contract Diff (Preview)
\`\`\`diff
<paste preview API diff>
\`\`\`

### Checklist
- [ ] Cherry-picks verified
- [ ] Contract files (GA and preview) added to hotfix branch
- [ ] Contract files (GA and preview) synced to master (separate PR)
- [ ] Kiran sign-off obtained
"
```

### 3.11 Post-Merge Steps (Guidance Only)

After the hotfix PR is merged:

1. **Create GitHub Release:**
   - Tag: `X.Y.Z+1`, Target: `releases/X.Y.Z+1`
   - Body: Copy changelog notes

---

## 4. Common Reference

### 4.1 Version Files

| File | Properties |
|------|-----------|
| `Directory.Build.props` | `ClientOfficialVersion`, `ClientPreviewVersion`, `ClientPreviewSuffixVersion` |

### 4.2 Contract Files Location

```
Microsoft.Azure.Cosmos\contracts\API_X.Y.Z.txt        # GA
Microsoft.Azure.Cosmos\contracts\API_X.Y.Z-preview.N.txt  # Preview
```

### 4.3 GenAPI Tool Location

```
tools\GenAPI\GenAPI.exe
```

Usage:
```powershell
tools\GenAPI\GenAPI.exe -assembly:"<path-to-dll>" -apiOnly -s:text -out:"<output-path>"
```

### 4.4 PR Title Format

All PRs must follow the lint regex:
```
(\[Internal\]|\[v4\] )?.{3}.+: (Adds|Fixes|Refactors|Removes) .{3}.+
```

- `[Internal]` PRs are excluded from changelog
- `[v4]` PRs are for the v4 SDK (excluded from v3 changelog)

### 4.5 Key Contacts & Approvals

- **Kiran (@Kiran Kumar Kolli)** — Required sign-off for all SDK releases
- **Kiran & Kirill** — API contract diff approval
- **Team email:** `azurecosmossdkdotnet@microsoft.com` — Preview API review (minor release only)

### 4.6 Pipeline Links

- **Release pipeline:** `CosmosDB-Official-Release` (Azure DevOps)
- **Publish pipeline:** `Azure SDK Partner Release to Nuget` (Azure DevOps)
- **Nightly pipeline:** `CosmosDB-Nightly` (for internal/preview builds)

### 4.7 Recommended Version

If the release contains critical fixes or important availability improvements, update the recommended version in `changelog.md`:

```markdown
## <a name="recommended-version"></a> Recommended version

The **minimum recommended version is [X.Y.Z](#X.Y.Z)**.
```

---

## 5. Troubleshooting

### 5.1 GenAPI.exe Fails

- Ensure you are on Windows
- Ensure the SDK was built successfully before running GenAPI
- Check that the DLL path is correct: `Microsoft.Azure.Cosmos\src\bin\Release\netstandard2.0\Microsoft.Azure.Cosmos.Client.dll`

### 5.2 Cherry-Pick Conflicts (Hotfix Mode)

- Resolve conflicts manually
- If the conflict is in `Directory.Build.props`, use the hotfix branch version
- If the conflict is in `changelog.md`, keep both entries

### 5.3 Contract Enforcement Test Failures

If `ContractEnforcementTests` fails after version bump, verify:
- Preview minor = Official minor + 1
- Preview suffix = `preview.{official_patch}`
- Both preview and GA contract files exist in `Microsoft.Azure.Cosmos\contracts\`

### 5.4 Build Failures

```powershell
# Clean and rebuild
dotnet clean Microsoft.Azure.Cosmos\src -c Release
dotnet build Microsoft.Azure.Cosmos\src -c Release
```
