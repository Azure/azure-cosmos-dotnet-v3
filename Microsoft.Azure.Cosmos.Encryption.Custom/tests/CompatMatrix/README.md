# CompatMatrix — Encryption.Custom OLD↔NEW interop harness

Proves data interop between **OLD = 1.0.0-preview07** (nuget.org) and
**NEW = 2.0.0-preview01** (`..\..\..\..\local-feed`) for
`Microsoft.Azure.Cosmos.Encryption.Custom`. Two subprocesses (one binary pinned
per version) cross-write/read **one shared Cosmos DB** in the Docker Linux
emulator.

## Layout
- `src/Program.cs` – shared write/read agent (cells, deterministic ids, verify).
- `src/KeyProviders.cs` – MDE store provider, AEAD wrap provider, encryptor.
- `Old/CompatMatrix.Old.csproj` – pins 1.0.0-preview07 → `CompatMatrix.Old.dll`.
- `New/CompatMatrix.New.csproj` – pins 2.0.0-preview01 → `CompatMatrix.New.dll`.
- `nuget.config` – nuget.org + local-feed.
- `run-matrix.ps1` – launcher (emulator check, build, cross run, grid, exit code).

## Run
```powershell
docker run -d --name cosmos-emu -p 8081:8081 -p 1234:1234 `
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview
.\run-matrix.ps1                       # http://127.0.0.1:8081 (vnext gateway = HTTP)
.\run-matrix.ps1 -Processor Stream     # force every MDE read through the Stream decrypt path
.\run-matrix.ps1 -Processor Newtonsoft # force every MDE read through the Newtonsoft decrypt path
```
`-Processor both` (default) reads every MDE doc under **both** processors (A/B) and
adds an equivalence cell; `Stream`/`Newtonsoft` force a single read processor.
Exit: `0` all PASS · `1` data break / wrong cell count · `3` emulator unreachable (skips, no hang).

## Matrix
{old-write,new-write} × {old-read,new-read} × algo{MDE=v3, AEAD=v2} ×
**write-proc** × **read-proc** {Newtonsoft, Stream} × read-path{point, query, feed}.
Reads honor the read-proc via the per-request override (`RequestOptions.Properties`
`["encryption-json-processor"]`), so the **Stream DECRYPT path actually runs** and a
doc written under one processor can be read under the other (A/B). For each MDE cell an
extra `…->A/B|equiv` row asserts Newtonsoft and Stream decrypt the SAME `_ei` doc to the
IDENTICAL original (MDE docs are processor-interchangeable).
Stream is MDE-only + preview01/net8-only (preview07 ignores → Newtonsoft).
AEAD+Stream is **unsupported-by-design**. Compression is **N/A**.
Default `-Processor both` yields **42 grid cells** (39 data + 3 equivalence); a single
processor yields 30. The launcher hard-fails if the exact count is off.
