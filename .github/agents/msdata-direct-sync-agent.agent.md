---
name: 'MsdataDirectSyncAgent'
description: 'Orchestrates syncing the msdata/direct branch with the latest v3 main and msdata direct codebase.'
tools:
  - read
  - search
  - edit
  - terminal
---

# Copilot Agent: msdata/direct Branch Sync
## Azure Cosmos DB .NET SDK (azure-cosmos-dotnet-v3)

---

## Quick Start Prompt

**Copy-paste this prompt to start the sync workflow:**

```
Follow the msdata/direct sync agent plan in .github/agents/msdata-direct-sync-agent.agent.md

Sync the msdata/direct branch with the latest v3 main and msdata direct codebase.
```

**What the agent will do:**
1. Verify environment setup (git, .NET SDK, repo clones)
2. Prompt for msdata CosmosDB repo path
3. Create feature branch from `msdata/direct`
4. Merge latest `main` into feature branch
5. Run `msdata_sync.ps1` to sync direct package files
6. **Verify sync completeness** — scan msdata source dirs for files missed by the script and auto-copy them
7. Build and validate
8. Create PR to `msdata/direct` with proper formatting
9. Monitor CI pipeline

---

## 0. Prerequisites

### 0.1 Required Tools

```yaml
required_tools:
  git:
    verify: "git --version"
    minimum: "2.x"
    
  dotnet_sdk:
    verify: "dotnet --version"
    minimum: "8.0"
    
  gh_cli:
    verify: "gh auth status"
    purpose: "PR creation and monitoring"
    setup: "gh auth login --web"
    
  powershell:
    verify: "$PSVersionTable.PSVersion"
    minimum: "5.1"
```

### 0.2 Required Repository Clones

```yaml
required_repos:
  azure_cosmos_dotnet_v3:
    url: "https://github.com/Azure/azure-cosmos-dotnet-v3.git"
    required_branches:
      - main
      - msdata/direct
    verify: "git branch -a | Select-String 'msdata/direct'"
    
  msdata_cosmosdb:
    description: "Internal CosmosDB repository (msdata)"
    note: "User will be prompted for local path at runtime"
    required_branch: "main"
    verify: "Test-Path <user_provided_path>"
```

### 0.3 Verification Checklist

```markdown
## Pre-Sync Verification

- [ ] git installed and configured: `git config user.name`
- [ ] .NET SDK installed: `dotnet --version`
- [ ] gh CLI authenticated: `gh auth status`
- [ ] v3 repo cloned with msdata/direct branch available
- [ ] msdata CosmosDB repo cloned and on main branch
```

---

## 1. Core Principles

> ⚠️ **These principles apply to ALL phases of the sync workflow.**

```yaml
sync_principles:
  rules:
    - "ALWAYS verify each phase before proceeding to the next"
    - "ALWAYS accept incoming main changes when resolving merge conflicts"
    - "NEVER force-push to msdata/direct directly"
    - "ALWAYS create PRs as draft first"
    - "ALWAYS run a clean build before creating the PR"
    - "Prompt user for input when paths or decisions are needed"
    
  evidence_required:
    merge_complete: "Git merge output showing success or resolved conflicts"
    sync_complete: "msdata_sync.ps1 output showing all files copied"
    build_passed: "dotnet build output showing 'Build succeeded. 0 Error(s)'"
    pr_created: "PR URL from gh pr create"
```

---

## 2. Workflow Phases

### Phase 1: Environment Setup & Validation

**Goal:** Ensure all prerequisites are met and gather required user input.

```yaml
phase_1_steps:
  step_1_verify_tools:
    commands:
      - "git --version"
      - "dotnet --version"
      - "gh auth status"
    action: "If any tool is missing, guide user through installation"
    
  step_2_verify_v3_repo:
    commands:
      - "git remote -v"
      - "git fetch origin --quiet"
      - "git branch -a | Select-String 'msdata/direct'"
    verify: "origin points to Azure/azure-cosmos-dotnet-v3 and msdata/direct exists"
    
  step_3_prompt_msdata_path:
    action: "Ask user for local path to msdata CosmosDB repo"
    question: "What is the local path to your msdata CosmosDB repository clone?"
    examples:
      - "Q:\\CosmosDB"
      - "C:\\repos\\CosmosDB"
      - "E:\\src\\CosmosDB"
    validation: "Test-Path <user_path> && Test-Path <user_path>\\sdk"
    
  step_4_verify_msdata_repo:
    commands:
      - "cd <msdata_path> && git status"
      - "cd <msdata_path> && git branch --show-current"
    action: "Ensure msdata repo is on main and up to date"
    fix: "cd <msdata_path> && git checkout main && git pull"
```

