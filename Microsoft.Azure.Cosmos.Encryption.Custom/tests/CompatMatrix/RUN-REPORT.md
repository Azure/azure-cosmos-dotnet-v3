# RUN-REPORT — Encryption.Custom 1.0.0-preview07 ↔ 2.0.0-preview01

Date: 2026-06-29 (hardened 2026-06-30) · Emulator:
`mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview`
(container `cosmos-emu`, **HTTP** gateway `http://127.0.0.1:8081/`).

## 1. Verdict
**PASS=42 FAIL=0 — no data break.** Every OLD-written doc is readable by NEW and every
NEW-written doc (incl. Stream-written MDE) is readable by OLD across point, query and feed.
Reads now honor the read processor via the per-request override, so the **Stream DECRYPT
path runs end-to-end** and each MDE doc is read under BOTH processors with an equivalence
assertion (write-N/read-S and write-S/read-N decrypt the SAME `_ei` doc to the identical
original). Both packages share Cosmos `3.41.0-preview.0`; they differ only in MDE crypto
(0.2.0-pre vs 2.0.0-pre015) and wire fmt is unchanged (MDE=v3, AEAD=v2).
Every cell now rides a **hardened document** (escaped string value + property name, encrypted
object/array with inner nulls, large long, integral + ordinary double — see §7) and asserts a
**BYTE/VALUE-for-value** round-trip of every field, so the grid actually exercises the
Stream-processor data-corruption fixes (§8 proves it catches the pre-fix corruption).

## 2. Inventory of existing back-compat coverage
- `EmulatorTests/MdeCustomEncryptionTests.cs`: `BackCompat_RoundTrip_WithAndWithoutJsonProcessorOverride`, `FetchDataEncryptionKey{Mde,Legacy}`, legacy↔MDE DEK bridging, `ProvidedOutputDecrypt_StreamOverrideWithLegacyAlgorithm_Throws` (stream decrypt rejects non-MDE).
- `Tests/LegacyEncryptionProcessorTests.cs` (AEAD v2, both processors), `DekCacheInteropTests.cs` (L2 wire).
- Gaps filled: cross-**version** (not just in-proc), Stream-written read by preview07, **real Stream DECRYPT honored on reads + cross-processor A/B equivalence** (write-N/read-S and write-S/read-N decrypt the same `_ei` doc identically), query+feed read-paths, explicit AEAD+Stream/compression assertions, deterministic per-cell enumeration.

## 3. Grid (writer × reader × algo × write-proc→read-proc × path)
```
Write      Read      Algo wproc->rproc          point query feed
old-write  old-read  MDE  Newtonsoft->Newtonsoft  PASS  PASS  PASS
old-write  old-read  AEAD Newtonsoft->Newtonsoft  PASS  PASS  PASS
old-write  new-read  MDE  Newtonsoft->Newtonsoft  PASS  PASS  PASS
old-write  new-read  MDE  Newtonsoft->Stream      PASS  PASS  PASS   <- NEW: real Stream DECRYPT of preview07-written MDE
old-write  new-read  MDE  Newtonsoft->A/B|equiv   PASS               <- N==S interchangeable
old-write  new-read  AEAD Newtonsoft->Newtonsoft  PASS  PASS  PASS
new-write  old-read  MDE  Newtonsoft->Newtonsoft  PASS  PASS  PASS
new-write  old-read  MDE  Stream->Newtonsoft      PASS  PASS  PASS   <- preview07 reads preview01 stream-written MDE
new-write  old-read  AEAD Newtonsoft->Newtonsoft  PASS  PASS  PASS
new-write  new-read  MDE  Newtonsoft->Newtonsoft  PASS  PASS  PASS
new-write  new-read  MDE  Newtonsoft->Stream      PASS  PASS  PASS   <- NEW: real Stream DECRYPT (write-N read-S)
new-write  new-read  MDE  Stream->Newtonsoft      PASS  PASS  PASS   <- A/B cross (write-S read-N)
new-write  new-read  MDE  Stream->Stream          PASS  PASS  PASS
new-write  new-read  MDE  Newtonsoft->A/B|equiv   PASS               <- N==S interchangeable
new-write  new-read  MDE  Stream->A/B|equiv       PASS               <- N==S interchangeable
new-write  new-read  AEAD Newtonsoft->Newtonsoft  PASS  PASS  PASS
```
42 cells PASS = 39 data + 3 equivalence. old-end Stream = N/A (preview07 has no Stream → wrote/read Newtonsoft).
`-Processor Stream|Newtonsoft` forces a single read processor (30 cells, no equivalence).

