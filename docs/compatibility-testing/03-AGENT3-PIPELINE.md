# Agent 3: Pipeline Configuration

## Overview

**Goal**: Create Azure DevOps pipeline configuration to automatically run compatibility tests across version matrices.

**Estimated Time**: 2-3 hours

**Dependencies**: Agent 1 (Infrastructure) must be complete

**Deliverables**:

1. Main compatibility testing pipeline YAML
2. Reusable pipeline template for version matrix
3. Integration with Azure DevOps

---

## Pipeline Strategy

### Two Operating Modes

**1. Automatic Mode (PR/CI)**

- Runs on every Pull Request
- Tests against **last published version** only
- Fast feedback (~5 minutes)
- Blocks PR if compatibility breaks

**2. Matrix Mode (Scheduled/Manual)**

- Runs on schedule or manual trigger
- Tests against **configurable list of versions**
- Comprehensive validation (~15-20 minutes)
- Parallel execution for speed

---

## Task 1: Create Main Pipeline

### File: `azure-pipelines-encryption-custom-compatibility.yml`

```yaml
# Compatibility Testing Pipeline for Microsoft.Azure.Cosmos.Encryption.Custom
# Tests current build against published NuGet package versions to catch breaking changes

trigger:
  branches:
    include:
      - master
      - feature/*
  paths:
    include:
      - Microsoft.Azure.Cosmos.Encryption.Custom/**
      - azure-pipelines-encryption-custom-compatibility.yml

pr:
  branches:
    include:
      - master
  paths:
    include:
      - Microsoft.Azure.Cosmos.Encryption.Custom/**

schedules:
  - cron: "0 2 * * *"  # Run daily at 2 AM UTC
    displayName: 'Nightly Compatibility Test (Full Matrix)'
    branches:
      include:
        - master
    always: true

variables:
  BuildConfiguration: 'Release'
  VmImage: 'windows-latest'
  
  # Baseline version (last published) for PR/CI builds
  BaselineVersion: '1.0.0-preview08'
  
  # Full version matrix for scheduled/manual runs
  # Update this when new versions are published
  VersionMatrix: |
    1.0.0-preview08
    1.0.0-preview07
    1.0.0-preview06
    1.0.0-preview05

stages:
  # Stage 1: Quick compatibility check against last published version
  - stage: QuickCompatibilityCheck
    displayName: 'Quick Check (Last Version)'
    condition: and(succeeded(), eq(variables['Build.Reason'], 'PullRequest'))
    jobs:
      - template: templates/encryption-custom-compatibility-test.yml
        parameters:
          BuildConfiguration: $(BuildConfiguration)
          VmImage: $(VmImage)
          TestVersion: $(BaselineVersion)
          JobName: 'QuickCheck'
          DisplayName: 'Test vs $(BaselineVersion)'

  # Stage 2: Full matrix compatibility testing
  - stage: FullMatrixCompatibility
    displayName: 'Full Matrix Compatibility'
    condition: and(succeeded(), ne(variables['Build.Reason'], 'PullRequest'))
    jobs:
      # Parse version matrix and create parallel jobs
      - job: TestPreview08
        displayName: 'Test vs 1.0.0-preview08'
        pool:
          name: 'OneES'
        steps:
          - template: templates/encryption-custom-compatibility-test-steps.yml
            parameters:
              BuildConfiguration: $(BuildConfiguration)
              TestVersion: '1.0.0-preview08'

      - job: TestPreview07
        displayName: 'Test vs 1.0.0-preview07'
        pool:
          name: 'OneES'
        steps:
          - template: templates/encryption-custom-compatibility-test-steps.yml
            parameters:
              BuildConfiguration: $(BuildConfiguration)
              TestVersion: '1.0.0-preview07'

      - job: TestPreview06
        displayName: 'Test vs 1.0.0-preview06'
        pool:
          name: 'OneES'
        steps:
          - template: templates/encryption-custom-compatibility-test-steps.yml
            parameters:
              BuildConfiguration: $(BuildConfiguration)
              TestVersion: '1.0.0-preview06'

      - job: TestPreview05
        displayName: 'Test vs 1.0.0-preview05'
        pool:
          name: 'OneES'
        steps:
          - template: templates/encryption-custom-compatibility-test-steps.yml
            parameters:
              BuildConfiguration: $(BuildConfiguration)
              TestVersion: '1.0.0-preview05'

  # Stage 3: Summary and reporting
  - stage: Report
    displayName: 'Compatibility Report'
    dependsOn:
      - QuickCompatibilityCheck
      - FullMatrixCompatibility
    condition: succeededOrFailed()
    jobs:
      - job: PublishResults
        displayName: 'Publish Test Results'
        pool:
          name: 'OneES'
        steps:
          - task: PublishBuildArtifacts@1
            displayName: 'Publish Compatibility Report'
            inputs:
              PathtoPublish: '$(Build.ArtifactStagingDirectory)'
              ArtifactName: 'CompatibilityResults'
              publishLocation: 'Container'
            condition: always()

          - task: PowerShell@2
            displayName: 'Generate Summary'
            inputs:
              targetType: 'inline'
              script: |
                Write-Host "=================================="
                Write-Host "Compatibility Testing Summary"
                Write-Host "=================================="
                Write-Host "Build: $(Build.BuildNumber)"
                Write-Host "Branch: $(Build.SourceBranch)"
                Write-Host "Reason: $(Build.Reason)"
                Write-Host ""
                Write-Host "See test results for details."
```

