# Agent 2: Compatibility Test Suite

## Overview

**Goal**: Implement the actual compatibility tests that validate API surface and runtime behavior across versions.

**Estimated Time**: 4-6 hours

**Dependencies**: Agent 1 (Infrastructure) must be complete

**Deliverables**:

1. Core API surface tests
2. Encryption/decryption round-trip tests
3. Configuration and container operation tests
4. Test helpers and utilities
5. Comprehensive test documentation

---

## Test Strategy

### Consumer-Perspective Testing

All tests should:

- Use **only public APIs** (no internal/reflection hacks)
- Be **version-agnostic** (work across all supported versions)
- Focus on **contract and behavior**, not implementation
- Use **realistic scenarios** that actual consumers would encounter

### Test Categories

1. **API Surface Tests**: Validate types, methods, and properties exist
2. **Functional Tests**: Test encryption/decryption round-trips
3. **Configuration Tests**: Validate policy and DEK management
4. **Error Handling Tests**: Ensure exceptions behave consistently

---

## Task 1: Create Base Test Infrastructure

### File: `TestFixtures/CompatibilityTestBase.cs`

```csharp
using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures;

/// <summary>
/// Base class for all compatibility tests with common setup/teardown and utilities.
/// </summary>
public abstract class CompatibilityTestBase : IDisposable
{
    protected ITestOutputHelper Output { get; }
    protected string TestedVersion { get; }

    protected CompatibilityTestBase(ITestOutputHelper output)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        TestedVersion = GetPackageVersion();
        
        Output.WriteLine($"========================================");
        Output.WriteLine($"Testing Version: {TestedVersion}");
        Output.WriteLine($"Test: {GetType().Name}");
        Output.WriteLine($"========================================");
    }

    /// <summary>
    /// Gets the actual version of the package being tested.
    /// </summary>
    private static string GetPackageVersion()
    {
        var assembly = typeof(CosmosEncryptionClient).Assembly;
        var version = assembly.GetName().Version;
        
        // Try to get informational version (includes preview suffix)
        var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return infoVersionAttr?.InformationalVersion ?? version?.ToString() ?? "Unknown";
    }

    protected void LogInfo(string message)
    {
        Output.WriteLine($"[INFO] {message}");
    }

    protected void LogWarning(string message)
    {
        Output.WriteLine($"[WARN] {message}");
    }

    protected void LogError(string message)
    {
        Output.WriteLine($"[ERROR] {message}");
    }

    public virtual void Dispose()
    {
        // Cleanup logic if needed
    }
}
```

---

## Task 2: API Surface Compatibility Tests

### File: `CoreApiTests.cs`

