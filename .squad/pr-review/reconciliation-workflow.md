# Post-Review Reconciliation Workflow

Use reconciliation after human review or after merge to improve reviewer memory/evals.

## Inputs

- saved agent findings file
- final PR inline comments and review bodies
- author responses / resolved status when available
- post-merge fixes, reverted commits, linked bugs, or CI failures

## Steps

1. Fetch final human comments and review bodies.
2. Run `reconcile-review.mjs` as a first-pass keyword triage.
3. Manually classify each candidate:
   - agent hit
   - missed real issue
   - false positive
   - duplicate/noise
   - human-only style preference
4. Promote verified misses into:
   - eval corpus expected finding
   - `PR Review Feedback --always-check`
   - SDK pack recurring theme with verification probe
5. Promote false positives into scoped `--stop-flagging` entries.

## Guardrails

Never auto-promote keyword-only misses. Human verification is required before memory or corpus updates.
