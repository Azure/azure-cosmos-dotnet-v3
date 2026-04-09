# Flaky Test Detection Agent — Implementation Plan

## Overview

This document describes the implementation plan for each deliverable. Each phase builds on the previous one, and can be delivered and validated independently.

---

## Phase 1 — Data Foundation

### 1.1 `scripts/flaky-agent/config.py` — Configuration

Central configuration with environment variable overrides.

**Key parameters:**

```python
# Azure DevOps
ado_org = "cosmos-db-sdk-public"
ado_project = "cosmos-db-sdk-public"

# Pipelines to monitor (resolved to definition IDs at runtime)
pipelines = [
    "azure-cosmos-dotnet-v3",            # PR pipeline
    "azure-cosmos-dotnet-v3-rolling",    # Rolling (4×/day weekdays)
    "azure-cosmos-dotnet-v3-cron",       # Cron (every 6h, cross-platform)
    "azure-cosmos-dotnet-v3-functional", # Functional (PR-triggered)
]

# Detection thresholds
min_runs_for_analysis = 10
fliprate_suspected = 0.15
fliprate_confirmed = 0.30
ewma_alpha = 0.3

# Issue filing
github_repo = "Azure/azure-cosmos-dotnet-v3"
max_issues_per_run = 5
data_retention_days = 90
```

**Environment variables:**
- `ADO_PAT` — Azure DevOps Personal Access Token
- `FLAKY_DB_PATH` — SQLite database path (default: `flaky-test-data.db`)
- `FLAKY_DRY_RUN` — Skip issue filing when `true`
- `FLAKY_LOOKBACK_HOURS` — Override default lookback window

**Acceptance criteria:**
- [ ] All config loadable from env vars with defaults
- [ ] Pipeline names validate against ADO API on first run
- [ ] Config printable for debugging (`config.dump()`)

---

### 1.2 `scripts/flaky-agent/ado_client.py` — Azure DevOps REST API Client

HTTP client for the ADO REST API with retry, pagination, and rate limiting.

**Methods to implement:**

| Method | Endpoint | Returns |
|--------|----------|---------|
| `get_pipeline_definitions()` | `GET /build/definitions` | List of `{id, name}` |
| `get_builds(definition_id, min_time, top)` | `GET /build/builds` | List of completed builds |
| `get_test_runs(build_id)` | `GET /test/runs?buildUri=...` | Test runs for a build |
| `get_test_results(run_id, top)` | `GET /test/runs/{id}/results` | Individual test outcomes |
| `get_build_timeline(build_id)` | `GET /build/builds/{id}/timeline` | Build stages/jobs/tasks |
| `get_build_log(build_id, log_id)` | `GET /build/builds/{id}/logs/{id}` | Raw log text |

**Implementation details:**
- `requests.Session` with `HTTPAdapter(max_retries=Retry(total=3, backoff_factor=1, status_forcelist=[429, 500, 502, 503]))`
- Pagination: ADO uses `continuationToken` header; loop until absent
- Rate limit: 0.3s sleep between batch calls to stay well under 200 req/min
- Auth: Basic auth with empty username, PAT as password
- All endpoints use `api-version=7.0`

**Acceptance criteria:**
- [ ] Authenticates with PAT and fetches builds
- [ ] Handles pagination (>1000 test results per run)
- [ ] Retries on 429/5xx with exponential backoff
- [ ] Pipeline definition name → ID resolution cached per session
- [ ] Unit tests with mocked HTTP responses

---

### 1.3 `scripts/flaky-agent/database.py` — SQLite Data Store

Database schema, connection management, and query helpers.

**Tables:**

1. **`test_executions`** — Every individual test outcome
   - PK: `id` (auto-increment)
   - Key fields: `test_name`, `outcome`, `build_id`, `pipeline_name`, `run_timestamp`, `retry_attempt`
   - Enrichment: `error_message`, `stack_trace`, `os`, `emulator_used`, `emulator_healthy`, `pr_number`
   - Indexes on: `test_name`, `run_timestamp`, `pipeline_name`, `outcome`, `build_id`, `pr_number`

