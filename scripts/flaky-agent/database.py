"""
Flaky Test Detection Agent — SQLite Data Store

Schema, migrations, and query helpers for test execution history,
flaky test registry, and filed issue tracking.
"""

import json
import sqlite3
from pathlib import Path
from typing import Any, Dict, List, Optional


SCHEMA_VERSION = 1

SCHEMA_SQL = """
CREATE TABLE IF NOT EXISTS test_executions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    test_name TEXT NOT NULL,
    test_class TEXT,
    test_assembly TEXT,
    outcome TEXT NOT NULL,
    duration_ms INTEGER,
    error_message TEXT,
    stack_trace TEXT,
    build_id INTEGER NOT NULL,
    pipeline_name TEXT NOT NULL,
    pipeline_definition_id INTEGER,
    source_branch TEXT,
    pr_number INTEGER,
    run_timestamp TEXT NOT NULL,
    retry_attempt INTEGER DEFAULT 0,
    os TEXT DEFAULT 'Windows',
    job_name TEXT,
    emulator_used INTEGER DEFAULT 0,
    emulator_healthy INTEGER DEFAULT 1,
    test_run_title TEXT,
    created_at TEXT DEFAULT (datetime('now')),
    UNIQUE(build_id, test_name, retry_attempt)
);

CREATE INDEX IF NOT EXISTS idx_te_test_name ON test_executions(test_name);
CREATE INDEX IF NOT EXISTS idx_te_timestamp ON test_executions(run_timestamp);
CREATE INDEX IF NOT EXISTS idx_te_pipeline ON test_executions(pipeline_name);
CREATE INDEX IF NOT EXISTS idx_te_outcome ON test_executions(outcome);
CREATE INDEX IF NOT EXISTS idx_te_build ON test_executions(build_id);
CREATE INDEX IF NOT EXISTS idx_te_pr ON test_executions(pr_number);

CREATE TABLE IF NOT EXISTS flaky_registry (
    test_name TEXT PRIMARY KEY,
    test_class TEXT,
    test_assembly TEXT,
    fliprate REAL DEFAULT 0.0,
    ewma_fliprate REAL DEFAULT 0.0,
    total_runs INTEGER DEFAULT 0,
    total_failures INTEGER DEFAULT 0,
    failure_rate REAL DEFAULT 0.0,
    consecutive_failures INTEGER DEFAULT 0,
    max_consecutive_failures INTEGER DEFAULT 0,
    first_seen_flaky TEXT,
    last_failure TEXT,
    last_success TEXT,
    status TEXT DEFAULT 'monitoring',
    github_issue_number INTEGER,
    primary_error_pattern TEXT,
    correlated_conditions TEXT,
    affected_pipelines TEXT,
    updated_at TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS filed_issues (
    test_name TEXT NOT NULL,
    issue_number INTEGER NOT NULL,
    filed_at TEXT DEFAULT (datetime('now')),
    issue_status TEXT DEFAULT 'open',
    PRIMARY KEY (test_name, issue_number)
);

CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER PRIMARY KEY,
    applied_at TEXT DEFAULT (datetime('now'))
);
"""


