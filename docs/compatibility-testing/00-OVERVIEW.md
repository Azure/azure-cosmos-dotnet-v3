# Compatibility Testing for Microsoft.Azure.Cosmos.Encryption.Custom

## Executive Summary

This document outlines the implementation plan for a comprehensive compatibility testing infrastructure for the `Microsoft.Azure.Cosmos.Encryption.Custom` NuGet package. The goal is to ensure that new releases do not break existing consumers by validating both API compatibility and runtime behavior across multiple released versions.

## Current State

- **Package**: `Microsoft.Azure.Cosmos.Encryption.Custom`
- **Current Version**: `1.0.0-preview08`
- **Target Frameworks**: `netstandard2.0`, `net8.0`
- **Test Frameworks**: `net6.0`, `net8.0`
- **Published Versions**: `1.0.0-preview01` through `1.0.0-preview08`

## Goals

1. **Automated Testing**: Run compatibility tests automatically on every PR and CI build
2. **Version Matrix**: Test against multiple published versions in parallel
3. **Breaking Change Detection**: Catch API surface changes early
4. **Runtime Validation**: Ensure behavioral compatibility across versions
5. **Standalone Pipeline**: New independent pipeline (not integrated into existing workflows)

## Key Features

### 1. Automatic Mode
- Tests current branch against the last published version
- Runs on every PR/CI build
- Fast feedback for developers

### 2. Matrix Mode
- Tests against a configurable list of versions
- Runs in parallel for efficiency
- Useful for comprehensive validation before releases

### 3. API Compatibility Checks
- Uses Microsoft.DotNet.ApiCompat.Tool
- Validates no breaking changes in public API surface
- Fails fast before runtime tests

### 4. Runtime Compatibility Tests
- Consumer-style tests using only public APIs
- Validates behavioral compatibility
- Tests encryption/decryption round-trips across versions

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│         Azure DevOps Pipeline                           │
│  (azure-pipelines-encryption-custom-compatibility.yml)  │
└─────────────────┬───────────────────────────────────────┘
                  │
                  ├─── Stage 1: API Compatibility Check
                  │    └─── ApiCompat Tool (fast fail)
                  │
                  └─── Stage 2: Runtime Compatibility Matrix
                       ├─── Job: Test vs 1.0.0-preview08
                       ├─── Job: Test vs 1.0.0-preview07
                       ├─── Job: Test vs 1.0.0-preview06
                       └─── Job: Test vs [configurable versions]
                                │
                                └─── Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
                                     └─── Uses Central Package Management
```

## Work Breakdown Structure

The implementation is divided into **6 independent work packages** that can be executed in parallel:

1. **[Agent 1] Infrastructure Setup** - Central Package Management & project structure
2. **[Agent 2] Compatibility Test Suite** - Core test implementations
3. **[Agent 3] Pipeline Configuration** - Azure DevOps YAML files
4. **[Agent 4] API Compatibility Tooling** - ApiCompat integration
5. **[Agent 5] Documentation & Scripts** - Developer tools and docs
6. **[Agent 6] Advanced Features** - Side-by-side testing (optional)

Each work package is detailed in its own document (01-06).

## Timeline Estimate

- **Agent 1**: 2-3 hours (foundational)
- **Agent 2**: 4-6 hours (test implementation)
- **Agent 3**: 2-3 hours (pipeline setup)
- **Agent 4**: 2-3 hours (tooling integration)
- **Agent 5**: 1-2 hours (documentation)
- **Agent 6**: 4-6 hours (advanced, optional)

**Total**: 15-23 hours of work (can be parallelized to 4-6 hours wall time)

## Success Criteria

✅ New pipeline runs successfully on PR  
✅ Tests pass against last published version  
✅ Matrix configuration allows testing specific versions  
✅ API breaking changes are detected automatically  
✅ Documentation enables local testing  
✅ Zero integration with existing pipelines (standalone)  

## Dependencies Between Work Packages

```
Agent 1 (Infrastructure) ──┬──> Agent 2 (Tests)
                           ├──> Agent 3 (Pipeline)
                           ├──> Agent 4 (ApiCompat)
                           └──> Agent 6 (Advanced)

Agent 5 (Documentation) ──> Independent (can start anytime)
```

**Recommendation**: Start Agent 1 first, then parallelize Agents 2-5, finally Agent 6 if needed.

## Related Documents

- [01-AGENT1-INFRASTRUCTURE.md](./01-AGENT1-INFRASTRUCTURE.md) - Project and CPM setup
- [02-AGENT2-TEST-SUITE.md](./02-AGENT2-TEST-SUITE.md) - Test implementations
- [03-AGENT3-PIPELINE.md](./03-AGENT3-PIPELINE.md) - Azure DevOps configuration
- [04-AGENT4-APICOMPAT.md](./04-AGENT4-APICOMPAT.md) - API validation tooling
- [05-AGENT5-DOCS-SCRIPTS.md](./05-AGENT5-DOCS-SCRIPTS.md) - Documentation and helpers
- [06-AGENT6-ADVANCED.md](./06-AGENT6-ADVANCED.md) - Side-by-side testing (optional)

## Getting Started

1. Review all agent work package documents
2. Assign agents to different developers/teams
3. Start with Agent 1 (foundational work)
4. Parallelize Agents 2-5 once Agent 1 is complete
5. Optionally implement Agent 6 for advanced scenarios

## Questions & Decisions

### Q: Why a separate pipeline instead of integrating into existing ones?
**A**: Keeps compatibility testing isolated, easier to maintain, and doesn't impact existing workflows.

### Q: Why test against published NuGet packages instead of building old versions?
**A**: This tests what actual consumers use. NuGet packages are the source of truth.

### Q: Why Central Package Management?
**A**: Allows clean version overrides via MSBuild properties without modifying project files.

### Q: Do we need the advanced side-by-side testing (Agent 6)?
**A**: Start with Agents 1-5. Add Agent 6 only if you need to load multiple versions in the same process for A/B comparisons.

## Support & Maintenance

- **Owner**: Encryption Custom Team
- **Pipeline**: `azure-pipelines-encryption-custom-compatibility.yml`
- **Test Project**: `Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests`
- **Version Config**: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Directory.Packages.props`

---

**Next Steps**: Review [01-AGENT1-INFRASTRUCTURE.md](./01-AGENT1-INFRASTRUCTURE.md) to begin implementation.
