# PR Review Router — design note (v3.2, post-critique hardening)

> Supersedes v3.1 (which superseded v3 and v2). v3.1 → v3.2 changes (additive; no breaking parse changes):
> - Restores `Fresh Eyes Reviewer` as the Tier 1 / transitive low-context reviewer and adds `Skeptic Lens` as a Tier 2/3 adversarial overlay.
> - `Skeptic Lens` focuses on sink assumptions, concrete uncovered edge cases, rollback story, cheap falsification tests, and material context gaps. It emits the same mandatory `structured-findings` block as other reviewers.
> - Adds `PR Test Model Reviewer` as a read-only Tier 2/3 artifact producer for practical coverage, Cosmos conceptual gaps, and recommended tests.
> - Adds `CI Artifact Reviewer` for build/CI/package/generated/security-adjacent release paths.
> - Adds `industry_review_practices_path` to `ACTIVE_REVIEW_CONTEXT` and requires all dispatched reviewers to apply the distilled industry + AI-review rubric before finalizing findings.
> - Adds `policy_paths` to `ACTIVE_REVIEW_CONTEXT`, plus conditional `Security Reviewer` routing and a post-merge `Review Quality Gate`.
> - Tier 2 standard dispatch is now `PR Deep Reviewer` + `Engineering Quality Reviewer` + `PR Test Model Reviewer` + `Skeptic Lens`, plus `CI Artifact Reviewer` when applicable; Tier 3 deep dispatch adds `cosmos-pr-reviewer` when domain-surface rules match.

> v3 → v3.1 changes (additive; no breaking parse changes):
> - **Structured Findings YAML appendix is now mandatory** for every dispatched reviewer (PR Deep, EQ, cosmos, Cross SDK, Fresh Eyes, Skeptic Lens, PR Test Model, CI Artifact). The router's Stage 6 merge parses the structured block as the source of truth; format-specific prose is a fallback. Schema documented in router Stage 5 ("Structured Findings appendix") and recapped below.
> - **Pre-flight verification gate is now enforced on `PR Deep Reviewer` too** (was only enforced on EQ + cosmos). Missing-test / diverges-from-peer / breaks-convention findings without `verified_by_grep` are downgraded by `PR Deep Reviewer` itself, with a router-side safety net catching anything that slips through.
> - **Router-side safety net strengthened** to trigger on message pattern words (`missing`, `no test`, `no benchmark`, `diverges from`, `breaks convention`, `should have a test`, `should benchmark`, `add a test`, `add a benchmark`) **regardless of category**. Previously the safety net was category-gated and missed findings whose category had been mis-normalized.
> - **Scope filters injected via dispatch prompts** so `PR Deep Reviewer` and `cosmos-pr-reviewer` don't double-flag findings owned by `Engineering Quality Reviewer`. PR Deep suppresses pure testing/perf/agentic findings unless correctness-critical; cosmos-pr-reviewer limits itself to Dimension 4 + propose-design-doc-extension. Architectural / process-level dupes (no file:line) are now deduped by `(category, message similarity ≥ 0.7)`.
> - **`--peers` propagation extended to Tier 2** — previously only Tier 3 dispatches received the explicit peer list, so Tier 2 cross-SDK runs ignored user `--peers` overrides. Now any tier where cross-SDK review is active receives the peer list.
> - **`ACTIVE_REVIEW_CONTEXT` canonical shape extended** with `pr_ref`, `pr_url`, `tier`, `risk_score`, `risk_breakdown`, `changed_files`, `sdk_pack_path`, `industry_review_practices_path`, and `peers` — these fields were previously expected by sub-agents but not produced by Stage 3.
> - **Console output mode reconciled** — `--output console` no longer contradicts itself by claiming "save nothing" while also "always also write the file". The file is always written; console mode prints inline AND tells the user the file path.
> - **`design-docs-map.md` keyword section restructured** to a deterministic `keyword → chapter#anchor` table with a `default_anchor` column on the chapter inventory. Anchor selection algorithm (curated path with no keyword match → default_anchor; multiple `##` matches → first in document order; explicit map → wins) eliminates run-to-run nondeterminism.