### Phase 2: Branch Preparation

**Goal:** Create a feature branch from `msdata/direct` and merge latest `main`.

```yaml
phase_2_steps:
  step_1_update_branches:
    commands:
      - "git fetch origin main:main --quiet"
      - "git fetch origin msdata/direct --quiet"
    verify: "Both branches are up to date with remote"
    
  step_2_create_feature_branch:
    description: "Create feature branch from msdata/direct"
    naming_convention: "users/<username>/update_msdata_direct_<mm_dd_yyyy>"
    commands:
      - "git checkout msdata/direct"
      - 'git checkout -b users/<username>/update_msdata_direct_<date>'
    example: "git checkout -b users/nalutripician/update_msdata_direct_03_03_2026"
    get_username: "git config user.name or gh api user --jq '.login'"
    get_date: "(Get-Date).ToString('MM_dd_yyyy')"
    
  step_3_merge_master:
    commands:
      - "git merge main"
    expect: "Merge conflicts are likely"
    
  step_4_resolve_conflicts:
    strategy: "Accept incoming main changes for most conflicts"
    commands:
      - "git checkout --theirs <conflicted_files>"
      - "git add <resolved_files>"
      - "git merge --continue"
    manual_review_needed:
      - "Files in Microsoft.Azure.Cosmos/src/direct/ — these may need careful review"
      - "Project files (.csproj) — ensure both sets of changes are preserved"
      - "Directory.Build.props — version numbers need careful handling"
    notes:
      - "If conflicts are too complex, ask user for guidance"
      - "Document all conflict resolutions for PR description"
```

### Phase 3: msdata File Sync

**Goal:** Copy latest Microsoft.Azure.Cosmos.Direct files from msdata repo.

```yaml
phase_3_steps:
  step_1_locate_sync_script:
    path: "Microsoft.Azure.Cosmos/src/direct/msdata_sync.ps1"
    verify: "Test-Path Microsoft.Azure.Cosmos/src/direct/msdata_sync.ps1"
    fallback: "If script doesn't exist after merge, check msdata/direct branch directly"
    
  step_2_configure_sync_script:
    description: "Update msdata_sync.ps1 with user-provided msdata repo path"
    action: "Replace the $baseDir value with the user-provided path"
    pattern: '$baseDir    = "<src_directory>\\CosmosDB"'
    replacement: '$baseDir    = "<user_provided_path>"'
    important: "Do NOT commit this path change — revert after sync"
    
  step_3_run_sync_script:
    commands:
      - "cd Microsoft.Azure.Cosmos/src/direct"
      - ".\\msdata_sync.ps1"
    expect: "Console output showing files being copied"
    success_indicator: "Script completes without Write-Error lines"
    
  step_4_verify_and_copy_missing_files:
    description: >
      IMPORTANT: msdata_sync.ps1 only copies files that already exist locally.
      New files added in the msdata repo will be silently missed. This step
      performs a reverse scan of all msdata source directories and auto-copies
      any .cs files that are not yet present in the v3 direct/ folder.
    msdata_source_directories:
      - "\\Product\\SDK\\.net\\Microsoft.Azure.Cosmos.Direct\\src\\"
      - "\\Product\\Microsoft.Azure.Documents\\Common\\SharedFiles\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\Routing\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\Rntbd2\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\Rntbd\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\Rntbd\\rntbdtokens\\"
      - "\\Product\\SDK\\.net\\Microsoft.Azure.Documents.Client\\LegacyXPlatform\\"
      - "\\Product\\Cosmos\\Core\\Core.Trace\\"
      - "\\Product\\Cosmos\\Core\\Core\\Utilities\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\Collections\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\Query\\"
      - "\\Product\\Microsoft.Azure.Documents\\SharedFiles\\Management\\"
    exclude_files:
      - "AssemblyKeys.cs"
      - "BaseTransportClient.cs"
      - "CpuReaderBase.cs"
      - "LinuxCpuReader.cs"
      - "MemoryLoad.cs"
      - "MemoryLoadHistory.cs"
      - "UnsupportedCpuReader.cs"
      - "WindowsCpuReader.cs"
      - "msdata_sync.ps1"
    procedure:
      - step: "For each msdata source directory, list all .cs files"
        command: 'Get-ChildItem "<msdata_path>\\<source_dir>" -Filter "*.cs" -File -ErrorAction SilentlyContinue'
      - step: "For each .cs file found, check if it already exists in Microsoft.Azure.Cosmos/src/direct/"
        note: "Files from the Rntbd2 source dir go into the direct/rntbd2/ subdirectory"
      - step: "If the file does NOT exist locally and is NOT in the exclude list, copy it"
        command: 'Copy-Item "<msdata_path>\\<source_dir>\\<file>" -Destination "Microsoft.Azure.Cosmos/src/direct/" -Force'
      - step: "Log every file that was auto-copied so it can be included in the PR description"
      - step: "After all directories are scanned, report a summary"
    success_criteria: "No new files remain uncopied from any msdata source directory"
    notes:
      - "The Rntbd2 directory is special — its files go to direct/rntbd2/, not direct/"
      - "TransportClient.cs, RMResources.Designer.cs, and RMResources.resx are handled separately by the sync script and should be skipped in this check"
      - "If any files are copied, re-run msdata_sync.ps1 afterward to ensure consistency"
      - "If using the helper script, this verification runs automatically as part of the Sync phase"
    
  step_5_revert_script_path:
    description: "Revert the $baseDir change in msdata_sync.ps1"
    commands:
      - "git checkout -- Microsoft.Azure.Cosmos/src/direct/msdata_sync.ps1"
    verify: "git diff Microsoft.Azure.Cosmos/src/direct/msdata_sync.ps1 shows no changes"
```

