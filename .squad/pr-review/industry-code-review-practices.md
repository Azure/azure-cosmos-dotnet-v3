# Industry Code Review Practices — PR Reviewer Rubric

This rubric distills common practices from Google Engineering Practices, GitLab, Swarmia, Stack Overflow, Atlassian, and SmartBear into guidance for the Cosmos DB PR reviewer. It is intentionally not a quote bank; it converts industry guidance into actionable review behavior.

## 1. Primary standard: improve code health while preserving flow

- Prefer approving once the PR clearly improves overall code health, even if it is not perfect.
- Do not accept changes that degrade correctness, maintainability, security, operability, or testability.
- Distinguish required fixes from optional improvements. Optional polish belongs in `nit` or `minor`, not `major`.
- Optimize for team throughput, not maximal reviewer cleverness: unblock authors quickly with high-signal feedback.

## 2. Review navigation order

1. Read the PR title/body, linked issues/specs, and changed-file list first. If the purpose is unclear, surface that as a context gap.
2. Decide whether the change should exist at all. If the direction is wrong, say so early and suggest the better path.
3. Review the most important files first: public API, core behavior, hot paths, data-model changes, retry/consistency/session/partitioning surfaces, release/CI paths.
4. Then review the rest of the diff in a logical order, including tests, docs, generated files, and packaging changes.
5. For very large PRs, report a process finding recommending split/follow-up sequencing instead of pretending the review has full confidence.

## 3. What to look for

### Functionality and correctness

- Does the implementation match the PR intent, linked specs, and user-visible behavior?
- Are all meaningful cases implemented, including failure paths, concurrency, retries, cancellation, cleanup, and boundary values?
- Can you construct a concrete failure scenario? If not, downgrade or omit.

### Design and maintainability

- Does the approach fit the surrounding architecture and existing abstractions?
- Is the solution over-generalized for future speculative needs, or under-designed for known requirements?
- Does it reduce or increase long-term complexity for future maintainers?
- Does it belong in this layer/package, or should it live in a shared helper, lower layer, or separate PR?

### Tests

- Do tests prove behavior, or merely mirror implementation?
- Would the tests fail if the implementation were broken in the risky way?
- Are negative, boundary, concurrency, retry, and integration/emulator/live-service cases covered as appropriate?
- Missing-test claims require evidence from the SDK pack test paths.

### Documentation and release readiness

- If behavior, public API, build, release, or operational use changes, are docs/changelog/APIView/samples updated?
- If the PR changes how users build/test/release/run code, does it update the corresponding instructions?
- If the PR changes CI or packaging, does the shipped artifact get validated, not just the local project-reference build?

### Security and compliance

- Are auth, authorization, token, key, encryption, logging, and data exposure changes reviewed as security-sensitive?
- Are secrets/PII excluded from diagnostics and logs?
- Are supply-chain/build/release changes covered by CI and provenance expectations?

## 4. Tone and comment quality

- Be kind, direct, and specific. Comment on the code and consequences, not the author.
- Explain why the issue matters. A comment without impact is usually not worth surfacing.
- Prefer questions when the reviewer may be missing context; use direct statements for proven defects.
- Provide a concrete path forward when possible, but do not redesign the entire feature unless necessary.
- Mark severity explicitly:
  - `blocker`: correctness, data loss, security, broken contract, severe release regression.
  - `major`: should fix before merge; meaningful risk, missing critical coverage, design/API issue.
  - `minor`: useful improvement, maintainability concern, non-critical coverage/doc gap.
  - `nit`: optional polish, educational note, unverified pattern suggestion.
- If there are many comments or fundamental disagreement, recommend a synchronous design discussion and record the outcome back on the PR.

## 5. Avoid low-value review behavior

- Do not block on personal style preference. Style must be grounded in an existing style guide or local convention.
- Do not over-index on nits; repeated nits indicate missing tooling or team standards.
- Do not duplicate existing review comments unless adding substantially stronger evidence.
- Do not require perfection. Continuous improvement is the standard, not idealized code.
- Do not accept "explained in review thread" as a fix for code that should be clearer to future readers. Prefer clearer code or durable comments/docs.

