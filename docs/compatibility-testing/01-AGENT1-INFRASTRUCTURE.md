# Agent 1: Infrastructure Setup

## Overview

**Goal**: Set up the foundational infrastructure for compatibility testing, including Central Package Management and the new test project structure.

**Estimated Time**: 2-3 hours

**Dependencies**: None (foundational work)

**Deliverables**:
1. `Directory.Packages.props` for centralized version management
2. New test project: `Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests`
3. Project configuration files
4. Version matrix configuration

---

## Task 1: Create Central Package Management Configuration

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Directory.Packages.props`

**Purpose**: Enable centralized control of the package version being tested via MSBuild properties.

**Implementation**:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    
    <!-- Default to current branch version for local development -->
    <!-- Override with -p:TargetEncryptionCustomVersion=x.y.z during CI/testing -->
    <TargetEncryptionCustomVersion Condition="'$(TargetEncryptionCustomVersion)' == ''">$(CustomEncryptionVersion)</TargetEncryptionCustomVersion>
    
    <!-- Track the baseline version for API compatibility checks -->
    <BaselineEncryptionCustomVersion Condition="'$(BaselineEncryptionCustomVersion)' == ''">1.0.0-preview08</BaselineEncryptionCustomVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- The package version under test -->
    <PackageVersion Include="Microsoft.Azure.Cosmos.Encryption.Custom" Version="$(TargetEncryptionCustomVersion)" />
    
    <!-- Test framework dependencies -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageVersion Include="xunit" Version="2.6.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.4" />
    <PackageVersion Include="FluentAssertions" Version="6.12.0" />
    <PackageVersion Include="Moq" Version="4.20.70" />
    
    <!-- Azure SDK dependencies for test setup -->
    <PackageVersion Include="Azure.Identity" Version="1.11.4" />
    <PackageVersion Include="Microsoft.Data.Encryption.Cryptography" Version="1.2.0" />
  </ItemGroup>
</Project>
```

**Key Features**:
- Falls back to current branch version (`$(CustomEncryptionVersion)`) for local dev
- Can be overridden via `-p:TargetEncryptionCustomVersion=1.0.0-preview07`
- Baseline version tracked for API comparison
- Uses xUnit (modern test framework with good CI integration)

---

## Task 2: Create Compatibility Test Project

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.csproj`

**Implementation**:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <RootNamespace>Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests</RootNamespace>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    
    <!-- Display which version is being tested -->
    <TargetEncryptionCustomVersionDisplay Condition="'$(TargetEncryptionCustomVersion)' != ''">$(TargetEncryptionCustomVersion)</TargetEncryptionCustomVersionDisplay>
    <TargetEncryptionCustomVersionDisplay Condition="'$(TargetEncryptionCustomVersion)' == ''">Current Branch</TargetEncryptionCustomVersionDisplay>
  </PropertyGroup>

  <Target Name="DisplayTestVersion" BeforeTargets="Build">
    <Message Importance="High" Text="🔍 Compatibility Testing Against: $(TargetEncryptionCustomVersionDisplay)" />
  </Target>

  <ItemGroup>
    <!-- Package reference WITHOUT Version attribute - managed by Directory.Packages.props -->
    <PackageReference Include="Microsoft.Azure.Cosmos.Encryption.Custom" />
    
    <!-- Test frameworks -->
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Moq" />
    
    <!-- Additional dependencies for test scenarios -->
    <PackageReference Include="Azure.Identity" />
    <PackageReference Include="Microsoft.Data.Encryption.Cryptography" />
  </ItemGroup>

  <ItemGroup>
    <None Update="testconfig.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

**Key Features**:
- **net8.0** test host (can consume netstandard2.0 packages)
- No explicit version on `Microsoft.Azure.Cosmos.Encryption.Custom` reference
- Display message showing which version is being tested
- Consumer-style project (uses package, not project reference)

---

## Task 3: Create Test Configuration File

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/testconfig.json`

**Implementation**:

