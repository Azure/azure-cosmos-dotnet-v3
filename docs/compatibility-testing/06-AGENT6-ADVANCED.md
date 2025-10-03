# Agent 6: Advanced Features (Optional)

## Overview

**Goal**: Implement advanced side-by-side testing capabilities for A/B comparison of multiple versions in the same process.

**Estimated Time**: 4-6 hours

**Dependencies**: Agent 1 (Infrastructure)

**Priority**: **OPTIONAL** - Only implement if needed for specific scenarios

**Deliverables**:

1. Assembly Load Context (ALC) infrastructure
2. Side-by-side version loader
3. Behavioral comparison tests
4. Documentation

---

## When to Use Side-by-Side Testing

### ✅ Use Cases

- **Data Migration**: Verify encrypted data written by old version can be read by new version
- **Behavioral Parity**: Compare exact behavior between versions (e.g., encryption output, error messages)
- **Regression Testing**: Ensure bug fixes don't change other behavior
- **Performance Comparison**: Benchmark old vs new version

### ❌ Don't Use For

- **API Surface Validation**: Use ApiCompat tool instead (Agent 4)
- **Basic Compatibility**: Use standard tests (Agent 2)
- **CI/PR Checks**: Too slow and complex for quick feedback

---

## Task 1: Assembly Load Context Infrastructure

### File: `Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/SideBySide/IsolatedLoadContext.cs`

```csharp
using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.SideBySide;

/// <summary>
/// Custom AssemblyLoadContext for loading multiple versions of the same assembly in isolation.
/// Based on Microsoft's plugin pattern: https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
/// </summary>
public sealed class IsolatedLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public IsolatedLoadContext(string mainAssemblyPath, string contextName)
        : base(name: contextName, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Resolve dependencies using the resolver
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Allow shared assemblies (like System.* or Microsoft.Extensions.*) to be resolved from default context
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
```

---

## Task 2: Version Loader Utility

### File: `Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/SideBySide/VersionLoader.cs`

```csharp
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.SideBySide;

/// <summary>
/// Utility for loading specific versions of the Encryption.Custom library from NuGet packages.
/// </summary>
public class VersionLoader : IDisposable
{
    private readonly IsolatedLoadContext _loadContext;
    private readonly Assembly _assembly;
    
    public string Version { get; }
    public Assembly Assembly => _assembly;

    private VersionLoader(string version, IsolatedLoadContext loadContext, Assembly assembly)
    {
        Version = version;
        _loadContext = loadContext;
        _assembly = assembly;
    }

    /// <summary>
    /// Loads a specific version of the library from the NuGet global packages folder.
    /// </summary>
    public static VersionLoader Load(string version, string? targetFramework = null)
    {
        targetFramework ??= "netstandard2.0";
        
        // Find the package in NuGet global packages folder
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nugetPackagesPath = Path.Combine(userProfile, ".nuget", "packages");
        
        var packagePath = Path.Combine(
            nugetPackagesPath,
            "microsoft.azure.cosmos.encryption.custom",
            version,
            "lib",
            targetFramework,
            "Microsoft.Azure.Cosmos.Encryption.Custom.dll");

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException(
                $"Package version {version} not found at {packagePath}. " +
                $"Ensure the package is restored: dotnet restore -p:TargetEncryptionCustomVersion={version}",
                packagePath);
        }

        // Create isolated load context
        var contextName = $"EncryptionCustom-{version}";
        var loadContext = new IsolatedLoadContext(packagePath, contextName);
        
        // Load the assembly
        var assembly = loadContext.LoadFromAssemblyPath(packagePath);

        return new VersionLoader(version, loadContext, assembly);
    }

    /// <summary>
    /// Creates an instance of a type from the loaded assembly.
    /// </summary>
    public dynamic CreateInstance(string typeName, params object[] args)
    {
        var type = _assembly.GetType(typeName)
            ?? throw new TypeLoadException($"Type '{typeName}' not found in assembly {_assembly.FullName}");

        var instance = Activator.CreateInstance(type, args)
            ?? throw new InvalidOperationException($"Failed to create instance of '{typeName}'");

        return instance;
    }

    /// <summary>
    /// Gets a type from the loaded assembly.
    /// </summary>
    public Type GetType(string typeName)
    {
        return _assembly.GetType(typeName)
            ?? throw new TypeLoadException($"Type '{typeName}' not found in assembly {_assembly.FullName}");
    }

    public void Dispose()
    {
        _loadContext?.Unload();
    }
}
```

---

## Task 3: Side-by-Side Comparison Tests

### File: `Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/SideBySide/SideBySideTests.cs`

