# Compatibility Testing Quick Start

## 5-Minute Setup

### Prerequisites

- .NET 8 SDK installed
- PowerShell 7+ (or Windows PowerShell 5.1)
- Access to NuGet.org

### Quick Test

```powershell
# Navigate to repo
cd C:\repos\azure-cosmos-dotnet-v3

# Test against last published version
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
dotnet test -p:TargetEncryptionCustomVersion=1.0.0-preview07
```

That's it! ✅

---

## Common Scenarios

### Scenario 1: Test Before Creating PR

```powershell
# Build your changes
dotnet build Microsoft.Azure.Cosmos.Encryption.Custom\src -c Release

# Check for API breaking changes (fast!)
.\tools\test-api-compat-local.ps1

# Run compatibility tests against last version
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
.\test-compatibility.ps1 -Version 1.0.0-preview07

# If passed, create PR
```

### Scenario 2: Test Against Multiple Versions

```powershell
# Run full matrix locally (tests all 4 versions)
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
.\test-compatibility.ps1
```

### Scenario 3: Check API Changes Only

```powershell
# Fast API check (no full tests) - takes ~30 seconds
.\tools\test-api-compat-local.ps1
```

### Scenario 4: Add New Test

1. Open `Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests` project
2. Add test to appropriate file:
   - `CoreApiTests.cs` - API surface tests
   - `EncryptionDecryptionTests.cs` - Encryption functionality
   - `ConfigurationTests.cs` - Configuration and policy tests
   - `VersionSpecificTests.cs` - Version metadata tracking
3. Run tests locally to verify
4. Commit and push

Example test:
```csharp
[Fact]
public void MyNewFeature_Exists()
{
    // Arrange
    var assembly = typeof(DataEncryptionKeyProperties).Assembly;
    
    // Act
    var type = assembly.GetType("Microsoft.Azure.Cosmos.Encryption.Custom.MyNewFeature");
    
    // Assert
    type.Should().NotBeNull("MyNewFeature should exist in all tested versions");
}
```

---

## File Locations

| What | Where |
|------|-------|
| Test Project | `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/` |
| Test Scripts | `test-compatibility.ps1` in test project directory |
| Pipeline | `azure-pipelines-encryption-custom-compatibility.yml` (root) |
| Version Config | `testconfig.json` in test project directory |
| API Tools | `tools/apicompat-check.ps1`, `tools/test-api-compat-local.ps1` |
| Documentation | `docs/compatibility-testing/` |
| Package Config | `tests/Directory.Packages.props` |

---

## Understanding Test Results

### Expected Results

**23/30 tests passing (77%)** is the current baseline.

Some tests are expected to fail when testing against published packages:
- Extension method tests (reflection limitations)
- Base SDK type tests (not in Encryption.Custom package)
- Assembly reference name tests (different in packages vs source)

### What Failures to Investigate

❌ **Investigate these failures:**
- Public type not found (possible breaking change)
- Method signature changes
- Property removals
- New test failures that didn't exist before

✅ **Expected failures (can ignore):**
- `ContainerExtensions_WithEncryptor_Method_Exists` - Extension method reflection limitation
- `Assembly_References_ExpectedPackages` - Reference name differences in packages
- Tests for base SDK types not exported by Encryption.Custom

---

## FAQ

### Q: How do I know which version to test against?

**A**: Always test against the **baseline version** in `testconfig.json`. Currently: `1.0.0-preview07`

```powershell
# Check current baseline
cat Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\testconfig.json | Select-String "baselineVersion"
```

### Q: Tests pass locally but fail in CI. Why?

**A**: Common causes:

1. **Different NuGet feed** - CI may use different package source
2. **Cache issues** - Clear with `dotnet nuget locals all --clear`
3. **Version resolution differences** - Check pipeline logs for actual version resolved
4. **Environment differences** - .NET SDK version, OS differences

**Solution**: Check CI logs for package resolution details, compare with local.

### Q: How do I add a new version to test?

**A**: Two steps:

1. Update `testconfig.json` - Add to `versionMatrix` array
2. Update pipeline YAML - Add new job in Stage 2

Or use the helper tool:
```powershell
.\tools\update-test-matrix.ps1 -Version "1.0.0-preview08" -SetBaseline
```

See [MAINTENANCE.md](MAINTENANCE.md) for detailed steps.

### Q: Can I skip compatibility tests for my PR?

**A**: **No.** Compatibility tests are required for all PRs that modify:
- `Microsoft.Azure.Cosmos.Encryption.Custom/src/**`
- `Microsoft.Azure.Cosmos.Encryption.Custom/tests/**`

The pipeline automatically runs on PRs to prevent breaking changes.

### Q: What if API compat check fails?

**A**: Three possibilities:

1. **Unintentional breaking change** - Revert the change, find alternative approach
2. **Intentional breaking change** - Document in `API-CHANGES.md` and add suppression to `ApiCompatSuppressions.txt`
3. **False positive** - Add suppression with justification

See [API-CHANGES.md](API-CHANGES.md) for guidelines.

### Q: How long do tests take?

**A**: 

- **API Compat Check**: ~30 seconds
- **Single Version Test**: ~3-5 minutes
- **Full Matrix (4 versions)**: ~15-20 minutes (local), ~12-15 minutes (CI parallel)

### Q: Where are test results stored?

**A**:

- **Local**: `TestResults/` directory in test project
- **CI**: Azure DevOps → Pipeline Run → Tests tab
- **Artifacts**: Azure DevOps → Pipeline Run → Artifacts

---

## Quick Reference Commands

```powershell
# API compatibility check (fastest)
.\tools\test-api-compat-local.ps1

# Test single version
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
dotnet test -p:TargetEncryptionCustomVersion=1.0.0-preview07

# Test all versions
.\test-compatibility.ps1

# Check what versions are available
.\tools\discover-published-versions.ps1

# Clear NuGet cache (if having package issues)
dotnet nuget locals all --clear

# Build in release mode
dotnet build Microsoft.Azure.Cosmos.Encryption.Custom\src -c Release
```

---

## Development Workflow

```
┌─────────────────────┐
│ Make Code Changes   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Build (Release)     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ API Compat Check    │ ← Fast (~30s)
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Compat Tests        │ ← Thorough (~5-10min)
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Create PR           │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ CI Pipeline Runs    │ ← Automatic
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Review & Merge      │
└─────────────────────┘
```

---

## Getting Help

- **Pipeline issues**: Check [PIPELINE-GUIDE.md](PIPELINE-GUIDE.md)
- **Test failures**: Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **API changes**: Check [API-CHANGES.md](API-CHANGES.md)
- **Maintenance tasks**: Check [MAINTENANCE.md](MAINTENANCE.md)
- **Quick reference**: Check [CHEATSHEET.md](CHEATSHEET.md)
- **General questions**: Contact Cosmos DB Encryption Custom team

---

## Next Steps

Once you're comfortable with the basics:

1. Read [TESTING-GUIDE.md](../Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests/README.md) for in-depth test strategy
2. Review [PIPELINE-GUIDE.md](PIPELINE-GUIDE.md) to understand CI/CD integration
3. Check [MAINTENANCE.md](MAINTENANCE.md) if you're maintaining the test infrastructure

---

**Last Updated**: 2025-10-02  
**Version**: 1.0
