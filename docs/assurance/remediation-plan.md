# Proposed .NET SDK assurance-gap remediation plan

[Initial feature/behavior-to-test matrix](feature-behavior-test-matrix.md) |
[Existing SDK CI guide](../builds-and-pipelines.md) |
[Current shared design handoff][current-policy]

| Field | Draft receipt |
|---|---|
| Version / recorded | 0.2.3 / 2026-11-01 UTC |
| Status | Evolving proposed plan with authorized bounded testing increments; not an approved contract catalog, whole-SDK executed assessment or merge recommendation |
| Scope | All repository SDK source and companion packages; Gateway and Direct; improve established SDK tests and CI |
| Starting evidence | Completed A-01 reconciliation audit, identified below; no duplicate SDK audit |
| Current source assessment | Upstream `main` resolved once to `52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`, equal to A-01 |
| Layout | `docs/assurance/` is a provisional SDK-docs layout for these two drafts, not a globally selected schema or retention policy |
| Authorization | User authorized paired unit plus emulator/isolated-service testing improvements; no arbitrary production access, cloud provisioning, merge or release authority |

### Revision and authorization receipt

The delivered v0.1.0 documentation-only snapshots remain historical: plan SHA-256 `405c8acda6dec4d2436259f636443e8fafac5bffa19749313fbbbc40953143a1`, matrix SHA-256 `9649d2f7face0e452ed46eaeac1ec22d7bfb7ff9c5a13cf6446db42d889a2600`. Their original scope and A-01 provenance are not relabeled.

The user's subsequent request to start extensive testing explicitly replaces that stop boundary. Every gap and existing behavior/test assessed in this effort needs a focused unit test (mocking where useful) and meaningful emulator OR explicitly authorized isolated real-service counterpart. Sufficient existing tests count; do not manufacture duplicates, call a mock the service half, or silently waive local/unsupported capability cells. Record proposed treatment for such exceptions before claiming closure. The whole-SDK counterpart backlog remains, not just newly written tests.

D-05's evidence-backed inventory/atomic-contract/independent-oracle **approach** was approved and published in [handoff v0.2.5 section 10.5][implementation-d05] at `532e2401be02da06becbbc459dd58b78ae9b35d4`. [Section 11.1][implementation-authorization] records the separate testing authorization; baseline/domain matrix were v0.1.5 at that receipt. D-06's [v0.2.6 receipt][implementation-d06] at `72e9f57918ed81a38bd750f4a6aca58173877d7b` records the protected/fail-closed future unattended-merge approach DESIGN ONLY.