```csharp
using System;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.SideBySide;

/// <summary>
/// Advanced tests that load multiple versions side-by-side for behavioral comparison.
/// These tests are slower and more complex - use only when necessary.
/// </summary>
[Trait("Category", "SideBySide")]
[Trait("Speed", "Slow")]
public class SideBySideTests : CompatibilityTestBase
{
    public SideBySideTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TwoVersions_CanLoadSideBySide_Successfully()
    {
        // Arrange
        var version1 = "1.0.0-preview07";
        var version2 = "1.0.0-preview08";

        // Act
        using var loader1 = VersionLoader.Load(version1);
        using var loader2 = VersionLoader.Load(version2);

        // Assert
        loader1.Assembly.Should().NotBeNull();
        loader2.Assembly.Should().NotBeNull();
        loader1.Assembly.Should().NotBeSameAs(loader2.Assembly);
        
        LogInfo($"✓ Loaded {version1} and {version2} side-by-side");
        LogInfo($"  Assembly 1: {loader1.Assembly.FullName}");
        LogInfo($"  Assembly 2: {loader2.Assembly.FullName}");
    }

    [Fact]
    public void EncryptionType_Values_AreConsistent_AcrossVersions()
    {
        // Arrange
        var versions = new[] { "1.0.0-preview06", "1.0.0-preview07", "1.0.0-preview08" };
        
        // Expected values that must never change for serialization compatibility
        const int expectedDeterministic = 1;
        const int expectedRandomized = 2;

        foreach (var version in versions)
        {
            using var loader = VersionLoader.Load(version);
            
            // Act
            var encryptionType = loader.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.EncryptionType");
            var deterministicValue = (int)Enum.Parse(encryptionType, "Deterministic");
            var randomizedValue = (int)Enum.Parse(encryptionType, "Randomized");

            // Assert
            deterministicValue.Should().Be(expectedDeterministic,
                $"Deterministic value in {version} must be {expectedDeterministic} for wire format compatibility");
            randomizedValue.Should().Be(expectedRandomized,
                $"Randomized value in {version} must be {expectedRandomized} for wire format compatibility");
            
            LogInfo($"✓ {version}: Deterministic={deterministicValue}, Randomized={randomizedValue}");
        }
    }

    [Fact]
    public void DataEncryptionKeyProperties_Structure_IsCompatible_AcrossVersions()
    {
        // Arrange
        var oldVersion = "1.0.0-preview06";
        var newVersion = "1.0.0-preview08";

        using var oldLoader = VersionLoader.Load(oldVersion);
        using var newLoader = VersionLoader.Load(newVersion);

        // Act
        var oldType = oldLoader.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyProperties");
        var newType = newLoader.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyProperties");

        var oldProperties = oldType.GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        var newProperties = newType.GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        // Assert - new version should be superset of old (can add, but shouldn't remove)
        var missingProperties = oldProperties.Except(newProperties).ToArray();
        
        missingProperties.Should().BeEmpty(
            $"Properties removed from DataEncryptionKeyProperties between {oldVersion} and {newVersion}: {string.Join(", ", missingProperties)}");
        
        LogInfo($"✓ All properties from {oldVersion} exist in {newVersion}");
        
        var addedProperties = newProperties.Except(oldProperties).ToArray();
        if (addedProperties.Any())
        {
            LogInfo($"  New properties in {newVersion}: {string.Join(", ", addedProperties)}");
        }
    }

    [Theory]
    [InlineData("1.0.0-preview06", "1.0.0-preview07")]
    [InlineData("1.0.0-preview07", "1.0.0-preview08")]
    public void PublicApiSurface_IsBackwardCompatible_BetweenVersions(string olderVersion, string newerVersion)
    {
        // Arrange
        using var olderLoader = VersionLoader.Load(olderVersion);
        using var newerLoader = VersionLoader.Load(newerVersion);

        var criticalTypes = new[]
        {
            "Microsoft.Azure.Cosmos.Encryption.Custom.CosmosEncryptionClient",
            "Microsoft.Azure.Cosmos.Encryption.Custom.DatabaseCore",
            "Microsoft.Azure.Cosmos.Encryption.Custom.ContainerCore",
            "Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyProperties",
            "Microsoft.Azure.Cosmos.Encryption.Custom.IKeyEncryptionKeyResolver"
        };

        foreach (var typeName in criticalTypes)
        {
            // Act
            var olderType = olderLoader.GetType(typeName);
            var newerType = newerLoader.GetType(typeName);

            // Assert
            olderType.Should().NotBeNull($"{typeName} should exist in {olderVersion}");
            newerType.Should().NotBeNull($"{typeName} should exist in {newerVersion}");
            
            // Check public methods haven't been removed
            var olderPublicMethods = olderType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(m => m.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();
            
            var newerPublicMethods = newerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(m => m.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();

            var removedMethods = olderPublicMethods.Except(newerPublicMethods).ToArray();
            
            removedMethods.Should().BeEmpty(
                $"Public methods removed from {typeName}: {string.Join(", ", removedMethods)}");
            
            LogInfo($"✓ {typeName}: No methods removed between {olderVersion} and {newerVersion}");
        }
    }
}
```

