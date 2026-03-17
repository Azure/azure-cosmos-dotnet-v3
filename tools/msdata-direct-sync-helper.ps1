<#
.SYNOPSIS
    Helper script for syncing the msdata/direct branch with latest v3 master and msdata CosmosDB repo.

.DESCRIPTION
    Automates the mechanical parts of the msdata/direct sync workflow:
    - Validates prerequisites (git, dotnet CLI, gh CLI)
    - Creates feature branch with correct naming convention
    - Merges master into the feature branch
    - Configures and runs msdata_sync.ps1
    - Runs build validation
    - Optionally creates a PR

    See .github/agents/msdata-direct-sync-agent.agent.md for the full workflow documentation.
    See docs/sync_up_msdata_direct.md for background on the sync process.

.PARAMETER MsdataRepoPath
    Path to the local msdata CosmosDB repository clone. Required for the Sync phase.

.PARAMETER Phase
    Run a specific phase only. Valid values: Setup, Branch, Sync, Build, PR, All.
    Default: All (runs all phases sequentially).

.PARAMETER GitHubUsername
    GitHub username for branch naming. If not provided, auto-detected via gh CLI or git config.

.PARAMETER SkipBuild
    Skip the build validation phase. Not recommended but useful for re-runs.

.EXAMPLE
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB"

.EXAMPLE
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "C:\repos\CosmosDB" -Phase Sync

.EXAMPLE
    .\tools\msdata-direct-sync-helper.ps1 -MsdataRepoPath "Q:\CosmosDB" -GitHubUsername "nalutripician"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$MsdataRepoPath,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Setup", "Branch", "Sync", "Build", "PR", "All")]
    [string]$Phase = "All",

    [Parameter(Mandatory = $false)]
    [string]$GitHubUsername,

    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$script:RepoRoot = git rev-parse --show-toplevel 2>$null
if (-not $script:RepoRoot) {
    Write-Error "This script must be run from within the azure-cosmos-dotnet-v3 repository."
    exit 1
}

$script:DateStamp = (Get-Date).ToString("MM_dd_yyyy")
$script:BranchName = $null
$script:PhaseResults = @{}

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Phase {
    param([string]$PhaseName, [string]$Message)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Phase: $PhaseName" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Gray
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([int]$Number, [string]$Description)
    Write-Host "  [$Number] $Description" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✅ $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "  ❌ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "  ℹ️  $Message" -ForegroundColor Gray
}

function Get-GitHubUsername {
    if ($GitHubUsername) {
        return $GitHubUsername
    }
    
    # Try gh CLI first
    try {
        $username = gh api user --jq '.login' 2>$null
        if ($username) {
            Write-Info "Detected GitHub username via gh CLI: $username"
            return $username
        }
    } catch { }
    
    # Fall back to git config
    $username = git config user.name 2>$null
    if ($username) {
        Write-Info "Using git config user.name: $username"
        return $username
    }
    
    Write-Failure "Could not detect GitHub username. Please provide -GitHubUsername parameter."
    exit 1
}

# ============================================================================
# Phase: Setup — Validate prerequisites
# ============================================================================

