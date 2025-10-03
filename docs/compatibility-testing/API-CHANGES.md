# API Changes Log

## Purpose

This document tracks all API changes to `Microsoft.Azure.Cosmos.Encryption.Custom` that affect backward compatibility. It serves as:

1. **Historical Record** - Document all API evolution over time
2. **Migration Guide** - Help consumers understand how to update their code
3. **Review Tool** - Ensure breaking changes are intentional and justified

## How to Use This Document

### When API Compatibility Check Fails

1. **Review the Changes**: Check the output from `dotnet-apicompat` to understand what changed
2. **Determine Intent**: Is this change intentional or accidental?
3. **Document the Change**: Add an entry to this file with full details
4. **Update Suppressions**: Add to `ApiCompatSuppressions.txt` if the change is approved
5. **Communicate**: Ensure the breaking change is noted in release notes

### Entry Format

```markdown
### Version X.Y.Z

**Date**: YYYY-MM-DD  
**PR**: #XXXX  
**Reviewer**: @username  
**Baseline**: X.Y.Z-previous

#### Breaking Changes

- **Type**: `Namespace.TypeName`
- **Member**: `MethodName()` or `PropertyName`
- **Change**: Description of what changed (method removed, signature changed, etc.)
- **Reason**: Why this change was necessary
- **Migration**: How consumers should update their code (with code examples)
- **Suppression**: Added to ApiCompatSuppressions.txt (Yes/No)

#### Non-Breaking Changes

- Added new method `TypeName.NewMethod()`
- Added new property `TypeName.NewProperty`
- Added new type `NewTypeName`
```

---

## Change History

### 1.0.0-preview07 (Current Baseline)

**Date**: 2024-06-12  
**PR**: #4546  
**Baseline**: 1.0.0-preview06

#### Breaking Changes

None

#### Non-Breaking Changes

- Updated package reference `Microsoft.Azure.Cosmos` to version 3.41.0-preview and 3.40.0
- Updated `Microsoft.Data.Encryption.Cryptography` dependency

---

### 1.0.0-preview06

**Date**: 2024-04-15  
**PR**: #4321

#### Breaking Changes

None

#### Non-Breaking Changes

- Performance improvements to encryption pipeline
- Updated dependency versions

---

### 1.0.0-preview05

**Date**: 2024-02-20  
**PR**: #4123

#### Breaking Changes

None

#### Non-Breaking Changes

- Bug fixes for edge cases in decryption
- Improved error messages

---

### 1.0.0-preview04

**Date**: 2023-12-10  
**PR**: #3987

#### Breaking Changes

None

#### Non-Breaking Changes

- Initial preview release
- Core encryption/decryption functionality

---

## Future Changes

### Version 1.0.0-preview08 (Planned)

**Date**: TBD  
**PR**: TBD  
**Baseline**: 1.0.0-preview07

#### Breaking Changes

TBD - Will be documented when changes are made

#### Non-Breaking Changes

TBD

---

## API Compatibility Guidelines

### What Constitutes a Breaking Change?

**Breaking Changes (Require Major/Minor Version Bump):**

1. **Removing public types, members, or parameters**
   ```csharp
   // Before
   public void OldMethod(string param) { }
   
   // After - BREAKING
   // Method removed entirely
   ```

2. **Changing method signatures**
   ```csharp
   // Before
   public Task<Result> ProcessAsync(string input);
   
   // After - BREAKING
   public Task<Result> ProcessAsync(string input, int newParam);
   ```

3. **Changing return types**
   ```csharp
   // Before
   public string GetValue();
   
   // After - BREAKING
   public int GetValue();
   ```

4. **Changing inheritance hierarchy**
   ```csharp
   // Before
   public class MyClass : BaseClass { }
   
   // After - BREAKING
   public class MyClass : DifferentBaseClass { }
   ```

5. **Making types or members less accessible**
   ```csharp
   // Before
   public class MyClass { }
   
   // After - BREAKING
   internal class MyClass { }
   ```

6. **Changing interface implementations**
   ```csharp
   // Before
   public class MyClass : IInterface { }
   
   // After - BREAKING (removal)
   public class MyClass { }
   ```

**Non-Breaking Changes (Safe for Patch/Minor Version):**

