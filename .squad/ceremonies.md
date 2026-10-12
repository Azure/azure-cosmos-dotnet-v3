# Ceremonies

> Operational ceremonies for the Cosmos DB PR Review Squad.

## Review Calibration

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before dispatch |
| **Condition** | Tier 3 reviews or cross-SDK parity evaluations |
| **Facilitator** | `pr-review-router` |
| **Participants** | `pr-deep-reviewer`, `engineering-quality-reviewer`, `cosmos-pr-reviewer` |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Ground review scope in canonical `cosmosdb-design-docs` chapters.
2. Review per-SDK always-check rules and recent review feedback.
3. Validate peer SDK reality for any proposed cross-SDK assertions.
4. Establish shared understanding of PR intent from author's description.

---

## Memory & Retrospective Sync

| Field | Value |
|-------|-------|
| **Trigger** | manual |
| **When** | after review |
| **Condition** | user provides review feedback or updates preferences |
| **Facilitator** | `pr-review-feedback` |
| **Participants** | user, `pr-review-router` |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Capture user feedback (`--good`, `--bad`, `--always-check`, `--stop-flagging`).
2. Update `.squad/pr-review/memory/dotnet.json` or `_global.json` with confidence scores.
3. Distill new heuristics into `.squad/identity/wisdom.md`.
