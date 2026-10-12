# Cosmos DB PR Review Squad

This directory contains the repository-native **Squad pilot** for Azure Cosmos DB .NET SDK (`Azure/azure-cosmos-dotnet-v3`), implementing the complete multi-tiered PR review harness.

## Architecture & Principles

1. **Mandatory Review Router:** All PR reviews and re-reviews MUST go through `PR Review Router` (`.squad/agents/pr-review-router/charter.md`). Individual review specialists are dispatched conditionally based on risk tier and changed file paths.
2. **Read-Only Safety Invariant:** All review operations are read-only. Autonomous code generation, auto-assignment of GitHub issues, and unapproved posting of PR comments or PR merging are strictly forbidden. Posting to GitHub requires explicit human authorization in Stage 7 (`--output post`).
3. **Passive Work Monitoring:** Autonomous issue execution (`copilot-auto-assign: false`) is disabled. Ralph and Squad monitoring run only on explicit user request for status reporting.
4. **Pre-flight Verification Gates:** Any finding asserting missing tests, missing benchmarks, or broken conventions must be substantiated by a `verified_by_grep:` verification line or be downgraded to `nit` with tag `unverified-pattern-suggestion`.
5. **Cosmos DB Canonical Grounding:** Domain findings (Dimension 4) are grounded in the canonical [`tvaron3/cosmosdb-design-docs`](https://github.com/tvaron3/cosmosdb-design-docs) repository.
6. **Per-SDK Reviewer Memory:** Persistent maintainer preferences and learnings are maintained in `.squad/pr-review/memory/dotnet.json`.

## Specialist Roster

| Specialist ID | Role | Rubric / Focus |
|---------------|------|----------------|
| `pr-review-router` | Lead PR Review Orchestrator | Stage 0-7 coordination, tier scoring, context manifest, merge |
| `pr-deep-reviewer` | Deep Correctness & Architecture | Correctness, regression detection, public API surface |
| `engineering-quality-reviewer` | Engineering Quality Specialist | Dimensions 1, 2, 3, 5 (code quality, testing, perf, agentic) |
| `cosmos-pr-reviewer` | Cosmos Domain Specialist | Dimension 4 (Cosmos distributed semantics & design-doc citations) |
| `cross-sdk-reviewer` | Cross-SDK Parity Specialist | Peer SDK reality check (Java, Python, Go, Rust, Spark) |
| `fresh-eyes-reviewer` | Unbiased Fresh Eyes Reviewer | Tier 1 Triage & clean-slate code readability review |
| `ci-artifact-reviewer` | CI & Shipped Artifact Reviewer | Build matrices, package manifests, changelog gating |
| `security-reviewer` | Security & Cryptography Reviewer | Auth tokens, keys, encryption, TLS, secret protection |
| `skeptic-lens` | Adversarial & Edge Case Reviewer | Riskiest assumptions, uncovered edge cases, falsification tests |
| `pr-test-model-reviewer` | Test Coverage Model Reviewer | Practical test scan + conceptual Cosmos failure mode modeling |
| `review-context-builder` | Context & Call Graph Explorer | Codebase callers, callees, sibling patterns, existing tests |
| `review-quality-gate` | Quality Normalizer & Gatekeeper | Tone, severity calibration, comment quality gate enforcement |
| `past-review` | Past PR Triage Reviewer | Git file history mining for senior reviewer precedents |
| `past-pr-investigator` | Past PR Detailed Investigator | Isolated deep-dive on historical PR diffs and discussions |
| `pr-review-feedback` | Feedback & Memory Maintainer | Captures review reactions and updates per-SDK memory store |

## How to Run a PR Review

### Using Copilot CLI

In Copilot CLI or chat:
```
@PR Review Router Azure/azure-cosmos-dotnet-v3#<PR_NUMBER>
```
or with explicit options:
```
@PR Review Router Azure/azure-cosmos-dotnet-v3#<PR_NUMBER> --tier 2 --output file
@PR Review Router Azure/azure-cosmos-dotnet-v3#<PR_NUMBER> --tier 3 --explain
```

### Using the Safe Script Runner

A repo-local safe invocation script is provided at `.squad/scripts/review-pr.sh`:
```bash
./.squad/scripts/review-pr.sh Azure/azure-cosmos-dotnet-v3#6137
./.squad/scripts/review-pr.sh 6137 --tier 2 --output file
./.squad/scripts/review-pr.sh 6137 --dry-run
```

## Structure

```
.squad/
├── config.json               # Squad configuration (stateBackend: local)
├── team.md                   # Team roster and member capabilities
├── routing.md                # Work routing and dispatch rules
├── decisions.md              # Architectural decisions (DEC-001)
├── ceremonies.md             # Review calibration and memory sync ceremonies
├── governance.md             # Review gates, invariants, and approval rules
├── casting/                  # Cast policy, registry, and history
├── identity/                 # now.md and wisdom.md
├── agents/                   # Specialist charters (<id>/charter.md)
└── pr-review/                # Transferable PR review harness
    ├── README.md             # Review playbook
    ├── router-design.md      # Router architecture (Stages 0-7)
    ├── engineering-quality-rubric.md # 6-dimension rubric
    ├── schemas/              # JSON schemas (structured-findings, manifest, etc.)
    ├── validators/           # validate-structured-findings.mjs
    ├── evals/                # corpus.json, score-review.mjs
    ├── scripts/              # review-pr.sh, reconcile-review.mjs
    ├── sdk-packs/            # dotnet.md SDK pack
    └── memory/               # dotnet.json and _global.json reviewer memory
```
