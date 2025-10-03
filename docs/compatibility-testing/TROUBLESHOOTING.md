# Troubleshooting Guide - Encryption.Custom Compatibility Testing

This guide covers common issues and their solutions when working with the compatibility testing framework.

---

## 🔍 Quick Diagnostics

Before diving into specific issues, run these basic checks:

```powershell
# Verify package references
dotnet list Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests package

# Check for version conflicts
dotnet restore --verbosity detailed Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests

# Validate test config
Get-Content Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\testconfig.json | ConvertFrom-Json

# Test NuGet feed connectivity
Invoke-WebRequest -Uri "https://api.nuget.org/v3/index.json" -UseBasicParsing
```

---

## Issue 1: Package Not Found

### 🔴 Symptoms
```
error NU1101: Unable to find package Microsoft.Azure.Cosmos.Encryption.Custom. 
No packages exist with this id in source(s): nuget.org
```

Or during test execution:
```
Could not load file or assembly 'Microsoft.Azure.Cosmos.Encryption.Custom, Version=1.0.0.0'
```

### 🔎 Root Causes
1. Version doesn't exist on NuGet.org
2. NuGet cache corruption
3. Network/firewall blocking NuGet.org
4. Typo in version string

### ✅ Solutions

**Check if version exists:**
```powershell
.\tools\discover-published-versions.ps1
```

**Clear NuGet cache:**
```powershell
dotnet nuget locals all --clear
dotnet restore Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests --force
```

**Verify connectivity:**
```powershell
Test-NetConnection api.nuget.org -Port 443
Invoke-RestMethod https://api.nuget.org/v3-flatcontainer/microsoft.azure.cosmos.encryption.custom/index.json
```

**Update to valid version:**
```powershell
# Find valid versions
.\tools\discover-published-versions.ps1

# Update test matrix
.\tools\update-test-matrix.ps1 -Version "1.0.0-preview07"
```

---

## Issue 2: Tests Pass Locally But Fail in CI

### 🔴 Symptoms
- All tests pass on developer machine
- Same tests fail in Azure Pipelines
- Error: "Assembly not found" or "Type not found"

### 🔎 Root Causes
1. **Package cache differences** - Local machine has cached older version
2. **Feed configuration** - CI uses different NuGet sources
3. **Version pinning** - Local Directory.Build.props overrides
4. **Framework mismatches** - Different SDK versions

### ✅ Solutions

**Reproduce CI environment locally:**
```powershell
# Clear all caches (simulate fresh CI)
dotnet nuget locals all --clear
Remove-Item -Recurse -Force bin,obj

# Restore exactly like CI does
dotnet restore --no-cache Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests

# Build exactly like CI does
dotnet build Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests --no-restore

# Test exactly like CI does
dotnet test Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests --no-build
```

**Check for local overrides:**
```powershell
# Look for global.json constraints
Get-Content global.json

# Look for version overrides
Get-ChildItem -Recurse -Filter "Directory.Build.props" | ForEach-Object { 
    Write-Host $_.FullName
    Select-String -Path $_.FullName -Pattern "EncryptionCustomVersion"
}
```

**Verify SDK versions match:**
```powershell
# Local SDK
dotnet --version

# CI SDK (check azure-pipelines YAML)
# Look for: UseDotNet@2 task with version specification
```

---

## Issue 3: Version Resolution Conflict

### 🔴 Symptoms
```
error NU1107: Version conflict detected for Microsoft.Azure.Cosmos.
error NU1608: Detected package version outside of dependency constraint
```

Or at runtime:
```
Could not load file or assembly 'Microsoft.Azure.Cosmos, Version=3.X.X.X'
```

### 🔎 Root Causes
- Transitive dependencies require different Cosmos SDK versions
- Version override not respected by all dependencies
- Package graph has conflicting constraints

### ✅ Solutions

**Analyze dependency tree:**
```powershell
# See full dependency graph
dotnet list Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests package --include-transitive

# Focus on Cosmos packages
dotnet list Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests package --include-transitive | Select-String "Cosmos"
```

**Check version binding redirects:**
```xml
<!-- If needed, add to test project .csproj -->
<PropertyGroup>
  <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
  <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
</PropertyGroup>
```

**Force specific version:**
```powershell
# Test with explicit version
dotnet test -p:TargetEncryptionCustomVersion=1.0.0-preview07 -v detailed
```