### Phase 4: Build Validation

**Goal:** Verify the merged and synced code builds successfully.

```yaml
phase_4_steps:
  step_1_clean_build:
    command: "dotnet build Microsoft.Azure.Cosmos.sln -c Release"
    expected: "Build succeeded. 0 Error(s)"
    timeout: "5-10 minutes"
    
  step_2_fix_build_errors:
    description: "If build fails, investigate and fix errors"
    common_issues:
      missing_files:
        symptom: "CS file not found or type not defined"
        fix: "Check if file was missed during sync, copy from msdata repo"
      namespace_conflicts:
        symptom: "Ambiguous reference or namespace conflict"
        fix: "Check using statements, resolve with fully qualified names"
      api_changes:
        symptom: "Method signature mismatch"
        fix: "Update to match latest API from main or direct"
    action: "Fix errors, rebuild, repeat until clean"
    
  step_3_verify_build:
    command: "dotnet build Microsoft.Azure.Cosmos.sln -c Release"
    required: "Build MUST succeed before proceeding"
    evidence: "Capture build output as proof"
```

### Phase 5: PR Creation & Submission

**Goal:** Push the feature branch and create a properly formatted PR.

```yaml
phase_5_steps:
  step_1_stage_and_commit:
    commands:
      - "git add -A"
      - 'git commit -m "[Internal] Direct package: Adds msdata/direct update from main"'
    verify: "git status shows clean working tree"
    
  step_2_push_branch:
    command: "git push origin users/<username>/update_msdata_direct_<date>"
    verify: "Push succeeds without errors"
    
  step_3_create_pr:
    command: |
      gh pr create --draft \
        --base msdata/direct \
        --title "[Internal] Direct package: Adds msdata/direct update from main" \
        --body "<detailed_description>"
    pr_description_template: |
      # Pull Request Template

      ## Description

      Syncs the `msdata/direct` branch with:
      - Latest `main` branch (v3 SDK changes)
      - Latest `Microsoft.Azure.Cosmos.Direct` files from msdata CosmosDB repo

      ### Changes Include
      - Merged latest `main` branch into `msdata/direct`
      - Updated `Microsoft.Azure.Cosmos.Direct` files via `msdata_sync.ps1`
      - Resolved merge conflicts (accepted main changes)
      - Build validated: `dotnet build` passes

      ## Type of change

      - [x] New feature (non-breaking change which adds functionality)

      ## Validation

      - [x] Local build passes (`dotnet build Microsoft.Azure.Cosmos.sln -c Release`)
    
    reviewers:
      - "kirillg"
      - "khdang"
      - "adityasa"
      - "sboshra"
      - "FabianMeiswinkel"
      - "leminh98"
      - "neildsh"
      
  step_4_monitor_ci:
    description: "Monitor the azure-pipelines-msdata-direct.yml pipeline"
    commands:
      - "gh pr checks <pr_number>"
    action: "Wait for CI to complete, investigate failures if any"
    pipeline: "azure-pipelines-msdata-direct.yml"
    
  step_5_mark_ready:
    condition: "All CI checks pass"
    command: "gh pr ready <pr_number>"
    action: "Convert from draft to ready for review"
```

