# Azure DevOps Pipeline Guide
## Compatibility Testing for Microsoft.Azure.Cosmos.Encryption.Custom

This guide explains how to use and maintain the Azure DevOps pipeline for automated compatibility testing.

## Overview

The compatibility testing pipeline validates that new versions of `Microsoft.Azure.Cosmos.Encryption.Custom` remain compatible with previous versions. It runs in two modes:

1. **Quick Compatibility Check** (Pull Requests)
2. **Full Matrix Compatibility** (Scheduled/Manual)

## Pipeline File

**Location:** `azure-pipelines-encryption-custom-compatibility.yml`

## Operating Modes

### 1. Quick Compatibility Check (PR Mode)

**Trigger:** Automatically runs on pull requests to `master` branch

**Purpose:** Fast feedback during development

**Behavior:**
- Tests only the **baseline version** (latest stable version)
- Runs in ~5-10 minutes
- Provides quick pass/fail feedback
- Blocks PR merge if tests fail

**What it validates:**
- Core API surface hasn't changed
- Public types and methods exist
- Basic encryption/decryption functionality
- Configuration and policy APIs work

### 2. Full Matrix Compatibility (Scheduled Mode)

**Trigger:** 
- Scheduled: Daily at 2:00 AM UTC
- Manual: Can be triggered from Azure DevOps UI

**Purpose:** Comprehensive compatibility validation

**Behavior:**
- Tests **all versions** in the version matrix
- Runs in parallel (4 jobs simultaneously)
- Takes ~15-20 minutes
- Generates comprehensive test results
- Creates artifacts for analysis

**What it validates:**
- All versions in the matrix remain compatible
- No breaking changes across any tested version
- Version-specific metadata tracking
- Assembly reference consistency

## Pipeline Structure

### Stages

```yaml
Stage 1: QuickCompatibilityCheck (PR only)
  └─ Job: TestBaseline
      └─ Steps: Run tests against baseline version

Stage 2: FullMatrixCompatibility (Scheduled/Manual)
  ├─ Job: TestPreview07 (baseline)
  ├─ Job: TestPreview06
  ├─ Job: TestPreview05
  └─ Job: TestPreview04
      └─ Steps: Run tests against each version

Stage 3: Report (Scheduled/Manual)
  └─ Job: PublishReport
      ├─ Publish test artifacts
      └─ Generate summary
```

### Templates

The pipeline uses reusable templates for maintainability:

#### `templates/encryption-custom-compatibility-test-steps.yml`

**Purpose:** Reusable steps for running tests against any version

**Parameters:**
- `BuildConfiguration`: Release or Debug (default: Release)
- `TestVersion`: Version to test (e.g., "1.0.0-preview07")

**Steps:**
1. Checkout source code
2. Install .NET 6.0 runtime and 8.0 SDK
3. Display test configuration
4. Restore NuGet packages
5. Build with version override
6. Run tests with version-specific logging
7. Publish test results (TRX format)
8. Display package resolution details

#### `templates/encryption-custom-compatibility-test.yml`

**Purpose:** Job-level wrapper for test steps

**Parameters:**
- All parameters from test-steps template
- `JobName`: Unique job identifier
- `DisplayName`: Human-readable job name

**Usage:** Simplifies pipeline definition when full job control is needed

## Variables

### Pipeline Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `BaselineVersion` | 1.0.0-preview07 | Latest tested version |
| `BuildConfiguration` | Release | Build configuration |

### Update Variables

To update the baseline version:

```yaml
variables:
  BaselineVersion: '1.0.0-preview08'  # Update this line
  BuildConfiguration: 'Release'
```

## Triggers

### Branch Triggers

```yaml
trigger:
  branches:
    include:
      - master
      - feature/*
  paths:
    include:
      - Microsoft.Azure.Cosmos.Encryption.Custom/**
```

**Behavior:** Pipeline runs on commits to `master` or `feature/*` branches that modify files in `Microsoft.Azure.Cosmos.Encryption.Custom/` directory

