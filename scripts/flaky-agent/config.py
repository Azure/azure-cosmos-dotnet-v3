"""
Flaky Test Detection Agent — Configuration

All settings are centralized here with environment variable overrides.
"""

import os
from dataclasses import dataclass, field
from typing import List


@dataclass
class FlakyAgentConfig:
    """Configuration for the Flaky Test Detection Agent."""

    # Azure DevOps
    ado_org: str = "cosmos-db-sdk-public"
    ado_project: str = "cosmos-db-sdk-public"
    ado_api_version: str = "7.0"

    # Pipelines to monitor (definition names — resolved to IDs at runtime)
    pipelines: List[str] = field(default_factory=lambda: [
        "azure-cosmos-dotnet-v3",
        "azure-cosmos-dotnet-v3-rolling",
        "azure-cosmos-dotnet-v3-cron",
        "azure-cosmos-dotnet-v3-functional",
    ])

    # Collection
    default_lookback_hours: int = 48
    max_lookback_hours: int = 168  # 1 week
    collection_batch_size: int = 100
    request_delay_seconds: float = 0.3  # Rate limit safety margin

    # Detection thresholds
    min_runs_for_analysis: int = 10
    fliprate_suspected: float = 0.15
    fliprate_confirmed: float = 0.30
    ewma_alpha: float = 0.3
    failure_rate_threshold: float = 0.05
    retry_pass_ewma_boost: float = 0.15

    # PR classification
    master_baseline_days: int = 14
    master_failure_rate_flaky: float = 0.10
    min_master_runs_for_clean: int = 20

    # Issue filing
    github_repo: str = "Azure/azure-cosmos-dotnet-v3"
    flaky_label: str = "flaky-test"
    auto_label: str = "automated"
    investigation_label: str = "needs-investigation"
    max_issues_per_run: int = 5

    # Lifecycle
    consecutive_passes_for_fixed: int = 50

    # Retention
    data_retention_days: int = 90

    # Cosmos DB
    cosmos_endpoint: str = ""  # Set via COSMOS_ENDPOINT env var
    cosmos_key: str = ""       # Set via COSMOS_KEY env var (or use DefaultAzureCredential)
    cosmos_database: str = "flaky-test-agent"
    cosmos_container_executions: str = "test-executions"
    cosmos_container_registry: str = "flaky-registry"
    cosmos_container_issues: str = "filed-issues"
    cosmos_execution_ttl: int = 7776000  # 90 days in seconds

    @classmethod
    def from_env(cls) -> "FlakyAgentConfig":
        """Create config with environment variable overrides."""
        config = cls()
        config.cosmos_endpoint = os.environ.get("COSMOS_ENDPOINT", config.cosmos_endpoint)
        config.cosmos_key = os.environ.get("COSMOS_KEY", config.cosmos_key)
        config.default_lookback_hours = int(
            os.environ.get("FLAKY_LOOKBACK_HOURS", config.default_lookback_hours)
        )
        config.max_issues_per_run = int(
            os.environ.get("FLAKY_MAX_ISSUES", config.max_issues_per_run)
        )
        config.github_repo = os.environ.get("FLAKY_GITHUB_REPO", config.github_repo)
        return config

    @property
    def ado_base_url(self) -> str:
        return f"https://dev.azure.com/{self.ado_org}/{self.ado_project}/_apis"

    def dump(self) -> str:
        """Print config for debugging."""
        lines = ["Flaky Agent Configuration:"]
        for k, v in self.__dict__.items():
            lines.append(f"  {k}: {v}")
        return "\n".join(lines)