> v3.1 changes are additive — the v3 schema still parses; legacy reviewer outputs without the structured-findings block fall through to prose extraction; tier scoring + memory + design-doc grounding are unchanged.

### Structured Findings schema (v3.1 sub-agent contract — recap)

Every dispatched reviewer ends its output with a fenced ` ```structured-findings ` YAML block. Required keys: `id`, `severity`, `category`, `file`, `message`. Optional keys: `line_start`, `line_end`, `evidence`, `suggested_fix`, `verified_by_grep`, `design_doc_citation`, `tags`. Severity enum: `blocker | major | minor | nit`. Category enum (11 values): `correctness | code_quality | testing | perf | security | api | parity | design_alignment | agentic | style | other`. Reviewers who emit prose-only severity schemes (PR Deep emojis; cosmos `[CRITICAL ISSUES]` brackets) map to the enum in the YAML block. Zero findings → `structured-findings: []`. Findings with no clean (file, line) — process-level observations — set `file: <none>`, `line_start: 0`. The router parses ONLY this block during merge; prose stays for human reading.

### Original v2 → v3 changes (preserved for history)

> - **New `Engineering Quality Reviewer` agent** dispatched in parallel with `PR Deep Reviewer` on every Tier 2/3 review (covers code quality, testing depth, performance, agentic / AI-readiness — Dimensions 1, 2, 3, 5 of the rubric).
> - **`cosmos-pr-reviewer` extended** to own Dimension 4 (Cosmos design alignment) with mandatory citations against `tvaron3/cosmosdb-design-docs` chapters.
> - **Pre-flight verification gates** (PR #5792 lesson) baked in at three layers: rubric, sub-agent prompts, and router-side merge safety net.
> - **Cosmos design-doc grounding** wired into Stage 3 via curated path→chapter map + keyword auto-grep against the PR title/body, capped at 4 chapters / 400 tokens.
> - **Memory schema** gains an additive `category` field (11-value enum) and `verified_by_grep_command` on recurring patterns; backward-compatible.
> - **SDK packs** gain four new sections: `perf_sensitive_paths`, `test_entry_points`, `observability`, `agentic_consumer_surfaces`.
> - **Finding schema** extends severity to `blocker | major | minor | nit` (was `blocker | important | nit` — old `important` maps to `major`) and category to the 11-value enum.
> - **Output sectioning** adds `## Design alignment`, `## Verified-negative findings (rigor receipts)`, and `## What we explicitly did NOT flag` so the user sees the full review surface, not just actionable items.
> - **`PR Review Feedback`** gains a `--category <dim>` flag so feedback can be tagged by dimension and filtered into per-agent context.
> - **`--no-design-docs`** flag to skip Cosmos design-doc grounding for non-Cosmos infra PRs (rarely needed).

> v2 → v3 changes are additive — Tier 1 (Fresh Eyes only) is unchanged; the v2 finding schema still parses; legacy memory entries without `category` continue to work (treated as `other`).

## Shape decision

The router is a thin orchestrator agent at `~/.copilot/agents/PR Review Router.agent.md`.
It is **thin**: its only jobs are routing, distilled context injection, finding merge,
approval gate, and output sink dispatch. It does NOT itself review code.

Justification (vs a shell script):
- Dynamic sub-agent dispatch via the `task` tool.
- Prompt-time distilled context/memory injection (memory + design-doc map).
- Merge/dedupe of findings from multiple reviewers + pre-flight verification gate enforcement.
- Graceful degraded-mode handling when one reviewer fails.

If the router is ever stripped of those four responsibilities, downgrade it to a script.

## Invocation contract

