# Encryption.Custom compatibility matrix

The `net8.0` Encryption.Custom emulator tests build two isolated workers:

- `Released` references the exact public package `Microsoft.Azure.Cosmos.Encryption.Custom` `1.0.0-preview07`.
- `Current` references the Encryption.Custom project in this repository.

`CrossVersionCompatibilityTests` runs both workers against one temporary emulator database with one shared key container and one shared item container. Role-scoped DEK and document IDs keep writes isolated while verifying that each worker can consume the other worker's key metadata and ciphertext. The matrix checks hardened payload fidelity, raw ciphertext metadata, point/query/read-feed behavior, processor selection on supported Stream paths, exact worker identity, and strict structured results.

Processor evidence is captured separately for writes, typed reads, and stream reads. A Stream request is recorded independently from the processor that actually completed the read; filtered queries may report Newtonsoft when the SDK supplies a response stream that cannot support in-place Stream processing.

The workers are built automatically by `Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.csproj`; no local package feed, custom NuGet configuration, or standalone launcher is required. The emulator account key is supplied to child processes through the `COSMOS_COMPAT_MATRIX_KEY` environment variable rather than command-line arguments.

```powershell
dotnet test ..\EmulatorTests\Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.csproj `
  -f net8.0 `
  --filter "FullyQualifiedName~CrossVersionCompatibilityTests"
```

Use the same emulator configuration as the other Encryption.Custom emulator tests.