---

## Task 2: Create Reusable Test Steps Template

### File: `templates/encryption-custom-compatibility-test-steps.yml`

```yaml
# Reusable steps template for running compatibility tests against a specific version
parameters:
  - name: BuildConfiguration
    type: string
    default: 'Release'
  - name: TestVersion
    type: string
    default: ''

steps:
  - checkout: self
    clean: true
    displayName: 'Checkout Source'

  - task: UseDotNet@2
    displayName: 'Use .NET 6.0'
    inputs:
      packageType: 'runtime'
      version: '6.x'

  - task: UseDotNet@2
    displayName: 'Use .NET 8.0 SDK'
    inputs:
      packageType: 'sdk'
      version: '8.x'

  - task: PowerShell@2
    displayName: 'Display Test Configuration'
    inputs:
      targetType: 'inline'
      script: |
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Compatibility Test Configuration" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Testing Against Version: ${{ parameters.TestVersion }}" -ForegroundColor Yellow
        Write-Host "Build Configuration: ${{ parameters.BuildConfiguration }}" -ForegroundColor Yellow
        Write-Host "Agent: $(Agent.Name)" -ForegroundColor Gray
        Write-Host "========================================" -ForegroundColor Cyan

  - task: DotNetCoreCLI@2
    displayName: 'Restore NuGet Packages'
    inputs:
      command: 'restore'
      projects: 'Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/*.csproj'
      feedsToUse: 'config'
      nugetConfigPath: 'NuGet.config'
      verbosityRestore: 'Minimal'

  - task: DotNetCoreCLI@2
    displayName: 'Build Compatibility Tests'
    inputs:
      command: 'build'
      projects: 'Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/*.csproj'
      arguments: '--configuration ${{ parameters.BuildConfiguration }} --no-restore'

  - task: DotNetCoreCLI@2
    displayName: 'Run Compatibility Tests (Version: ${{ parameters.TestVersion }})'
    inputs:
      command: 'test'
      projects: 'Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/*.csproj'
      arguments: >
        --configuration ${{ parameters.BuildConfiguration }}
        --no-build
        --no-restore
        --logger "trx;LogFileName=compatibility-${{ parameters.TestVersion }}.trx"
        --logger "console;verbosity=normal"
        --results-directory $(Build.ArtifactStagingDirectory)/TestResults
        -p:TargetEncryptionCustomVersion=${{ parameters.TestVersion }}
    continueOnError: false

  - task: PublishTestResults@2
    displayName: 'Publish Test Results'
    inputs:
      testResultsFormat: 'VSTest'
      testResultsFiles: '**/*.trx'
      searchFolder: '$(Build.ArtifactStagingDirectory)/TestResults'
      mergeTestResults: false
      testRunTitle: 'Compatibility Tests - ${{ parameters.TestVersion }}'
      failTaskOnFailedTests: true
    condition: always()

  - task: PowerShell@2
    displayName: 'Display Package Resolution'
    inputs:
      targetType: 'inline'
      script: |
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Package Resolution Details" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        
        $projectPath = "Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/*.csproj"
        dotnet list $projectPath package --include-transitive | Where-Object { $_ -match "Microsoft.Azure.Cosmos" }
        
        Write-Host "========================================" -ForegroundColor Cyan
    condition: always()
```