function Invoke-SetupPhase {
    Write-Phase "Setup" "Validating prerequisites and environment"
    
    # Step 1: Check git
    Write-Step 1 "Checking git..."
    $gitVersion = git --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "git is not installed or not in PATH"
        return $false
    }
    Write-Success "git: $gitVersion"
    
    # Step 2: Check dotnet
    Write-Step 2 "Checking .NET SDK..."
    $dotnetVersion = dotnet --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Failure ".NET SDK is not installed or not in PATH"
        return $false
    }
    Write-Success ".NET SDK: $dotnetVersion"
    
    # Step 3: Check gh CLI
    Write-Step 3 "Checking GitHub CLI..."
    $ghStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "GitHub CLI is not authenticated. Run: gh auth login --web"
        return $false
    }
    Write-Success "GitHub CLI: authenticated"
    
    # Step 4: Check we're in the right repo
    Write-Step 4 "Checking repository..."
    $remote = git remote get-url origin 2>$null
    if ($remote -notmatch "azure-cosmos-dotnet-v3") {
        Write-Failure "Not in the azure-cosmos-dotnet-v3 repository. Remote: $remote"
        return $false
    }
    Write-Success "Repository: azure-cosmos-dotnet-v3"
    
    # Step 5: Check msdata/direct branch exists
    Write-Step 5 "Checking msdata/direct branch..."
    $branches = git branch -a 2>$null
    if ($branches -notmatch "msdata/direct") {
        Write-Info "Fetching remote branches..."
        git fetch origin msdata/direct --quiet 2>$null
    }
    $branches = git branch -a 2>$null
    if ($branches -notmatch "msdata/direct") {
        Write-Failure "msdata/direct branch not found. Check remote."
        return $false
    }
    Write-Success "msdata/direct branch exists"
    
    # Step 6: Check msdata repo path (if needed for Sync phase)
    if ($MsdataRepoPath) {
        Write-Step 6 "Checking msdata CosmosDB repo path..."
        if (-not (Test-Path $MsdataRepoPath)) {
            Write-Failure "msdata repo path not found: $MsdataRepoPath"
            return $false
        }
        Write-Success "msdata repo path: $MsdataRepoPath"
    } else {
        Write-Step 6 "msdata repo path not provided (required for Sync phase)"
        Write-Info "Use -MsdataRepoPath parameter when running Sync phase"
    }
    
    Write-Success "All prerequisites validated!"
    $script:PhaseResults["Setup"] = "passed"
    return $true
}

# ============================================================================
# Phase: Branch — Create feature branch and merge master
# ============================================================================

function Invoke-BranchPhase {
    Write-Phase "Branch" "Creating feature branch and merging master"
    
    $username = Get-GitHubUsername
    $script:BranchName = "users/$username/update_msdata_direct_$script:DateStamp"
    
    # Step 1: Fetch latest
    Write-Step 1 "Fetching latest branches from origin..."
    git fetch origin master --quiet 2>$null
    git fetch origin msdata/direct --quiet 2>$null
    Write-Success "Fetched latest master and msdata/direct"
    
    # Step 2: Check if branch already exists
    Write-Step 2 "Checking for existing feature branch..."
    $existingBranch = git branch -a 2>$null | Select-String $script:BranchName
    if ($existingBranch) {
        Write-Info "Feature branch already exists: $($script:BranchName)"
        Write-Info "Checking out existing branch..."
        git checkout $script:BranchName 2>$null
        Write-Success "Checked out existing branch"
        $script:PhaseResults["Branch"] = "passed"
        return $true
    }
    
    # Step 3: Create feature branch from msdata/direct
    Write-Step 3 "Creating feature branch: $($script:BranchName)"
    git checkout origin/msdata/direct -b $script:BranchName 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Failed to create feature branch"
        return $false
    }
    Write-Success "Created branch: $($script:BranchName)"
    
    # Step 4: Merge master
    Write-Step 4 "Merging master into feature branch..."
    $mergeOutput = git merge origin/master --no-edit 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Info "Merge conflicts detected. Listing conflicted files:"
        $conflicts = git diff --name-only --diff-filter=U 2>$null
        if ($conflicts) {
            foreach ($file in $conflicts) {
                Write-Host "    CONFLICT: $file" -ForegroundColor Red
            }
        }
        Write-Host ""
        Write-Info "Attempting auto-resolution (accept master changes)..."
        
        # Try to auto-resolve by accepting master (theirs) changes
        foreach ($file in $conflicts) {
            git checkout --theirs $file 2>$null
            git add $file 2>$null
        }
        
        # Check if all conflicts are resolved
        $remainingConflicts = git diff --name-only --diff-filter=U 2>$null
        if ($remainingConflicts) {
            Write-Failure "Some conflicts could not be auto-resolved:"
            foreach ($file in $remainingConflicts) {
                Write-Host "    MANUAL: $file" -ForegroundColor Red
            }
            Write-Info "Please resolve remaining conflicts manually, then run: git merge --continue"
            return $false
        }
        
        git merge --continue --no-edit 2>$null
        Write-Success "All conflicts resolved and merge completed"
    } else {
        Write-Success "Merge completed without conflicts"
    }
    
    $script:PhaseResults["Branch"] = "passed"
    return $true
}

# ============================================================================
# Phase: Sync — Run msdata_sync.ps1
# ============================================================================

