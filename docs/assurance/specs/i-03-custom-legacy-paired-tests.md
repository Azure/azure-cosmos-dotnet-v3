# I-03: discoverable, paired Encryption.Custom legacy protection

| Field | Receipt |
|---|---|
| Version / recorded | 0.1.3 / 2026-11-02 UTC; corrected-account retry and conditional one-build authorization |
| Status | Four methods repaired with all original processor rows; local units pass and paired service rows compile/list. Standard PR publication blocked by GitHub 403/Enterprise Managed User restriction; service/native execution unobtained. Not independent oracle approval, retirement, whole-G-07 closure or merge qualification |
| Bounded scope | Repair four existing `LegacyEncryptionProcessorTests` methods; preserve their meaningful assertions and controls; pair the underlying legacy behavior using existing real-emulator infrastructure |
| Isolated intake | Parent head `7330088136240c8586ed5eea63f2a4f9a0c366e5`, tree `ec1584846a7b553195840a174b07bb551ebde57d`; clean child checkout. Child branch `kuthapar-microsoft-i-03-legacy-protection` targets `kuthapar-microsoft-net-assurance-gap-plan` in a separate stacked PR |
| Frozen upstream / history | Parent resolved upstream main once at I-03 intake to A-01's unchanged `52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`. No duplicate audit or new moving-main assessment. A-01 SHA-256 `63e368bc66f5c876bb853de62116a9d54ce34f9e1066b5f0b89da8df95b4949c`, 98,011 bytes; all earlier I-01/I-02 identities remain historical |
| Authorization / stop | User selected a separate I-03 worktree and stacked PR and requires focused unit plus emulator/authorized-isolated-live protection. Later authorization permits exactly one existing Windows/emulator PR-validation build after publication if automatic selection is absent, with current limits/fixtures. No other CI trigger/rerun/cancellation, I-04, evaluator/platform, new technique, review/fanout, permission/resource change, merge or release |

## Authority, lifecycle and the actual gap

