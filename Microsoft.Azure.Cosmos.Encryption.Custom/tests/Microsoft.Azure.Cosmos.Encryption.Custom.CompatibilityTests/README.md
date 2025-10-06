# Cross-Version Encryption Compatibility Tests

## Purpose

These tests have **one primary goal**: Verify that data encrypted with version A can be decrypted with version B, and vice versa.

This ensures customers can:
- Upgrade their library version without breaking existing encrypted data
- Roll back versions if needed without data loss
- Mix versions across different services that share encrypted data

## What These Tests DO

✅ **Encrypt data with version A, decrypt with version B** - All version combinations tested  
✅ **Test both Deterministic and Randomized encryption modes**  
✅ **Verify data integrity across all version pairs**

## What These Tests DO NOT Do

❌ API surface validation (use API compat tools instead)  
❌ Type/method existence checks (not the purpose of compatibility tests)  
❌ Assembly metadata verification (irrelevant to encryption compatibility)  
❌ Performance testing (separate performance test suite exists)

## Test Structure

### CrossVersionEncryptionTests.cs

The **only** test file. Contains:

1. **CanEncryptWithVersionA_AndDecryptWithVersionB** - Basic cross-version compatibility
2. **CanEncryptAndDecryptDeterministic_AcrossVersions** - Deterministic mode testing
3. **CanEncryptAndDecryptRandomized_AcrossVersions** - Randomized mode testing

Each test runs for **all version pairs** defined in `testconfig.json`.

### Version Matrix

Configured in `testconfig.json`:

```json
{
  "versionMatrix": {
    "versions": [
      "1.0.0-preview07",
      "1.0.0-preview06",
      "1.0.0-preview05"
    ]
  }
}
```

This generates test combinations:
- 1.0.0-preview07 → 1.0.0-preview07 ✓
- 1.0.0-preview07 → 1.0.0-preview06 ✓
- 1.0.0-preview07 → 1.0.0-preview05 ✓
- 1.0.0-preview06 → 1.0.0-preview07 ✓
- 1.0.0-preview06 → 1.0.0-preview06 ✓
- 1.0.0-preview06 → 1.0.0-preview05 ✓
- ... (all combinations)

## Running Tests Locally

### Test Current Build Against Published Versions

```powershell
# Test current code against all versions in matrix
pwsh .\Microsoft.Azure.Cosmos.Encryption.Custom\tests\test-compatibility.ps1 -CurrentOnly
```

### Test Local Package Build

```powershell
# Build local package and test against all published versions
pwsh .\Microsoft.Azure.Cosmos.Encryption.Custom\tests\test-compatibility.ps1 -UseLocalBuild
```

### Test Specific Version

```powershell
# Test against a single specific version
pwsh .\Microsoft.Azure.Cosmos.Encryption.Custom\tests\test-compatibility.ps1 -Version "1.0.0-preview07"
```

## CI Pipeline

The `azure-pipelines-encryption-custom-compatibility.yml` pipeline:

1. Builds the current branch as a local NuGet package
2. Restores all versions from the matrix
3. Runs cross-version tests for all combinations
4. Fails if any version pair cannot encrypt/decrypt correctly

## Adding New Versions

To test against a new version:

1. Update `testconfig.json` to add the version to the matrix
2. The tests will automatically include all new combinations

Example:
```json
{
  "versionMatrix": {
    "versions": [
      "1.0.0-preview08",  // ← New version added
      "1.0.0-preview07",
      "1.0.0-preview06"
    ]
  }
}
```

## How It Works

### Version Loading

The `VersionLoader` utility loads different package versions side-by-side using isolated AssemblyLoadContexts:

```csharp
using (var encryptLoader = VersionLoader.Load("1.0.0-preview07"))
using (var decryptLoader = VersionLoader.Load("1.0.0-preview06"))
{
    // Use reflection to call public API from each version
    var encrypted = EncryptWithVersion(encryptLoader, data);
    var decrypted = DecryptWithVersion(decryptLoader, encrypted);
    
    // Verify data matches
    Assert.Equal(data, decrypted);
}
```

### Test Data

Each test encrypts a standard payload containing:
- ASCII text
- Special characters
- Unicode from multiple languages

This ensures the encryption/decryption handles all character encodings correctly.

## Troubleshooting

### "Package version X.Y.Z not found"

The package must be restored before testing:

```powershell
dotnet restore Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests `
  -p:TargetEncryptionCustomVersion=X.Y.Z
```

### "Encryption failed with version X"

Check that the version's public encryption API matches what the test expects. The test uses reflection to invoke the public API.

### Tests are slow

Cross-version tests load multiple assemblies in isolation. This is inherently slower than single-version tests. Typical runtime: 1-3 minutes for full matrix.

## Design Principles

1. **Single Responsibility**: These tests only verify encryption/decryption compatibility
2. **Comprehensive Coverage**: Test ALL version pairs automatically
3. **Public API Only**: Use only public APIs via reflection (ensures customer code would work)
4. **Simple & Maintainable**: One test file, clear purpose, minimal complexity
5. **Fast Feedback**: Fail fast if any version pair is incompatible

## See Also

- **API Compatibility**: Use ApiCompat tool for API surface validation
- **Performance Tests**: Separate test suite for performance regression detection
- **Functional Tests**: Main test suite for feature validation
