# Flaky Test Detection Agent — Design Specification

## 1. Problem Statement

The Azure Cosmos DB .NET SDK CI system runs thousands of tests across multiple pipelines — PR validation, rolling (4×/day on weekdays), cron (every 6 hours, cross-platform), and functional. Test failures are a recurring pain point:

- **Flaky tests block PRs** — contributors re-run builds hoping for green, wasting time and CI resources.
- **Manual `[TestCategory("Flaky")]` tagging is reactive** — a test must be noticed and manually triaged before it's quarantined.
- **No historical tracking** — there's no data on test reliability trends, environmental correlations, or failure patterns.
- **PR regressions vs. flakiness ambiguity** — when a test fails on a PR, contributors can't tell if their change broke something or if the test is unreliable.

### Current State

The repository already has infrastructure to build on:

| Capability | Current State | Gap |
|-----------|--------------|-----|
| Flaky test marking | `[TestCategory("Flaky")]` attribute on ~7 test methods | Manual discovery, no automated detection |
| Quarantine | `[TestCategory("Quarantine")]` for intentional exclusions | No lifecycle management |
| Pipeline retry | `retryCountOnTaskFailure: 2` (standard), `4` (flaky) | Retry data not analyzed for patterns |
| Test result publishing | `publishTestResults: true` (TRX format to ADO) | Results not aggregated or analyzed cross-build |
| Issue filing | Manual via GitHub | No automation, no dedup, no CODEOWNERS tagging |
| Correlation analysis | None | No environmental factor tracking |

### Desired State

An automated agent that:

1. **Continuously collects** test results from all CI pipelines via the Azure DevOps REST API
2. **Statistically detects** flaky tests using fliprate analysis and EWMA scoring
3. **Correlates failures** with environmental conditions (time of day, OS, emulator state, parallel group, retry behavior)
4. **Classifies PR failures** as either PR-caused regressions or pre-existing flakiness
5. **Files GitHub issues** automatically — with team tagging via CODEOWNERS, error details, correlation insights, and proposed fixes
6. **Manages lifecycle** — tracks tests from detection through quarantine to fix, auto-comments on issues when tests stabilize

---

## 2. Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                    FLAKY TEST DETECTION AGENT                        │
│                                                                      │
│  ┌────────────┐   ┌─────────────────┐   ┌────────────────────────┐  │
│  │ Scheduler  │──▶│ Data Collector   │──▶│  Analysis Engine       │  │
│  │ (GH Action │   │ (ado_client.py)  │   │  ┌──────────────────┐ │  │
│  │  cron)     │   │                  │   │  │ Fliprate/EWMA    │ │  │
│  └────────────┘   │ • Test results   │   │  │ analyzer.py      │ │  │
│                   │ • Build timeline │   │  └──────────────────┘ │  │
│                   │ • Emulator logs  │   │  ┌──────────────────┐ │  │
│                   └─────────────────┘   │  │ PR Classifier    │ │  │
│                                          │  │ classifier.py    │ │  │
│                                          │  └──────────────────┘ │  │
│                                          │  ┌──────────────────┐ │  │
│                                          │  │ Env Correlator   │ │  │
│                                          │  │ correlator.py    │ │  │
│                                          │  └──────────────────┘ │  │
│                                          └───────────┬────────────┘  │
│                                                      │               │
│  ┌───────────────────────────────────────────────────▼────────────┐  │
│  │           Azure Cosmos DB Data Store (database.py)              │  │
│  │  test_executions │ flaky_registry │ filed_issues (containers)  │  │
│  └───────────────────────────────────────────────────┬────────────┘  │
│                                                      │               │
│  ┌─────────────────────┐   ┌─────────────────────────▼────────────┐ │
│  │ Issue Generator     │◀──│ Decision Engine                      │ │
│  │ (issue_filer.py)    │   │ • Threshold check                   │ │
│  │ • gh CLI            │   │ • Dedup (local DB + GitHub search)   │ │
│  │ • CODEOWNERS mapper │   │ • Rate limiting (max 5/run)          │ │
│  │ • Fix proposer      │   │ • Severity assessment                │ │
│  └─────────────────────┘   └──────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
```

### Data Flow

```
Azure DevOps Pipelines (PR, Rolling, Cron, Functional)
        │
        ▼  ADO REST API (PAT auth)
