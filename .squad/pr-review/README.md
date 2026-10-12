# Cosmos DB PR Review Playbook

A repeatable workflow for reviewing PRs across the 8 Cosmos DB SDK surfaces with **per-SDK context**, **persistent reviewer memory**, **cross-SDK pollination**, **engineering-quality coverage** (code quality, testing, perf, agentic), and **Cosmos design-doc grounding** — built as a glue layer over your existing Copilot agents.

## What this gives you

- One entry point: **`PR Review Router`** picks the right pipeline, loads the right context, and routes output the way you want.
- **6-dimension review coverage** — correctness (PR Deep Reviewer), code quality / testing / performance / agentic AI-readiness (`Engineering Quality Reviewer`), and Cosmos design alignment (`cosmos-pr-reviewer`). Single source of truth: `engineering-quality-rubric.md`.
- **Cosmos design-doc grounding** — every domain finding cites a `cosmosdb-design-docs/<chapter>.md#<anchor>` from the canonical [`tvaron3/cosmosdb-design-docs`](https://github.com/tvaron3/cosmosdb-design-docs) repo. Curated path→chapter map plus PR-title/body keyword auto-grep.
- **Pre-flight verification gates** — every "missing test / missing benchmark / breaks convention / diverges from peer" finding requires a `verified_by_grep:` proof line OR is auto-downgraded to a `nit` tagged `unverified-pattern-suggestion`. The PR #5792 lesson, hard-coded.
- Persistent memory per SDK so your "always check / stop flagging" preferences carry across reviews. Memory entries are tagged by `category` so each sub-agent gets dimension-relevant context only.
- Cross-SDK parity hints injected automatically when a PR touches a parity-tracked area.
- Feedback loop (`PR Review Feedback`) that updates memory after each review with a `--category <dim>` flag — same pattern as `review_design_doc` / `review_feedback`.
- Zero changes to the underlying review agents' core behavior — the router augments via dispatch prompts only.
- **Structured Findings contract (v3.1).** Every dispatched reviewer emits a fenced ` ```structured-findings ` YAML block at the end of its output; the router parses ONLY this block during merge (prose remains for human reading). Required keys: `id`, `severity`, `category`, `file`, `message`. See `router-design.md` "Structured Findings schema" for the full schema.
- **`--peers` works on every tier where cross-SDK review is active** (v3.1 fix — Tier 2 was previously ignoring user peer overrides).

## SDKs in scope

| SDK key | Repo |
|---|---|
| dotnet | Azure/azure-cosmos-dotnet-v3 |
| java | Azure/azure-sdk-for-java (sdk/cosmos) |
| python | Azure/azure-sdk-for-python (sdk/cosmos) |
| go | Azure/azure-sdk-for-go (sdk/data/azcosmos) |
| rust | Azure/azure-sdk-for-rust (sdk/cosmos) |
| spark | azure-cosmos-spark connector |
| kafka | microsoft/kafka-connect-cosmosdb |
| spring-data | azure-spring-data-cosmos |

Sandbox paths are listed in `cross-sdk-peers.md`.

## How to invoke

```
@PR Review Router <pr-url>
@PR Review Router owner/repo#<N> --tier 2 --output file
@PR Review Router <pr-url> --tier 3 --interactive --peers java,dotnet --output post
@PR Review Router <pr-url> --explain --no-memory
```

### Flags
- `--tier 1|2|3` — override auto-detection.
- `--peers <csv>` — replace the default peer SDKs.
- `--interactive` — adds Interactive Deep Reviewer (Tier 3 only).
- `--output console|file|post` — default `file`. `post` requires explicit approval at the gate.
- `--explain` — print tier-score rationale and what context was loaded (including matched design-doc chapters).
- `--no-memory` — skip per-SDK memory injection (useful when memory is suspect).
- `--no-design-docs` — skip Cosmos design-doc grounding (rare; for non-Cosmos infra PRs).

## Tiers

| Tier | When | Pipeline |
|---|---|---|
| 1 — Triage | Small / doc-only / low-risk | `Fresh Eyes Reviewer` only |
| 2 — Standard | Default | `PR Deep Reviewer` (orchestrates Past Review + Cross SDK + Fresh Eyes + Context Builder) **+ `Engineering Quality Reviewer`** (code quality / testing / perf / agentic — Dimensions 1, 2, 3, 5) — parallel |
| 3 — Deep | Public-API / refactor / perf-sensitive | Tier 2 **+ `cosmos-pr-reviewer`** (Dimension 4 — design alignment with citations) + optionally `Interactive Deep Reviewer` |

Auto-detection scoring is described in `router-design.md`. Use `--explain` to see the score for a given PR.

## Review dimensions (the 6-dimension rubric)

Every Tier 2/3 review covers (rubric at `engineering-quality-rubric.md`):

| # | Dimension | Owned by |
|---|---|---|
| 1 | **Code quality** — observability, supportability, debuggability, API hygiene, complexity, allocations | `Engineering Quality Reviewer` |
| 2 | **Testing quality** — existence, path coverage, failure-mode coverage, concurrency, determinism, regression scope, benchmark gating | `Engineering Quality Reviewer` |
| 3 | **Performance** — hot-path identification, allocation hotspots, lock contention, async patterns, payload efficiency | `Engineering Quality Reviewer` |
| 4 | **Cosmos design alignment** — every domain finding cites a `cosmosdb-design-docs/<chapter>.md#<anchor>` | `cosmos-pr-reviewer` |
| 5 | **Agentic / AI readiness** — streaming async for LLM, structured I/O contracts, idempotency-safe retry, observable for LLM diagnostics, tool-callable public API | `Engineering Quality Reviewer` |
| — | **Correctness, parity, security, API, style** | `PR Deep Reviewer` + `Cross SDK Reviewer` |

The reviewer agents output dimension-tagged findings. The router merges them in Stage 6 and surfaces them in Stage 7 grouped by severity (`blocker | major | minor | nit`) with separate sections for `## Design alignment` and `## Verified-negative findings (rigor receipts)`.

## Output sinks

- **console** — Findings printed in chat.
- **file** *(default)* — `~/.copilot/session-state/pr-review/findings/<owner>_<repo>_pr<N>.md`. Always written, even when another sink is also active.
- **post** — Delegates to `PR Deep Reviewer` Step 7 (its existing posting helper using `gh` CLI). Always passes through the human gate first; the router never calls `gh pr review` directly.

## Feedback loop — keep memory sharp

After every review, run:

```
@PR Review Feedback --sdk <sdk> [--pr <ref>] --rating 4 \
  --good "caught the retry leak" \
  --bad "noisy on test files" \
  --always-check "verify session token consistency on multi-region writes" \
  --stop-flagging "missing XML doc on internal types" \
  --category correctness
```

The `--category <dim>` flag (new in v3) tags each entry by dimension so the router can filter active context per sub-agent. Valid values: `correctness | code_quality | testing | perf | security | api | parity | design_alignment | agentic | style | other`. One `--category` per invocation, applied uniformly to all entries added/updated. Omit it (defaults to `other`) only for cross-cutting preferences.

That updates `memory/<sdk>.json` with governance fields (`source`, `confidence`, `last_used_at`, `scope`, `category`) and enforces bounds (30/30/50/25). When a `--recurring` pattern crosses `seen_count >= 3`, it suggests promoting it into the SDK pack.

## File layout

```
~/.copilot/agents/
  PR Review Router.agent.md            ← entry point (v3)
  PR Review Feedback.agent.md          ← memory updater (with --category flag)
  PR Deep Reviewer.agent.md            ← Tier 2/3 omnibus + posting helper (existing)
  Engineering Quality Reviewer.agent.md ← Tier 2/3 — Dimensions 1, 2, 3, 5 (NEW v3)
  Fresh Eyes Reviewer.agent.md         ← Tier 1 (existing)
  Cross SDK Reviewer.agent.md          ← Tier 2/3 peer reviewer (existing)
  cosmos-pr-reviewer.agent.md          ← Tier 3 — Dimension 4 with design-doc citations (extended v3)
  Interactive Deep Reviewer.agent.md   ← Tier 3 with --interactive (existing)

~/.copilot/session-state/pr-review/
  README.md                            ← this file
  router-design.md                     ← v3 design spec (authoritative)
  engineering-quality-rubric.md        ← single source of truth for the 6-dimension rubric (NEW v3)
  design-docs-map.md                   ← curated path→chapter map + auto-grep heading list (NEW v3)
  cross-sdk-peers.md                   ← canonical peer map (user-editable)
  sdk-packs/
    _template.md                       ← required sections (extended v3 with 4 new sections)
    {dotnet,java,python,go,rust,spark,kafka,spring-data}.md  ← all extended v3
  memory/
    _schema.md                         ← v1 schema + governance fields + bounds + category enum
    {dotnet,java,python,go,rust,spark,kafka,spring-data,_global}.json
  findings/                            ← file output sink

~/Data/microsoft/sandbox/cosmosdb-design-docs/  ← canonical Cosmos concept docs (read-only by reviewers)
```

## Editing tips

- **SDK pack** → edit when guidance becomes permanent (style rule, recurring footgun, parity map cell, hot-path identification).
- **Memory JSON** → let `PR Review Feedback` write to it; only hand-edit to clean up.
- **Peer map** → edit `cross-sdk-peers.md` to add a new connector or rebalance peers.
- **Engineering-quality rubric** → edit `engineering-quality-rubric.md` directly. The Engineering Quality Reviewer reads it on every dispatch, so changes propagate immediately without redeploying any agent.
- **Design-doc map** → edit `design-docs-map.md` when a new SDK area appears (add a row to the per-SDK table) or when the upstream `cosmosdb-design-docs` repo gains a new chapter (add a row to the chapter inventory + heading list).
- **Cosmos concepts (canonical docs)** → contribute via PR to `tvaron3/cosmosdb-design-docs` (per `copilot-instructions.md` repo rules: feature branch + PR, no direct push to main).
- **Router behaviour** → edit `router-design.md` first, then mirror the change into `PR Review Router.agent.md`.

## Hard rules (recap)

1. Never bypass `PR Deep Reviewer` Step 6 human gate.
2. Never call `gh pr review --comment` from anywhere except `PR Deep Reviewer` Step 7.
3. Never paste raw memory JSON or full SDK packs to sub-agents — only the distilled `ACTIVE_REVIEW_CONTEXT` (filtered by category per Stage 3c).
4. Never paste a full design-doc chapter into a sub-agent prompt — only matched `##` section excerpts, capped at 400 tokens total.
5. Never modify underlying agents' core prompts — augment via dispatch prompt only.
6. One PR fetch per invocation — cached, never re-fetched.
7. **Pre-flight verification gate:** never let a "missing X / breaks convention / diverges from peer" finding through without either a `verified_by_grep` proof line OR an explicit `unverified-pattern-suggestion` tag at severity `nit`. (PR #5792 lesson.)
8. Never silently drop a `design_alignment` finding during merge — it gets its own Stage 7 section.

## Next: dogfood

Run on 2-3 real PRs across different SDKs (one core, one connector, ideally one with cross-SDK overlap). Confirm:
- New dimensions surface at least one useful finding per PR (not noise).
- Pre-flight gates suppress at least one would-be false-positive.
- Design-doc citations land on the right chapter.
- No duplicate findings between Engineering Quality Reviewer and PR Deep Reviewer (Stage 6 merge handles this).

If any of those fail → iterate on the rubric / agent prompt before declaring v3 stable. Refine packs / memory / peer map / router based on what breaks. Then promote stable findings into SDK packs.
