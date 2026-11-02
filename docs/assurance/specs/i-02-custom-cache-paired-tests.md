# I-02: paired Encryption.Custom cache/cancellation protection

| Field | Receipt |
|---|---|
| Version / recorded | 0.1.6 / 2026-11-02 UTC; native corrected-observer receipt and local source-reference repair |
| Status | Corrected candidate passed ordinary units and all twelve new emulator cells. Introduced preview compilation regression reproduced and repaired locally; repaired candidate still requires native parity/preview-unit/service receipts. Not independently approved contract, complete G-02 closure or merge qualification |
| Scope | A-01 G-02/G-07; matrix B-035/B-036 and their immediate cache/crypto/cancellation preservation neighbors |
| Intake | Existing isolated SDK worktree at `acaff6b6f90c58ca920980d23f31018475eed1eb`; clean before I-02. Upstream main resolved once at intake to `52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`, unchanged from A-01 |
| Historical evidence | A-01 SHA-256 `63e368bc66f5c876bb853de62116a9d54ce34f9e1066b5f0b89da8df95b4949c`, 98,011 bytes. [I-01](i-01-encryption-patch-paired-tests.md) and its original source/merge-candidate/TRX receipts remain unchanged |
| Authorization | User directed implementation after the proposed I-02 continuation; existing paired unit plus emulator/explicitly authorized isolated-live scope and resource boundaries remain binding |
| Process | Proportionate spec first, existing MSTest/fixtures/native CI; no new assurance platform, testing dependencies, production-source change or tool-admission experiment |

## Intended behavior and authority

This increment protects three existing expectations:

1. A distributed-cache `TimeoutException` with a live caller token falls back to the real DEK source.
2. An `OperationCanceledException` originating inside the distributed cache, while the caller token is not cancelled, likewise falls back without cancelling the caller's operation.
3. A caller token cancelled before a cold cache request is honored without invoking the L2 cache or DEK source. A later uncancelled request using the same instance remains usable.

The actual assertion witnesses are the existing
[`DekCacheDistributedCacheContractTests`][cache-witness] methods
`GetAsync_ThrowsTimeoutException_FallsBackToCosmos`,
`GetAsync_ThrowsOperationCanceledException_CallerTokenNotCancelled_MustNotFailTheOperation`,
and `GetOrAddDekPropertiesAsync_CallerTokenAlreadyCancelled_ThrowsOperationCanceledExceptionPromptly`.
Their names and implementation are evidence, not independent authority. The
[public cache options][cache-options], [provider surface][provider] and
[customer-facing cache/diagnostic descriptions][custom-changelog] ground
the feature's optional nature, container scoping, wrapped-properties boundary and
provider lifetime. They do not independently settle every cancellation phase,
exception message or source-call-count contract. B-035/B-036 therefore retain
their **Q/oracle-unqualified** status; this adds observed protection rather than
new product semantics or an assertion that current implementation validates itself.

The [canonical concept reference, chapter 17 section 1][concept-retries],
consulted at `bcfbc786fa5296deebcccd11369efb583e63e48c`, distinguishes SDK
and gateway/backend surfaces. Here the injected fault belongs to the optional
client cache, not the service or transport. Logical key-read observations are
not physical HTTP/RNTBD attempt counts. Pre-cancelled-before-dispatch protection
does not imply that timeout/cancellation after transmission prevents commit,
or that the SDK can roll back a possibly committed operation.

## Protection gaps and bounded cases

The existing unit assertions cover returned DEK id, source-call count and
pre-cancelled zero source/L2 reads. They do not jointly establish cold L2
participation, preserved caller-token forwarding, returned-properties identity,
or successful warm-cache/recovery controls. A provider constructor/build or
mocked source is not the meaningful service counterpart.

| Case | Focused unit protection | Emulator protection through public SDK |
|---|---|---|
| Timeout, caller live | Preserve named test; observe one L2 read, exact id/context/token at one source fetch, same returned properties and live token; a repeated read uses L1 without another L2/source read | Cold provider + injected `IDistributedCache` timeout + actual key container: encrypted typed Create succeeds, ciphertext is present in a separate unencrypted point read, and a second cold provider successfully reads/decrypts the exact item. Both cold lookups must be observed at L2 and the real key-container request boundary |
| Internal cancellation, caller live | Same assertions, preserving this distinct named seed; an internal cache exception must not be mistaken for caller cancellation | Same public encrypted Create/read sequence, injecting internal cache `OperationCanceledException` while the caller token remains live |
| Caller pre-cancelled | Preserve existing named test's required exception and zero L2/source reads; assert cancelled caller remains cancelled, then perform a successful uncancelled cold read through the same cache | Public encrypted typed Create with an already-cancelled token throws before L2/key reads. A fresh ordinary point read observes item absence. Same client/provider/cache then successfully creates and reads the item with an uncancelled token, proving an active observer and usable recovery path |

