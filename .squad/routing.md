# Work Routing

Dispatch ownership and routing table for the Cosmos DB PR Review Squad.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| **Pull Request Review (All Tiers, All Types)** | **`pr-review-router`** | **MANDATORY:** "review PR #123", "@squad review", follow-up re-reviews, triage |
| Deep Correctness & Public API Architecture | `pr-deep-reviewer` | Dispatched by Router for Stage 4 Tier 2 & Tier 3 |
| Engineering Quality (Code Quality, Testing, Perf, Agentic) | `engineering-quality-reviewer` | Dispatched by Router for Stage 4 Tier 2 & Tier 3 |
| Cosmos DB Domain Alignment & Design Docs | `cosmos-pr-reviewer` | Dispatched by Router for Stage 4 Tier 3 (Dimension 4) |
| Cross-SDK Parity & Peer Verification | `cross-sdk-reviewer` | Dispatched by Router for Stage 4 Tier 2 & Tier 3 |
| Low-Risk / Triage / Unbiased Review | `fresh-eyes-reviewer` | Dispatched by Router for Stage 4 Tier 1 |
| CI Matrix, Shipped Artifacts & Packaging | `ci-artifact-reviewer` | Dispatched by Router for CI, packaging, or release paths |
| Security, Auth, Cryptography, Tokens | `security-reviewer` | Dispatched by Router for security risk paths |
| Adversarial Edge Cases & Falsification | `skeptic-lens` | Dispatched by Router as skeptical overlay |
| Cosmos Failure Modes & Test Coverage Model | `pr-test-model-reviewer` | Dispatched by Router for conceptual test modeling |
| Codebase Context, Callers, Callees | `review-context-builder` | Dispatched by Router for context manifest gathering |
| Finding Tone, Severity & Quality Gating | `review-quality-gate` | Dispatched by Router for Stage 6 normalization |
| Past Review Triage & Senior Patterns | `past-review` | Dispatched by Router for historical review mining |
| Detailed Past PR Investigation | `past-pr-investigator` | Dispatched by Router or user to investigate past PR diffs |
| Review Feedback & Per-SDK Memory Sync | `pr-review-feedback` | "review feedback: good/bad", "update memory", "always check" |

## Dispatch Rules

1. **Mandatory Review Router Rule:** ALL PR reviews, re-reviews, one-off reviews, and PR triage MUST be routed directly and unconditionally to `pr-review-router`. Sub-reviewers must NEVER be dispatched as standalone primary reviewers.
2. **Read-Only Safety Invariant:** All review actions are strictly read-only. Posting comments to GitHub or merging PRs requires explicit user authorization at Stage 7.
3. **Evidence Verification Rule:** Any claim of "missing test", "missing benchmark", or "breaks convention" MUST include a `verified_by_grep:` verification line or be downgraded to `nit` with tag `unverified-pattern-suggestion`.
4. **Autonomous Issue Execution Disabled:** Ralph and Squad monitoring must NOT auto-assign or auto-execute GitHub issues or pull requests.
5. Squad coordinates. Scribe, Ralph, Rai, Fact Checker, and `@copilot` are never primary routing destinations for PR reviews.
