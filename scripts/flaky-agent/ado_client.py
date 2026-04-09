"""
Flaky Test Detection Agent — Azure DevOps REST API Client

Wraps the ADO REST API with retry, pagination, and rate limiting.
"""

import time
from typing import Any, Dict, List, Optional

import requests
from requests.adapters import HTTPAdapter, Retry

from config import FlakyAgentConfig


class AzureDevOpsClient:
    """Client for the Azure DevOps REST API."""

    def __init__(self, pat: str, config: Optional[FlakyAgentConfig] = None):
        self.config = config or FlakyAgentConfig()
        self.base_url = self.config.ado_base_url
        self.api_version = self.config.ado_api_version

        self.session = requests.Session()
        self.session.auth = ("", pat)
        self.session.headers.update({"Accept": "application/json"})

        retry_strategy = Retry(
            total=3,
            backoff_factor=1,
            status_forcelist=[429, 500, 502, 503, 504],
        )
        adapter = HTTPAdapter(max_retries=retry_strategy)
        self.session.mount("https://", adapter)

        self._definition_cache: Dict[str, int] = {}

    def _get(self, path: str, params: Optional[dict] = None) -> dict:
        """Make a GET request with API version and rate limiting."""
        url = f"{self.base_url}{path}"
        params = params or {}
        params["api-version"] = self.api_version

        time.sleep(self.config.request_delay_seconds)
        response = self.session.get(url, params=params)
        response.raise_for_status()
        return response.json()

    def _get_paginated(self, path: str, params: Optional[dict] = None,
                       top: int = 1000) -> List[dict]:
        """Fetch all pages of a paginated endpoint."""
        params = params or {}
        params["$top"] = top
        all_items = []

        while True:
            data = self._get(path, params)
            items = data.get("value", [])
            all_items.extend(items)

            continuation = data.get("continuationToken")
            if not continuation or not items:
                break
            params["continuationToken"] = continuation

        return all_items

    # -- Pipeline Definitions --

    def get_pipeline_definitions(self) -> List[Dict[str, Any]]:
        """List all build definitions in the project."""
        return self._get_paginated("/build/definitions")

    def resolve_definition_id(self, name: str) -> Optional[int]:
        """Resolve a pipeline definition name to its ID (cached)."""
        if name in self._definition_cache:
            return self._definition_cache[name]

        definitions = self.get_pipeline_definitions()
        for d in definitions:
            self._definition_cache[d["name"]] = d["id"]

        return self._definition_cache.get(name)

    # -- Builds --

    def get_builds(self, definition_id: int, min_time: str,
                   top: int = 100, status: str = "completed") -> List[dict]:
        """Fetch completed builds for a pipeline definition."""
        return self._get_paginated("/build/builds", params={
            "definitions": str(definition_id),
            "minTime": min_time,
            "statusFilter": status,
            "$top": top,
        })

    # -- Test Results --

    def get_test_runs(self, build_id: int) -> List[dict]:
        """Get test runs associated with a build."""
        build_uri = f"vstfs:///Build/Build/{build_id}"
        return self._get_paginated("/test/runs", params={"buildUri": build_uri})

    def get_test_results(self, run_id: int, top: int = 10000) -> List[dict]:
        """Get individual test results for a test run."""
        return self._get_paginated(f"/test/runs/{run_id}/results", top=top)

    # -- Build Timeline & Logs --

    def get_build_timeline(self, build_id: int) -> dict:
        """Get build timeline (stages, jobs, tasks with status)."""
        return self._get(f"/build/builds/{build_id}/timeline")

    def get_build_log(self, build_id: int, log_id: int) -> str:
        """Get raw log content for a specific log ID."""
        url = f"{self.base_url}/build/builds/{build_id}/logs/{log_id}"
        params = {"api-version": self.api_version}
        time.sleep(self.config.request_delay_seconds)
        response = self.session.get(url, params=params)
        response.raise_for_status()
        return response.text