2. **`flaky_registry`** — Aggregated per-test flakiness scores
   - PK: `test_name`
   - Scores: `fliprate`, `ewma_fliprate`, `failure_rate`
   - Counts: `total_runs`, `total_failures`, `consecutive_failures`
   - State: `status` enum, `github_issue_number`
   - Metadata: `primary_error_pattern`, `correlated_conditions` (JSON), `affected_pipelines` (JSON)

3. **`filed_issues`** — Test-to-issue mapping for dedup
   - Composite PK: `(test_name, issue_number)`
   - Fields: `filed_at`, `issue_status`

4. **`schema_version`** — Migration tracking

**Key methods:**

```python
class Database:
    def __init__(self, path: str): ...
    def initialize(self): ...                           # Create tables if not exist
    def insert_executions(self, rows: list[dict]): ...  # Bulk insert with executemany
    def get_outcome_sequence(self, test_name, branch_filter, days): ... # Chronological outcomes
    def get_tests_with_min_runs(self, min_runs, days): ...
    def update_registry(self, entries: list): ...       # Upsert flaky_registry
    def get_unfiled_flaky_tests(self): ...              # confirmed_flaky with no issue
    def record_filed_issue(self, test_name, issue_number): ...
    def check_issue_exists(self, test_name): ...        # Local dedup check
    def cleanup(self, retention_days): ...              # Delete old data + VACUUM
    def close(self): ...
```

**Dedup strategy for `insert_executions`:**
- Use `INSERT OR IGNORE` with a unique constraint on `(build_id, test_name, retry_attempt)`
- This makes re-runs of the collector idempotent

**Acceptance criteria:**
- [ ] Schema creates cleanly on empty database
- [ ] Bulk insert handles 10K+ rows efficiently
- [ ] Idempotent: re-inserting same build data is a no-op
- [ ] Cleanup respects retention and runs VACUUM
- [ ] Migration system supports future schema changes

---

### 1.4 `scripts/flaky-agent/collector.py` — Test Result Collection

Orchestrates data collection: fetches builds, enriches results, stores in database.

**Collection flow:**

```
1. Resolve pipeline names to definition IDs (cached)
2. For each pipeline:
   a. Fetch builds completed since last collection (or lookback window)
   b. For each build:
      i.   Fetch build timeline (detect retries, emulator health)
      ii.  Fetch test runs
      iii. For each test run: fetch all test results (paginated)
      iv.  Enrich each result with build context
   c. Bulk insert into database
3. Run cleanup (delete data older than retention period)
4. Log summary: N builds processed, M test results collected
```

**Enrichment fields derived from build/timeline:**

| Field | Source | Logic |
|-------|--------|-------|
| `pipeline_name` | `build.definition.name` | Map to "PR", "Rolling", "Cron", "Functional" |
| `pr_number` | `build.sourceBranch` | Extract from `refs/pull/{N}/merge` |
| `os` | `build.queue.pool.name` or job name | Detect "Linux", "MacOS", "Windows" |
| `retry_attempt` | `timeline.records[].previousAttempts` | Count of previous attempts |
| `emulator_used` | `testRun.name` | Contains "EmulatorTests" |
| `emulator_healthy` | Build timeline log parsing | Check if emulator needed restart |
| `test_class` | `automatedTestName` | Second-to-last dot-separated segment |
| `test_assembly` | `automatedTestStorage` | Strip `.dll` extension |

**Emulator health detection:**

Parse the build timeline for the "Waiting for Cosmos DB Emulator status" task. If its log contains "Shutting down and restarting", mark `emulator_healthy = False` for all emulator tests in that build.

**Acceptance criteria:**
- [ ] Collects from all 4 pipeline types
- [ ] Correctly extracts PR numbers from branch refs
- [ ] Detects retry attempts from build timeline
- [ ] Handles builds with 0 test runs gracefully
- [ ] Idempotent on re-run (dedup by build_id + test_name + retry)
- [ ] First run collects 48h of history; subsequent runs are incremental
- [ ] Logs progress: pipelines processed, builds scanned, results stored

---

## Phase 2 — Detection Engine

### 2.1 `scripts/flaky-agent/analyzer.py` — Flakiness Detection

Core detection algorithm: scores every test for flakiness using fliprate and EWMA.

**Algorithm steps:**