---

## Task 4: Package Downloader Helper

### File: `tools/download-package-version.ps1`

```powershell
<#
.SYNOPSIS
    Downloads a specific version of the package to a local directory for SxS testing
.PARAMETER Version
    Package version to download
.PARAMETER OutputPath
    Where to extract the package (default: ./packages-sxs)
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$OutputPath = ".\packages-sxs"
)

$ErrorActionPreference = "Stop"

Write-Host "📦 Downloading Package for Side-by-Side Testing" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host ""

$packageId = "Microsoft.Azure.Cosmos.Encryption.Custom"

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
}

# Download package
Write-Host "Downloading $packageId version $Version..." -ForegroundColor Gray

nuget install $packageId `
    -Version $Version `
    -OutputDirectory $OutputPath `
    -NonInteractive `
    -PackageSaveMode nuspec

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to download package"
}

$packageDir = Join-Path $OutputPath "$packageId.$Version"
$dllPath = Join-Path $packageDir "lib\netstandard2.0\$packageId.dll"

if (Test-Path $dllPath) {
    Write-Host ""
    Write-Host "✅ Package downloaded successfully" -ForegroundColor Green
    Write-Host "   Location: $packageDir" -ForegroundColor Gray
    Write-Host "   DLL: $dllPath" -ForegroundColor Gray
    Write-Host ""
    
    # Display assembly info
    $assembly = [Reflection.Assembly]::LoadFrom($dllPath)
    Write-Host "Assembly Information:" -ForegroundColor Cyan
    Write-Host "   Full Name: $($assembly.FullName)" -ForegroundColor Gray
    Write-Host "   Version: $($assembly.GetName().Version)" -ForegroundColor Gray
    Write-Host "   Location: $($assembly.Location)" -ForegroundColor Gray
} else {
    Write-Error "DLL not found at expected location: $dllPath"
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
```

---

## Task 5: SxS Testing Documentation

### File: `Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/SideBySide/README.md`

```markdown
# Side-by-Side (SxS) Compatibility Testing

## Overview

Side-by-side tests load multiple versions of the library in the same process using `AssemblyLoadContext` for advanced behavioral comparison.

## When to Use

✅ **Use SxS testing for**:

- Verifying data migration scenarios
- Comparing exact behavior between versions
- Testing wire format compatibility
- Regression testing for subtle behavior changes

❌ **Don't use SxS testing for**:

- Basic API surface validation (use ApiCompat)
- Standard compatibility checks (use regular tests)
- Fast PR feedback (too slow)

## Architecture

```
┌─────────────────────────────────────┐
│  Test Host Process (net8.0)        │
│                                     │
│  ┌───────────────────────────────┐ │
│  │ Default Load Context          │ │
│  │ (Test Framework, xUnit, etc.) │ │
│  └───────────────────────────────┘ │
│                                     │
│  ┌───────────────────────────────┐ │
│  │ IsolatedLoadContext "v07"     │ │
│  │ ├─ Encryption.Custom 1.0.0-07 │ │
│  │ ├─ Microsoft.Azure.Cosmos 3.x │ │
│  │ └─ Dependencies...            │ │
│  └───────────────────────────────┘ │
│                                     │
│  ┌───────────────────────────────┐ │
│  │ IsolatedLoadContext "v08"     │ │
│  │ ├─ Encryption.Custom 1.0.0-08 │ │
│  │ ├─ Microsoft.Azure.Cosmos 3.x │ │
│  │ └─ Dependencies...            │ │
│  └───────────────────────────────┘ │
└─────────────────────────────────────┘
```

## Usage

### Basic Example

```csharp
[Fact]
public void CompareVersions()
{
    using var v7 = VersionLoader.Load("1.0.0-preview07");
    using var v8 = VersionLoader.Load("1.0.0-preview08");
    
    // Compare types, methods, behavior
    var type7 = v7.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.CosmosEncryptionClient");
    var type8 = v8.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.CosmosEncryptionClient");
    
    type7.Should().NotBeNull();
    type8.Should().NotBeNull();
}
```

### Creating Instances

```csharp
using var loader = VersionLoader.Load("1.0.0-preview08");

// Option 1: Using dynamic
dynamic instance = loader.CreateInstance(
    "Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyProperties",
    args...);
    
