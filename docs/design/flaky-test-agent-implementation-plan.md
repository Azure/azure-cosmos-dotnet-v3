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
- `COSMOS_ENDPOINT` — Cosmos DB account endpoint (e.g., `https://flaky-test-agent.documents.azure.com:443/`)
- `COSMOS_KEY` — Cosmos DB account key (or omit to use `DefaultAzureCredential`)
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

### 1.3 `scripts/flaky-agent/database.py` — Cosmos DB Data Store

Cosmos DB client, container management, and query helpers. Uses the Azure Cosmos DB Python SDK (NoSQL API) with the serverless tier to dogfood our own product.

**Why Cosmos DB over SQLite:**
- We are the Cosmos DB team — we should dogfood our own product
- Built-in TTL for automatic data retention (no manual cleanup)
- No GitHub Actions cache juggling — data is always available
- Serverless tier keeps cost near-zero for this workload (~$2–5/month)
- Point reads by `id` + partition key are ideal for the access patterns here
- Cross-query with SQL-like syntax already familiar to the team

**Cosmos DB Account Setup:**
- **Account:** Create a serverless NoSQL account (e.g., `flaky-test-agent`)
- **Database:** `flaky-test-agent`
- **Throughput:** Serverless (pay per RU consumed, no provisioned throughput)

**Containers:**

1. **`test-executions`** — Every individual test outcome
   - Partition key: `/test_name`
   - TTL: 90 days (`"ttl": 7776000` — automatic retention, no cleanup needed)
   - Unique key: `/build_id, /test_name, /retry_attempt`
   - Document schema:
     ```json
     {
       "id": "{build_id}_{test_name}_{retry_attempt}",
       "test_name": "Microsoft.Azure.Cosmos.Tests.CosmosHttpClientCoreTests.RetryTransientIssuesTestAsync",
       "test_class": "CosmosHttpClientCoreTests",
       "test_assembly": "Microsoft.Azure.Cosmos.Tests",
       "outcome": "Failed",
       "duration_ms": 31250,
       "error_message": "TaskCanceledException: ...",
       "stack_trace": "...",
       "build_id": 59172,
       "pipeline_name": "Rolling",
       "source_branch": "refs/heads/master",
       "pr_number": null,
       "run_timestamp": "2026-04-09T07:30:00Z",
       "retry_attempt": 0,
       "os": "Windows",
       "job_name": "EmulatorTests Release - Others",
       "emulator_used": true,
       "emulator_healthy": true,
       "test_run_title": "Microsoft.Azure.Cosmos.EmulatorTests",
       "ttl": 7776000
     }
     ```

2. **`flaky-registry`** — Aggregated per-test flakiness scores
   - Partition key: `/test_name`
   - TTL: disabled (permanent tracking)
   - Document schema:
     ```json
     {
       "id": "{test_name}",
       "test_name": "...",
       "test_class": "...",
       "test_assembly": "...",
       "fliprate": 0.45,
       "ewma_fliprate": 0.33,
       "total_runs": 48,
       "total_failures": 11,
       "failure_rate": 0.229,
       "consecutive_failures": 0,
       "first_seen_flaky": "2026-03-15T00:00:00Z",
       "last_failure": "2026-04-08T07:30:00Z",
       "last_success": "2026-04-09T07:30:00Z",
       "status": "issue_filed",
       "github_issue_number": 5761,
       "primary_error_pattern": "TaskCanceledException",
       "correlated_conditions": { "emulator_specific": true },
       "affected_pipelines": ["Rolling", "PR"]
     }
     ```

3. **`filed-issues`** — Test-to-issue mapping for dedup
   - Partition key: `/test_name`
   - TTL: disabled (permanent)
   - Document schema:
     ```json
     {
       "id": "{test_name}_{issue_number}",
       "test_name": "...",
       "issue_number": 5761,
       "filed_at": "2026-04-09T13:00:00Z",
       "issue_status": "open"
     }
     ```

**Key methods:**

```python
class Database:
    def __init__(self, endpoint: str, key: str): ...
    def initialize(self): ...                           # Create database + containers if not exist
    def insert_executions(self, items: list[dict]): ... # Bulk upsert with batch operations
    def get_outcome_sequence(self, test_name, days): ...# Cross-partition query, ordered by timestamp
    def get_tests_with_min_runs(self, min_runs, days): ... # Aggregate query
    def update_registry(self, entries: list): ...       # Upsert to flaky-registry
    def get_unfiled_flaky_tests(self): ...              # Query confirmed_flaky with no issue
    def record_filed_issue(self, test_name, issue_number): ...
    def check_issue_exists(self, test_name): ...        # Point read by test_name
    def close(self): ...                                # Close client
```

**Dedup strategy for `insert_executions`:**
- Use deterministic `id` = `{build_id}_{test_name_hash}_{retry_attempt}`
- Cosmos DB `upsert` makes re-runs idempotent — same document ID overwrites with identical data

**Query patterns:**

```sql
-- Get outcome sequence for a test (partition-scoped, efficient)
SELECT c.outcome, c.run_timestamp FROM c
WHERE c.test_name = @test_name
  AND c.source_branch LIKE '%master%'
  AND c.run_timestamp > @cutoff
ORDER BY c.run_timestamp ASC

-- Get tests with min runs (cross-partition aggregate)
SELECT c.test_name, c.test_class, c.test_assembly,
       COUNT(1) as total_runs,
       SUM(c.outcome = 'Failed' ? 1 : 0) as failures
FROM c
WHERE c.run_timestamp > @cutoff
  AND CONTAINS(c.source_branch, 'master')
  AND c.outcome IN ('Passed', 'Failed')
GROUP BY c.test_name, c.test_class, c.test_assembly
HAVING COUNT(1) >= @min_runs

-- Retry-pass detection
SELECT DISTINCT c.test_name FROM c
WHERE c.outcome = 'Failed' AND c.retry_attempt = 0
  AND EXISTS (
    SELECT VALUE 1 FROM c2 IN c
    WHERE c2.test_name = c.test_name AND c2.build_id = c.build_id
      AND c2.outcome = 'Passed' AND c2.retry_attempt > 0
  )
```

**Acceptance criteria:**
- [ ] Creates database and containers on first run (idempotent)
- [ ] Bulk upsert handles 10K+ items efficiently (batched by partition key)
- [ ] Idempotent: re-inserting same build data is a no-op (deterministic IDs)
- [ ] TTL automatically cleans up old test executions (no manual cleanup)
- [ ] Point reads for registry/issue lookups (single-digit ms latency)
- [ ] Cross-partition queries for aggregate analysis complete in <5s

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
   c. Bulk upsert into Cosmos DB
3. Log summary: N builds processed, M test results collected
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
4. Run `main.py collect --lookback {hours}` (writes to Cosmos DB)
5. Run `main.py analyze` (reads/writes Cosmos DB)
6. Run `main.py file-issues` (skip if dry_run)
7. Run `main.py report >> $GITHUB_STEP_SUMMARY`

**Secrets:** `ADO_PAT`, `COSMOS_ENDPOINT`, `COSMOS_KEY` (repo secrets), `GITHUB_TOKEN` (auto-provided)

**Acceptance criteria:**
- [ ] Runs successfully on schedule
- [ ] Connects to Cosmos DB and reads/writes data
- [ ] Dry-run mode skips issue filing
- [ ] Report visible in GitHub Actions step summary
- [ ] Handles first run (empty Cosmos DB) gracefully
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