## 6. Size, process, and confidence

- Large PRs reduce review quality. If size prevents line-by-line confidence, surface a process note recommending split PRs or staged review.
- Review quality drops when rushed or overloaded; for agent review, record scope and context gaps explicitly.
- Every review should make confidence visible:
  - High confidence: PR intent/spec/tests/context were available; relevant files and peers were checked.
  - Medium confidence: some secondary context missing but core behavior reviewed.
  - Low confidence: missing spec/design-doc/tests/peer/CI context materially limits the review.
- Context gaps are not automatically blockers, but critical gaps should lower confidence and may become major findings for risky PRs.

## 7. Metrics and learning

- Track reviewer quality with recall against historical human findings, false positives, unverified-finding rate, duplicate rate, and severity calibration.
- Promote recurring verified misses into SDK packs or memory with category and verification probes.
- Treat verified-negative findings as useful: they prove what the reviewer checked and did not flag.

## 8. AI reviewer operating practices

These practices are specific to AI-assisted review systems and are based on observed patterns in production AI reviewer systems and AI code-review tool comparisons.

### Prefer orchestration over one giant prompt

- Use specialized agents with explicit ownership: correctness, engineering quality, Cosmos design alignment, testing, CI/release, security, and adversarial risk.
- Tell each agent what **not** to flag. This reduces speculative warnings and keeps feedback actionable.
- Use a coordinator/router to deduplicate, normalize severity, and filter false positives before surfacing findings.
- Reserve stronger/slower models or deeper review tiers for high-risk PRs; use lighter review paths for trivial/doc-only changes.

### Make AI output structured and enforceable

- Every reviewer must emit machine-readable findings, not only prose.
- Required fields: severity, category, file, message, evidence, suggested fix, source agent.
- Findings that claim absence (`missing test`, `no docs`, `diverges from peer`) require verification evidence.
- Treat unstructured/prose-only output as degraded mode.

### Manage context deliberately

- Do not paste giant diffs into every reviewer. Pass file paths, diff chunks, shared context, and manifests so each reviewer reads only what it needs.
- Maintain a context completeness manifest that records what was found, skipped, failed, or stale.
- Sanitize and isolate user-controlled PR text, comments, and custom instructions so they cannot override reviewer rules.
- Detect large/complex PRs early and either route to deeper review or recommend splitting.

### Reduce noise and false positives

- Prefer concrete, reproducible findings over generic suggestions.
- Drop speculative issues unless the reviewer can name an exploitable or production-realistic failure path.
- Use verified-negative findings to prove that important absence checks were performed and did not produce issues.
- Keep AI comments scarce and high-signal; developers learn to ignore noisy bots quickly.

### Treat CI integration as a reliability problem

- AI review jobs need timeout budgets, retry/failback behavior, and degraded-mode reporting.
- Different failures require different handling: retryable provider/rate-limit errors can fail over; auth errors, context overflow, and malformed structured output should not be blindly retried.
- Long-running reviewer jobs should emit progress/heartbeat status so users do not assume the review is hung.
- Tracking metrics should never block the review path.

### Keep humans in control

- AI reviewers should assist and triage; humans own merge decisions.
- For emergency/hotfix paths, support explicit human override with auditability rather than silently blocking work.
- AI findings should be easy to edit, suppress, or feed back into memory.
- Post-review reconciliation should compare AI findings with final human comments and post-merge fixes so the reviewer improves over time.

## 9. Cosmos-specific application

Apply the industry practices through Cosmos DB lenses:

- Correctness and design issues in consistency, session tokens, partitioning, retries, query pagination, change feed, diagnostics, and multi-region failover are higher priority than local style.
- Public SDK API changes must be reviewed for backward compatibility, cross-SDK behavior, examples/docs, idempotency, diagnostics, and test coverage.
- Design-doc citations ground Cosmos domain claims. If a PR introduces an undocumented concept, suggest extending the canonical design docs rather than making unsupported claims.