**Update Central Package Management:**
```xml
<!-- In Directory.Packages.props, add explicit version for transitive dependency -->
<ItemGroup>
  <PackageVersion Include="Microsoft.Azure.Cosmos" Version="3.43.0" />
</ItemGroup>
```

---

## Issue 4: Tests Hang or Timeout

### 🔴 Symptoms
- Tests start but never complete
- CI job times out (default 60 minutes)
- No output/progress for extended period

### 🔎 Root Causes
1. **Deadlock** - Blocking async calls
2. **Missing ConfigureAwait** - Synchronization context issues
3. **Infinite retry loops** - Network calls never timeout
4. **Resource contention** - Multiple tests fighting for same resource

### ✅ Solutions

**Run with timeout and verbose logging:**
```powershell
# Local debugging with timeout
dotnet test --logger "console;verbosity=detailed" -- NUnit.Timeout=60000
```

**Identify hanging test:**
```powershell
# Run tests one at a time
dotnet test --filter "FullyQualifiedName~CoreApiTests"
dotnet test --filter "FullyQualifiedName~EncryptionDecryptionTests"
dotnet test --filter "FullyQualifiedName~ConfigurationTests"
```

**Check for blocking calls:**
```powershell
# Search for common anti-patterns
Select-String -Path "*.cs" -Pattern "\.Result" -Context 2
Select-String -Path "*.cs" -Pattern "\.Wait\(\)" -Context 2
Select-String -Path "*.cs" -Pattern "Task\.Run\(" -Context 2
```

**Enable deadlock detection:**
```csharp
// In test base class, add:
[SetUp]
public void SetupTimeout()
{
    // Fail after 30 seconds per test
    CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
    TestContext.CurrentContext.CancellationToken = cts.Token;
}
```

---

## Issue 5: API Compatibility Check Fails Unexpectedly

### 🔴 Symptoms
```
ApiCompat: API breaking change detected
ApiCompat: Member 'SomeMethod' exists on the right but not on the left
```

But you know the API hasn't actually changed.

### 🔎 Root Causes
1. **False positive** - Tool detects internal API changes
2. **Assembly resolution** - Tool comparing wrong assemblies
3. **Missing suppression** - Known difference not suppressed
4. **Package vs. source mismatch** - Comparing different builds

### ✅ Solutions

**Verify what's being compared:**
```powershell
# Run with detailed output
.\tools\test-api-compat-local.ps1 -BaselineVersion "1.0.0-preview07" -ComparisonVersion "1.0.0-preview06" -Verbose
```

**Check suppression file:**
```xml
<!-- ApiCompatSuppressions.txt -->
<Suppression>
  <DiagnosticId>CP0001</DiagnosticId>
  <Target>T:Microsoft.Azure.Cosmos.Encryption.Custom.SomeType</Target>
  <Justification>Internal API change, not customer-facing</Justification>
</Suppression>
```

**Compare assemblies manually:**
```powershell
# Download packages
$baselineVer = "1.0.0-preview07"
$comparisonVer = "1.0.0-preview06"
$packageId = "Microsoft.Azure.Cosmos.Encryption.Custom"

# Extract and compare
# (See tools\apicompat-check.ps1 for complete logic)
```

**Add suppression if legitimate:**
```powershell
# Edit Microsoft.Azure.Cosmos.Encryption.Custom\ApiCompatSuppressions.txt
# Add entry for specific diagnostic ID and target
```

---

## Issue 6: "Type Not Found" in Reflection Tests

### 🔴 Symptoms
```
Test Failed: Expected type 'EncryptionContainer' to exist
Test Failed: Method 'CreateEncryptionContainerAsync' not found
```

But the type/method definitely exists in the source code.

### 🔎 Root Causes
- **Assembly not loaded** - Test is looking at wrong assembly
- **Type is internal** - Reflection can't access private types
- **Package vs. project reference** - Testing package doesn't include type
- **Incorrect namespace** - Type moved between versions

### ✅ Solutions

**Verify assembly loading:**
```csharp
// Add diagnostic logging in test
var assembly = typeof(EncryptionContainer).Assembly;
Console.WriteLine($"Assembly: {assembly.FullName}");
Console.WriteLine($"Location: {assembly.Location}");
Console.WriteLine($"Version: {assembly.GetName().Version}");
```

**List all types in assembly:**
```csharp
var allTypes = assembly.GetTypes();
foreach (var type in allTypes.Where(t => t.IsPublic))
{
    Console.WriteLine($"  {type.FullName}");
}
```

**Check visibility:**
```csharp
// Only public types are visible to package consumers
var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic;
var type = assembly.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.InternalType", false);
```

