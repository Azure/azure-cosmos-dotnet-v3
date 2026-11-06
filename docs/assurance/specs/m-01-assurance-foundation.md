# M-01.1-M-01.4: assurance record foundation

**Version:** 0.1. **State:** pre-implementation specification for a bounded
infrastructure/test-only slice; no SDK behavior, contract or qualification change.

## Authorization and ordering

The original assignment authorized a plan only. The concrete session plan v0.1
was delivered before this implementation: 420 lines, 70,210 bytes, SHA-256
`ecb2d5b22c3d4c9bbc608206e63c7bc29ca3f1efd6c85c93db214e8f11d9f0c5`.
A byte-identical copy is retained by the coordinator. The unexplained native
"Plan approved" event and mode switch were not treated as user authority.

The coordinator subsequently relayed this **new direct user decision**.
The selected label is preserved as a JSON string (the escape preserves its
original typographic dash):

```json
"Approve M-01.1\u2013M-01.4 implementation (Recommended)"
```

The exact approved question, also represented as a JSON string, is:

```json
"Confirm implementation of only M-01.1\u2013M-01.4 and publication of its feature-branch PR: the foundation spec/JSON format, offline validator and synthetic fixtures, historical I-01 example, and lightweight native validation YAML/CI guide. This permits local validator-fixture checks and ordinary repository CI automatically started by the PR, but not manual SDK/emulator/live campaigns, new pipeline registration or required-check enforcement, protected evaluator admission, migration, permission changes, merging or releases."
```

No exact approval timestamp is inferred. This file and the format contract in
[`../README.md`](../README.md) are to be committed and pushed before validator,
fixture or pipeline implementation. The later mapping sidecar references that
immutable specification commit and its file-byte digest, not a self-referential
future commit hash. Git chronology is not independent oracle approval.

## Authority and boundaries