┌─────────────────┐
│  collect phase   │  Fetches builds → test runs → test results
│  (every 12h)     │  Enriches with: branch, PR#, OS, retry#, emulator health
└────────┬────────┘
         ▼
┌─────────────────┐
│  Cosmos DB store │  test_executions container (90-day TTL)
└────────┬────────┘
         ▼
┌─────────────────┐
│  analyze phase   │  Per-test: fliprate, EWMA, failure rate, retry-pass
│                  │  Updates flaky_registry with scores + status
└────────┬────────┘
         ▼
┌─────────────────┐
│  correlate phase │  Per-flaky-test: time, OS, emulator, pipeline, duration
└────────┬────────┘
         ▼
┌─────────────────┐
│  file phase      │  Dedup → propose fix → file issue → tag owners
│                  │  Max 5 new issues per run
└────────┬────────┘
         ▼
GitHub Issues (labeled `flaky-test`, assigned to CODEOWNERS)
```

### Technology Choices

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Language | Python 3.12 | Rich HTTP/data libraries, easy scripting, team familiarity |
| Data store | Azure Cosmos DB (NoSQL) | Dogfoods our own product; serverless tier keeps cost near-zero; built-in TTL for retention; no cache/persistence workarounds needed |
| Scheduler | GitHub Actions cron | Native to the repo, free, `GITHUB_TOKEN` auto-provided |
| Issue filing | `gh` CLI | Bypasses SAML SSO issues with Azure org |
| ADO access | REST API + PAT | Already documented in issue-fix-agent, minimal scope |
| Interactive | Copilot Agent (`.agent.md`) | Follows established repo pattern |

---

## 3. Detection Algorithm

### 3.1 Fliprate

Measures how often a test transitions between pass and fail. A perfectly stable test has fliprate 0.0; a maximally flaky test alternates every run with fliprate 1.0.

```
Given outcomes: [P, P, F, P, F, P, P, F, P, P]
Transitions:     P→P  P→F  F→P  P→F  F→P  P→P  P→F  F→P  P→P
Flips:               1    1    1    1         1    1
Fliprate = 6 flips / 9 transitions = 0.667
```

### 3.2 EWMA Fliprate

Exponentially Weighted Moving Average gives more weight to recent behavior, enabling detection of newly-flaky tests before they accumulate enough history for raw fliprate to be significant.

```
EWMA(t) = α × flip(t) + (1 − α) × EWMA(t−1)

