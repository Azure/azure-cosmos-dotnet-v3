---
name: 'ReleaseCopilotAgent'
description: 'Guides the team through full releases and hotfix releases of the Cosmos DB .NET SDK.'
tools:
  - read
  - search
  - edit
  - terminal
---

# Release Copilot Agent
## Azure Cosmos DB .NET SDK (azure-cosmos-dotnet-v3)

---

## Quick Start Prompt

**In VS Code Copilot Chat:**
```
@ReleaseCopilotAgent start minor
```
```
@ReleaseCopilotAgent start hotfix
```
```
@ReleaseCopilotAgent add missed PRs
```

**In the Copilot CLI (terminal):**
```
I want to start a minor release
```
```
I want to start a hotfix release
```
```
I need to add missed PRs to a release
```

> **Note:** The Copilot CLI loads these instructions via `.github/copilot-instructions.md`. All GitHub MCP tools, PowerShell, and file editing tools are built-in — no additional extensions needed.

**What the agent will do:**
1. Ask whether to run in **Minor Mode**, **Hotfix Mode**, or **Add Missed PRs**
2. Verify environment setup (.NET SDK, `gh` CLI, GenAPI tool)
3. Determine current and target versions from `Directory.Build.props`
4. Generate changelog entries from merged PRs (filtering out `[Internal]`)
5. Prompt for any additional PRs that were missed by automatic discovery
6. Bump version numbers following versioning rules
7. Build the SDK and generate API contract files via GenAPI
8. Create a release PR with API diff in the description
9. Ensure that a full test suite passes, including contract enforcement tests.
10. Guide through post-merge pipeline queuing and NuGet publish
11. Update the `azure-docs-sdk-dotnet` metadata file with the new version (fork/sync, worktree clone, PR)

---

## 0. Environment Setup

### 0.1 Prerequisites

| Tool | Purpose | Required |
|------|---------|----------|
| **.NET SDK** | Build the SDK, produce release DLLs | ✅ Yes |
| **GitHub CLI (`gh`)** | PR creation, branch management | ✅ Yes |
| **GitHub MCP tools** | List PRs, search code, create PRs (built-in for Copilot CLI; requires GitHub MCP Server extension for VS Code) | ✅ Yes |
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
> 3. **Add Missed PRs** — Add PRs that were missed in a previous release changelog

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

#### 2.3.3 Review & Add Missed PRs

After generating the filtered PR list, present it to the user and ask:

> **Here are the PRs that will be included in the changelog. Are there any additional PRs that should be included that weren't automatically discovered?**
> Provide PR numbers separated by commas, or type 'none'.

For each additional PR number provided:

```powershell
# Fetch details for each missed PR
gh pr view <PR_NUMBER> --json number,title,mergedAt
```

Add the missed PRs to the appropriate changelog category based on their title verb (`Adds`, `Fixes`, `Removes`, `Refactors`). If the PR title doesn't follow the standard format, ask the user which category it belongs to.

Re-display the final combined PR list for confirmation before proceeding.

#### 2.3.4 Format Changelog Entries

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

First, determine the GitHub username for branch naming:

```powershell
# Get the GitHub username
gh api user --jq '.login'
```

Then create the feature branch:

```powershell
git checkout -b users/<username>/release-X.Y.Z
```

Where `<username>` is the GitHub username obtained above (e.g., `users/nalutripician/release-3.58.0`).

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

