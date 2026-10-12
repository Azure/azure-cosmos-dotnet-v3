# Squad Team

> Cosmos DB .NET PR Review Squad — specialist cast implementing the full PR review harness.

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Dispatches work, enforces ownership, handoffs, and reviewer gates. Routes ALL PR reviews to `PR Review Router`. |

## Members

The casting registry contains 15 active review specialists:

| Name | Role | Charter | Status |
|------|------|---------|--------|
| pr-review-router | Lead PR Review Orchestrator & Tier Dispatcher | `.squad/agents/pr-review-router/charter.md` | ✅ Active (Mandatory Entry Point) |
| pr-deep-reviewer | Deep Correctness & Architecture Reviewer | `.squad/agents/pr-deep-reviewer/charter.md` | ✅ Active |
| engineering-quality-reviewer | Engineering Quality Specialist (Dim 1, 2, 3, 5) | `.squad/agents/engineering-quality-reviewer/charter.md` | ✅ Active |
| cosmos-pr-reviewer | Cosmos DB Domain & Architecture Specialist (Dim 4) | `.squad/agents/cosmos-pr-reviewer/charter.md` | ✅ Active |
| cross-sdk-reviewer | Cross-SDK Parity Specialist | `.squad/agents/cross-sdk-reviewer/charter.md` | ✅ Active |
| fresh-eyes-reviewer | Unbiased Fresh Eyes Reviewer (Tier 1 Triage) | `.squad/agents/fresh-eyes-reviewer/charter.md` | ✅ Active |
| ci-artifact-reviewer | CI Pipeline & Shipped Artifact Reviewer | `.squad/agents/ci-artifact-reviewer/charter.md` | ✅ Active |
| security-reviewer | Security & Cryptography Reviewer | `.squad/agents/security-reviewer/charter.md` | ✅ Active |
| skeptic-lens | Adversarial & Edge Case Reviewer | `.squad/agents/skeptic-lens/charter.md` | ✅ Active |
| pr-test-model-reviewer | Test Coverage & Cosmos Failure Modes Reviewer | `.squad/agents/pr-test-model-reviewer/charter.md` | ✅ Active |
| review-context-builder | Codebase Context & Call Graph Explorer | `.squad/agents/review-context-builder/charter.md` | ✅ Active |
| review-quality-gate | Quality Normalizer & Gatekeeper | `.squad/agents/review-quality-gate/charter.md` | ✅ Active |
| past-review | Past PR Triage Reviewer | `.squad/agents/past-review/charter.md` | ✅ Active |
| past-pr-investigator | Past PR Detailed Investigator | `.squad/agents/past-pr-investigator/charter.md` | ✅ Active |
| pr-review-feedback | Review Feedback & Memory Maintainer | `.squad/agents/pr-review-feedback/charter.md` | ✅ Active |

## Built-in Support Agents

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Scribe | Durable Decision Merger | `.squad/agents/scribe/charter.md` | 📋 Silent |
| Ralph | Work Monitor (Read-Only) | `.squad/agents/ralph/charter.md` | 🔄 Monitor (Passive, NO auto-execution) |
| Rai | Responsible AI Policy Reviewer | `.squad/agents/rai/charter.md` | 🛡️ RAI |
| Fact Checker | Devil's Advocate & Claim Verification Agent | `.squad/agents/fact-checker/charter.md` | 🔍 Verifier |

## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Disabled for autonomous PR review actions |

### PR Review Safety Invariant

**MANDATORY RULE:** ALL pull request reviews and re-reviews MUST go through `PR Review Router`. Autonomous code edits, auto-assignment of GitHub issues, and unapproved posting of PR comments or PR merging are strictly forbidden. All review operations are read-only until explicit user approval is granted at Stage 7.