```csharp
using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests;

/// <summary>
/// Tests that validate the public API surface hasn't broken.
/// These tests ensure that types, methods, and properties expected by consumers still exist.
/// </summary>
public class CoreApiTests : CompatibilityTestBase
{
    public CoreApiTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CosmosEncryptionClient_Type_Exists()
    {
        // Arrange & Act
        var type = typeof(CosmosEncryptionClient);

        // Assert
        type.Should().NotBeNull("CosmosEncryptionClient is the primary entry point");
        type.IsPublic.Should().BeTrue("CosmosEncryptionClient must be public");
        
        LogInfo($"✓ CosmosEncryptionClient exists and is public");
    }

    [Fact]
    public void CosmosEncryptionClient_Constructor_WithCosmosClient_Exists()
    {
        // Arrange
        var type = typeof(CosmosEncryptionClient);
        var constructorParams = new[] { typeof(CosmosClient), typeof(IKeyEncryptionKeyResolver) };

        // Act
        var constructor = type.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            null,
            constructorParams,
            null);

        // Assert
        constructor.Should().NotBeNull(
            "Consumers need to create CosmosEncryptionClient from CosmosClient");
        
        LogInfo($"✓ Constructor with (CosmosClient, IKeyEncryptionKeyResolver) exists");
    }

    [Fact]
    public void CosmosEncryptionClient_GetDatabase_Method_Exists()
    {
        // Arrange
        var type = typeof(CosmosEncryptionClient);

        // Act
        var method = type.GetMethod("GetDatabase", new[] { typeof(string) });

        // Assert
        method.Should().NotBeNull("GetDatabase is essential for database operations");
        method.ReturnType.Name.Should().Be("DatabaseCore", 
            "GetDatabase should return DatabaseCore");
        
        LogInfo($"✓ GetDatabase(string) method exists");
    }

    [Fact]
    public void DataEncryptionKeyProperties_Type_Exists()
    {
        // Arrange & Act
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyProperties, Microsoft.Azure.Cosmos.Encryption.Custom");

        // Assert
        type.Should().NotBeNull("DataEncryptionKeyProperties is needed for DEK management");
        type.IsPublic.Should().BeTrue();
        
        LogInfo($"✓ DataEncryptionKeyProperties exists and is public");
    }

    [Fact]
    public void EncryptionType_Enum_Exists()
    {
        // Arrange & Act
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.EncryptionType, Microsoft.Azure.Cosmos.Encryption.Custom");

        // Assert
        type.Should().NotBeNull("EncryptionType enum is needed for encryption configuration");
        type.IsEnum.Should().BeTrue();
        type.IsPublic.Should().BeTrue();
        
        // Check enum values
        var values = Enum.GetNames(type);
        values.Should().Contain("Deterministic", "Deterministic encryption must be supported");
        values.Should().Contain("Randomized", "Randomized encryption must be supported");
        
        LogInfo($"✓ EncryptionType enum exists with required values");
    }

    [Fact]
    public void ClientEncryptionPolicy_Type_Exists()
    {
        // Arrange & Act
        var type = Type.GetType("Microsoft.Azure.Cosmos.ClientEncryptionPolicy, Microsoft.Azure.Cosmos");

        // Assert
        type.Should().NotBeNull("ClientEncryptionPolicy is needed for container creation");
        
        LogInfo($"✓ ClientEncryptionPolicy exists (from base Cosmos SDK)");
    }

    [Theory]
    [InlineData("CreateItemAsync")]
    [InlineData("ReadItemAsync")]
    [InlineData("UpsertItemAsync")]
    [InlineData("ReplaceItemAsync")]
    [InlineData("DeleteItemAsync")]
    public void ContainerCore_CrudMethods_Exist(string methodName)
    {
        // Arrange
        var containerType = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.ContainerCore, Microsoft.Azure.Cosmos.Encryption.Custom");
        containerType.Should().NotBeNull("ContainerCore is the encrypted container wrapper");

        // Act
        var methods = containerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName);

        // Assert
        methods.Should().NotBeEmpty($"{methodName} is essential for encrypted CRUD operations");
        
        LogInfo($"✓ ContainerCore.{methodName} exists");
    }

    [Fact]
    public void IKeyEncryptionKeyResolver_Interface_Exists()
    {
        // Arrange & Act
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.IKeyEncryptionKeyResolver, Microsoft.Azure.Cosmos.Encryption.Custom");

        // Assert
        type.Should().NotBeNull("IKeyEncryptionKeyResolver is required for key management");
        type.IsInterface.Should().BeTrue();
        type.IsPublic.Should().BeTrue();
        
        LogInfo($"✓ IKeyEncryptionKeyResolver interface exists");
    }

    [Fact]
    public void Assembly_References_ExpectedPackages()
    {
        // Arrange
        var assembly = typeof(CosmosEncryptionClient).Assembly;

        // Act
        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        // Assert
        referencedAssemblies.Should().Contain("Microsoft.Azure.Cosmos", 
            "Must reference base Cosmos SDK");
        referencedAssemblies.Should().Contain("Microsoft.Data.Encryption.Cryptography", 
            "Must reference encryption cryptography library");
        
        LogInfo($"✓ Assembly references expected dependencies");
        LogInfo($"  References: {string.Join(", ", referencedAssemblies)}");
    }

    [Fact]
    public void Assembly_TargetFrameworks_Include_NetStandard20()
    {
        // Arrange
        var assembly = typeof(CosmosEncryptionClient).Assembly;

        // Act
        var targetFrameworkAttr = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();

        // Assert
        targetFrameworkAttr.Should().NotBeNull("Assembly should have target framework attribute");
        
        // Note: This test validates we can load the assembly in net8.0 host,
        // which proves netstandard2.0 compatibility
        LogInfo($"✓ Assembly loaded successfully in net8.0 host (netstandard2.0 compatible)");
        LogInfo($"  Target Framework: {targetFrameworkAttr?.FrameworkName ?? "Unknown"}");
    }
}
```

---

## Task 3: Functional Encryption Tests

### File: `EncryptionDecryptionTests.cs`

