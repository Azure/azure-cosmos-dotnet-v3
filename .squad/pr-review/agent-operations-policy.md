# Agent Operations Policy

AI review is production automation and must be explicit about failures.

## Budgets

- Lightweight reviewers should finish quickly.
- Deep reviewers may take longer, but the router must show progress and degraded-mode state.
- No optional sub-agent failure should silently become success.

## Failure handling

| Failure | Action |
|---|---|
| Optional sub-agent timeout/rate limit | Continue, mark partial, list skipped reviewer |
| Primary reviewer failure | Stop or ask user; do not silently fall back to Fresh Eyes |
| Auth failure | Surface exact tool/auth failure and preserve any partial findings |
| Context overflow | Reduce context or mark degraded; do not retry same prompt blindly |
| Malformed structured output | Parse fallback if possible, tag `parse-fallback`, run quality gate |

## Heartbeats

Long-running review flows should emit short progress updates so users know the review is active.

## Metrics

Track review duration, skipped agents, parse fallback, unverified findings, finding count, and token/cost estimates when available. Metrics collection must never block the review.
