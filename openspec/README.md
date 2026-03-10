# OpenSpec — Spec-Driven Development for the Azure Cosmos DB .NET SDK

This directory contains [OpenSpec](https://github.com/openspec-dev/openspec) artifacts for the Azure Cosmos DB .NET v3 SDK. OpenSpec provides a structured, AI-assisted workflow for proposing, specifying, designing, and implementing changes.

## Why OpenSpec?

The Cosmos DB .NET SDK is a large, complex codebase (~1,400+ source files) with many interdependent subsystems — retry policies, handler pipelines, cross-region routing, change feed processing, query execution, and more. OpenSpec helps by:

1. **Capturing behavioral contracts** — Specs define *what* a feature should do (invariants, edge cases, error handling), not *how* it's implemented. This makes them durable even as implementation evolves.
2. **Guiding AI-assisted development** — AI agents use specs as context when proposing and implementing changes, leading to more accurate code generation.
3. **Reducing tribal knowledge** — Complex features like PPAF, cross-region hedging, and the handler pipeline have subtle invariants that are easy to break. Specs make these invariants explicit and reviewable.

## Directory Structure

```
openspec/
├── config.yaml              # Project context and artifact rules
├── README.md                # This file
├── specs/                   # Main spec catalog (living documentation)
│   ├── README.md            # Spec index organized by area
│   ├── retry-and-failover/
│   │   └── spec.md
│   └── ...
├── changes/                 # Active changes (in-progress work)
│   └── archive/             # Completed changes
```

| Concept | Location | Purpose |
|---------|----------|---------|
| **Specs** | `openspec/specs/<feature>/spec.md` | Living behavioral contracts for major feature areas. |
| **Changes** | `openspec/changes/<name>/` | In-progress work with proposal, design, and task artifacts. |
| **Archive** | `openspec/changes/archive/` | Completed changes with full context preserved. |
| **Config** | `openspec/config.yaml` | Project context and per-artifact rules that guide AI. |

## Workflow

```
Propose ──▶ Specs ──▶ Design ──▶ Tasks ──▶ Apply ──▶ Archive
```

| Command | Purpose |
|---------|---------|
| `/opsx:propose <name>` | Create a new change with proposal, design, and task artifacts |
| `/opsx:apply [name]` | Implement tasks from a change |
| `/opsx:explore [topic]` | Investigate ideas or problems without making code changes |
| `/opsx:archive [name]` | Archive a completed change |

## Writing Good Specs

Specs capture **behavioral contracts** using [EARS notation](https://en.wikipedia.org/wiki/Easy_Approach_to_Requirements_Syntax) (WHEN/THEN/SHALL). They should answer: "What do I need to know to safely modify this feature?"

### What a spec should include

1. **Purpose** — One-paragraph summary of what the feature does
2. **Public API surface** — Key types, methods, and their contracts (C# code blocks)
3. **Requirements** — Behavioral requirements using EARS notation (`WHEN <condition>, THEN the SDK SHALL <behavior>`)
4. **Reference tables** — Status code tables, configuration defaults, parameter matrices for dense reference data
5. **Edge cases** — Non-obvious behaviors, race conditions, failure modes
6. **Interactions** — How this feature relates to other SDK components (cross-spec links)
7. **References** — Links to source files and existing design docs

### What a spec should NOT include

- Implementation details (specific variable names, internal algorithms)
- Performance benchmarks (these change; use test projects instead)
- Step-by-step code walkthroughs (that's what `docs/SdkDesign.md` is for)

## When to Create or Update Specs

**Create a new spec when:**
- Adding a new major feature to the SDK
- An area has complex invariants that are easy to break
- The same behavioral rules are explained in multiple PR reviews

**Update an existing spec when:**
- Your PR changes behavior covered by a spec
- A bug fix reveals an invariant that wasn't captured
- A design doc in `docs/` gets updated

**Don't need a spec for:**
- Pure refactoring with no behavioral change
- Test-only changes, documentation updates, dependency bumps

## Best Practices

| ✅ Do | ❌ Don't |
|-------|---------|
| Be specific about invariants (status codes, timeouts) | Copy implementation details into specs |
| Use EARS notation for requirements | Create a spec per class (group by feature area) |
| Include cross-spec "Interactions" sections | Let specs go stale |
| Reference source files by path | Duplicate content from `docs/` |
| Update specs as part of behavioral change PRs | Skip `/opsx:explore` for complex changes |
| Review spec diffs in PRs like code | Archive before the PR is merged |

## Related Documentation

- [Spec Index](specs/README.md) — All specs organized by area
- [SdkDesignGuidelines.md](../SdkDesignGuidelines.md) — Public API contract rules
- [docs/SdkDesign.md](../docs/SdkDesign.md) — SDK architecture overview