# GitHub Copilot & AI Agent Instructions

This document provides guidelines for AI assistants (GitHub Copilot, Copilot Workspace, and similar tools) working on this repository.

## Repository Overview

This is the **Microsoft Azure Cosmos DB .NET SDK Version 3** - the official .NET client library for Azure Cosmos DB.

- **Language**: C# (.NET Standard 2.0)
- **Test Framework**: MSTest v1.2.0
- **Build System**: MSBuild / dotnet CLI

## Code Style & Conventions

### General
- Follow [Azure SDK Guidelines](https://azure.github.io/azure-sdk/dotnet_introduction.html)
- Use `TreatWarningsAsErrors` - all warnings must be resolved
- XML documentation required for all public APIs
- Use nullable reference types where supported

### Naming
- PascalCase for public members, types, and methods
- camelCase for private fields and local variables
- Prefix private fields with underscore (e.g., `_cosmosClient`)
- Async methods must have `Async` suffix

### Testing
- MSTest framework (v1.2.0) - note: `[DoNotParallelize]` attribute is NOT available
- Test class names: `{ClassName}Tests`
- Test method names: `{MethodName}_{Scenario}_{ExpectedResult}`
- Use `[DataRow]` for parameterized tests

## Build & Test Commands

```bash
# Build entire solution
dotnet build Microsoft.Azure.Cosmos.sln

# Build specific project
dotnet build Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj

# Run unit tests
dotnet test Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Microsoft.Azure.Cosmos.Tests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName"

# Pack NuGet package
dotnet pack Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj -c Release
```

## Important Patterns

### Error Handling
- Use `CosmosException` for service-related errors
- Preserve inner exceptions for debugging
- Include diagnostic context in exceptions

### Async/Await
- All I/O operations must be async
- Use `ConfigureAwait(false)` in library code
- Avoid `async void` except for event handlers

### Disposable Resources
- Implement `IDisposable` for unmanaged resources
- Use `using` statements or `using` declarations
- `CosmosClient` should be a singleton per application

## AI-Generated Code Guidelines

### Attribution
When AI generates significant code contributions:

1. **Commit Messages**: Use `Co-authored-by: Copilot <copilot@github.com>` trailer
2. **PR Description**: Note AI assistance in the PR body
3. **Code Comments**: For complex algorithms, optionally note: `// AI-assisted implementation`

### Quality Standards
AI-generated code must meet the same standards as human-written code:

- [ ] Compiles without errors or warnings
- [ ] Passes all existing tests
- [ ] Includes tests for new functionality
- [ ] Follows code style guidelines
- [ ] Has appropriate XML documentation
- [ ] Has been reviewed and understood by a human

### What AI Should NOT Do
- Do not commit secrets, keys, or credentials
- Do not disable security features or warnings
- Do not remove existing tests without explicit approval
- Do not make breaking changes to public APIs
- Do not add dependencies without discussion

## File Structure

```
Microsoft.Azure.Cosmos/
├── src/                          # Main SDK source
│   ├── Microsoft.Azure.Cosmos.csproj
│   ├── Microsoft.Azure.Cosmos.targets  # NuGet package targets
│   └── ...
├── tests/
│   ├── Microsoft.Azure.Cosmos.Tests/           # Unit tests
│   └── Microsoft.Azure.Cosmos.EmulatorTests/   # Integration tests (require emulator)
└── contracts/                    # API contracts
```

## Common Tasks

### Adding a New Feature
1. Create feature branch from `master`
2. Implement in `src/` with full XML documentation
3. Add unit tests in `tests/Microsoft.Azure.Cosmos.Tests/`
4. Update changelog.md if user-facing
5. Submit PR with clear description

### Fixing a Bug
1. Write a failing test that reproduces the bug
2. Fix the bug
3. Verify test passes
4. Update changelog.md

### Updating Dependencies
- Discuss in an issue first
- Check for breaking changes
- Update all affected projects consistently

## Resources

- [SDK Best Practices](https://docs.microsoft.com/azure/cosmos-db/sql/best-practice-dotnet)
- [Cosmos DB Documentation](https://docs.microsoft.com/azure/cosmos-db/)
- [Contributing Guide](./CONTRIBUTING.md)
- [SDK Design Guidelines](./SdkDesignGuidelines.md)