1. **Adding new public types**
2. **Adding new public members**
3. **Adding optional parameters with defaults**
4. **Adding new interface implementations**
5. **Making types or members more accessible**
6. **Adding method overloads**
7. **Bug fixes that don't change behavior**
8. **Internal implementation changes**

### When to Suppress Breaking Changes

Suppressions should be used **sparingly** and only when:

1. **Removing deprecated APIs** that have been marked obsolete for multiple releases
2. **Fixing critical security issues** that require API changes
3. **Correcting design flaws** in preview releases (with proper documentation)
4. **Removing internal APIs** accidentally exposed as public

### Documentation Requirements

For every suppressed breaking change, you **must** provide:

1. **Clear justification** in this file
2. **Migration guide** with code examples
3. **Timeline** showing when the change was announced (for deprecated APIs)
4. **Alternative APIs** that consumers should use instead

### Example: Documented Breaking Change

```markdown
### Version 2.0.0

**Date**: 2025-01-15  
**PR**: #5678  
**Reviewer**: @lead-developer  
**Baseline**: 1.5.0

#### Breaking Changes

- **Type**: `Microsoft.Azure.Cosmos.Encryption.Custom.CosmosEncryptionClient`
- **Member**: `EncryptAsync(string plaintext)`
- **Change**: Method removed
- **Reason**: Security vulnerability - method did not validate input length, risking buffer overflow
- **Migration**: 
  
  **Before:**
  ```csharp
  var encrypted = await client.EncryptAsync(plaintext);
  ```
  
  **After:**
  ```csharp
  var encrypted = await client.EncryptAsync(plaintext, validationOptions: new ValidationOptions());
  ```
  
- **Suppression**: Yes, added to ApiCompatSuppressions.txt
- **Release Notes**: Documented in CHANGELOG.md and upgrade guide
```

---

## Maintenance

### Regular Reviews

- **Before Each Release**: Review this document to ensure all changes are documented
- **During PR Review**: Check if PR includes API changes and update this file
- **Version Planning**: Use this document to plan major/minor version bumps

### Version Numbering

Follow [Semantic Versioning](https://semver.org/):

- **Major Version (X.0.0)**: Breaking changes that affect most consumers
- **Minor Version (0.X.0)**: New features or limited breaking changes in preview
- **Patch Version (0.0.X)**: Bug fixes and non-breaking changes

### Preview Versions

Preview versions (1.0.0-previewXX) have **relaxed** compatibility requirements:

- Breaking changes are acceptable with proper documentation
- Deprecation warnings are recommended but not required
- Focus on getting the API right before GA

### GA Versions

After GA (1.0.0 and above), **strict** compatibility requirements:

- Breaking changes require major version bump
- Deprecation period of at least 2 minor versions
- Comprehensive migration documentation
- Release notes must highlight all breaking changes

---

## Tools and Automation

### Running API Compatibility Checks

**Local Development:**
```powershell
# Quick check against baseline
.\tools\test-api-compat-local.ps1

# Check against specific version
.\tools\test-api-compat-local.ps1 -Baseline "1.0.0-preview06"

# Strict mode (fail on any change)
.\tools\apicompat-check.ps1 -BaselineVersion "1.0.0-preview07" -Strict
```

**CI/CD Pipeline:**

The API compatibility check runs automatically in Azure DevOps:

- **Stage**: ApiCompatibilityCheck (runs before all other stages)
- **Trigger**: Every commit and PR
- **Mode**: Non-strict (reports but doesn't fail build)
- **Artifacts**: API compatibility report published for review

### Generating API Baselines

To create a baseline snapshot for a new release:

```powershell
# After publishing version X.Y.Z to NuGet
.\tools\generate-api-baseline.ps1 -Version "X.Y.Z"
```

This creates a file in `Microsoft.Azure.Cosmos.Encryption.Custom/api-baseline/X.Y.Z.txt` for future comparisons.

---

## References

- [.NET API Compatibility Tool](https://learn.microsoft.com/en-us/dotnet/fundamentals/package-validation/overview)
- [Semantic Versioning](https://semver.org/)
- [.NET Breaking Change Guidelines](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md)
- [Azure SDK Design Guidelines](https://azure.github.io/azure-sdk/general_introduction.html)

---

**Last Updated**: 2025-10-02  
**Maintained By**: Cosmos DB SDK Team
