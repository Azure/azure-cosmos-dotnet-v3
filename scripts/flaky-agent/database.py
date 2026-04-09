"""
Flaky Test Detection Agent — Azure Cosmos DB Data Store

Uses Azure Cosmos DB (NoSQL API) for persistent storage of test execution
history, flaky test registry, and filed issue tracking. Dogfoods our own
product with the serverless tier for near-zero cost.
"""

import hashlib
import json
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

from azure.cosmos import CosmosClient, PartitionKey, exceptions
from azure.cosmos.container import ContainerProxy
from azure.cosmos.database import DatabaseProxy

from config import FlakyAgentConfig


class Database:
    """Azure Cosmos DB data store for flaky test tracking."""

    def __init__(self, config: FlakyAgentConfig):
        self.config = config
        self.client = CosmosClient(config.cosmos_endpoint, config.cosmos_key)
        self.db: Optional[DatabaseProxy] = None
        self.executions: Optional[ContainerProxy] = None
        self.registry: Optional[ContainerProxy] = None
        self.issues: Optional[ContainerProxy] = None
        self.initialize()

    def initialize(self):
        """Create database and containers if they don't exist."""
        self.db = self.client.create_database_if_not_exists(
            id=self.config.cosmos_database
        )

        # test-executions: partitioned by test_name, 90-day TTL
        self.executions = self.db.create_container_if_not_exists(
            id=self.config.cosmos_container_executions,
            partition_key=PartitionKey(path="/test_name"),
            default_ttl=self.config.cosmos_execution_ttl,
        )

        # flaky-registry: partitioned by test_name, no TTL (permanent)
        self.registry = self.db.create_container_if_not_exists(
            id=self.config.cosmos_container_registry,
            partition_key=PartitionKey(path="/test_name"),
        )

        # filed-issues: partitioned by test_name, no TTL (permanent)
        self.issues = self.db.create_container_if_not_exists(
            id=self.config.cosmos_container_issues,
            partition_key=PartitionKey(path="/test_name"),
        )

    # -- Test Executions --

    @staticmethod
    def _execution_id(build_id: int, test_name: str, retry_attempt: int) -> str:
        """Deterministic document ID for dedup on re-runs."""
        name_hash = hashlib.sha256(test_name.encode()).hexdigest()[:16]
        return f"{build_id}_{name_hash}_{retry_attempt}"

    def insert_executions(self, items: List[Dict[str, Any]]):
        """Bulk upsert test execution documents (idempotent)."""
        for item in items:
            item["id"] = self._execution_id(
                item["build_id"], item["test_name"], item.get("retry_attempt", 0)
            )
            item["ttl"] = self.config.cosmos_execution_ttl
            try:
                self.executions.upsert_item(item)
            except exceptions.CosmosHttpResponseError as e:
                if e.status_code == 429:
                    raise  # Let caller handle throttling
                print(f"Warning: Failed to upsert execution {item['id']}: {e.message}")

    def get_outcome_sequence(self, test_name: str, days: int = 30,
                             branch_filter: str = "master") -> List[str]:
        """Get chronologically ordered outcomes for a test on a branch."""
        cutoff = _days_ago_iso(days)
        query = (
            "SELECT c.outcome FROM c "
            "WHERE c.test_name = @test_name "
            "  AND CONTAINS(c.source_branch, @branch) "
            "  AND c.outcome IN ('Passed', 'Failed') "
            "  AND c.run_timestamp > @cutoff "
            "ORDER BY c.run_timestamp ASC"
        )
        params = [
            {"name": "@test_name", "value": test_name},
            {"name": "@branch", "value": branch_filter},
            {"name": "@cutoff", "value": cutoff},
        ]
        results = self.executions.query_items(
            query=query,
            parameters=params,
            partition_key=test_name,
        )
        return [item["outcome"] for item in results]

    def get_tests_with_min_runs(self, min_runs: int, days: int = 30) -> List[dict]:
        """Get tests with at least min_runs executions on master.

        Note: This is a cross-partition query (GROUP BY across all test_names).
        For the expected data volume (~100K documents), this completes in seconds.
        """
        cutoff = _days_ago_iso(days)
        query = (
            "SELECT "
            "  c.test_name, c.test_class, c.test_assembly, "
            "  COUNT(1) as total_runs, "
            "  SUM(c.outcome = 'Failed' ? 1 : 0) as failures "
            "FROM c "
            "WHERE c.run_timestamp > @cutoff "
            "  AND CONTAINS(c.source_branch, 'master') "
            "  AND c.outcome IN ('Passed', 'Failed') "
            "GROUP BY c.test_name, c.test_class, c.test_assembly"
        )
        params = [{"name": "@cutoff", "value": cutoff}]
        results = list(self.executions.query_items(
            query=query,
            parameters=params,
            enable_cross_partition_query=True,
        ))

        # Client-side filter for HAVING (Cosmos DB doesn't support HAVING)
        filtered = [r for r in results if r["total_runs"] >= min_runs]

        # Fetch last_failure / last_success per test (partition-scoped)
        for test in filtered:
            test["last_failure"] = self._last_outcome(test["test_name"], "Failed", days)
            test["last_success"] = self._last_outcome(test["test_name"], "Passed", days)
            test["pipelines"] = self._get_pipelines(test["test_name"], days)

        return filtered

    def _last_outcome(self, test_name: str, outcome: str, days: int) -> Optional[str]:
        """Get the timestamp of the most recent occurrence of an outcome."""
        cutoff = _days_ago_iso(days)
        query = (
            "SELECT TOP 1 c.run_timestamp FROM c "
            "WHERE c.test_name = @test_name AND c.outcome = @outcome "
            "  AND c.run_timestamp > @cutoff "
            "ORDER BY c.run_timestamp DESC"
        )
        params = [
            {"name": "@test_name", "value": test_name},
            {"name": "@outcome", "value": outcome},
            {"name": "@cutoff", "value": cutoff},
        ]
        results = list(self.executions.query_items(
            query=query, parameters=params, partition_key=test_name
        ))
        return results[0]["run_timestamp"] if results else None

    def _get_pipelines(self, test_name: str, days: int) -> str:
        """Get comma-separated distinct pipeline names for a test."""
        cutoff = _days_ago_iso(days)
        query = (
            "SELECT DISTINCT VALUE c.pipeline_name FROM c "
            "WHERE c.test_name = @test_name AND c.run_timestamp > @cutoff"
        )
        params = [
            {"name": "@test_name", "value": test_name},
            {"name": "@cutoff", "value": cutoff},
        ]
        results = list(self.executions.query_items(
            query=query, parameters=params, partition_key=test_name
        ))
        return ",".join(results)

    def get_retry_pass_tests(self, days: int = 14) -> List[str]:
        """Find tests that failed initially but passed on retry (same build).

        Uses a two-step approach: find failures with retry_attempt=0,
        then check if the same test+build has a passing retry.
        """
        cutoff = _days_ago_iso(days)
        # Step 1: Get test_name + build_id pairs where first attempt failed
        query = (
            "SELECT DISTINCT c.test_name, c.build_id FROM c "
            "WHERE c.outcome = 'Failed' AND c.retry_attempt = 0 "
            "  AND c.run_timestamp > @cutoff"
        )
        params = [{"name": "@cutoff", "value": cutoff}]
        failures = list(self.executions.query_items(
            query=query, parameters=params, enable_cross_partition_query=True,
        ))

        # Step 2: For each, check if a retry passed
        retry_pass_tests = set()
        for f in failures:
            retry_id = self._execution_id(f["build_id"], f["test_name"], 1)
            try:
                item = self.executions.read_item(
                    item=retry_id, partition_key=f["test_name"]
                )
                if item.get("outcome") == "Passed":
                    retry_pass_tests.add(f["test_name"])
            except exceptions.CosmosResourceNotFoundError:
                continue

        return list(retry_pass_tests)

    # -- Flaky Registry --

    def update_registry(self, entries: List[Dict[str, Any]]):
        """Upsert entries into the flaky registry container."""
        for entry in entries:
            test_name = entry["test_name"]
            entry["id"] = test_name
            entry["updated_at"] = _now_iso()

            # Preserve status if already issue_filed or quarantined
            existing = self.get_registry_entry(test_name)
            if existing and existing.get("status") in ("issue_filed", "quarantined"):
                entry["status"] = existing["status"]
                entry["github_issue_number"] = existing.get("github_issue_number")

            self.registry.upsert_item(entry)

    def get_unfiled_flaky_tests(self) -> List[dict]:
        """Get confirmed flaky tests that don't have an open issue."""
        query = (
            "SELECT * FROM c "
            "WHERE c.status = 'confirmed_flaky' "
            "ORDER BY c.ewma_fliprate DESC"
        )
        results = list(self.registry.query_items(
            query=query, enable_cross_partition_query=True,
        ))
        # Filter out tests that already have filed issues
        return [r for r in results if not self.check_issue_exists(r["test_name"])]

    def get_registry_entry(self, test_name: str) -> Optional[dict]:
        """Get a single registry entry by test name (point read)."""
        try:
            return self.registry.read_item(item=test_name, partition_key=test_name)
        except exceptions.CosmosResourceNotFoundError:
            return None

    # -- Filed Issues --

    def record_filed_issue(self, test_name: str, issue_number: int):
        """Record that an issue was filed for a test."""
        item = {
            "id": f"{test_name}_{issue_number}",
            "test_name": test_name,
            "issue_number": issue_number,
            "filed_at": _now_iso(),
            "issue_status": "open",
        }
        self.issues.upsert_item(item)

        # Update registry status
        entry = self.get_registry_entry(test_name)
        if entry:
            entry["status"] = "issue_filed"
            entry["github_issue_number"] = issue_number
            entry["updated_at"] = _now_iso()
            self.registry.upsert_item(entry)

    def check_issue_exists(self, test_name: str) -> bool:
        """Check if an open issue exists for this test (partition-scoped query)."""
        query = (
            "SELECT VALUE COUNT(1) FROM c "
            "WHERE c.test_name = @test_name AND c.issue_status = 'open'"
        )
        params = [{"name": "@test_name", "value": test_name}]
        results = list(self.issues.query_items(
            query=query, parameters=params, partition_key=test_name,
        ))
        return results[0] > 0 if results else False

    # -- Lifecycle --

    def close(self):
        """Close the Cosmos DB client."""
        # The Python SDK client doesn't require explicit close,
        # but we keep the method for interface consistency.
        pass


def _now_iso() -> str:
    """Return current UTC time as ISO 8601 string."""
    return datetime.now(timezone.utc).isoformat()


def _days_ago_iso(days: int) -> str:
    """Return ISO 8601 timestamp for N days ago."""
    from datetime import timedelta
    return (datetime.now(timezone.utc) - timedelta(days=days)).isoformat()
