# Encryption.Custom compatibility matrix

The `net8.0` Encryption.Custom emulator tests build two isolated workers:

- `Released` references the exact public package `Microsoft.Azure.Cosmos.Encryption.Custom` `1.0.0-preview07`.
- `Current` references the Encryption.Custom project in this repository.

`CrossVersionCompatibilityTests` runs both workers against one temporary emulator database and verifies bidirectional MDE and AEAD compatibility. It checks hardened payload fidelity, raw ciphertext metadata, peer-created DEKs, point/query/read-feed behavior, processor selection on supported Stream paths, exact worker identity, and strict structured results.

The workers are built automatically by `Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.csproj`; no local package feed, custom NuGet configuration, or standalone launcher is required.

```powershell
dotnet test ..\EmulatorTests\Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.csproj `
  -f net8.0 `
  --filter "FullyQualifiedName~CrossVersionCompatibilityTests"
```

Use the same emulator configuration as the other Encryption.Custom emulator tests.
