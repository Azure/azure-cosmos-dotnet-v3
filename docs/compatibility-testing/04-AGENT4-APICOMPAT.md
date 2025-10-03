# Agent 4: API Compatibility Tooling

## Overview

**Goal**: Integrate Microsoft.DotNet.ApiCompat.Tool to catch API breaking changes before runtime tests.

**Estimated Time**: 2-3 hours

**Dependencies**: Agent 1 (Infrastructure)

**Deliverables**:

1. ApiCompat tool integration
2. Baseline API snapshot
3. CI pipeline stage for API validation
4. Documentation for API changes

---

## Task 1: Install and Configure ApiCompat Tool

### File: `tools/apicompat-check.ps1`

```powershell
<#
.SYNOPSIS
    Checks API compatibility between current build and baseline version
.PARAMETER BaselineVersion
    Version to compare against (default: last published)
.PARAMETER Strict
    Enable strict mode (fail on any change)
#>

param(
    [string]$BaselineVersion = "1.0.0-preview08",
    [switch]$Strict
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 API Compatibility Check" -ForegroundColor Cyan
Write-Host "Baseline Version: $BaselineVersion" -ForegroundColor Yellow
Write-Host ""

# Ensure ApiCompat tool is installed
$toolName = "Microsoft.DotNet.ApiCompat.Tool"
$toolInstalled = dotnet tool list --global | Select-String $toolName

if (-not $toolInstalled) {
    Write-Host "Installing $toolName..." -ForegroundColor Yellow
    dotnet tool install --global $toolName
}

# Build current version
Write-Host "Building current version..." -ForegroundColor Yellow
dotnet build Microsoft.Azure.Cosmos.Encryption.Custom\src\Microsoft.Azure.Cosmos.Encryption.Custom.csproj `
    --configuration Release `
    --no-restore

# Download baseline package
$packagesDir = ".\packages-temp"
$baselineDir = "$packagesDir\Microsoft.Azure.Cosmos.Encryption.Custom.$BaselineVersion"

if (Test-Path $packagesDir) {
    Remove-Item $packagesDir -Recurse -Force
}

Write-Host "Downloading baseline package $BaselineVersion..." -ForegroundColor Yellow
nuget install Microsoft.Azure.Cosmos.Encryption.Custom `
    -Version $BaselineVersion `
    -OutputDirectory $packagesDir `
    -NonInteractive

# Locate assemblies
$currentDll = "Microsoft.Azure.Cosmos.Encryption.Custom\src\bin\Release\netstandard2.0\Microsoft.Azure.Cosmos.Encryption.Custom.dll"
$baselineDll = "$baselineDir\lib\netstandard2.0\Microsoft.Azure.Cosmos.Encryption.Custom.dll"

if (-not (Test-Path $currentDll)) {
    Write-Error "Current assembly not found: $currentDll"
}

if (-not (Test-Path $baselineDll)) {
    Write-Error "Baseline assembly not found: $baselineDll"
}

# Run ApiCompat
Write-Host ""
Write-Host "Running API compatibility check..." -ForegroundColor Yellow
Write-Host "  Current:  $currentDll" -ForegroundColor Gray
Write-Host "  Baseline: $baselineDll" -ForegroundColor Gray
Write-Host ""

$apiCompatArgs = @(
    "-a", $currentDll,
    "-b", $baselineDll,
    "--generate-suppression-file"
)

if ($Strict) {
    $apiCompatArgs += "--strict"
}

& dotnet-apicompat $apiCompatArgs

$exitCode = $LASTEXITCODE

# Cleanup
Remove-Item $packagesDir -Recurse -Force -ErrorAction SilentlyContinue

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "✅ No breaking API changes detected" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "❌ Breaking API changes detected!" -ForegroundColor Red
    Write-Host "Review the output above for details." -ForegroundColor Red
    exit 1
}
```

---

## Task 2: Create API Baseline Snapshot

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/api-baseline/1.0.0-preview08.txt`