[MSTest's public asynchronous test-method rule][mstest] independently supports
the discovery repair. This repository's adapter/framework remain 1.2.0 for
units and 1.3.2 for emulator tests; no newer `DiscoverInternals` facility,
package upgrade or public product enum is needed. The existing
[`MdeEncryptionProcessorTests`][mde-unit] supplies the direct prior art:
public `Task` methods take numeric processor data and resolve the internal
`JsonProcessor` inside the test.

The [public algorithm constant][algorithm] marks legacy
`AEAes256CbcHmacSha256Randomized` obsolete in favor of MDE, but
`VerifyIfSupportedAlgorithm`, [dispatch][dispatch] and the existing
[`LegacyEncryptionTests`][legacy-service] still implement/use it. Classification
is **deprecated-but-present compatibility protection**, not permission to
remove tests or supported behavior. `EncryptionOptions.EncryptionAlgorithm`
still has an outdated "only one supported" remark; it is not an independent
oracle for current MDE/legacy eligibility.

There is an important pre-existing boundary: [processor validation][validation]
explicitly rejects legacy encryption with `JsonProcessor.Stream` on .NET 8.
The existing unit
`EncryptionProcessorTests.Encrypt_LegacyAlgorithm_StreamProcessor_Throws`
already requires that rejection. Request-option legacy decryption may fall
back to Newtonsoft; a stream item API is not the Stream JSON processor.
The original four hidden methods nevertheless supply Stream to encryption
and then expect a successful round-trip. Preserve those inputs and expose
their original failures before correcting this unreachable test expectation.
Do not silently remove four rows, change product validation, classify them as
skipped, or claim four additional successful legacy Stream round-trips.

The [legacy processor][legacy-processor] removes selected present properties
into format-2 `_ei` encrypted data; it does not retain per-field MDE ciphertext.
A syntactically valid but nonexistent `/Invalid` path is ignored in the
document, not a malformed-path rejection. These source and existing-test
witnesses justify bounded regression protection, **not independently approved
.NET/service semantics, a public wire-format guarantee or a self-approved
contract change**. Q/oracle qualification and the authority for any future
intentional change remain open; preserve a reproduction if these sources,
public operations or independently qualified authority disagree.

The canonical concept repository was synchronized on clean `main` at
`bcfbc786fa5296deebcccd11369efb583e63e48c`.
[Chapter 6, Two Connectivity Modes][concept] distinguishes Gateway HTTPS
and Direct RNTBD, so a Gateway-only emulator cannot qualify the Direct cell.
Its numbered chapters contain no independent legacy-encryption contract;
concept grounding is not this increment's encryption oracle.

I-03's shared-policy input is
`kushagraThapar/SuicideSquad@447c660872ceaced30a5b5c60abec3bc5c54eb7b`:
[handoff v0.2.12, sections 8.12-8.13/10.12][policy], baseline/domain matrix
v0.1.12. All D-01..D-12 approaches are selected, zero unselected approaches,
not factual/oracle/tooling/operational/qualification closure. D-12 distinguishes
a wrong-test correction from product contract change and requires retained
history, replacement paired protection and no candidate self-waiver.
[kushagraThapar/SuicideSquad#10](https://github.com/kushagraThapar/SuicideSquad/pull/10)'s
original OPEN publication and later observed merge at
`2026-11-02T01:12:37Z` to main
`79fe48b9cb00896e1bb04095ae2fde684b480914` remain separate. D-11 source
`725724e4cb6f24c75191cb9c192a48b596366eec`,
[kushagraThapar/SuicideSquad#9](https://github.com/kushagraThapar/SuicideSquad/pull/9)'s original publication and
later integration `2e7d97f46ce2ee28cd1a6a707a596a6e33ab167e` likewise remain
separate. Earlier D-10-frozen receipts are not rewritten.

## Cases, assertions and paired mapping

Keep all four exact method identities. `JsonProcessors` supplies integer
`0` (Newtonsoft) on net6/net8 and integer `1` (Stream) additionally on net8.
`ResolveJsonProcessor` rejects undefined data rather than silently defaulting.

| Existing unit method | Newtonsoft positive/boundary protection | .NET 8 Stream protection | Real-service counterpart |
|---|---|---|---|
| `InvalidPathToEncrypt` | Configure `/SensitiveStr` plus absent `/Invalid`; encrypt only the existing string, preserve every untargeted field, exact configured metadata; decrypt exact original document with exactly `/SensitiveStr` in the context and no invented `Invalid` property | Require explicit unsupported-processor rejection before encryptor calls or input mutation | Existing normal legacy item witnesses plus new `EncryptionLegacyProcessorRoundTrip` missing-path rows, Gateway/Direct |
| `EncryptDecryptPropertyWithNullValue` | Explicit null string must remain a present JSON null after decrypt; other sensitive fields including integer/array/dictionary round-trip; complete path context | Same rejection using the null-valued input | New null rows, Gateway/Direct, with independent stored-format and exact decrypted observers |
| `ValidateEncryptDecryptDocument` | Remove every selected string/integer/array/dictionary from raw document, preserve id/PK/plain field; nonempty format-2 encrypted bytes, exact key/algorithm/configured paths; exact full decrypted document and context | Same rejection using the normal input | Reuse existing `EncryptionCreateItem` typed/stream witness; new normal rows add both transports, wider data and independent raw metadata/plaintext controls |
| `ValidateDecryptStream` | Preserve encrypted-input/decrypted-output path; seekable nonempty output rewound to zero and exact decrypted values/context | Same rejection before unsupported encryption; existing `Decrypt_StreamSelection_LegacyAlgorithm_FallsBackToNewtonsoft` remains a distinct positive decryption neighbor | Each new row uses a fresh public typed and stream read; net8 explicitly selects Stream on reads to exercise legacy fallback, not streaming encryption |

The common encryption observer must check all modeled fields: selected
properties absent, other properties unchanged, `_ei` an object with the
legacy algorithm, key id, format 2, nonempty encrypted data and exact configured
path set. For the missing-path seed, configured metadata and actual decrypted
paths are intentionally different. The decryption observer must require one
context entry, exact key and path set, no `_ei`, exact whole modeled document
and explicit null preservation. Lost/mutated non-target values, plaintext
leakage, wrong metadata, invented/missing paths and empty ciphertext fail.

Retain `DecryptStreamWithoutEncryptedProperty`'s seekability, zero position,
equal length and null context; strengthen exact plaintext and unchanged-stream
identity. Keep MDE format/null/path cases, algorithm-validation tests, legacy
Stream rejection/fallback/provided-output neighbors and I-02's cache and
diagnostic tests. Do not copy the MDE retained-field ciphertext observer into
legacy tests.

The signature-only intermediate candidate must run before assertion repair:
expected class outcome is five positive leaves on net6 and five positive /
four unsupported-processor failures on net8. Retain the actual TRX and
source-file identities; these original failures are evidence, not product
defects or a final acceptance result. The final expected class cohort is
**5 net6 leaves** (four Newtonsoft plus plaintext control) and **9 net8
leaves** (four Newtonsoft, four explicit Stream-rejection rows, plaintext
control). Four data-driven parents are not four/eight executed parameter rows.
These are bounded cohorts, never whole-suite coverage denominators.

### Real-emulator fixture and finite service cases

Use the existing Legacy class's unique database/key/item containers and test
KEK wrapper. Each new row uses a case-owned Gateway/Direct client, real
`CosmosDataEncryptionKeyProvider`, public real DEK creation and `CosmosEncryptor`.
No mocked encryptor, mocked item response or helper-synthesized success counts
as service proof. Fake KEK wrapping is an explicit existing fixture boundary,
not real Key Vault/credential/rotation coverage.

`EncryptionLegacyProcessorRoundTrip(useGateway, nullSensitive, includeMissingPath)`
declares exactly:

| Arguments | Transport / scenario | Expected per TFM |
|---|---|---|
| `(true,false,false)` | Gateway / normal | One leaf |
| `(true,true,false)` | Gateway / null string | One leaf |
| `(true,false,true)` | Gateway / missing configured property | One leaf |
| `(false,false,false)` | Direct / normal | One leaf |
| `(false,true,false)` | Direct / null string | One leaf |
| `(false,false,true)` | Direct / missing configured property | One leaf |

Each row must create using Newtonsoft, require Created, inspect the stored
item independently without the encryption wrapper, then require successful
fresh typed/stream/decryptable reads with all modeled values and exact context.
Include string, integer, array, dictionary and non-target fields. .NET 8 first
attempts the same public Create with Stream, requires `NotSupportedException`
and an independent NotFound read, then succeeds on the same provider/document
with Newtonsoft. That is a bounded pre-encryption rejection/no-item control,
not a general no-commit or no-network guarantee. .NET 8 reads select Stream
and must recover legacy values through fallback; net6 reads use Newtonsoft.
Six leaves per TFM each contain several assertions, not extra counted leaves.

Dispose new clients/providers/responses/streams. Existing class cleanup targets
its unique database but does not check the delete response; independent fixture,
cleanup/lease fencing and capacity/trust isolation stay unqualified. Do not
add sleeps, retries, production hooks, account provisioning or new resources
outside the already configured ordinary native fixture.

## Existing selection, artifact bindings and execution limits

I-02 already added the Custom unit invocation at
`templates/build-test.yml:69-80` and `templates/build-preview.yml:237-248`.
Preserve both TFMs, existing caller filters/configuration, `SdkProjectRef=false`,
the source-reference solution build and package-symbol parity build. The
existing Custom emulator task at `templates/build-test.yml:386-398` selects
the Legacy class; no new job, filter, gate or evaluator is needed.

Ordinary and preview package-backed runs both bind core
`3.41.0-preview.0`, not a GA-core comparison. Source-preview comparison uses
current core `3.64.0-preview.0`; default `SDKPROJECTREF` and
`DefineSdkProjectRefSymbol=false` compilation are distinct. Compiled output/
dependency identities do not qualify actual loaded native payloads or
installed consumers.

The tracked root `azure-pipelines.yml` has `trigger: none` and PR target
filters **main / releases/**, not this stacked feature-branch target.
Therefore an ordinary automatic native run is not promised for the stacked
PR. The initial automatic-only scope is retained in v0.1.0-v0.1.2.
The later user decision authorizes exactly one validation build in the existing
SDK Windows/emulator PR pipeline **after actual publication**, only if
automatic selection is absent, using its current limits and fixtures.
Inspect definition/source/branch/reason conditions before queueing so this
does not activate release/publishing. No second queue, rerun, cancellation,
selection/filter/resource/permission/release/merge change is authorized.
The one-run allowance is currently **unconsumed** because publication has
not succeeded. Do not borrow I-02's build 64334 results or retarget/modify
the parent to manufacture evidence.
Existing Custom emulator job limit is 60 minutes and task retry limit is 2;
the unit tasks declare no task retry. Preserve actual attempt histories and
finite inherited limits; do not introduce a new budget.

Local validation uses macOS ARM64, SDK 9.0.304, VSTest 17.14.1 and the already
verified session-private runtimes 6.0.36 / 8.0.33, explicitly selected through
`RunConfiguration.DotNetHostPath`. No global installation or roll-forward.
Use the smallest existing MSTest commands, then the impacted full Custom unit
project in ordinary/preview package mode and focused source-preview comparison:

```text
dotnet test <Custom-unit.csproj> --configuration Release --framework <net6.0|net8.0>
  -p:SdkProjectRef=false [-p:IsPreview=true]
  --filter "FullyQualifiedName~LegacyEncryptionProcessorTests"
  --results-directory <attempt> --logger "trx;LogFileName=<unique>.trx"
  -- RunConfiguration.DotNetHostPath=<verified-matching-private-runtime-host>
```

Wider units retain `TestCategory!=Quarantine&TestCategory!=Ignore`.
Compile both emulator TFMs and existing source-preview Debug/Optimize modes
without changing references or gates. No local capable Windows emulator is
available; compilation/listing is not service execution. The exposed tool
catalog has no relevant `ado` build/timeline/test reader; the Bluebird
connection targets service `msdata/CosmosDB/CosmosDB`, not this SDK. Existing
authenticated read-only Azure CLI/native receipt access may be used if an
ordinary build actually exists; never print/save tokens or change login.

## Receipt rules, baseline and completion checklist

The original runtime-less net6 listing aborted testhost discovery even though
the command returned exit code 0; retain that log and give it no discovery
credit. With explicit private hosts, both baseline class runs execute only
`DecryptStreamWithoutEncryptedProperty` (1/1 passed each) and report all four
UTA007 methods as absent, not passed/skipped. Baseline TRX SHA-256:
net6 `b6f7383faff66ed1169fae6d11bc6fd14349ea889803fbe7a69ce734759bc7f7`;
net8 `e398fece472a9fcdb8a68adf198795726bbed28be465ae051e228014b661db7d`.

Record expected rows, discovered parents, expanded executed leaves, passed,
failed, skipped, inconclusive and missing results separately, bound to actual
source/tree, commands/reference modes, host/runtime, attempt and original TRX
digest. Native source, actual merge candidate, build/run/task/attempt/subresult
identities and original logs are separate from a later documentation head.
Written/compiled/discovered/queued is not executed. No denominator reduction,
original-failure overwrite, whole-build-clean or paired closure claim follows
from an unavailable fixture, publication failure or empty native cohort.

### Actual local receipt

The v0.1.0 specification was frozen **before any test-source edit** at
`2026-11-02T01:43:09Z`, SHA-256
`fc42ebd194dae1603c98d433aeda49f46c1e34e7b3f7f9486c4c436d2688a8b4`,
Git blob `fbdae65da9b20b5559d8ee72608b8e38f790e6d8`.
It was subsequently persisted verbatim and immediately pushed in spec-only
commit `721e7b2f51239895bb99cafa0eb91ae6afd5d082`, tree
`2dc09c62a9c3e0255e24905cb9f6f19962bde575`; that publication time is not
misrepresented as the earlier freeze time.

The signature-only intermediate source file has SHA-256
`5c3776d6a3ef851b5f66d27dcf02ebb6ca77f99733f1817e646297e54fc29182`;
its retained patch has SHA-256
`6209139a20cc30c8a605b6e5b2b7be1d2d1029a5c732c90486e818aa843a5b96`.
Actual net6 outcome: 5 passed, TRX
`ce1ca06810de8ec7be9c14ca5ca197bb0dbfcaa0033f5c881ad3873168ee59dd`.
Actual net8: 5 passed / 4 failed, TRX
`89218c93d265d036f1d11d025c5288a4a5320ecccdd27f0ab5b0188f5cfa65ff`.
The four `(1)` rows fail with the existing
`NotSupportedException: JsonProcessor.Stream is not supported for AE AES encryption algorithm.`
Their names, inputs and original failures remain; the final rows now require
that explicit rejection, no encryptor calls and unchanged readable input.
No product behavior or row eligibility was changed.

Final focused package-backed class runs pass 5/5 net6 and 9/9 net8:
TRX SHA-256 respectively
`33eb7b59b807b1e2a74b295b7e685041d93413bf6887b96c0d3f950419a909e4`
and `dc0e2248512080b24338dec7fde7798d077c17e3f24b9c8db0115e88ed21718d`.
Listing separately discovers four data-driven parents plus the plaintext
control on each TFM. Execution expands them to four/eight parameter rows
plus that control, with no UTA007. No passing four-row Stream encryption
claim is made.

| Actual run / reference mode | TFM | Executed / passed terminal leaves | Original TRX SHA-256 |
|---|---|---|---|
| Ordinary full Custom units; package | net6 | 428 / 428 | `5440c1ba3b273ce5ffd232bfbf24383af08bfdccf5b1726228f3ed7b8592ddf6` |
| Ordinary full Custom units; package | net8 | 763 / 763 | `225603e1e4e3daf8a5c8a88215644d62708d009843a132acba227dd13b9dbce2` |
| Preview full Custom units; package | net6 | 428 / 428 | `e0a368aa405d0307bb1d8e978a2d81855aa254b5e7bdd3e769fcb6067d175558` |
| Preview full Custom units; package | net8 | 763 / 763 | `55a0b26d3d28d1e9b22104a431a97333d7af62cfa639f9c0cf3439ec3c20f173` |
| Focused source preview; current core | net6 | 14 / 14 | `d51ecc42c2c1fd1272105732f1130e2310c21892f519039f56932921059a7d96` |
| Focused source preview; current core | net8 | 41 / 41 | `f481ad0379490cd68a7dac9ec1aa6db20c17da079b9736972ed7a82af5a451ce` |

These are actual Release/macOS ARM64 executions using the explicit private
hosts above, first final-candidate attempts, zero failed/skipped/inconclusive
leaves. Raw result and terminal counts agree for these unit TRX files.
Each full package variant contains the exact five/nine Legacy leaves,
13 cache, 39 diagnostics and 8/24 MDE leaves, all passed. Source focus is
`FullyQualifiedName~EncryptionProcessorTests|FullyQualifiedName~EncryptionOptionsExtensionsTests`:
Legacy 5/9, MDE 8/24, validation 1/1 and seven net8 dispatcher neighbors,
including the original rejection, fallback and provided-output boundary.
Full-project growth from I-02's historical 424/755 is exactly the four/eight
newly executable rows, not new SDK behavior coverage or a denominator claim.

An introduced service-test compiler error is retained, not hidden:
the first net8 package build reports two `CS0200` errors because
`RequestOptions.Properties` exposes a read-only dictionary indexer.
Original log SHA-256
`d62df3fee5481b1a99517adf18c6478a41e90f661bc58adb717dfe536d58cbbf`.
The test now retains its strongly typed `Dictionary<string, object>` local
and mutates that local while preserving the original option reference.
No cast, API, dependency, assertion, processor row or build gate changed.
The repaired package Release build succeeds for both emulator TFMs with zero
warnings/errors, log SHA-256
`d1de2822a18db7c8a128d142e4771ea184aa387a5c2651c267b4ffc0520c0de5`.
Both source-preview Debug/Optimize emulator builds also succeed on both TFMs
with `DefineSdkProjectRefSymbol=true` and `false`, zero warnings/errors.
These are bounded local compilation comparisons, not execution of either
native solution/parity task.

The two source files at these local receipts have Git blob identities:
unit `2b779aacf9fc6fda2f90b26a3ffcd938bb94693a`;
emulator `3d0a17d8cf43338f3a8f1d2d9a0d305b40462ba9`.
Package dependency outputs identify core `3.41.0-preview.0`;
source outputs identify core `3.64.0-preview.0` as a project. Both identify
Custom `1.1.0-preview01` and cryptography `2.0.0-pre024`; no dependency range
was changed. Output hashes/dependency manifests are retained separately,
not asserted to be full loaded/native/installed-consumer attestation.

Each package emulator assembly lists the **one** new data-driven parent.
The six declared data rows per TFM have **not executed locally**; neither
listing nor compilation initializes a service fixture or supplies twelve
passed cells. These local runs supply no native build/run/attempt/TRX or
actual native merge-candidate identity; the separate publication and target
selection disposition follows below.

### Publication / native disposition

The implementation and local result update were committed and immediately
pushed as `3748a6a873f08cb9a5bfb71fb2d1ab581faa6d77`, tree
`8f2e7bc9903cd8a471e755232a55cf7c6904a29a`; both test-file blobs match
the actual local executions/compilations above. Those executions occurred
before the commit; documentation publication is not a new test execution.
Origin's parent branch still resolves to the original
`7330088136240c8586ed5eea63f2a4f9a0c366e5`.

The standard linked-account `create_pull_request` attempt for that head and
explicit base `kuthapar-microsoft-net-assurance-gap-plan` was rejected:
**403 Forbidden: "Unauthorized: As an Enterprise Managed User, you cannot
access this content"**, request ID `215C:173B74:15F9:25A2:6AE7ED51`.
Read-only verification at `2026-11-02T01:55:05Z` found **no matching PR**,
zero head check runs and zero commit status entries. The API's aggregate
`pending` state with zero entries is not a queued CI run. At that initial
receipt, no retry, global
login/account/permission change, alternate publisher credential, or PR6182
personal-publisher exception was used.

On resumption, the user reported: "I fixed the account to be personal account
on dotnet repository." The standard app publication route was retried for
receipt head `9697fa53f1602f04a93f26f48bfd9e33bee5a457`, tree
`0e35833e8852bbd70e2f46369f3622c337dea216`, with the same explicit stacked
base and unchanged validated test blobs. It again returned **403 Forbidden:
"Unauthorized: As an Enterprise Managed User, you cannot access this
content"**, new request ID `CDF6:3B1635:81A46:BB23F:6AE7F087`.
A duplicate check at `2026-11-02T02:08:21Z` found no matching PR.
This proves the retry's failure, not which repository/session account
selection the app currently stores or why the corrected link was not used.
Supported repository/session metadata does not expose that selection.
Direct app inspection was rejected by the computer-use server's built-in
safety policy; no internal-file/credential or shell-UI workaround was used.

The separately relayed user choice was:
"Authorize one I-03 validation build if automatic CI does not select it
(Recommended)". Its question explicitly conditions that one existing
Windows/emulator pipeline run on a **published** candidate and preserves
current limits/fixtures, filters, resources, permissions, releases and merge
settings. No run was queued before publication, and no allowance has been
consumed. A read-only native definition check identifies existing
`dotnet-v3-ci`, definition 63 revision 104, YAML `azure-pipelines.yml`,
GitHub repository `Azure/azure-cosmos-dotnet-v3`, enabled existing hosted
queue. Its stored default/trigger branch metadata still names master;
that metadata is not evidence that the requested stack is selected or that
a manual run would have an appropriate reason/merge identity. No release
definition, source/configuration override or queue request was submitted.

There is **no new PR URL**, actual PR/native merge candidate, native build,
task attempt, service run or TRX identity to report. Standard publication
requires the corrected personal-account binding to reach the standard
publication tool, or separately explicit publisher-route authorization.
The exact cause/account binding is not exposed by the available tools;
the failed retry is not bypassed with PR6182's personal credential exception.
After actual publication, observe automatic selection and only then, if
absent, use the unconsumed one-build allowance subject to the safety checks
above. Publication/fixture boundaries are not waived by compilation, local
units, a successful SSH push, an empty status response or I-02's build
64334. All twelve declared service cells
remain unexecuted, not MSTest-skipped/passed cells. Stop here: no I-04,
M-01 evaluator/CI implementation, review/fanout or new operational action.

- [x] Freeze the pre-edit specification identity.
- [x] Capture signature-only discovery/execution and original Stream failures.
- [x] Implement all four discoverable, meaningfully asserted legacy cases and controls.
- [x] Add only the genuinely missing real-provider/emulator observer cells.
- [x] Record local actual execution and compilation/parity results.
- [ ] Publish a separate stacked PR through the standard linked account: the corrected-account retry still receives the recorded 403/Enterprise Managed User restriction.
- [x] Observe applicable automatic native evidence or record its exact blocker: publication is still blocked; the conditional single-build allowance remains unconsumed and no execution is credited.
- [x] Update plan/matrix without relabeling earlier results or closing other gaps.

The standard linked-account publication route is required. Existing SSH
dry-run push succeeded; the current CLI account `kuthapar_microsoft` reports
pull=true/push=false. PR6182's personal publisher exception is not inherited.
A precise publication/access blocker must remain visible; no account/token/
permission/global-login substitution is authorized.

[mstest]: https://github.com/dotnet/docs/blob/9b3cf83bd33efaae3099a5a151a4ce1d4626ec21/docs/core/testing/unit-testing-mstest-writing-tests.md#testmethodattribute
[mde-unit]: ../../../Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/MdeEncryptionProcessorTests.cs
[algorithm]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/src/CosmosEncryptionAlgorithm.cs
[dispatch]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/src/EncryptionProcessor.cs#L260-L307
[validation]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/src/EncryptionOptionsExtensions.cs#L40-L50
[legacy-processor]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/src/AeadAes/AeAesEncryptionProcessor.cs
[legacy-service]: ../../../Microsoft.Azure.Cosmos.Encryption.Custom/tests/EmulatorTests/LegacyEncryptionTests.cs
[concept]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/06-transport-layer-rntbd.md#two-connectivity-modes
[policy]: https://github.com/kushagraThapar/SuicideSquad/blob/447c660872ceaced30a5b5c60abec3bc5c54eb7b/docs/dotnet/design-handoff.md#812-d-12-intentional-changes-exceptions-and-obligation-retirement
