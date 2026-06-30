# RUN-REPORT — Encryption.Custom 1.0.0-preview07 ↔ 2.0.0-preview01

Date: 2026-06-29 · Branch: `ec/compat-2.0.0-preview01` · Emulator:
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