class Database:
    """SQLite database for flaky test tracking."""

    def __init__(self, path: str):
        self.path = path
        self.conn = sqlite3.connect(path)
        self.conn.row_factory = sqlite3.Row
        self.conn.execute("PRAGMA journal_mode=WAL")
        self.conn.execute("PRAGMA foreign_keys=ON")
        self.initialize()

    def initialize(self):
        """Create tables if they don't exist."""
        self.conn.executescript(SCHEMA_SQL)
        # Set schema version if not already set
        cursor = self.conn.execute("SELECT COUNT(*) FROM schema_version")
        if cursor.fetchone()[0] == 0:
            self.conn.execute(
                "INSERT INTO schema_version (version) VALUES (?)",
                [SCHEMA_VERSION],
            )
        self.conn.commit()

    # -- Test Executions --

    def insert_executions(self, rows: List[Dict[str, Any]]):
        """Bulk insert test executions (ignores duplicates)."""
        if not rows:
            return

        self.conn.executemany(
            """INSERT OR IGNORE INTO test_executions
            (test_name, test_class, test_assembly, outcome, duration_ms,
             error_message, stack_trace, build_id, pipeline_name,
             pipeline_definition_id, source_branch, pr_number, run_timestamp,
             retry_attempt, os, job_name, emulator_used, emulator_healthy,
             test_run_title)
            VALUES
            (:test_name, :test_class, :test_assembly, :outcome, :duration_ms,
             :error_message, :stack_trace, :build_id, :pipeline_name,
             :pipeline_definition_id, :source_branch, :pr_number, :run_timestamp,
             :retry_attempt, :os, :job_name, :emulator_used, :emulator_healthy,
             :test_run_title)
            """,
            rows,
        )
        self.conn.commit()

    def get_outcome_sequence(self, test_name: str, days: int = 30,
                             branch_filter: str = "%master%") -> List[str]:
        """Get chronologically ordered outcomes for a test."""
        cursor = self.conn.execute(
            """SELECT outcome FROM test_executions
            WHERE test_name = ? AND source_branch LIKE ?
              AND outcome IN ('Passed', 'Failed')
              AND run_timestamp > datetime('now', ?)
            ORDER BY run_timestamp ASC""",
            [test_name, branch_filter, f"-{days} days"],
        )
        return [row["outcome"] for row in cursor.fetchall()]

    def get_tests_with_min_runs(self, min_runs: int, days: int = 30) -> List[dict]:
        """Get tests with at least min_runs executions on master."""
        cursor = self.conn.execute(
            """SELECT test_name, test_class, test_assembly,
                   COUNT(*) as total_runs,
                   SUM(CASE WHEN outcome = 'Failed' THEN 1 ELSE 0 END) as failures,
                   MAX(CASE WHEN outcome = 'Failed' THEN run_timestamp END) as last_failure,
                   MAX(CASE WHEN outcome = 'Passed' THEN run_timestamp END) as last_success,
                   GROUP_CONCAT(DISTINCT pipeline_name) as pipelines
            FROM test_executions
            WHERE run_timestamp > datetime('now', ?)
              AND source_branch LIKE '%master%'
              AND outcome IN ('Passed', 'Failed')
            GROUP BY test_name
            HAVING total_runs >= ?""",
            [f"-{days} days", min_runs],
        )
        return [dict(row) for row in cursor.fetchall()]

    def get_retry_pass_tests(self, days: int = 14) -> List[str]:
        """Find tests that failed initially but passed on retry (same build)."""
        cursor = self.conn.execute(
            """SELECT DISTINCT e1.test_name
            FROM test_executions e1
            JOIN test_executions e2
              ON e1.test_name = e2.test_name AND e1.build_id = e2.build_id
            WHERE e1.outcome = 'Failed' AND e1.retry_attempt = 0
              AND e2.outcome = 'Passed' AND e2.retry_attempt > 0
              AND e1.run_timestamp > datetime('now', ?)""",
            [f"-{days} days"],
        )
        return [row["test_name"] for row in cursor.fetchall()]

    # -- Flaky Registry --

    def update_registry(self, entries: List[Dict[str, Any]]):
        """Upsert entries into the flaky registry."""
        for entry in entries:
            self.conn.execute(
                """INSERT INTO flaky_registry
                (test_name, test_class, test_assembly, fliprate, ewma_fliprate,
                 total_runs, total_failures, failure_rate, first_seen_flaky,
                 last_failure, last_success, status, primary_error_pattern,
                 affected_pipelines, updated_at)
                VALUES
                (:test_name, :test_class, :test_assembly, :fliprate, :ewma_fliprate,
                 :total_runs, :total_failures, :failure_rate, :first_seen_flaky,
                 :last_failure, :last_success, :status, :primary_error_pattern,
                 :affected_pipelines, datetime('now'))
                ON CONFLICT(test_name) DO UPDATE SET
                    fliprate = :fliprate,
                    ewma_fliprate = :ewma_fliprate,
                    total_runs = :total_runs,
                    total_failures = :total_failures,
                    failure_rate = :failure_rate,
                    last_failure = :last_failure,
                    last_success = :last_success,
                    status = CASE
                        WHEN flaky_registry.status IN ('issue_filed', 'quarantined')
                        THEN flaky_registry.status
                        ELSE :status
                    END,
                    primary_error_pattern = :primary_error_pattern,
                    affected_pipelines = :affected_pipelines,
                    updated_at = datetime('now')
                """,
                entry,
            )
        self.conn.commit()

    def get_unfiled_flaky_tests(self) -> List[dict]:
        """Get confirmed flaky tests that don't have an open issue."""
        cursor = self.conn.execute(
            """SELECT * FROM flaky_registry
            WHERE status = 'confirmed_flaky'
              AND (github_issue_number IS NULL
                   OR test_name NOT IN (
                       SELECT test_name FROM filed_issues WHERE issue_status = 'open'
                   ))
            ORDER BY ewma_fliprate DESC"""
        )
        return [dict(row) for row in cursor.fetchall()]

    def get_registry_entry(self, test_name: str) -> Optional[dict]:
        """Get a single registry entry."""
        cursor = self.conn.execute(
            "SELECT * FROM flaky_registry WHERE test_name = ?", [test_name]
        )
        row = cursor.fetchone()
        return dict(row) if row else None

    # -- Filed Issues --

    def record_filed_issue(self, test_name: str, issue_number: int):
        """Record that an issue was filed for a test."""
        self.conn.execute(
            "INSERT OR REPLACE INTO filed_issues (test_name, issue_number) VALUES (?, ?)",
            [test_name, issue_number],
        )
        self.conn.execute(
            """UPDATE flaky_registry SET
                github_issue_number = ?, status = 'issue_filed', updated_at = datetime('now')
            WHERE test_name = ?""",
            [issue_number, test_name],
        )
        self.conn.commit()

    def check_issue_exists(self, test_name: str) -> bool:
        """Check if an open issue exists for this test."""
        cursor = self.conn.execute(
            "SELECT 1 FROM filed_issues WHERE test_name = ? AND issue_status = 'open'",
            [test_name],
        )
        return cursor.fetchone() is not None

    # -- Maintenance --

    def cleanup(self, retention_days: int = 90):
        """Delete old test execution data and reclaim space."""
        self.conn.execute(
            "DELETE FROM test_executions WHERE run_timestamp < datetime('now', ?)",
            [f"-{retention_days} days"],
        )
        self.conn.commit()
        self.conn.execute("VACUUM")

    def close(self):
        """Close the database connection."""
        self.conn.close()