```
1. Query all tests with ≥ min_runs executions on master in the last 30 days
2. For each test:
   a. Get chronological outcome sequence [P, F, P, P, F, ...]
   b. Calculate raw fliprate
   c. Calculate EWMA fliprate (α = 0.3)
   d. Calculate failure rate
   e. Check for retry-pass pattern (same build: fail attempt 0, pass attempt 1+)
   f. If retry-pass detected, boost EWMA by 0.15
   g. Classify: stable / suspected / confirmed_flaky
3. Update flaky_registry with new scores
4. Detect status transitions (monitoring → suspected → confirmed)
5. Return list of flaky/suspected tests sorted by EWMA descending
```

**Retry-pass detection SQL:**

```sql
SELECT DISTINCT e1.test_name
FROM test_executions e1
JOIN test_executions e2
  ON e1.test_name = e2.test_name AND e1.build_id = e2.build_id
WHERE e1.outcome = 'Failed' AND e1.retry_attempt = 0
  AND e2.outcome = 'Passed' AND e2.retry_attempt > 0
  AND e1.run_timestamp > datetime('now', '-14 days')
```

**Acceptance criteria:**
- [ ] Fliprate: `[P,P,P,P] → 0.0`, `[P,F,P,F] → 1.0`, `[P,P,F,P] → 0.67`
- [ ] EWMA weights recent events more heavily
- [ ] Tests with 100% failure rate excluded (broken, not flaky)
- [ ] Retry-pass boosts EWMA score
- [ ] Registry updated with scores and status transitions
- [ ] Unit tests for all edge cases

---

### 2.2 `scripts/flaky-agent/classifier.py` — PR Failure Classification

Determines whether a test failure on a PR is caused by the PR or is pre-existing flakiness.

**Signal priority (checked in order):**

1. **Registry check** — Is test already `confirmed_flaky`? → `flaky` (95%)
2. **Emulator health** — Did emulator fail in this build? → `infrastructure` (80%)
3. **Master baseline** — >10% failure rate on master? → `flaky` (85%)
4. **Retry-pass** — Passed on retry in this build? → `flaky` (90%)
5. **Error pattern** — Matches known flaky patterns? → `flaky` (70%)
6. **Clean master** — 0% failure on master with 20+ runs? → `pr_regression` (85%)
7. **Default** — Insufficient data → `unknown` (30%)

**Known flaky error patterns:**

```python
PATTERNS = {
    "timeout": r"TaskCanceledException|OperationCanceledException|timed out",
    "emulator_connection": r"Connection refused|localhost:8081|ECONNREFUSED",
    "race_condition": r"Assert\.(AreEqual|IsTrue|IsFalse) failed",
    "throttling": r"Request rate is too large|429|TooManyRequests",
    "socket": r"SocketException|EPIPE|connection reset",
    "telemetry_signal": r"ManualResetEventSlim|telemetry.*signal|Wait.*timed",
    "concurrent_mod": r"InvalidOperationException.*modified",
}
```

**Acceptance criteria:**
- [ ] Returns classification enum + confidence + reason
- [ ] Known flaky tests correctly classified without master data
- [ ] Clean master history → `pr_regression`
- [ ] Emulator startup failures → `infrastructure`
- [ ] Unit tests with mock DB data for each signal path

---

### 2.3 `scripts/flaky-agent/correlator.py` — Environmental Correlation

Analyzes which environmental factors correlate with test failures.

**Dimensions analyzed:**

1. **Time of day** — Failure hour distribution vs. uniform
2. **Day of week** — Weekday vs. weekend failure rates
3. **OS** — Per-OS failure rate comparison
4. **Pipeline** — Which pipelines see failures
5. **Emulator health** — Failure rate with healthy vs. unhealthy emulator
6. **Job group** — Pipeline 1/2/3/4 correlation
7. **Duration anomaly** — Failures with >3× median success duration
8. **Retry behavior** — First attempt vs. retry pass rates

**Output:** `CorrelationReport` with per-dimension analysis + `format_summary()` producing markdown for issue body.

**Acceptance criteria:**
- [ ] All 8 dimensions analyzed
- [ ] Markdown output is clear and actionable
- [ ] Handles sparse data (tests with <5 failures)
- [ ] Correctly identifies emulator-specific failures

---

## Phase 3 — Issue Filing & Notification

### 3.1 `scripts/flaky-agent/codeowners.py` — Team Mapper