// Option 2: Using reflection
var type = loader.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyProperties");
var instance = Activator.CreateInstance(type, args...);
```

## Prerequisites

### 1. Ensure Packages are Restored

```powershell
# Restore specific versions to NuGet global cache
dotnet restore -p:TargetEncryptionCustomVersion=1.0.0-preview07
dotnet restore -p:TargetEncryptionCustomVersion=1.0.0-preview08
```

### 2. Verify Package Location

Packages are expected in:

- **Windows**: `%USERPROFILE%\.nuget\packages\`
- **macOS/Linux**: `~/.nuget/packages/`

Override with `NUGET_PACKAGES` environment variable if needed.

## Running SxS Tests

```powershell
# Run all SxS tests
dotnet test --filter "Category=SideBySide"

# Run specific test
dotnet test --filter "FullyQualifiedName~SideBySideTests.TwoVersions_CanLoadSideBySide_Successfully"
```

## Troubleshooting

### Issue: "Package version not found"

**Solution**: Restore the package first:

```powershell
dotnet restore -p:TargetEncryptionCustomVersion=1.0.0-preview07
```

### Issue: "Type not found in assembly"

**Solution**: Check the type's full name (namespace + type name):

```csharp
// List all types in assembly
var allTypes = loader.Assembly.GetTypes();
foreach (var t in allTypes)
{
    Console.WriteLine(t.FullName);
}
```

### Issue: "Assembly load conflict"

**Solution**: Ensure assemblies are loaded in isolated contexts and not mixed with default context.

## Limitations

1. **Performance**: SxS tests are slower than regular tests
2. **Complexity**: Requires understanding of AssemblyLoadContext
3. **Shared Types**: Types can't be directly compared across contexts (use reflection)
4. **Memory**: Each context holds full assembly graph in memory

## Best Practices

1. **Use sparingly** - Only when regular tests aren't sufficient
2. **Always dispose** - Use `using` blocks for VersionLoader
3. **Cache loaders** - Reuse VersionLoader instances in test fixtures
4. **Document expectations** - Explain what behavioral difference you're testing for
5. **Keep tests focused** - One comparison per test

## Advanced Scenarios

### Comparing Serialization Output

```csharp
[Fact]
public void Encryption_Output_IsCompatible()
{
    using var oldVersion = VersionLoader.Load("1.0.0-preview07");
    using var newVersion = VersionLoader.Load("1.0.0-preview08");
    
    // Encrypt with old version
    var oldResult = /* ... encrypt using old version ... */;
    
    // Decrypt with new version
    var newResult = /* ... decrypt using new version ... */;
    
    // Verify compatibility
    newResult.Should().Be(oldResult);
}
```

### Performance Comparison

```csharp
[Fact]
public void NewVersion_NotSlowerThan_OldVersion()
{
    using var oldVersion = VersionLoader.Load("1.0.0-preview07");
    using var newVersion = VersionLoader.Load("1.0.0-preview08");
    
    var oldTime = MeasurePerformance(oldVersion);
    var newTime = MeasurePerformance(newVersion);
    
    newTime.Should().BeLessThanOrEqualTo(oldTime * 1.1, // Allow 10% regression
        "New version should not be significantly slower");
}
```

## References

- [.NET Assembly Loading](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/overview)
- [Creating Apps with Plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
- [AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)
```

---

## Task 6: Integration with Main Test Suite

Update `testconfig.json` to include SxS configuration:

```json
{
  "testConfig": {
    "enableSideBySideTesting": false,
    "sideBySideVersionPairs": [
      ["1.0.0-preview07", "1.0.0-preview08"],
      ["1.0.0-preview06", "1.0.0-preview08"]
    ]
  }
}
```

---

## Verification

- [ ] IsolatedLoadContext loads assemblies successfully
- [ ] VersionLoader can load multiple versions simultaneously
- [ ] Tests pass locally
- [ ] Documentation is clear
- [ ] Performance is acceptable (tests complete in <5 minutes)

---

## Performance Considerations

### Expected Timings

- **Regular tests**: ~30 seconds
- **SxS tests**: ~2-5 minutes (due to multiple assembly loads)

### Optimization Tips

1. **Cache loaders** in test fixtures
2. **Minimize versions loaded** - only test necessary pairs
3. **Run SxS tests separately** from main suite
4. **Use parallel test execution** carefully (high memory usage)

---

## When to Skip This Agent

Skip Agent 6 if:

- You only need API surface validation
- Standard compatibility tests are sufficient
- CI/CD time budget is tight
- Team doesn't have ALC expertise

You can always add SxS testing later if needed.

---

## Files Created

```
Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/
└── SideBySide/
    ├── IsolatedLoadContext.cs
    ├── VersionLoader.cs
    ├── SideBySideTests.cs
    └── README.md
tools/
└── download-package-version.ps1
```

**Total**: 5 files

---

## Summary

Agent 6 provides advanced testing capabilities but is **optional**. Implement only if you need:

- Data migration validation
- Exact behavioral parity testing
- Performance regression detection
- Wire format compatibility verification

For most scenarios, **Agents 1-5 are sufficient**.