**Accept limitations:**
- **Expected**: Some tests fail when testing packages (23/30 passing)
- **Why**: Packages expose limited public API surface
- **Solution**: Document expected failures in test documentation

---

## Issue 7: Pipeline Doesn't Trigger

### 🔴 Symptoms
- Push code changes but pipeline doesn't run
- Manual trigger works but automatic doesn't
- Pipeline runs but skips compatibility stages

### 🔎 Root Causes
1. **Path filters** - Changes not in monitored paths
2. **Branch filters** - Wrong branch name
3. **YAML syntax error** - Pipeline config invalid
4. **Pipeline disabled** - Admin disabled pipeline
5. **Conditional stages** - Stage conditions not met

### ✅ Solutions

**Check path filters:**
```yaml
# In azure-pipelines-encryption-custom-compatibility.yml
trigger:
  branches:
    include:
      - main
      - master
  paths:
    include:
      - Microsoft.Azure.Cosmos.Encryption.Custom/**
      - tools/**
    exclude:
      - '**/*.md'  # Documentation changes don't trigger
```

**Validate YAML syntax:**
```powershell
# In Azure DevOps, use pipeline editor validation
# Or install Azure CLI:
az pipelines validate --yaml-path azure-pipelines-encryption-custom-compatibility.yml
```

**Check stage conditions:**
```yaml
# Stage 1 only runs on PRs
- stage: QuickCompatibilityCheck
  displayName: 'Quick Compatibility Check (PR only)'
  condition: eq(variables['Build.Reason'], 'PullRequest')
```

**Force trigger for testing:**
```yaml
# Temporarily change trigger to:
trigger:
  branches:
    include:
      - '*'  # All branches
  paths:
    include:
      - '**/*'  # All files
```

**Check pipeline status in Azure DevOps:**
1. Go to Pipelines > [Your Pipeline]
2. Click "..." menu > Settings
3. Verify "Disabled" is not checked
4. Check "Processing" tab for errors

---

## 🛠️ Advanced Debugging

### Collect Diagnostic Logs

```powershell
# Enable binary logs
dotnet build /bl:build.binlog Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests

# Detailed test output
dotnet test --logger "trx;LogFileName=results.trx" --logger "console;verbosity=detailed"

# Dump test results
Get-Content TestResults\*.trx
```

### Blame and Collect Crash Dumps

```powershell
# If tests crash, collect dump
dotnet test --blame --collect:"Code Coverage" --results-directory TestResults
```

### Compare Working vs. Broken State

```powershell
# Diff configurations
git diff HEAD~10 -- testconfig.json
git diff HEAD~10 -- Directory.Packages.props

# Bisect to find breaking commit
git bisect start
git bisect bad HEAD
git bisect good v1.0.0-preview06
# Run tests at each step
dotnet test Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
git bisect good  # or bad
```

---

## 📞 Getting Help

If you're still stuck after trying these solutions:

1. **Check documentation:**
   - [QUICKSTART.md](./QUICKSTART.md) - Basic setup
   - [PIPELINE-GUIDE.md](./PIPELINE-GUIDE.md) - CI/CD troubleshooting
   - [API-CHANGES.md](./API-CHANGES.md) - API compatibility issues

2. **Collect diagnostic info:**
   ```powershell
   # Create support bundle
   dotnet --info > diagnostics.txt
   dotnet list package --include-transitive >> diagnostics.txt
   Get-Content testconfig.json >> diagnostics.txt
   git log --oneline -10 >> diagnostics.txt
   ```

3. **File an issue:**
   - Include error messages (full output)
   - Include diagnostic info
   - Include steps to reproduce
   - Include expected vs. actual behavior

---

## ✅ Prevention Checklist

Avoid issues by following these practices:

- [ ] Always validate versions exist before adding to matrix
- [ ] Clear caches periodically (`dotnet nuget locals all --clear`)
- [ ] Test locally before pushing pipeline changes
- [ ] Keep SDK versions consistent across team
- [ ] Document known limitations and expected failures
- [ ] Review pipeline logs even when tests pass
- [ ] Update dependencies regularly (quarterly)
- [ ] Monitor for new versions monthly

---

**See also:**
- [MAINTENANCE.md](./MAINTENANCE.md) - Ongoing maintenance tasks
- [CHEATSHEET.md](./CHEATSHEET.md) - Quick reference commands
- [QUICKSTART.md](./QUICKSTART.md) - Getting started guide