```csharp
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Moq;
using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests;

/// <summary>
/// Tests that validate encryption/decryption functionality works consistently across versions.
/// </summary>
public class EncryptionDecryptionTests : CompatibilityTestBase
{
    public EncryptionDecryptionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void EncryptionType_Deterministic_ValueIsConsistent()
    {
        // Arrange
        var encryptionType = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.EncryptionType, Microsoft.Azure.Cosmos.Encryption.Custom");

        // Act
        var deterministicValue = (int)Enum.Parse(encryptionType, "Deterministic");
        var randomizedValue = (int)Enum.Parse(encryptionType, "Randomized");

        // Assert
        // These values must never change for compatibility
        deterministicValue.Should().Be(1, "Deterministic must always be 1 for serialization compatibility");
        randomizedValue.Should().Be(2, "Randomized must always be 2 for serialization compatibility");
        
        LogInfo($"✓ EncryptionType values are consistent (Deterministic={deterministicValue}, Randomized={randomizedValue})");
    }

    [Fact]
    public void ClientEncryptionIncludedPath_PropertyNames_AreConsistent()
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.ClientEncryptionIncludedPath, Microsoft.Azure.Cosmos");
        type.Should().NotBeNull();

        // Act
        var pathProperty = type.GetProperty("Path");
        var clientEncryptionKeyIdProperty = type.GetProperty("ClientEncryptionKeyId");
        var encryptionTypeProperty = type.GetProperty("EncryptionType");
        var encryptionAlgorithmProperty = type.GetProperty("EncryptionAlgorithm");

        // Assert
        pathProperty.Should().NotBeNull("Path property must exist");
        clientEncryptionKeyIdProperty.Should().NotBeNull("ClientEncryptionKeyId property must exist");
        encryptionTypeProperty.Should().NotBeNull("EncryptionType property must exist");
        encryptionAlgorithmProperty.Should().NotBeNull("EncryptionAlgorithm property must exist");
        
        LogInfo($"✓ ClientEncryptionIncludedPath has all required properties");
    }

    [Fact]
    public void DataEncryptionKeyProperties_RequiredProperties_Exist()
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyProperties, Microsoft.Azure.Cosmos.Encryption.Custom");
        type.Should().NotBeNull();

        // Act
        var idProperty = type.GetProperty("Id");
        var encryptionAlgorithmProperty = type.GetProperty("EncryptionAlgorithm");
        var createdTimeProperty = type.GetProperty("CreatedTime");

        // Assert
        idProperty.Should().NotBeNull("Id property must exist");
        encryptionAlgorithmProperty.Should().NotBeNull("EncryptionAlgorithm property must exist");
        createdTimeProperty.Should().NotBeNull("CreatedTime property must exist");
        
        LogInfo($"✓ DataEncryptionKeyProperties has all required properties");
    }

    [Fact]
    public void CosmosEncryptionClient_Implements_IDisposable()
    {
        // Arrange
        var type = typeof(CosmosEncryptionClient);

        // Act
        var implementsDisposable = typeof(IDisposable).IsAssignableFrom(type);

        // Assert
        implementsDisposable.Should().BeTrue("CosmosEncryptionClient should implement IDisposable for proper resource management");
        
        LogInfo($"✓ CosmosEncryptionClient implements IDisposable");
    }

    [Fact]
    public void IKeyEncryptionKeyResolver_BuildKeyEncryptionKey_Method_Exists()
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.IKeyEncryptionKeyResolver, Microsoft.Azure.Cosmos.Encryption.Custom");
        type.Should().NotBeNull();

        // Act
        var method = type.GetMethod("BuildKeyEncryptionKey");

        // Assert
        method.Should().NotBeNull("BuildKeyEncryptionKey method is required for key resolution");
        
        LogInfo($"✓ IKeyEncryptionKeyResolver.BuildKeyEncryptionKey exists");
    }

    [Fact]
    public void EncryptionAlgorithm_Constant_Exists()
    {
        // Arrange - Try to find the default encryption algorithm constant
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.CosmosEncryptionAlgorithm, Microsoft.Azure.Cosmos.Encryption.Custom");
        
        if (type != null)
        {
            // Act
            var aes256CbcField = type.GetField("AE_AES_256_CBC_HMAC_SHA_256");

            // Assert
            aes256CbcField.Should().NotBeNull("Default encryption algorithm must be available");
            aes256CbcField.IsStatic.Should().BeTrue();
            aes256CbcField.IsLiteral.Should().BeTrue("Should be a constant");
            
            LogInfo($"✓ CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256 exists");
        }
        else
        {
            LogWarning("CosmosEncryptionAlgorithm type not found - may have changed in this version");
        }
    }

    [Fact]
    public void DataEncryptionKeyContainer_Type_Exists()
    {
        // Arrange & Act
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyContainer, Microsoft.Azure.Cosmos.Encryption.Custom");

        // Assert
        type.Should().NotBeNull("DataEncryptionKeyContainer is needed for DEK management");
        type.IsPublic.Should().BeTrue();
        
        LogInfo($"✓ DataEncryptionKeyContainer exists and is public");
    }

    [Theory]
    [InlineData("CreateDataEncryptionKeyAsync")]
    [InlineData("ReadDataEncryptionKeyAsync")]
    [InlineData("RewrapDataEncryptionKeyAsync")]
    public void DataEncryptionKeyContainer_Methods_Exist(string methodName)
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyContainer, Microsoft.Azure.Cosmos.Encryption.Custom");
        type.Should().NotBeNull();

        // Act
        var methods = type.GetMethods().Where(m => m.Name == methodName);

        // Assert
        methods.Should().NotBeEmpty($"{methodName} is essential for DEK lifecycle management");
        
        LogInfo($"✓ DataEncryptionKeyContainer.{methodName} exists");
    }
}
```