```json
{
  "testConfig": {
    "description": "Configuration for compatibility tests",
    "testMode": "compatibility",
    "strictMode": false,
    "enableDetailedLogging": true
  },
  "versionMatrix": {
    "description": "Versions to test against in CI",
    "versions": [
      "1.0.0-preview08",
      "1.0.0-preview07",
      "1.0.0-preview06",
      "1.0.0-preview05"
    ],
    "baseline": "1.0.0-preview08"
  },
  "compatibility": {
    "allowedBreakingChanges": [],
    "deprecatedApis": []
  }
}
```

**Purpose**: Centralize test configuration and version matrix.

---

## Task 4: Create Version Matrix Helper

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/VersionMatrix.cs`

**Implementation**:

```csharp
using System.Text.Json;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests;

/// <summary>
/// Manages the version matrix for compatibility testing.
/// </summary>
public static class VersionMatrix
{
    private static readonly Lazy<TestConfig> LazyConfig = new(() => LoadConfig());

    public static TestConfig Config => LazyConfig.Value;

    public static string[] GetTestVersions()
    {
        return Config.VersionMatrix.Versions;
    }

    public static string GetBaselineVersion()
    {
        return Config.VersionMatrix.Baseline;
    }

    private static TestConfig LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "testconfig.json");
        if (!File.Exists(configPath))
        {
            // Fallback to default configuration
            return new TestConfig
            {
                VersionMatrix = new VersionMatrixConfig
                {
                    Baseline = "1.0.0-preview08",
                    Versions = new[] { "1.0.0-preview08", "1.0.0-preview07", "1.0.0-preview06" }
                }
            };
        }

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<TestConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to load test configuration");
    }
}

public class TestConfig
{
    public VersionMatrixConfig VersionMatrix { get; set; } = new();
}

public class VersionMatrixConfig
{
    public string Baseline { get; set; } = string.Empty;
    public string[] Versions { get; set; } = Array.Empty<string>();
}
```

**Purpose**: Programmatic access to version matrix configuration.

---

## Task 5: Create Project README

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/README.md`

**Implementation**:

```markdown
# Compatibility Tests for Microsoft.Azure.Cosmos.Encryption.Custom

## Purpose

This project validates compatibility of the current build against published NuGet package versions. It ensures:

1. **API Compatibility**: No breaking changes in public API surface
2. **Runtime Compatibility**: Behavioral consistency across versions
3. **Consumer Experience**: Tests from a consumer's perspective (package reference, not project reference)

## Project Structure

```
CompatibilityTests/
├── Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.csproj
├── Directory.Packages.props        # Central Package Management
├── testconfig.json                 # Version matrix configuration
├── VersionMatrix.cs                # Version management helper
├── CoreApiTests.cs                 # Core API surface tests
├── EncryptionDecryptionTests.cs    # E2E encryption tests
└── README.md                       # This file
```

## Running Tests Locally

### Test against current branch (default):
```powershell
dotnet test
```

### Test against a specific published version:
```powershell
dotnet test -p:TargetEncryptionCustomVersion=1.0.0-preview07
```

### Test against all versions in matrix:
```powershell
$versions = @("1.0.0-preview08", "1.0.0-preview07", "1.0.0-preview06")
foreach ($version in $versions) {
    Write-Host "Testing version $version" -ForegroundColor Cyan
    dotnet test -p:TargetEncryptionCustomVersion=$version --no-restore
}
```

## CI/CD Integration

The Azure Pipeline `azure-pipelines-encryption-custom-compatibility.yml` automatically:
- Tests against the last published version on every PR
- Runs a full matrix test on scheduled builds
- Fails the build if compatibility breaks

## Adding New Test Versions

Edit `testconfig.json` and add to the `versionMatrix.versions` array:

```json
{
  "versionMatrix": {
    "versions": [
      "1.0.0-preview09",  // Add new version here
      "1.0.0-preview08",
      "1.0.0-preview07"
    ]
  }
}
```

## Troubleshooting

### "Package not found" errors
- Ensure the version exists on NuGet.org or your private feed
- Check NuGet.config is properly configured

### Version resolution issues
- Run `dotnet list package --include-transitive` to see resolved versions
- Clear NuGet cache: `dotnet nuget locals all --clear`

### Test failures
- Check if the failure is legitimate (breaking change) or test issue
- Compare behavior between versions manually
- Update tests if API intentionally changed
```

---

## Task 6: Update Solution File