### Pull Request Trigger

```yaml
pr:
  branches:
    include:
      - master
```

**Behavior:** Quick check runs on PRs targeting `master` branch

### Scheduled Trigger

```yaml
schedules:
  - cron: "0 2 * * *"
    displayName: Daily compatibility check
    branches:
      include:
        - master
```

**Behavior:** Full matrix test runs daily at 2:00 AM UTC on `master` branch

## Manual Execution

### From Azure DevOps UI

1. Navigate to **Pipelines** → **azure-pipelines-encryption-custom-compatibility**
2. Click **Run pipeline**
3. Select branch (usually `master`)
4. (Optional) Set variables:
   - `BaselineVersion`: Override baseline version
   - `BuildConfiguration`: Change build configuration
5. Click **Run**

**Result:** Runs full matrix compatibility test (Stage 2 and 3)

### From Azure CLI

```bash
# Trigger pipeline run
az pipelines run --name "azure-pipelines-encryption-custom-compatibility" --branch master

# Trigger with variable override
az pipelines run --name "azure-pipelines-encryption-custom-compatibility" \
  --branch master \
  --variables BaselineVersion=1.0.0-preview08
```

## Updating the Version Matrix

The version matrix is defined in two places:

### 1. Test Configuration File

**Location:** `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/testconfig.json`

```json
{
  "versionMatrix": [
    "1.0.0-preview07",
    "1.0.0-preview06",
    "1.0.0-preview05",
    "1.0.0-preview04"
  ],
  "baselineVersion": "1.0.0-preview07"
}
```

**Purpose:** Used by local test execution and test discovery

### 2. Pipeline Jobs

**Location:** `azure-pipelines-encryption-custom-compatibility.yml` → Stage 2

```yaml
jobs:
  - template: templates/encryption-custom-compatibility-test.yml
    parameters:
      JobName: 'TestPreview07'
      DisplayName: 'Test v1.0.0-preview07 (Baseline)'
      TestVersion: '1.0.0-preview07'
      BuildConfiguration: $(BuildConfiguration)
```

**Purpose:** Defines parallel jobs for each version

### Adding a New Version

**Example:** Add version 1.0.0-preview08

1. **Update testconfig.json:**
   ```json
   {
     "versionMatrix": [
       "1.0.0-preview08",  // Add new version
       "1.0.0-preview07",
       "1.0.0-preview06",
       "1.0.0-preview05",
       "1.0.0-preview04"
     ],
     "baselineVersion": "1.0.0-preview08"  // Update baseline
   }
   ```

2. **Update pipeline variables:**
   ```yaml
   variables:
     BaselineVersion: '1.0.0-preview08'  # Update baseline
   ```

3. **Add pipeline job:**
   ```yaml
   - template: templates/encryption-custom-compatibility-test.yml
     parameters:
       JobName: 'TestPreview08'
       DisplayName: 'Test v1.0.0-preview08 (Baseline)'
       TestVersion: '1.0.0-preview08'
       BuildConfiguration: $(BuildConfiguration)
   ```

4. **Update Stage 1 (Quick Check):**
   ```yaml
   - stage: QuickCompatibilityCheck
     jobs:
       - template: templates/encryption-custom-compatibility-test.yml
         parameters:
           TestVersion: '1.0.0-preview08'  # Update to new baseline
   ```

### Removing an Old Version

When a version becomes obsolete:

1. Remove from `testconfig.json` version matrix
2. Remove corresponding job from Stage 2 in pipeline
3. Keep baseline version updated to latest

## Test Results

### Artifacts

The pipeline publishes test results as artifacts:

**Location:** `$(Build.ArtifactStagingDirectory)/TestResults`

**Contents:**
- `compatibility-{version}.trx` - Test results in TRX format
- Test output logs

**Access:** 
- Azure DevOps → Pipeline Run → **Artifacts** tab
- Download for offline analysis