3. **Metadata update — `azure-docs-sdk-dotnet`:**

   Update the Cosmos DB metadata file in `MicrosoftDocs/azure-docs-sdk-dotnet` so the documentation site reflects the new release version. This work is done in a **separate worktree** to avoid disrupting the SDK repository.

   **3a. Get GitHub username:**
   ```powershell
   $ghUser = gh api user --jq '.login'
   ```

   **3b. Check for existing fork and fork/sync:**
   ```powershell
   # Check if user already has a fork
   $forkExists = gh repo view "$ghUser/azure-docs-sdk-dotnet" --json name 2>$null

   if ($forkExists) {
       # Sync existing fork with upstream
       gh repo sync "$ghUser/azure-docs-sdk-dotnet" --branch main
   } else {
       # Fork the repo (no local clone yet)
       gh repo fork MicrosoftDocs/azure-docs-sdk-dotnet --clone=false
   }
   ```

   **3c. Clone fork into a separate worktree and create working branch:**
   ```powershell
   # Clone the fork into a sibling directory (worktree)
   git clone "https://github.com/$ghUser/azure-docs-sdk-dotnet.git" ../azure-docs-sdk-dotnet-worktree
   cd ../azure-docs-sdk-dotnet-worktree

   # Add upstream remote and fetch
   git remote add upstream https://github.com/MicrosoftDocs/azure-docs-sdk-dotnet.git
   git fetch upstream

   # Create working branch from upstream main
   git checkout -b update-cosmos-version upstream/main
   ```

   **3d. Update `metadata/latest/Microsoft.Azure.Cosmos.json`:**

   Update **two fields** in the file to reflect the new GA version (`X.Y.Z`):

   | Field | Old Value | New Value |
   |-------|-----------|-----------|
   | `Version` | `PREV_VERSION` | `X.Y.Z` |
   | `ServiceDirectory` | `https://github.com/Azure/azure-cosmos-dotnet-v3/tree/PREV_VERSION/Microsoft.Azure.Cosmos` | `https://github.com/Azure/azure-cosmos-dotnet-v3/tree/X.Y.Z/Microsoft.Azure.Cosmos` |

   Use the edit tool or `sed`/PowerShell string replacement to make these changes.

   **3e. Commit, push, and create PR:**
   ```powershell
   git add metadata/latest/Microsoft.Azure.Cosmos.json
   git commit -m "Update Microsoft.Azure.Cosmos.json"
   git push origin update-cosmos-version
   ```

   Create the PR targeting the **upstream** repo:
   ```powershell
   gh pr create --repo MicrosoftDocs/azure-docs-sdk-dotnet `
     --base main `
     --head "${ghUser}:update-cosmos-version" `
     --title "Update Microsoft.Azure.Cosmos.json" `
     --body "-  update ``Microsoft.Azure.Cosmos`` minor version to match with the latest release ``X.Y.Z``."
   ```

   > **Reference:** See [PR #2526](https://github.com/MicrosoftDocs/azure-docs-sdk-dotnet/pull/2526) for the expected format.

   **3f. Clean up worktree:**
   ```powershell
   # Return to the SDK repo
   cd $sdkRepoRoot   # or cd ../azure-cosmos-dotnet-v3

   # Remove the worktree clone
   Remove-Item -Recurse -Force ../azure-docs-sdk-dotnet-worktree
   ```

---

## 3. Hotfix Mode

### 3.1 Select PRs for Hotfix

Ask the user:
> **Which PR(s) should be included in this hotfix?**
> Please provide the PR number(s).

Fetch details for each PR using GitHub MCP tools or `gh` CLI.

### 3.2 Identify Release Branch

Ask the user which release branch to hotfix. If they specify a version number (e.g., "3.50"), search for matching branches:

```powershell
# List all release branches (or filter by the user's specified minor version)
git fetch origin
git branch -r --list "origin/releases/3.Y.*" | Sort-Object

# If the user hasn't specified, show recent branches
git branch -r --list "origin/releases/3.*" | Sort-Object | Select-Object -Last 20
```

The user can hotfix **any** release branch, not just the most recent one. Ask for confirmation:

> **Which release branch should be hotfixed?**
> (e.g., `releases/3.57.0`, `releases/3.50.0`, etc.)

### 3.3 Determine Hotfix Version

After the user selects a release branch, check for existing patches on that minor version:

```powershell
# List all existing patches for the selected minor version
git branch -r --list "origin/releases/X.Y.*" | Sort-Object
```

Present the existing versions and **ask the user to confirm the target hotfix version**:

> **Existing versions for 3.Y: 3.Y.0, 3.Y.1**
> **What version should this hotfix target?** (suggested: 3.Y.2)

After the user confirms, update versioning accordingly. The preview version is calculated relative to the **hotfix base version**, not from `master`:

- `ClientOfficialVersion` → user-confirmed version (e.g., `3.55.2`)
- `ClientPreviewVersion` → hotfix minor + 1, patch 0 (e.g., `3.56.0`)
- `ClientPreviewSuffixVersion` → `preview.{hotfix_patch}` (e.g., `preview.2`)

> **⚠️ Important:** Do NOT use the `master` branch preview version. The hotfix preview version is always derived from the hotfix's own minor version.

**Examples:**

| Hotfixing | Existing patches | User target | Preview version | Preview suffix |
|-----------|-----------------|-------------|-----------------|----------------|
| `releases/3.57.0` | 3.57.0 | 3.57.1 | 3.58.0 | preview.1 |
| `releases/3.55.0` | 3.55.0, 3.55.1 | 3.55.2 | 3.56.0 | preview.2 |
| `releases/3.50.0` | 3.50.0 | 3.50.1 | 3.51.0 | preview.1 |

### 3.4 Create Hotfix Branch

Create the hotfix release branch using the user-confirmed version:

```powershell
git fetch origin
git checkout -b releases/X.Y.PATCH origin/releases/X.Y.BASE
git push origin releases/X.Y.PATCH
```

Where `X.Y.PATCH` is the user-confirmed hotfix version (e.g., `3.55.2`) and `X.Y.BASE` is the branch being hotfixed (e.g., the highest existing patch, such as `releases/3.55.1`).

### 3.5 Cherry-Pick PRs

First, determine the GitHub username for branch naming (if not already known):

```powershell
gh api user --jq '.login'
```

For each PR selected by the user:

```powershell
# Get the merge commit SHA for the PR
gh pr view <PR_NUMBER> --json mergeCommit --jq '.mergeCommit.oid'

# Create a working branch for cherry-picks
git checkout -b users/<username>/hotfix-X.Y.Z+1 releases/X.Y.Z+1

# Cherry-pick each commit
git cherry-pick <MERGE_COMMIT_SHA>
```

Where `<username>` is the GitHub username (e.g., `users/nalutripician/hotfix-3.55.2`).

If there are conflicts, notify the user and assist with resolution.

### 3.6 Version Bump on Hotfix Branch

Update `Directory.Build.props` on the hotfix branch with the user-confirmed hotfix version (same versioning rules as Minor Mode, but derived from the hotfix base version — see Section 3.3).

### 3.7 Changelog Update

Add hotfix entries to `changelog.md` on the hotfix branch. Start with the PRs being cherry-picked:

```markdown
### <a name="X.Y.Z+1"/> [X.Y.Z+1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/X.Y.Z+1) - YYYY-M-DD

#### Fixed
- [PR#](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/PR#) PR Title
```

After generating the initial changelog entries, ask the user:

> **Are there any additional PRs that should be included in this hotfix changelog beyond the cherry-picked ones?**
> Provide PR numbers separated by commas, or type 'none'.

For any additional PR numbers, fetch their details and add them to the changelog. Present the final changelog for confirmation.

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

1. Create a working branch from `master` for the sync PR:
   ```powershell
   git checkout master && git pull
   git checkout -b users/<username>/hotfix-X.Y.Z+1-master-sync
   ```
2. Add the following to the branch:
   - The new GA API contract file (`API_X.Y.Z+1.txt`)
   - The new preview API contract file (`API_X.Y+1.0-preview.Z+1.txt`)
   - Changelog entries for the hotfix version
3. Create a PR targeting `master`:
   ```powershell
   gh pr create --base master --title "[Internal] Contracts: Adds hotfix X.Y.Z+1 contract files and changelog" --body "Syncs master with hotfix release X.Y.Z+1 contract files and changelog entries."
   ```
4. This ensures `master` stays in sync with released versions

### 3.10 Create Hotfix PR

Create a PR from the working branch (`users/<username>/hotfix-X.Y.Z+1`) targeting the hotfix release branch:

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

2. **Metadata update — `azure-docs-sdk-dotnet`:**

   Update the Cosmos DB metadata file so the documentation site reflects the hotfix version. This follows the same workflow as Minor Mode §2.8 step 3.

   **2a. Get GitHub username:**
   ```powershell
   $ghUser = gh api user --jq '.login'
   ```

   **2b. Check for existing fork and fork/sync:**
   ```powershell
   $forkExists = gh repo view "$ghUser/azure-docs-sdk-dotnet" --json name 2>$null

   if ($forkExists) {
       gh repo sync "$ghUser/azure-docs-sdk-dotnet" --branch main
   } else {
       gh repo fork MicrosoftDocs/azure-docs-sdk-dotnet --clone=false
   }
   ```

   **2c. Clone fork into a separate worktree and create working branch:**
   ```powershell
   git clone "https://github.com/$ghUser/azure-docs-sdk-dotnet.git" ../azure-docs-sdk-dotnet-worktree
   cd ../azure-docs-sdk-dotnet-worktree
   git remote add upstream https://github.com/MicrosoftDocs/azure-docs-sdk-dotnet.git
   git fetch upstream
   git checkout -b update-cosmos-version upstream/main
   ```

   **2d. Update `metadata/latest/Microsoft.Azure.Cosmos.json`:**

   Update **two fields** to reflect the hotfix version (`X.Y.Z+1`):

   | Field | Old Value | New Value |
   |-------|-----------|-----------|
   | `Version` | `PREV_VERSION` | `X.Y.Z+1` |
   | `ServiceDirectory` | `https://github.com/Azure/azure-cosmos-dotnet-v3/tree/PREV_VERSION/Microsoft.Azure.Cosmos` | `https://github.com/Azure/azure-cosmos-dotnet-v3/tree/X.Y.Z+1/Microsoft.Azure.Cosmos` |

   **2e. Commit, push, and create PR:**
   ```powershell
   git add metadata/latest/Microsoft.Azure.Cosmos.json
   git commit -m "Update Microsoft.Azure.Cosmos.json"
   git push origin update-cosmos-version

   gh pr create --repo MicrosoftDocs/azure-docs-sdk-dotnet `
     --base main `
     --head "${ghUser}:update-cosmos-version" `
     --title "Update Microsoft.Azure.Cosmos.json" `
     --body "-  update ``Microsoft.Azure.Cosmos`` minor version to match with the latest release ``X.Y.Z+1``."
   ```

   **2f. Clean up worktree:**
   ```powershell
   cd $sdkRepoRoot
   Remove-Item -Recurse -Force ../azure-docs-sdk-dotnet-worktree
   ```

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

---

## 6. Add Missed PRs to Existing Release

This mode allows adding PRs that were missed in a previous release's changelog. It can be invoked standalone via:

**In VS Code Copilot Chat:**
```
@ReleaseCopilotAgent add missed PRs
```

**In the Copilot CLI (terminal):**
```
I need to add missed PRs to a release
```

### 6.1 Identify the Target Release

Ask the user:

> **Which release version needs missed PRs added?**
> (e.g., `3.58.0`, `3.55.2`)

### 6.2 Locate the Release PR

Search for the existing release PR:

```powershell
# Search for the release PR by title
gh pr list --repo Azure/azure-cosmos-dotnet-v3 --state all --limit 50 --json number,title,state --jq '.[] | select(.title | test("Release:.*X.Y.Z|Hotfix:.*X.Y.Z"))'
```

Or use the GitHub MCP `search_pull_requests` tool.

Determine whether the PR is **open** or **merged**.

### 6.3 Collect Missed PRs

Ask the user:

> **Which PR(s) should be added to the X.Y.Z changelog?**
> Provide PR numbers separated by commas.

For each PR number, fetch its details:

```powershell
gh pr view <PR_NUMBER> --json number,title,mergedAt
```

Categorize each PR by its title verb (`Adds` → Added, `Fixes` → Fixed, `Removes` → Removed, `Refactors` → Fixed). If the title doesn't match the standard format, ask the user which category it belongs to.

### 6.4 Update the Changelog

#### If the release PR is still open:

1. Check out the existing PR branch:
   ```powershell
   gh pr checkout <PR_NUMBER>
   ```
2. Edit `changelog.md` to add the missed entries under the appropriate version heading and category
3. Commit and push:
   ```powershell
   git add changelog.md
   git commit -m "Release: Adds missed PRs to X.Y.Z changelog"
   git push
   ```

#### If the release PR is already merged:

First, determine the GitHub username:

```powershell
gh api user --jq '.login'
```

Then create a new branch and PR:

1. Create a new branch:
   ```powershell
   git checkout master && git pull
   git checkout -b users/<username>/changelog-fix-X.Y.Z
   ```
2. Edit `changelog.md` to add the missed entries under the existing X.Y.Z version heading
3. If the release branch (`releases/X.Y.Z`) also needs the changelog fix, cherry-pick the commit there too
4. Commit, push, and create a PR:
   ```powershell
   git add changelog.md
   git commit -m "Changelog: Adds missed PRs to X.Y.Z changelog"
   git push origin users/<username>/changelog-fix-X.Y.Z
   gh pr create --base master --title "Changelog: Adds missed PRs to X.Y.Z release notes" --body "## Changelog Fix for X.Y.Z

   ### Added PRs
   - #PR1: Title
   - #PR2: Title

   These PRs were missed during the original X.Y.Z release changelog generation."
   ```

### 6.5 Verify

Present the updated changelog section to the user for final review and confirmation.
