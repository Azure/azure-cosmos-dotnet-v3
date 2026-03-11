# Spec-Based Development for the Azure Cosmos DB .NET SDK

## An Exploration of Best Practices, Organization, and Agentic Integration

> **Purpose**: This document explores how the Cosmos DB .NET SDK team can adopt spec-based development to improve feature planning, implementation consistency, and AI-assisted (agentic) development workflows. It is intended as a discussion document for the team — no decisions are final.

---

## Table of Contents

- [1. Current State of Documentation](#1-current-state-of-documentation)
- [2. What is Spec-Based Development?](#2-what-is-spec-based-development)
- [3. Industry Patterns and Frameworks](#3-industry-patterns-and-frameworks)
- [4. Recommended Spec Structure for the SDK](#4-recommended-spec-structure-for-the-sdk)
- [5. Where to Store Specs](#5-where-to-store-specs)
- [6. Organization and Categorization](#6-organization-and-categorization)
- [7. Agentic Development Integration](#7-agentic-development-integration)
- [8. New Features vs. Retroactive Specs](#8-new-features-vs-retroactive-specs)
- [9. Recommended Best Practices](#9-recommended-best-practices)
- [10. Example: What a Spec Would Look Like](#10-example-what-a-spec-would-look-like)
- [11. Recommended Next Steps](#11-recommended-next-steps)
- [References](#references)

---

## 1. Current State of Documentation

### What exists today

The repository currently has several documentation artifacts, but they follow no consistent structure or format:

| Location | Contents | Format | Count |
|---|---|---|---|
| `docs/` | Feature design docs (SdkDesign, Cross Region Hedging, PPAF, Replica Validation, Observability, Caches, Versioning, Builds, LocalQuorum, Query) | Free-form markdown, varying structures | 11 files |
| Root `.md` files | SdkDesignGuidelines, Exceptions, CONTRIBUTING, PULL_REQUEST_TEMPLATE | Policy/guideline docs | 4+ files |
| `.github/` | copilot-instructions.md, IssueFixAgent, issue/PR templates | Agent instructions & templates | 5+ files |
| `docs/images/` | Diagrams and screenshots | Image files | Varies |

### Observed patterns in existing design docs

| Document | Has Scope | Has Background | Has API Examples | Has Diagrams | Has Test Plan | Has References |
|---|---|---|---|---|---|---|
| Cross Region Hedging | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ |
| PPAF Design | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| Replica Validation | ✅ | ✅ | ❌ | ✅ (Mermaid) | ❌ | ✅ |
| Observability | ❌ | ❌ | ✅ | ✅ (Mermaid) | ❌ | ✅ |
| Caches | ❌ | ❌ | ❌ | ✅ (Mermaid) | ❌ | ❌ |
| Local Quorum | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ |
| Query Hybrid Search | ❌ | ✅ | ✅ | ❌ | ✅ | ❌ |

### Key gaps

1. **No consistent template** — Each doc has a different structure and level of detail
2. **No machine-parseable metadata** — No frontmatter, status tracking, or categorization
3. **No explicit acceptance criteria or test plans** — Only the Query Hybrid Search doc includes a test plan
4. **No clear lifecycle** — Docs don't indicate whether a feature is proposed, in-progress, shipped, or deprecated
5. **No linkage to code** — Some docs link to source files (SdkDesign.md is excellent at this), others don't
6. **No agent-consumable structure** — An AI agent reading these docs would struggle to extract actionable implementation guidance

---

## 2. What is Spec-Based Development?

Spec-based development (SDD) is a methodology where **formal specifications are written before code** and serve as the authoritative source of truth throughout the development lifecycle.

### Degrees of adoption

| Level | Description | Fits SDK? |
|---|---|---|
| **Spec-First** | Write specs before coding; specs drive the workflow | ✅ Best fit for new features |
| **Spec-Anchored** | Specs are living artifacts, updated alongside code | ✅ Best fit for existing features |
| **Spec-as-Source** | Only edit specs; code is generated from specs | ❌ Too extreme for a complex SDK |

### Why it matters for this SDK

- **Consistency**: The SDK has 50+ public API surface areas across CRUD, queries, change feed, bulk, encryption, etc. Specs ensure each follows the same design patterns
- **Onboarding**: New team members (human or AI) can read a spec to understand a feature without spelunking through code
- **Review quality**: PR reviewers can validate implementations against specs
- **Agent enablement**: AI agents can use specs as authoritative context for implementation, testing, and code review tasks

---

## 3. Industry Patterns and Frameworks

### 3.1 RFCs (Request for Comments)

**What they are**: Collaborative proposals for changes that solicit feedback before implementation.

**Typical structure**: Context → Goals/Non-goals → Detailed proposal → Alternatives → Migration plan → Open questions

**Used by**: Rust, React, Ember, Sourcegraph, Uber, Google

**Best for**: Major architectural decisions, new feature proposals, breaking changes

**Fit for this SDK**: ✅ Good for proposing new features that need team buy-in. The existing `SdkDesignGuidelines.md` already requires API reviews — RFCs would formalize this.

### 3.2 ADRs (Architecture Decision Records)

**What they are**: Short, focused records capturing a single architectural decision with context, options, and rationale.

**Typical structure**: Status → Context → Decision → Consequences

**Used by**: Spotify, GitHub, ThoughtWorks (originated by Michael Nygard)

**Best for**: Capturing "why" decisions were made — especially valuable months or years later

**Fit for this SDK**: ✅ Good for decisions like "Why we use Newtonsoft.Json as default serializer" or "Why direct mode uses RNTBD protocol"

### 3.3 Design Documents

**What they are**: Detailed technical plans for implementing a feature or system change.

**Typical structure**: Problem → Goals → Solution → Implementation plan → Alternatives → Impact

**Used by**: Google, Amazon, Microsoft (already partially used in this repo's `docs/`)

**Best for**: Feature implementation planning — the "how" to complement specs' "what"

**Fit for this SDK**: ✅ Already partially adopted. The existing `docs/` design docs are closest to this pattern.

### 3.4 Spec-Kit (GitHub)

**What it is**: An open-source toolkit for spec-driven development with AI agents.

**Structure**: `specs/<feature>/spec.md` + `plan.md` + `tasks.md`

**Key insight**: Separates *what* (spec) from *how* (plan) from *work items* (tasks)

**Fit for this SDK**: ✅ The three-file pattern is well-suited. The SDK could adopt a similar structure adapted for its needs.

### 3.5 Kiro (AWS)

**What it is**: An agentic IDE built around spec-driven development.

**Structure**: `requirements.md` (EARS notation) + `design.md` + `tasks.md`

**Key insight**: Uses the EARS (Easy Approach to Requirements Syntax) notation for unambiguous, testable requirements: `WHEN <event>, THEN the system SHALL <behavior>`

**Fit for this SDK**: ✅ EARS notation is particularly good for SDK behavior specifications (e.g., "WHEN a 429 response is received, THEN the SDK SHALL retry up to MaxRetryAttemptsOnRateLimitedRequests times")

### 3.6 OpenSpec (Fission AI)

**What it is**: A lightweight, open-source, tool-agnostic SDD framework designed specifically for AI coding assistants. Built with Node.js (requires 20.19+), it provides a structured change-proposal workflow that works across 20+ AI agents (Copilot, Claude, Cursor, Codex, etc.).

**Structure**: Each change proposal creates a self-contained folder:
```
.openspec/
├── system-spec.md          # Persistent system-level context and conventions
└── changes/
    └── <change-name>/
        ├── proposal.md     # Change proposal (what and why)
        ├── specs/           # Detailed specifications for the change
        ├── design.md        # Technical design
        └── tasks.md         # Implementation tasks
```

**Key insights**:
- **Brownfield-first**: Unlike many SDD tools that target greenfield projects (0→1), OpenSpec excels at evolving existing codebases (1→n) — directly relevant for a mature SDK like this one
- **Change-folder model**: Each change proposal is self-contained with its own proposal, specs, design, and tasks, making it natural to track incremental evolution
- **Agent-native workflow**: Slash commands (`/opsx:new`, `/opsx:ff`, `/opsx:apply`, `/opsx:archive`) integrate directly into AI assistant conversations
- **No vendor lock-in**: Works with any AI assistant, any editor, any model — no proprietary IDE required
- **Persistent context**: The `system-spec.md` acts as always-on project memory, similar to `.github/copilot-instructions.md` but specifically for spec-driven workflows

**Fit for this SDK**: ✅ The brownfield-first philosophy is a strong fit for this mature SDK. The change-folder model naturally maps to feature development and bug fixes. The tool-agnostic approach means team members can use their preferred AI assistants. However, the Node.js dependency may be a consideration for a .NET-focused team.

### Comparison matrix

| Aspect | RFC | ADR | Design Doc | Spec-Kit | Kiro | OpenSpec |
|---|---|---|---|---|---|---|
| Focus | Proposal & consensus | Single decision | Implementation plan | What + plan + tasks | Requirements + design + tasks | Change proposals + specs + tasks |
| Granularity | Feature-level | Decision-level | Feature-level | Feature-level | Feature-level | Change-level (feature or fix) |
| Lifecycle | Draft → Accepted/Rejected | Proposed → Accepted → Superseded | Draft → Final | Living doc | Living doc | Propose → Review → Implement → Archive |
| Agent-friendly | Medium | Low (too terse) | Medium | High | High | Very High (built for agents) |
| Overhead | Medium-High | Low | Medium | Medium | Medium | Low-Medium |
| Brownfield support | ✅ | ✅ | ✅ | Partial | Partial | ✅ (primary focus) |
| Tool lock-in | None | None | None | Low (GitHub-aligned) | High (Kiro IDE) | None (20+ agents) |
| Already in repo? | ❌ | ❌ | Partially (docs/) | ❌ | ❌ | ❌ |

---

## 4. Recommended Spec Structure for the SDK

Based on the analysis above, we recommend a **hybrid approach** that combines the best elements from each pattern, tailored for this SDK's needs and agentic development.

### Proposed spec format: Three-file pattern per feature

```
specs/<feature-name>/
├── spec.md          # WHAT: Requirements, API surface, acceptance criteria
├── design.md        # HOW: Technical design, architecture, code paths
└── tasks.md         # WORK: Implementation tasks, test plan, status tracking
```

### Why three files?

1. **Separation of concerns**: Requirements (spec.md) are stable; design and tasks evolve
2. **Agent optimization**: An agent implementing a feature reads `spec.md` for requirements and `tasks.md` for work items. An agent reviewing code reads `spec.md` for acceptance criteria and `design.md` for architecture
3. **Review efficiency**: Different reviewers care about different artifacts — PMs review specs, architects review design, developers review tasks
4. **Incrementalism**: You can start with just `spec.md` and add `design.md`/`tasks.md` as needed

### Proposed `spec.md` template

```markdown
---
title: <Feature Name>
status: draft | in-review | approved | in-progress | shipped | deprecated
area: <routing | retry | query | changefeed | bulk | serialization | encryption | telemetry | other>
authors: [<GitHub handles>]
created: YYYY-MM-DD
updated: YYYY-MM-DD
related-issues: [#NNN, #NNN]
related-docs: [docs/<file>.md]
preview: true | false
---

# <Feature Name>

## Problem Statement
_What user or business problem does this feature solve? Why now?_

## Goals
- Goal 1
- Goal 2

## Non-Goals
- Non-goal 1 (and why it's excluded)

## API Surface

### New types or methods
```csharp
// Public API additions with XML doc comments
```

### Configuration options
```csharp
// New options/settings with defaults
```

### Usage example
```csharp
// End-to-end usage example a customer would write
```

## Behavior Specification

### Core behaviors (EARS notation)
- WHEN <condition>, THEN the SDK SHALL <behavior>
- WHERE <condition>, the SDK SHALL <behavior>

### Error handling
- WHEN <error condition>, THEN the SDK SHALL <response>

### Edge cases
- IF <edge case>, THEN <expected behavior>

## Acceptance Criteria
- [ ] Criterion 1 (testable)
- [ ] Criterion 2 (testable)

## Dependencies
- _Service-side dependencies, other SDK features, packages_

## Breaking Changes
- _None_ or list of breaking changes with migration guidance

## Open Questions
- Question 1
- Question 2
```

### Proposed `design.md` template

```markdown
---
title: <Feature Name> — Design
spec: ./spec.md
---

# <Feature Name> — Technical Design

## Architecture Overview
_How does this feature fit into the SDK component graph? Reference [SdkDesign.md](../../docs/SdkDesign.md)._

## Component Changes

### New classes/files
| Class | Location | Responsibility |
|---|---|---|

### Modified classes/files
| Class | Location | Change |
|---|---|---|

## Flow Diagrams
```mermaid
// Mermaid diagram showing request flow through the feature
```

## Code Paths
_Link to specific source files and methods that are relevant._
- [ClassName.cs](../../Microsoft.Azure.Cosmos/src/Path/ClassName.cs) — description

## Alternatives Considered
| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|

## Risks and Mitigations
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
```

### Proposed `tasks.md` template

```markdown
---
title: <Feature Name> — Tasks
spec: ./spec.md
design: ./design.md
---

# <Feature Name> — Implementation Tasks

## Implementation Tasks
- [ ] Task 1: description
- [ ] Task 2: description

## Test Plan

### Unit tests
- [ ] Test: <scenario> — verifies <acceptance criterion>

### Integration tests (emulator)
- [ ] Test: <scenario> — verifies <acceptance criterion>

### Performance tests
- [ ] Benchmark: <scenario> — baseline vs. new

## Documentation Tasks
- [ ] Update changelog.md
- [ ] Update copilot-instructions if needed
- [ ] Add/update sample in Microsoft.Azure.Cosmos.Samples/
```

---

## 5. Where to Store Specs

### Option A: `specs/` at repository root (Recommended)

```
azure-cosmos-dotnet-v3/
├── specs/                          # Feature specifications
│   ├── README.md                   # Index and guide
│   ├── _templates/                 # Spec templates
│   │   ├── spec.md
│   │   ├── design.md
│   │   └── tasks.md
│   ├── cross-region-hedging/       # Feature specs
│   │   ├── spec.md
│   │   ├── design.md
│   │   └── tasks.md
│   ├── ppaf/
│   │   └── spec.md
│   └── ...
├── docs/                           # Keep as-is for general docs
├── Microsoft.Azure.Cosmos/         # Source code
└── ...
```

**Pros**:
- High visibility and discoverability at root level
- Clean separation from general docs (`docs/`) which serve a different purpose
- Easy for agents to reference (`specs/<feature>/spec.md` is a predictable path)
- Aligns with Spec-Kit and Kiro conventions

**Cons**:
- Adds a top-level directory to an already large root
- Requires migration decision for existing docs

### Option B: `docs/specs/` under existing docs folder

```
azure-cosmos-dotnet-v3/
├── docs/
│   ├── specs/                      # Feature specifications (new)
│   │   ├── cross-region-hedging/
│   │   └── ...
│   ├── SdkDesign.md               # Keep existing docs
│   ├── caches.md
│   └── ...
```

**Pros**:
- Keeps all documentation in one tree
- No new top-level directory

**Cons**:
- Less visible — buried one level deeper
- Conflates specs (development artifacts) with docs (reference material)
- Existing `docs/` design files don't follow spec format, creating inconsistency

### Option C: Co-located with source code

```
Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/
├── spec.md
├── CrossRegionHedgingAvailabilityStrategy.cs
└── ...
```

**Pros**:
- Specs live right next to the code they describe
- Natural discovery when browsing code

**Cons**:
- SDK source code packages shouldn't include markdown specs
- Features span multiple directories, making it unclear where to put the spec
- Harder for agents to discover (scattered across the tree)

### Recommendation

**Option A (`specs/` at root)** is the strongest choice because:
1. It's the emerging industry standard (Spec-Kit, Kiro both use top-level `specs/`)
2. It's immediately discoverable by both humans and agents
3. It keeps specs separate from reference docs and source code
4. The `specs/README.md` can serve as a living index

The existing `docs/` folder should continue to hold general reference material (SdkDesign.md, versioning.md, builds-and-pipelines.md, etc.).

---

## 6. Organization and Categorization

### Naming convention

Use kebab-case directory names that match the feature's commonly-used name:

```
specs/
├── cross-region-hedging/
├── per-partition-automatic-failover/
├── distributed-tracing/
├── replica-validation/
├── change-feed-processor/
├── bulk-execution/
├── local-quorum/
├── hybrid-search-scoring/
└── ...
```

### Categorization via frontmatter

Rather than creating category subdirectories (which become unwieldy), use the `area` field in spec frontmatter for categorization:

```yaml
area: routing | retry | query | changefeed | bulk | serialization | encryption | telemetry | transport | diagnostics | client-config
```

This enables:
- Agents can filter specs by area: "find all specs where area = routing"
- The README index can group specs by area
- No directory restructuring when a feature's categorization changes

### Spec index (`specs/README.md`)

Maintain a human-readable and machine-parseable index:

```markdown
# Feature Specifications

## Index by Area

### Routing & Availability
| Spec | Status | Preview |
|---|---|---|
| [Cross Region Hedging](cross-region-hedging/spec.md) | shipped | yes |
| [Per-Partition Automatic Failover](per-partition-automatic-failover/spec.md) | shipped | no |

### Query
| Spec | Status | Preview |
|---|---|---|
| [Hybrid Search Scoring](hybrid-search-scoring/spec.md) | in-progress | yes |

...
```

---

## 7. Agentic Development Integration

### Layer 1: Standalone specs (baseline)

Even without any agent integration, well-structured specs provide value:
- Developers read them before implementing
- PR reviewers validate against acceptance criteria
- New team members use them for onboarding

### Layer 2: Copilot instructions update

Update `.github/copilot-instructions.md` to tell AI assistants about the spec system:

```markdown
- **Feature specifications**: Before implementing any feature, check `specs/<feature>/spec.md`
  for requirements, acceptance criteria, and behavior specifications. Check `specs/<feature>/design.md`
  for architecture decisions and code paths. Check `specs/<feature>/tasks.md` for the implementation plan.
- **When creating new features**: Create a spec in `specs/<feature-name>/` using the templates
  in `specs/_templates/` before writing code.
```

### Layer 3: Dedicated Copilot agent

Create a `SpecDrivenDevAgent` in `.github/agents/` that:

1. **For new features**: Guides the user through spec creation using the template
2. **For implementation**: Reads the spec and generates implementation tasks
3. **For code review**: Validates PRs against the spec's acceptance criteria and behavior specifications
4. **For testing**: Generates test cases from the EARS-notation behavior specs

Example agent prompt workflow:
```
@SpecDrivenDevAgent create spec for <feature-name>
→ Agent creates spec.md from template, asks clarifying questions

@SpecDrivenDevAgent implement <feature-name>
→ Agent reads spec.md, creates design.md and tasks.md, implements tasks

@SpecDrivenDevAgent review PR #NNN against spec <feature-name>
→ Agent validates PR changes against acceptance criteria
```

### Layer 4: Agent memory and context

The spec system naturally creates a **knowledge base** that agents can query:
- "What retry behavior does the SDK specify?" → Search specs where `area: retry`
- "What are the acceptance criteria for hedging?" → Read `specs/cross-region-hedging/spec.md`
- "Which features are in preview?" → Filter specs where `preview: true`

This is significantly more reliable than agents trying to infer behavior from code alone.

---

## 8. New Features vs. Retroactive Specs

### Approach A: New features only

**Description**: Only require specs for features developed going forward. Existing features continue with their current documentation.

| Aspect | Assessment |
|---|---|
| **Effort** | Low — specs are written as part of the natural development process |
| **Immediate value** | High — new features get consistent, structured documentation from day one |
| **Agent benefit** | Partial — agents can only use specs for newer features |
| **Risk** | Low — doesn't disrupt existing workflows |
| **Gap** | Existing features (50+ API surfaces) remain undocumented in spec format |

### Approach B: Retroactive specs for existing features

**Description**: Create specs for all existing features by converting/expanding current design docs.

| Aspect | Assessment |
|---|---|
| **Effort** | High — requires significant investment to spec 50+ existing feature areas |
| **Immediate value** | Very high — complete knowledge base for the entire SDK |
| **Agent benefit** | Maximum — agents have specs for every feature |
| **Risk** | Medium — risk of specs becoming stale if not maintained; opportunity cost |
| **Gap** | None — full coverage |

### Approach C: Hybrid (Recommended for discussion)

**Description**: Require specs for new features; retroactively create specs for high-value existing features on a prioritized basis.

**Prioritization criteria for retroactive specs**:
1. Features that are actively being modified or extended
2. Features with the most customer issues or confusion
3. Features that existing design docs partially cover (lower effort to convert)
4. Features where AI agents are most likely to be asked to make changes

**Candidate features for early retroactive specs** (based on existing docs and complexity):

| Feature | Has existing doc? | Conversion effort | Agent value |
|---|---|---|---|
| Cross Region Hedging | ✅ Detailed | Low | High |
| PPAF | ✅ Short | Medium | High |
| Distributed Tracing | ✅ Detailed | Low | High |
| Replica Validation | ✅ Detailed | Low | Medium |
| Retry Policies | ❌ (in SdkDesign.md) | Medium | Very High |
| Change Feed Processor | ❌ | High | High |
| Bulk Execution | ❌ | High | Medium |

---

## 9. Recommended Best Practices

### For writing specs

1. **Be behavior-oriented, not implementation-oriented**: Describe *what* the SDK should do, not *how* (that's for design.md)
2. **Use EARS notation for behavioral specs**: `WHEN <condition>, THEN the SDK SHALL <behavior>` — this is unambiguous for both humans and AI
3. **Include concrete API examples**: Actual C# code showing how a customer would use the feature
4. **Make acceptance criteria testable**: Each criterion should map to at least one test case
5. **Link to code paths**: In design.md, link to specific source files using relative paths (e.g., `[ClientRetryPolicy.cs](../../Microsoft.Azure.Cosmos/src/ClientRetryPolicy.cs)`)
6. **Track status in frontmatter**: Use YAML frontmatter so agents can programmatically query spec status
7. **Keep specs up to date**: When behavior changes, update the spec first, then the code

### For organizing specs

8. **One directory per feature**: Keep all spec artifacts together
9. **Use the index**: Maintain `specs/README.md` as the entry point
10. **Use area tags, not category folders**: Frontmatter categorization is more flexible than directory hierarchy
11. **Name directories after features, not code**: `cross-region-hedging/` not `availability-strategy/`

### For agentic development

12. **Explicit over implicit**: AI agents cannot infer intent — spell out every requirement and edge case
13. **Include error handling specs**: Agents are notoriously weak at handling edge cases unless explicitly told
14. **Reference the spec in PRs**: When a PR implements a spec, link to it in the PR description
15. **Let agents help write specs**: Use AI to draft initial specs from existing code, then refine manually

### For the team process

16. **Spec review before code review**: Require spec approval before implementation begins (similar to existing API review process in SdkDesignGuidelines.md)
17. **Lightweight for small changes**: Not every bug fix needs a spec — use judgment. A good threshold: "Does this change public behavior or add new functionality?"
18. **Living documents**: Specs should be updated when behavior changes, not treated as write-once artifacts

---

## 10. Example: What a Spec Would Look Like

Below is an abbreviated example of what a retroactive spec for Cross Region Hedging might look like, derived from the existing `docs/Cross Region Request Hedging.md`:

```markdown
---
title: Cross Region Request Hedging
status: shipped
area: routing
authors: [nicktra]
created: 2024-01-15
updated: 2025-02-20
related-issues: []
related-docs: [docs/Cross Region Request Hedging.md]
preview: true
---

# Cross Region Request Hedging

## Problem Statement
In multi-region deployments, a single region may experience high latency
or temporary unavailability. Customers need the SDK to automatically
hedge requests to alternate regions to maintain low end-to-end latency.

## Goals
- Reduce read latency during regional degradation
- Provide configurable thresholds for hedging behavior
- Allow per-request override of hedging strategy

## Non-Goals
- Write request hedging for single-master accounts (handled separately by PPAF)
- Automatic region selection without ApplicationPreferredRegions

## API Surface

### Configuration
​```csharp
CosmosClientOptions options = new CosmosClientOptions()
{
    AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
        threshold: TimeSpan.FromSeconds(1.5),
        thresholdStep: TimeSpan.FromSeconds(1)),
    ApplicationPreferredRegions = new List<string>() { "East US", "West US" },
};
​```

## Behavior Specification

### Core behaviors
- WHEN a request exceeds the threshold time without a response,
  THEN the SDK SHALL send a hedged request to the next region in the preferred regions list.
- WHEN a hedged request is sent, THEN subsequent hedged requests SHALL be sent
  at intervals defined by thresholdStep.
- WHEN a final response is received from any region,
  THEN the SDK SHALL return that response and cancel outstanding hedged requests.
- WHERE ApplicationPreferredRegions is not set,
  THEN the SDK SHALL NOT apply any availability strategy.

### Error handling
- WHEN all hedged requests return non-final responses,
  THEN the SDK SHALL return the last response received.

## Acceptance Criteria
- [ ] Hedging triggers after threshold elapsed without response
- [ ] Hedged requests go to regions in preferred-regions order
- [ ] thresholdStep controls interval between hedged requests
- [ ] Per-request AvailabilityStrategy overrides client-level strategy
- [ ] No hedging when ApplicationPreferredRegions is unset
```

---

## 11. Recommended Next Steps

### Immediate (team discussion)

1. **Review this document as a team** — Discuss the proposed spec format, storage location, and level of adoption
2. **Decide on approach**: New-only, retroactive, or hybrid (see [Section 8](#8-new-features-vs-retroactive-specs))
3. **Decide on location**: `specs/` at root (recommended) or alternative

### Short-term (if approved)

4. **Create the `specs/` directory** with `README.md` and `_templates/`
5. **Write the spec template files** based on the templates proposed in [Section 4](#4-recommended-spec-structure-for-the-sdk)
6. **Pilot with one new feature**: Write a full spec (spec.md + design.md + tasks.md) for an upcoming feature
7. **Pilot one retroactive spec**: Convert the Cross Region Hedging doc as a proof-of-concept
8. **Update `.github/copilot-instructions.md`** to reference the spec system

### Medium-term

9. **Create the SpecDrivenDevAgent** (`.github/agents/spec-driven-dev-agent.agent.md`)
10. **Add spec references to PR template** — Prompt PR authors to link related specs
11. **Retroactively spec high-priority features** per the prioritization in [Section 8](#8-new-features-vs-retroactive-specs)

### Long-term

12. **Integrate spec validation into CI** — Lint spec frontmatter, check for broken links
13. **Measure impact** — Track whether spec-driven features have fewer bugs, faster reviews, better agent outcomes

---

## References

- [GitHub Spec Kit](https://github.com/github/spec-kit) — Open-source toolkit for spec-driven development
- [Kiro by AWS](https://kiro.dev/) — Agentic IDE with spec-driven development support
- [OpenSpec by Fission AI](https://openspec.dev/) — Lightweight, tool-agnostic SDD framework for AI coding assistants ([GitHub](https://github.com/Fission-AI/OpenSpec))
- [Architecture Decision Records](https://adr.github.io/) — Lightweight decision recording
- [Martin Fowler on SDD](https://martinfowler.com/articles/exploring-gen-ai/sdd-3-tools.html) — Analysis of spec-driven development tools
- [Design Docs](https://www.designdocs.dev/) — Design document patterns and templates
- [RFCs and Design Docs in Practice](https://blog.pragmaticengineer.com/rfcs-and-design-docs/) — How companies use RFCs
- [EARS (Easy Approach to Requirements Syntax)](https://en.wikipedia.org/wiki/Easy_Approach_to_Requirements_Syntax) — Structured requirements notation
- [Azure SDK .NET Guidelines](https://azure.github.io/azure-sdk/dotnet_introduction.html) — Central SDK design guidelines
- [OpenSpec vs Spec Kit Comparison](https://avasdream.com/blog/openspec-vs-spec-kit-ai-development) — Side-by-side framework comparison