### Test Reports

Azure DevOps automatically generates test reports from TRX files:

**Location:** Pipeline Run → **Tests** tab

**Features:**
- Pass/fail summary
- Test duration tracking
- Failure analysis
- Historical trends

## Troubleshooting

### Common Issues

#### 1. Test Failures on Specific Version

**Symptom:** Tests pass on baseline but fail on older version

**Diagnosis:**
- Check test output in Azure DevOps
- Compare test results across versions
- Review error messages in TRX file

**Resolution:**
- If legitimate breaking change: Update tests and document
- If test bug: Fix test implementation
- If package issue: Investigate version-specific behavior

**Example:**
```bash
# Local reproduction
cd Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
dotnet test -p:TargetEncryptionCustomVersion=1.0.0-preview06
```

#### 2. Package Not Found

**Symptom:** Error during NuGet restore: "Package 'Microsoft.Azure.Cosmos.Encryption.Custom' version 'X.X.X' not found"

**Diagnosis:**
- Verify version exists on NuGet.org: https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom
- Check NuGet.config for correct feed

**Resolution:**
- Update version matrix to use only published versions
- Wait for package publication if testing pre-release

#### 3. Pipeline Doesn't Trigger

**Symptom:** Commit to master doesn't trigger pipeline

**Diagnosis:**
- Check if changes are in trigger path: `Microsoft.Azure.Cosmos.Encryption.Custom/**`
- Verify branch name matches trigger pattern
- Check pipeline is enabled in Azure DevOps

**Resolution:**
- Trigger manually from UI
- Update trigger paths if needed
- Verify pipeline is not disabled

#### 4. Parallel Jobs Timeout

**Symptom:** One or more parallel jobs timeout after 60 minutes

**Diagnosis:**
- Check agent availability
- Review test execution time per version
- Check for hanging tests

**Resolution:**
- Increase job timeout in pipeline:
  ```yaml
  jobs:
    - template: templates/encryption-custom-compatibility-test.yml
      timeoutInMinutes: 90  # Increase timeout
  ```
- Optimize test execution
- Split large test classes

#### 5. Agent Pool Unavailable

**Symptom:** "No agents available" error

**Diagnosis:**
- Check Azure DevOps agent pool status
- Verify `windows-latest` pool is accessible

**Resolution:**
- Wait for agents to become available
- Use alternative pool:
  ```yaml
  pool:
    vmImage: 'windows-2022'  # Specific Windows version
  ```

### Debugging Tips

#### Enable Verbose Logging

Add system diagnostic variables:

```yaml
variables:
  system.debug: true  # Enable debug logging
```

#### Run Subset of Tests

Temporarily modify pipeline to run specific test class:

```yaml
- task: DotNetCoreCLI@2
  inputs:
    arguments: '--filter FullyQualifiedName~CoreApiTests'  # Specific class
```

#### Check Package Resolution

Add diagnostic step:

```yaml
- task: PowerShell@2
  displayName: 'List All Packages'
  inputs:
    targetType: 'inline'
    script: |
      dotnet list Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/*.csproj package --include-transitive
```

## Maintenance

### Regular Tasks

#### Monthly Review
- Review test pass rates
- Update version matrix (add new versions, remove obsolete)
- Check for NuGet package availability

#### Quarterly Review
- Analyze test coverage
- Update baseline version
- Review and update test cases for new APIs

#### Annual Review
- Review pipeline architecture
- Update .NET SDK versions
- Audit test suite effectiveness

### Version Lifecycle

**When to add a version:**
- New preview or stable version is published to NuGet
- Version is considered important for compatibility tracking

**When to remove a version:**
- Version is 6+ months old
- Version has critical bugs and is deprecated
- Maintaining test matrix becomes too large (>10 versions)

**Best Practice:** Keep 4-6 recent versions in matrix

## Integration with Development Workflow

### Pull Request Workflow

