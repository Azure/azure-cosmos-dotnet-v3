---
last_updated: 2026-10-12T15:00:00.000Z
---

# Team Wisdom

Reusable patterns and heuristics learned through work. NOT transcripts — each entry is a distilled, actionable insight.

## Patterns

- **Pattern:** Pre-flight Verification Gate (PR #5792 lesson). **Context:** Any claim of "missing test", "missing benchmark", or "breaks convention" MUST include a `verified_by_grep:` verification line or be downgraded to `nit` with tag `unverified-pattern-suggestion`.
- **Pattern:** Parameterized Test Data Rows. **Context:** Before asserting missing test coverage in Cosmos .NET, always inspect `[DataRow]`, `[DataTestMethod]`, `[Theory]`, and `[InlineData]` on existing tests. Names with `BasedOnAccountType` or `BothAccountTypes` indicate multi-scenario coverage already exists.
- **Pattern:** Mandatory Cross-SDK Reality Check. **Context:** Before asserting cross-SDK parity gap or shared anti-pattern, verify the peer SDK repository actually contains the equivalent code. Never reason from analogy alone.
- **Pattern:** Read-Only Review Safety. **Context:** All reviews are read-only. Ralph and Squad work monitoring must not autonomously assign, execute, or post comments to GitHub issues or pull requests.
- **Pattern:** Mandatory PR Review Router. **Context:** All pull request reviews and re-reviews MUST go through the `PR Review Router` orchestrator. Never use sub-reviewers directly as the primary reviewer.
