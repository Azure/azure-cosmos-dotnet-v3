# Initial .NET SDK feature/behavior-to-test matrix

[Proposed remediation plan](remediation-plan.md) |
[A-01 intake and immutable identity](remediation-plan.md#a-01) |
[Versioned original navigation inputs][shared-domains]

| Field | Draft receipt |
|---|---|
| Version / recorded | 0.3.6 / 2026-11-02 UTC |
| Status | Evolving proposed matrix with separate local/native testing receipts; no approved complete contract/oracle catalog or whole-SDK assurance |
| Source | A-01 at `52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`, tree `2c89abbd9680af78d5072b523df277d8c46b755d`; this assessment's once-resolved main matched |
| Audit receipt | A-01 SHA-256 `63e368bc66f5c876bb853de62116a9d54ce34f9e1066b5f0b89da8df95b4949c`, 98,011 bytes; original policy provenance preserved in the linked intake |
| Additional policy | Historical `3b141f8c...`, [D-05/testing `532e2401...`][implementation-policy], [D-06 `72e9f579...`][implementation-d06], [D-07 roles/written scope][current-d07], D-08 `12c7cc3a...`, I-02's D-09 `d05b94e4...` and [D-10 v0.2.10][current-policy] at `90368a6ac0fa0434fcf972cd66109ebcf57cda54` retained, including the then-open D-11/D-12 and shared PR8 receipt. Current I-03 input is [handoff v0.2.12][i03-policy] at `447c660872ceaced30a5b5c60abec3bc5c54eb7b`, baseline/domain v0.1.12: D-01..D-12 selected, zero unselected approaches. Original shared PR10 publication and later integration remain distinct below. No factual/oracle/tooling/operational/qualification closure, actual retirement/waiver, review/fanout or new operational/merge authority follows |
| Layout / IDs | Human-readable provisional `docs/assurance/` draft; B-/DB-IDs are local traceability handles, not a selected global schema |

The delivered v0.1.0 matrix SHA-256 `9649d2f7face0e452ed46eaeac1ec22d7bfb7ff9c5a13cf6446db42d889a2600` remains historical. The new user-directed scope requires both focused unit and meaningful emulator/isolated-service evidence for every behavior/test assessed; existing sufficient witnesses count. [I-01's versioned spec](specs/i-01-encryption-patch-paired-tests.md) precedes implementation. Its new evidence supplements, rather than relabels, A-01.

## 1. How to read the matrix

Four inputs stay **distinct**: 37 navigation lanes (F-01..F-31, N-01..N-06), eight proposed atomic candidates (N-I01..N-I08), 28 critical seeds (SC-01..SC-28), and 12 general database families (local DB-01..DB-12). None is a coverage denominator, availability promise or executed case count. The initial B-rows split selected assertion witnesses into actionable behaviors; they are not a whole-SDK assertion census. Overlap between lanes/seeds/behaviors earns no duplicate assurance credit.

A-01's historical assertion witnesses and CI aliases are reused below. Fresh implementation evidence is isolated in increment-specific receipts, not attributed to other rows. An assertion read in source is not a discovered or executed test. "No explicit invocation" concerns A-01's inspected 33 YAML files, not every possible external registration. Unknown execution is not zero tests, a functional defect, unsupported behavior, N/A, passing evidence or an approved exclusion.

**Authority:** all intended SDK rules below remain proposed independent-authority/oracle questions (`Q`). Existing assertions (`A`) show what a test observes, not independent contract approval. Mocks/manual objects, header observers and helper-produced statuses retain their narrow scope. A-01 establishes no SDK functional defect merely from these observations.

**Gap classes:** `CFG` configuration distinction; `SEL` explicit selection gap; `ART` artifact/consumer identity; `OBS` partial assertion/observer; `SEQ` unclosed sequence; `COHORT` discovery/attempt receipt; `FIX` fixture/capability fidelity; `ATTR` metric attribution/bookkeeping; `TRACE` spec/mapping closure; `EXT` external/evaluated inputs; `FAIL-U` observed failure with unknown cause; `ORACLE` unapproved independent semantics. Confirmed text/observer gaps and execution unknowns remain separate.

**Execution/artifact column:** `U` means no case-level, attempt/configuration/artifact-bound receipt was obtained for that row. `SC` means the **declared** test-project binding is current core source plus packaged Direct/HybridRow; the actual loaded payload is still U. `ENC`/`CUSTOM` mean declared companion reference modes, not proof of current-core or installed-package execution. Run metadata elsewhere is not silently attached to a B-row. Exact method/data-row identifiers are follow-up where A-01 preserved only a file/assertion span.

Every B-row inherits [G-01](remediation-plan.md#g-01), [G-03](remediation-plan.md#g-03), [G-07](remediation-plan.md#g-07) and, where relevant, [G-08](remediation-plan.md#g-08) receipt prerequisites. Its specific gap/work/control is in the last column. Each follow-up must preserve affected and neighboring features and pass the [bounded proof checklist](remediation-plan.md#5-common-proof-required-for-a-future-implemented-slice), not just add a test name.

## 2. Existing CI aliases and declared cells

These are A-01 configuration aliases, not new jobs or actual-execution receipts.

| Alias / established span | Declared selection/configuration and artifact | Boundary to retain |
|---|---|---|
| J-U [core units][j-unit] | Caller filter/OS; ordinary PR excludes Quarantine/Ignore and can include Flaky; SC | Generic command omits `BuildConfiguration`; labels do not prove Release. Compile/discovery/execution U |
| J-UF [core Flaky][j-unit-flaky] | Hardcoded Flaky; task retry 4; SC | Caller exclusions not retained; overlap/multi-tag disposition U |
| J-C [coverage][j-coverage] | IncludeCoverage; core unit only, Debug, caller filter, Cobertura | Not emulator/live/companion/staged-source/consumer coverage; no measured score |
| J-M [allocation gate][j-memory] | IncludePerformance; Release/net6 GateBenchmark, Short/memory/BaselineValidation | Workload result/cohort U; [comparator bookkeeping][p-perf-validation] is narrower than general performance assurance |
| J-E1 [ordinary emulator bucket 1][j-emulator] | Windows parameter; ClientTelemetryEmulator/Query/ReadFeed/Batch/ChangeFeed; SC | Ordinary exclusions include Flaky, Quarantine, Functional, ThinClient, LongRunning, MultiRegion, MultiRegionAad, MultiMaster, Ignore and release telemetry |
| J-E2 [ordinary emulator bucket 2][j-emulator] | Complementary ordinary category bucket; task retry 2; SC | Same exclusions; actual discovery/fixture/runtime U |
| J-EF [emulator Flaky][j-emulator-flaky] | Hardcoded Flaky; retry 4; SC | Ordinary bucket exclusions not retained; no independently verified first-attempt receipt |
| J-CE [companion emulator][j-companion-emulator] | IncludeEncryption; existing Encryption/Custom emulator projects; configuration forwarded | Not companion units. Default published SDK dependencies are not current-core cross-feature proof |
| J-LR [live MultiRegion][j-regions] | MultiRegion, external account configuration; retry 2 | Actual topology, consistency/capabilities and cohort U |
| J-LW [live MultiMaster][j-regions] | MultiMaster, external account configuration; retry 2 | Writer count/regions/capabilities and cohort U |
| J-P [dedicated preview units][j-preview] | Explicit IsPreview/configuration; caller filter; SC | Different from rolling's generic preview-labelled call; actual compiled/executed rows U |
| J-PF [preview Flaky][j-preview] | Flaky-only separate selection; retry 4; SC | Normal/Flaky overlap and first outcomes U |
| J-I [internal build][j-internal] | INTERNAL solution compilation; runtime 6 and SDK 8/9 installation declarations | Build-only: no assertion invocation in this template |
| J-A [live AAD][j-aad] | Windows/nonempty connection; strict MultiRegionAad; retry 2; nonempty all-passed leaf/no-skip gate | Strong observed-leaf guard, not complete expected-cohort or first-attempt proof. Only W-05 explicitly declares both modes; other selected data-plane witnesses default Direct |
| J-T [ThinClient][j-thin] | ThinClient/env/account/emulator; `always()`; job/task `continueOnError` | Advisory selection, not sufficient required behavior proof |
| J-F [FaultInjection][j-fi] | PR named unit subset includes FaultInjectionUnitTests/FaultInjectionBuilderValidationTests; build, no-build run, pack validation; optional live integration | W-16 conditional Inconclusive; no corresponding all-required-leaf/no-skip gate established |
| J-B [consumer/benchmark/parity builds][t-preview] | Existing source/package-symbol/override builds, samples/CTL/benchmark/internal compilation | Build-only, not behavioral execution or a measured benchmark |
| J-PACK [core pack][t-pack] and companion pack templates | Separate core/companion nupkg production | Actual package contents/digests and installed consumer binding U; source tests do not consume a sibling pack job by default |

The existing [Encryption unit project][p-encryption-unit] and [Custom unit project][p-custom-unit] had **no explicit invocation** in A-01's inspected CI. I-01 adds executed Encryption unit tasks within J-U and the existing preview Encryption job. I-02 adds one Custom unit task in J-U and one in the established Custom preview parity job, retaining caller filters/both TFMs and default preview core-package binding. Repaired c9e118/build 64334 passes ordinary/preview units, all twelve new service cells and both unchanged source-preview parity builds. Earlier c9ed paired passes/compiler failure and 64329 canceled-parent skips remain separate. Fuzz and Custom-performance selection remain unresolved/parked prior art, not new campaigns.

### I-01 paired-layer supplement: B-034

This is the first bounded assessed slice, not closure of N-04, F-06 or F-21. Root encrypted/plain Increment is in scope; nested/floating-point/filter/batch/replay and the broader representation/key lifecycle remain unexpanded. Unit helper objects are not the service half. See the [spec](specs/i-01-encryption-patch-paired-tests.md) for exact cases, authority limitations, preservation controls and candidate/configuration requirements.

| Layer / intended witness | Exact assertion / preservation | Variant / established selection | Added, selected, executed and artifact state |
|---|---|---|---|
| Unit: `EncryptPatchOperationsAsync_Increment_On_Encrypted_Path_Throws` | Require exception/path for original single and encrypted-first/last mixed lists; retain input operation identity/order | net6, Release; package GA/preview within J-U/preview Encryption job; current-core source checks are distinct | Exact three rejected leaves passed in each of four local bindings and native GA/preview runs 1331299/1331287. Original runtime failure retained; expanded pre-execution discovery not captured |
| Unit: `EncryptPatchOperationsAsync_Increment_On_NonEncrypted_Path_Passes_Through` | Exact count/order/reference identity for original single and two plain increments | Same; dependency/output hashes distinct for package 3.51.0/3.52.0-preview.0 versus current source 3.63.0/3.64.0-preview.0 | Exact two accepted leaves passed in each local binding and both native unit runs. Local package projects and native unit projects each passed 58 leaves, not 58 closed obligations |
| Existing integration: `EncryptionPatchItem` | Preserve broad encrypted/plain patch/diagnostics sequence; require formerly optional rejection assertion | Existing J-CE ordinary/preview package-backed Release/net6; fixture defaults Direct | Observer corrected; passed in native GA run 1331291/result 100016 and preview run 1331290/result 100029. Broad sequence is not completely paired/qualified |
| New integration: `EncryptionPatchItemIncrement` | Gateway/Direct x typed/stream x encrypted-first/last; required exception/path, logical Patch count 0, exact fresh whole-item read and unchanged ETag; valid plain Increment OK/exact value, observer count 1, exact fresh whole-item read and changed ETag | Eight rows in existing J-CE ordinary/preview project selection; real policy-v2/key/Patch fixture with fake KEK wrapping only | All eight exact argument leaves passed in native GA 1331291 and preview 1331290 on task attempt 1; original TRX/subresults and command/host receipts obtained. No current-source service run or complete installed-payload/fixture snapshot |
| Local harness rule: explicit companion selection | Native project/condition/command/filter plus expected/discovered/executed TRX receipt; no meaningful live assertion for YAML itself | Existing tasks/runtimes/limits and published-reference matching J-CE retained; YAML comparison found only two additional tasks | Both native new unit tasks executed/published and both service tasks executed on attempt 1. Proposed treatment remains selection proof PLUS paired service behavior, not a waiver or all-G-02 closure |

The [spec's local receipt](specs/i-01-encryption-patch-paired-tests.md#actual-local-resultconfiguration-receipt) binds final five-case/full-project outcomes to local configurations, original failures and hashes. Local macOS ARM64 / SDK 9.0.304 / private runtime 6.0.36 is not the native Windows receipt. The [native receipt](specs/i-01-encryption-patch-paired-tests.md#native-prci-receipt) separately identifies Release/net6, SDK/compiler 8.0.427, VSTest 17.11.1 x64, installed runtime 6.0.36, source/package inputs, host and executed cohorts. Exact loaded payloads, built-nupkg consumers and complete runtime fixture identity remain unknown.

CLR operation reference identity has no direct live-service observer: proposed treatment is its local identity/order assertion plus real-service value/state preservation, subject to review before closure. The document comparison observes the modeled `TestDoc` payload and separately checked ETag, not arbitrary unknown JSON/system properties.

The delivered v0.2.1 matrix SHA-256 `3b66e1ce43f62a979ed6e895b12e70ac39caf7b60ab3208fcbe622f08c6185e5` remains historical. [The pushed spec v0.1.3](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/ee19f83e9e13d4f98790a6fbfe09dd4bd087ad29/docs/assurance/specs/i-01-encryption-patch-paired-tests.md#changed-files-and-remaining-prerequisites) records the publishing probe and local ARM64 vNext compatibility boundary (Gateway-only, CE-key endpoints no-op). Those historical/local probes are not service-case execution; native CI subsequently supplied the real service cells below.

[SDK PR #6182](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6182) carries tested source candidate `ee19f83e9e13d4f98790a6fbfe09dd4bd087ad29`; native build 64324 uses merge candidate `0babdeb0748c59bd2c552fd3c1a06bb8b63515ef`. Existing Azure CLI authentication made scoped receipts readable after retained anonymous HTTP 401s. Every service cell below passed in both runs; subresult IDs 1..8 are under GA result 100037 and preview result 100040. This supplements historical/local evidence; later docs-only revisions do not relabel these results as a new candidate's execution.

| Service cell / exact arguments `(useGateway,useStream,encryptedFirst)` | Transport / API / rejected order | Unit counterpart | Actual service state |
|---|---|---|---|
| `(true,true,true)` | Gateway / stream / encrypted first | Mixed encrypted-first plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 1 |
| `(true,true,false)` | Gateway / stream / encrypted last | Mixed encrypted-last plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 2 |
| `(true,false,true)` | Gateway / typed / encrypted first | Mixed encrypted-first plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 3 |
| `(true,false,false)` | Gateway / typed / encrypted last | Mixed encrypted-last plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 4 |
| `(false,true,true)` | Direct / stream / encrypted first | Mixed encrypted-first plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 5 |
| `(false,true,false)` | Direct / stream / encrypted last | Mixed encrypted-last plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 6 |
| `(false,false,true)` | Direct / typed / encrypted first | Mixed encrypted-first plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 7 |
| `(false,false,false)` | Direct / typed / encrypted last | Mixed encrypted-last plus accepted-single unit rows | Passed: GA 1331291 + preview 1331290, subresult 8 |

The wider service cohorts retain one existing ignored `EncryptionChangeFeedDecryptionSuccessful` each: GA has 44 passed/one ignored terminal leaf; preview has 47 passed/one ignored. Aggregate API/TRX-parent counters differ and are preserved in the spec, not substituted for leaf counts. No I-01 required leaf was skipped/inconclusive. These project totals do not close every existing behavior's counterpart or the ignored change-feed obligation.

### I-02 paired-layer supplement: B-035/B-036

The [versioned I-02 spec](specs/i-02-custom-cache-paired-tests.md) was frozen
before test/CI edits, including a baseline-driven harness amendment. These
are existing Q/oracle-unqualified expectations with additional protection,
not independent approval of cache/cancellation contracts. Timeout/internal
cache cancellation with a live caller and caller pre-cancellation are
different cells; neither proves no commit for arbitrary in-flight cancellation.

| Layer / exact witness | Shared behavior and preservation controls | Variant / established selection | Added versus actual execution |
|---|---|---|---|
| Unit B-035: `GetAsync_ThrowsTimeoutException_FallsBackToCosmos`; `GetAsync_ThrowsOperationCanceledException_CallerTokenNotCancelled_MustNotFailTheOperation` | One cold L2 lookup; exact original id/context/token and returned-properties identity at one source fetch; caller stays live; warm read repeats neither L2 nor source | Existing cache contract class, net6/net8; ordinary/preview default core package `3.41.0-preview.0`; distinct current-source preview comparison | Strengthened, not duplicated. Repaired c9e118/build 64334 ordinary runs 1331452/net6 and 1331451/net8; preview 1331443/net6 and 1331444/net8: exact seeds and all 13 cache leaves passed, within full 424/424 and 755/755 cohorts per variant |
| Unit B-036: `GetOrAddDekPropertiesAsync_CallerTokenAlreadyCancelled_ThrowsOperationCanceledExceptionPromptly` | Required cancellation, source/L2 count 0; same-cache uncancelled control reaches each once and returns exact properties | Same; original three seed identities and ten other cache-class leaves preserved | Passed in all four repaired-candidate native unit runs above. Four unrelated UTA007 legacy methods remain undiscoverable backlog, not executed or credited |
| Service B-035: `EncryptionDistributedCacheReadFailureFallsBack` | Fault optional L2 only; public real provider/encryptor; unique real key and item, successful encrypted Create, independent stored ciphertext/plain-field check, separate cold-provider decrypt/read and exact modeled values; real key-read observer positive at each cold lookup | Gateway/Direct x timeout/internal-OCE, typed APIs, net6/net8; existing ordinary J-CE Custom project, no invented preview-service lane | All four exact rows passed on repaired c9e118 in runs 1331436/net6 and 1331437/net8. Original c9ed passes remain historical; complete fixture/loaded-artifact identity remains unqualified |
| Service B-036: `EncryptionDistributedCacheCallerCancellation` | Pre-cancelled encrypted Create must throw, L2/key reads 0 and independent item read 404; same-instance uncancelled Create/read succeeds with real key-read observer active | Gateway/Direct, typed APIs, net6/net8, same J-CE | Both exact rows passed in each repaired-candidate native service run above; item absence is bounded to this pre-dispatch case |
| Test-only harness: `ScopeRecord_Properties_AreCorrect`; `ScopeRecord_Elapsed_UsesStopwatchFrequency` | Original one-second expectation unchanged; correct Stopwatch-frequency conversion and exact 0/1/2-second controls | Existing diagnostics test helper, not production timing | Both original baselines failed on macOS ARM64; originals retained. All four witness leaves and all 39 diagnostics leaves passed in each repaired native unit variant/TFM. Proposed local-only treatment surfaced; no meaningless live arithmetic test or product-layer waiver |
| MDE preservation: `ValidateEncryptDecryptDocument_VerifyByNewtonsoft` and existing same-helper neighbors | Nonnull sensitive property is retained as a string, differs from plaintext and contains nonempty base64 bytes; null/untargeted fields, metadata and exact decrypted-value controls remain | Existing MDE unit class, distinct current-core preview net6/net8; same representation observer in the real service fault cases | Existing class passed 8 net6/24 net8 leaves in each repaired native unit variant, plus all twelve new native service cells. Earlier focused source and c9ed receipts remain separate. Original wrong observer never executed; no new public format/oracle approval |
| Selection/configuration | Exactly one new Custom unit task per existing ordinary/preview job, both TFMs, original filters/reference parity/retries retained | Default Custom dependency is preview even in ordinary job; current-source preview is separate | Both native unit invocations/TFMs/leaves and both unchanged source-preview parity gates passed on c9e118/build 64334; all five required tasks attempt 1. Original compiler failure and local controls retained; source solution build still has 16 warnings/zero errors |

| Exact service arguments | Transport / fault | Unit counterpart | Actual service state |
|---|---|---|---|
| `ReadFailureFallsBack(true,false)` | Gateway / timeout | B-035 timeout seed | Passed: repaired c9e118/build 64334 net6 run 1331436 + net8 run 1331437; not executed locally |
| `ReadFailureFallsBack(true,true)` | Gateway / internal cancellation | B-035 internal-OCE seed | Passed: same two native runs; not executed locally |
| `ReadFailureFallsBack(false,false)` | Direct / timeout | B-035 timeout seed | Passed: same two native runs; not executed locally |
| `ReadFailureFallsBack(false,true)` | Direct / internal cancellation | B-035 internal-OCE seed | Passed: same two native runs; not executed locally |
| `CallerCancellation(true)` | Gateway / caller pre-cancelled | B-036 cancellation seed | Passed: same two native runs; not executed locally |
| `CallerCancellation(false)` | Direct / caller pre-cancelled | B-036 cancellation seed | Passed: same two native runs; not executed locally |

The method prefixes above abbreviate the exact `EncryptionDistributedCache...`
names in the layer table; each TFM needs its own six-leaf native receipt.
The six-SDK D-08 manifest remains .NET inspected; Java/Python/Go/Rust/JS/TS
applicability/oracles are unverified, not equivalent or unsupported. This
increment dispatches no peer work. D-09 introduces no test dependency or
campaign here. Stream, in-flight cancellation, expiry/write races, provider
isolation and every broader Custom obligation retain separate follow-up.

Published I-02 source is `d5004b187a04adf6a5f87a3197b912b217c8aae2`,
tree `b1de7ba171479d9bc26f32f5eba1c43df7be3c83`; automatically queued
build 64329 uses merge candidate `21b9f761f022f689399c496978693d4625c8cfb7`
with that same tree and verified A-01/source parents. Native case receipts
at `23:27:48Z` were still pending. The later observation records all three
Custom tasks skipped under canceled parent jobs, with no Custom result run;
actor/reason is not established. Its incorrect raw field-removal assertion
never executed and earns no passed credit. Corrected source c9ed and actual
merge 24003/build 64330 provide the separate native unit/service results above.
All required leaves passed, task attempt 1; full service terminal cohorts are
79/net6 and 85/net8, not API parent counts 75/81 or raw TRX 81/87.
Original digests/commands and the separate failed preview predecessor are in
[historical c9ed receipt](specs/i-02-custom-cache-paired-tests.md#native-pr-ci-receipt-corrected-observer-before-source-parity-repair).
Subsequent repaired source `c9e11829da397192338459b9d8c58edbe5d7860d`,
tree `1f2e6156bd1cb0c5e428f8ec48eac00875e746f0`, actual merge
`de046306064296ee62a92fb8dfc9639616fe470c`, build 64334 now has the separate
ordinary/preview unit and service receipts above. Both existing parity gates
also passed. [Spec v0.1.7](specs/i-02-custom-cache-paired-tests.md#native-pr-ci-receipt-repaired-candidate)
retains all six original TRX digests, exact required leaves and first-attempt
records; reconciliation SHA-256
`7be7ef6a5a7deaf9532ce1f86b072895a19462f40d8d6ef190d1de01016bc3bf`.
The overall build was still in progress at the bounded `01:11:20Z` observation;
this is not all-checks-clean or a merge recommendation. A later documentation
head is not the executed source. The local no-restore graph failure and
existing-graph restore/rebuild remain historical.
D-10's later profile-envelope publication changes no
resource/execution authority, and concrete trust/isolation/capacity/cleanup/
budget/retention inputs remain unqualified; it does not block unrelated
separately authorized testing or waive its required evidence.
The existing class cleanup targets its unique fixture but does not assert its
delete response; test success alone does not qualify cleanup/lease fencing.

### I-03 legacy discovery and paired protection: G-07 / N-05

The [I-03 spec](specs/i-03-custom-legacy-paired-tests.md) was frozen before
test edits, SHA-256
`fc42ebd194dae1603c98d433aeda49f46c1e34e7b3f7f9486c4c436d2688a8b4`,
and persisted verbatim in pushed commit
`721e7b2f51239895bb99cafa0eb91ae6afd5d082`. This is a separate child from
I-01/I-02 head `7330088136240c8586ed5eea63f2a4f9a0c366e5`, tree
`ec1584846a7b553195840a174b07bb551ebde57d`, targeting
`kuthapar-microsoft-net-assurance-gap-plan`; no I-03 result is assigned to
build 64334 or PR6182.

The following **supplemental subrows**, not new historical B-IDs or coverage
denominators, refine the N-05/G-07 Custom backlog. All 41 historical B-IDs,
37 navigation lanes, eight atomic candidates, 28 scenario seeds, 12 families
and G-01..G-12 remain intact. Legacy is obsolete-but-present, not retired.
Existing implementation/negative tests justify unchanged-behavior protection;
independent .NET/service oracle and D-12 authority qualification remain open.

| Supplemental row / exact unit identity | Intended observer / preservation | Actual unit receipt | Paired real-service mapping / actual state |
|---|---|---|---|
| I-03.L1 `InvalidPathToEncrypt` | Present `/SensitiveStr` encrypts; absent `/Invalid` stays absent. Untargeted integer/array/dictionary/plain fields unchanged; exact format-2 metadata and exactly one decrypted path | Newtonsoft `(0)` passed in ordinary/preview net6/net8 and source preview; Stream `(1)` net8 explicitly rejects without encryptor calls/input mutation | New `EncryptionLegacyProcessorRoundTrip` missing-path rows, Gateway/Direct, both TFMs: compiled and one parent listed per assembly, not executed |
| I-03.L2 `EncryptDecryptPropertyWithNullValue` | Present JSON null restored, all other modeled fields/types exact; selected fields absent from raw legacy document; exact context | Same variants; original null body retained and strengthened. Stream row is rejection, not a null round-trip | New null rows, Gateway/Direct, both TFMs; independent raw read plus typed/stream/decryptable reads implemented, execution pending |
| I-03.L3 `ValidateEncryptDecryptDocument` | Every selected string/integer/array/dictionary removed from plaintext; nonempty encrypted bytes; exact key/algorithm/version/configured paths; exact decrypted document and context | Same variants and boundary disposition | Existing `EncryptionCreateItem` remains a normal typed/stream witness. New normal rows add both transports, wider data and raw-format control; no new native execution claimed |
| I-03.L4 `ValidateDecryptStream` and plaintext control | Nonempty seekable decrypted output at position zero, exact document/context; plaintext retains same stream, length/content, zero position and null context | Legacy class 5/5 net6 and 9/9 net8 in both package variants; same exact leaves pass source preview | Every new row performs public stream reads; net8 selects Stream for decryption fallback after rejected Stream Create and successful Newtonsoft Create. Six declared leaves per TFM, none locally executed |

Baseline execution passed only one plaintext leaf per TFM and emitted all
four UTA007 warnings; hidden rows were neither passed nor skipped. A retained
signature-only candidate exposed five passing / four failing net8 leaves,
all four failures the explicit unsupported Stream-encryption error. Final
ordinary/preview full Custom units each pass 428/428 net6 and 763/763 net8,
with zero failed/skipped/inconclusive results, exact five/nine Legacy leaves,
13 cache, 39 diagnostics and 8/24 MDE neighbors. Focused source preview passes
14/14 and 41/41. [Spec v0.1.3](specs/i-03-custom-legacy-paired-tests.md#actual-local-receipt)
retains original/final TRX digests, source-file blobs, commands and the
introduced CS0200 compile failure followed by its typed-dictionary repair.

Both emulator TFMs compile in package Release and source-preview
Debug/Optimize with `SDKPROJECTREF` on/off. Package core
`3.41.0-preview.0`, source core `3.64.0-preview.0`, compiled dependency
identity, loaded payload and installed consumers remain distinct.
The existing unit/service tasks and both native parity gates are unchanged.
Root PR CI targets main/releases/* and has `trigger: none`; the requested
stacked feature target is not automatically selected by that YAML.
Native service/build/attempt/TRX proof remains unobtained. Following actual
publication and absent automatic selection, the later user choice permits
exactly one existing Windows/emulator PR-validation build with current
limits/fixtures, after safe definition/source/branch/reason inspection.
That allowance is unconsumed. No new filter/job/evaluator, second queue/
rerun/cancellation, retarget, resource/permission/release/merge change,
local Gateway-only substitute or borrowed I-02 execution is allowed.
Source `3748a6a873f08cb9a5bfb71fb2d1ab581faa6d77`, tree
`8f2e7bc9903cd8a471e755232a55cf7c6904a29a`, is pushed with the validated
test-file blobs. Standard linked-account publication failed **403:
Enterprise Managed User cannot access this content**. Read-only observation
at `2026-11-02T01:55:05Z` found no matching PR, zero head checks/statuses;
aggregate `pending` with zero entries is not queued execution. No new PR URL
or actual PR/native merge/build/attempt/service/TRX identity is available.
No alternate personal publisher or login/permission change was used.
After the user reported correcting the repository account to personal,
the standard publication retry for receipt head
`9697fa53f1602f04a93f26f48bfd9e33bee5a457` again received the same EMU
403, request `CDF6:3B1635:81A46:BB23F:6AE7F087`. The duplicate check at
`2026-11-02T02:08:21Z` still found no matching PR. The tool exposes neither
the actual account selection nor the mismatch's cause; direct app inspection
is safety-blocked and was not bypassed. Publication remains the prerequisite
to observing selection and consuming the single authorized validation run.

For this substantive update, shared source
`447c660872ceaced30a5b5c60abec3bc5c54eb7b` is distinct from original
[kushagraThapar/SuicideSquad#10](https://github.com/kushagraThapar/SuicideSquad/pull/10)
OPEN publication and its later observed `2026-11-02T01:12:37Z` integration
to `79fe48b9cb00896e1bb04095ae2fde684b480914`. D-11 source
`725724e4cb6f24c75191cb9c192a48b596366eec`, original
[kushagraThapar/SuicideSquad#9](https://github.com/kushagraThapar/SuicideSquad/pull/9)
publication and later `2e7d97f46ce2ee28cd1a6a707a596a6e33ab167e`
integration are also retained separately. Earlier D-10-frozen receipt history
is unchanged; zero unselected approaches is not missing-proof closure,
retirement/waiver authority or M-01 evaluator/CI implementation activation.

**Counterpart backlog for every other existing B-row and unexpanded lane:** unit and emulator/live counterpart identifiers, exact shared intended rule, preservation neighbors, eligibility/transport/runtime and separate actual artifact/execution receipts are **unreconciled**, not presumed absent or satisfied. Retain G-06.F-/N-, G-06.SC-, database families and G-10 follow-ups below to expand this progressively. A local/compile-time rule or unsupported capability must surface its explicit proposed treatment before closure. The new paired expectation applies to existing tests as well as new ones; four navigation input sets remain distinct.

### Configuration and decision-cell constraints

| Axis | Retained facts and required unresolved cells |
|---|---|
| Transport | Gateway and Direct are in D-01 scope; not every witness runs both. ThinClient HTTP/2 advertisement/fallback and physical RNTBD behavior are separate capability/transport questions |
| GA / preview / internal | G/P below means source/CI eligibility subject to actual compilation/filtering, not executed GA+preview proof. IsNightly can define preview symbols. Public DTX is PREVIEW; internal unit access exists outside preview; INTERNAL template is build-only |
| TFM / runtime / host | Core compile `netstandard2.0`, selected core tests `net6.0`, usually runtime 6 + SDK 8 requested; SDK installation is not runtime-8 core execution. Custom source/tests have `net8.0` as well as their recorded lower TFMs. Effective OneES OS/image/architecture/patch/roll-forward U |
| Capability / consistency | Emulator declares Strong, DisableRateLimiting, preview-related switches and PartitionCount 10 with external MSI. Live account topology/consistency/capabilities U. Manual/mocked fixture state is not a live service receipt |
| Status / origin / commit | Preserve operation/resource, HTTP+substatus, producer/origin/transformation, transport/artifact, transmission/possible commit, consistency/session, options/capability, topology/cache, remaining budgets, and resulting retry/refresh/route/delay/surface/diagnostics/cleanup |
| Budgets / lifetime | No new numeric limits. Unless explicitly witnessed, configured attempts/delay/deadline/resource limits and completion/cancellation races are unexercised/U. Wall-clock sleeping is not controlled-clock proof. Timeout/cancellation is not non-commit |
| Artifact | SC is source plus packaged Direct, not the staged Direct project or installed nupkg. `ProjectRef`, `SdkProjectRef`/`SDKPROJECTREF` and `OSSProjectRef` are distinct. Companion default published dependencies and current-source parity builds stay separate |
| Independent oracle | Concept/cross-SDK materials frame questions only; current .NET/service authority, allowed outcomes/ties/token scopes and disputed behavior remain D-05. No mocks-as-durability, pagination snapshot, cross-partition bulk atomicity or universal exactly-once change feed |

## 3. Actionable initial behavior rows

### Options, bulk, retry, change feed and DTX

| Behavior ID / lanes | Intended observable rule + authority | Cells / interacting features | Existing method, assertion and fixture | Established job/filter | Execution / artifact receipt | Specific gap / remediation / proof needed |
|---|---|---|---|---|---|---|
| B-001; F-02/F-06 | Conflicting nonnull serializer selections reject with ArgumentException; Q, N-I01 | G/P eligibility; local option setters, not a transport claim; default/custom/STJ and companion representation | [ThrowOnSerializerOptionsWithCustomSerializer, reverse order, and custom/options with STJ][a-serializer]; A: all three nonnull pairs/both orders reject | J-U/J-P | U; declared SC | OBS/ORACLE; [G-05](remediation-plan.md#g-05), G-06/G-10. Add reviewed null/reset and state-after-rejection cases; silently replacing a selection or corrupting unrelated options must fail |
| B-002; F-02/F-09 | Builder preserves supported selected read strategy; Q, N-I02 | G/P, mock-backed construction; strategy/legacy consistency interaction; no service visibility claim | [VerifyReadConsistencyStrategyBuilderProperties][a-builder]; A: default null, explicit LatestCommitted, enum loop preserves value/legacy unset | J-U/J-P | U; declared SC | ORACLE/SEQ; G-06/G-10. Independently reconcile supported enum/account cells; dropped value or changed unrelated legacy setting is a control |
| B-003; F-02/F-09 | Valid request strategy takes precedence over client strategy with correct header/hub mapping; Q, N-I03 | Nonbulk reads; legacy unset; unit header observer and Direct emulator; Gateway/topology eligibility U | [ReadConsistencyStrategyRequestLevelOverridesClientLevel][a-precedence] and [Direct emulator same-name method][a-precedence-emulator]; A: request Session/client Eventual, and LastCommittedSingleWriteRegion maps to LatestCommitted+hub, no legacy header | J-U/J-P; J-E2 for observed emulator witness | U; declared SC | OBS/ORACLE/FIX; G-05/G-06. Actual outbound/configuration authority needed; client-wins, wrong hub bit or leaked legacy header must fail; preserve valid account/strategy neighbors |
| B-004; F-20/F-24 | Unsupported request throughput bucket rejects before bulk operation materialization/dispatch; Q, N-I04 | Bulk point operation; bucket 1/null fixture; local validation has no transmission witness; client-level/nonbulk neighbors | [ValidateThroughputBucketRequestOptionThrows][a-bulk]; A: direct validation throws InvalidOperationException for 1, null accepted | J-U/J-P | U; declared SC | Confirmed OBS; [G-05 bulk slice](remediation-plan.md#g-05). Actual entry-path zero materialization/serialization/send journal; silently ignored bucket or validation after work must fail |
| B-005; F-10/F-11 | Isolated non-DTX barrier `408/21013` returns ShouldRetry=false at ClientRetryPolicy and does not poison partition health; Q, N-I05 | Direct barrier origin; not generic 408, DTX or outer policy; budget/possible-commit behavior U | [RequestTimeoutWithWriteBarrierThrottledSubStatusDoesNotMarkEndpointUnavailable and ...ReturnsNoRetry][a-barrier]; A: no failover state and ShouldRetry=false | J-U/J-P | U; declared SC | ORACLE/SEQ/ART; G-05/G-06. Preserve exact no-retry/no-health slice; generic timeout, outer retry and other partitions are distinct neighbors, not assumed outcomes |
| B-006; F-13/F-02 | Client configuration reaches actual writer/transport boundary; Q, propagation question under N-I05 | Direct producer/artifact and client constructor/factory chain; true/false and generic/DTX origin cells U | [EndToEndConstructorChainWriteBarrierEarlyYieldThroughConsistencyWriter][a-barrier-producer]; A: writer fixture produces 21013; [helper directly constructs writer with mocks/flag][a-barrier-helper] | J-U/J-P for helper test | U; declared SC/packaged Direct boundary | Confirmed OBS/ART; G-05/G-03. Observe real client-chain configuration, not injected final flag; lost/inverted propagation must fail. No full client/RNTBD E2E evidence |
| B-007; F-22/F-18 | 304 retains usable same-scope continuation, not merely different bytes; Q, N-I06 | Mock unit 304; range/RID/mode/version/token scope; no live/topology guarantee | [ChangeFeedIteratorCore_UpdatesContinuation_On304][a-cf-unit]; A: old/new tokens differ only | J-U/J-P | U; declared SC | Confirmed OBS; G-05. Explicit status/nonempty usable opaque state and independent resume journal; omitted/stale continuation must fail; preserve token-format opacity |
| B-008; F-22/F-18 | Drain to 304, resume same approved scope, read exact newly written item; Q, N-I06 | Live AAD/default Direct, same range/incremental fixture; retention/merge/timestamp cells U; conditional read 304 distinct | [AadChangeFeedContinuationAsync][a-cf-aad]; A: 304, nonempty token, resumed result only second item. [Emulator ReadAll][a-cf-emulator] is weaker aggregate-50 prior art without explicit 304 | J-A; J-E1 for weaker emulator witness | U; declared SC/live capability U | Strong existing A, broader scope/ORACLE/U execution; G-05/G-06/G-08. Reuse this pattern; stale token/replayed drained item fails in the approved fixed same-scope journal, not a universal dedup guarantee |
| B-009; N-02/F-10 | Retriable non-durably-aborted commit replays current idempotency token while budget remains; Q, N-I07 | Internal unit G/P; public PREVIEW only; synthetic 503/isRetriable, frozen operations; real commit state U | [CommitTransaction_NRetriableNonAbortedReplaysSameToken][a-dtx-replay]; A: four retriable replies then success, five nonempty tokens/one distinct; [factory][a-dtx-factories] supplies outcome | J-U/J-P | U; declared SC/mock commit | ORACLE/SEQ; G-06/G-08. Independent coordinator protocol and token/body/budget journal; premature rotation is control, not proof of live durability |
| B-010; N-02/F-10 | Approved terminal-abort retry rotates token without changing frozen body; Q, N-I08 | Same surface; synthetic 452/isRetriable, remaining budgets; durable-abort authority U | [CommitTransaction_SendsNewIdempotencyTokenOnEachRetry][a-dtx-rotate] and [...NRetriableAbortsProduceNPlusOneDistinctTokens][a-dtx-aborts]; A: distinct nonempty tokens, byte-identical bodies, N+1 tokens | J-U/J-P | U; declared SC/mock commit | ORACLE/SEQ; G-06. Independent terminal-abort model; reused terminal token or changed operation snapshot must fail. Factory labels do not prove server abort |
| B-011; N-02/F-10 | Abort then nonabort rotates then replays, rather than treating all retries alike; Q, N-I07/N-I08 neighbor | Mixed mocked outcome sequence, budget/cancel/session/result neighbors U | [CommitTransaction_AbortThenNonAbort_RotatesThenReplaysToken][a-dtx-mixed]; A: captured rotation then replay | J-U/J-P | U; declared SC/mock commit | ORACLE/SEQ; G-06. Independent transition table and request journal; rotate-on-every-reply/replay-on-abort controls must fail |
| B-012; N-02/F-19 | Operation serialization is distinct from coordinator atomicity/durability; Q, not an approved live DTX guarantee | Intercepted commit plus real emulator metadata; separate live Gateway/Session classes ignored | [MixedOperations_AllOperationsAreSerialized][a-dtx-emulator]; A: captured count/types. [Conditional][a-dtx-ignored] and [read live class][a-dtx-read-ignored] are Ignore | Emulator project; exact ordinary bucket linkage unverified; ignored live classes not selected | U; mock commit, metadata does not identify live durable outcome | OBS/SEL/FIX; G-04/G-06/G-08. Reconcile capability/authority before live proof; do not remove Ignore or treat intercepted success as atomicity |

### Identity, CRUD, cancellation and lifecycle

| Behavior ID / lanes | Intended observable rule + authority | Cells / interacting features | Existing method, assertion and fixture | Established job/filter | Execution / artifact receipt | Specific gap / remediation / proof needed |
|---|---|---|---|---|---|---|
| B-013; F-04/F-08/F-09 | Clearing one container RID leaves another RID's session state intact; Q | In-memory session cache; full name/RID recreate/account workflow U | [TestClearTokenKeepsUnmatchedCollection][w-session-clear]; A: cleared RID null, other RID LSN 100 retained | J-U/J-P | U; declared SC | SEQ/ORACLE; G-06 SC-08/DB-12. Independent delete/recreate/identity journal; wrong-RID clearing/reuse must fail; preserve another container |
| B-014; F-09 | Higher scoped session progress is not overwritten by lower progress; Q | Narrow in-memory LSN examples; version/vector/region/topology and service visibility U | [TestSetSessionTokenDoesntOverwriteHigherLSN and ...OverwritesLowerLSN][w-session-lsn]; A: LSN 105 retained/produced | J-U/J-P | U; declared SC | ORACLE/SEQ; G-06/G-10. Independently reconcile versioned token algebra; lower-progress overwrite is control, not approval of a generic component-wise MAX rule |
| B-015; F-07/F-13 | Supported multi-hash/prefix inputs produce authoritative exact EPK encoding; Q | Null/numeric/string/prefix vectors; packaged Direct type; feed ranges/splits U | [TestPartitionKeyBinaryEncodingMultiHash/TestPrefixPartitionKeyExtraction][w-key-vectors] + [hardcoded-vector helpers][w-key-helpers]; A: exact expected vectors | J-U/J-P | U; declared SC/packaged Direct, not staged source | ART/ORACLE/SEQ; G-03/G-06. Independent version/key-boundary authority and range neighbors; altered hash/prefix vector must fail |
| B-016; F-08/F-11 | Late stale regional event does not overwrite confirmed hub route; Q | Mock endpoints/direct state call; `!INTERNAL`; actual 403/substatus-producing request and outage U | [TryMoveNextLocation_LateArrivingStale403_DoesNotOverwriteConfirmedHub][w-region]; A: cached route remains confirmed hub | J-U/J-P subject to compile/filter | U; declared SC | OBS/SEQ; G-06 SC-09/SC-11. Request/refresh completion journal plus unrelated partition; stale overwrite is control, not complete regional failback proof |
| B-017; F-03/F-14 | Authorized account read returns expected account in each declared transport; Q | AAD strict live; explicitly Gateway and Direct rows; rotation/role/config U | [AadReadAccountAsync][w-aad-crud]; A: account nonnull | J-A | U; declared SC/live capability U | FIX/COHORT/ORACLE; G-07/G-08. Both eligible transport leaves and account identity receipt; omitted mode or wrong fixture cannot count as proof |
| B-018; F-03/F-05 | Authorized item CRUD preserves statuses/content/identity in each declared transport; Q | AAD strict live, Gateway and Direct rows; typed/stream/read-many/rotation breadth U | [AadItemCrudAsync][w-aad-crud]; A: CRUD status/content checks | J-A | U; declared SC/live capability U | FIX/SEQ/ORACLE; G-06/G-07/G-08. Exact per-row operation/result journal; wrong ID/PK/response or missing mode fails; no blanket overload closure |
| B-019; F-19 | Failed same-PK transactional batch does not persist earlier create and reports dependent errors; Q | AAD/default Direct, single PK; bulk/DTX and lost-response scope distinct | [AadTransactionalBatchRollsBackAsync][w-batch]; A: envelope 404, first op 424, missing op 404, follow-up created-item read 404 | J-A | U; declared SC/live capability U | Strong A, broader SEQ/FIX/ORACLE; G-06 SC-17. Preserve live rollback observer; erroneous persisted create/dependent mapping is control. Limits/corruption/possible-commit cells unverified |
| B-020; F-04/F-28 | Create-if-not-exists distinguishes new/existing and recovers the inspected create conflict; Q | Real emulator plus handler-injected 409, not independent concurrent writers | [CreateIfNotExists][w-database]; A: new 201/existing 200, charge/diagnostics, injected create 409 recovered to 200 | J-E2 | U; declared SC/emulator capability U | OBS/SEQ; G-06 SC-05/SC-08. Real race/stale-handle/identity journal needed; duplicate or wrong-resource result must fail |
| B-021; F-10/F-14/F-29 | Caller cancellation surfaces the expected cancellation through failed-connect path; Q | Mocked Gateway connect failure/cancel; transmission, no-next-dispatch, owned cleanup and terminal race U | [GatewayProcessMessageAsyncCancels][w-cancel]; A: OperationCanceledException | J-U/J-P | U; declared SC/mock HTTP | OBS/SEQ; G-06 SC-04. Phase-specific send/cleanup/cancellation journal; extra attempt after accepted cancellation is control under independently defined race rules; no non-commit inference |
| B-022; F-10/F-14/F-29 | Configured deadline bounds the inspected failed-connect path; Q | Gateway, wall-clock sleep; actual controlled timers/scheduler and outer budgets U | [GatewayProcessMessageAsyncCancelsOnDeadline][w-cancel]; A: OperationCanceledException after wall-clock deadline | J-U/J-P | U; declared SC/mock HTTP | OBS/SEQ; G-06 SC-01/SC-04. Observe nested configured budgets and cleanup; out-of-budget dispatch is control. No new timing tolerance or virtual-time claim |
| B-023; F-01/F-23/F-29 | Processor stop invokes lease-manager shutdown at its scoped lifecycle boundary; Q | Mock lease manager, no callback/checkpoint/rebalance/restart proof | [StopAsync_CallsShutdownAsync][w-processor]; A: ShutdownAsync exactly once | J-U/J-P | U; declared SC/mock leases | OBS/SEQ; G-06 SC-21/SC-23. Independent lease/callback/ownership journal; duplicated/missing shutdown or post-terminal owned callback is control once lifecycle authority is established |
| B-024; F-01/F-12 | Client dispose clears/terminates owned partition manager without invalidating unrelated owners; Q | Field-clearing fixture; failed/lazy construction, shared/external ownership and active work U | [Dispose_DisposesPartitionKeyRangeLocationWhenIDisposable][w-client-dispose]; A: client field null; subsequent background initialization call has no independent no-work assertion | J-U/J-P | U; declared SC/mock lifecycle | Confirmed OBS/SEQ; G-06 SC-23. Owned work/loop cancellation observer; continued owned work or disposal of shared/external resources is control under approved ownership rules |

### ThinClient, query, companions and compatibility

| Behavior ID / lanes | Intended observable rule + authority | Cells / interacting features | Existing method, assertion and fixture | Established job/filter | Execution / artifact receipt | Specific gap / remediation / proof needed |
|---|---|---|---|---|---|---|
| B-025; F-08/F-15 | Withdrawal of thin locations changes next dispatch to Gateway without another thin dispatch; Q | Mock transport/test-updated advertisement, not actual account-refresh/HTTP2 network path | [ProcessMessageAsync_AfterServiceWithdrawsThinLocations_FallsBackToGateway][w-thin]; A: first thin/no Gateway; later Gateway/no extra thin | J-U/J-P; J-T is separate advisory live/emulator lane | U; declared SC/mock transport | OBS/FIX/ORACLE; G-05/G-06/G-08. Advertisement/refresh journal and ordinary Gateway preservation; stale thin selection is control |
| B-026; F-14/F-15/F-28 | Owned retriable HTTP contents are disposed and later diagnostics/metrics remain safe; Q | In-memory two 408 replies then 200; disposed-response diagnostics and zero-body fallback, no real HTTP2 stream-reset proof | [ThinClientRetriableResponsesAreDisposedBeforeRetryAsync][w-http]; A: both contents disposed after call, success 200, diagnostics/metric safety | J-U/J-P | U; declared SC/mock handler | A for post-call scope; broader OBS/FIX; G-05. Preserve these assertions, successful caller response ownership and other modes/factories; unsafe disposed-body read is control |
| B-027; F-14/F-15/F-29 | Each retry content is disposed before the next send; Q, separately observable timing rule | Same fixture; actual next-dispatch disposal timing and physical stream/task cleanup U | W-11 [same method][w-http] has post-loop assertions only; no next-send timing witness established | J-U/J-P as extension point, not existing timing proof | U; declared SC/mock handler | Confirmed OBS; G-05. Observe disposal at next send; delayed-until-after-send control must fail while retaining B-026; no physical HTTP2 claim |
| B-028; F-16/F-17 | Hybrid results retain approved ID/score alignment and allowed order/ties across scopes; Q | Direct/MultiPartition, local/global full-text scope, HoldState drain; Gateway/partial-empty/tied components and capability cells U | [SanityTests][w-hybrid] + [RunTests][w-hybrid-run]; A: expected ID lists/accepted ties and enabled score predicates; Gateway commented out | J-E1 Query | U; declared SC/emulator capability U | Confirmed scope/OBS plus ORACLE; G-05/G-06 SC-19. Independent ranking/component journal; mismatched IDs/scores or disallowed ordering fails, permitted ties do not |
| B-029; F-16/F-18 | Fresh iterators resume tokens and return the fixed created ID set within recorded budget; Q | AAD/default Direct, fixed six-item dataset/multiple pages; no concurrent-mutation snapshot or split guarantee | [AadCrossPartitionQueryWithContinuationAsync][w-query-resume]; A: fresh iterator per token, multiple pages, budget drain, equivalent created/read IDs | J-A | U; declared SC/live capability U | SEQ/ORACLE/FIX; G-06 SC-06/SC-18. Independent token/options/range journal; fixed-data lost/duplicate IDs or stale token fail; concurrent allowed outcomes need separate authority |
| B-030; N-01/F-16/F-06 | Supported filtered/ordered LINQ expression returns its independent expected results; Q | AAD/default Direct, narrow expression subset; serializer/null/numeric/scalar/async breadth U | [AadLinqQueryAsync][w-linq]; A: ordered task numbers 2,3 | J-A | U; declared SC/live capability U | SEQ/ORACLE; G-06 SC-20/G-10. Independent translation/result oracle; wrong filtering/order/naming fails, not full LINQ closure |
| B-031; F-30/F-28 | Applicable GA/preview framework API baseline detects deliberate contract differences; Q for semantic scope | Windows/UpdateContract; core net6 test TFM despite multi-version comments; msdata filters differ | [ContractEnforcementTests][w-api] + [ValidateContract][w-api-helper]; A: framework baseline comparison, deliberately different JSON fails/identical passes | J-U/J-P subject to actual filter/compile | U; declared SC/reflection | A negative control, ART/TRACE/ORACLE breadth; G-03/G-10. Bind actual assembly/framework/symbol/baseline; obsolete/wrong baseline fails. Not historical semantic compatibility proof |
| B-032; N-06 | Invalid FI NaN/bounds values produce authoritative rejection; Q | Account-free builder validation, core/Direct version coupling; concurrency/hit-limit/fault origin breadth U | [FaultInjectionBuilderValidationTests hard assertions][w-fi-hard]; A: NaN/bounds validation | J-F named PR subset | U; declared core/FI source plus packaged Direct | ORACLE/SEQ; G-04/G-06. Preserve hard cases and coupling; accepted invalid value is control under reviewed validation rules |
| B-033; N-06 | Required SendDelay/non-delay rejection must be discriminated, not accepted as Inconclusive; Q | Conditional branches in selected FI validation class, not every FI test | [FaultInjectionBuilderValidationTests conditional branches][w-fi-skip]; A: missing expected rejection can yield Inconclusive | J-F | U; declared core/FI source plus packaged Direct | Confirmed OBS/COHORT; G-04/G-07. After rule reconciliation, missing rejection must produce meaningful failure, not pass-shaped evidence; retain valid fault types and original outcomes |
| B-034; N-04/F-06/F-21 | Encrypted-path Increment rejects with path-specific error; nonencrypted Increment passes same operation through; Q, existing expectation strengthened without independent contract approval | I-01 real SDK/service counterpart; package versus current-source GA/preview distinct | Historical [W-17][w-encryption]; exact current methods/rows/assertions in [I-01](specs/i-01-encryption-patch-paired-tests.md) | Both native unit tasks executed/published; existing J-CE GA/preview service tasks passed on attempt 1 | Historical U retained; local five unit cases passed in four bindings; native units exact 5/5 within 58/58 each; native service exact 8/8 each plus existing patch witness passed | G-02 partial/ART/ORACLE. Protection executed, not independently approved closure; loaded payload/complete fixture, Custom, wider source/counterpart and independent-review/challenge backlog retained |
| B-035; N-05/F-29 | Internal distributed-cache timeout/cancellation falls back to source without consuming caller cancellation; Q remains unqualified | I-02 real-service counterpart: typed Gateway/Direct, both Custom TFMs; TTL/stream/privacy breadth U | Historical [W-18][w-custom] preserved; exact two strengthened unit seeds and four service rows in [I-02](specs/i-02-custom-cache-paired-tests.md) | Explicit ordinary/preview Custom unit tasks and existing J-CE Custom service task executed on repaired c9e118/build 64334 | Historical U retained; new candidate-bound exact units passed ordinary 1331452/net6, 1331451/net8 and preview 1331443/net6, 1331444/net8. Four service rows passed in 1331436/net6 and 1331437/net8. Loaded-artifact/fixture qualification remains U | G-02 bounded protection, not closure/ORACLE/SEQ. Preserve warm L1 and exact caller/source forwarding versus B-036; independent oracle and broader counterparts remain required |
| B-036; N-05/F-29 | Pre-cancelled caller throws before source or L2 cache I/O; Q remains unqualified | Same I-02 fixture, narrowly pre-dispatch, not arbitrary cancellation/no-commit | Historical [W-18][w-custom]; existing unit strengthened with same-cache recovery; two public-SDK service rows with real absent-item and recovery observers in [I-02](specs/i-02-custom-cache-paired-tests.md) | Same executed unit tasks and existing J-CE, c9e118/build 64334 | Exact unit passed in all four native unit runs above; both service rows passed in each native TFM above. Separate original TRX/attempt/configuration receipts retained; not current-core service or complete fixture proof | G-02 bounded protection, not closure/ORACLE/FIX/SEQ. Caller-triggered cache/source read or swallowed cancellation fails; preserve B-035 internal fault fallback and broader phase/fixture qualification |
| B-037; F-13/F-16/F-30 | Applicable native query provider can be created for the actual artifact; Q | Windows native smoke, not ARM/AOT/installed-nupkg runtime matrix | [DirectContractTests native span][w-native]; A: native provider creation | J-U/J-P, Windows | U; declared SC/packaged Direct/native inputs | ART/FIX; G-03. Exact nupkg/loaded native/runtime consumer receipt; wrong/missing native payload is control, no native portability guarantee |
| B-038; F-13/F-30/N-06 | Direct assembly/dependency/nuspec identities agree with applicable package contract; Q | Packaged Direct and core/FI coupling; staged-source/consumer parity U | [DirectContractTests dependency span][w-dependencies]; A: Direct assembly/no exported public types, dependency-version/nuspec checks | J-U/J-P, Windows; J-F coupling separate | U; declared SC/packaged Direct | ART/EXT/TRACE; G-03/G-11. Reject mismatched resolved/loaded/nuspec identities; preserve FI version gate and companion consumers |
| B-039; F-05/F-21 | Matching ETag write advances ETag; stale replacement returns 412; Q | AAD/default Direct; concurrent writers/non-idempotent lost response U | [AadItemConditionalOperationsAsync][w-etag]; A: successful matching replacement changes ETag; stale replacement 412 | J-A | U; declared SC/live capability U | SEQ/ORACLE; G-06 SC-16/DB-01. Independent effect/ETag journal; stale write accepted or lost-response duplicate masked as success fails under approved rule |
| B-040; F-05/F-18 | Conditional item read returns 304 under its matching condition; Q | AAD/default Direct; bodyless read, not change-feed continuation | [AadItemConditionalOperationsAsync][w-etag]; A: conditional read 304 | J-A | U; declared SC/live capability U | SEQ/ORACLE; G-06. Preserve distinct conditional versus change-feed 304; wrong condition/status/body ownership is control; overload breadth U |
| B-041; F-25/F-31/F-17 | Vector-policy quantizer/shard fields round-trip according to representation contract; Q | Unit serialization only; service validation/index evolution/full policy catalog U | [ValidateVectorIndexQuantizerTypeSerialization][w-policies]; A: vector-policy/quantizer/shard-key round-trip | J-U/J-P subject to compile/filter | U; declared SC | OBS/ORACLE/SEQ; G-06/G-10. Independent serialized representation and default/evolution neighbors; lost/renamed fields fail. Not live index enforcement or GSI support |

## 4. Eight proposed atomic candidates retained separately

The candidate IDs and proposed semantic status come from the versioned bootstrap matrix and A-01 section 6. They are not promoted by B-row mapping or by tests existing in source.

| Candidate | Atomic question / mapped behavior | Actual A-01 assertion witness | Remaining authority and proof |
|---|---|---|---|
| N-I01 | Serializer mutual exclusion; B-001 | A-serializer, J-U/J-P | Independent option table; null/reset/rejected-state neighbors; actual candidate execution U |
| N-I02 | Builder read-strategy round-trip; B-002 | A-builder, J-U/J-P | Supported enum/builder contract; not account/service consistency; execution U |
| N-I03 | Request-level strategy precedence; B-003 | A-precedence/A-precedence-emulator; J-U/J-P/J-E2 | Wire/header/account/topology/mode authority and complete eligible cells U |
| N-I04 | Bulk request bucket rejection before work; B-004 | A-bulk, J-U/J-P | Actual public-entry materialization/dispatch observer and option authority U |
| N-I05 | Barrier origin `408/21013` health classification; B-005/B-006 | A-barrier/A-barrier-producer/helper; J-U/J-P | A-01 refines "enters retry branch": **this policy returns ShouldRetry=false**, not retry. Full configuration/producer/outer-policy/service proof U |
| N-I06 | 304 same-scope continuation/resume; B-007/B-008 | Unit token inequality, weaker emulator aggregate, stronger live AAD exact resumed item | Mode/RID/range/version/retention/topology authority; actual execution U |
| N-I07 | Nonterminal retriable commit retains token; B-009/B-011 | A-dtx-replay/mixed, J-U/J-P | Independent coordinator protocol/outcome/commit journal; real durability U |
| N-I08 | Terminal-abort retry rotates token; B-010/B-011 | A-dtx-rotate/aborts/mixed, J-U/J-P | Factory-generated 452/isRetriable is not authoritative durable abort; service/budget/cancel/result/session cells U |

## 5. All 37 navigation lanes retained

Each lane's **unexpanded remainder is unverified**. `G-06.F-xx` / `G-06.N-xx` below denotes a future bounded expansion task, not current execution authorization or an approved guarantee. Cross-cutting receipt/spec dependencies also apply.

| Lane / retained feature scope | Selected B-row / A-01 evidence and established CI | Unexpanded remainder and follow-up |
|---|---|---|
| F-01 construction/initialization/ownership/shutdown | B-023/B-024; W-09/client-dispose; J-U/J-P | Failed/lazy construction, shared/external owners, active/post-dispose/concurrent calls; G-06.F-01, G-10 |
| F-02 defaults/validation/precedence | B-001..B-003; N-I01..N-I03; J-U/J-P/J-E2 | All null/unset/zero/clone/environment/capability states and unrelated-option preservation; G-06.F-02 |
| F-03 auth/refresh/privacy | B-017/B-018; W-05; J-A | Keys/resource tokens, rotation/revocation/scopes/concurrency/redaction/midworkflow changes; G-06.F-03, G-08 |
| F-04 database/container/resource lifecycle/recreate | B-013/B-020; W-01/W-07; J-U/J-P/J-E2 | Actual create races, stale names/handles/RIDs, delete/recreate and account separation; G-06.F-04 |
| F-05 typed/stream CRUD/read-many/conditionals | B-018/B-039/B-040; W-05/W-20; J-A | Every overload, partial/bodyless payload, key extraction/result correlation/read-many; G-06.F-05 |
| F-06 serialization/representation/streams | B-001/B-034/B-041; N-I01/W-17/W-21 | Default/custom/STJ/binary across query/read-many/patch/child resources, JSON/short-read/ownership; G-06.F-06, G-02 |
| F-07 logical/hierarchical keys/EPK/feed ranges | B-015; W-03; J-U/J-P | Extraction/hash versions/prefix/API/boundary/range/topology isolation; G-06.F-07 |
| F-08 metadata/name/RID/range/address/query-plan caches | B-013/B-016/B-025; W-01/W-04/W-10 | Complete cache/owner inventory, refresh/coalescing/reordering/recreate/shared lifetime; G-06.F-08 |
| F-09 consistency/session | B-002/B-003/B-013/B-014; J-U/J-P/J-E2 | Account/client/request eligibility, legacy coexistence, versioned token algebra, split/merge/live visibility; G-06.F-09, D-05 |
| F-10 HTTP/substatus/origin/retry/budgets/possible commit | B-005/B-009..B-011/B-021/B-022/B-026/B-039/B-040 | Complete decision cells/defaults/transforms/nested budgets/unknown write outcome; G-06.F-10 |
| F-11 region eligibility/outage/recovery | B-016/W-04; J-LR/J-LW declarations | No eligible endpoint, dynamic single/multi-writer topology, partition/region health/failback; G-06.F-11, G-08 |
| F-12 hedging/circuit/winners/losers/probes | B-016/B-024 nearby state/lifetime evidence only | No complete selected hedge/circuit sequence; winner/session/loser cleanup/cooldown/isolation; G-06.F-12 |
| F-13 Direct framing/TLS/pooling/timeouts/partial I/O | B-006/B-015/B-037/B-038; packaged Direct | Physical protocol/cleanup and staged-source/loaded-artifact parity; G-06.F-13, G-03; migration separate |
| F-14 Gateway/HTTP/proxy/factory/pools/streams | B-017/B-018/B-021/B-022/B-026/B-027 | Physical protocol/runtime/acquisition/response/ownership/local-origin matrix; G-06.F-14, G-05 |
| F-15 ThinClient/probes/capability/fallback | B-025..B-027; W-10/W-11; J-T advisory | Real advertisement refresh/HTTP2/probes/unsupported account/diagnostics attribution; G-06.F-15, G-04/G-08 |
| F-16 SQL/plans/operators/order/ties/limits | B-028..B-030/B-037; J-E1/J-A/J-U/J-P | Native/Gateway planning, parameters/partition scope/aggregates/distinct/grouping/concurrency/independent oracles; G-06.F-16 |
| F-17 vector/full-text/hybrid/ranking/component alignment | B-028/B-041; W-12/W-21 | Gateway/capability/partial-empty components/score authority and historical ranking closure; G-06.F-17, G-05/G-10 |
| F-18 iterator/feed/stream paging/empty/resume/repeat | B-007/B-008/B-029/B-040 | Terminal/throttled/empty/reused/cancelled pages, token/options/version/concurrent allowed outcomes; G-06.F-18; no snapshot promise |
| F-19 single-PK transactional batch | B-019/W-06; J-A | Limits/conditionals/bodyless/corrupt/lost-response/possible-commit cases; G-06.F-19; not bulk/DTX |
| F-20 bulk scheduling/backpressure/per-item retry | B-004/N-I04; J-U/J-P | Mixed/absent per-item results, correlation/skew/RetryAfter/topology/resource cleanup; G-06.F-20, G-05 |
| F-21 patch/path/filter/ETag/replay | B-034/B-039; W-17/W-20 | Non-idempotent lost response, expression/serializer/batch/DTX/conditions/limits; G-06.F-21 |
| F-22 pull change feed/modes/304/ranges/start/retention | B-007/B-008/N-I06; J-U/J-P/J-E1/J-A | Mode/time/retention/RID/version/split/merge/cancel/capability scope; G-06.F-22, G-08 |
| F-23 processor/leases/callback/checkpoint/restart/estimator | B-023/W-09; ChangeFeed selection | Callback failure/replay, ownership transfer/rebalance/checkpoint/estimator/restart; G-06.F-23 |
| F-24 throughput/autoscale/shared capacity/buckets/priority/skew | B-004 plus fixture/header declarations | Real RU/deadline/backpressure, hot partitions, capacity/policy transitions; G-06.F-24, G-08; emulator disables rate limits |
| F-25 indexing/spatial/composite/vector/full-text/TTL/unique/conflict/computed policies | B-041/W-21; source/API/fixture flags | Complete public/wire validation/lifecycle/evolution/enforcement; G-06.F-25/G-10; TTL not exact client-clock liveness |
| F-26 scripts/triggers/UDFs/users/permissions/conflicts | Retained resource/API path/compile navigation; no additional assertion selected by A-01 | Per-resource operations/body/auth/serializer/paging/error/atomicity boundaries unverified, not absent; G-06.F-26/G-10 |
| F-27 dedicated Gateway/integrated cache/staleness/bypass | Retained options/API and bulk-validation source; no selected freshness assertion | Account/operation/transport eligibility, freshness/consistency/unsupported cells; G-06.F-27/G-08 |
| F-28 diagnostics/charge/tracing/metrics/errors/hooks/privacy | B-020/B-026/B-031; telemetry jobs declared | Per-attempt fields/cardinality/redaction/failing or saturated observers/overhead/cleanup; G-06.F-28/G-09 |
| F-29 async/context/cancellation/concurrency/repeat/request isolation | B-021..B-023/B-035/B-036; DTX source nearby | Every overload token, races/shared mutable options/sync context/clocks/prefetch/cleanup; G-06.F-29 |
| F-30 API/binary/source/package/native/wire/token/runtime/dependency compatibility | B-031/B-037/B-038; J-B/J-PACK parity/build inputs | Installed consumers, old/new tokens/wire/runtime/dependency surfaces and semantic history; G-03/G-10, G-06.F-30 |
| F-31 new/preview metadata/capability/service lifecycle | B-041/W-21; bootstrap metadata/source navigation | Every new capability/default/lifecycle needs reconciliation; GSI support/absence unresolved; G-06.F-31/G-10 |
| N-01 .NET LINQ translation/execution | B-030/W-14 plus bootstrap source | Full expressions/naming/converters/null/numerics/scalar/async/differential oracle; G-06.N-01 |
| N-02 preview distributed read/write transactions | B-009..B-012/N-I07/N-I08; mocked/intercepted/ignored-live distinction | Protocol/scope/capability/read-write/result/session/token/budget/durability; G-06.N-02/G-04/G-08 |
| N-03 preview embeddings/semantic reranking/inference | Bootstrap E-26 source/deferred-runtime navigation; no new actual assertion selected | Endpoints/auth/order/errors/cancel/generator ownership/runtime/cost/companion overrides; G-06.N-03/G-10 |
| N-04 Encryption | B-034/W-17; J-CE and source/package-symbol parity builds | Unit selection, keys/wrap/rewrap/cache/query/representation/consumer/core preservation; G-02/G-03, G-06.N-04 |
| N-05 Encryption.Custom | B-035/B-036/W-18; multi-TFM/parity/J-CE | Unit selection, pooled plaintext/stream/short-read/provider/cache/TTL/isolation/cleanup/runtime dependencies; G-02/G-03, G-06.N-05 |
| N-06 FaultInjection/control fidelity | B-032/B-033/W-16; J-F coupling/unit/pack and optional live selection | Fault stage/origin/status/probability/NaN/concurrent hit limits/activation/cleanup/Direct fidelity; G-04/G-08, G-06.N-06 |

## 6. Scenario and database-family inputs

All sequence outcomes below are **questions for independently approved .NET cells**, not promised guarantees. Nearby B-row assertions do not establish the complete seed. Existing-suite extension points use the CI aliases above; actual fixture/account/candidate/cohort and execution remain U. Every seed is a separate `G-06.SC-xx` follow-up with G-01/G-03/G-07/G-08 and relevant D-05/D-10 dependencies.

### All 28 critical scenario seeds

| Seed / retained sequence | Actual nearby witness and scope | Next precise sequence observer / negative control |
|---|---|---|
| SC-01 throttle near deadline | B-005 and B-022 separately; not combined throttle/budget proof | Faithful throttle + nested configured budget/attempt/phase/cleanup journal; out-of-budget send or wrong health origin is control; rate-limit-disabled emulator cannot qualify real RU throttle |
| SC-02 nested throttle/topology/availability | B-005/B-016 isolated | Combine retry/refresh/route/health and remaining-budget transitions; unexpected budget reset or unrelated partition poisoning fails under approved classification |
| SC-03 committed write/lost response | B-009..B-011 mocked token mechanics only | Independent write/effect/commit journal plus lost-response phase; repeated non-idempotent effect hidden as success fails; timeout alone cannot prove non-commit |
| SC-04 cancellation in backoff/acquisition/partial response/completion | B-021/B-022/B-035/B-036 limited scopes | Separate phase/race cases, terminal winner and owned-cleanup/no-extra-dispatch journal; swallowed caller cancel or extra accepted-after-cancel work is control; no blanket race outcome guessed |
| SC-05 cold/unavailable metadata | B-020 and B-012 warm real-metadata setup | Cold/failing/coalesced refresh and identity/topology sequence; stale target or duplicate uncontrolled refresh violates independently specified case, not mere warm success |
| SC-06 query resume across split | B-029 fixed-data resume only | Fixed writer/range/options/token/split journal; lost/duplicate fixed-data IDs or wrong-range resume is control; concurrent mutation has separate allowed outcomes, not snapshot semantics |
| SC-07 change-feed timestamp/token across merge | B-007/B-008 same-range partial | Approved mode/time/RID/version/retention/merge journal; wrong resume scope or lost approved progress discriminated without adopting concept hypotheses/global dedup/exactly-once promises |
| SC-08 delete/recreate/session identity | B-013 RID-local clear | Real stale-name/handle/cache/token recreation sequence with separate container identity; wrong-RID token/target or clearing another container is control |
| SC-09 midwrite regional change | B-016 state method only | Writer/topology/transmission/possible-commit/result/session journal; excluded/wrong writer, stale cache overwrite or masked duplicate effect is control |
| SC-10 no eligible region | Endpoint/exclusion navigation, no complete actual witness | Exhaust permitted endpoint set within established budgets with diagnostic/cleanup observer; excluded route or unbounded recovery loop is control; exact error authority pending |
| SC-11 out-of-order refresh | B-016 late stale event | Ordered completion journal for name/RID/range/address/query-plan caches and shared clients; stale completion overwriting newer confirmed state is control |
| SC-12 local protocol failure | B-021 mocked connect failure/B-026 synthetic HTTP statuses | Faithful local framing/stream/reset/TLS/partial-I/O origin plus reviewed classifier/cleanup; wrong origin/transport/health attribution is control, not HTTP-only status substitution |
| SC-13 hedge winner/session/loser cleanup | Availability navigation, no complete selected hedge assertion | Winner/result/session and loser cancellation/disposal journal, unaffected request neighbor; stale session merge or surviving owned losing arm is control under approved rules |
| SC-14 partition circuit recovery | B-016/B-024 state/lifetime nearby only | Open/probe/cooldown/recovery timeline with unrelated partition traffic; wrong isolation, missing recovery or retained owned work is control |
| SC-15 bulk mixed/absent responses | B-004 direct option validation only | Per-item submitted/result/retry/absent-entry/cleanup journal; whole-batch success credit despite missing/wrong item fails; no cross-partition atomicity |
| SC-16 non-idempotent patch lost response/conditions | B-034 rejection/B-039 ETag only | Independent effect/ETag/lost-response journal with authoritative replay rule; duplicated Increment or stale condition accepted/masked as success is control |
| SC-17 batch atomicity/dependent errors | B-019 strong single-PK live rollback slice | Preserve follow-up no-persisted-create assertion; add limit/bodyless/corrupt/lost-response/possible-commit cases; persisted earlier create or wrong dependent error is control |
| SC-18 empty/throttled page/resume/repeat | B-007/B-008/B-029 partial | Fresh/reused iterator/token/options/empty/throttle/cancel journal within configured budgets; fixed-data loss/duplicate or dropped continuation is control; no concurrent snapshot assumption |
| SC-19 hybrid component alignment | B-028 Direct/MultiPartition accepted lists/ties | Independent ID/score/ranking/component/paging journal through partial/empty/tied components and applicable modes; ID-score mismatch or order outside approved ties fails |
| SC-20 serializer across query/read-many/patch | B-001/B-030/B-034/B-041 slices | Independent representation/result/ownership observer across default/custom/STJ/binary/companions; changed field/value/naming/stream lifecycle is control, not production helper as oracle |
| SC-21 callback/checkpoint/lease/restart | B-023 shutdown call only | Callback failure/lease ownership/checkpoint/restart journal with allowed replay; wrong checkpoint advancement or lost ownership/progress is control, not universal exactly-once processing |
| SC-22 credential rotation/failover | B-017/B-018 static auth fixture only | Midworkflow refresh/revoke/failover/scope/auth-error and cleanup journal without credential values; wrong-scope success or failed refresh mislabeled success is control |
| SC-23 shutdown active work | B-023/B-024 call/field evidence | Concurrent bulk/query/feed/transport/processor ownership journal; owned work after agreed terminal shutdown or disposing external/shared resources fails; post-dispose contract authority pending |
| SC-24 failing/saturated/repeated telemetry | B-026 disposed-response consumer safety | Throwing/saturated/repeated observer versus request/result/retry/resource journal; changed result/retry or unbounded observer resource use is control using established budgets |
| SC-25 hot logical partition | Throughput/live fixture declarations | Skew/capacity/per-partition backpressure/RU attribution journal with idle peers; wrong isolation/correlation fails; no new throughput target and no disabled-rate-limit proof |
| SC-26 malformed/partial/oversized wire/tokens | Existing fuzz/parser paths, not executed proof | Bounded production-artifact parse/error/ownership/resource witnesses and minimized input records; silent success, uncontrolled resource growth or wrong error shape is control; tooling/campaign unselected |
| SC-27 runtime/dependency/consumer transitions | B-031/B-037/B-038/parity builds | Exact installed nupkg/loaded native/runtime/old-new dependency/token/wire consumer record; wrong package/symbol/runtime or stale baseline cannot qualify compatibility |
| SC-28 new status/capability mismatch | B-005/B-009..B-011/B-025 narrow known cells | Unknown status/body/origin and capability advertisement sequence plus reviewed default rule; fabricated success, unsafe token/route choice or ignored withdrawal fails; no imported Java default |

### All 12 general database families

DB-IDs are local handles added for this draft; names are retained from A-01 section 9.3. Each `G-06.DB-xx` expansion needs independent authority and actual proof; no family is closed by one witness.

| Family | Actual nearby witness / seed links | Remaining assertion/negative-control work |
|---|---|---|
| DB-01 concurrent writers/lost updates | B-039 stale ETag; SC-03/SC-16 | Independent simultaneous writer/effect/ETag journal; lost update/duplicated effect discriminated; no executed concurrency proof |
| DB-02 isolation/transactions | B-019 single-PK rollback; B-009..B-012 mock/ignored DTX; SC-17 | Keep batch/bulk/DTX boundaries separate; wrong persisted batch effect is control; DTX live authority/capability U |
| DB-03 identity/uniqueness | B-013 RID scope/B-018 IDs; SC-08 | PK/account/container identity/recreate/uniqueness journal; wrong target/key or reused stale identity discriminated |
| DB-04 representation | B-001/B-015/B-034/B-041; SC-20/SC-26 | Independent value/JSON/encoding/stream/ownership oracle; lost/null/missing/numeric/Unicode/partial-input representation is control |
| DB-05 query determinism | B-028 accepted ties/B-030 order; SC-19 | Approved independent operator/ranking/tie oracle; invalid order/score alignment fails without rejecting permitted ties |
| DB-06 concurrent mutation/enumeration | B-029 fixed dataset; SC-06/SC-18 | Writer/enumerator history with independently allowed outcomes; do not demand snapshot isolation or reuse fixed-data oracle blindly |
| DB-07 expiration/retention | Change-feed/TTL navigation; SC-07 | Mode/retention/service-progress journal; invalid resume/policy outcome discriminated; no exact local-clock expiration promise |
| DB-08 index/schema evolution | B-041 metadata round-trip | Independent policy/default/backfill/evolution/compatibility observer; missing representation/enforcement step distinguished; live authority U |
| DB-09 possible-commit failure | B-009..B-011 token mocks; SC-03/SC-09/SC-16 | Independent durable outcome/effect journal; false non-commit claim or masked duplicate fails; unknown remains unknown |
| DB-10 durability/consistency | B-003 headers/B-013/B-014 session cache | Approved visibility/progress/session scope plus real fixture receipt; header/mock success cannot qualify live durability |
| DB-11 authorization changes midworkflow | B-017/B-018 static auth; SC-22 | Refresh/revoke/scope/permission transition journal; wrong privilege/result/control outcome discriminated without collecting secrets |
| DB-12 multi-client/account/container isolation | B-013 plus unique AAD partition namespaces; SC-08/SC-11/SC-23 | Independent cache/key/provider/client/transport ownership and identity journal; cross-container contamination/shared-owner disposal fails |

## 7. Cross-cutting gap routing and closure boundaries

All original A-01 G-items retain explicit links; the B-row/seed links never replace the audit's evidence states or original policy/source provenance.

| A-01 item | Matrix surface / proposed closure proof |
|---|---|
| [G-01](remediation-plan.md#g-01) | Every job/cell: nominal versus evaluated host/configuration/runtime/flags; candidate-bound mismatched-label control |
| [G-02](remediation-plan.md#g-02) | B-034..B-036/N-04/N-05: exact eligible companion unit project/TFM/leaves and reference-mode receipts |
| [G-03](remediation-plan.md#g-03) | Every artifact column, F-13/F-30/companions/J-B/J-PACK: source/staged/package/loaded-consumer identity |
| [G-04](remediation-plan.md#g-04) | J-T/J-F/Flaky/excluded/ignored DTX: explicit disposition, no skipped/advisory pass credit |
| [G-05](remediation-plan.md#g-05) | B-004..B-008/B-027/B-028: entry-path, exact 304/resume, real configuration propagation, next-send disposal and independent hybrid observers |
| [G-06](remediation-plan.md#g-06) | Every unexpanded lane, SC and DB follow-up: independently specified sequence, allowed outcomes, negative control and preserved neighbors |
| [G-07](remediation-plan.md#g-07) | Every execution column: expected/discovered/selected/executed/disposition identities, original and final attempt receipts |
| [G-08](remediation-plan.md#g-08) | Emulator/live/AAD/Thin/hybrid/DTX cells: actual safe topology/consistency/capability/namespace/cleanup identity |
| [G-09](remediation-plan.md#g-09) | J-C/J-M and F-28/F-30: unit-only attribution, sample/cohort/key comparator logic; no metrics-as-behavior score |
| [G-10](remediation-plan.md#g-10) | Every future B/SC/DB/spec/history link: proportionate spec before implementation and applicable evidence, no blanket assurance-docs filter bypass |
| [G-11](remediation-plan.md#g-11) | Every evaluated/input/registration cell: parent/feed/task/tool/MSI/agent/required-check identity U until safe authorized receipt |
| [G-12](remediation-plan.md#g-12) | Build 64312/job `4a79149b-973a-554c-792a-7556fdd68297`: failed preview check, no diagnosed leaf/cause and no B-row attachment without receipts |

The tool catalog was checked again for I-02: no relevant `ado`
build/timeline/TRX reader is exposed; unrelated Azure tools or a policy-mutating
PR watcher do not substitute. A-01's REST 401 and the coordinator's RCAKit
OAuth-challenge 401 remain historical, not current failures or a semantic
oracle. I-01 subsequently recovered scoped native read access through existing
Azure CLI authentication. I-02 subsequently used that scoped read-only route
for its separate original and repaired-candidate native receipts above,
including exact original TRX and attempt/configuration records. No undiagnosed
nightly rerun is authorized or issued.

Authorized increments use existing suites/pipelines in their normal locations
and native CI artifacts with durable SDK-docs result/configuration/reproduction
summaries. D-01..D-10 approach selections do not complete concrete
layout/wiring/oracles/role identities/governance/peer manifests/tool
qualification or operating admission. D-10's concrete numerical profiles,
stores/resources/permissions/enforcement remain unqualified; D-11..D-12
remain open for history/graduation and contract exceptions.
These drafts grant no docs-only testing waiver, complete
inventory, merge or release eligibility beyond separately recorded user scope.

## Immutable A-01 witness references

[shared-domains]: https://github.com/kushagraThapar/SuicideSquad/blob/3b141f8c82a18d660069627c399ccfc9efc837c0/docs/dotnet/domain-gap-matrix.md
[implementation-policy]: https://github.com/kushagraThapar/SuicideSquad/blob/532e2401be02da06becbbc459dd58b78ae9b35d4/docs/dotnet/design-handoff.md
[implementation-d06]: https://github.com/kushagraThapar/SuicideSquad/blob/72e9f57918ed81a38bd750f4a6aca58173877d7b/docs/dotnet/design-handoff.md#106-user-approved-d-06-approach
[current-policy]: https://github.com/kushagraThapar/SuicideSquad/blob/90368a6ac0fa0434fcf972cd66109ebcf57cda54/docs/dotnet/design-handoff.md
[i03-policy]: https://github.com/kushagraThapar/SuicideSquad/blob/447c660872ceaced30a5b5c60abec3bc5c54eb7b/docs/dotnet/design-handoff.md
[current-d07]: https://github.com/kushagraThapar/SuicideSquad/blob/1fef335ecb72e15e75518cffc2d39814c2e6bd13/docs/dotnet/design-handoff.md#107-user-selected-d-07-approach-and-router-scope-addition
[shared-handoff]: https://github.com/kushagraThapar/SuicideSquad/blob/3b141f8c82a18d660069627c399ccfc9efc837c0/docs/dotnet/design-handoff.md
[j-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L23-L55
[j-unit-flaky]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L57-L89
[j-coverage]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L92-L134
[j-memory]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L136-L175
[j-emulator]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L177-L255
[j-emulator-flaky]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L258-L296
[j-companion-emulator]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L298-L375
[j-regions]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L378-L453
[j-preview]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-preview.yml#L227-L289
[j-internal]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-internal.yml#L20-L46
[j-aad]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test-aad.yml#L37-L188
[j-thin]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-thinclient.yml#L17-L59
[j-fi]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-fault-injection.yml#L17-L130
[t-preview]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-preview.yml
[t-pack]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/nuget-pack.yml#L3-L78
[p-encryption-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/Microsoft.Azure.Cosmos.Encryption.Tests.csproj
[p-custom-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests.csproj
[p-perf-validation]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/Contracts/PerformanceValidation.cs#L29-L134
[a-serializer]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CosmosClientOptionsUnitTests.cs#L891-L951
[a-builder]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CosmosClientOptionsUnitTests.cs#L231-L279
[a-precedence]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/HandlerTests.cs#L1088-L1145
[a-precedence-emulator]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/ReadConsistencyStrategyTests.cs#L113-L160
[a-bulk]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Batch/BatchAsyncContainerExecutorTests.cs#L480-L508
[a-barrier]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/BarrierEarlyYieldOn429Tests.cs#L41-L112
[a-barrier-producer]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/BarrierEarlyYieldOn429Tests.cs#L197-L280
[a-barrier-helper]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/BarrierEarlyYieldOn429Tests.cs#L751-L818
[a-cf-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/FeedRange/ChangeFeedIteratorCoreTests.cs#L114-L135
[a-cf-emulator]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/FeedToken/ChangeFeedIteratorCoreTests.cs#L81-L135
[a-cf-aad]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosAadLiveDataPlaneTests.cs#L920-L967
[a-dtx-replay]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedTransactionCommitterTests.cs#L1673-L1718
[a-dtx-rotate]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedTransactionCommitterTests.cs#L1910-L1978
[a-dtx-aborts]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedTransactionCommitterTests.cs#L1981-L2025
[a-dtx-mixed]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedTransactionCommitterTests.cs#L1721-L1768
[a-dtx-factories]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedTransactionCommitterTests.cs#L2829-L2851
[a-dtx-emulator]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/DistributedTransaction/DistributedTransactionTests.cs#L87-L111
[a-dtx-ignored]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/DistributedTransaction/DistributedTransactionConditionalE2ETests.cs#L24-L64
[a-dtx-read-ignored]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/DistributedTransaction/DistributedReadTransactionE2ETests.cs#L30-L72
[w-session-clear]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/SessionContainerTest.cs#L417-L464
[w-session-lsn]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/SessionContainerTest.cs#L681-L740
[w-key-vectors]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Direct/Routing/DirectPartitionKeyInternalTests.cs#L26-L73
[w-key-helpers]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Direct/Routing/DirectPartitionKeyInternalTests.cs#L986-L1022
[w-region]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/PartitionKeyRangeFailoverTests/GlobalPartitionEndpointManagerUnitTests.cs#L568-L617
[w-client-dispose]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/PartitionKeyRangeFailoverTests/GlobalPartitionEndpointManagerUnitTests.cs#L406-L431
[w-aad-crud]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosAadLiveTests.cs#L70-L116
[w-batch]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosAadLiveDataPlaneTests.cs#L479-L507
[w-database]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosDatabaseTests.cs#L200-L250
[w-cancel]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CancellationTokenTests.cs#L28-L130
[w-processor]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/ChangeFeed/ChangeFeedProcessorCoreTests.cs#L566-L595
[w-thin]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/ThinClientStoreModelTests.cs#L165-L204
[w-http]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CosmosHttpClientCoreTests.cs#L947-L1052
[w-hybrid]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Query/HybridSearchQueryTests.cs#L61-L151
[w-hybrid-run]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Query/HybridSearchQueryTests.cs#L338-L423
[w-query-resume]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosAadLiveDataPlaneTests.cs#L595-L647
[w-linq]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosAadLiveDataPlaneTests.cs#L837-L870
[w-api]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/ContractEnforcementTests.cs#L17-L136
[w-api-helper]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/ContractEnforcement.cs#L399-L453
[w-fi-hard]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/FaultInjection/tests/FaultInjectionBuilderValidationTests.cs#L154-L227
[w-fi-skip]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/FaultInjection/tests/FaultInjectionBuilderValidationTests.cs#L255-L301
[w-encryption]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/EncryptionContainerPatchTests.cs#L38-L75
[w-custom]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/DekCacheDistributedCacheContractTests.cs#L57-L122
[w-native]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/DirectContractTests.cs#L24-L53
[w-dependencies]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/DirectContractTests.cs#L113-L184
[w-etag]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosAadLiveDataPlaneTests.cs#L194-L237
[w-policies]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CosmosContainerSettingsTests.cs#L283-L356
