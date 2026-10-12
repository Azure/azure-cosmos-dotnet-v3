# Reviewer Memory — schema

Each SDK has a JSON file at `memory/<sdk>.json` with this shape (mirroring
`review_design_doc` semantics so the same mental model applies):

```jsonc
{
  "sdk": "java",
  "version": 1,
  "updated_at": "2025-01-01T00:00:00Z",

  // Things to always check on every PR for this SDK.
  // Promote stable items from here into sdk-packs/<sdk>.md "Recurring review themes".
  "always_check": [
    // { "id": "ac-1", "text": "Confirm CHANGELOG.md updated", "added_at": "..." }
  ],

  // Patterns the reviewer should NOT flag (you've decided they're noise).
  "stop_flagging": [
    // { "id": "sf-1", "text": "Comment style nitpicks", "added_at": "..." }
  ],

  // Free-form preferences for tone, depth, format.
  "preferences": {
    // "tone": "concise",
    // "depth": "deep",
    // "default_output": "file"
  },

  // Bug classes seen repeatedly.
  "recurring_patterns": [
    // { "id": "rp-1", "text": "Reactor blocking call inside Mono", "seen_count": 3 }
  ],

  // Rolling window of last N reviews (oldest evicted).
  "recent_reviews": [
    // { "pr": "Azure/azure-sdk-for-java#12345",
    //   "tier": 2,
    //   "verdict": "approve_with_suggestions",
    //   "reviewed_at": "...",
    //   "themes": ["diagnostics", "retry"] }
  ]
}
```

**Governance fields (all optional, additive — still version 1)**

Every entry inside `always_check`, `stop_flagging`, `recurring_patterns` may carry:

```jsonc
{
  "id": "ac-1",
  "text": "...",
  "added_at": "2025-01-01T00:00:00Z",

  // who/what put this here
  "source": "feedback-cmd | bootstrap | manual",

  // 0.0–1.0; default 1.0 for explicit user feedback, lower for inferred items
  "confidence": 1.0,

  // bumped each time the entry is included in active context for a review
  "last_used_at": "2025-01-01T00:00:00Z",

  // optional expiry; entries past expiry are ignored when building active context
  "expires_at": null,

  // dimension this entry belongs to (additive — defaults to "other" if missing).
  // Mirrors the finding categories used by the router/merger and the engineering
  // quality rubric. Used by the feedback command (--category flag) and by the
  // router when bucketing memory entries into the per-dimension reviewer prompts.
  "category": "correctness | code_quality | testing | perf | security | api | parity | design_alignment | agentic | style | other",

  // optional scoping so an entry only applies in a subset
  "scope": {
    "repo": "Azure/azure-sdk-for-java",
    "path_prefix": "sdk/cosmos/",
    "area": "diagnostics"
  }
}
```

`scope` is matched as: all present sub-fields must match the PR being reviewed.
A missing sub-field means "any". Entries with no `scope` apply globally within the SDK.

**`recurring_patterns` only — pre-flight verification grep**

Entries in `recurring_patterns` may additionally carry:

```jsonc
{
  "id": "rp-1",
  "text": "Reactor blocking call inside Mono",
  "category": "perf",

  // Optional reusable shell command that the reviewer can run to detect this
  // pattern in a PR's diff (or in the SDK source). Encourages the reviewer to
  // verify the pattern before flagging — see Dimension 6 in
  // engineering-quality-rubric.md. The command is appended to the finding's
  // `evidence` field along with its output.
  "verified_by_grep_command": "grep -rn '\\.block(' sdk/cosmos/azure-cosmos/src/main/java/"
}
```

If a `recurring_patterns` entry has `verified_by_grep_command`, the reviewer
MUST run it and include the output in any finding that cites the pattern.
Findings citing a pattern with this field present but no executed command →
downgraded to severity `nit` per the rubric's Dimension 6 rule.

**Bounds (enforced by the feedback command)**

- `always_check`: max 30 entries; eviction by oldest `last_used_at` then `added_at`
- `stop_flagging`: max 30; same eviction rule
- `recurring_patterns`: max 50; eviction by oldest `last_used_at` then lowest `seen_count`
- `recent_reviews`: max 25; FIFO

**Precedence (highest first) when the router builds active context**

1. Explicit user request in this session (e.g. "for this review, also check X")
2. Repo rules (`.pragentrules`, CODEOWNERS hints) if present
3. Per-SDK memory (`memory/<sdk>.json`)
4. Global memory (`memory/_global.json`)
5. Base agent defaults (PR Deep Reviewer, etc.)

Higher tiers override lower tiers when they conflict (e.g. an `always_check` from
the SDK file beats a `stop_flagging` for the same theme in `_global.json`).

**Conventions**

- `version` lets future migrations be safe; bump when shape changes.
  - Adding the optional `category` and `verified_by_grep_command` fields is
    backward-compatible (additive only) — no version bump required. Old entries
    without `category` are treated as `category: "other"` when bucketing.
- IDs are kebab-case with a 2-letter prefix (`ac-`, `sf-`, `rp-`) for easy grep.
- The feedback command appends/updates entries here. Humans can edit by hand;
  keep JSON valid (`jq . memory/<sdk>.json > /dev/null` to validate).
- New entries from the feedback command default to `confidence: 1.0`,
  `source: "feedback-cmd"`, `last_used_at = added_at`, and `category: "other"`
  unless the user passed `--category <value>`.

**Cross-SDK lessons**

`memory/_global.json` uses the same shape but with no `sdk` field — its entries
apply across all SDKs subject to the precedence rules above. Use sparingly;
prefer per-SDK entries.