```
PR Review Router: <github-pr-url-or-owner/repo#N>
  [--tier {1|2|3|auto}]            default: auto
  [--output {console|file|post}]   default: file
  [--peers <sdk1,sdk2,...>]        override Cross-SDK peer selection
  [--no-memory]                    skip memory injection (for clean baseline)
  [--no-design-docs]               skip Cosmos design-doc grounding (rare; non-Cosmos infra PRs only)
  [--explain]                      print tier-scoring rationale
  [--interactive]                  Tier 3 only: also run Interactive Deep Reviewer
```

Output `file` writes to `~/.copilot/session-state/pr-review/findings/<owner>_<repo>_pr<N>.md`.

## Stage 0 — single stat-only fetch

Before any other work, fetch:
- PR metadata (title, body, labels, base/head, author)
- Changed-files list with size stats (additions/deletions/path)

Reused by SDK detection, tier scoring, peer selection, and downstream prompts.
**One fetch only** — do not duplicate per sub-agent.

## SDK detection (uses changed files, not heuristic guessing)

Map from `owner/repo` + path prefix:

| owner/repo | path prefix | sdk |
|---|---|---|
| Azure/azure-cosmos-dotnet-v3 | * | dotnet |
| Azure/azure-sdk-for-java | sdk/cosmos/azure-cosmos-spark_* | spark |
| Azure/azure-sdk-for-java | sdk/spring/azure-spring-data-cosmos | spring-data |
| Azure/azure-sdk-for-java | sdk/cosmos/* | java |
| Azure/azure-sdk-for-python | sdk/cosmos/* | python |
| Azure/azure-sdk-for-go | sdk/data/azcosmos/* | go |
| Azure/azure-sdk-for-rust | sdk/cosmos/* | rust |
| microsoft/kafka-connect-cosmosdb | * | kafka |
| Azure/azure-cosmosdb-spark | * | spark (legacy — flag in findings) |

For monorepos, primary SDK = the one with the most changed source files (excluding
generated/test). If multiple SDKs are touched in one PR (rare but real), warn the
user and pick the dominant; offer `--peers` override.

## Tier auto-pick (scored model, not first-match)

Compute a risk score; thresholds map to tier. Print the breakdown when `--explain`
or whenever the auto-pick differs from the simple LOC heuristic.

| Signal | Weight | Notes |
|---|---|---|
| Touches public API surface | +4 | path patterns per SDK pack |
| Touches retry / auth / serialization / query / diagnostics | +3 each (max +6) | path patterns per SDK pack |
| Source-LOC delta ≥ 1500 | +3 | excluding generated, test, doc |
| Source-LOC delta 300–1499 | +1 | |
| Source files changed ≥ 30 | +2 | excluding generated/test |
| Label: `breaking-change` / `api-change` | +4 | |
| Label: `safe` / `dependencies` / `docs-only` | −3 | |
| Title/body keyword: refactor, rewrite, GA, breaking | +2 | one-shot, not stacked |
| Source-vs-(test+doc) ratio < 0.2 | −2 | mostly tests/docs |
| All changed files match doc/sample/changelog/yaml | force Tier 1 | overrides score |

Thresholds: score ≥ 6 → Tier 3; score 2–5 → Tier 2; score < 2 → Tier 1.
User `--tier` always wins. Router prints chosen tier + score + top 3 contributors.

## Pipeline composition (no full-stack duplication)

| Tier | Composition |
|---|---|
| **1 — Triage** | `Fresh Eyes Reviewer` only. Console output by default. |
| **2 — Standard** | `PR Deep Reviewer` (orchestrates Past Review, Past PR Investigator, Review Context Builder, Cross SDK Reviewer, Fresh Eyes) **+ `Engineering Quality Reviewer`** (Dimensions 1, 2, 3, 5 from the rubric) **+ `PR Test Model Reviewer`** (read-only test model) **+ `Skeptic Lens`** (adversarial assumption / edge-case / rollback overlay) **+ `CI Artifact Reviewer` when build/CI/package/generated/security-adjacent release files changed** **+ `Security Reviewer` when security-sensitive signals match** — dispatched in parallel. Then `Review Quality Gate` runs on merged findings. |
| **3 — Deep** | `PR Deep Reviewer` (primary) **+ `Engineering Quality Reviewer`** (always) **+ `PR Test Model Reviewer`** (always) **+ `Skeptic Lens`** (always) **+ `CI Artifact Reviewer` when applicable** **+ `Security Reviewer` when applicable** **+ `cosmos-pr-reviewer`** (Dimension 4 — required when PR touches query / CRUD / consistency / partitioning / session / replication surface). All applicable reviewers in parallel. Then `Interactive Deep Reviewer` only if `--interactive`; `Review Quality Gate` runs on merged findings. |

**Engineering Quality Reviewer** lives at `~/.copilot/agents/Engineering Quality Reviewer.agent.md` and reads its rubric from `~/.copilot/session-state/pr-review/engineering-quality-rubric.md` on every dispatch (so rubric edits propagate without redeploying the agent). It owns Dimensions 1 (code quality), 2 (testing), 3 (performance), and 5 (agentic / AI-readiness). It explicitly does NOT own Dimension 4 (design alignment — that's `cosmos-pr-reviewer`) or correctness/parity (that's `PR Deep Reviewer` + `Cross SDK Reviewer`).

**`cosmos-pr-reviewer`** owns Dimension 4 (design alignment). Every domain finding it produces must cite `cosmosdb-design-docs/<chapter>.md#<anchor>`. If the PR introduces a concept missing from the design docs, it surfaces a `propose-design-doc-extension` item in its `[DESIGN ALIGNMENT]` block with a chapter recommendation and a one-paragraph stub.

**`Skeptic Lens`** is not a dimension owner. It is a risk overlay that asks what assumption would sink the change, which edge cases are still uncovered, and whether rollback / falsification / context are strong enough. It should not duplicate ordinary correctness, engineering-quality, or design-alignment findings unless it has a stronger sink-assumption framing.

**`PR Test Model Reviewer`** owns the review artifact for existing coverage, uncovered changed paths, Cosmos-domain conceptual test gaps, and recommended test scenarios. It is read-only; test generation remains a separate explicitly approved workflow.

**`CI Artifact Reviewer`** owns CI / release-artifact regression checks for build matrices, package metadata, generated-code baselines, changelog/APIView/docs, security-adjacent release paths, and CI-vs-shipped-artifact parity. It runs only when the PR touches those surfaces.

**`Security Reviewer`** owns high-confidence security review when security-sensitive signals match. It suppresses speculative hardening advice unless there is a concrete trust boundary and production-realistic impact.

**`Review Quality Gate`** owns final finding hygiene: tone, evidence, required-vs-optional labeling, severity calibration, duplicate suppression, and downgrade/suppress decisions. It does not review code.

Rationale: Dimension responsibilities are explicit per-agent so we get coverage without duplication. The router's Stage 6 merge dedupes any overlap between EQ Reviewer and PR Deep Reviewer at the `(file, line ± 3)` level, while PR Test Model Reviewer contributes a dedicated test artifact, Skeptic Lens contributes independent risk framing, CI Artifact Reviewer covers non-source release regressions, Security Reviewer covers security-sensitive trust boundaries, and Review Quality Gate keeps final output high-signal.

## Cosmos design-doc grounding (Dimension 4 substrate)

The canonical Cosmos concept docs at [`tvaron3/cosmosdb-design-docs`](https://github.com/tvaron3/cosmosdb-design-docs) (cloned to `~/Data/microsoft/sandbox/cosmosdb-design-docs/`) are the source of truth for every domain finding. The router injects them into ACTIVE_REVIEW_CONTEXT in Stage 3 via two complementary mechanisms (both ON by default — disable with `--no-design-docs`):

1. **Curated path→chapter map** at `~/.copilot/session-state/pr-review/design-docs-map.md` — a per-SDK lookup table where each row is `path-prefix-substring → chapter-file`. The router scans every touched file in the PR; any match emits the corresponding chapter as a candidate.
2. **Keyword auto-grep** — the router tokenizes the PR title and body (≥4-char tokens, lowercased), matches against the `## ` heading list at the bottom of design-docs-map.md and against chapter file names, and emits matched chapters as candidates.

The candidate list is unioned, deduped, and capped at 4 chapters total (curated wins on ties; chapters appearing in BOTH lists win). For each kept chapter the router opens the file and pulls only the matched `##` section (heading + first paragraph), capped at 400 tokens total across the sub-block. The result is the `design_doc_chapters` block in ACTIVE_REVIEW_CONTEXT.

**Living-doc sync.** At the start of every Tier 2/3 review the router runs `git -C ~/Data/microsoft/sandbox/cosmosdb-design-docs pull --ff-only origin main` (best-effort). If the pull fails (network, dirty tree, missing clone) it records `design_docs_sync: stale` in the output but proceeds with whatever is on disk — never aborts the review.

**Citation requirement.** Every domain finding in `[DESIGN ALIGNMENT]` and any code-quality / agentic finding that cites a design rationale must include a `Citation: cosmosdb-design-docs/<chapter>.md#<anchor>` line. Findings without a citation are accepted but logged so we can grow the citation muscle over time.

**Living docs (per `copilot-instructions.md`).** When a PR introduces a concept missing from the design docs, the reviewer surfaces a `propose-design-doc-extension` item — chapter recommendation + section heading + one-paragraph stub. The user decides whether to open a follow-up PR against `cosmosdb-design-docs`.

## Pre-flight verification gates (PR #5792 lesson, system invariant)

The PR #5792 dogfood review flagged "missing tests" without first grepping the .NET repo for the test name — the tests existed. To prevent recurrence, the verification requirement is enforced at three independent layers:

1. **Rubric layer** (`engineering-quality-rubric.md` §6) — defines the exact probes for each finding class (`grep -rn "<symbol>" tests/` for "missing test", `grep -rn "<symbol>" benchmarks/` for "missing benchmark", actual `view` of peer SDK file for "diverges from peer", etc.).
2. **Sub-agent prompt layer** — every router dispatch to `Engineering Quality Reviewer` and `cosmos-pr-reviewer` ends with the rubric-quoting line *"…any 'missing X / breaks convention / diverges from peer' finding MUST include a `verified_by_grep:` line with command + output, OR be downgraded to `nit` and tagged `unverified-pattern-suggestion`."*
3. **Router merge safety net** (Stage 6) — if a finding of category `testing | perf | parity | code_quality | agentic` contains pattern words like "missing" / "no test" / "no benchmark" / "diverges from" / "breaks convention" AND lacks `verified_by_grep` AND lacks the `unverified-pattern-suggestion` tag, the router force-downgrades severity to `nit`, adds the tag, and prepends `[UNVERIFIED] ` to the message. Last line of defense.

The user sees the unverified-pattern-suggestions in the `## Nits` section of Stage 7 output — they are not hidden, just clearly labeled as not-yet-verified hypotheses.

## Cross-SDK peer selection (2–3 peers, not all 8)

Default peer map by primary SDK and PR area:

| Primary SDK | Default peers (core SDK PR) | Default peers (connector PR) |
|---|---|---|
| dotnet | java, python | — |
| java | dotnet, python | — |
| python | java, dotnet | — |
| go | java, rust | — |
| rust | go, java | — |
| spark | (n/a — connector) | java (host SDK), kafka |
| kafka | (n/a — connector) | spark, java |
| spring-data | java | — |

Override with `--peers java,python,go`. Cross SDK Reviewer agent is told the explicit
peer list; it does not pick on its own.

## Distilled context injection (not raw paste)

Before dispatching, the router builds a small "active context" block:

```
ACTIVE_REVIEW_CONTEXT:
  sdk: <name>
  industry_review_practices_path: ~/.copilot/session-state/pr-review/industry-code-review-practices.md
  policy_paths:
    prompt_trust_boundaries: ~/.copilot/session-state/pr-review/prompt-trust-boundaries.md
    diff_filtering: ~/.copilot/session-state/pr-review/diff-filtering-policy.md
    comment_quality_gate: ~/.copilot/session-state/pr-review/comment-quality-gate.md
    reviewability_confidence: ~/.copilot/session-state/pr-review/reviewability-confidence-policy.md
    agent_operations: ~/.copilot/session-state/pr-review/agent-operations-policy.md
    security_routing: ~/.copilot/session-state/pr-review/security-routing-policy.md
    reconciliation_workflow: ~/.copilot/session-state/pr-review/reconciliation-workflow.md
  always_check (top 5 by confidence × recency):
    - <id> [<category>]: <text>
  stop_flagging (top 5):
    - <id> [<category>]: <text>
  recurring_patterns relevant to touched paths:
    - <id> [<category>]: <pattern>
      verified_by_grep: <cmd if present>
  parity hints for touched area:
    - <touched-path> ↔ <peer-sdk-path>
  recent_reviews (last 3, summary only):
    - PR#N: <verdict>, <one-line-takeaway>
  design_doc_chapters (top ≤4, ≤400 tokens total):
    - <chapter-file>#<heading-anchor>: <one-line excerpt of the matched ## section>
  context_manifest:
    schema: ~/.copilot/session-state/pr-review/context-manifest.schema.json
    status: complete | partial | degraded
    checks:
      pr_metadata: found | failed
      diff: found | failed
      linked_issues: found | not_found | skipped | failed
      existing_comments: found | not_found | skipped | failed
      design_docs: found | not_found | skipped | failed
      specs: found | not_found | skipped | failed
      tests: found | not_found | skipped | failed
      ci: found | not_found | skipped | failed
      codeowners: found | not_found | skipped | failed
      callers_callees: found | not_found | skipped | failed
      peer_sdks: found | not_found | skipped | failed
    gaps:
      - <severity>: <message>
```

This block — not the raw memory JSON, not the raw context pack, not the full design-doc chapters — is what gets passed to sub-agents. Hard cap ~1500 tokens for the memory portion; the `design_doc_chapters` sub-block has its own ≤400-token budget. Sub-agents may still read full files if the context block points at something specific.

The `context_manifest` is the review's compact completeness contract. It records which context sources were found, not found, skipped, or failed, and it carries material gaps that should lower confidence. The JSON schema lives at `~/.copilot/session-state/pr-review/context-manifest.schema.json`.

The `industry_review_practices_path` points to the general review-quality rubric. Reviewers use it for tone, severity, focus, context confidence, and AI-review noise control. It does not override Cosmos design docs, SDK packs, or user feedback memory when those are more specific.

The `policy_paths` block points to short operational contracts. The router owns enforcement of prompt trust boundaries, diff/noise policy, final comment quality, reviewability confidence, security routing, agent operations, and reconciliation workflow.

**Category-aware filtering** (see router agent Step 3c): when assembling per-sub-agent ACTIVE_REVIEW_CONTEXT, the router filters memory entries by `category` so each agent gets dimension-relevant entries only. EQ Reviewer gets `code_quality | testing | perf | agentic | other`; cosmos-pr-reviewer gets `correctness | design_alignment | parity | api | other`; PR Deep Reviewer gets everything; Cross SDK Reviewer gets `parity | api | correctness | other`; PR Test Model Reviewer gets `testing | correctness | design_alignment | perf | other`; CI Artifact Reviewer gets `testing | security | api | code_quality | other`; Security Reviewer gets `security | correctness | api | other`; Review Quality Gate gets all final merged findings plus policy paths; Fresh Eyes gets everything (no priors filtered out); Skeptic Lens gets everything plus any known context gaps, but uses them only to probe assumptions / rollback / falsification risk.

Legacy memory entries without a `category` field are treated as `other` — they always pass the filter.

## Approval / posting boundary (router owns it)

Sub-agents return findings only. They do NOT post.

`--output post` flow:
1. Run review pipeline → collect findings.
2. Router merges/dedupes findings (see schema below).
3. Router presents the merged findings draft to the user with explicit prompt:
   *"Review the draft above. Reply `post` to publish, `edit` to refine, `cancel` to skip."*
4. On `post`, router invokes the GitHub-posting helper (the same one PR Deep Reviewer's
   Step 7 uses, called directly here — reuse, don't reimplement).

This preserves the human approval gate that PR Deep Reviewer relies on, even when
delegated through the router.

## Finding schema + merge/dedupe

Each finding produced by sub-agents must conform to:

```json
{
  "id": "<sub-agent>-<sequence>",
  "severity": "blocker | major | minor | nit",
  "category": "correctness | code_quality | testing | perf | security | api | parity | design_alignment | agentic | style | other",
  "file": "path/to/file",
  "line_start": 123,
  "line_end": 130,
  "message": "...",
  "evidence": "code snippet or link",
  "suggested_fix": "optional",
  "verified_by_grep": "<command + truncated output> (when the rubric required pre-flight verification)",
  "design_doc_citation": "cosmosdb-design-docs/<chapter>.md#<anchor> (for category=design_alignment, or any domain finding citing the docs)",
  "tags": ["unverified-pattern-suggestion?", "propose-design-doc-extension?"]
}
```

**Severity normalization.** v2 used `blocker | important | nit`. v3 uses `blocker | major | minor | nit`. The router maps legacy `important → major` and unknown values → `minor` so old sub-agent prompts still parse.

**Category normalization.** v2 used 7 values; v3 adds `code_quality | testing | design_alignment | agentic` (11 total). Sub-agents may emit non-enum values — the router maps `bug → correctness`, `tests → testing`, `perf-related → perf`, `docs → other`. Missing category → `other`.

Merge rules:
- Two findings on the same `(file, line_start ± 3)` with overlapping message → keep the more severe; record both `id`s in `merged_from`. When EQ Reviewer and PR Deep Reviewer both flag the same line, prefer the one with `verified_by_grep` populated (stronger evidence) and concatenate `evidence`.
- Cross-SDK parity findings (`category: parity`) are never merged with same-file findings (they're contextual, not bug reports).
- Design-alignment findings (`category: design_alignment`) carrying a `design_doc_citation` are never silently dropped — surfaced separately in Stage 7 output even if the underlying message overlaps with a code-quality or correctness finding.
- `[VERIFIED-NEGATIVE FINDINGS]` from EQ Reviewer are preserved verbatim — they prove what was checked and verified clean, even though they are not actionable.

**Pre-flight gate enforcement (router-side safety net):** see "Pre-flight verification gates" section above for the three-layer scheme.

## Output sectioning (Stage 7)

Findings are surfaced to the user in this order:
1. `## Blockers`
2. `## Major findings`
3. `## Minor findings`
4. `## Nits` (includes `[UNVERIFIED] `-prefixed items downgraded by the pre-flight gate)
5. `## Design alignment` — every finding with `category=design_alignment` OR a non-empty `design_doc_citation`, regardless of severity (includes `propose-design-doc-extension` items)
6. `## Context completeness` — compact `context_manifest` status, failed/skipped checks, and critical/warning gaps
7. `## Verified-negative findings (rigor receipts)` — items EQ Reviewer explicitly checked and verified are NOT issues
8. `## What we explicitly did NOT flag` — covered-and-clean per dimension; collapsed if more than 5 items

This shape lets the user triage actionable items first while still seeing the full review surface (rigor receipts) and design-alignment context.

## Degraded-mode / failure policy

- Each sub-agent dispatch is independent.
- If a sub-agent fails (rate limit, auth, timeout): record the failure, continue with
  remaining agents, mark the final report `partial: true` with the list of skipped
  reviewers and the reason.
- If `PR Deep Reviewer` (Tier 2/3 primary) itself fails: do NOT silently fall back to
  Fresh Eyes. Surface the error and ask the user.
- Auth failure on the GitHub-posting step: keep the findings file; tell the user to
  re-auth and re-run with `--output post`.

## Memory governance (delta — see _schema.md v1+)

The schema additions in v3 are backward-compatible:
- `category` (11-value enum: `correctness | code_quality | testing | perf | security | api | parity | design_alignment | agentic | style | other`) on every `always_check` / `stop_flagging` / `recurring_patterns` entry. Legacy entries without `category` default to `other`.
- `verified_by_grep_command` on `recurring_patterns` entries — a reusable grep command that catches the pattern. The router can run it pre-emptively to verify the pattern still holds before injecting the entry into ACTIVE_REVIEW_CONTEXT.

v2 governance fields still apply (all optional, backward-compatible):
- `source`: who/what added the entry (`feedback-cmd`, `bootstrap`, `manual`)
- `confidence`: 0.0–1.0 (default 1.0 for explicit user feedback)
- `last_used_at`: ISO timestamp; bumped each time the entry is included in active context
- `expires_at`: ISO timestamp; entries past expiry are ignored (default: never)
- `scope`: optional `{repo?, path_prefix?, area?}` to scope an entry to a subset

Precedence (highest first) when building active context:
1. Explicit user request in this session
2. Repo rules (`.pragentrules` or CODEOWNERS hints, if present)
3. Per-SDK memory
4. `_global.json` memory
5. Base agent defaults

Bounds (enforced by feedback-cmd):
- `always_check`: max 30 entries; oldest-by-`last_used_at` evicted first
- `stop_flagging`: max 30
- `recent_reviews`: max 25

**`PR Review Feedback` `--category` flag.** New in v3. One value per invocation, applied uniformly to every entry added/updated in that call. 11-value enum mirrors the memory schema. Default when omitted: `"other"`. Validation: invalid value → stop and report (no partial write); >1 `--category` per invocation → stop. For `--recurring` matched against an existing entry: increments `seen_count`; sets `category` only if the existing entry is legacy (no category); otherwise leaves it untouched.

## After-review prompt

After producing findings, router always reminds the user:

> Run `PR Review Feedback` with `--good`, `--bad`, `--always-check`, `--stop-flagging` (and `--category <dim>` to tag the dimension) so the next review on this SDK stays consistent.

If `Engineering Quality Reviewer` or `cosmos-pr-reviewer` returned a `NOTES FOR FEEDBACK / MEMORY` section, surface their suggested invocations verbatim so the user can copy-paste.

## Open items deferred to implementation

- Exact GitHub-posting helper signature: PR Deep Reviewer's Step 7 needs to be
  inspected at impl time so we call the same helper directly.
- Whether `cosmos-pr-reviewer` accepts a "domain validation only" mode or always
  produces a full review (impacts how Tier 3 dedupes).
- `_global.json` activation policy: only inject when an entry's `scope` matches, or
  always inject top-N? Defer to dogfood phase.
- Whether `_global.json` should also gain `category` entries from cross-SDK lessons.
- "Hot path" heuristic per SDK is now codified in each pack's `perf_sensitive_paths` block — but the router doesn't yet auto-tag findings as "hot path" based on it. Defer to a future iteration if dogfood shows the EQ Reviewer doesn't catch hot-path changes consistently.
