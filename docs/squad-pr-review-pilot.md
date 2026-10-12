# Cosmos DB .NET PR Review Squad Pilot

This document describes the repository-native **Squad pilot** in `Azure/azure-cosmos-dotnet-v3`, integrating the complete multi-tiered PR review harness as a cohesive team of specialists.

## Overview

The PR Review Squad establishes an in-repo multi-agent architecture (based on the [Squad](https://github.com/bradygaster/squad) specification) that unifies:
- **Mandatory PR Review Router** as the entry point for all pull request reviews.
- **6-Dimension Engineering Quality Rubric** (code quality, testing depth, performance, Cosmos DB design alignment, agentic / AI-readiness, and functional correctness).
- **Pre-flight Verification Gates** (the PR #5792 lesson: any claim of missing tests or broken conventions must be proven via `grep` or be downgraded to `nit`).
- **Cosmos DB Canonical Grounding** (`cosmosdb-design-docs` chapter citations).
- **Per-SDK Reviewer Memory** (`.squad/pr-review/memory/dotnet.json`).
- **Read-Only Safety Invariant** (no autonomous posting or merging; explicit user confirmation required).

## Specialist Roster

The squad is organized under `.squad/agents/` with 15 active review specialists and 4 support identities:

| Specialist | Role | Key Responsibility |
|------------|------|--------------------|
| `pr-review-router` | Lead PR Review Orchestrator | Stage 0-7 coordination, tier scoring, context manifest, finding merge |
| `pr-deep-reviewer` | Deep Correctness Reviewer | Functional correctness, regression detection, public API surface |
| `engineering-quality-reviewer` | Engineering Quality Specialist | Dimensions 1, 2, 3, 5 (code quality, testing, perf, agentic readiness) |
| `cosmos-pr-reviewer` | Cosmos Domain Specialist | Dimension 4 (Cosmos distributed semantics & design-doc citations) |
| `cross-sdk-reviewer` | Cross-SDK Parity Specialist | Peer SDK reality check (Java, Python, Go, Rust, Spark) |
| `fresh-eyes-reviewer` | Unbiased Fresh Eyes Reviewer | Tier 1 Triage & clean-slate readability review |
| `ci-artifact-reviewer` | CI & Shipped Artifact Reviewer | Build matrices, package manifests, changelog gating |
| `security-reviewer` | Security & Cryptography Reviewer | Auth tokens, keys, encryption, TLS, secret protection |
| `skeptic-lens` | Adversarial & Edge Case Reviewer | Riskiest assumptions, uncovered edge cases, falsification tests |
| `pr-test-model-reviewer` | Test Coverage Model Reviewer | Practical test scan + conceptual Cosmos failure mode modeling |
| `review-context-builder` | Context & Call Graph Explorer | Codebase callers, callees, sibling patterns, existing tests |
| `review-quality-gate` | Quality Normalizer & Gatekeeper | Tone, severity calibration, comment quality gate enforcement |
| `past-review` | Past PR Triage Reviewer | Git file history mining for senior reviewer precedents |
| `past-pr-investigator` | Past PR Detailed Investigator | Isolated deep-dive on historical PR diffs and discussions |
| `pr-review-feedback` | Feedback & Memory Maintainer | Captures review reactions and updates per-SDK memory store |
| `ralph` | Work Monitor (Read-Only) | Passive board monitoring; autonomous execution is disabled |
| `scribe` | Decision Merger | Merges accepted architectural decisions into `.squad/decisions.md` |
| `rai` | Responsible AI Reviewer | Privacy, credential leakage, and AI safety gating |
| `fact-checker` | Claim Verifier | Challenges assumptions and validates repository existence |

## Safety & Governance Invariants

1. **Mandatory Review Router:** All pull request reviews and re-reviews MUST be routed through `PR Review Router`. No individual specialist is ever invoked as a standalone primary reviewer.
2. **Read-Only Operation:** All review pipelines are read-only.
3. **Explicit User Approval:** Posting review comments to GitHub or modifying PR status requires explicit human confirmation (`--output post`).
4. **No Autonomous Code Changes:** The squad does not modify product source code or merge pull requests autonomously.
5. **No Autonomous Issue Execution:** Autonomous issue polling and execution (`copilot-auto-assign: false`) are disabled in Ralph and Squad monitoring.
6. **Pre-flight Verification:** Any assertion that tests or benchmarks are missing must include a `verified_by_grep:` verification line proving the absence in the codebase, or be downgraded to `nit` with tag `unverified-pattern-suggestion`.
7. **Cross-SDK Reality Invariant:** Cross-SDK reasoning by analogy is forbidden. Reviewers must cite verified peer repository code before asserting parity gaps.

## Running a PR Review

### 1. In Copilot CLI or IDE Chat

Review a PR with automatic tier selection:
```
@PR Review Router Azure/azure-cosmos-dotnet-v3#6137
```

Specify tier or output mode:
```
@PR Review Router Azure/azure-cosmos-dotnet-v3#6137 --tier 2 --output file
@PR Review Router Azure/azure-cosmos-dotnet-v3#6137 --tier 3 --explain
```

### 2. Using the Safe Review Runner Script

A repo-local helper is available at `.squad/scripts/review-pr.sh`:
```bash
# Dry run: fetch PR, calculate risk score and tier, list roster
./.squad/scripts/review-pr.sh 6137 --dry-run

# Run full review and save to .squad/pr-review/findings/
./.squad/scripts/review-pr.sh 6137 --tier 2 --output file
```

### 3. Review Feedback & Memory Loop

After reviewing findings, provide feedback to maintain team memory:
```
@PR Review Router feedback --good "Findings on volatile read were spot-on" --always-check "Check TokenCredentialCache volatile reads" --category auth
```

## Repository & State Hygiene

- **Transferable vs. Private State:** Transferable policies, rubrics, schemas, validators, evals, and clean memory templates reside in `.squad/pr-review/`.
- **Excluded Runtime Artifacts:** Runtime findings (`.squad/pr-review/findings/`), cache files, reconciliation outputs, and logs are excluded from git via `.gitignore`.
- **Merge Drivers:** Append-only squad files (`decisions.md`, `wisdom.md`, `ceremonies.md`, `history.json`) are configured with `merge=union` in `.gitattributes`.
EOF