```powershell
# Generate baseline snapshot
$version = "1.0.0-preview08"
$outputFile = "Microsoft.Azure.Cosmos.Encryption.Custom\api-baseline\$version.txt"

# Download and extract
nuget install Microsoft.Azure.Cosmos.Encryption.Custom -Version $version -OutputDirectory temp
$dll = "temp\Microsoft.Azure.Cosmos.Encryption.Custom.$version\lib\netstandard2.0\Microsoft.Azure.Cosmos.Encryption.Custom.dll"

# Use ildasm or reflection to extract public API surface
# This is a placeholder - actual implementation would generate comprehensive API list
@"
# API Surface for Microsoft.Azure.Cosmos.Encryption.Custom $version
# Generated: $(Get-Date -Format 'yyyy-MM-dd')

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    public class CosmosEncryptionClient : IDisposable
    {
        public CosmosEncryptionClient(CosmosClient cosmosClient, IKeyEncryptionKeyResolver keyResolver);
        public DatabaseCore GetDatabase(string id);
        public void Dispose();
    }
    
    public class DatabaseCore
    {
        public ContainerCore GetContainer(string id);
        public DataEncryptionKeyContainer DataEncryptionKeys { get; }
    }
    
    public class ContainerCore
    {
        public Task<ItemResponse<T>> CreateItemAsync<T>(...);
        public Task<ItemResponse<T>> ReadItemAsync<T>(...);
        // ... other CRUD methods
    }
    
    public interface IKeyEncryptionKeyResolver
    {
        Task<KeyEncryptionKey> BuildKeyEncryptionKeyAsync(...);
    }
    
    // ... rest of public API
}
"@ | Out-File $outputFile -Encoding UTF8

Remove-Item temp -Recurse -Force
```

---

## Task 3: Add API Check to Pipeline

### File: `templates/encryption-custom-apicompat-check.yml`

```yaml
# API Compatibility Check Template
parameters:
  - name: BaselineVersion
    type: string
    default: '1.0.0-preview08'
  - name: StrictMode
    type: boolean
    default: false

steps:
  - task: UseDotNet@2
    displayName: 'Use .NET 8.0 SDK'
    inputs:
      packageType: 'sdk'
      version: '8.x'

  - task: PowerShell@2
    displayName: 'Install ApiCompat Tool'
    inputs:
      targetType: 'inline'
      script: |
        dotnet tool install --global Microsoft.DotNet.ApiCompat.Tool --version 8.0.* || echo "Already installed"

  - task: DotNetCoreCLI@2
    displayName: 'Build Current Version'
    inputs:
      command: 'build'
      projects: 'Microsoft.Azure.Cosmos.Encryption.Custom/src/Microsoft.Azure.Cosmos.Encryption.Custom.csproj'
      arguments: '--configuration Release'

  - task: PowerShell@2
    displayName: 'Run API Compatibility Check'
    inputs:
      filePath: 'tools/apicompat-check.ps1'
      arguments: '-BaselineVersion ${{ parameters.BaselineVersion }} ${{ eq(parameters.StrictMode, true) && '-Strict' || '' }}'
      failOnStderr: false
    continueOnError: ${{ eq(parameters.StrictMode, false) }}

  - task: PublishBuildArtifacts@1
    displayName: 'Publish API Compatibility Report'
    inputs:
      PathtoPublish: '$(Build.ArtifactStagingDirectory)'
      ArtifactName: 'ApiCompatReport'
    condition: always()
```

---

## Task 4: Integrate into Main Pipeline

Update `azure-pipelines-encryption-custom-compatibility.yml`:

```yaml
stages:
  # NEW Stage 0: API Compatibility Check (fast fail)
  - stage: ApiCompatibilityCheck
    displayName: 'API Compatibility Check'
    jobs:
      - job: CheckApi
        displayName: 'Validate API Surface'
        pool:
          name: 'OneES'
        steps:
          - template: templates/encryption-custom-apicompat-check.yml
            parameters:
              BaselineVersion: $(BaselineVersion)
              StrictMode: false

  # Existing stages follow...
  - stage: QuickCompatibilityCheck
    dependsOn: ApiCompatibilityCheck  # Only run if API check passes
    displayName: 'Quick Check (Last Version)'
    # ... rest of pipeline
```

---

## Task 5: Create API Change Documentation Template

### File: `docs/compatibility-testing/API-CHANGES.md`

