# OpenSpec — Spec-Driven Development for the Azure Cosmos DB .NET SDK

This directory contains [OpenSpec](https://github.com/openspec-dev/openspec) artifacts for the Azure Cosmos DB .NET v3 SDK. OpenSpec provides a structured, AI-assisted workflow for proposing, specifying, designing, and implementing changes.

## Table of Contents

- [Why OpenSpec?](#why-openspec)
- [Quick Start](#quick-start)
- [Directory Structure](#directory-structure)
- [Workflow Overview](#workflow-overview)
- [Slash Commands Reference](#slash-commands-reference)
- [Writing Good Specs](#writing-good-specs)
- [When to Create or Update Specs](#when-to-create-or-update-specs)
- [Example Walkthrough](#example-walkthrough)
- [Best Practices](#best-practices)
- [Anti-Patterns](#anti-patterns)
- [FAQ](#faq)

---

## Why OpenSpec?

The Cosmos DB .NET SDK is a large, complex codebase (~1,400+ source files) with many interdependent subsystems — retry policies, handler pipelines, cross-region routing, change feed processing, query execution, and more. OpenSpec helps by:

1. **Capturing behavioral contracts** — Specs define *what* a feature should do (invariants, edge cases, error handling), not *how* it's implemented. This makes them durable even as implementation evolves.

2. **Guiding AI-assisted development** — AI agents (GitHub Copilot, etc.) use specs as context when proposing and implementing changes, leading to more accurate code generation.

3. **Reducing tribal knowledge** — Complex features like PPAF, cross-region hedging, and the handler pipeline have subtle invariants that are easy to break. Specs make these invariants explicit and reviewable.

4. **Structured change management** — Every change goes through a propose → design → implement cycle, creating a reviewable paper trail.

---

## Quick Start

### Prerequisites

- **OpenSpec CLI** installed: `npm install -g openspec-cli`
- **GitHub Copilot** (VS Code or CLI) with the OpenSpec skills available

### Your first change in 3 steps

**Step 1: Propose a change**

Use the `/opsx:propose` slash command in Copilot Chat, or run manually:

```
/opsx:propose add-retry-logging
```

This creates `openspec/changes/add-retry-logging/` with three artifacts:
- `proposal.md` — What and why
- `design.md` — How (architecture, key decisions)
- `tasks.md` — Implementation steps

**Step 2: Implement the change**

```
/opsx:apply add-retry-logging
```

The AI reads all artifacts for context and works through tasks one by one, marking each complete.

**Step 3: Archive when done**

```
/opsx:archive add-retry-logging
```

Moves the change to `openspec/changes/archive/YYYY-MM-DD-add-retry-logging/` and syncs any spec updates to the main spec catalog.

---

## Directory Structure

```
openspec/
├── config.yaml              # Project context and artifact rules
├── README.md                # This file
├── specs/                   # Main spec catalog (living documentation)
│   ├── crud-operations/
│   │   └── spec.md
│   ├── retry-and-failover/
│   │   └── spec.md
│   ├── handler-pipeline/
│   │   └── spec.md
│   └── ...
├── changes/                 # Active changes (in-progress work)
│   ├── add-retry-logging/
│   │   ├── .openspec.yaml
│   │   ├── proposal.md
│   │   ├── design.md
│   │   └── tasks.md
│   └── archive/             # Completed changes
│       └── 2026-03-01-add-retry-logging/
│           ├── .openspec.yaml
│           ├── proposal.md
│           ├── design.md
│           └── tasks.md
```

### Key concepts

| Concept | Location | Purpose |
|---------|----------|---------|
| **Specs** | `openspec/specs/<feature>/spec.md` | Living behavioral contracts for major feature areas. The source of truth for how features should behave. |
| **Changes** | `openspec/changes/<name>/` | In-progress work with proposal, design, and task artifacts. Ephemeral — archived when complete. |
| **Archive** | `openspec/changes/archive/` | Completed changes with full context preserved. Useful for understanding why decisions were made. |
| **Config** | `openspec/config.yaml` | Project-level context and per-artifact rules that guide AI when creating artifacts. |

---

## Workflow Overview

OpenSpec uses the `spec-driven` schema with four artifact types:

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   Proposal   │────▶│    Specs      │────▶│    Design    │────▶│    Tasks     │
│  (what/why)  │     │  (contracts)  │     │    (how)     │     │   (steps)    │
└──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                                                                       │
                                                                       ▼
                                                               ┌──────────────┐
                                                               │    Apply     │
                                                               │ (implement)  │
                                                               └──────────────┘
                                                                       │
                                                                       ▼
                                                               ┌──────────────┐
                                                               │   Archive    │
                                                               │  (complete)  │
                                                               └──────────────┘
```

### Artifact descriptions

- **Proposal** (`proposal.md`): Defines *what* you're changing and *why*. Includes scope, non-goals, and backward compatibility analysis.
- **Specs** (`specs/`): Behavioral contracts — what the feature should do, error cases, invariants, public API surface. These may reference or update the main spec catalog.
- **Design** (`design.md`): *How* the change will be implemented. References specific source files, classes, and architectural decisions.
- **Tasks** (`tasks.md`): Ordered implementation steps with checkboxes. Each task should be independently testable.

---

## Slash Commands Reference

Use these in GitHub Copilot Chat (VS Code or CLI):

### `/opsx:propose <name>`
Create a new change and generate all artifacts in one step.

```
/opsx:propose fix-session-retry-race
```

The AI will:
1. Create `openspec/changes/fix-session-retry-race/`
2. Generate proposal.md, design.md, and tasks.md
3. Show a summary of what was created

### `/opsx:apply [name]`
Implement tasks from a change. The AI reads all artifacts for context and works through tasks sequentially.

```
/opsx:apply fix-session-retry-race
```

The AI will:
1. Read proposal, specs, design, and tasks
2. Show progress (e.g., "Working on task 3/7")
3. Make code changes and mark tasks complete
4. Pause on blockers and ask for guidance

### `/opsx:explore [topic]`
Enter explore mode — a thinking partner for investigating ideas or problems *without* making code changes.

```
/opsx:explore how does the retry handler interact with PPAF?
```

Great for:
- Understanding existing behavior before proposing changes
- Comparing design approaches
- Investigating bugs or unexpected behavior
- Clarifying requirements

### `/opsx:archive [name]`
Archive a completed change. Moves it to the archive directory and optionally syncs spec updates.

```
/opsx:archive fix-session-retry-race
```

---

## Writing Good Specs

Specs in this repo capture **behavioral contracts** for major SDK feature areas. They should answer: "If I'm an AI agent (or a new developer), what do I need to know to safely modify this feature?"

### What a spec should include

1. **Purpose** — One-paragraph summary of what the feature does
2. **Public API surface** — Key types, methods, and their contracts
3. **Behavioral invariants** — Rules that must always hold (e.g., "Retry handler must not retry 4xx responses except 429")
4. **Error handling** — What errors can occur and how they're handled
5. **Configuration** — Options, defaults, and their effects
6. **Edge cases** — Non-obvious behaviors, race conditions, failure modes
7. **Interactions** — How this feature interacts with other SDK components
8. **References** — Links to existing design docs, source files, and external documentation

### Example: Retry spec snippet

```markdown
## Behavioral Invariants

1. **Throttle retries (429)**: The SDK retries using the delay from the `x-ms-retry-after`
   response header, up to `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests`
   (default: 9).

2. **Cross-region retries**: HTTP 403/SubStatus 3 (write region changed) triggers
   account refresh and retry on the new write region. This is handled by
   `ClientRetryPolicy`, not `ResourceThrottleRetryPolicy`.

3. **CancellationToken**: User-provided `CancellationToken` MUST be honored and can
   stop any retry loop at any point. No retry policy may ignore cancellation.

4. **Timeout boundaries**: Direct mode retries are bounded to 30 seconds by
   `GoneAndRetryWithRequestRetryPolicy`. If exhausted, returns HTTP 503.
```

### What a spec should NOT include

- Implementation details (specific variable names, internal algorithms)
- Performance benchmarks (these change; use test projects instead)
- Step-by-step code walkthroughs (that's what `docs/SdkDesign.md` is for)

---

## When to Create or Update Specs

### Create a new spec when:
- Adding a **new major feature** to the SDK (e.g., a new availability strategy, a new query pipeline stage)
- An area has **complex invariants** that are easy to break without documentation
- You find yourself explaining the same behavioral rules in multiple PR reviews

### Update an existing spec when:
- Your PR **changes behavior** covered by a spec (e.g., modifying retry logic, adding a new change feed mode)
- A **bug fix** reveals an invariant that wasn't captured in the spec
- A **design doc** in `docs/` gets updated — check if the corresponding spec needs updating too

### Don't need a spec for:
- Pure refactoring with no behavioral change
- Test-only changes
- Documentation or comment updates
- Dependency version bumps

### PR checklist integration

When submitting a PR, consider:
- [ ] Does this PR change behavior covered by an existing spec in `openspec/specs/`?
- [ ] If yes, have I updated the relevant spec?
- [ ] If this is a new feature, should I create a spec for it?

---

## Example Walkthrough

Let's walk through adding a new feature: **adaptive retry backoff for throttled requests**.

### 1. Explore the problem

```
/opsx:explore adaptive retry backoff
```

The AI investigates the current retry behavior, reads the retry spec and source code, and discusses approaches with you. No code changes are made.

### 2. Propose the change

```
/opsx:propose adaptive-retry-backoff
```

The AI creates three artifacts:

**proposal.md** — Explains why fixed backoff can be suboptimal and proposes exponential backoff with jitter as an alternative.

**design.md** — Details how `ResourceThrottleRetryPolicy` will be modified, including:
- A new `RetryBackoffStrategy` enum
- Changes to `CosmosClientOptions` for configuration
- Interaction with existing `x-ms-retry-after` headers

**tasks.md** — Implementation steps:
```markdown
- [ ] Add `RetryBackoffStrategy` enum (Fixed, ExponentialWithJitter)
- [ ] Add `RetryBackoffStrategy` property to `CosmosClientOptions`
- [ ] Modify `ResourceThrottleRetryPolicy.ShouldRetryAsync` to use strategy
- [ ] Add unit tests for exponential backoff calculation
- [ ] Add unit tests for jitter bounds
- [ ] Add emulator tests for end-to-end throttle retry with new strategy
- [ ] Update retry-and-failover spec with new invariants
```

### 3. Review and refine

Read the generated artifacts and adjust if needed. The artifacts are regular markdown files — edit them directly or ask the AI to refine.

### 4. Implement

```
/opsx:apply adaptive-retry-backoff
```

The AI works through tasks, making focused code changes and marking each complete.

### 5. Archive

After the PR is merged:
```
/opsx:archive adaptive-retry-backoff
```

---

## Best Practices

### For specs
- **Be specific about invariants** — "Retries are bounded to 30 seconds" is better than "Retries have a timeout"
- **Include error codes** — Document which HTTP status codes and substatus codes trigger specific behaviors
- **Reference source files** — Link to the actual source files (e.g., `Microsoft.Azure.Cosmos/src/ClientRetryPolicy.cs`)
- **Keep specs up to date** — A stale spec is worse than no spec. Update specs as part of behavioral changes.
- **One spec per feature area** — Don't create specs per class. Group related behavior (e.g., all retry policies in one spec)

### For changes
- **Start with `/opsx:explore`** — For complex changes, explore before proposing. This produces better proposals.
- **Keep changes focused** — One change per feature or fix. Don't bundle unrelated work.
- **Let the AI draft first, then refine** — The AI's first pass captures the structure; your refinements add domain expertise.
- **Archive after merge** — Don't leave completed changes hanging in `openspec/changes/`. Archive them to keep the workspace clean.

### For the team
- **Review specs in PRs** — When a PR includes spec changes, review the spec diff like you would code.
- **Reference specs in PR descriptions** — "This PR implements the behavior described in `openspec/specs/retry-and-failover/spec.md`"
- **Use explore mode for design discussions** — Instead of long Slack threads, use `/opsx:explore` to investigate and capture insights.

---

## Anti-Patterns

| ❌ Anti-Pattern | ✅ Better Approach |
|---|---|
| Copying implementation details into specs | Capture *behavioral contracts* — what should happen, not how the code does it |
| Creating a spec for every class | One spec per *feature area* (e.g., `retry-and-failover` covers all retry policies) |
| Letting specs go stale | Update specs as part of the PR that changes behavior |
| Skipping explore for complex changes | Invest thinking time upfront to produce better proposals |
| Archiving before the PR is merged | Archive only after the implementation is merged and verified |
| Duplicating content from `docs/` | Reference existing design docs. Specs add behavioral contracts on top. |
| Making specs too long | Focus on invariants and edge cases. If it reads like a tutorial, it's too detailed. |

---

## FAQ

### How does this relate to the existing docs in `docs/`?

The `docs/` directory contains **design documents** — they explain *how* the SDK works (architecture, data flows, component interactions). OpenSpec specs capture **behavioral contracts** — they define *what* must be true (invariants, error handling, API guarantees).

Think of it as: `docs/SdkDesign.md` tells you how the handler pipeline is structured. The `handler-pipeline` spec tells you what rules the pipeline must follow (ordering constraints, handler responsibilities, what happens when a handler throws).

Both are valuable. Specs reference design docs where relevant.

### Can I edit spec files directly?

Yes. Specs are regular markdown files. You can edit them in any editor. The OpenSpec CLI and skills work with the files — they don't own them.

### What if the AI generates a bad spec?

Edit it. The AI's first draft captures structure and pulls from existing docs/code, but you should review and refine. Domain expertise matters.

### Do I need to use the slash commands?

No. You can also:
- Create change directories manually (`openspec new change "my-change"`)
- Write artifacts by hand
- Use `openspec` CLI commands directly

The slash commands are convenient wrappers, not requirements.

### How do I see all active changes?

```bash
openspec list
```

### How do I check the status of a change?

```bash
openspec status --change "my-change"
```
