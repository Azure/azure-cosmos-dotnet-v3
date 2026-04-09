"""
Flaky Test Detection Agent — Flakiness Analyzer

Core detection algorithm using fliprate and EWMA scoring.
"""

from dataclasses import dataclass, field
from typing import List, Optional, Set

from config import FlakyAgentConfig
from database import Database


@dataclass
class FlakyTestResult:
    """Result of flakiness analysis for a single test."""
    test_name: str
    test_class: str
    test_assembly: str
    fliprate: float
    ewma_fliprate: float
    total_runs: int
    total_failures: int
    failure_rate: float
    last_failure: Optional[str]
    last_success: Optional[str]
    status: str  # 'suspected' or 'confirmed_flaky'
    affected_pipelines: List[str] = field(default_factory=list)
    primary_error: Optional[str] = None
    first_seen_flaky: Optional[str] = None


class FlakinessAnalyzer:
    """Analyzes test execution history to detect flaky tests."""

    def __init__(self, config: FlakyAgentConfig, db: Database):
        self.config = config
        self.db = db

    def analyze_all_tests(self) -> List[FlakyTestResult]:
        """Run flakiness analysis on all tests with sufficient data."""
        tests = self.db.get_tests_with_min_runs(self.config.min_runs_for_analysis)
        retry_pass_tests = set(self.db.get_retry_pass_tests())

        results = []
        for test in tests:
            result = self._analyze_test(test, retry_pass_tests)
            if result:
                results.append(result)

        return sorted(results, key=lambda r: r.ewma_fliprate, reverse=True)

    def _analyze_test(self, test: dict,
                      retry_pass_tests: Set[str]) -> Optional[FlakyTestResult]:
        """Analyze a single test for flakiness."""
        outcomes = self.db.get_outcome_sequence(test["test_name"])

        if len(outcomes) < 2:
            return None

        # A test must have both passes and failures to be flaky
        has_failures = test["failures"] > 0
        has_passes = test["failures"] < test["total_runs"]
        if not (has_failures and has_passes):
            return None

        fliprate = self.calculate_fliprate(outcomes)
        ewma = self.calculate_ewma_fliprate(outcomes, self.config.ewma_alpha)
        failure_rate = test["failures"] / test["total_runs"]

        # Boost EWMA for tests that pass on retry
        if test["test_name"] in retry_pass_tests:
            ewma = min(1.0, ewma + self.config.retry_pass_ewma_boost)

        # Determine status
        if ewma >= self.config.fliprate_confirmed and test["total_runs"] >= 20:
            status = "confirmed_flaky"
        elif ewma >= self.config.fliprate_suspected:
            status = "suspected"
        else:
            return None  # Below threshold

        pipelines = test["pipelines"].split(",") if test["pipelines"] else []

        return FlakyTestResult(
            test_name=test["test_name"],
            test_class=test["test_class"] or "",
            test_assembly=test["test_assembly"] or "",
            fliprate=fliprate,
            ewma_fliprate=ewma,
            total_runs=test["total_runs"],
            total_failures=test["failures"],
            failure_rate=failure_rate,
            last_failure=test["last_failure"],
            last_success=test["last_success"],
            status=status,
            affected_pipelines=list(set(pipelines)),
        )

    def update_registry(self, results: List[FlakyTestResult]):
        """Update the flaky registry with analysis results."""
        entries = []
        for r in results:
            entries.append({
                "test_name": r.test_name,
                "test_class": r.test_class,
                "test_assembly": r.test_assembly,
                "fliprate": r.fliprate,
                "ewma_fliprate": r.ewma_fliprate,
                "total_runs": r.total_runs,
                "total_failures": r.total_failures,
                "failure_rate": r.failure_rate,
                "first_seen_flaky": r.first_seen_flaky,
                "last_failure": r.last_failure,
                "last_success": r.last_success,
                "status": r.status,
                "primary_error_pattern": r.primary_error,
                "affected_pipelines": ",".join(r.affected_pipelines),
            })
        self.db.update_registry(entries)

    # -- Core Algorithms --

    @staticmethod
    def calculate_fliprate(outcomes: List[str]) -> float:
        """
        Calculate fliprate: proportion of state transitions in outcome sequence.

        A perfectly stable test (all pass or all fail) has fliprate 0.0.
        A maximally flaky test (alternating) has fliprate 1.0.
        """
        if len(outcomes) < 2:
            return 0.0
        flips = sum(
            1 for i in range(1, len(outcomes)) if outcomes[i] != outcomes[i - 1]
        )
        return flips / (len(outcomes) - 1)

    @staticmethod
    def calculate_ewma_fliprate(outcomes: List[str], alpha: float = 0.3) -> float:
        """
        Exponentially Weighted Moving Average of flip events.

        Gives more weight to recent state transitions, enabling early
        detection of newly-flaky tests.
        """
        if len(outcomes) < 2:
            return 0.0
        ewma = 0.0
        for i in range(1, len(outcomes)):
            flip = 1.0 if outcomes[i] != outcomes[i - 1] else 0.0
            ewma = alpha * flip + (1 - alpha) * ewma
        return ewma