Parses `CODEOWNERS` file and maps test names to responsible owners.

**Mapping strategy:**
1. Match test namespace/class against component patterns
2. Fall back to default test owners
3. Limit to 3 assignees per issue

**Component patterns from CODEOWNERS:**

| Test namespace contains | Owners |
|------------------------|--------|
| `Query` | @adityasa, @neildsh, @kirankumarkolli |
| `Linq` | @khdang, @adityasa, @kirankumarkolli |
| `ChangeFeed`, `Batch`, `ReadFeed` | @khdang, @sboshra, @adityasa |
| `Tracing` | @khdang, @sboshra, @adityasa |
| `Contract` | @kirillg, @kirankumarkolli, @FabianMeiswinkel |
| `Encryption` | @kirankumarkolli, @sboshra |
| Default | @khdang, @sboshra, @kirankumarkolli |

**Acceptance criteria:**
- [ ] Correctly maps tests to owners from CODEOWNERS
- [ ] Returns max 3 owners per test
- [ ] Falls back to defaults for unmatched tests

---

### 3.2 `scripts/flaky-agent/fix_proposer.py` — Fix Suggestions

Generates markdown fix proposals based on the error pattern category.

**7 patterns with code-level fix suggestions:**

1. **Timeout** → Increase `[Timeout]`, use explicit `ManualResetEventSlim.Wait(TimeSpan)`
2. **Emulator connection** → Add health check in `[ClassInitialize]` with `ReadAccountAsync()`
3. **Race condition** → Add polling/retry before assertion, ensure proper `await`
4. **Throttling** → Add retry with backoff, reduce test parallelism
5. **Telemetry signal** → Reset `ManualResetEventSlim` in loops, add timeout to `Wait()`
6. **Socket error** → Add connection retry, ensure `using` disposal
7. **Concurrent modification** → Use `ConcurrentDictionary` or snapshot collection

**Falls back to generic investigation steps when no pattern matches.**

**Acceptance criteria:**
- [ ] Each pattern returns a specific, actionable fix with code snippet
- [ ] Includes correlation insights (emulator-specific, timeout-likely)
- [ ] Generic fallback includes diagnostic commands

---

### 3.3 `scripts/flaky-agent/issue_filer.py` — GitHub Issue Creation

Creates GitHub issues via `gh` CLI with full context.

**Issue structure:**
- **Title:** `🔴 Flaky Test: {ClassName}.{MethodName}`
- **Labels:** `flaky-test`, `automated`, `needs-investigation`
- **Assignees:** From CODEOWNERS (max 3)
- **Body:** Metrics table, error, correlations, affected pipelines, proposed fix, action items

**Dedup flow:**
```
1. Check filed_issues table for open issue → skip if exists
2. Search GitHub: gh search issues --repo {repo} --label flaky-test --state open "{method_name}"
3. If found → skip; if not → create issue → record in filed_issues
```

**Rate limit:** Max 5 issues per agent run.

**PR comment capability:** When classifying a PR failure as flaky, post:
```
⚠️ Known Flaky Test Failure: {name}
This failure is caused by a known flaky test, not your PR.
Tracking Issue: #{issue_number}
Safe to re-run the failed job.
```

**Acceptance criteria:**
- [ ] Issues filed with correct labels, assignees, and body
- [ ] Dedup prevents duplicate issues
- [ ] Max 5 issues per run enforced
- [ ] PR comments clear and actionable
- [ ] Records filed issues in local database
- [ ] Handles `gh` CLI errors gracefully

---

### 3.4 `scripts/flaky-agent/.github/ISSUE_TEMPLATE/flaky_test.md`

Dedicated issue template for manually reporting flaky tests (complements automated filing).

**Sections:** Test name, assembly, failure frequency, error message, affected pipelines, environmental factors, local repro command.

---

## Phase 4 — Automation & Reporting

### 4.1 `.github/workflows/flaky-test-detection.yml` — GitHub Action

**Schedule:**
- `cron: '30 7 * * 1-5'` — Weekday mornings after overnight rolling builds
- `cron: '0 13 * * 1-5'` — Weekday afternoon check
- `workflow_dispatch` — Manual with `lookback_hours` and `dry_run` inputs