The current [handoff v0.2.7][current-policy] at `1fef335ecb72e15e75518cffc2d39814c2e6bd13`, with baseline/domain matrix v0.1.7, adds [D-07's two independent Router-led judgments plus a separate test challenger and written personal-Router scope][current-d07]. Its sections 8.1-8.6 retain role/proof requirements and provenance/adaptation limits; written scope is not runtime installation, executed challenge, peer fanout or an actual review. D-01 through D-07 approaches are selected; D-08 through D-12 remain open (five). No auto-merge/Agent merge, credentials/permissions, governance activation, individual Q-oracle approval or paired-service waiver is granted. Actual .NET/service contracts, source/history/API closure and protected runtime/identity/wiring remain unfinished.

The current shared receipt is the open/unmerged source head of [kushagraThapar/SuicideSquad#6][current-policy-pr], not merged shared main. The coordinator's later verified shared main is `01803bacd42f9b66bc20e1af03dcb6bcc2a30dc7`, after [kushagraThapar/SuicideSquad#5](https://github.com/kushagraThapar/SuicideSquad/pull/5) merged. The previous D-06 receipt had that PR open and main `b45999c818c29169fefbe2902155d2fd65fd284f`; earlier source pins and A-01 retain their historical policy boundaries.

The first bounded increment is [I-01: paired Encryption patch protection and unit selection](specs/i-01-encryption-patch-paired-tests.md), a partial G-02/B-034 slice. Its versioned spec precedes test/pipeline implementation. Implementation intake main resolved once at `2026-11-01T18:40:10Z` to A-01's unchanged commit. Layer-specific execution, artifact and remaining-input records belong in the linked matrix/spec; they do not refresh other historical rows.

**I-01 current status:** testing and explicit tracked GA/preview Encryption unit selection are implemented. Local package-backed unit projects passed 58/58 each, including five exact patch cases; current-core source comparisons passed those five cases in each surface. Tested source commit `ee19f83e9e13d4f98790a6fbfe09dd4bd087ad29` was pushed and [SDK PR #6182][sdk-increment-pr] opened. Native build 64324 against merge candidate `0babdeb0748c59bd2c552fd3c1a06bb8b63515ef` supplies GA/preview unit runs 1331299/1331287 (58/58 each, exact five patch leaves) and service runs 1331291/1331290 (all eight exact mode/API/order cells plus the strengthened existing patch witness passed). All four tasks passed on attempt 1. Wider service projects retain one existing ignored change-feed leaf each; no required I-01 cell is skipped. [Spec v0.1.4](specs/i-01-encryption-patch-paired-tests.md#native-prci-receipt) records original TRX, parent/leaf reporting distinctions, commands, host/runtime and remaining identity limits. The overall build was still in progress at that receipt. This is executed protection, **not independent contract, independent review/challenge, all-G-02 or whole-SDK closure**.

The delivered v0.2.1 plan SHA-256 `c838ff1c1f9e392ca39e46850422516f66a83e2d1cb97a2856c3963c9c7c3773` remains historical. Continuing the first increment, an existing origin/SSH dry-run push verified a publishing route despite the current CLI account's read-only upstream permission. No account, permission or remote was changed. The local ARM64 vNext emulator is not a faithful alternative for Direct/CE-key fixture proof; the linked spec records that compatibility boundary. Actual pushed-candidate, PR and native CI/service receipts remain separate from this pre-push record.

## 1. What needs fixing

The immediate gaps are **trustworthiness and completeness of applicable test evidence**, not twelve diagnosed SDK functional bugs. A-01 establishes concrete selection/configuration distinctions and partial assertion observers, while case execution, effective environments, installed artifacts and many interaction sequences remain unknown. An existing nightly preview check failed, but its cause is unknown.

Start with evaluated configuration, existing companion-unit selection, artifact identity and attempt-aware cohort receipts. Then strengthen a bounded set of existing assertions, followed by critical cross-feature sequences. Broader semantic/history closure, compatibility and resource comparisons build on that foundation. This order reduces dependencies; it is not a new severity scale, numerical target, deadline or execution budget.

The v0.1.0 assignment created only the plan and matrix, without SDK execution. The newly authorized work proceeds in small spec-first testing/fixture/existing-CI increments, with separate local/native executed receipts and honest blocked or unqualified cells. Package versions, permissions, publishing/release lanes, merge settings and arbitrary production resources remain out of scope. Java remains paused at its accepted checkpoint. The separate Direct-source migration is not advanced or assumed complete.

## 2. Portable A-01 intake and snapshot provenance

<a id="a-01"></a>

This section is a **derived intake**, not a replacement or republished copy of A-01. The original report was read and its byte identity verified. The coordinator retains that original; no machine/session path, SDK source snapshot, raw operational log or credential is copied here. A portable publication/retention location for the original report remains pending under D-04/D-10. The digest identifies the report; the immutable links below independently identify its public source witnesses.

| Input | Identity and evidence boundary |
|---|---|
| A-01 original | `.NET CI-to-test-to-source reconciliation audit`; SHA-256 `63e368bc66f5c876bb853de62116a9d54ce34f9e1066b5f0b89da8df95b4949c`; 98,011 bytes |
| A-01 SDK pin | [`Azure/azure-cosmos-dotnet-v3@52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`][sdk-pin]; tree `2c89abbd9680af78d5072b523df277d8c46b755d` |
| A-01 resolution | Upstream main resolved `2026-11-01T16:45:17Z`; freeze receipt `2026-11-01T16:45:53Z` |
| Original project-policy provenance | [D-01-era shared inputs at `fecb6db64817175ef5752ce28b6f8c675196b348`][audit-policy]; A-01 separately recorded the later [D-02/spec-first receipt at `a4af4bcee8b4f931b1e127599ca6fdc719aa2dee`][audit-d02] |
| Original policy boundary | A-01 said D-03 through D-12 were open. Do not rewrite that report to claim D-03/D-04 had already been selected |
| This plan's additional policy input | `kushagraThapar/SuicideSquad@3b141f8c82a18d660069627c399ccfc9efc837c0`: [handoff v0.2.4][shared-handoff], [baseline v0.1.4][shared-baseline], [domain matrix v0.1.4][shared-domains]; published context in [kushagraThapar/SuicideSquad#3][shared-pr], open at intake |
| Later implementation policy receipt | `kushagraThapar/SuicideSquad@532e2401be02da06becbbc459dd58b78ae9b35d4`: [handoff v0.2.5][implementation-policy], sections [10.5][implementation-d05]/[11.1][implementation-authorization]; baseline/domain matrix v0.1.5. Not retroactive A-01 policy |
| This assessment's main/base | A single upstream `refs/heads/main` read returned A-01's commit. The clean isolated worktree HEAD/base also matched its commit and tree; identity receipt recorded `2026-11-01T17:44:01Z`, after the resolution |
| Source delta | None between A-01 and this once-resolved main. This is not a new assertion census or refreshed CI execution receipt |
| Historical v0.1.0 candidate boundary | The original documentation drafts were not an SDK implementation candidate and produced no tested package/merge receipt. The later I-01 candidate and bounded dependency/execution evidence are recorded separately above/in the spec |
| Concept reference | `tvaron3/cosmosdb-design-docs@bcfbc786fa5296deebcccd11369efb583e63e48c`, synchronized on main and consulted for the qualified boundaries below |

**Frozen evidence breadth:** A-01 read 12 root Azure Pipelines entries, 19 local templates and two tracked Actions workflows (33 tracked CI YAML files). It reconciled 37 navigation lanes, eight proposed atomic candidates, 21 additional assertion witnesses, 28 critical scenario seeds and 12 database families. These are navigation/inventory inputs, not coverage denominators, executed case counts or approved guarantees. Selected assertion bodies are not a whole-SDK assertion census.

**Future drift/revalidation backlog:** if main advances, keep A-01's digest, commit, source spans and original policy receipts unchanged. Record the new main/base/candidate once, then bound the changed-file/configuration delta affecting G-01 through G-12 and the linked matrix rows. Revalidate impacted witnesses, parameters, reference modes and filters; leave other historical rows explicitly historical. Mutable CI registration, evaluated inputs, fixture capabilities, retention and case receipts already need G-07/G-08/G-11/G-12 revalidation even while the source commit is unchanged. Do not silently replace an old pin with a moving `main` URL.

### Evidence-state vocabulary

| State | Meaning in these drafts |
|---|---|
| Confirmed selection/configuration deficiency or distinction | A-01 read the relevant tracked selection, manifest, flag, condition or comparator logic; runtime effects can still be unknown |
| Assertion witness / partial observer | A-01 read the actual assertion and fixture; the named observer may establish only a smaller behavior than the test name or feature lane suggests |
| Execution/access unknown | No applicable case-level, attempt/configuration/artifact-bound receipt was obtained; this does not mean zero tests, zero coverage, unsupported behavior or a product defect |
| Proposed contract/oracle question | Intended observable rule needs independent authority and reviewed oracle decisions; source, mocks and concept examples do not approve it |
| Observed failed check, undiagnosed | Revision-bound failure metadata exists; failing cases, infrastructure versus assertion cause and reproducibility are unknown |
| Proven SDK functional defect | None established merely by A-01. Confirmed harness bookkeeping/selection issues are not automatically customer-visible SDK bugs |

### Concept boundaries

Consult [transport: Two Connectivity Modes][concept-transport], [partitioning: SDK-Side Terminology and split semantics][concept-partition], [consistency: Session Consistency][concept-consistency], [session tokens: Scope and Non-Goals / lifecycle][concept-session], [change feed and its qualified merge scenarios][concept-cf], and [status/retry surfaces][concept-retry]. These concept/cross-SDK references frame questions; they are not approved current .NET/service oracles.

Timeout/cancellation does not prove non-commit. Pagination does not imply snapshot isolation. Bulk is not cross-partition atomicity; single-PK transactional batch and preview DTX remain distinct. Change feed is mode/range/retention scoped, not universal global order or exactly-once processing. Session state is identity/session scoped, not account-global. Mocks do not prove live durability. Do not adopt Java-specific defaults, illustrative retry tables, token algebra or hypothesized merge cost/replay claims as .NET contracts.

## 3. Selected policy versus remaining decisions

At this plan's shared-policy pin, D-01 selects main/all-source/CI-native/Gateway-and-Direct scope. D-02 prioritizes behavioral and cross-feature preservation, requires applicable testing evidence for every PR (sufficient existing tests count), and requires a proportionate design/spec **before** agents implement a feature or bug. D-03 starts with established CI gates/configured limits and strengthens demonstrated gaps, with finite attempts and original failures retained; it does not approve new tolerances. D-04 places SDK-specific records in SDK docs and shared agentic standards/templates/process in SuicideSquad.

This is additional planning authority, not evidence of existing enforcement. Later D-05/D-06/D-07 approach selections are recorded above, not retroactively inserted into that shared-policy pin or A-01. D-07 selects minimum independent review/challenge roles and written scope, not actual qualifying identities or runtime proof. Specific contracts/oracles and protected-policy/identity/enforcement wiring remain unfinished; D-08 through D-12 remain open. No oracle owner, actuator identity, tool, budget, live capability policy or exception/retirement procedure is selected or implemented here.

## 4. Prioritized work packages

| Proposed order | Work packages and first bounded outputs | Prerequisite / stopping boundary |
|---|---|---|
| A: trustworthy harness and selection | [G-01](#g-01)/[G-11](#g-11) evaluated job/input receipts; [G-02](#g-02) existing companion-unit selection; [G-04](#g-04)/[G-07](#g-07) cohort and attempt records; identity slice of [G-03](#g-03) and fixture slice of [G-08](#g-08) | I-01 begins the dependency-ready Encryption slice. Effective execution, artifact and account claims require actual authorized receipts; inaccessible ADO inputs do not block independent local testing |
| Access-dependent lead alongside A | [G-12](#g-12): examine the existing failed build's receipts, not a rerun | Lack of ADO access does not block other planning; no speculative SDK fix |
| B: bounded observer improvements | [G-05](#g-05): bulk pre-I/O, explicit 304/exact resume, barrier propagation, HTTP disposal timing, hybrid alignment/scope | Reuse existing suites and negative controls; independent disputed semantics remain D-05 work |
| C: critical cross-feature preservation | [G-06](#g-06): constrained sequences selected from all retained seeds/families | Approved rule/oracle and faithful capability/phase observer first; no invented guarantees or live fixtures |
| D: broader closure and comparisons | [G-09](#g-09) attribution/comparator; broader [G-03](#g-03) consumer compatibility; [G-10](#g-10) history/API/spec mapping and assurance-docs wiring | G-10's spec-first rule applies from the first future change, not only in this later wave. Tooling, history admission, retention and governance decisions remain open |

The dependencies below distinguish a planning input from the proof needed to close a later implemented slice. An unknown receipt may be recorded now; it cannot be accepted as passing evidence later.

## 5. Common proof required for a future implemented slice

Use native SDK CI outputs, particularly existing TRX, logs, package artifacts and configured reports, plus a small human-readable durable summary. This is a proposed minimum proof checklist, not a new database, catalog engine, executable schema or duplicate pipeline.

| Proof | Proposed acceptance record |
|---|---|
| Intent before implementation | Versioned SDK feature/bug spec; intended rule or reproduction/correction; authority status; affected/interacting features; preserved versus intentional changes; negative/boundary sequences; mapped existing/new assertions. Reference the versioned shared process |
| Candidate and artifact | Exact main/base/head and, when assessed, actual merge-candidate; evaluated CI/template revision; source-reference versus installed nupkg; package/dependency/native payload digests and loaded assembly identity. Do not substitute a sibling pack job for the tested artifact |
| Configuration/capability | Evaluated command/filter/parameters, GA/preview/nightly/internal symbols, configuration, project/TFM, actual runtime/SDK, OS/image/architecture, sanitized fixture/topology/consistency/capability identity and transmission/possible-commit scope |
| Assertions and controls | Exact test method/data row, assertion/observer and independent authority; a control that would fail for the specific incorrect behavior; preservation neighbors. A green test name, mocked success or implementation-derived expected value is insufficient |
| Cohort and attempts | Expected applicable cases versus discovered, selected, executed, passed, failed, skipped/inconclusive/excluded/overlapping cases; identities and reasons, not just totals. Attempt-indexed original failures and eventual outcomes; retain configured finite limits rather than invent new ones |
| Durable result | Bounded conclusion, missing cells and blocking inputs; meaningful result/configuration/reproduction identities with retained CI artifact references. Raw large evidence stays in CI; expiry/inaccessibility is unknown, not pass or zero |

Acceptance always concerns the named slice and its required neighbors. No sum of green rows qualifies the entire SDK. Added regression protection must not be weakened to obtain green CI; intentional contract changes/retirements remain D-12 decisions.

## 6. Evidence-backed remediation detail

### G-01: nominal versus effective configuration

<a id="g-01"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 sections 3.1, 4.1, 5 and G-01; confirmed configuration distinctions, effective execution unknown. [Core unit command][j-unit], [cron][e-cron], [rolling][e-rolling], [nightly][e-nightly], [CTL][t-ctl]/[samples][t-samples] parameter declarations |
| Behavior risk | A Release/preview/platform label can be credited to a different compiled/runtime path. Generic unit/Flaky commands omit `BuildConfiguration`; rolling embeds preview in an unforwarded configuration parameter; ordinary OneES `VmImage` parameters do not bind hosts; nightly GA-version labels can carry preview symbols |
| Existing-suite/CI change | Reconcile caller/parameter intent in existing entries/templates, including undeclared CTL/samples `Arguments`; explicitly forward or correctly label configuration/symbol intent and bind retained host cells to evaluated jobs. Preserve distinct GA, dedicated preview and nightly-preview-symbol records |
| Assertion / negative control / neighbors | Compare declared intent with evaluated command, loaded assembly/symbols, actual runtime and host receipt. A deliberately mismatched Release/Debug, preview flag or host label must be classified as mismatched, not credited. Preserve Debug, ordinary/Flaky filters, `OS` condition behavior, companion TFMs and current configured limits |
| Dependencies / candidate proof | G-11 effective inputs and G-07 result publishing supply closure receipts. Every downstream behavior row needs its actual candidate/configuration, not nominal labels |
| Acceptance / blocked inputs | Named cells agree with evaluated commands and artifacts, or remain explicitly unqualified. Authorized evaluated OneES/job/runtime receipts and confirmed template intent are missing; no new platform/runtime baseline is selected |

### G-02: explicitly unselected companion unit projects

<a id="g-02"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 sections 4.2, 7.1 and G-02; confirmed explicit selection gap in the 33 inspected YAML files. Existing [Encryption][p-encryption-unit]/[Custom][p-custom-unit] unit targets contain [W-17 encrypted/nonencrypted Increment][w-encryption] and [W-18 cache/cancellation][w-custom] assertions |
| Behavior risk | Core, serializer, patch, cancellation or dependency changes can evade these existing companion observers. Solution/parity builds and companion emulator invocation do not execute companion units |
| Existing-suite/CI change | Add the existing unit projects to suitable established PR/preview/companion jobs. Keep current-core source-reference proof separate from compatibility against each companion's default published SDK. Reuse existing source/package-symbol parity compilation |
| Assertion / negative control / neighbors | Preserve encrypted-path Increment rejection/message and nonencrypted pass-through; internal cache timeout fallback versus pre-cancelled caller throwing with zero source/L2 reads. A selection receipt omitting either expected project/TFM/eligible leaf must expose missing proof. Preserve ordinary core/nonencrypted behavior, GA/preview symbol surfaces, and Custom `net6.0`/`net8.0` distinctions |
| Dependencies / candidate proof | G-01 evaluated variants, G-03 reference/artifact identity slice, G-07 eligible/discovered/executed leaf mapping; exact method names/data rows must be reconciled where A-01 retained only assertion spans |
| Acceptance / blocked inputs | Named applicable companion leaves have candidate-bound invocation/result receipts; published-SDK runs are not mislabeled current-core runs. Missing evaluated reference resolution, runtime-8 availability and case receipts block closure, not the draft |

### G-03: source, package, companion and Direct identity

<a id="g-03"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 section 4.2/G-03; confirmed artifact boundary, parity/execution unknown. [Core pack inputs][p-core-artifacts], [staged Direct project][p-direct], [consumer targets][p-consumer], [benchmark reference conditions][p-benchmark], [native][w-native]/[dependency][w-dependencies] witnesses |
| Behavior risk | Source tests can pass while a different packaged DLL/native target or older companion dependency is shipped/consumed. `ProjectRef`, `SdkProjectRef`/`SDKPROJECTREF` and `OSSProjectRef` are not interchangeable |
| Existing-suite/CI change | First record resolved/loaded core, Direct, HybridRow and companion identities in existing jobs. Later extend established parity/package/consumer lanes to install and consume the exact produced nupkg for applicable native/runtime/companion cases; reconcile overlapping benchmark source/wildcard references |
| Assertion / negative control / neighbors | Consumer receipt must show the exact expected package/assembly/native identities and observable consumer behavior. Substitute a sibling/older package or mismatched dependency as a control and reject identity credit. Preserve reflection/API baselines, override/package-symbol compilation, native loading and FI Direct coupling |
| Dependencies / candidate proof | G-01/G-11 evaluation and G-07 receipts; wider consumer cells depend on D-05 applicability and D-09 tooling feasibility. Track the separate Direct migration only after its actual wiring changes |
| Acceptance / blocked inputs | Source-reference, published dependency, installed package and staged-source rows remain distinct. Missing nupkg contents/digests, loaded-assembly/consumer results and effective restore inputs block parity claims. No staged Direct execution or completed DLL replacement is claimed |

### G-04: advisory, ignored, conditional and excluded cohorts

<a id="g-04"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 sections 5, 6, 8.2/G-04; confirmed advisory/disabled/conditional declarations, actual outcomes unknown. [ThinClient][j-thin], [FI][j-fi], ignored DTX [conditional][dt-ignored]/[read][dt-read-ignored] classes, [AAD leaf gate][aad-gate] |
| Behavior risk | Advisory success, conditional Inconclusive, source-level Ignore, excluded categories or overlapping Flaky lanes can be mistaken for passing required behavior |
| Existing-suite/CI change | Expose these dispositions and reasons through existing result publishing. Reuse AAD's nonempty/no-skip/all-passed leaf validation where applicable; add expected-cohort reconciliation without silently making governance decisions |
| Assertion / negative control / neighbors | Deliberately omitted, skipped or inconclusive required leaves must not become pass-shaped evidence. Multi-tag Flaky/Quarantine controls must retain exclusions/disposition and prevent duplicate credit across normal/Flaky jobs. Preserve valid account-free FI cases and honest advisory/live distinctions |
| Dependencies / candidate proof | G-07 cohort receipts, G-08 capability identity; D-05 applicability, actual required-check/governance wiring under the design-only D-06 approach, and D-10 live ownership remain unfinished |
| Acceptance / blocked inputs | Each expected obligation has an explicit executed or unresolved/parked disposition. Do not remove DTX Ignore, enable live integration, change ThinClient blocking status, or retire excluded obligations under this draft. Live-capability/policy receipts are missing |

### G-05: bounded assertion and observer improvements

<a id="g-05"></a>

These are five separate proposed slices, not one "feature covered" claim. Common dependencies are G-01/G-03/G-07 for configuration/artifact/cohort proof, independent D-05 authority for disputed rules, and G-08/D-10 for any live-capability slice.

| Slice / matrix rows | Existing witness and precise deficiency | Proposed assertion, control and preservation neighbors | Acceptance / blocked inputs |
|---|---|---|---|
| Bulk pre-I/O: B-004 | N-I04 [`ValidateThroughputBucketRequestOptionThrows`][a-bulk] directly calls validation; public operation/AddAsync materialization/dispatch is not negatively observed | Extend the existing bulk executor/point-operation fixture through the actual entry path. Reject the nonnull request bucket with the expected exception and record zero materialization/serialization/dispatch for the rejected operation. A silently dropped option or validation moved after I/O must fail. Preserve null acceptance, unrelated items and independently confirmed client-level/non-bulk bucket behavior | Entry-path observer and exact candidate are required. Public option authority, fixture interception points and neighbor applicability need confirmation; no new production hook is selected |
| 304 and exact resume: B-007/B-008 | [Unit token inequality][a-cf-unit] is weak; [emulator aggregate resume][a-cf-emulator] does not explicitly assert 304. Stronger [`AadChangeFeedContinuationAsync`][a-cf-aad] already asserts 304, nonempty token and only the second item after resume | Reuse the stronger pattern with an independent known-write/range journal: explicit 304, valid nonempty opaque continuation, fresh same-scope resume, exact newly written item/no replay of the drained item. Stale/ignored continuation or omitted 304 must fail. Preserve conditional-read 304 as a distinct behavior, query paging, mode/range/options and allowed topology/retention outcomes | Existing live witness is prior art, not newly executed evidence. Same-scope capability/receipt required; merge/timestamp/mode/version closure remains G-06/D-05 work |
| Barrier policy and propagation: B-005/B-006 | [Policy witness][a-barrier] asserts **ShouldRetry=false** and no partition-health poisoning for isolated non-DTX `408/21013`. [Named constructor-chain helper][a-barrier-helper] directly constructs the writer with mocks/flag, not a full CosmosClient/RNTBD path | Preserve the exact policy assertions. Separately observe the option/configuration from real client construction through the actual factory/Direct artifact boundary; do not inject the final flag and call it E2E. Lost/inverted propagation must fail. Keep generic 408, DTX origins, outer policies and unrelated partitions as distinct independently reconciled neighbors | Packaged producer/configuration propagation, outer-policy/service authority and suitable observer are missing. No universal retry outcome, no-commit guarantee or real-RNTBD claim |
| HTTP disposal timing: B-026/B-027 | W-11 [`ThinClientRetriableResponsesAreDisposedBeforeRetryAsync`][w-http] checks disposal after the loop, not at the next dispatch | In the existing handler fixture record prior owned retry-content disposal at each subsequent send. A control disposing only after the next send must fail even if final disposal passes. Retain successful response ownership and existing post-disposal diagnostics/zero-body metric safety; preserve other modes/factory lifetimes | Requires attempt/ownership observer and candidate receipt. This does not prove physical HTTP/2 reset, unobserved-task cleanup or all transport lifetime behavior |
| Hybrid alignment and scope: B-028 | W-12 [SanityTests][w-hybrid] + [RunTests][w-hybrid-run] assert expected ID lists/accepted ties and some scores; fixture is Direct/MultiPartition, Gateway commented out | Independently reconcile ranking/tie authority, then journal component ID/score alignment through partial/empty/tied components and paging. Misaligned component IDs/scores or an order outside approved tie allowances must fail. Reuse existing Query fixtures for applicable Gateway/local-global scope cases rather than claiming the commented-out cell passed. Preserve fixed-data query/LINQ, continuation and serializer neighbors | Independent ranking oracle and Gateway/capability applicability are unresolved. Existing accepted ties must not be overconstrained; revision history alone is not reproduction or a new defect |

### G-06: critical interaction sequences and broader behavioral closure

<a id="g-06"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 sections 7.2, 9.1-9.3/G-06; unverified closure, not proof tests are absent. Matrix retains every SC-01 through SC-28 and DB-01 through DB-12 separately |
| Behavior risk | Isolated status, cache, token, disposal or serialization assertions do not establish correctness when phase, topology, budgets and other features interact |
| Existing-suite/CI change | Expand bounded sequences in their normal unit/emulator/Query/Batch/ChangeFeed/live/companion suites. First candidates: throttle plus deadline/health; exact resume plus topology/identity; possible-commit write/patch; delete/recreate/session cache; per-item bulk/missing results versus batch rollback; shutdown/observer failure; hybrid/serializer/companion interactions |
| Assertion / negative control / neighbors | Use independent operation/request/result/ownership journals. Record status **and** substatus, origin/transformation, transmission/possible commit, remaining configured budgets, route/refresh decision, final result and cleanup. Controls include replaying a non-idempotent effect, wrong-RID reuse, missing bulk-item correlation, lost page state, stale winner/session merge and callbacks after shutdown. The matrix names each seed's narrower observer/control; do not infer no commit or snapshot semantics |
| Dependencies / candidate proof | Reuse G-05 observer patterns; G-01/G-03/G-07/G-08 receipts; D-05 rule/oracle and D-10 faithful live/fault/capacity authorization. Public DTX is preview; GA internal mock access is not public capability |
| Acceptance / blocked inputs | Close only the chosen, fully specified valid sequence and required preservation neighbors. All other lane/seed/family cells remain unverified with follow-up tasks. Full inventory, service authority, capable fixtures, allowed concurrent outcomes and budgets remain incomplete; no live DTX enablement or new tooling |

### G-07: expected cohorts and original-attempt evidence

<a id="g-07"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 sections 5, 8.1-8.2/G-07; first-attempt/cohort receipts unverified. Existing task retries include 2/4; fixed result filenames do not prove receipts were lost or retained. [AAD][aad-gate] validates observed leaves but not a complete expected set |
| Behavior risk | Eventual retry success can conceal first failures; undiscovered/filtered cases and data-driven aggregate rows can hide missing obligations |
| Existing-suite/CI change | Retain native attempt-indexed TRX/log/configuration/artifact receipts in existing publishing. Reconcile expected eligible case/data-row identities with discovered/selected/executed leaves and exclusions; record first and final outcomes separately |
| Assertion / negative control / neighbors | A missing expected case, zero eligible output without an explicit applicability disposition, skipped required leaf, overwritten original-failure link or parent-row-only receipt must expose incomplete proof. Overlapping normal/Flaky cases receive no duplicate assurance credit. Preserve existing finite retry limits and distinguish test retries from task/startup retries |
| Dependencies / candidate proof | Uses G-01's variant definitions, G-03's artifact identities and G-08's capabilities; raw receipt inventory can start with explicit unknown fields. Retention/access details are D-10 work |
| Acceptance / blocked inputs | Exact eligible cohort/dispositions and attempt lineage reconcile for the named job/candidate. Generic green metadata is insufficient. TRX, actual discovery/results and retained original-attempt artifacts remain inaccessible/unobtained here; no discovery or run is performed |

### G-08: actual fixture, capability and topology identity

<a id="g-08"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 section 4.3/G-08; confirmed fixture declarations, effective fidelity unknown. [Emulator setup][t-emulator], [AAD support][p-aad-support], [live region jobs][j-regions] and ThinClient declarations |
| Behavior risk | Strong/rate-limit-disabled preview emulator behavior can be credited to realistic RU throttling, live topology or coordinator durability; external connection strings do not identify regions/writers/capabilities |
| Existing-suite/CI change | Add sanitized fixture/version/capability/consistency/topology and namespace/cleanup receipts to existing jobs/results. Prefer the already strict AAD prerequisite pattern; use existing authorized accounts only in a separately authorized phase |
| Assertion / negative control / neighbors | A rate-limit-disabled emulator cannot qualify realistic throttle behavior; a missing mode/capability/topology receipt cannot qualify its cell. Namespace/identity controls distinguish another container/account/client without recording credentials. Preserve account-free units and legitimate mocked evidence at their narrower scope |
| Dependencies / candidate proof | G-01/G-03/G-07 identities; D-05 capability authority and D-10 live ownership, capacity, isolation, cleanup/access. Record actual service/emulator version rather than an MSI URL alone |
| Acceptance / blocked inputs | Each applicable executed cell binds its actual safe fixture identity and cleanup outcome. MSI payload/version, external live capabilities, topology, consistency and role/configuration receipts are missing. No accounts, roles, keys, live faults or capacity policy are created/changed |

### G-09: coverage attribution and allocation comparator bookkeeping

<a id="g-09"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 sections 4.1, 8.2/G-09; confirmed unit-only Debug coverage scope and comparator logic, no measured score. [Coverage job][j-coverage], [performance program][p-perf-program], [PerformanceValidation][p-perf-validation] |
| Behavior risk | A unit-only report can be mistaken for shipped/companion/staged-source closure. Pairwise allocation folding and current-key-only iteration can hide comparison/cohort changes |
| Existing-suite/CI change | Attribute existing reports to candidate/assembly/configuration/suite and explicit unloaded/excluded source. Separately spec comparator bookkeeping: reconcile expected/new/disappeared keys and actual samples before applying established configured tolerances |
| Assertion / negative control / neighbors | Independent synthetic comparator cases must expose missing expected keys, unexpected/new keys without disposition, empty/cohort mismatch and sample order/weight changes. If an equal-weight three-sample mean is confirmed as intended, `(10 + 20 + 30) / 3 = 20` is an exact arithmetic control, not a new performance threshold. Preserve valid baseline serialization and existing tolerances unless separately justified |
| Dependencies / candidate proof | G-01/G-03/G-07 comparison/cohort identity; D-05 statistic/obligation authority and D-09 instrumentation/tool feasibility; D-03 comparable evidence/rationale for any new tolerance |
| Acceptance / blocked inputs | Comparable cohorts and explicitly intended arithmetic are independently checked; missing obligations cannot vanish silently. Complete instrumentation attribution, configured effective values, baseline/report payloads and comparator intent remain unresolved. No coverage/resource gain or SDK functional defect is claimed |

### G-10: spec-to-behavior-to-assertion-to-CI traceability

<a id="g-10"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 G-10 is a new project requirement, not a historical finding that old PRs violated later policy. Current [shared handoff sections 10.2-10.4][shared-handoff] adds D-03/D-04. [Main PR entry][e-main] excludes Markdown/docs; historical [CI guide][ci-guide-pin] is secondary to A-01's inspected YAML |
| Behavior risk | A future feature/bug spec or mapping can change obligations without corresponding applicable assertion/result proof, especially under a blanket docs-only filter |
| Existing-suite/CI change | Use these SDK-docs drafts as initial human-readable mappings. Future changes link pre-implementation specs to affected/preserved behaviors, exact assertions, native job/filter/result receipts and versioned shared rules. Reconcile assurance-spec/mapping path handling in existing PR/CI checks without creating a parallel gate platform |
| Assertion / negative control / neighbors | An assurance-spec/mapping-only change that alters obligations must not silently get a docs-only bypass. Missing affected behavior, preserved neighbor, authority, exact assertion or applicable execution link must remain incomplete. Preserve ordinary docs workflows without inventing a D-02 testing waiver or selecting a new enforcement owner |
| Dependencies / candidate proof | G-01/G-03/G-07 evidence, G-06 expansion; D-04 concrete layout/wiring, D-05 oracle/lifecycle, D-06/D-07 protected governance/review integration. SDK-specific history and sanitized regression records stay in SDK docs; shared templates/process stay in SuicideSquad |
| Acceptance / blocked inputs | A bounded future change has the bidirectional chain and honest unresolved cells, with no source/test/CI payload copied into docs. Exact layout/schema/retention/wiring, historical PR/API closure and protected runtime review protocol remain pending; no filters or gates are changed now |

### G-11: external/evaluated inputs and registrations

<a id="g-11"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 sections 3.3, 4.1, 8.1/G-11; confirmed external/dormant boundaries, effective state unknown. [Root props][p-props] permit an optional above-repository import; A-01 found no tracked NuGet.config/global.json/runsettings. Tasks, feeds, SDK/MSI/tool/image payloads and registrations remain external |
| Behavior risk | Different restore/import/task/agent inputs can change the compiled/tested candidate. Disabled tracked static tasks do not prove absent security analysis; A-01 also observed dynamic CodeQL success metadata |
| Existing-suite/CI change | Capture evaluated safe build inputs, resolved package/tool/task identities, host capabilities and live registration/check-context receipts through established metadata/artifact publishing. Reconcile declared config literals with actual task/feed handling without reading unrelated private parent checkouts |
| Assertion / negative control / neighbors | Unknown/mismatched import/feed/task/SDK/MSI/registration identity cannot be treated as identical candidate proof. A control with a changed resolved payload must change the comparison identity. Preserve legitimate dynamic CodeQL/component-governance distinctions and existing approval boundaries |
| Dependencies / candidate proof | Authorized read-only evaluated/registration receipts; D-10 retention/access. G-01 consumes this input inventory, not an invented new host/tool baseline |
| Acceptance / blocked inputs | The named job has safe, sufficient reproducibility inputs or an explicit unresolved boundary. Parent imports, evaluated NuGet handling, external payloads, live required checks/branch governance and service connections remain unknown; no installation, permission change or authentication bypass |

### G-12: existing failed nightly preview check

<a id="g-12"></a>

| Field | Proposed work |
|---|---|
| Evidence / class | A-01 section 8.1/G-12: observed failed check, cause unknown. [Existing build 64312][failed-run]; job `4a79149b-973a-554c-792a-7556fdd68297`; check `124709467675`, `dotnet-v3-nightly (Preview parity build Preview Tests Release)`, failure at the frozen SDK SHA |
| Behavior risk | Generic dotnet exit-1 annotations can be misdiagnosed as an SDK regression, or ignored because sibling parity/package/AAD labels are green |
| Existing-suite/CI work | Retrieve only existing authorized evaluated job/configuration, per-attempt TRX/logs, case/cohort and artifact receipts; localize test assertion versus infrastructure/configuration failure before proposing any SDK or harness correction |
| Assertion / negative control / neighbors | Diagnosis must identify the actual failed leaf/attempt/exception/observer and candidate; a missing or unrelated successful job receipt cannot satisfy it. Preserve original failure and distinguish Preview Tests from Preview Flaky/parity/AAD/package outcomes |
| Dependencies / candidate proof | Authorized ADO read access and retained payloads; G-01/G-03/G-07 receipt identities. No rerun or speculative reproduction is part of this draft |
| Acceptance / blocked inputs | Close only with case-level cause evidence, or retain an explicit blocked/expired/inaccessible disposition. A-01's build/timeline REST requests returned HTTP 401. The current available/deferred catalog was checked again: no `ado` tool or relevant build/timeline/TRX reader was exposed; no REST retry or new execution was attempted |

## 7. Exact unresolved boundaries

| Decision / input | Remaining proof/wiring or open decision; no implementation authority inferred |
|---|---|
| D-04 concrete follow-through | Final SDK folder/schema/indexing, controlled publication/migration of A-01/bootstrap inputs, shared-rule links and existing-CI docs-path wiring |
| D-05 | Source/changelog/API/internal-workflow closure, atomic oracle authority, disputed contracts and explicit lifecycle exclusions |
| D-06 implementation follow-through | Protected/fail-closed future approach approved DESIGN ONLY; actual repo/org policy inspection, actuator/owner identities, protected-input wiring and any authorized governance changes remain unfinished. Existing approval/protection boundaries remain binding |
| D-07 implementation follow-through | Two independent Router-led judgments plus independent test challenger, explicit required-role passes and written personal-Router scope selected DESIGN ONLY; actual qualifying identities, runtime/challenge execution and protected final-candidate integration remain unfinished. No reviewers or peer fanout run here |
| D-08 | Cross-SDK matrix depth and bounded Router peer fanout; no new peers or Java work launched |
| D-09 | Mutation/property/concurrency/coverage tooling fit; existing fuzz/performance facilities are prior art, not selected campaigns |
| D-10 | Evidence retention/access/provenance, costs/concurrency, live ownership/capacity/fault isolation, cleanup and stop/recovery |
| D-11 | Historical corpus admission, qualification samples/strata/duration/uncertainty and prospective escape criteria |
| D-12 | Protected intentional contract changes, exceptions, obsolete obligations and historical behavior that is itself incorrect |
| Access / execution receipts | I-01 build 64324 is readable through existing Azure CLI authentication; anonymous 401s retained. All four relevant tasks passed on attempt 1, with exact five unit/eight service leaves each plus existing patch witness; original TRX and effective configuration/host receipts obtained. Whole-build disposition, complete loaded-payload/fixture identity, required-check/protected governance and broader live/consumer/review/challenge proof remain unverified. No inference for other rows |
| Operational guidance boundary | Coordinator's RCAKit attempt returned MCP OAuth challenge required, HTTP 401; no operational result was obtained. That is not a .NET/service semantic oracle, and this planning work does not depend on rerunning it |

Retain all nine diagnostic dimensions for later comparable assessments: executable lines, branch outcomes, mutation detection, atomic invariants, status decisions, scenarios/interactions, historical regressions, compatibility, and reliability/performance/resources. No weighted score or gains elsewhere compensate for a failed required behavior; no numerical threshold or new budget is introduced.

## 8. Draft acceptance and next authorized boundary

This draft is complete as a planning intake when every A-01 G-01 through G-12 has a stable link, risk, existing-suite proposal, observer/control, preservation neighbors, dependencies and closure/blocked-input criteria; the matrix retains all four navigation inputs and marks unexpanded cells unverified. That document acceptance is **not** SDK assurance, design approval, implementation authorization, applicable test execution evidence or merge eligibility.

The user's new authorization permits [I-01](specs/i-01-encryption-patch-paired-tests.md) and later small spec-first paired-testing increments, starting with dependency-ready G-02 and bounded observer/cohort work. G-12 receipt access is independent. Missing emulator/account/ADO inputs remain explicit and do not authorize arbitrary resources or rerunning the nightly. This is not whole-SDK closure or autonomous eligibility; disputed contracts and policy decisions still require their separate decisions.

## Immutable references

[sdk-pin]: https://github.com/Azure/azure-cosmos-dotnet-v3/tree/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8
[audit-policy]: https://github.com/kushagraThapar/SuicideSquad/blob/fecb6db64817175ef5752ce28b6f8c675196b348/docs/dotnet/design-handoff.md
[audit-d02]: https://github.com/kushagraThapar/SuicideSquad/blob/a4af4bcee8b4f931b1e127599ca6fdc719aa2dee/docs/dotnet/design-handoff.md#L689-L727
[shared-handoff]: https://github.com/kushagraThapar/SuicideSquad/blob/3b141f8c82a18d660069627c399ccfc9efc837c0/docs/dotnet/design-handoff.md
[shared-baseline]: https://github.com/kushagraThapar/SuicideSquad/blob/3b141f8c82a18d660069627c399ccfc9efc837c0/docs/dotnet/design-baseline.md
[shared-domains]: https://github.com/kushagraThapar/SuicideSquad/blob/3b141f8c82a18d660069627c399ccfc9efc837c0/docs/dotnet/domain-gap-matrix.md
[shared-pr]: https://github.com/kushagraThapar/SuicideSquad/pull/3
[implementation-policy]: https://github.com/kushagraThapar/SuicideSquad/blob/532e2401be02da06becbbc459dd58b78ae9b35d4/docs/dotnet/design-handoff.md
[implementation-d05]: https://github.com/kushagraThapar/SuicideSquad/blob/532e2401be02da06becbbc459dd58b78ae9b35d4/docs/dotnet/design-handoff.md#105-user-approved-d-05-approach
[implementation-authorization]: https://github.com/kushagraThapar/SuicideSquad/blob/532e2401be02da06becbbc459dd58b78ae9b35d4/docs/dotnet/design-handoff.md#111-separate-sdk-testing-authorization-context
[implementation-d06]: https://github.com/kushagraThapar/SuicideSquad/blob/72e9f57918ed81a38bd750f4a6aca58173877d7b/docs/dotnet/design-handoff.md#106-user-approved-d-06-approach
[current-policy]: https://github.com/kushagraThapar/SuicideSquad/blob/1fef335ecb72e15e75518cffc2d39814c2e6bd13/docs/dotnet/design-handoff.md
[current-d07]: https://github.com/kushagraThapar/SuicideSquad/blob/1fef335ecb72e15e75518cffc2d39814c2e6bd13/docs/dotnet/design-handoff.md#107-user-selected-d-07-approach-and-router-scope-addition
[current-policy-pr]: https://github.com/kushagraThapar/SuicideSquad/pull/6
[sdk-increment-pr]: https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6182
[concept-transport]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/06-transport-layer-rntbd.md#two-connectivity-modes
[concept-partition]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/04-partitioning.md#sdk-side-terminology
[concept-consistency]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/05-replication-consistency.md#session-consistency-default
[concept-session]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/16-session-tokens.md#scope-and-non-goals
[concept-cf]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/12-change-feed.md#partition-merge--startfrom-timestamp--scenarios-and-performance-concerns
[concept-retry]: https://github.com/tvaron3/cosmosdb-design-docs/blob/bcfbc786fa5296deebcccd11369efb583e63e48c/17-status-codes-and-sdk-retries.md#1-overview
[e-main]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/azure-pipelines.yml#L1-L106
[e-cron]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/azure-pipelines-cron.yml
[e-rolling]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/azure-pipelines-rolling.yml#L1-L63
[e-nightly]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/azure-pipelines-nightly.yml
[t-ctl]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-ctl.yml#L3-L38
[t-samples]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-samples.yml#L3-L38
[j-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L23-L55
[j-coverage]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L92-L134
[j-regions]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test.yml#L378-L453
[j-thin]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-thinclient.yml#L17-L59
[j-fi]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-fault-injection.yml#L17-L130
[aad-gate]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/build-test-aad.yml#L119-L188
[t-emulator]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/templates/emulator-setup.yml
[p-props]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Directory.Build.props#L3-L18
[p-core-artifacts]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L120-L168
[p-direct]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/Direct/src/Microsoft.Azure.Cosmos.Direct.csproj
[p-consumer]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.targets#L17-L75
[p-benchmark]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Samples/Tools/Benchmark/CosmosBenchmark.csproj#L29-L50
[p-encryption-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/Microsoft.Azure.Cosmos.Encryption.Tests.csproj
[p-custom-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests.csproj
[p-aad-support]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Utils/AadLiveTestSupport.cs#L33-L176
[p-perf-program]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/Program.cs#L18-L59
[p-perf-validation]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/Contracts/PerformanceValidation.cs#L29-L134
[a-bulk]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Batch/BatchAsyncContainerExecutorTests.cs#L480-L508
[a-cf-unit]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/FeedRange/ChangeFeedIteratorCoreTests.cs#L114-L135
[a-cf-emulator]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/FeedToken/ChangeFeedIteratorCoreTests.cs#L81-L135
[a-cf-aad]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/CosmosAadLiveDataPlaneTests.cs#L920-L967
[a-barrier]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/BarrierEarlyYieldOn429Tests.cs#L41-L112
[a-barrier-helper]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/BarrierEarlyYieldOn429Tests.cs#L751-L818
[dt-ignored]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/DistributedTransaction/DistributedTransactionConditionalE2ETests.cs#L24-L64
[dt-read-ignored]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/DistributedTransaction/DistributedReadTransactionE2ETests.cs#L30-L72
[w-http]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/CosmosHttpClientCoreTests.cs#L947-L1052
[w-hybrid]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Query/HybridSearchQueryTests.cs#L61-L151
[w-hybrid-run]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Query/HybridSearchQueryTests.cs#L338-L423
[w-encryption]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/EncryptionContainerPatchTests.cs#L38-L75
[w-custom]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Tests/DekCacheDistributedCacheContractTests.cs#L57-L122
[w-native]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/DirectContractTests.cs#L24-L53
[w-dependencies]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/DirectContractTests.cs#L113-L184
[ci-guide-pin]: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8/docs/builds-and-pipelines.md
[failed-run]: https://cosmos-db-sdk-public.visualstudio.com/4000bd49-81c3-47f2-94d8-d1392b95c228/_build/results?buildId=64312
