# .NET assurance records

This is the bounded M-01 foundation, not a complete SDK contract catalog or an
adopted cross-SDK schema. Shared standards and process remain in
[SuicideSquad at 447c6608](https://github.com/kushagraThapar/SuicideSquad/blob/447c660872ceaced30a5b5c60abec3bc5c54eb7b/docs/dotnet/design-handoff.md).
SDK specifications, assertion mappings and compact evidence indexes belong here;
executable tests and CI artifacts remain in their normal locations.

The [foundation specification](specs/m-01-assurance-foundation.md) precedes this
slice's executable changes. The existing paired-test work in
[#6182](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6182) is a separate
workstream. This branch neither imports nor replaces its specs or tests.

## What validation means

`metadata=valid` means only that the supported local record format, references,
digests and mappings passed the implemented checks. It is not test execution,
independent oracle approval, protected admission, qualification or merge
eligibility. Candidate-authored JSON, YAML and pass summaries are untrusted.

The following observations remain separate:

| Observation | Establishes | Does not establish |
|---|---|---|
| Declared | A spec/source/filter declares a rule or case | Discovery or execution |
| Inspected | A particular source/assertion was read at a pin | Independent correctness authority |
| Discovered | Methods or expanded cases were discovered; distinguish which | Execution of every expected row |
| Executed | Exact terminal outcomes for a candidate, binding and attempt | Adequacy of assertions or oracle |
| Independently approved | An attributable authorized independent decision | Qualification of another candidate |
| Qualified | Separately admitted evaluation of all applicable proof | Anything a local metadata check can issue |

Historical documentation reporting native execution is `native-reported`, not
independently reconciled raw evidence. Unknowns are retained with their proof
impact. They can be valid metadata, but cannot satisfy required proof.

## Version 1 record contract

JSON is UTF-8. Reject duplicate keys before conversion, unknown fields, unknown
versions/enums, duplicate IDs/cases, unsafe paths and missing required references.
Hash file bytes with SHA-256; do not normalize newlines before hashing.

The first slice supports `change` and `historical-example`. `run-index`,
`audit-index` and producer-only `native-receipt` are reserved for later admitted
work and fail explicitly if submitted as first-slice records. No collector,
artifact downloader, migration or live result store is implemented here.

### Common fields

| Field | Required value |
|---|---|
| `formatVersion` | Integer `1` |
| `kind` | `change` or `historical-example` |
| `id` | Nonempty stable SDK-local identifier; unique across the record directory |
| `recordState` | `proposed`, `active`, or `superseded`; a historical example cannot be active |
| `policyRef` | `repository`, immutable `revision`, `path`, and `version`; this is a declaration, not independent admission |
| `provenance` | `sourceType`, `verification`, `references`, `limits` |
| `unknowns` | Array of objects with `id`, `reason`, `proofImpact`; do not fill unknowns with successful defaults |
| `supersedes` | Optional object with `id`, `sha256`, `reason`; never changes an original result |

`sourceType` is `candidate-authored`, `published-documentation`, or
`synthetic-fixture`. `verification` is `declared`, `inspected`, or
`native-reported`. These are provenance labels, not approvals. The public
metadata command rejects synthetic records; fixtures use only pure validation
functions and cannot become live proof.

A source reference has exactly `repository`, `revision`, `path`, `sha256`.
Git references use a repository name, a full commit and a repository-relative
path. Local references use `repository: "self"` and must exist, remain inside the
checkout without symlink traversal, and match their file-byte digest. A local
candidate assertion can have `revision: null`: its actual final candidate is
emitted by future CI, not embedded self-referentially in its own commit. That
unknown must be disclosed. Immutable external references are syntax-checked
only; the validator never downloads or attests their contents.

### Change/spec mapping

Both supported kinds require the following fields in addition to the common
fields. This keeps the historical example useful without turning it into an
active contract.

| Field | Required contents |
|---|---|
| `specRef` | Source reference to the readable spec, distinct from the original pre-implementation receipt |
| `preImplementationReceipt` | `kind`, original `sha256`, `reference`, `attribution`, `verification`; optional `approvalLabel`, `approvalQuestion`, `approvalTimestamp` |
| `impact` | `paths`, `obligationIds`, `gapIds`, `preservation`, `intentionalChanges` |
| `obligations` | Objects with `id`, `version`, `lifecycle`, `preconditions`, `allowed`, `forbidden`, `interactions`, `scenarios`, `authority` |
| `assertions` | Objects with `id`, `obligationIds`, `scenarioIds`, `project`, `sourceRef`, `symbol`, `observes`, `limits` |
| `requiredCells` | Objects with `id`, `obligationIds`, `scenarioIds`, `assertionIds`, `layer`, `variant`, `tfm`, `binding`, `cases` |
| `expectedCohorts` | Objects with `cellId`, explicit `caseIds`, `sha256`, independently declared `origin`; observed rows cannot define the expected set |
| `peerComparison` | Objects with `sdk`, `disposition`, `reason`, `proofImpact`; .NET, Java, Python, Go, Rust and JS/TS are always visible |
| `decisions` | Array of immutable decision-reference strings; empty is not independent approval |

All required arrays except `unknowns`, `decisions`, and `intentionalChanges` are
nonempty. Each obligation's outcome/scenario arrays are nonempty. IDs and links
are unique and resolve within their containing record. `gapIds` use G-01 through
G-12; they are navigation, not a coverage denominator.

`lifecycle` is `supported`, `preview`, `deprecated`, `removed`, `unsupported`, or
`unresolved`. `authority` has `status` (`unresolved` or
`independently-approved`) and `reference` (null or immutable reference string).
A claimed independent approval requires its reference but is still not
authenticated by metadata validation. Each scenario has `id`, `kind`
(`positive`, `negative`, or `boundary`) and `expectation`.

`layer` is `unit`, `emulator`, or `authorized-isolated-live`. A case has `id`,
typed scalar `arguments`, and `nativeName` (string or null). Supported scalar
arguments in this slice are strings, booleans, Int64 integers and null; other
shapes fail rather than losing type/precision. Null means native
label reconciliation is missing, not that a method name establishes expanded
rows. Native names and typed tuples must be unique within a cell.

The cohort digest hashes UTF-8 bytes of compact JSON with no trailing newline:
case objects sorted by ordinal `id`, each written in `id`, `arguments`,
`nativeName` property order. Arguments retain their declared order and type.
This detects stale declared cohorts; it does not make their origin independent.

`binding` describes the actual intended source/package/consumer boundary. Keep
surface, TFM and dependency mode separate: an ordinary Custom task using
3.41.0-preview.0 is not a GA-core dependency. A local validator property does
not acquire a fictitious service observer. Independent applicability,
meaningful paired product proof and native integration remain separate.

PowerShell assertion symbols in local `.ps1` references must resolve to actual
function definitions using the PowerShell parser, not text matches.

### Historical execution summary

Only `historical-example` requires `execution`:

- `source`, `base`, `mergeCandidate`: immutable commits, never replaced by a
  later documentation head.
- `binding`, `discovery`, `assessment`: explicit bounded descriptions.
- `runs`: objects with `id`, `layer`, `variant`, `taskAttempt`,
  `requiredCaseIds`, `trxSha256`, `reportedTerminal`, `reportedParent`,
  `reportedRawTotal`, `passedTerminal`, `ignoredTerminal`, `locator`.

Counters are nonnegative integers or null where the source does not establish
them. Keep API parents, raw TRX aggregates and terminal leaves distinct.
Hashes are **documented expected hashes**, not a claim that the validator
retrieved originals. Each run names its required cells by case ID.

Large/raw TRX, logs and packages stay in native artifact storage. An SDK index
must carry identity, outcomes, limits and retained artifact references rather
than only a moving URL.

## Offline evidence controls

The script also contains pure functions exercised by synthetic fixtures:

- Compare exact source/base/merge/tree, policy/spec/cohort, binding, artifact,
  runtime and fixture identities against an independently supplied test input.
- Parse the supported VSTest 2010 TRX shape with DTD/external entities disabled;
  preserve aggregate counters, parents, terminal leaves and execution IDs.
- Reconcile the complete declared expected set, not just nonempty results.
  Missing, duplicate, ambiguous, failed, skipped or inconclusive required leaves
  fail. Unrelated ignored leaves remain disclosed.
- Preserve failed prior attempts; do not replace them with a retry's pass.
- Detect deletion/weakening of previous obligations, required cells, lifecycle
  and policy inputs against an explicitly supplied base record.

These are deterministic tests of validation logic, not evidence that a protected
native entry, SDK service fixture, oracle or independent role has been admitted.
The public `RequiredProof` mode always fails closed in this slice because no
protected producer/evaluator is admitted. It cannot fall back to metadata
success. No command emits eligibility.

## Local commands

Use the already available PowerShell 7 runtime; no dependency installation:

```powershell
pwsh -NoLogo -NoProfile -File scripts/tests/Validate-Assurance.Tests.ps1
pwsh -NoLogo -NoProfile -File scripts/Validate-Assurance.ps1 -Mode Metadata
pwsh -NoLogo -NoProfile -File scripts/Validate-Assurance.ps1 -Mode RequiredProof
```

The last command is expected to return nonzero with the pending admission
reasons. `-OutputPath` optionally writes a JSON summary outside the records
directory. A failure summary is explicitly invalid/blocked, never success-shaped.
The script performs no network, SDK build/test, emulator or live operation.

## Native CI source, not activation

`azure-pipelines-assurance.yml` calls
`templates/assurance-validation.yml` for small secretless record/self-check
validation on PRs to `main` and `releases/*`, including Markdown. It does not
alter the main pipeline's Markdown exclusions or duplicate its SDK test matrix.
The YAML has not registered a definition, made a check required, pinned an
independent evaluator, or granted any new permission.

Before trusted proof consumption, separate owners must establish protected
invocation outside candidate control, raw attempt/artifact retention,
actual runtime/fixture identity, valid operating profiles, independently
grounded oracles and Router-led judgments/test challenge. Removing a
candidate-controlled job is not prevented by another candidate-controlled file.
Those admission controls remain pending, not passed by the synthetic fixtures.

Controlled retirement, exceptions, policy/evaluator changes and migration
retain history and need independent authorization. Neither deleting an
obligation nor an ignored test improves coverage. No historical audit is
imported, no new tooling is admitted, and Java's workstream remains frozen.