---

## Task 3: Create Job-Level Template (Alternative)

### File: `templates/encryption-custom-compatibility-test.yml`

```yaml
# Job template for compatibility testing (alternative to steps-only template)
parameters:
  - name: BuildConfiguration
    type: string
    default: 'Release'
  - name: VmImage
    type: string
    default: 'windows-latest'
  - name: TestVersion
    type: string
    default: ''
  - name: JobName
    type: string
    default: 'CompatTest'
  - name: DisplayName
    type: string
    default: 'Compatibility Test'

jobs:
  - job: ${{ parameters.JobName }}
    displayName: ${{ parameters.DisplayName }}
    pool:
      name: 'OneES'
    
    steps:
      - template: encryption-custom-compatibility-test-steps.yml
        parameters:
          BuildConfiguration: ${{ parameters.BuildConfiguration }}
          TestVersion: ${{ parameters.TestVersion }}
```

---

## Task 4: Create Pipeline README

### File: `docs/compatibility-testing/PIPELINE-GUIDE.md`

```markdown
# Compatibility Testing Pipeline Guide

## Overview

The compatibility testing pipeline (`azure-pipelines-encryption-custom-compatibility.yml`) automatically validates that new changes don't break compatibility with existing published versions.

## Pipeline Modes

### 1. Pull Request Mode (Quick Check)

**Trigger**: Automatic on PR to `master`

**Behavior**:

- Runs only against the **last published version** (currently `1.0.0-preview08`)
- Fast feedback (~5 minutes)
- Blocks merge if compatibility breaks

**When it runs**:

```yaml
pr:
  branches:
    include:
      - master
```

### 2. Nightly Mode (Full Matrix)

**Trigger**: Scheduled daily at 2 AM UTC

**Behavior**:

- Tests against **all versions** in the matrix
- Parallel execution (~15-20 minutes)
- Comprehensive validation

**Schedule**:

```yaml
schedules:
  - cron: "0 2 * * *"
    displayName: 'Nightly Compatibility Test'
```

### 3. Manual Mode (On-Demand)

**Trigger**: Manual pipeline run

**Behavior**:

- Same as Nightly Mode
- Can be triggered for validation before releases
- Useful for testing specific scenarios

## Pipeline Structure

```
┌─────────────────────────────────────────────┐
│ Stage 1: Quick Compatibility Check          │
│ (PR only - tests last version)              │
└────────────┬────────────────────────────────┘
             │
             ├─ Job: Test vs 1.0.0-preview08
             │  └─ Runs all compatibility tests
             │
┌────────────┴────────────────────────────────┐
│ Stage 2: Full Matrix Compatibility          │
│ (Scheduled/Manual - all versions)           │
└────────────┬────────────────────────────────┘
             │
             ├─ Job: Test vs 1.0.0-preview08  (parallel)
             ├─ Job: Test vs 1.0.0-preview07  (parallel)
             ├─ Job: Test vs 1.0.0-preview06  (parallel)
             └─ Job: Test vs 1.0.0-preview05  (parallel)
             │
┌────────────┴────────────────────────────────┐
│ Stage 3: Report                             │
│ (Always runs - publishes results)           │
└─────────────────────────────────────────────┘
```

## Adding New Versions to Test Matrix

When a new version is published, update the pipeline:

### Step 1: Update Version Matrix Variable

Edit `azure-pipelines-encryption-custom-compatibility.yml`:

```yaml
variables:
  BaselineVersion: '1.0.0-preview09'  # Update to latest
  VersionMatrix: |
    1.0.0-preview09  # Add new version
    1.0.0-preview08
    1.0.0-preview07
    1.0.0-preview06
```

### Step 2: Add New Job (If Using Explicit Jobs)

```yaml
- job: TestPreview09
  displayName: 'Test vs 1.0.0-preview09'
  pool:
    name: 'OneES'
  steps:
    - template: templates/encryption-custom-compatibility-test-steps.yml
      parameters:
        BuildConfiguration: $(BuildConfiguration)
        TestVersion: '1.0.0-preview09'
