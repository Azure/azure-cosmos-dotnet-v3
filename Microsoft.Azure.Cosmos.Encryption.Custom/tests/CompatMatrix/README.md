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

Before any data cells run, the launcher asks each subprocess for its loaded
`Microsoft.Azure.Cosmos.Encryption.Custom` informational version and fails if
OLD is not `1.0.0-preview07`, NEW is not `2.0.0-preview01`, or both nodes
accidentally load the same assembly version.

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

## Hardened document (regression coverage)
Each cell rides a document built to **exercise the Stream-processor data-corruption fixes**, not just
a single ASCII scalar. Every field must round-trip **BYTE/VALUE-for-value** under Newtonsoft *and*
Stream, and the `…->A/B|equiv` cell now covers all of them:

| Field | On encrypted path? | Catches |
| --- | --- | --- |
| `Sensitive` = `secret::<id>` | yes | baseline ASCII scalar |
| `PlainEscaped` (`"` `\` newline `\u00e9`) | **no** (plaintext) | string **double-escape** in the Stream plaintext-passthrough path |
| `EncEscaped` (`"` `\` newline tab `\u00e9` `\u0001`) | yes | escaped string through encrypt/decrypt (un-escape via `CopyString`) |
| `EncAstral` = `😀𐍈🜨 日本語 العربية \uD83D\uDE00 Z\u0301` | yes | UTF-16 surrogate pairs, multi-script text, and combining-mark fidelity |
| property **name** `esc"name\x` | yes | **property-name double-escape** |
| `EncObj` = `{"a":null,"b":1}` | yes | **null inside an encrypted object** (path-nulling) |
| `EncArr` = `[1,null,2]` | yes | **null inside an encrypted array** |
| `EncLong` = `9007199254740993` (2^53+1) | yes | large-integer precision (double mis-routing → `…992`) |
| `EncIntegralDouble` = `5.0`, `EncNormalDouble` = `1234.5` | yes | integral-vs-ordinary double fidelity |

A truly out-of-`Int64` integer is **rejected on write** by the fixed Stream encryptor (throws), so it is
intentionally *not* in the round-trip doc; that "big-int reject" path is noted in `RUN-REPORT.md` instead
of breaking every cell. The raw assertion additionally requires `EncObj`/`EncArr` to be stored as opaque
ciphertext and `_ei._ep` to list every encrypted path with **no null/empty entry** — the direct fingerprint
of the null-in-container bug.