---

## 3. Helper Script

A companion PowerShell script is available at `tools/msdata-direct-sync-helper.ps1` to automate the mechanical parts of this workflow.

```yaml
helper_script:
  path: "tools/msdata-direct-sync-helper.ps1"
  
  usage: |
    # Full automated workflow
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB"
    
    # Individual phases
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB" -Phase Setup
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB" -Phase Branch
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB" -Phase Sync
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB" -Phase Build
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB" -Phase PR
    
  parameters:
    MsdataRepoPath: "Path to local msdata CosmosDB repo clone (required)"
    Phase: "Run a specific phase only (optional; default runs all)"
    GitHubUsername: "GitHub username for branch naming (optional; auto-detected)"
    SkipBuild: "Skip build validation phase (optional; not recommended)"
```

---

## 4. Troubleshooting

### Common Merge Conflicts

```yaml
merge_conflicts:
  directory_build_props:
    description: "Version number conflicts between main and msdata/direct"
    resolution: "Accept main version numbers, they are the source of truth"
    
  csproj_files:
    description: "Project file conflicts from both sides adding references"
    resolution: "Manually merge to include all references from both sides"
    
  direct_files:
    description: "Files in src/direct/ modified on both branches"
    resolution: "Accept msdata/direct version — these will be overwritten by msdata_sync.ps1"
```

### Build Failures After Sync

```yaml
build_failures:
  missing_type_or_namespace:
    symptom: "error CS0246: The type or namespace name 'X' could not be found"
    causes:
      - "File not copied by msdata_sync.ps1"
      - "New file added in msdata but not in sync script"
    fix: "Locate file in msdata repo, copy manually to src/direct/"
    
  duplicate_definitions:
    symptom: "error CS0111: Type already defines a member"
    causes:
      - "Same file exists in both main and direct"
    fix: "Remove the duplicate, keep the direct version"
    
  api_incompatibility:
    symptom: "error CS1501: No overload for method"
    causes:
      - "Direct package API changed"
    fix: "Update calling code to match new API signature"
```

### msdata_sync.ps1 Errors

```yaml
sync_script_errors:
  file_not_found:
    symptom: "Write-Error: <filename> False"
    cause: "File exists in sync script list but not in msdata repo"
    fix: "File may have been renamed or removed — check msdata repo history"
    
  permission_denied:
    symptom: "Access to the path is denied"
    cause: "File is read-only or locked"
    fix: "Close any editors, remove read-only flag: attrib -r <file>"
    
  path_not_found:
    symptom: "Cannot find path 'Q:\\CosmosDB\\...'"
    cause: "$baseDir not set correctly"
    fix: "Verify msdata repo path and update $baseDir in script"
```

---

## 5. Sample Pull Requests

Reference these PRs for expected format and scope:

| PR | Title | Date | Scope |
|----|-------|------|-------|
| [#5612](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5612) | [Internal] Direct package: Adds msdata/direct update from main | Feb 2026 | 545 files, 76K additions |
| [#3776](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3776) | [Internal] Msdata/Direct: Refactors msdata/direct with v3 main and Direct v3.30.4 | Mar 2023 | 155 files, 13K additions |
| [#3726](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3726) | [Internal] Msdata/Direct: Refactors msdata branch with latest v3 and direct release | Feb 2023 | 361 files, 27K additions |

---

## 6. CI Pipeline

The `azure-pipelines-msdata-direct.yml` pipeline runs automatically on PRs targeting `msdata/direct*` branches. It includes:

```yaml
ci_pipeline:
  trigger: "PR to msdata/direct*"
  jobs:
    - "Static analysis tools"
    - "CTL build"
    - "Samples build"
    - "msdata test suite (Release)"
    - "Internal build"
    - "Preview msdata build"
    - "Thin client build"
  variables:
    test_filter: '--filter "TestCategory!=Flaky & TestCategory!=Quarantine & TestCategory!=Functional"'
    vm_image: "windows-latest"
```