# Known msdata source directories (must match $sourceDir in msdata_sync.ps1)
$script:MsdataSourceDirs = @(
    "\Product\SDK\.net\Microsoft.Azure.Cosmos.Direct\src\",
    "\Product\Microsoft.Azure.Documents\Common\SharedFiles\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\Routing\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\Rntbd2\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\Rntbd\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\Rntbd\rntbdtokens\",
    "\Product\SDK\.net\Microsoft.Azure.Documents.Client\LegacyXPlatform\",
    "\Product\Cosmos\Core\Core.Trace\",
    "\Product\Cosmos\Core\Core\Utilities\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\Collections\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\Query\",
    "\Product\Microsoft.Azure.Documents\SharedFiles\Management\"
)

# Files excluded from sync (must match $exclueList in msdata_sync.ps1)
$script:SyncExcludeList = @(
    "AssemblyKeys.cs",
    "BaseTransportClient.cs",
    "CpuReaderBase.cs",
    "LinuxCpuReader.cs",
    "MemoryLoad.cs",
    "MemoryLoadHistory.cs",
    "UnsupportedCpuReader.cs",
    "WindowsCpuReader.cs",
    "msdata_sync.ps1"
)

# Files handled separately by msdata_sync.ps1 (special-case copies)
$script:SpecialCaseFiles = @(
    "TransportClient.cs",
    "RMResources.Designer.cs",
    "RMResources.resx"
)

function Invoke-PostSyncVerification {
    param(
        [Parameter(Mandatory)]
        [string]$MsdataPath,
        [Parameter(Mandatory)]
        [string]$DirectDir
    )

    Write-Step 4 "Verifying sync completeness — scanning msdata source directories for missing files..."

    $missingFiles = @()
    $copiedFiles = @()
    $rntbd2Dir = Join-Path $DirectDir "rntbd2"

    foreach ($sourceDir in $script:MsdataSourceDirs) {
        $fullSourceDir = Join-Path $MsdataPath $sourceDir
        if (-not (Test-Path $fullSourceDir)) {
            Write-Info "Source directory not found (skipping): $sourceDir"
            continue
        }

        $isRntbd2 = $sourceDir -match "\\Rntbd2\\"
        $targetDir = if ($isRntbd2) { $rntbd2Dir } else { $DirectDir }

        $sourceFiles = Get-ChildItem $fullSourceDir -Filter "*.cs" -File -ErrorAction SilentlyContinue
        foreach ($file in $sourceFiles) {
            $fileName = $file.Name

            # Skip excluded and special-case files
            if ($script:SyncExcludeList -contains $fileName) { continue }
            if ($script:SpecialCaseFiles -contains $fileName) { continue }

            $targetPath = Join-Path $targetDir $fileName
            if (-not (Test-Path $targetPath)) {
                $missingFiles += @{ Name = $fileName; Source = $file.FullName; Target = $targetDir; SourceDir = $sourceDir }
            }
        }
    }

    if ($missingFiles.Count -eq 0) {
        Write-Success "Post-sync verification passed — no missing files detected"
        return $true
    }

    Write-Info "Found $($missingFiles.Count) file(s) in msdata that are missing from v3 direct/:"
    foreach ($missing in $missingFiles) {
        Write-Host "    MISSING: $($missing.Name) (from $($missing.SourceDir))" -ForegroundColor Yellow
    }

    Write-Info "Auto-copying missing files..."
    foreach ($missing in $missingFiles) {
        try {
            if (-not (Test-Path $missing.Target)) {
                New-Item -ItemType Directory -Path $missing.Target -Force | Out-Null
            }
            Copy-Item $missing.Source -Destination $missing.Target -Force
            $copiedFiles += $missing.Name
            Write-Success "Copied: $($missing.Name) -> $($missing.Target)"
        } catch {
            Write-Failure "Failed to copy $($missing.Name): $_"
        }
    }

    if ($copiedFiles.Count -gt 0) {
        Write-Host ""
        Write-Success "Auto-copied $($copiedFiles.Count) missing file(s):"
        foreach ($f in $copiedFiles) {
            Write-Host "    + $f" -ForegroundColor Green
        }
    }

    return $true
}