**Steps:**
1. Checkout repo
2. Setup Python 3.12
3. Install dependencies (`pip install -r scripts/flaky-agent/requirements.txt`)
4. Restore SQLite database from GitHub Actions cache
5. Run `main.py collect --lookback {hours}`
6. Run `main.py analyze`
7. Run `main.py file-issues` (skip if dry_run)
8. Run `main.py report >> $GITHUB_STEP_SUMMARY`
9. Save database to cache + upload as artifact

**Secrets:** `ADO_PAT` (repo secret), `GITHUB_TOKEN` (auto-provided)

**Acceptance criteria:**
- [ ] Runs successfully on schedule
- [ ] Database persists across runs via Actions cache
- [ ] Dry-run mode skips issue filing
- [ ] Report visible in GitHub Actions step summary
- [ ] Handles first run (no existing DB) gracefully
- [ ] Completes within 30-minute timeout

---

### 4.2 `scripts/flaky-agent/main.py` — CLI Entrypoint

Subcommands:

| Command | Description | Exit Codes |
|---------|-------------|------------|
| `collect --lookback N` | Collect test results from ADO | 0=ok, 1=error |
| `analyze` | Run flakiness analysis | 0=ok, 1=error |
| `file-issues [--dry-run] [--max-issues N]` | File GitHub issues | 0=none, 2=filed |
| `report` | Print markdown summary | 0=ok |
| `classify --test-name X --pr-number N` | Classify PR failure | 0=ok |
| `run [--dry-run]` | Full pipeline | 0=ok, 1=error, 2=filed |

**Acceptance criteria:**
- [ ] All subcommands work independently
- [ ] `run` chains: collect → analyze → file → report
- [ ] Clear console output with counts and progress
- [ ] Exit code 2 when issues are filed (useful for CI alerting)

---

### 4.3 `scripts/flaky-agent/reporter.py` — Weekly Summary

Generates a markdown report for the GitHub Actions step summary.

**Sections:**
1. **Summary stats** — New flaky, fixed, quarantined, overall reliability %
2. **Top 10 flakiest tests** — Ranked by EWMA with failure rate and affected pipelines
3. **New flaky tests this week** — Detected since last report
4. **Recently fixed tests** — Tests that stabilized
5. **Pipeline health** — Per-pipeline pass rates and failure breakdowns
6. **Environmental insights** — Emulator-related failures, OS-specific, time correlations

**Acceptance criteria:**
- [ ] Renders correctly in GitHub Actions step summary (markdown)
- [ ] Includes links to issues and builds
- [ ] Shows week-over-week trends when history available

---

### 4.4 `.github/agents/flaky-test-agent.agent.md` — Copilot Agent

Interactive agent following the established repo pattern (like `issue-fix-agent.agent.md`).

**Structure:**
1. Quick Start prompts
2. Prerequisites (ADO PAT, gh CLI)
3. Capabilities table
4. Workflow phases
5. Troubleshooting

**Acceptance criteria:**
- [ ] Follows existing `.agent.md` structure and conventions
- [ ] Documents all prerequisites
- [ ] Includes example prompts for each capability
- [ ] Troubleshooting section for common issues (SAML, PAT expiry)

---

### 4.5 Lifecycle Management (in `analyzer.py` + `issue_filer.py`)

**State transitions:** Automated status changes in `flaky_registry` based on detection scores and source code analysis.

**Auto-close comment:** When a filed test passes 50 consecutive master runs, post a comment on the issue suggesting closure.

**Regression detection:** If a previously `fixed` test starts failing again, transition back to `monitoring` for re-analysis.

**Acceptance criteria:**
- [ ] All state transitions logged and auditable
- [ ] Auto-close comments include evidence (run count, date range)
- [ ] Regression re-opens tracking

---

## Dependency Graph

```
Phase 1:  config ──► ado_client ──► database ──► collector
                                        │
Phase 2:                                ▼
          analyzer ◄── database ──► classifier
              │                        │
              ▼                        ▼
          correlator              (PR comment)
              │
Phase 3:      ▼
          codeowners ──► fix_proposer ──► issue_filer
                                              │
Phase 4:                                      ▼
          main.py ──► GH Action ──► reporter ──► agent.md
```

All Phase 2+ components depend on Phase 1 (data in the database). Phase 3 depends on Phase 2 (detection results). Phase 4 wires everything together.