1. Developer creates PR to `master`
2. Pipeline automatically runs **Quick Check** (baseline version only)
3. Results appear in PR checks
4. If tests fail:
   - Developer investigates failure
   - Updates code or tests
   - Pushes new commit
   - Pipeline re-runs automatically
5. PR can only merge after tests pass

### Release Workflow

1. New version is released to NuGet
2. Update `testconfig.json` with new version
3. Update pipeline with new baseline version
4. Add job for new version in Stage 2
5. Commit changes to `master`
6. Next scheduled run validates new version
7. Monitor results for any compatibility issues

### Hotfix Workflow

1. Create hotfix branch from `master`
2. Make changes
3. Test locally: `.\test-compatibility.ps1`
4. Create PR to `master`
5. Quick check runs automatically
6. After merge, scheduled run validates against full matrix

## Performance Optimization

### Current Performance

- **Quick Check (PR):** ~5-10 minutes
- **Full Matrix (4 versions):** ~15-20 minutes
- **Per-version test execution:** ~3-5 minutes

### Optimization Strategies

#### 1. Reduce Test Execution Time
- Use `[Trait]` attributes to categorize tests
- Run fast tests in Quick Check, all tests in Full Matrix
- Example:
  ```csharp
  [Fact]
  [Trait("Category", "Fast")]
  public void CoreApi_TypeExists() { }
  ```

#### 2. Optimize Package Restore
- Use NuGet package caching in Azure DevOps
- Configure NuGet.config for optimal package sources

#### 3. Parallelize More Aggressively
- Increase parallel jobs (if agent pool supports)
- Split test classes if they become too large

#### 4. Conditional Execution
- Skip older versions on PR (already implemented)
- Run full matrix only on schedule or manual trigger

## Security Considerations

### Secrets Management

If tests require secrets (e.g., Cosmos DB connection strings):

```yaml
variables:
  - group: 'CosmosDBSecrets'  # Variable group in Azure DevOps

steps:
  - task: DotNetCoreCLI@2
    env:
      COSMOS_ENDPOINT: $(CosmosEndpoint)  # From variable group
      COSMOS_KEY: $(CosmosKey)
```

### Access Control

- Pipeline should have minimum required permissions
- Use service connections for accessing external resources
- Limit manual trigger permissions to authorized users

## Monitoring and Alerts

### Success Rate Monitoring

Track test pass rate over time:

- Azure DevOps Analytics
- Test results trends
- Set up alerts for declining pass rates

### Pipeline Health

Monitor pipeline execution:

- Average execution time
- Agent availability
- Failure patterns

### Recommended Alerts

1. **Pipeline Failure:** Email on pipeline failure
2. **Test Pass Rate Drop:** Alert if pass rate < 90%
3. **Execution Time Increase:** Alert if execution time > 30 minutes

## Future Enhancements

### Planned Improvements

1. **API Compatibility Tool Integration** (Agent 4)
   - Automated API surface comparison
   - Breaking change detection
   - API baseline snapshots

2. **Extended Test Coverage** (Agent 6)
   - Performance regression testing
   - Memory usage tracking
   - Integration tests with actual Cosmos DB

3. **Multi-Framework Testing**
   - Test against different .NET versions
   - Validate netstandard2.0 compatibility across runtimes

4. **Automated Version Discovery**
   - Query NuGet API for available versions
   - Auto-update version matrix

## References

- [Azure Pipelines YAML Schema](https://docs.microsoft.com/en-us/azure/devops/pipelines/yaml-schema)
- [.NET Core CLI Task](https://docs.microsoft.com/en-us/azure/devops/pipelines/tasks/build/dotnet-core-cli)
- [NuGet Package Versioning](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning)

## Support

For issues or questions:

1. Check this guide's troubleshooting section
2. Review Azure DevOps pipeline logs
3. Contact the Cosmos DB SDK team

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-XX  
**Maintained by:** Cosmos DB SDK Team
