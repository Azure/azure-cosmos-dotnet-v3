# Squad Decisions

## Active Decisions

### DEC-001: Establish Cosmos DB .NET PR Review Squad Pilot with Full Router Harness

- **Date:** 2026-10-12
- **Status:** Approved
- **Accountable Specialist:** `pr-review-router` / `architect`
- **Context:**
  The Cosmos DB team requires a repository-native multi-agent review capability that preserves the complete multi-tiered PR review harness, 6-dimension engineering quality rubric, pre-flight verification gates, canonical Cosmos design-doc grounding, and per-SDK reviewer memory, without compromising read-only safety.
- **Decisions:**
  1. **Pinned Squad Source:** Initialized from `bradygaster/squad` at commit `aa81f08dedd0c0943e59db71a0be625e2ea23304` (v1.0.0 specification profiles).
  2. **Mandatory Entry Point:** `PR Review Router` is the sole and mandatory orchestrator for all pull request reviews and re-reviews. No sub-agent may be substituted as a primary reviewer.
  3. **Read-Only Safety:** Squad operates in read-only review mode. Autonomous PR comment posting, code modifications, and PR merging are prohibited without explicit human authorization at Stage 7.
  4. **Disabled Autonomous Issue Execution:** Ralph and Squad monitoring are configured for passive, read-only status checks. Autonomous issue assignment and execution (`copilot-auto-assign: false`) are disabled.
  5. **Transferable Harness Layout:** All reusable policies, schemas, validators, evals, SDK packs, and sanitized memory reside in `.squad/pr-review/`. Private runtime artifacts (caches, local findings, diff dumps) are strictly excluded from git tracking.
  6. **Pre-flight Verification Gate:** The PR #5792 lesson is permanently codified: any claim of missing coverage or broken conventions must be substantiated by a `verified_by_grep:` verification line or be downgraded to `nit` with tag `unverified-pattern-suggestion`.

## Governance

- Every work item has one accountable specialist selected through `.squad/routing.md`.
- Review and escalation follow `.squad/governance.md`; consensus is not required.
- PR review findings follow the `structured-findings.schema.json` contract.
