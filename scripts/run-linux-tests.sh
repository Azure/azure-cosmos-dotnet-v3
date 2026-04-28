#!/usr/bin/env bash
# Builds the test image and runs Microsoft.Azure.Cosmos.EmulatorTests on
# Linux inside a container against a live Cosmos DB endpoint.
#
# Required env vars:
#   COSMOS_ENDPOINT   Cosmos DB account endpoint (https://...)
#   COSMOS_KEY        Account key
#
# Optional env vars:
#   TEST_FILTER       dotnet test --filter argument (default targets
#                     the RntbdIdleTimerStarvationTests class)
#   IMAGE_TAG         docker image tag (default: cosmos-dispatcher-test:local)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS_DIR="${REPO_ROOT}/TestResults/linux"
IMAGE_TAG="${IMAGE_TAG:-cosmos-dispatcher-test:local}"
# Placeholder filter — will match the class created for the real repro
# test in Stage 3, and already matches the Stage 2 smoke test.
TEST_FILTER="${TEST_FILTER:-FullyQualifiedName~RntbdIdleTimerStarvationTests}"

if [[ -z "${COSMOS_ENDPOINT:-}" || -z "${COSMOS_KEY:-}" ]]; then
    echo "ERROR: COSMOS_ENDPOINT and COSMOS_KEY must be set." >&2
    exit 2
fi

mkdir -p "${RESULTS_DIR}"

echo "==> Building image ${IMAGE_TAG}"
docker build \
    --file "${REPO_ROOT}/scripts/Dockerfile.test" \
    --tag "${IMAGE_TAG}" \
    "${REPO_ROOT}"

# NOTE: On msdata/direct the full Microsoft.Azure.Cosmos.EmulatorTests
# project transitively depends on FaultInjection, which fails to build
# locally because Microsoft.Azure.Cosmos.Client (built from src/) and
# Microsoft.Azure.Cosmos.Direct NuGet contain duplicate types. We side-
# step that by pointing the Linux pipeline at a minimal project that
# links only the RntbdIdleTimerStarvationTests.cs source file. The
# canonical .cs file still lives under Microsoft.Azure.Cosmos.EmulatorTests
# so Windows CI compiles it normally.
TEST_PROJECT="scripts/LinuxSmoke/LinuxSmoke.csproj"

echo "==> Running dotnet test in container (filter: ${TEST_FILTER})"
set +e
docker run --rm \
    --volume "${REPO_ROOT}:/repo" \
    --volume "${RESULTS_DIR}:/results" \
    --env COSMOS_ENDPOINT \
    --env COSMOS_KEY \
    --workdir /repo \
    "${IMAGE_TAG}" \
    bash -c "dotnet test '${TEST_PROJECT}' \
        --configuration Release \
        --filter '${TEST_FILTER}' \
        --logger 'trx;LogFileName=linux-test-results.trx' \
        --results-directory /results"
EXIT_CODE=$?
set -e

echo "==> dotnet test exit code: ${EXIT_CODE}"
echo "==> TRX written to ${RESULTS_DIR}/linux-test-results.trx"
exit ${EXIT_CODE}
