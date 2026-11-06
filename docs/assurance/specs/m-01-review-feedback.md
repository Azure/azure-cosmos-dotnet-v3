# M-01 review-feedback addendum

**Version:** 0.1. **Scope:** corrections to the existing offline validator,
not new record kinds, SDK behavior, native proof or protected-CI activation.

## Pre-implementation baseline and authority

This addendum precedes the executable corrections requested in the submitted
review of [PR #6185](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6185).
The coordinator relayed the user's directive to incorporate open-PR code-review
comments. This is implementation of that feedback, not a new independent
review or oracle approval.

The original [foundation specification](m-01-assurance-foundation.md) and
[format contract](../README.md) remain pinned at
`6cacc4038e349b5d9aa5d1371c33f06505195f7b`. The original implementation is
`9406387a7b253543915eb97254a835b8c76a42fc`, tree
`81e64a812d1079a3340b7469bdd9461461d9bbc7`, based on
`52ff1383daf7a1a0e8f2d77da0ce819c8ea2f2c8`. Preserve these historical
identities and the original approval/plan receipt; do not rewrite them to
describe the corrected candidate.

Commit and push this readable addendum before parser/test changes. The later
mapping update will reference its immutable revision and file-byte digest,
retain the original spec/receipt pins, and declare the additional local
controls without replacing the original 78-control ledger.

## Confirmed defects and required behavior

### Raw JSON array boundaries

[Required comment](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6185#discussion_r4446163374):
PowerShell's `[array]` parameter conversion wraps an object before the
validator checks its original JSON shape. A valid singleton `assertions` array
and its contained object both pass at the baseline. The equivalent `Cases`
boundary has the same defect.

Validate the raw parsed value before any list conversion. Reuse the existing
list check in ID indexing, case validation and cohort hashing; do not add a
second schema engine. Genuine singleton arrays remain arrays. An object,
string, integer, boolean, null or empty list cannot replace a required nonempty
array. Array-element checks and typed argument preservation remain unchanged.
Apply the same rule to the existing indexed-record, required-cell case,
synthetic-context case and cohort-helper entry points.

This corrects metadata format integrity. The reproduction did not bypass the
always-blocked public `RequiredProof` entry.

### Supported TRX result topology

[Non-blocking comment](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6185#discussion_r4446163387):
a same-namespace failed `WebTestResult` added under `Results` is omitted by
the baseline's `UnitTestResult` selection, even with its updated fixture
digest. Reconciliation of the remaining synthetic rows can still succeed.

Before reconciling results, validate the supported VSTest 2010 result
containers. `Results` and each `InnerResults` container may contain only
same-namespace `UnitTestResult` elements. An `InnerResults` container must
belong directly to its containing unit result and occur at most once per
result. Unit results must occur under the root `Results` or a valid containing
`InnerResults`; unfamiliar result kinds, namespaces, wrappers, duplicate
containers and misplaced result nodes fail explicitly as unsupported shapes.
Do not silently skip an unfamiliar result kind or guess how to count it.

Retain valid nested unit-result support, execution IDs and parent links,
original counters, expected terminal reconciliation, and disclosed ignored
neighbors. DTD prohibition, null XML resolvers, digest verification and
existing nonpassing/missing/ambiguous-case checks remain unchanged. No native
qualification bypass was established by the synthetic reproduction.

### Locatable, safe failure diagnostics

[Non-blocking comment](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6185#discussion_r4446163399):
the baseline reports `stale-reference: assertions.sourceRef` without naming
the failing record or assertion.

Keep the error-code prefix and current JSON/native-log escaping. Directory
validation failures must identify the actual repository-relative record
filename. Reference errors must distinguish the spec, provenance-reference
index and actual assertion ID; a stale local-reference diagnostic also names
its validated repository-relative source path. Do not insert absolute
checkout paths, credentials, raw file contents or unvalidated identity text.
Use context only for the record actually being processed; the intentional
post-validation RequiredProof block must not be attributed to the last file.

## Regression plan and preservation

Extend the dependency-free driver and existing synthetic fixture transforms:

- Pair valid singleton arrays with object/scalar/null/empty-list rejection
  through the index, case and cohort helpers. Exercise actual metadata
  assertions and required-cell cases, plus synthetic-context cases; preserve
  typed arguments and distinguish wrong array elements from wrong containers.
- Add unfamiliar same-namespace results under both `Results` and
  `InnerResults`, wrong-namespace results, duplicate/misplaced containers, and
  misplaced unit results. Pair rejection controls with a valid deeper nested
  unit-result case that verifies parent/leaf accounting and ignored debt.
- Use a directory with a valid record followed by an invalid record to assert
  stable error prefixes plus actual filename/reference/assertion context for
  spec, provenance and assertion digest failures. Check escaping and absence
  of absolute checkout paths without depending on the entire error sentence.
- Retain every original named control and its failure semantics. Add the new
  independently declared control IDs/cohort digests and assertion source
  hashes to the existing mapping; the measured new count is an outcome, not
  a target. Preserve pre-fix reproductions and any first failing regression.

Run only the existing offline validator/control driver and bounded direct
metadata/RequiredProof checks. Public metadata, required proof and
qualification remain distinct: `RequiredProof` is blocked, qualification is
not assessed, and no eligibility is emitted. Ordinary automatic PR CI on each
new pushed candidate is a new observation, not inherited green evidence.

No shipped SDK source/tests/dependencies/versions, main pipeline/template
settings, other PRs, service fixtures or historical I-01 records change.
Protected evaluator/producer admission and CI registration/enforcement remain
separate M-01.5 work. No collector, migration, new campaign, manual CI
queue/rerun/cancel, permission change, merge, Direct/Java work or release is
authorized by these corrections.
