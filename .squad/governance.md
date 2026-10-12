# Squad Governance

This file defines team-specific ownership, review, and escalation policies for the Cosmos DB PR Review Squad.

## Accountable Ownership

1. Every review task is owned by `pr-review-router`.
2. The router selects the review tier (Tier 1 Triage, Tier 2 Standard, Tier 3 Deep) based on risk scoring and dispatches specialist reviewers.
3. Review specialists advise and emit structured findings; they do not speak on behalf of the squad or post directly to GitHub.
4. All findings must conform to `structured-findings.schema.json`.

## Review Gates & Verification Invariants

1. **Mandatory Pre-flight Verification Gate:**
   - Any claim of "missing test", "missing benchmark", or "breaks convention" MUST include a `verified_by_grep:` verification line proving the absence in the codebase.
   - Any finding lacking reproducible verification is automatically downgraded to severity `nit` and tagged `unverified-pattern-suggestion`.
2. **Cross-SDK Reality Invariant:**
   - Reviewers must never reason by analogy from other SDKs. Before asserting a cross-SDK parity gap, the reviewer must cite verified code or files in the peer repository.
3. **PR Description & Intent Invariant:**
   - Before adding any blocker or major finding, the reviewer must verify that the PR author has not already scoped, documented, or acknowledged the tradeoff in the PR description.
4. **Existing Review Deduplication:**
   - Existing PR comments and resolved threads must be inspected before finalizing findings. Do not re-raise resolved or duplicate points.

## Approval & Posting Authority

1. **Read-Only Default:** All review pipelines output findings to console or a local markdown file by default.
2. **Explicit User Authorization:** Posting review comments, creating review threads, or approving/rejecting PRs on GitHub requires explicit human authorization in Stage 7 (`--output post`).
3. **No Autonomous Code Changes:** The squad does not modify product source code or merge pull requests autonomously.
