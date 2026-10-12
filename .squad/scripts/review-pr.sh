#!/usr/bin/env bash
set -euo pipefail

# Cosmos DB PR Review Squad - Safe PR Review Runner
# Enforces mandatory PR Review Router entry point, tier selection, and read-only safety.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PR_REVIEW_DIR="${REPO_ROOT}/.squad/pr-review"

# Resolve gh binary (prefer homebrew gh if present)
GH_BIN="gh"
if [[ -x "/opt/homebrew/bin/gh" ]]; then
  GH_BIN="/opt/homebrew/bin/gh"
fi

usage() {
  cat <<EOF
Usage: $(basename "$0") <PR-REF-OR-NUMBER> [OPTIONS]

Arguments:
  <PR-REF-OR-NUMBER>      PR URL (https://github.com/owner/repo/pull/N), owner/repo#N, or N (defaults to Azure/azure-cosmos-dotnet-v3)

Options:
  --tier <1|2|3|auto>     Review tier (default: auto)
  --output <console|file|post> Output destination (default: file)
  --peers <csv>           Comma-separated peer SDKs to compare (e.g. java,python)
  --dry-run               Pre-flight check only: fetch PR metadata, calculate tier score, display roster without reviewing
  --explain               Print tier-score rationale and context details
  --no-memory             Skip per-SDK memory injection
  --no-design-docs        Skip Cosmos design-doc grounding
  -h, --help              Show this help message

Safety Invariant:
  All reviews are strictly READ-ONLY by default.
  Posting to GitHub (--output post) requires explicit human confirmation.
EOF
}

if [[ $# -eq 0 ]]; then
  usage
  exit 1
fi

PR_INPUT=""
TIER="auto"
OUTPUT="file"
PEERS=""
DRY_RUN=false
EXPLAIN=false
NO_MEMORY=false
NO_DESIGN_DOCS=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --tier)
      TIER="$2"
      shift 2
      ;;
    --output)
      OUTPUT="$2"
      shift 2
      ;;
    --peers)
      PEERS="$2"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --explain)
      EXPLAIN=true
      shift
      ;;
    --no-memory)
      NO_MEMORY=true
      shift
      ;;
    --no-design-docs)
      NO_DESIGN_DOCS=true
      shift
      ;;
    -*)
      echo "Error: Unknown option $1" >&2
      usage
      exit 1
      ;;
    *)
      if [[ -z "${PR_INPUT}" ]]; then
        PR_INPUT="$1"
        shift
      else
        echo "Error: Unexpected argument $1" >&2
        usage
        exit 1
      fi
      ;;
  esac
done

if [[ -z "${PR_INPUT}" ]]; then
  echo "Error: Missing PR reference or number." >&2
  usage
  exit 1
fi

# Normalize PR ref into OWNER_REPO and PR_NUMBER
OWNER_REPO="Azure/azure-cosmos-dotnet-v3"
PR_NUMBER=""

