# I-01: paired Encryption patch protection and existing unit selection

[Remediation plan](../remediation-plan.md) |
[Behavior matrix](../feature-behavior-test-matrix.md)

| Field | Spec receipt |
|---|---|
| Version / written | 0.1.3 / 2026-11-01 UTC; original v0.1.0 preceded test/pipeline edits; v0.1.1 preceded preservation-case edits; v0.1.2 recorded results; v0.1.3 records publishing/local-host continuation |
| Status | Bounded tests/CI selection implemented; local unit receipts obtained; integration execution blocked |
| Evidence / behavior | A-01 G-02, W-17, B-034; bounded G-05 observer and G-07 receipt work |
| Base / intake main | `52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`; main read once for implementation intake, `2026-11-01T18:40:10Z`, unchanged from A-01 |
| Historical inputs | A-01 digest and policy pins in the plan remain frozen; delivered plan/matrix v0.1.0 remain historical |
| Pre-implementation spec | v0.1.0 SHA-256 `6e754285dd82c70dcc07a979bd65365274c849e43f4538b58b17b4b7ae61c587`, recorded `2026-11-01T18:47:15Z`; immutable local snapshot retained |
| Scope | Encryption unit selection and root encrypted/nonencrypted Increment; no production, Custom, Direct-migration or Java changes |

## Authorization and authority

The user's new request explicitly replaces the documentation-only stop boundary:

> Sounds good, based on the .NET assurance gaps, let's start the work on the .NET SDK repository to improve those gaps through extensive testing now. For a gap, we should aim for both a unit test (mocking if it demands), and an emulator or a live test that covers the gap. For the existing tests as well, we should have a unit test with mocking if required, and a live test that runs against emulator or prod account. Let's start this work in the .NET assurance gap remediation agent.

Each assessed behavior needs a focused unit witness and a meaningful emulator OR explicitly authorized isolated real-service witness. Sufficient existing tests count. Written, compiled, selected and passed are different states, recorded independently per layer. A mock request/writer is not the service half. A local harness rule has no meaningful service assertion by itself: **proposed treatment for review** is native project/test-result selection proof plus the paired service behavior selected by that harness, not a fabricated live YAML test. G-02 cannot be closed merely because its unit task is added.

Operation-object reference identity is also genuinely local. The proposed treatment is the exact CLR identity/order assertion plus independently meaningful service-side value/state preservation, not a claim that a service observes CLR object identity. This treatment is surfaced for review, not silently waived. The whole-document observer here means all modeled `TestDoc` payload fields plus id/PK and the separately checked ETag; unknown JSON/system-property and polymorphic representation closure remains backlog.