### Action: Add the new project to the solution

**Command**:
```powershell
cd C:\repos\azure-cosmos-dotnet-v3
dotnet sln Microsoft.Azure.Cosmos.sln add `
  Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.csproj
```

**Verification**:
```powershell
dotnet sln Microsoft.Azure.Cosmos.sln list
```

---

## Task 7: Create Build Script for Local Testing

### File: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/test-compatibility.ps1`

**Implementation**:

```powershell
<#
.SYNOPSIS
    Local compatibility testing script
.DESCRIPTION
    Tests the Encryption.Custom library against published NuGet versions
.PARAMETER Version
    Specific version to test against. If omitted, tests against all versions in testconfig.json
.PARAMETER CurrentOnly
    Test against current branch build only
.EXAMPLE
    .\test-compatibility.ps1 -Version "1.0.0-preview08"
.EXAMPLE
    .\test-compatibility.ps1
#>

param(
    [string]$Version,
    [switch]$CurrentOnly
)

$ErrorActionPreference = "Stop"
$testProject = "Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests"
$testPath = Join-Path $PSScriptRoot $testProject

Write-Host "🧪 Compatibility Testing for Encryption.Custom" -ForegroundColor Cyan
Write-Host ""

if ($CurrentOnly) {
    Write-Host "Testing against current branch build..." -ForegroundColor Yellow
    dotnet test $testPath --configuration Release --logger "console;verbosity=normal"
    exit $LASTEXITCODE
}

if ($Version) {
    Write-Host "Testing against version: $Version" -ForegroundColor Yellow
    dotnet test $testPath -p:TargetEncryptionCustomVersion=$Version --configuration Release --logger "console;verbosity=normal"
    exit $LASTEXITCODE
}

# Test against all versions in matrix
$configPath = Join-Path $testPath "testconfig.json"
$config = Get-Content $configPath | ConvertFrom-Json
$versions = $config.versionMatrix.versions

Write-Host "Testing against $($versions.Count) versions from matrix:" -ForegroundColor Yellow
$versions | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
Write-Host ""

$failed = @()
$passed = @()

foreach ($ver in $versions) {
    Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "Testing version: $ver" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
    
    dotnet test $testPath -p:TargetEncryptionCustomVersion=$ver --configuration Release --logger "console;verbosity=minimal" --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        $passed += $ver
        Write-Host "✅ PASSED: $ver" -ForegroundColor Green
    } else {
        $failed += $ver
        Write-Host "❌ FAILED: $ver" -ForegroundColor Red
    }
    Write-Host ""
}

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Passed: $($passed.Count)" -ForegroundColor Green
Write-Host "Failed: $($failed.Count)" -ForegroundColor Red

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed versions:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "✅ All compatibility tests passed!" -ForegroundColor Green
exit 0
```

---

## Verification Steps

After completing all tasks, verify:

1. **Project builds successfully**:
   ```powershell
   dotnet build Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
   ```

2. **Central Package Management works**:
   ```powershell
   dotnet list Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests package
   ```
   Should show the correct version of `Microsoft.Azure.Cosmos.Encryption.Custom`

3. **Version override works**:
   ```powershell
   dotnet build -p:TargetEncryptionCustomVersion=1.0.0-preview07
   dotnet list package
   ```
   Should show preview07

4. **Project appears in solution**:
   ```powershell
   dotnet sln list | Select-String "CompatibilityTests"
   ```

---

## Blockers & Dependencies

**None** - This is foundational work with no dependencies.

**Blocks**: Agents 2, 3, 4, and 6 depend on this infrastructure being in place.

---

## Next Steps

Once this infrastructure is complete, parallel work can begin on:
- **Agent 2**: Implementing actual compatibility tests
- **Agent 3**: Creating the Azure Pipeline
- **Agent 4**: Integrating API compatibility checks
- **Agent 6**: Advanced side-by-side testing (optional)

---

## File Summary

Files created by Agent 1:
```
Microsoft.Azure.Cosmos.Encryption.Custom/tests/
├── Directory.Packages.props
├── test-compatibility.ps1
└── Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/
    ├── Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.csproj
    ├── testconfig.json
    ├── VersionMatrix.cs
    └── README.md
```

**Total**: 6 files