```

### Step 3: Update testconfig.json

```json
{
  "versionMatrix": {
    "baseline": "1.0.0-preview09",
    "versions": [
      "1.0.0-preview09",
      "1.0.0-preview08",
      "1.0.0-preview07"
    ]
  }
}
```

## Monitoring and Debugging

### Viewing Test Results

1. Navigate to the pipeline run in Azure DevOps
2. Go to **Tests** tab
3. Filter by test version:
   - Test run title includes version number
   - Example: "Compatibility Tests - 1.0.0-preview08"

### Understanding Failures

**Failure in PR Mode** = Breaking change vs last published version

- **BLOCK THE PR** until resolved
- Review what API changed
- Update code or mark as intentional breaking change

**Failure in Matrix Mode** = Breaking change vs specific version

- Check if it's a known/intentional break
- Update version-specific test handling if needed
- Document in changelog

### Common Issues

#### Issue 1: "Package not found"

**Cause**: Version doesn't exist on NuGet

**Solution**:

```powershell
# Verify package exists
nuget list Microsoft.Azure.Cosmos.Encryption.Custom -AllVersions
```

#### Issue 2: "Assembly version mismatch"

**Cause**: NuGet cache or restore issue

**Solution**: Pipeline should automatically clear cache, but can be forced:

```yaml
- task: PowerShell@2
  inputs:
    script: dotnet nuget locals all --clear
```

#### Issue 3: Tests pass locally but fail in CI

**Cause**: Different NuGet feed or version resolution

**Solution**:

- Verify `NuGet.config` is correct
- Check Azure Artifacts feed permissions
- Review package resolution logs

## Pipeline Variables

### Configuration Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `BuildConfiguration` | `Release` | Build configuration for tests |
| `VmImage` | `windows-latest` | Agent VM image |
| `BaselineVersion` | `1.0.0-preview08` | Last published version for PR checks |
| `VersionMatrix` | Multi-line string | All versions to test in full matrix |

### Runtime Variables

| Variable | Description |
|----------|-------------|
| `Build.Reason` | Why pipeline ran (Manual, PR, Schedule) |
| `Build.SourceBranch` | Branch being tested |
| `Build.ArtifactStagingDirectory` | Where test results are stored |

## Manual Pipeline Triggers

### Trigger Full Matrix Test

1. Go to Azure DevOps Pipelines
2. Find `azure-pipelines-encryption-custom-compatibility`
3. Click **Run pipeline**
4. Select branch
5. Click **Run**

### Override Version to Test

Currently not parameterized, but can be added:

```yaml
parameters:
  - name: customVersion
    displayName: 'Custom Version to Test'
    type: string
    default: ''

# Then use $(customVersion) if provided
```

## Best Practices

1. **Keep matrix small** - Only test versions that matter (last 3-4)
2. **Update baseline frequently** - Always test vs latest published
3. **Monitor nightly runs** - Catch issues before PRs
4. **Document breaking changes** - Update changelog when compatibility breaks
5. **Clean up old versions** - Remove very old versions from matrix (keep last 4-5)

## Integration with Release Pipeline

The compatibility pipeline should be a **gate** for releases:

```yaml
# In azure-pipelines-encryption-custom-official.yml
stages:
  - stage: Validate
    jobs:
      - job: RunCompatibilityTests
        steps:
          - task: InvokeRESTAPI@1
            inputs:
              # Trigger compatibility pipeline and wait for result
```

## Performance Optimization

### Current Timings

- **PR Mode**: ~5 minutes (1 version)
- **Full Matrix**: ~15-20 minutes (4 versions in parallel)

### Optimization Tips

1. **Parallel execution** - Jobs run concurrently
2. **Shared restore** - Cache NuGet packages
3. **Fast tests** - No real Cosmos DB connections
4. **Incremental builds** - Use `--no-restore` and `--no-build`

## Troubleshooting Checklist

- [ ] Package version exists on NuGet.org or private feed
- [ ] NuGet.config is properly configured
- [ ] Test project builds successfully locally
- [ ] Tests pass locally with same version
- [ ] Azure Artifacts feed has correct permissions
- [ ] Agent pool has capacity
- [ ] .NET SDK versions are correct (6.x runtime, 8.x SDK)

## Support

**Pipeline Owner**: Encryption Custom Team  
**File**: `azure-pipelines-encryption-custom-compatibility.yml`  
**Templates**: `templates/encryption-custom-compatibility-*.yml`  
**Documentation**: This file
```

---

## Task 5: Create Pipeline Validation Script