D-05's inventory/atomic-contract/independent-oracle approach is approved by the user's subsequent decision. The published receipt is [shared handoff v0.2.5 section 10.5](https://github.com/kushagraThapar/SuicideSquad/blob/532e2401be02da06becbbc459dd58b78ae9b35d4/docs/dotnet/design-handoff.md#105-user-approved-d-05-approach); [section 11.1](https://github.com/kushagraThapar/SuicideSquad/blob/532e2401be02da06becbbc459dd58b78ae9b35d4/docs/dotnet/design-handoff.md#111-separate-sdk-testing-authorization-context) separately records this testing authorization. Baseline/domain matrix are v0.1.5 at the same commit. This does not approve every B-row or resolve specific .NET/service oracles. D-06 through D-12 remain open.

The existing unit tests and emulator test agree on the rejection of encrypted-path Increment. This increment preserves and strengthens that existing expectation; it does not promote it into a newly approved independent contract. The path-specific exception text is a regression witness, not an approved universal error-message guarantee. Independent API/history authority and lifecycle rationale remain open under D-05/G-10. Consult the [canonical transport modes](https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/06-transport-layer-rntbd.md#two-connectivity-modes) for scope, not as a current .NET/service oracle.

## Protection gap and intended observable slice

At the frozen base, [`EncryptionContainerPatchTests`](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/EncryptionContainerPatchTests.cs#L38-L75) has two local root-path assertions but no explicit companion-unit invocation in A-01's inspected CI.

The existing [`EncryptionPatchItem`](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption/tests/EmulatorTests/MdeEncryptionTests.cs#L2815-L2941) exercises real-service patch/decryption, including plain Increment. Its final `try/catch` checks the message only if the exception occurs; successful completion can bypass that assertion. This is a confirmed **test observer defect**, not a demonstrated SDK functional defect.

Preserve these narrow observations:

| Cell | Intended assertion and independent authority status |
|---|---|
| Root encrypted Increment | Require `InvalidOperationException` and the encrypted path; existing assertion evidence, independent contract approval unresolved |
| Mixed operation ordering | Reject whether encrypted Increment is first or last; preserve the caller's operation objects/order; never forward a prefix as a successful patch |
| Plain Increment | Keep the original unit operation objects/order; real service returns OK and the fresh item read has the exact expected increment |
| Public API / service state | Typed and stream entry points; unchanged item and nonempty unchanged ETag after rejected validation; no logical Patch request reaches the observing handler |
| Observer negative control | A subsequent valid plain patch must reach that same forwarding handler once and change the observed value/ETag, preventing an inactive observer from looking like successful zero-send proof |

The unchanged-state assertion concerns **known client-side validation**, before the observed public SDK Patch dispatch. It is not a timeout/cancellation/lost-response no-commit guarantee. A request-handler count observes logical pipeline entries, not all physical Gateway or RNTBD attempts.

## Paired test plan, cases and preservation controls

| Layer / method | Proposed cases and exact observers | Preservation neighbors |
|---|---|---|
| Unit: `EncryptPatchOperationsAsync_Increment_On_Encrypted_Path_Throws` | Three data rows: retain the original single operation, plus encrypted-first/last mixed lists. Required exception/path; original list length, order and reference identity retained | Plain operation not rewritten or applied as a prefix; original single-operation seed not retired |
| Unit: `EncryptPatchOperationsAsync_Increment_On_NonEncrypted_Path_Passes_Through` | Two data rows: retain the original single operation, plus two plain Increment operations; exact returned count/order/reference identity and Increment types | Serialization/value identity preserved by keeping the original immutable operation objects; original single-operation seed not retired |
| Existing service: `EncryptionPatchItem` | Replace optional catch with required exception; retain all existing encrypted Add/Replace/Set/Remove/plain-Increment and diagnostics checks | Existing broad patch/representation sequence is not replaced |
| New service: `EncryptionPatchItemIncrement` | Eight rows: Gateway/Direct x typed/stream x encrypted-first/last. Create a real encrypted item, require rejection/path, handler Patch count 0, fresh-read exact whole document and unchanged nonempty ETag. Perform a valid plain Increment, require OK/exact result, handler count 1, fresh-read exact whole document and changed nonempty ETag | Encrypted values, encrypted id/PK, unrelated plain fields, input ordering, response disposal, follow-up usable client/service |

Use the established Mde fixture's unique database/containers, CE policy v2, client encryption keys and cleanup. Its fake KEK resolver controls key wrapping only; do not replace Cosmos SDK data transport or the service. The counting request handler must always forward real requests. Typed/stream responses remain separately observed; stream responses are disposed.

Nested encrypted paths, floating-point numeric variants, patch filters/limits/batch, custom serialization, lost-response replay, real Key Vault permissions/rotation and other companion tests remain unexpanded counterpart backlog. Existing broad tests are not counted as complete paired assurance for those neighbors. No source/test assertion is weakened for a green run.

## Native CI changes and artifact boundaries

Add explicit Encryption unit execution to the existing ordinary `Microsoft.Azure.Cosmos.Tests` job in `templates/build-test.yml`, conditional on existing `IncludeEncryption`. Add preview Encryption unit execution to the existing Encryption preview/parity job in `templates/build-preview.yml`. Reuse installed runtime 6/SDK 8, MSTest, Moq and `DotNetCoreCLI@2` native result publication; retain caller arguments, exclusions, OS and configuration. If the generic caller leaves configuration empty, make the existing dotnet default `Debug` explicit rather than inventing a new Release guarantee.

The new unit invocations use the established default **published SDK reference** (`SdkProjectRef=false`); the preview task explicitly sets `IsPreview=true`. They pair with the unchanged ordinary and preview Encryption emulator reference modes, not with a sibling pack artifact. Existing source and package-symbol compile parity remains untouched. Candidate companion code is built from this worktree; resolved core SDK/native dependencies must be recorded separately.

Do not replace existing emulator tasks, reduce their filters, enable the nightly's disabled emulator, alter retries/timeouts, trigger CI, or infer effective OneES host/required-check policy from a label. The new eight-row test is selected by the existing Encryption emulator project tasks. Its actual CI discovery/execution and Windows runtime receipts are pending. No new automatic retry is added to unit tasks; existing task retries remain as configured and their first-attempt CI receipts still need G-07 work.

Local current-core source-reference checks may be recorded as a distinct comparison, with `SdkProjectRef=true` and `DefineSdkProjectRefSymbol=false`. A passing published-reference unit run is not current-core or built-nupkg consumer proof. Broader current-core paired selection, Custom net6/net8 selection/counterparts and artifact identity remain G-02/G-03 follow-up.

## Validation, finite attempts and stopping criteria

Before changes, targeted baseline attempt 01 restored and compiled, then aborted because the ARM64 .NET 6 runtime was missing. No executed/discovered case cohort was established. The original TRX is retained. Install only a private session runtime after this demonstrated failure; do not modify the shared installation or silently roll forward to .NET 9.

Baseline attempt 02 used .NET 6.0.36 through VSTest's explicit `RunConfiguration.DotNetHostPath`: expected 2, result leaves 2, executed/passed 2, failed/skipped 0. It is a pre-change unit receipt, not a post-change or integration result. Subsequent changes/configurations receive distinct attempt/result identifiers; no unbounded retry loop.

Record the exact project, filter, configuration, TFM, SDK/runtime/architecture, source/package properties, resolved/loaded assembly identities, expected/discovered/executed/pass/fail/skip/inconclusive leaves, original failure and TRX/assembly digests. Discovery and execution are separate receipts; a result total does not prove completeness outside its named cohort. Raw local results stay in local session artifacts; the SDK docs retain a portable result/configuration summary. Native CI artifacts remain the destination when CI actually runs.

| Required acceptance | Current stopping boundary |
|---|---|
| Versioned spec before test/pipeline edits | This v0.1.0 spec |
| Unit assertions compile and targeted leaves execute with exact cohort | Five exact patch leaves passed in each of the four bindings below |
| Existing-suite preservation where local execution is meaningful | Final published-reference GA and preview runs each passed 58 result leaves, including the five patch cases; not a whole-SDK/counterpart census |
| Emulator test compiles and eight exact cases are discoverable | All four bindings compiled cleanly. Native discovery listed one parameterized method, not eight expanded leaves; expanded-cohort acceptance remains pending |
| Both modes/API/order cells execute against an authorized capable service | BLOCKED: no local emulator listener and no authorized isolated live account supplied |
| CI selection/configuration/publication behaves as intended | YAML parsed and a structural comparison verified only the two new unit tasks; evaluated OneES/ADO/native result-publication execution remains unavailable |
| No overclaim | G-02 partial; B-034 remains unclosed until both layers, authority and candidate/configuration receipts are sufficient |

For integration execution, supply a reachable authorized Windows Cosmos DB Emulator host with CE policy v2/client-key/Patch support and both Gateway/Direct connectivity, the project's .NET 6 testhost/configuration, and isolated fixture cleanup permission. Alternatively supply an explicitly authorized isolated SDK test account/namespace with those capabilities. Do not infer authorization from existing settings or the user's generic reference to a production account. No cloud provisioning, permissions, arbitrary production/customer data, release/publish lanes, merge or governance changes.

No relevant `ado` read tool was exposed when checked; the prior A-01 REST receipt was HTTP 401. The existing build 64312 failure remains undiagnosed and is not rerun. This access boundary does not block independent unit/test improvements. If a test reveals a product defect or disputed expectation, preserve the reproduction and raise a separate fix/contract question; do not change production or relax the assertion within this increment.

## Actual local result/configuration receipt

**Conclusion:** testing/CI-selection changes are implemented and locally unit-validated; **the paired service slice and G-02 remain unclosed**. No integration execution was attempted without an authorized capable host/account. No product defect was established by these runs.

All final runs used Release/net6.0, macOS ARM64, SDK 9.0.304 / MSBuild 17.14.16 / VSTest 17.14.1, and the private ARM64 runtime 6.0.36 through explicit `RunConfiguration.DotNetHostPath`. The preview diagnostic log confirms that executable/testhost launch. This is not Windows/OneES/SDK-8 validation or .NET-9 runtime roll-forward. Runtime acquisition used the official [6.0.36 ARM64 runtime archive](https://builds.dotnet.microsoft.com/dotnet/Runtime/6.0.36/dotnet-runtime-6.0.36-osx-arm64.tar.gz), verified SHA-512 `d13fa060270c376307c7086da52cff71a02f8550a3a21812e25127308970c99a32d3c4834e342f23365869600c0b51fce03236906e13fe76456fc78964c531d4`; the shared installation was not changed.

### Final unit cohort and native artifact receipts

The independently enumerated patch cohort is five cases: rejected single `(false,true)`, rejected encrypted-first `(true,true)`, rejected encrypted-last `(true,false)`, accepted single `(false)`, accepted two-operation `(true)`. Boolean tuples follow the method parameters above. Native discovery lists the two methods without expanded data rows; actual TRX leaves establish those five executed outcomes. All final runs have failed/skipped/inconclusive/not-executed counts zero.

| Final attempt | Binding / exact command distinctions | Expected/discovered/executed evidence | TRX SHA-256 |
|---|---|---|---|
| `ga-preservation-attempt-01` | `IsPreview=false`, `SdkProjectRef=false`, `DefineSdkProjectRefSymbol=false`; entire Encryption unit project, empty filter; core NuGet 3.51.0, companion source 2.0.5 | Expected patch cases 5, exact five leaves passed; full requested project yielded 58 executed/passed leaves. Full obligation census not established | `dd6610cf657a7c63a026bc37769ab1ea5184374325b9270b10a163fef8331469` |
| `preview-preservation-attempt-01` | `IsPreview=true`, `SdkProjectRef=false`, `DefineSdkProjectRefSymbol=false`; same full project/empty filter; core NuGet 3.52.0-preview.0, companion source 2.1.0-preview5 | Expected patch cases 5, exact five leaves passed; full requested project yielded 58 executed/passed leaves. Expanded pre-execution discovery was not independently obtained | `ca0d8b6c325d0622628a55f8ec808fcdab65479bd6fdc47f281419ecd977e843` |
| `source-ga-preservation-attempt-01` | `IsPreview=false`, `SdkProjectRef=true`, `DefineSdkProjectRefSymbol=false`; `FullyQualifiedName~EncryptionContainerPatchTests`; current core source 3.63.0, companion source 2.0.5 | Expected 5, exact result/executed/passed leaves 5; broader current-source suite not run | `07445d360763aa3107cf2e936bcc83978d94e18dd6fe16d68633b7c330ba4ff5` |
| `source-preview-preservation-attempt-01` | `IsPreview=true`, `SdkProjectRef=true`, `DefineSdkProjectRefSymbol=false`; same patch filter; current core source 3.64.0-preview.0, companion source 2.1.0-preview5 | Expected 5, exact result/executed/passed leaves 5; native discovery separately lists two methods, not five expanded cases | `1d82d60bae8025e42a964cb4998b585a5f26fde599fb5a423438075412b1682f` |

Each filename is `encryption-<variant>-preservation-attempt-01.trx` in its named attempt record. Raw local TRX/test logs, dependency bindings and `assemblies.sha256` receipts are retained in session artifacts, not committed as source or relabeled CI artifacts. `deps.json` and output hashes identify the selected bindings; exact loaded native payloads and installed built-nupkg consumer proof remain G-03 unknowns.

| Binding | Test assembly SHA-256 | Core Client output SHA-256 | Companion output SHA-256 |
|---|---|---|---|
| GA package | `3cb779958d89f84ce0686c569a4e4530b7aebd2cd41b26f1bd8566bce8a431ef` | `959c5af29eb02c5583a5e424568603faf2af5c6a395b064f20acde9b82134580` | `75a6d8253e0545879af5697fd2ac7852cc15e21f99c219bdf6edeed790104201` |
| Preview package | `6d15772f2e6ea733b559a7e16c46a7823190cf83b80be7a9363c7e1dd40a31c8` | `8e2c549ae23b77375e9732fa9b7ebbc21c4df999bcf379e1a7c6795da7e1af7a` | `c5c54a77677f80c5e26b91aab46985a7898911032e118cfdf7487f6517ca75fe` |
| GA source | `b045cee11efb3c9515199a23804694636beb58c916842a724b0d9c3ce0e13c04` | `00cb04ba19611a8a19c603831052b67c08a1997c8708c0d6d611253a331042b3` | `53b05f2f0cbdaab0ca95ef58413de684de7ce3e30f5bf24d573e3f93201690f7` |
| Preview source | `6a6c47d343d295a7ac4acaa2136b151418b580067ba3a74e9b5c3f196f9078cd` | `13f441f0ad093f8b9124850e267344050f70586733f8377ce2fd5a995ea84db7` | `5e17c6c21180872081cc569d84c077acb263f8d0f978e3174d07fbdfa69c64f2` |

### Integration compilation versus actual service execution

| Build receipt | Emulator test assembly SHA-256 | Actual result and execution boundary |
|---|---|---|
| GA package, `integration-build-attempt-02` | `3d644984c993a3524246fb70b07e694285dd6833641f5bb3190cf0ab673694f4` | Build succeeded, zero warnings/errors; native discovery lists `EncryptionPatchItemIncrement` only |
| Preview package, `integration-preview-build-attempt-01` | `185645940dab448890a987efbaa99f979e46efb8aaa7237c6272546c3d46f576` | Build succeeded, zero warnings/errors; no service run or expanded-leaf receipt |
| GA source, `integration-source-ga-build-attempt-01` | `2d795d846d329afd0f53619872c7433328b955d1ff45d1cb6ae5f7cbbb33cbb7` | Build succeeded, zero warnings/errors; no service run or expanded-leaf receipt |
| Preview source, `integration-source-preview-build-attempt-01` | `3043afabd5c73bcc7e028a6934882aaa1ec50540006265ca1364d5ab48dab764` | Build succeeded, zero warnings/errors; no service run or expanded-leaf receipt |

The eight service data-row declarations are compiled; this is **not eight discovered/executed/passed tests**. Account/emulator version, actual topology/consistency/capabilities, loaded service/native payload, expanded required cohort and per-case TRX/first-attempt CI receipts remain absent. The existing `EncryptionPatchItem` observer correction is compiled, not claimed passed against a service.

### Retained first failures and intermediate candidates

| Receipt | Original result / retained identity | Disposition |
|---|---|---|
| Baseline attempt 01 | Missing ARM64 `Microsoft.NETCore.App 6.0.0` testhost abort after successful restore/build; TRX SHA-256 `a2ccdc53e7743af87f0d23ff2a2a3f38f7042985bf3e9cd50f6f244232470b74` | TRX counters 0 describe an aborted run, not an empty expected cohort or green result; preserve original failure |
| Baseline attempt 02 | Original two unit cases passed; TRX SHA-256 `aefee5a943292524ad038b58922cde00edec8911afb0de639c5f9b05c722dbbe` | Explicit private runtime resolved the demonstrated prerequisite; no automatic retry/roll-forward |
| Initial integration build attempt 01 | `CS8030` at `MdeEncryptionTests.cs`: a block lambda returned a client although the established helper accepts `Action<CosmosClientBuilder>`; diagnostic retained in the original command result | Our introduced compile error was fixed by configuring the builder and letting the existing helper build once; second build passed. Not a pre-existing SDK defect |
| Intermediate unit candidate | Targeted GA 3/3; full GA and preview each 56/56; current-source GA/preview each 3/3, before restoring explicit original single-operation seeds | All original TRX retained. These earlier counts/artifacts are not substituted for the final five-case/58-case receipts |

The final preservation runs are a new finite candidate/configuration pass, not a retry hiding the original failures. No repository CI, nightly, benchmark, coverage, mutation, fuzz, fault-injection or live-account campaign was triggered.

## Changed files and remaining prerequisites

| File | Bounded change |
|---|---|
| `Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/EncryptionContainerPatchTests.cs` | Preserve original single-operation seeds and add mixed ordering/multi-operation identity checks; final content SHA-256 `c1b0a6c1ac4a7a98fc6cee5c7c104a479fe7a995f0473c43f6b1ffde22edb8c7` |
| `Microsoft.Azure.Cosmos.Encryption/tests/EmulatorTests/MdeEncryptionTests.cs` | Required existing exception plus eight real SDK/service cases and forwarding handler; content SHA-256 `b90b9caf0b972cfd4037facaa092f82890268a8a60286f7d9f8b30024812aab3` |
| `templates/build-test.yml` | Explicit Encryption units within existing job/IncludeEncryption; content SHA-256 `d0338c64c1425781ca8539ae86d6f8d67018653a57e00063358809f789b55b2d` |
| `templates/build-preview.yml` | Explicit package-backed preview Encryption units within existing parity job; content SHA-256 `53829707f50170f3c5d365dddc05097eab36158a107cfa7ea969e8371e795ef2` |
| `docs/builds-and-pipelines.md` | Document the new existing-job selection/reference/configuration boundaries and unresolved assurance-docs wiring |
| `docs/assurance/remediation-plan.md` and `feature-behavior-test-matrix.md` | Authorization, current shared-policy receipt, paired-layer mappings/status and retained whole-SDK counterpart backlog |
| This spec | Pre-implementation intent and versioned local results/failures/remaining inputs |

No shipped-package source, API, dependency version or production behavior changed; no changelog entry is required for this test/docs/CI-only increment. Actual candidate commit/merge-candidate and CI result identity are not supplied by the file/assembly hashes.

The next input for this slice is an authorized CE-policy-v2/Patch-capable Windows emulator host with both data transports and .NET 6 testhost, or an explicitly authorized isolated SDK service account/namespace with equivalent capabilities and fixture cleanup permission. Native ADO/evaluated OneES/Windows-SDK-8 receipts and expanded case outcomes remain separate. G-02 Custom selection/paired cache counterparts, broader current-core paired CI selection, G-03 payload/consumer identity, G-07 attempt/cohort records, G-08 fixture identity and actual D-05 oracle closure remain backlog. Do not mark this slice closed or autonomous-eligible while those applicable receipts are missing.

**Historical publishing receipt (v0.1.2):** no commit, push or PR had been made. The current Homebrew `gh` account reported `pull=true`, `push=false` for the upstream repository. A user-owned-fork search returned HTTP 422 validation failure (not an authentication-expiry diagnosis); no matching SDK fork was returned in the inspected first page of 100 recently updated owned repositories. No permission, account, remote or governance change was made. The delivered v0.1.2 spec SHA-256 `0048ad2641f9ad96e7095ebfbabdc4698c58f9e23186570c0432eb463fa5cc63` remains historical.

**Publishing continuation:** an explicit `git push --dry-run` to the current feature ref through the existing origin/SSH credential succeeded, reporting a permitted new branch without updating any remote ref. The Homebrew CLI account's `push=false` is not evidence that every configured Git credential lacks write access. This corrects the earlier publishing-block inference without changing accounts, permissions, remotes or governance. Use that verified existing route, the dedicated worktree branch, immediate commit/push and the repository's root `PULL_REQUEST_TEMPLATE.md`. Actual commit/PR/CI receipts are recorded separately when produced; the dry run is not a pushed candidate or executed test.

**Local emulator continuation:** Docker is not installed; Podman is installed, but its existing local ARM64 machine is stopped. No machine/container was started, image acquired or machine configuration changed. The [official Linux vNext emulator support table](https://learn.microsoft.com/azure/cosmos-db/emulator-linux#feature-support) declares Gateway-only operation and client-encryption-key endpoints as no-ops. That ARM64 alternative is not faithful proof for the required Direct/CE-policy-v2 fixture; do not substitute it or downgrade the eight-cell requirement. This is a bounded compatibility check, not an exhaustive emulator census or a diagnosed SDK defect. The established Windows-emulator CI lane remains a candidate execution route; actual fixture and expanded case receipts are still required.