if [[ "${PR_INPUT}" =~ ^https://github\.com/([^/]+/[^/]+)/pull/([0-9]+) ]]; then
  OWNER_REPO="${BASH_REMATCH[1]}"
  PR_NUMBER="${BASH_REMATCH[2]}"
elif [[ "${PR_INPUT}" =~ ^([^/]+/[^#]+)#([0-9]+)$ ]]; then
  OWNER_REPO="${BASH_REMATCH[1]}"
  PR_NUMBER="${BASH_REMATCH[2]}"
elif [[ "${PR_INPUT}" =~ ^[0-9]+$ ]]; then
  PR_NUMBER="${PR_INPUT}"
else
  echo "Error: Unable to parse PR input '${PR_INPUT}'. Expected URL, owner/repo#N, or N." >&2
  exit 1
fi

echo "=========================================================="
echo " Cosmos DB PR Review Squad - Safe Review Runner"
echo " Target: ${OWNER_REPO}#${PR_NUMBER}"
echo " Mode:   Tier=${TIER}, Output=${OUTPUT}, DryRun=${DRY_RUN}"
echo "=========================================================="

# Check gh CLI
if ! command -v "${GH_BIN}" &>/dev/null; then
  echo "Error: GitHub CLI (${GH_BIN}) not found in PATH." >&2
  exit 1
fi

echo "[1/4] Fetching PR metadata from GitHub..."
PR_JSON=$("${GH_BIN}" pr view "${PR_NUMBER}" --repo "${OWNER_REPO}" --json number,title,author,baseRefName,headRefName,additions,deletions,changedFiles,mergeable,reviewDecision,files)

TITLE=$(echo "${PR_JSON}" | jq -r '.title')
AUTHOR=$(echo "${PR_JSON}" | jq -r '.author.login')
CHANGED_FILES=$(echo "${PR_JSON}" | jq -r '.changedFiles')
ADDITIONS=$(echo "${PR_JSON}" | jq -r '.additions')
DELETIONS=$(echo "${PR_JSON}" | jq -r '.deletions')

echo "PR #${PR_NUMBER}: ${TITLE}"
echo "Author: @${AUTHOR} | Changed Files: ${CHANGED_FILES} (+${ADDITIONS}/-${DELETIONS})"

echo "[2/4] Scoring review tier and inspecting files..."
# Check for risk paths
HAS_PUBLIC_API=false
HAS_AUTH=false
HAS_RELEASE=false

FILE_NAMES=$(echo "${PR_JSON}" | jq -r '.files[].path')
while IFS= read -r f; do
  if [[ "${f}" =~ Microsoft\.Azure\.Cosmos/src/.*\.cs$ ]] && ! [[ "${f}" =~ Internal/|/tests/ ]]; then
    HAS_PUBLIC_API=true
  fi
  if [[ "${f}" =~ Authorization/|Token|KeyVault|Encryption|Crypto ]]; then
    HAS_AUTH=true
  fi
  if [[ "${f}" =~ \.props$|\.csproj$|azure-pipelines.*\.yml$ ]]; then
    HAS_RELEASE=true
  fi
done <<< "${FILE_NAMES}"

CALC_TIER=2
SCORE=0
if [[ "${HAS_PUBLIC_API}" == "true" ]]; then
  SCORE=$((SCORE + 4))
fi
if [[ "${HAS_AUTH}" == "true" ]]; then
  SCORE=$((SCORE + 3))
fi
if [[ "${HAS_RELEASE}" == "true" ]]; then
  SCORE=$((SCORE + 3))
fi
if [[ "${ADDITIONS}" -ge 1500 ]]; then
  SCORE=$((SCORE + 3))
fi

if [[ "${SCORE}" -ge 6 ]]; then
  CALC_TIER=3
elif [[ "${CHANGED_FILES}" -le 2 && "${ADDITIONS}" -le 50 && "${HAS_PUBLIC_API}" == "false" && "${HAS_AUTH}" == "false" ]]; then
  CALC_TIER=1
fi

EFFECTIVE_TIER="${TIER}"
if [[ "${TIER}" == "auto" ]]; then
  EFFECTIVE_TIER="${CALC_TIER}"
fi

echo "Calculated Risk Score: ${SCORE} -> Recommended Tier ${CALC_TIER} (Effective: Tier ${EFFECTIVE_TIER})"

echo "[3/4] Validating Squad Specialist Roster for Tier ${EFFECTIVE_TIER}..."
case "${EFFECTIVE_TIER}" in
  1)
    echo "  - Orchestrator: pr-review-router"
    echo "  - Primary Reviewer: fresh-eyes-reviewer (Tier 1 Triage)"
    ;;
  2)
    echo "  - Orchestrator: pr-review-router"
    echo "  - Correctness & Architecture: pr-deep-reviewer"
    echo "  - Engineering Quality (Dim 1, 2, 3, 5): engineering-quality-reviewer"
    echo "  - Context & Precedents: review-context-builder, past-review"
    echo "  - Parity & Skeptic Overlay: cross-sdk-reviewer, skeptic-lens"
    ;;
  3)
    echo "  - Orchestrator: pr-review-router"
    echo "  - Correctness & Architecture: pr-deep-reviewer"
    echo "  - Engineering Quality (Dim 1, 2, 3, 5): engineering-quality-reviewer"
    echo "  - Cosmos DB Domain Alignment (Dim 4): cosmos-pr-reviewer (Canonical design-docs citations)"
    echo "  - Test Coverage Model: pr-test-model-reviewer"
    echo "  - Parity & Skeptic Overlay: cross-sdk-reviewer, skeptic-lens"
    ;;
esac

if [[ "${DRY_RUN}" == "true" ]]; then
  echo "[4/4] Dry run complete. No review executed."
  echo "To run the actual review, execute:"
  echo "  @PR Review Router ${OWNER_REPO}#${PR_NUMBER} --tier ${EFFECTIVE_TIER} --output ${OUTPUT}"
  exit 0
fi

if [[ "${OUTPUT}" == "post" ]]; then
  echo ""
  echo "⚠️ WARNING: You selected --output post."
  echo "Posting review comments directly to GitHub requires explicit human confirmation."
  read -r -p "Are you sure you want to authorize posting review comments to GitHub PR #${PR_NUMBER}? [y/N]: " CONFIRM
  if [[ "${CONFIRM}" != "y" && "${CONFIRM}" != "Y" ]]; then
    echo "Operation aborted by user. Switching output mode to 'file'."
    OUTPUT="file"
  fi
fi

OUTPUT_FILE="${REPO_ROOT}/.squad/pr-review/findings/${OWNER_REPO//\//_}_pr${PR_NUMBER}.md"
mkdir -p "$(dirname "${OUTPUT_FILE}")"

echo "[4/4] Ready to execute review."
echo "Review findings will be saved to: ${OUTPUT_FILE}"
echo "To invoke via Copilot CLI or IDE agent:"
echo "  @PR Review Router ${OWNER_REPO}#${PR_NUMBER} --tier ${EFFECTIVE_TIER} --output ${OUTPUT}"
EOF