Where α = 0.3 (configurable)
flip(t) = 1 if outcome(t) ≠ outcome(t−1), else 0
```

### 3.3 Detection Thresholds

| Condition | Threshold | Result |
|-----------|-----------|--------|
| EWMA ≥ 0.30 AND runs ≥ 20 | Confirmed flaky | File issue |
| EWMA ≥ 0.15 AND runs ≥ 10 | Suspected flaky | Monitor, include in report |
| Failure rate > 0.0 AND < 1.0 AND passes on retry | Strong signal | Weight +0.15 to EWMA |
| Failure rate = 1.0 | Broken, not flaky | Exclude from flaky analysis |
| Failure rate = 0.0 | Stable | No action |

### 3.4 PR vs. Flaky Classification

When a test fails on a PR build, the classifier applies these signals in priority order:

| Priority | Signal | Classification | Confidence |
|----------|--------|---------------|------------|
| 1 | Test in flaky_registry as `confirmed_flaky` | `flaky` | 95% |
| 2 | Emulator had startup issues in this build | `infrastructure` | 80% |
| 3 | Master failure rate > 10% (last 14 days) | `flaky` | 85% |
| 4 | Passed on retry in this PR build | `flaky` | 90% |
| 5 | Error matches known flaky pattern | `flaky` | 70% |
| 6 | Never failed on master in 20+ runs | `pr_regression` | 85% |
| 7 | Insufficient data | `unknown` | 30% |

---

## 4. Environmental Correlation

The agent tracks these dimensions and reports correlations for each flaky test:

| Dimension | Data Source | Detection Method |
|-----------|-----------|-----------------|
| **Time of day** | `run_timestamp` hour | Chi-squared distribution test vs. uniform |
| **Day of week** | `run_timestamp` day | Compare weekday vs. weekend failure rates |
| **OS** | Build agent info | Per-OS failure rate comparison |
| **Pipeline** | `pipeline_name` | Which pipelines see failures |
| **Emulator health** | Build timeline parsing | Failure rate when emulator required restart |
| **Job group** | `job_name` | Pipeline 1 vs. 2 vs. 3 vs. 4 correlation |
| **Duration anomaly** | `duration_ms` | Failures with >3× median duration = likely timeout |
| **Retry behavior** | `retry_attempt` | "Always passes on 2nd retry" pattern |

---

## 5. Issue Filing

### 5.1 Issue Template

Each filed issue includes:

- **Title:** `🔴 Flaky Test: {ClassName}.{MethodName}`
- **Labels:** `flaky-test`, `automated`, `needs-investigation`
- **Assignees:** Top 3 from CODEOWNERS for the test's component area
- **Body:**
  - Detection metrics table (fliprate, failure rate, run count, dates)
  - Most common error message and stack trace
  - Environmental correlation summary
  - Affected pipelines checklist
  - Proposed fix based on error pattern matching
  - Recommended action checklist (quarantine → investigate → fix → verify)
  - Link to recent failing build

### 5.2 Deduplication

Before filing, the agent checks:
1. Local `filed_issues` table for an open issue
2. GitHub search for open issues with `flaky-test` label matching the test method name

### 5.3 Rate Limiting

Maximum **5 new issues per agent run** to avoid flooding. Issues are prioritized by EWMA fliprate (worst first).

### 5.4 CODEOWNERS Mapping

Test-to-owner mapping based on the `CODEOWNERS` file and test namespace:

| Test Area | Owners |
|-----------|--------|
| `*/Query/*` | @adityasa, @neildsh, @kirankumarkolli |
| `*/Linq/*` | @khdang, @adityasa, @kirankumarkolli |
| `*/ChangeFeed/*`, `*/Batch/*`, `*/ReadFeed/*` | @khdang, @sboshra, @adityasa |
| `*/Tracing/*` | @khdang, @sboshra, @adityasa |
| `*/Contracts/*` | @kirillg, @kirankumarkolli, @FabianMeiswinkel |
| Default (all tests) | @khdang, @sboshra, @kirankumarkolli |

---

## 6. Fix Proposal Engine

Pattern-based fix suggestions for the 7 most common flaky error categories in this repository:

| Pattern | Error Signature | Proposed Fix |
|---------|----------------|-------------|
| **Timeout** | `TaskCanceledException`, `timed out` | Increase `[Timeout]`, add explicit `ManualResetEventSlim` wait |
| **Emulator connection** | `Connection refused`, `localhost:8081` | Add emulator health check in `[ClassInitialize]` |
| **Race condition** | `Assert.AreEqual failed` with timing values | Add polling/retry before assertion, ensure `await` |
| **Throttling** | `429`, `TooManyRequests` | Add retry with backoff, reduce concurrency |
| **Telemetry signal** | `ManualResetEventSlim`, `telemetry.*signal` | Reset signal in polling loops, add timeout to `Wait()` |
| **Socket error** | `SocketException`, `EPIPE` | Add connection retry, ensure `using` disposal |
| **Concurrent modification** | `InvalidOperationException.*modified` | Use `ConcurrentDictionary` or snapshot before iteration |

---

## 7. Lifecycle Management

### State Machine

```
monitoring ──► suspected ──► confirmed_flaky ──► issue_filed ──► quarantined ──► fixed
    ▲                                                                              │
    └──────────────────────────────────────────────────────────────────────────────┘
                                    (regression)
```

### Auto-Transitions

| Transition | Trigger |
|-----------|---------|
| `monitoring → suspected` | EWMA fliprate ≥ 0.15 with ≥ 10 runs |
| `suspected → confirmed_flaky` | EWMA fliprate ≥ 0.30 with ≥ 20 runs |
| `confirmed_flaky → issue_filed` | GitHub issue created |
| `issue_filed → quarantined` | `[TestCategory("Flaky")]` detected in source |
| `quarantined → fixed` | Fliprate < 0.05 for 50+ consecutive master runs |
| `fixed → monitoring` | Re-enters normal monitoring cycle |

### Auto-Close

When a filed test passes 50 consecutive runs on master, the agent comments:

> ✅ This test has passed 50 consecutive times on master. It appears to be fixed. Please verify and close.

---

## 8. Scheduling & Deployment

### GitHub Action Schedule

| Time (UTC) | Day | Purpose |
|-----------|-----|---------|
| 07:30 | Mon–Fri | Morning scan after overnight rolling builds |
| 13:00 | Mon–Fri | Afternoon check for daytime failures |
| Manual | Any | `workflow_dispatch` for ad-hoc analysis |

### Data Persistence

- **Store:** Azure Cosmos DB (NoSQL API, serverless tier) — dogfoods our own product
- **Database:** `flaky-test-agent`
- **Containers:**
  - `test-executions` — partitioned by `test_name`, TTL 90 days for automatic retention
  - `flaky-registry` — partitioned by `test_name`, no TTL (permanent tracking)
  - `filed-issues` — partitioned by `test_name`, no TTL (permanent tracking)
- **Cost:** Serverless tier — pay only for RU consumption; expected <$5/month at this scale
- **Retention:** 90-day TTL on `test-executions`; registry and issue data are permanent

### Secrets Required

| Secret | Scope | Purpose |
|--------|-------|---------|
| `ADO_PAT` | Repository secret | Azure DevOps Build (Read) access |
| `GITHUB_TOKEN` | Auto-provided | Issue creation (bypasses SAML in Actions context) |
| `COSMOS_ENDPOINT` | Repository secret | Cosmos DB account endpoint |
| `COSMOS_KEY` | Repository secret | Cosmos DB account key (or use managed identity) |

---

## 9. Interactive Copilot Agent

In addition to the automated GitHub Action, a Copilot Agent (`flaky-test-agent.agent.md`) provides interactive capabilities:

| Command | Description |
|---------|-------------|
| `scan pipelines` | Run full collection + analysis |
| `analyze test {name}` | Deep dive on a specific test |
| `classify failure {test} --pr {number}` | Is this PR failure flaky? |
| `report` | Current flaky test summary |
| `show correlations {test}` | Environmental correlations for a test |
| `propose fix {test}` | Fix suggestion for a flaky test |

---

## 10. File Structure

```
scripts/flaky-agent/
├── __init__.py
├── requirements.txt          # requests, urllib3
├── config.py                 # Thresholds, pipeline names, constants
├── ado_client.py             # Azure DevOps REST API client
├── database.py               # Cosmos DB client, containers, queries
├── collector.py              # Test result data collection & enrichment
├── analyzer.py               # Fliprate/EWMA flakiness scoring
├── correlator.py             # Environmental correlation engine
├── classifier.py             # PR-caused vs flaky classifier
├── issue_filer.py            # GitHub issue creation & dedup
├── fix_proposer.py           # Pattern-based fix suggestions
├── codeowners.py             # CODEOWNERS parser & team mapper
├── reporter.py               # Weekly summary report generator
├── main.py                   # CLI entrypoint
└── tests/
    ├── test_analyzer.py
    ├── test_classifier.py
    ├── test_correlator.py
    ├── test_codeowners.py
    └── fixtures/
        ├── sample_test_results.json
        ├── sample_build_timeline.json
        └── sample_emulator_log.txt

.github/
├── workflows/
│   └── flaky-test-detection.yml    # Scheduled GitHub Action
├── agents/
│   └── flaky-test-agent.agent.md   # Interactive Copilot Agent
└── ISSUE_TEMPLATE/
    └── flaky_test.md               # Issue template
```

---

## 11. Phased Delivery

| Phase | Scope | Deliverables |
|-------|-------|-------------|
| **1 — Data Foundation** | Collect & store test results | `ado_client.py`, `database.py`, `collector.py`, `config.py` |
| **2 — Detection Engine** | Analyze flakiness, classify failures | `analyzer.py`, `classifier.py`, `correlator.py` |
| **3 — Issue Filing** | File issues, tag teams, propose fixes | `issue_filer.py`, `fix_proposer.py`, `codeowners.py`, issue template |
| **4 — Automation** | GitHub Action, reports, lifecycle, agent | `flaky-test-detection.yml`, `reporter.py`, `main.py`, agent `.md` |

**MVP (Phases 1–2):** Collect data from Rolling pipeline, detect flaky tests, print report. Validates algorithm with real data before enabling issue filing.

---

## 12. Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| ADO API rate limiting | Batch requests, 0.3s delays, cache pipeline IDs |
| False positive detection | Conservative thresholds, dry-run mode, max 5 issues/run |
| Database size in GH cache | Cosmos DB serverless has no storage worries; TTL auto-cleans old data |
| Pipeline definition ID changes | Resolve by name at runtime, never hard-code |
| Team notification fatigue | Weekly digest, severity tiers, max issue rate |