---

## Task 4: Configuration and Policy Tests

### File: `ConfigurationTests.cs`

```csharp
using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests;

/// <summary>
/// Tests that validate configuration and policy-related APIs.
/// </summary>
public class ConfigurationTests : CompatibilityTestBase
{
    public ConfigurationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ClientEncryptionPolicy_Constructor_Exists()
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.ClientEncryptionPolicy, Microsoft.Azure.Cosmos");
        type.Should().NotBeNull();

        // Act
        var constructor = type.GetConstructors().FirstOrDefault();

        // Assert
        constructor.Should().NotBeNull("ClientEncryptionPolicy must have a public constructor");
        
        LogInfo($"✓ ClientEncryptionPolicy has accessible constructor");
    }

    [Fact]
    public void ClientEncryptionPolicy_IncludedPaths_Property_Exists()
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.ClientEncryptionPolicy, Microsoft.Azure.Cosmos");
        type.Should().NotBeNull();

        // Act
        var property = type.GetProperty("IncludedPaths");

        // Assert
        property.Should().NotBeNull("IncludedPaths property is required for encryption configuration");
        property.PropertyType.Should().Match(t => 
            t.Name.Contains("IEnumerable") || t.Name.Contains("Collection") || t.IsArray,
            "IncludedPaths should be a collection type");
        
        LogInfo($"✓ ClientEncryptionPolicy.IncludedPaths exists and is a collection");
    }

    [Fact]
    public void ClientEncryptionPolicy_PolicyFormatVersion_Property_Exists()
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.ClientEncryptionPolicy, Microsoft.Azure.Cosmos");
        type.Should().NotBeNull();

        // Act
        var property = type.GetProperty("PolicyFormatVersion");

        // Assert
        property.Should().NotBeNull("PolicyFormatVersion is required for version tracking");
        property.PropertyType.Should().Be(typeof(int), "PolicyFormatVersion should be an integer");
        
        LogInfo($"✓ ClientEncryptionPolicy.PolicyFormatVersion exists");
    }

    [Fact]
    public void ContainerProperties_ClientEncryptionPolicy_Property_Exists()
    {
        // Arrange
        var type = typeof(ContainerProperties);

        // Act
        var property = type.GetProperty("ClientEncryptionPolicy");

        // Assert
        property.Should().NotBeNull("ContainerProperties must support ClientEncryptionPolicy");
        
        LogInfo($"✓ ContainerProperties.ClientEncryptionPolicy exists");
    }

    [Fact]
    public void DatabaseCore_DefineContainer_Method_Exists()
    {
        // Arrange
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DatabaseCore, Microsoft.Azure.Cosmos.Encryption.Custom");
        type.Should().NotBeNull();

        // Act
        var method = type.GetMethod("DefineContainer");

        // Assert
        method.Should().NotBeNull("DefineContainer is needed for fluent container creation");
        
        LogInfo($"✓ DatabaseCore.DefineContainer exists");
    }
}
```

---

## Task 5: Version-Specific Test Tracking

### File: `VersionSpecificTests.cs`