```markdown
# API Changes Log

## How to Use This Document

When the API compatibility check fails:

1. Review the breaking changes reported by ApiCompat
2. Determine if changes are intentional
3. Document the changes here
4. Update suppression file if needed

## Format

### Version X.Y.Z

**Date**: YYYY-MM-DD  
**PR**: #XXXX  
**Reviewer**: @username

#### Breaking Changes

- **Type**: `Namespace.TypeName`
- **Change**: Description of what changed
- **Reason**: Why this change was necessary
- **Migration**: How consumers should update their code

#### Non-Breaking Changes

- Added new method `MethodName`
- Added new property `PropertyName`

---

## Change History

### 1.0.0-preview09 (Pending)

**Date**: TBD  
**PR**: TBD

#### Breaking Changes

None

#### Non-Breaking Changes

- TBD

---

### 1.0.0-preview08

**Date**: 2024-09-11  
**PR**: #4673

#### Breaking Changes

None

#### Non-Breaking Changes

- Updated `Microsoft.Data.Encryption.Cryptography` dependency to v1.2.0

---

### 1.0.0-preview07

**Date**: 2024-06-12  
**PR**: #4546

#### Breaking Changes

None

#### Non-Breaking Changes

- Updated package reference `Microsoft.Azure.Cosmos` to version 3.41.0-preview and 3.40.0
```

---

## Task 6: Create Suppression File Template

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/ApiCompatSuppressions.txt`

```text
# API Compatibility Suppressions
# Format: [AssemblyId]MemberName|Reason

# Suppress known intentional changes
# Example:
# Microsoft.Azure.Cosmos.Encryption.Custom/CosmosEncryptionClient.OldMethod()|Deprecated in favor of NewMethod

# Add suppressions here when intentional breaking changes are made
# Always document the reason and migration path
```

Usage in apicompat-check.ps1:

```powershell
$suppressionFile = "Microsoft.Azure.Cosmos.Encryption.Custom\ApiCompatSuppressions.txt"

$apiCompatArgs = @(
    "-a", $currentDll,
    "-b", $baselineDll,
    "--suppression-file", $suppressionFile
)
```

---

## Task 7: Local Testing Script

### File: `tools/test-api-compat-local.ps1`

```powershell
<#
.SYNOPSIS
    Quick local API compatibility test
#>

param(
    [string]$Baseline = "1.0.0-preview08"
)

Write-Host "🧪 Local API Compatibility Test" -ForegroundColor Cyan
Write-Host ""

# Build first
Write-Host "Building current version..." -ForegroundColor Yellow
dotnet build Microsoft.Azure.Cosmos.Encryption.Custom\src -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
}

# Run API compat
Write-Host ""
& .\tools\apicompat-check.ps1 -BaselineVersion $Baseline

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ API compatibility check passed!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "❌ API compatibility issues found" -ForegroundColor Red
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Review changes above" -ForegroundColor Gray
    Write-Host "2. If intentional, document in docs/compatibility-testing/API-CHANGES.md" -ForegroundColor Gray
    Write-Host "3. Add suppressions to ApiCompatSuppressions.txt if needed" -ForegroundColor Gray
}
```

---

## Verification Steps

1. **Install tool globally**:

   ```powershell
   dotnet tool install --global Microsoft.DotNet.ApiCompat.Tool
   ```

2. **Run local test**:

   ```powershell
   .\tools\test-api-compat-local.ps1
   ```

3. **Verify pipeline stage works** (after integration)

---

## Best Practices

1. **Run API check before runtime tests** (fast fail)
2. **Document all breaking changes** in API-CHANGES.md
3. **Use suppressions sparingly** and always with documentation
4. **Generate baseline snapshots** for each release
5. **Review API changes in PRs** before merging

---

## Files Created

```
tools/
├── apicompat-check.ps1
└── test-api-compat-local.ps1
Microsoft.Azure.Cosmos.Encryption.Custom/
├── api-baseline/
│   └── 1.0.0-preview08.txt
└── ApiCompatSuppressions.txt
templates/
└── encryption-custom-apicompat-check.yml
docs/compatibility-testing/
└── API-CHANGES.md
```

**Total**: 6 files