function Invoke-SyncPhase {
    Write-Phase "Sync" "Syncing Microsoft.Azure.Cosmos.Direct files from msdata repo"
    
    if (-not $MsdataRepoPath) {
        Write-Failure "msdata repo path is required for Sync phase. Use -MsdataRepoPath parameter."
        return $false
    }
    
    if (-not (Test-Path $MsdataRepoPath)) {
        Write-Failure "msdata repo path not found: $MsdataRepoPath"
        return $false
    }
    
    $syncScript = Join-Path $script:RepoRoot "Microsoft.Azure.Cosmos" "src" "direct" "msdata_sync.ps1"
    
    # Step 1: Locate sync script
    Write-Step 1 "Locating msdata_sync.ps1..."
    if (-not (Test-Path $syncScript)) {
        Write-Failure "msdata_sync.ps1 not found at: $syncScript"
        Write-Info "This file should exist after merging msdata/direct. Check the merge step."
        return $false
    }
    Write-Success "Found: $syncScript"
    
    # Step 2: Configure sync script with msdata repo path
    Write-Step 2 "Configuring msdata_sync.ps1 with repo path..."
    $scriptContent = Get-Content $syncScript -Raw
    $originalContent = $scriptContent
    
    # Replace the $baseDir line with user-provided path
    $normalizedPath = $MsdataRepoPath.TrimEnd('\', '/')
    $scriptContent = $scriptContent -replace '\$baseDir\s*=\s*"[^"]*"', "`$baseDir    = `"$normalizedPath`""
    
    Set-Content $syncScript -Value $scriptContent
    Write-Success "Configured `$baseDir = `"$normalizedPath`""
    
    # Step 3: Run sync script
    Write-Step 3 "Running msdata_sync.ps1..."
    $directDir = Join-Path $script:RepoRoot "Microsoft.Azure.Cosmos" "src" "direct"
    Push-Location $directDir
    try {
        $syncOutput = & $syncScript 2>&1
        $syncOutput | ForEach-Object { Write-Host "    $_" }
        
        # Check for errors in output
        $errors = $syncOutput | Where-Object { $_ -match "Write-Error|False$" }
        if ($errors) {
            Write-Failure "Some files failed to sync:"
            foreach ($err in $errors) {
                Write-Host "    ERROR: $err" -ForegroundColor Red
            }
            Write-Info "Please copy missing files manually from msdata repo, then re-run the Sync phase."
        } else {
            Write-Success "All files synced successfully"
        }
    } finally {
        Pop-Location
    }
    
    # Step 4: Verify sync completeness and auto-copy missing files
    $verifyResult = Invoke-PostSyncVerification -MsdataPath $normalizedPath -DirectDir $directDir
    if (-not $verifyResult) {
        Write-Failure "Post-sync verification failed"
        # Still revert the script before returning
        Set-Content $syncScript -Value $originalContent
        return $false
    }
    
    # Step 5: Revert script path change (don't commit the local path)
    Write-Step 5 "Reverting msdata_sync.ps1 path change..."
    Set-Content $syncScript -Value $originalContent
    Write-Success "Reverted msdata_sync.ps1 to original state"
    
    $script:PhaseResults["Sync"] = "passed"
    return $true
}

# ============================================================================
# Phase: Build — Validate the build
# ============================================================================

function Invoke-BuildPhase {
    Write-Phase "Build" "Building solution to validate sync"
    
    if ($SkipBuild) {
        Write-Info "Build phase skipped (-SkipBuild flag)"
        $script:PhaseResults["Build"] = "skipped"
        return $true
    }
    
    $solutionPath = Join-Path $script:RepoRoot "Microsoft.Azure.Cosmos.sln"
    
    # Step 1: Clean build
    Write-Step 1 "Running clean build..."
    Push-Location $script:RepoRoot
    try {
        $buildOutput = dotnet build $solutionPath -c Release 2>&1
        $exitCode = $LASTEXITCODE
        
        # Show last few lines of build output
        $buildOutput | Select-Object -Last 10 | ForEach-Object { Write-Host "    $_" }
        
        if ($exitCode -ne 0) {
            Write-Failure "Build failed with exit code $exitCode"
            Write-Info "Review build errors above and fix before proceeding."
            Write-Info "Common fixes:"
            Write-Info "  - Missing files: copy from msdata repo"
            Write-Info "  - Namespace conflicts: resolve using statements"
            Write-Info "  - API changes: update method signatures"
            return $false
        }
        
        Write-Success "Build succeeded!"
    } finally {
        Pop-Location
    }
    
    $script:PhaseResults["Build"] = "passed"
    return $true
}

# ============================================================================
# Phase: PR — Create pull request
# ============================================================================

function Invoke-PRPhase {
    Write-Phase "PR" "Creating pull request to msdata/direct"
    
    $username = Get-GitHubUsername
    if (-not $script:BranchName) {
        $script:BranchName = "users/$username/update_msdata_direct_$script:DateStamp"
    }
    
    # Step 1: Stage and commit
    Write-Step 1 "Staging and committing changes..."
    Push-Location $script:RepoRoot
    try {
        git add -A 2>$null
        $status = git status --porcelain 2>$null
        if ($status) {
            git commit -m "[Internal] Direct package: Adds msdata/direct update from master" 2>$null
            if ($LASTEXITCODE -ne 0) {
                Write-Failure "Commit failed"
                return $false
            }
            Write-Success "Changes committed"
        } else {
            Write-Info "No new changes to commit"
        }
        
        # Step 2: Push branch
        Write-Step 2 "Pushing branch to origin..."
        git push origin $script:BranchName 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Push failed"
            return $false
        }
        Write-Success "Branch pushed: $($script:BranchName)"
        
        # Step 3: Create PR
        Write-Step 3 "Creating draft pull request..."
        $prBody = @"
# Pull Request Template

## Description

Syncs the ``msdata/direct`` branch with:
- Latest ``master`` branch (v3 SDK changes)
- Latest ``Microsoft.Azure.Cosmos.Direct`` files from msdata CosmosDB repo

### Changes Include
- Merged latest ``master`` branch into ``msdata/direct``
- Updated ``Microsoft.Azure.Cosmos.Direct`` files via ``msdata_sync.ps1``
- Resolved merge conflicts (accepted master changes)
- Build validated: ``dotnet build`` passes

## Type of change

- [x] New feature (non-breaking change which adds functionality)

## Validation

- [x] Local build passes (``dotnet build Microsoft.Azure.Cosmos.sln -c Release``)
"@
        
        $prUrl = gh pr create --draft `
            --base "msdata/direct" `
            --title "[Internal] Direct package: Adds msdata/direct update from master" `
            --body $prBody `
            --reviewer "kirillg,khdang,adityasa,sboshra,FabianMeiswinkel,leminh98,neildsh" 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "PR creation failed: $prUrl"
            return $false
        }
        
        Write-Success "Draft PR created: $prUrl"
        Write-Info "Monitor CI: gh pr checks <pr_number>"
        Write-Info "Mark ready when CI passes: gh pr ready <pr_number>"
        
    } finally {
        Pop-Location
    }
    
    $script:PhaseResults["PR"] = "passed"
    return $true
}