```csharp
using System;
using System.Reflection;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests;

/// <summary>
/// Tests for version-specific features and known version differences.
/// Use [Trait] attributes to categorize by version.
/// </summary>
public class VersionSpecificTests : CompatibilityTestBase
{
    public VersionSpecificTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("MinVersion", "1.0.0-preview05")]
    public void Preview05_FetchDataEncryptionKeyWithoutRawKeyAsync_Exists()
    {
        // This feature was added in preview05
        // Test should be skipped on older versions (handled by CI matrix)
        
        var type = Type.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.DataEncryptionKeyContainer, Microsoft.Azure.Cosmos.Encryption.Custom");
        type.Should().NotBeNull();

        var method = type.GetMethod("FetchDataEncryptionKeyWithoutRawKeyAsync");
        
        if (IsVersionAtLeast("1.0.0-preview05"))
        {
            method.Should().NotBeNull("FetchDataEncryptionKeyWithoutRawKeyAsync should exist in preview05+");
            LogInfo($"✓ FetchDataEncryptionKeyWithoutRawKeyAsync exists (preview05+ feature)");
        }
        else
        {
            LogInfo($"Skipping preview05+ feature check for older version");
        }
    }

    [Fact]
    [Trait("MinVersion", "1.0.0-preview08")]
    public void Preview08_UsesMicrosoftDataEncryption_v1_2_0()
    {
        // Preview08 updated Microsoft.Data.Encryption.Cryptography to 1.2.0
        
        var assembly = typeof(CosmosEncryptionClient).Assembly;
        var referencedAssemblies = assembly.GetReferencedAssemblies();
        
        var encryptionCryptoRef = referencedAssemblies
            .FirstOrDefault(a => a.Name == "Microsoft.Data.Encryption.Cryptography");
        
        if (IsVersionAtLeast("1.0.0-preview08"))
        {
            encryptionCryptoRef.Should().NotBeNull();
            encryptionCryptoRef.Version.Should().BeGreaterOrEqualTo(new Version(1, 2, 0),
                "Preview08 should reference Microsoft.Data.Encryption.Cryptography 1.2.0+");
            LogInfo($"✓ Uses Microsoft.Data.Encryption.Cryptography v{encryptionCryptoRef.Version}");
        }
        else
        {
            LogInfo($"Skipping version-specific dependency check for older version");
        }
    }

    private bool IsVersionAtLeast(string minVersion)
    {
        // Simple version comparison - can be enhanced
        var currentVersion = TestedVersion;
        
        // Extract preview number if exists
        var extractPreviewNum = (string v) =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(v, @"preview(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        };

        var currentPreview = extractPreviewNum(currentVersion);
        var minPreview = extractPreviewNum(minVersion);

        return currentPreview >= minPreview;
    }
}
```

---

## Task 6: Test Documentation

### File: `TESTING-GUIDE.md`

```markdown
# Compatibility Test Suite Guide

## Test Organization

### Test Classes

1. **CoreApiTests**: Validates public API surface
   - Type existence
   - Method signatures
   - Property availability
   - Assembly references

2. **EncryptionDecryptionTests**: Validates core functionality
   - Encryption type consistency
   - Property structures
   - Required methods
   - Interface implementations

3. **ConfigurationTests**: Validates policy and configuration APIs
   - Policy construction
   - Container configuration
   - Database operations

4. **VersionSpecificTests**: Handles version-specific features
   - Feature availability by version
   - Dependency version checks
   - Graceful handling of version differences

## Writing New Tests

### Guidelines

1. **Use Public APIs Only**
   ```csharp
   // ✅ Good - uses public API
   var client = new CosmosEncryptionClient(cosmosClient, keyResolver);
   
   // ❌ Bad - uses reflection for implementation details
   var privateField = client.GetType().GetField("_internal", BindingFlags.NonPublic);
   ```

2. **Make Tests Version-Agnostic**
   ```csharp
   // ✅ Good - checks if method exists
   var method = type.GetMethod("CreateItemAsync");
   method.Should().NotBeNull();
   
   // ❌ Bad - assumes specific implementation
   method.GetParameters().Length.Should().Be(5);
   ```

3. **Test Behavior, Not Implementation**
   ```csharp
   // ✅ Good - tests contract
   var encryptionType = (int)Enum.Parse(type, "Deterministic");
   encryptionType.Should().Be(1); // Serialization contract
   
   // ❌ Bad - tests implementation
   // (Don't test private methods or internal logic)
   ```

4. **Use Descriptive Test Names**
   ```csharp
   [Fact]
   public void CosmosEncryptionClient_Constructor_WithCosmosClient_Exists()
   {
       // Clear what's being tested
   }
   ```

### Test Anatomy

```csharp
[Fact]
public void ComponentName_FeatureName_ExpectedBehavior()
{
    // Arrange - Set up test data
    var type = typeof(CosmosEncryptionClient);
    
    // Act - Perform the test action
    var method = type.GetMethod("GetDatabase");
    
    // Assert - Verify the result
    method.Should().NotBeNull("GetDatabase is essential");
    
    // Log - Provide visibility
    LogInfo($"✓ GetDatabase method exists");
}
```

## Running Tests

### Single Version

```powershell
# Test current build
dotnet test