Service parameter cells are Gateway and Direct. The failure method declares
`(useGateway, internalCancellation)` = `(true,false)`, `(true,true)`,
`(false,false)`, `(false,true)`; the pre-cancel method declares
`useGateway` = `true`, `false`. Six terminal leaves per TFM are expected,
not two data-driven parents. Typed item operations, both transports, cold
providers and the default JSON processor are deliberately bounded here.
Stream item APIs, in-flight caller cancellation, expiry/proactive refresh,
background-write races, cross-provider prefixes, real distributed-cache
servers and broader serializer/rotation behavior remain follow-up.

## Fixtures, independent outcome observers and controls

- Reuse the existing MDE Custom emulator class's unique database, real key/item
  containers and test key-store provider. Give every case a unique DEK and item.
  Bind a fresh `CosmosDataEncryptionKeyProvider` publicly to the key container
  from the case's Gateway/Direct client. Use the real `CosmosEncryptor`.
- Only the `IDistributedCache` dependency is controlled. The forwarding request
  handler must still call the real client/service boundary. Count the named
  key's logical point reads and require positive counts during the successful
  controls; an inactive handler cannot earn zero-I/O credit.
- For fault cases, use separate cold providers for Create and Read, so a warm
  key cache cannot hide the second fallback. Compare every field of the chosen
  existing simple document model and explicit success statuses. Inspect the
  stored document independently without the encryption wrapper: id/PK/plain
  field unchanged, encrypted field not plaintext and encryption metadata
  present. This is modeled payload/ciphertext evidence, not all-JSON or
  production-durability proof.
- For pre-cancellation, item absence and successful same-instance retry are
  outcome controls, not universal no-commit claims. Do not equate no logical
  key reads with no account/container/address metadata I/O.
- Dispose case clients/providers and all stream responses. Existing class
  cleanup targets only its unique fixture database but does not assert the
  `DeleteStreamAsync` response status. A passing test is not independent
  cleanup-outcome/lease-fencing or capacity/fault/trust-isolation proof;
  retain that fixture-qualification gap rather than claim D-10 activation.
  Do not log raw keys,
  ciphertext/cache blobs, credentials or account operational environment dumps.
- No sleeps, new retry loop, swallowed assertion, new production injection
  hook or expectation changed to make CI green. If a candidate exposes a real
  behavior discrepancy, retain the reproduction and separate fix/oracle question.

### Published stored-field observer correction: specification before edits