Shared policy source is
[handoff v0.2.12 at 447c6608](https://github.com/kushagraThapar/SuicideSquad/blob/447c660872ceaced30a5b5c60abec3bc5c54eb7b/docs/dotnet/design-handoff.md);
baseline/matrix are v0.1.12. All D-01-D-12 approaches are selected, not all
factual/tooling/operating/qualification details. Publication of that source and
its later integration at `79fe48b9cb00896e1bb04095ae2fde684b480914` are distinct.
Neither establishes SDK governance authority.

Implement only M-01.1-M-01.4:

1. SDK-local Markdown/JSON record contract and this preceding spec.
2. Offline fail-closed validator and deterministic synthetic JSON/TRX fixtures.
3. Historical, unqualified I-01 build 64324 example from immutable published
   documentation and inspected assertion references.
4. Lightweight native validation YAML/template source and related CI guide.

Do not modify shipped SDK source/tests/dependencies/versions, existing
`templates/build-test.yml` or `templates/build-preview.yml`, #6182's worktree or
PR, main CI triggers, live/AAD/Direct migration, or the shared policy repository.
No SDK/emulator/live/mutation/fuzz/benchmark campaign, manual CI operation,
new definition registration/required-check enforcement, protected admission,
migration, permission/resource change, semantic review/challenge/peer work,
merge or release is included. Java remains frozen.

Local validator fixtures and ordinary automatic repository CI resulting from
publication of this separate feature-branch PR are expressly allowed.

## Problem and approach

Existing specs and native receipts are useful but do not by themselves prove
the chain from pre-implementation behavior and independent expected outcomes
to actual assertions, applicable unit/service cases, terminal attempts and
exact source/package/runtime/fixture identities.

The main PR pipeline excludes Markdown and `docs/**/*`. Simply writing
assurance prose does not create an effective check. Conversely, building
the whole SDK for every ordinary prose change is disproportionate.

Use a small JSON sidecar/index format, not a second catalog/test database.
Use PowerShell/.NET primitives already present in native CI. Add a separate
lightweight native entry that includes Markdown without modifying the main
SDK matrix. Candidate-local metadata success is named precisely and can never
become trusted proof or eligibility.

The first-slice supported record kinds are `change` and `historical-example`.
Future producer/run/audit kinds fail explicitly as unadmitted. No native
collector or historical import is smuggled into format validation.

## Atomic validator invariants and assertions

| Invariant | Required observer/control | Preservation |
|---|---|---|
| Format integrity | Reject missing/unknown fields, duplicate JSON keys/IDs, wrong types/enums/digests, unsafe paths and empty records | Unknowns remain explicit, not converted to successful defaults |
| Actual local links | Verify local spec/assertion bytes, in-checkout paths and real PowerShell function definitions | External immutable refs are never downloaded or called independently verified |
| Closed mapping | Obligations/assertions/cells/cohorts resolve; case IDs/typed tuples/native labels are unique; explicit expected sets match cells | Observed output cannot shrink its own expected set |
| Terminal proof logic | Synthetic VSTest parent/leaf fixture: preserve all counters/topology, match all expected leaves exactly, reject nonpassing/missing/duplicate/ambiguous required rows | Disclose unrelated ignored leaves; never use zero-skips aggregate as a proxy |
| Identity logic | Synthetic expected versus observed source/base/merge/tree, spec/policy/cohort, binding/artifact/runtime/fixture must match | Published, current-core, produced package and consumer bindings are distinct |
| Attempt/history preservation | Failed prior attempts remain; missing prior evidence or a failed current native task cannot become success | Later repair belongs to its own candidate; no first-pass claim from eventual success |
| Protected input preservation | Synthetic base/candidate comparison detects deleted/changed obligations, cells, lifecycle or policy inputs | No author-selected weaker denominator or automatic retirement |
| Honest result boundary | Metadata output is separate from required proof and qualification; public RequiredProof mode fails closed | No synthetic record or author approval label can grant actual trust |

Concrete assertions will live in
`scripts/tests/Validate-Assurance.Tests.ps1`, with `Assert-Equal` and
`Assert-Rejected` functions exercising these invariants and subprocess checks
of real command exit codes/output. Fixed expected case declarations are
separate from observed synthetic TRX. No new test framework is installed.

Pure local validator logic has no Cosmos service observer. A meaningful
native integration/protected-entry test is a later admitted control, not a
fictional live JSON-parsing test or an author-issued general exemption from
paired product proof.

## Historical I-01 example

Use [the published v0.1.4 spec](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/c9e11829da397192338459b9d8c58edbe5d7860d/docs/assurance/specs/i-01-encryption-patch-paired-tests.md).
Tested source `ee19f83e9e13d4f98790a6fbfe09dd4bd087ad29`, base
`52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`, actual merge candidate
`0babdeb0748c59bd2c552fd3c1a06bb8b63515ef`, build 64324.

Map the inspected encrypted-Increment rejection, caller operation
identity/order, rejected modeled item/ETag state, valid plain-Increment
forwarding-positive control, and retained broad patch witness. Five exact
unit tuples and eight Gateway/Direct x typed/stream x order service tuples
are finite source-derived cells, not whole-SDK denominators.

Retain documented native runs 1331299/1331287 (units) and 1331291/1331290
(service), original expected TRX hashes, task attempt 1 and parent-versus-leaf
counter differences. The ignored neighboring change-feed leaf remains visible.
Published core 3.51.0/3.52.0-preview.0 is not current-core service or produced/
installed nupkg proof. Original TRX, loaded runtime/artifact, MSI/instantiated
fixture and independent oracle/role approval remain unreconciled by this slice.
Never generate native evidence from this prose.

I-02 remains a follow-on/retained-failure example, not implementation work here.
Its docs-only publication
[7330088](https://github.com/Azure/azure-cosmos-dotnet-v3/commit/7330088136240c8586ed5eea63f2a4f9a0c366e5)
reports repaired source `c9e118...`, merge `de046306...`, build 64334. Keep
64329 cancellation/skips and 64330 introduced parity failure/skipped preview
separate from later repaired successes. The docs head is not the tested source;
this slice does not re-investigate its native TRX or claim whole-build closure.

## Acceptance and pending controls

Local acceptance is measurable:

- Deterministic fixtures exercise valid metadata plus every implemented
  format/path/link/cohort/identity/history rejection; expected error codes
  and actual nonzero process exits are asserted.
- Synthetic data cannot pass the public metadata entry as real records.
- Metadata summaries never emit eligibility; RequiredProof never succeeds or
  downgrades to a metadata-only pass.
- The historical example validates its declared metadata while explicitly
  remaining unqualified and incomplete for current-candidate proof.
- YAML source has one secretless OneES/PowerShell record-validation job,
  includes PR Markdown, publishes bounded summaries and changes no existing
  SDK pipeline/filter/retry/fixture semantics.
- No dependencies or tooling are installed. Missing required PowerShell is a
  concrete blocker, not a successful skip or substitute result.

Actual protected-entry removal/input-weakening controls, native attempt/artifact
collection, service/fixture adequacy, oracle approval, independent judgments/
executed challenge and operating-profile admission are **pending**. Synthetic
fixtures cannot pass those controls. Initial implementation is not M-01.5.

## Risk and preservation neighbors

Treat JSON/TRX as untrusted data: reject duplicate keys and unsafe paths,
disable XML DTD/external entities, and never execute referenced artifacts.
Unknown report shapes fail rather than counting a parent as a terminal leaf.

A candidate can modify its own YAML/script. Protected evaluator trust cannot
be implemented by a candidate-controlled `trusted=true`, pass summary, pinned
base-script call, or status name. The new definition is source only and is
not registered, required or activated by this work.

Preserve spec/history pins, original failed attempts, supported lifecycle
states, all six visible SDK comparison cells, source/package boundaries,
existing tests/pipelines, shared policy ownership and retained frozen audits.
G-01-G-12 remain the bounded plan's backlog; this slice mainly supplies
G-04/G-07/G-10/G-11 validation foundations and the existing I-01 G-02/G-03/
G-05/G-08 example, not gap closure.