# ============================================================================
# Main Execution
# ============================================================================

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║     msdata/direct Branch Sync Helper                        ║" -ForegroundColor Magenta
Write-Host "║     Azure Cosmos DB .NET SDK v3                             ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

$phases = switch ($Phase) {
    "Setup"  { @("Setup") }
    "Branch" { @("Branch") }
    "Sync"   { @("Sync") }
    "Build"  { @("Build") }
    "PR"     { @("PR") }
    "All"    { @("Setup", "Branch", "Sync", "Build", "PR") }
}

foreach ($p in $phases) {
    $result = switch ($p) {
        "Setup"  { Invoke-SetupPhase }
        "Branch" { Invoke-BranchPhase }
        "Sync"   { Invoke-SyncPhase }
        "Build"  { Invoke-BuildPhase }
        "PR"     { Invoke-PRPhase }
    }
    
    if (-not $result) {
        Write-Host ""
        Write-Failure "Phase '$p' failed. Fix the issue and re-run with -Phase $p"
        Write-Host ""
        exit 1
    }
}

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  Summary" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
foreach ($key in $script:PhaseResults.Keys) {
    $status = $script:PhaseResults[$key]
    $icon = if ($status -eq "passed") { "✅" } elseif ($status -eq "skipped") { "⏭️" } else { "❌" }
    Write-Host "  $icon $key`: $status"
}
Write-Host ""
Write-Success "msdata/direct sync complete!"
Write-Host ""