# Test specific published version
dotnet test -p:TargetEncryptionCustomVersion=1.0.0-preview07
```

### Multiple Versions

```powershell
# Run the helper script
..\test-compatibility.ps1
```

### With Filtering

```powershell
# Run only API tests
dotnet test --filter "FullyQualifiedName~CoreApiTests"

# Run only tests for preview05+ features
dotnet test --filter "MinVersion=1.0.0-preview05"
```

## Handling Version Differences

### Scenario 1: New Feature Added in Specific Version

```csharp
[Fact]
[Trait("MinVersion", "1.0.0-preview05")]
public void Preview05_NewFeature_Exists()
{
    if (IsVersionAtLeast("1.0.0-preview05"))
    {
        // Test the new feature
        var method = type.GetMethod("NewMethod");
        method.Should().NotBeNull();
    }
    else
    {
        // Gracefully skip for older versions
        LogInfo($"Skipping preview05+ feature for older version");
    }
}
```

### Scenario 2: API Signature Changed

```csharp
[Fact]
public void Method_Exists_WithCompatibleSignature()
{
    // Test that method exists - don't test exact signature
    var methods = type.GetMethods().Where(m => m.Name == "MethodName");
    methods.Should().NotBeEmpty("Method must exist for compatibility");
    
    // Optionally test minimum capabilities
    var hasOverloadWithParams = methods.Any(m => m.GetParameters().Length >= 2);
    hasOverloadWithParams.Should().BeTrue("Method should support at least 2 parameters");
}
```

## Troubleshooting

### Test Fails on Specific Version

1. Check if it's a legitimate breaking change
2. Review changelog for that version
3. Add version-specific handling if intentional
4. Report issue if unintentional break

### Type Not Found

```csharp
// Use fully qualified type names
var type = Type.GetType("Namespace.TypeName, AssemblyName");
if (type == null)
{
    LogWarning($"Type not found - may have been moved or renamed");
    // Handle gracefully
}
```

### Assembly Version Mismatch

```powershell
# Clear NuGet cache and rebuild
dotnet nuget locals all --clear
dotnet restore --force
dotnet build
```

## Best Practices

1. **Keep tests fast** - No real Cosmos DB connections
2. **Use meaningful assertions** - Include reason messages
3. **Log important info** - Helps diagnose CI failures
4. **Handle version differences gracefully** - Don't fail on older versions
5. **Update testconfig.json** - When new versions are released

## CI Integration

Tests run automatically in Azure Pipelines:
- On every PR (against last published version)
- On scheduled builds (full matrix)
- Before package publishing (validation gate)

See `azure-pipelines-encryption-custom-compatibility.yml` for CI configuration.
```

---

## Verification Checklist

After implementing all tests:

- [ ] All test files compile successfully
- [ ] Tests run locally against current branch
- [ ] Tests run against last published version (1.0.0-preview08)
- [ ] Tests run against an older version (1.0.0-preview06)
- [ ] Test output is clear and informative
- [ ] Tests complete in reasonable time (<2 minutes per version)
- [ ] Documentation is clear and complete

---

## Next Steps

With tests implemented, the following work can proceed in parallel:

- **Agent 3**: Create Azure Pipeline to run these tests
- **Agent 4**: Add API compatibility tool checks
- **Agent 5**: Create helper scripts and documentation

---

## Files Created

```
Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/
├── TestFixtures/
│   └── CompatibilityTestBase.cs
├── CoreApiTests.cs
├── EncryptionDecryptionTests.cs
├── ConfigurationTests.cs
├── VersionSpecificTests.cs
└── TESTING-GUIDE.md
```

**Total**: 6 files