### File: `tools/validate-compatibility-pipeline.ps1`

```powershell
<#
.SYNOPSIS
    Validates compatibility pipeline configuration
.DESCRIPTION
    Checks that all versions in testconfig.json match the pipeline YAML
    Ensures consistency across configuration files
#>

param(
    [switch]$Fix
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Validating Compatibility Pipeline Configuration" -ForegroundColor Cyan
Write-Host ""

# Paths
$pipelineYaml = "azure-pipelines-encryption-custom-compatibility.yml"
$testConfig = "Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\testconfig.json"

if (-not (Test-Path $pipelineYaml)) {
    Write-Error "Pipeline YAML not found: $pipelineYaml"
}

if (-not (Test-Path $testConfig)) {
    Write-Error "Test config not found: $testConfig"
}

# Load test config
$config = Get-Content $testConfig | ConvertFrom-Json
$testVersions = $config.versionMatrix.versions
$baselineVersion = $config.versionMatrix.baseline

Write-Host "Test Config Versions:" -ForegroundColor Yellow
$testVersions | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
Write-Host "Baseline: $baselineVersion" -ForegroundColor Yellow
Write-Host ""

# Parse pipeline YAML (simple text parsing)
$pipelineContent = Get-Content $pipelineYaml -Raw

# Extract baseline version
if ($pipelineContent -match "BaselineVersion:\s*'([^']+)'") {
    $pipelineBaseline = $Matches[1]
    Write-Host "Pipeline Baseline: $pipelineBaseline" -ForegroundColor Yellow
    
    if ($pipelineBaseline -ne $baselineVersion) {
        Write-Host "❌ MISMATCH: Pipeline baseline ($pipelineBaseline) != Test config baseline ($baselineVersion)" -ForegroundColor Red
        
        if ($Fix) {
            Write-Host "Updating pipeline baseline to $baselineVersion..." -ForegroundColor Yellow
            $pipelineContent = $pipelineContent -replace "BaselineVersion:\s*'[^']+'", "BaselineVersion: '$baselineVersion'"
            Set-Content $pipelineYaml -Value $pipelineContent
            Write-Host "✅ Fixed" -ForegroundColor Green
        }
    } else {
        Write-Host "✅ Baseline versions match" -ForegroundColor Green
    }
}

# Extract job versions
$jobPattern = "TestVersion:\s*'([^']+)'"
$pipelineVersions = [regex]::Matches($pipelineContent, $jobPattern) | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

Write-Host ""
Write-Host "Pipeline Job Versions:" -ForegroundColor Yellow
$pipelineVersions | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
Write-Host ""

# Compare versions
$missingInPipeline = $testVersions | Where-Object { $pipelineVersions -notcontains $_ }
$extraInPipeline = $pipelineVersions | Where-Object { $testVersions -notcontains $_ }

if ($missingInPipeline) {
    Write-Host "❌ Versions in test config but NOT in pipeline:" -ForegroundColor Red
    $missingInPipeline | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

if ($extraInPipeline) {
    Write-Host "⚠️  Versions in pipeline but NOT in test config:" -ForegroundColor Yellow
    $extraInPipeline | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}

if (-not $missingInPipeline -and -not $extraInPipeline) {
    Write-Host "✅ All versions are consistent" -ForegroundColor Green
}

Write-Host ""
Write-Host "Validation complete." -ForegroundColor Cyan
```

---

## Verification Steps

1. **Validate YAML syntax**:

   ```powershell
   # Azure DevOps CLI
   az pipelines validate --yaml-path azure-pipelines-encryption-custom-compatibility.yml
   ```

2. **Run validation script**:

   ```powershell
   .\tools\validate-compatibility-pipeline.ps1
   ```

3. **Test pipeline locally** (if possible with local runner)

4. **Create and run pipeline in Azure DevOps**

---

## Next Steps

- **Agent 4**: Add API compatibility tool integration to pipeline
- **Agent 5**: Create developer documentation and helper scripts

---

## Files Created

```
azure-pipelines-encryption-custom-compatibility.yml
templates/
├── encryption-custom-compatibility-test-steps.yml
└── encryption-custom-compatibility-test.yml
docs/compatibility-testing/
└── PIPELINE-GUIDE.md
tools/
└── validate-compatibility-pipeline.ps1
```

**Total**: 5 files