## 4. Unsupported-by-design (asserted)
- AEAD+Stream throws — `EncryptionOptionsExtensions.cs:40-48`; harness write logs `EXPECTED-UNSUPPORTED`; offline `CompatMatrixContractTests.Aead_Stream_Unsupported_Throws`.
- Stream encrypt MDE-only — `EncryptionProcessor.cs:120-128`.
- Stream decrypt rejects non-MDE — `SystemTextJsonStreamAdapter.cs:102-105` (existing `ProvidedOutputDecrypt_StreamOverrideWithLegacyAlgorithm_Throws`).

## 5. Compression
N/A: `CompressionOptions`/`Brotli` absent from the preview01 public surface (verified
via contract + reflection). Asserted by `CompatMatrixContractTests.Compression_NotApplicable_NoCompressionPublicApi`.

## 6. Offline guarantee
Unit `CompatMatrixContractTests` enumerates all **96** cell rows (write × read × algo ×
write-proc × read-proc × path) → 39 SUPPORTED / 36 UNSUPPORTED / 21 NA, plus the
cross-processor interchangeability assertion (`Mde_DocsAreInterchangeableAcrossProcessors`:
write-N/read-S and write-S/read-N are SUPPORTED) and unsupported/compat/count checks
(net6+net8). The 39 SUPPORTED rows equal the harness's 39 data cells (42 grid − 3
equivalence). Subprocesses compile offline; launcher exits 3 (skip, no hang) without
emulator. With emulator: cross-grid runs, exit 1 on any break or wrong cell count.

## 7. Hardened payload (regression coverage for the Stream-processor fixes)
The original doc encrypted ONE ASCII scalar (`/Sensitive`), so the grid was BLIND to the
Stream JSON processor's data-corruption bugs. Every cell now carries (built by `BuildDoc`,
asserted field-by-field by `VerifyDoc`/`Signature`):

| Field | Encrypted? | Value | Catches |
| --- | --- | --- | --- |
| `Sensitive` | yes | `secret::<id>` | baseline scalar |
| `PlainEscaped` | **no** | `p_q=" p_b=\ p_nl=⏎ p_u=é end` | string **double-escape** (plaintext passthrough) |
| `EncEscaped` | yes | `q=" b=\ nl=⏎ tab=⇥ u=é ctl=␁ end` | escaped string through encrypt/decrypt |
| name `esc"name\x` | yes | `named-secret` | **property-name double-escape** |
| `EncObj` | yes | `{"a":null,"b":1}` | **null inside encrypted object** |
| `EncArr` | yes | `[1,null,2]` | **null inside encrypted array** |
| `EncLong` | yes | `9007199254740993` (2^53+1) | large-int precision (double mis-route → `…992`) |
| `EncIntegralDouble` / `EncNormalDouble` | yes | `5.0` / `1234.5` | integral vs ordinary double fidelity |

Doubles compare via round-trippable `"R"` (System.Text.Json emits `5.0` as `5`, Newtonsoft as
`5.0`; both deserialize to `5.0`). The raw assertion also requires `EncObj`/`EncArr` to be stored
as opaque ciphertext and `_ei._ep` to list every encrypted path with **no null/empty entry** —
the direct fingerprint of the null-in-container bug.

**Big-int reject (NOTE, not a cell):** a truly out-of-`Int64` integer is REJECTED on write by
the fixed Stream encryptor (throws), so it would break the round-trip of every cell. It is
therefore intentionally NOT in the doc; the in-range `2^53+1` long already proves the
double-misrouting precision regression (it would corrupt to `…992` if routed through a double).

## 8. Mutation proof (the grid CATCHES the corruption)
To prove the hardened grid is not fake-green, the SAME harness was compiled against the
**UNFIXED repo `src`** (a temp ProjectReference build, deleted after the proof — never committed)
and run write+read on a fresh emulator DB. With the buggy processor the grid FAILS, precisely
flagging the corruption the fixes address:

```
WROTE|FAIL|MDE|Stream|JsonSerializationException ... Path 'EncObj'         <- null-in-container: EncObj corrupted to a JValue
CELL|new-write|new-read|MDE|Newtonsoft->Stream|point|FAIL|
    PlainEscaped got 'p_q=\" p_b=\\ p_nl=\n p_u=é end'
              want 'p_q=" p_b=\ p_nl=\n p_u=é end'                          <- string double-escape on the Stream decrypt path
CELL|new-write|new-read|MDE|Stream->Stream|point|FAIL|JsonSerializationException ... Path 'EncObj'
... (further Stream cells FAIL; Newtonsoft-only and AEAD cells still PASS)
```

Against the FIXED `2.0.0-preview01` package the identical run is **PASS=42 FAIL=0**. So the grid
distinguishes fixed from broken: a regression in the string/property-name double-escape or the
null-in-encrypted-container handling turns at least one hardened Stream cell RED. The fixed
package is left in place; no source or package was left mutated.