Post-publication inspection found an introduced **test-observer defect** in
`d5004b187a04adf6a5f87a3197b912b217c8aae2`: the new service test requires
`Sensitive` to be absent in the raw stored item. The intended rule above is
that the encrypted field is **not plaintext**, not that its name disappears.
[MDE's current processor][mde-representation] replaces the property value
with encrypted bytes; it does not use the legacy whole-document removal
representation. Existing [MDE unit prior art][mde-unit-representation] already
requires the retained string property to differ from plaintext and checks
encryption metadata plus full decryption. This is source/assertion evidence,
not independent approval of a public ciphertext-format or cache contract.

Before any service result can qualify, correct only the observer and strengthen
the corresponding existing MDE unit helper: the nonnull sensitive property must
be a JSON string containing nonempty base64 bytes and differ from its original
plaintext. Missing/empty/plaintext/invalid-base64 values must fail. Preserve
the null-property unit case, untargeted fields, metadata, exact decrypted
values, cold-provider/L2/key-read counts, caller-cancellation controls, both
transports and both TFMs. Reuse
`ValidateEncryptDecryptDocument_VerifyByNewtonsoft` and its same-helper
neighbors rather than adding a duplicate format suite or changing production.
Run the existing MDE unit class on both TFMs and rebuild the service project;
real service execution remains separate and required.

The v0.1.3 amendment was frozen at `2026-11-01T23:23:00Z`, SHA-256
`7e5658b0e24f0c5013c006bcfba605509a90f46c4d1dc6fe7823ed95725ff366`,
before those edits. The corrected helper/class passed 8/8 net6 and 24/24 net8
leaves against current-core source preview, with zero skipped/inconclusive
results. The corrected service assemblies compile and list the two parents
on each TFM; they have not executed locally. This amendment does not
retroactively repair or qualify the original candidate.

The original source assertion and build 64329 remain attributable to their
original candidate. A static contradiction is not an executed service failure,
nor is the original assertion counted passed. No SDK functional defect or
changed cache/cancellation expectation is inferred. A later real cache/service
discrepancy still requires preserving its reproduction and separate contract/fix
disposition, not adjusting expectations for a green result.

### Source-reference compiler regression: specification before repair

Corrected source `c9ed2e4aa7d1c191c2eab0e10e12a6dd00d3194c`, merge
`24003b84da5c926c87a81c2c951296790843e4af`, automatically selected build
64330. Original TRX from emulator runs 1331387/net6 and 1331388/net8 proves
all six exact new leaves per TFM passed. This is actual service evidence,
not source discovery. It does not repair the separate preview predecessor:
`Build Microsoft.Azure.Cosmos.Encryption.Custom Project Ref` fails `CS0012`
for `PartitionKeyInternal`/`Microsoft.Azure.Cosmos.Direct, Version=3.44.1.0`
in both emulator TFMs, so the subsequent preview Custom unit task is skipped.
Original log 205 SHA-256 is
`03c63645fe4e13b0e59ec04e56231cb4b8d8881363aa233bcc4943210e9460ac`.

The same predecessor succeeded before I-02 in build 64325, log 216.
Do not dismiss this as pre-existing or diagnose a product defect from it.
The narrow existing emulator project locally reproduces both errors with
`--configuration Debug -p:Optimize=true -p:IsPreview=true -p:SdkProjectRef=true`,
matching the native command's relevant configuration/reference properties.
Original local failure and native results must remain attributable to their
actual sources.

The bounded repair hypothesis is that the two newly introduced `PartitionKey`
locals held across awaits create a compiler dependency on the struct's hidden
Direct-assembly field type. Existing service prior art constructs partition
keys inline. First test that minimal representation change, retaining the
same document partition-key value at every public operation. Do not remove
cases/observers, change expected results, exclude source builds, suppress the
compiler error or add/change a Direct/package dependency. If the hypothesis
fails, retain that attempt and investigate the precise compiler/reference
boundary rather than relabel a failed build or expand the Direct migration.

Acceptance requires both TFMs to compile in Debug/Optimize source-preview
mode and the existing Release source/package modes, with the original
parity tasks/gates unchanged. The cache/ciphertext/cancellation assertions,
all six Gateway/Direct service leaves and their unit counterparts must remain
unchanged and separately evidenced on the repaired candidate. A compile-only
representation repair has no independent service rule to manufacture; the
existing paired product tests remain required. Record the frozen amendment
before editing test source and keep the original compiler failures, canceled
candidate and passed service receipts separate from the repair.

The v0.1.5 amendment was frozen at `2026-11-02T00:11:28Z`, SHA-256
`35897927abee466b6e7a63a04cb4e137a40c22a6d14357d866e715ef61d4ac29`.
Removing only those two cross-await locals and constructing the same public
partition key inline, as neighboring tests do, makes both TFMs compile with
the failing command's properties. Both source-preview Debug parity modes
(`DefineSdkProjectRefSymbol` default and `false`), source-preview Release,
and package-backed Release then succeed without changing references or gates.
This isolates the introduced compiler dependency to the hoisted representation;
it does not diagnose a service failure or qualify the unfinished Direct migration.
The three source-build controls retain two Core SourceLink metadata warnings
each; package Release has zero warnings/errors. Do not call them all
warning-free or claim complete source-link/loaded-artifact attestation.

The original local compiler log SHA-256 is
`72315dbf5457668f7fa482a5d88946ec024c4f3a3c34ababf5e74ef72f62b9f7`;
the repaired matching-mode log is
`53f20062001bb91bea1a40db4f477ae49f3e48d80a98a4a934f6a1008c71ddd6`.
The structured local repair receipt SHA-256 is
`b06a1580e1a5ac0657f8dfb2c35bcd06afd2b1a15b680a45e761fc1280365388`.
It retains all five compilation attempts, source-file before/after hashes
and distinct source `3.64.0-preview.0` versus package `3.41.0-preview.0`
output/dependency records. The repaired test has SHA-256
`2496129b5a04b5884228c27e5fd3a9e320f902b6aa0fde96085a6832176e53e5`.
No assertion, case, cache fault, forwarding observer or product implementation
changed. Local compilation is not native execution of that repaired source.

## Native CI, bindings and preservation neighbors

The Custom unit and emulator projects already declare `net6.0;net8.0`.
The source project targets `netstandard2.0;net8.0`: the lower test target
therefore exercises a different companion build than the upper target.
Its default core dependency is unconditionally `Microsoft.Azure.Cosmos`
`3.41.0-preview.0`, even in the ordinary job. Do not label ordinary Custom
execution a GA core-package binding. Current-core `SdkProjectRef=true`
and `DefineSdkProjectRefSymbol=false` comparisons are distinct.

Add one explicit Custom unit task to the existing ordinary unit job
(`IncludeEncryption`-conditional) and one to the existing Custom preview
parity job. Preserve caller filters, OS/configuration, result publication,
existing timeouts/retries and existing source/package-surface parity builds.
Run both existing test TFMs; do not silently select only the lower target.
Retain the existing Custom emulator job; the new leaves are selected there
without another platform/job. Its ordinary and preview-symbol applicability
must remain explicit: no newly declared preview-service lane is claimed passed.

Preserve I-01 Encryption units/emulator tasks and their filters, core tests,
the existing Custom cache contract class, constructor/lifetime/crypto tests and
source-reference compile parity. Before crediting any native run, record the
evaluated command, actual TFM/host/runtime, candidate/merge candidate,
resolved source-versus-package binding and exact terminal leaves. A passing
job label is not sufficient. No package version, dependency, API baseline,
Direct-source wiring or production implementation is changed.

### Initial local baseline and discovered harness limits

Before test/CI edits, the existing cache contract class passed **13/13** on
Release/net6 with SDK 9.0.304, VSTest 17.14.1 ARM64 and the I-01 verified private
runtime 6.0.36. Original log/TRX remain separate from any strengthened candidate.
The same class compiled for net8, but execution aborted because ARM64 runtime
8 was absent. Retain the abort; install a verified private official runtime
only in response to that demonstrated prerequisite, without global changes,
test retargeting or roll-forward.

Net6 discovery also reports four pre-existing `UTA007` signature warnings in
`LegacyEncryptionProcessorTests`: `InvalidPathToEncrypt`,
`EncryptDecryptPropertyWithNullValue`, `ValidateEncryptDecryptDocument`,
`ValidateDecryptStream`. These are **not executed cases**. Their discovery and
paired-counterpart repair is separate G-07 backlog, not silently zero, waived,
fixed by selecting the project, or a new SDK functional-defect diagnosis.

The full eligible project baseline subsequently executed 421 net6 leaves
(420 passed, one failed) and 752 net8 leaves (751 passed, one failed), using
the verified private runtimes. Both failures are the existing
`ScopeRecord_Properties_AreCorrect`: one second of `Stopwatch.Frequency`
ticks becomes 100 seconds. Read-only inspection establishes the root cause
in the **test-only** `TestableCosmosDiagnosticsContext.ScopeRecord.Elapsed`:
`TimeSpan.FromTicks` treats Stopwatch-frequency ticks as TimeSpan ticks.
`TestableScope.Dispose` records actual Stopwatch ticks, not TimeSpan ticks.
No SDK production timing defect is inferred.

The independent framework reference is the official
[Stopwatch.ElapsedTicks remarks][stopwatch-authority]: timer ticks represent
`1 / Frequency` seconds and differ from 100-nanosecond time ticks. This
grounds the test-helper arithmetic repair, not B-035/B-036 product contracts
or their independent oracle approval.

This discovery is tightly coupled to selecting the existing unit suite.
Before enabling it, correct the test helper to convert using
`Stopwatch.Frequency`, preserve the original one-second assertion, and add
exact deterministic zero/one/two-second unit controls. Do not exclude the test,
relax its assertion, retarget projects or change production diagnostics.
There is no service boundary for this test-only arithmetic. The proposed
local-only treatment is surfaced here and in the implementation handoff;
it is not a silently waived paired product obligation or an approved general
exception policy. Native service evidence remains mandatory for B-035/B-036.

The OpenSpec CLI is absent; attempted status/apply commands returned
`command not found`. This increment uses the repository's existing
`docs/assurance/specs/` records instead of installing an unrelated tool.

## Shared policy, visible peer scope and non-activation

The I-02 implementation policy was [handoff v0.2.9][implementation-d09] at
`d05b94e439ad9542d322da96b9821927b4cdf999`, sections 6.1-6.2/10.9,
with baseline/domain matrix v0.1.9, published in open
[kushagraThapar/SuicideSquad#8][policy-pr] at the received receipt.
At that receipt D-01..D-09 approaches were selected and D-10..D-12 open (three).
The historical D-05/D-06/D-07 receipts and D-08's
`12c7cc3af87208d46a81b3f066262bcfdd3ceab2` remain unchanged.

The subsequent current [handoff v0.2.10][current-policy] at
`90368a6ac0fa0434fcf972cd66109ebcf57cda54`, sections 8.8-8.9/10.10,
baseline/domain matrix v0.1.10, published D-10 in the continuing open shared
PR8 while native I-02 CI was being observed. D-01..D-10 approaches are now
selected; D-11..D-12 remain open (two). The preauthorized-profile envelope
adds no operating/merge authority: concrete retention/stores, whole-run
numeric limits, resources/isolation/capacity/leases, identities/permissions,
cleanup/stop/recovery and enforcement remain unqualified. Unique namespaces
are not throughput/fault/trust isolation proof. It neither revokes this
separately authorized SDK increment nor waives its paired-service evidence.
Earlier policy/source snapshots are not rewritten.

D-08 keeps .NET, Java, Python, Go, Rust and JS/TS visible. For this bounded
companion/cache behavior, .NET source/assertions are inspected; peer
applicability/contracts/results are **unverified**, not unsupported or equivalent.
No peer research/execution/review is dispatched under this update, and the
frozen Java work is not resumed. Required independent comparisons/reviews are
not claimed complete or counted as extra votes.

D-09 retains native MSTest/coverage/CI and evidence-based technique admission.
Stryker.NET, FsCheck and selective Coyote remain unqualified candidates; the
existing SharpFuzz path is not a new authorized campaign. No installation,
spike, mutation/fuzz/schedule execution or score follows from policy publication.
This increment uses only the separately user-authorized SDK testing scope.
No governance, credentials/permissions, merge/Agent merge, release, arbitrary
production resource or account provisioning is authorized.

## Acceptance and result ledger

| Surface | Required evidence | At v0.1.6 |
|---|---|---|
| Focused units | All three named strengthened tests plus existing same-class neighbors, exact case outcomes and first-failure preservation for both TFMs | Three seeds strengthened; all 13 cache-class leaves pass in the six local bindings below |
| Wider Custom unit selection | Evaluated ordinary/preview invocation and eligible executed cases for both TFMs; retain warnings/skipped/inconclusive/failed dispositions | Ordinary native c9ed units passed 424/424 net6 and 755/755 net8; preview invocation skipped after compiler failure. Local repair passes both parity modes; new native candidate required. Original failures and UTA007 warnings retained |
| Local harness conversion | Correct test-only frequency conversion with original one-second assertion preserved and exact zero/one/two-second controls | Test helper repaired; original one-second assertion unchanged; all four witness leaves pass. Meaningful service half inapplicable to test-only arithmetic; proposed treatment surfaced |
| Gateway/Direct service | All six exact new leaves per applicable TFM, real key reads and meaningful ciphertext/decrypted-value/control outcomes | Corrected c9ed candidate passed all six exact leaves in each native TFM, runs 1331387/1331388. Repaired source-reference representation is separate and awaits its own native service receipt |
| MDE observer preservation | Existing nonnull string-encryption unit helper preserves the field, rejects plaintext/empty/invalid-base64 values and retains null/metadata/decryption controls | Strengthened existing helper; current-source preview class passed 8/8 net6 and 24/24 net8 leaves, original full-package receipts remain separately attributable |
| Current-core comparison | Explicit source/package-symbol binding and focused unit outcomes/compilation; service boundary qualified separately | Current-source preview `3.64.0-preview.0`, both TFMs, 52/52 cache+diagnostics leaves each; not package/service/GA proof |
| Candidate/configuration/artifact | Native evaluated logs, original TRX/subresults, exact source and actual merge-candidate identity; installed/loaded/fixture gaps explicit | c9ed/24003/build 64330 ordinary-unit/service commands, original TRX, terminal leaves and setup obtained below. Native repair/preview, complete loaded payload and fixture qualification remain separate |
| Whole-SDK obligations | Preserve all existing matrix inputs and unexpanded backlog, including counterparts and invalid-discovery methods | Remain open; no denominator or qualification claim |

Record each later attempt and version without overwriting original failures,
baseline outcomes, A-01 provenance or I-01's executed source. Written, compiled,
discovered, selected and passed execution are separate states. Local service
execution still requires a capable authorized Windows-emulator host or
explicitly authorized isolated account; none is inferred from arbitrary local
configuration. Ordinary PR CI may provide the native service receipt; do not
manually trigger a nightly/release or rerun undiagnosed build 64312.

## Actual local result/configuration receipt

Both pre-implementation snapshots are retained: v0.1.0 SHA-256
`e1335f339ea0f4b45cc7279eb5dc2ccf6a482fccb93f013cf8cb24041c53974e`
at `2026-11-01T22:51:55Z`; baseline-driven v0.1.1
`8e03199976ac6d90bcdcca1f5e4b69cb58c7c81590d872e0c868bca2ddc4acc5`
at `2026-11-01T22:53:33Z`. These times identify the frozen artifacts before
test/CI edits, not later executions or policy approval.

| Local attempt / binding | Result and original TRX SHA-256 |
|---|---|
| Existing cache baseline, net6 | 13/13 passed; `2621592e6a05152c77ca70c8160c3141070d66f4f0f8d56ca95b319f2e1481f1` |
| Existing cache baseline, net8 attempt 1 | Testhost aborted for missing runtime; zero-result counters do not mean no applicable tests. `b57e82e31c646880df70801b86624c11cd114d376f7eb175eaa82bc07e724087` |
| Full original net6 baseline | 420 passed/1 failed/0 skipped eligible leaves; `3af30a56438778eb8b4ca8974a7c0c65086c7dcd6011e2820713ab0a774b2f43` |
| Full original net8 after runtime repair | 751 passed/1 failed/0 skipped; `2fd1ccfbdbc0a40163d3d3b4c65b5ff1e3624b1bb2011d340eb4d25fdba6f343` |
| Ordinary package-backed net6 | 424/424 passed/0 skipped; `30fd86df1787d6e2cb4846ea706f1b7f94f52c4258fb0cd85ac3e4e39eeb88e1` |
| Ordinary package-backed net8 | 755/755 passed/0 skipped; `01759603c8671ff2fd3fcf6a0a7e40c75feac413c99046bdec272c63f55f6d40` |
| Preview package-backed net6 | 424/424 passed/0 skipped; `f806542282074174ac83909e0fe67c9989b0a4462c78cc47a5310934585a6e16` |
| Preview package-backed net8 | 755/755 passed/0 skipped; `4c5e76f9db4ff1f747aaaeb97c216461d035373ad2f60dad2a50b2a898f15bd4` |
| Current-source preview net6 focused | 52/52 passed/0 skipped; `d87d85f446fd4134323360d7ad2e1182af47f69d200bddb860c9f51fd92e3373` |
| Current-source preview net8 focused | 52/52 passed/0 skipped; `afcdc0ba27b5590b8091c0f222e7ea93bd3752716e865adcd8c62ae2e4fbc2c2` |

The package-focused attempt preceding each ordinary full project passed
52/52 as well. Focused selection is
`FullyQualifiedName~DekCacheDistributedCacheContractTests|FullyQualifiedName~CosmosDiagnosticsContextTests`:
13 cache and 39 diagnostics terminal leaves, including the three existing
cache seeds, original one-second assertion and three new frequency controls.
The full-project count increases by **three** relative to baseline because
existing cache tests were strengthened, not duplicated. Net8 already has a
larger conditional cohort; its extra leaves are not an I-02 coverage gain.
Those original v0.1.2 local unit-test commands succeeded on their first candidate attempt;
the original runtime abort and both pre-change assertion failures stay retained.
The four UTA007 methods are absent from discovery, not "0 skipped" success.

Local host: macOS ARM64, SDK 9.0.304, VSTest 17.14.1; Release; no global
runtime/roll-forward change. Explicit private testhost runtime 6.0.36 is the
verified I-01 prerequisite. Runtime 8.0.33 was installed privately after the
captured missing-runtime failure, using official .NET 8 release metadata
SHA-256 `e11dbb88706ea3ba9ac4e3b022f1ab9b49e7fcbe6448f63fa6764199b7cca822`
and the official osx-arm64 archive SHA-512
`0be37b64ea1f49651bbaa3dd08e89476919ceb98bd6b39d28883439c16c1d9cbcccf5cf01f67c913095e9f23235dc621125a551a6e736be4ebddbf060eb82758`.
The temporary downloaded archive was removed after verified extraction.
The private runtime is not an adopted test technique or production dependency.

Reproduction uses the existing Custom unit project:

```text
dotnet test <Custom-unit.csproj> --configuration Release --framework <net6.0|net8.0>
  --filter "TestCategory!=Quarantine&TestCategory!=Ignore"
  -p:SdkProjectRef=false [-p:IsPreview=true]
  --results-directory <attempt-directory> --logger "trx;LogFileName=<unique>.trx"
  -- RunConfiguration.DotNetHostPath=<verified-matching-private-runtime-host>
```

For source comparisons, select the focused filter above with
`-p:SdkProjectRef=true -p:IsPreview=true`. Output/deps records identify current
core `3.64.0-preview.0`, Custom `1.1.0-preview01` and resolved
`Microsoft.Data.Encryption.Cryptography` `2.0.0-pre024`; the dependency range
was not changed/pinned. Package-backed unit and emulator outputs resolve
core `3.41.0-preview.0`. Output assembly digests are retained separately;
this is not full loaded/native/installed-nupkg attestation.

`dotnet build` of the existing Custom emulator project succeeded with zero
warnings/errors for both TFMs and `SdkProjectRef=false`. `--no-build --list-tests`
with the two new method names lists **two data-driven parents per TFM**, not
six expanded rows. No class initialization/service execution was attempted
locally. A Gateway-only ARM64 container does not substitute for the existing
Windows/Direct Custom fixture.

Both YAML files parse, and a bounded comparison finds exactly one new Custom
unit invocation in each intended existing job. This is tracked selection,
not native evaluation. Existing filters/retries, I-01 test bodies,
TFMs/dependencies, production source, API contracts and docs-path exclusions
are unchanged. The c9ed native receipts below supplement these local records;
the repaired candidate requires separate native outcomes and identity.

### Corrected-observer local receipt

The existing MDE class was selected with
`FullyQualifiedName~MdeEncryptionProcessorTests`, `SdkProjectRef=true`,
`IsPreview=true`, the same explicit private hosts and native MSTest tooling.
Net6 passed 8/8, TRX SHA-256
`18422e266b379b77e67903986bbefb2108ffa36786e84558a7a735c8d11c8e18`;
net8 passed 24/24, TRX
`94ad6f3e4ebe0681980dea8a7ed751620d0e42f689e752634da7281807754461`.
No new cases, frameworks or dependencies were added. The existing
`ValidateEncryptDecryptDocument_VerifyByNewtonsoft` and same-helper cases
exercise the stronger observer; full null and round-trip controls stay intact.

The following service `--no-restore` build initially failed with
`CS0234`/`CS0246` missing core types after switching from source to package
references. The retained pre-restore assets identify core
`3.64.0-preview.0` as a project. Restoring the already-declared
`SdkProjectRef=false` graph changes that evaluated asset to package
`3.41.0-preview.0`; the repeated build then succeeds for both TFMs with zero
warnings/errors. No source, dependency range, runtime or expectation was
changed to fix this graph mismatch. The original failed log remains separate.
Both corrected service assemblies list only the two data-driven parents;
listing does not initialize the fixture or execute the six leaves.

The correction's local structured receipt SHA-256 is
`419412d0d989c5371ef942b4c74031c05774c234ec28117e52be1b32b5f0fcd0`.
It retains exact unit results, source-file digests, original/repaired graph
and build logs. These are resolved/build-output identities, not complete
loaded/native-artifact attestation or service results.

## Native PR-CI receipt: corrected observer before source-parity repair

Source `c9ed2e4aa7d1c191c2eab0e10e12a6dd00d3194c`, tree
`c84c7dd6b513a2c8f4f0e6991d6330cb12877563`, was immediately pushed.
Automatically queued [build 64330][corrected-native-build] uses actual merge
`24003b84da5c926c87a81c2c951296790843e4af`, with exactly A-01 base and c9ed
as parents and the same tree. This is not the later local parity repair.

| Existing task / original run | Target | API results / raw TRX / terminal leaves | Original TRX SHA-256 |
|---|---|---|---|
| Ordinary Custom units / 1331395 | net6 | 424 / 424 / 424, all passed | `4988ef090e254ba37823a49060895fb5f154d7e7dcff6ae0f227b1be39c1fa2f` |
| Ordinary Custom units / 1331396 | net8 | 755 / 755 / 755, all passed | `da7ab8343b504d34e0cee7df0465bb4943333e013a6593e28b9f83ed8ca4973e` |
| Custom emulator / 1331387 | net6 | 75 / 81 / 79, all terminal leaves passed | `a5fbb7cd49fa83a989c350f72921f377831be7066ce588698446cadf49047de7` |
| Custom emulator / 1331388 | net8 | 81 / 87 / 85, all terminal leaves passed | `a7a836b55ffec028c6eb8b091bb687a5cc999fbcabed148a097b495ce058637d` |

Both ordinary unit runs contain all three exact cache seeds, the original
one-second assertion, three frequency controls and existing MDE preservation
witnesses. Both service runs contain all six exact Gateway/Direct fault/
pre-cancellation leaves, with the corrected ciphertext, cold-provider,
real-key-read and successful-recovery observers. No required leaf is
failed/skipped/inconclusive. Four legacy UTA007 methods are still absent
from discovery, not passed or skipped. Service API parent counts are not
terminal counts: `75 - 2 + 6 = 79` and `81 - 2 + 6 = 85`; raw TRX includes
both the two parents and their children. These are observed eligible cohorts,
not a complete expected whole-SDK inventory or assurance denominator.

Unit task `ee96cbd6-2629-5f4f-bd04-a44d810c12d7`, log 299, ran
`00:11:07Z..00:11:38Z` on 2026-11-02; service task
`1c7974ee-11bd-5e6b-cde4-b712eba78e44`, log 280, ran
`23:52:06Z..23:55:33Z` on 2026-11-01. Both have task attempt 1 and empty
previous-attempt records; each full log contains one `dotnet test` command
and the two target invocations, not a concealed successful retry. Existing
configured retry limits were not changed.

Both commands use Release, `/p:OS=Windows` and
`TestCategory!=Quarantine & TestCategory!=Ignore`; units explicitly pass
`SdkProjectRef=false`, while service retains its existing default package
binding. Neither passes a new preview-service flag. Compiler/output records
resolve core package `3.41.0-preview.0`, not current-core service execution.
Both use VSTest 17.11.1 x64; setup logs install runtime 6.0.36 and SDK 8.0.427.
The emulator readiness log reports Running before invocation. Installed
versions/target paths do not attest every actually loaded runtime/native DLL,
installed nupkg, fixture capability, cleanup response or lease-fencing outcome.

The preview source-reference predecessor fails on both TFMs, as specified
above; subsequent NuGet-surface parity and Custom preview units are skipped,
not zero applicable cases. Its nominal Release job name does not override
the actual default-Debug/Optimize build command. The source-parity repair is
locally compiled only and needs a fresh automatically selected candidate.
Native task success elsewhere cannot conceal this introduced failure.

Original build 64329 remains separate: at the retained `23:46:17Z` observation,
all three Custom tasks were skipped under canceled parent jobs, with no Custom
result run. No cancellation actor/reason is established and none was requested
here. Its original incorrect observer did not execute and earns no passed
service credit. Neither a cancellation nor this compile failure is diagnosed
as an SDK functional defect.

## Changed files and publication boundary

| File | Bounded change |
|---|---|
| `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/DekCacheDistributedCacheContractTests.cs` | Strengthen three existing seeds with source/token/identity/L1/recovery controls and reusable throwing-cache observations |
| `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/TestableCosmosDiagnosticsContext.cs` | Correct test-only Stopwatch-frequency conversion |
| `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/CosmosDiagnosticsContextTests.cs` | Exact zero/one/two-second controls; original one-second expectation retained |
| `Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/MdeEncryptionProcessorTests.cs` | Strengthen existing nonnull sensitive-string preservation helper; no new format suite or product change |
| `Microsoft.Azure.Cosmos.Encryption.Custom/tests/EmulatorTests/MdeCustomEncryptionTests.cs` | Two public-SDK parent methods/six declared Gateway/Direct leaves, controlled optional cache, real key-read/ciphertext/decryption/recovery observers |
| `templates/build-test.yml`, `templates/build-preview.yml` | One explicit Custom unit task per established job, no new job/retry/framework |
| `docs/builds-and-pipelines.md` | Accurate Custom TFMs, selection, preview dependency and distinct service/source boundaries |
| This spec, `../remediation-plan.md`, `../feature-behavior-test-matrix.md` | Versioned authorization, historical D-09/current D-10 backlog, paired cases, observer amendment and separate local/native evidence states |

The user explicitly selected **"Add I-02 as a separate commit to PR #6182 and
collect its ordinary CI receipts"**. Publish on the existing dedicated
assurance-remediation branch; no new session/branch, direct main push,
manual CI trigger, merge or release is requested. Existing I-01 tests/spec
and its executed source/merge candidate stay historical. This test/CI/docs
increment changes no shipped source or customer behavior, so the PR's
no-changelog exception is justified; package/API/version settings stay untouched.

Published source is `d5004b187a04adf6a5f87a3197b912b217c8aae2`,
tree `b1de7ba171479d9bc26f32f5eba1c43df7be3c83`; commit was immediately
pushed. Native ordinary PR build [64329][native-build] automatically queued
at `2026-11-01T23:03:31Z` and started at `23:08:42Z` against
`21b9f761f022f689399c496978693d4625c8cfb7`. Its parents are exactly A-01
base `52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8` and I-02 source; its
tree equals the published tree. No explicit CI/nightly/release trigger
was issued. Queued/in-progress native state does not establish executed
unit or service cases.

At `2026-11-01T23:27:48Z`, that original build remained in progress and its
ordinary Custom-unit task was pending; no Custom unit/service run was yet
returned. Other projects' three returned test runs are not I-02 case evidence.
The finite 20-minute read-only observation window ended without cancelling,
rerunning or diagnosing CI. The corrective test/code and necessary result/D-10
update require a separate ordinary PR candidate; future native records must
identify it rather than relabel build 64329 as corrected execution.

The app's PR REST update returned HTTP 404, request id
`D864:8AE04:9E197D:2063FCD:6AE7C5DC`, retained as a publication/access error,
not an SDK/test failure or expired-login diagnosis. A fresh OPEN/head/body
concurrency guard followed by the already user-approved PR-only publisher
credential succeeded; live title/body were verified. No global authentication,
remote, permission or auto-merge/Agent-merge setting changed.

Ordinary c9ed units and all twelve service cells now have native case receipts;
preview execution and native verification of the local source-parity repair
remain pending. Independent oracle/review/challenge, complete fixture/loaded-
artifact identity and broader Custom/source/stream/in-flight/peer obligations
remain unqualified. No green label, passing local compile or earlier candidate
closes those separate obligations.

[cache-witness]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/DekCacheDistributedCacheContractTests.cs#L57-L122
[cache-options]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/src/DistributedCacheOptions.cs
[provider]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/src/CosmosDataEncryptionKeyProvider.cs
[custom-changelog]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/changelog.md
[concept-retries]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/17-status-codes-and-sdk-retries.md#1-overview
[implementation-d09]: https://github.com/kushagraThapar/SuicideSquad/blob/d05b94e439ad9542d322da96b9821927b4cdf999/docs/dotnet/design-handoff.md
[current-policy]: https://github.com/kushagraThapar/SuicideSquad/blob/90368a6ac0fa0434fcf972cd66109ebcf57cda54/docs/dotnet/design-handoff.md
[policy-pr]: https://github.com/kushagraThapar/SuicideSquad/pull/8
[stopwatch-authority]: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch.elapsedticks?view=net-8.0#remarks
[native-build]: https://cosmos-db-sdk-public.visualstudio.com/4000bd49-81c3-47f2-94d8-d1392b95c228/_build/results?buildId=64329
[corrected-native-build]: https://cosmos-db-sdk-public.visualstudio.com/4000bd49-81c3-47f2-94d8-d1392b95c228/_build/results?buildId=64330
[mde-representation]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/src/Transformation/MdeJObjectEncryptionProcessor.cs#L42-L86
[mde-unit-representation]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/MdeEncryptionProcessorTests.cs#L369-L414
