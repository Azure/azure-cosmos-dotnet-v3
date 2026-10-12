#!/usr/bin/env node
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const LOCAL_ROOT = join(SCRIPT_DIR, "..");
const USER_ROOT = join(process.env.HOME || "", ".copilot", "session-state", "pr-review");
const DEFAULT_ROOT = process.env.PR_REVIEW_ROOT || (existsSync(join(LOCAL_ROOT, "evals", "corpus.json")) ? LOCAL_ROOT : USER_ROOT);

function parseArgs(argv) {
  const args = {
    corpus: join(DEFAULT_ROOT, "evals", "corpus.json"),
    findingsDir: join(DEFAULT_ROOT, "findings")
  };

  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--corpus") args.corpus = argv[++i];
    else if (arg === "--findings-dir") args.findingsDir = argv[++i];
    else if (arg === "--help" || arg === "-h") {
      console.log("Usage: score-review.mjs [--corpus path] [--findings-dir path]");
      process.exit(0);
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }

  return args;
}

function findingsFileName(pr) {
  const match = /^([^/]+)\/([^#]+)#(\d+)$/.exec(pr);
  if (!match) throw new Error(`Invalid PR ref '${pr}', expected owner/repo#N`);
  return `${match[1]}_${match[2]}_pr${match[3]}.md`;
}

function includesAll(haystack, terms) {
  return terms.every((term) => haystack.includes(term.toLowerCase()));
}

function includesAny(haystack, terms = []) {
  return terms.some((term) => haystack.includes(term.toLowerCase()));
}

function zeroMetrics() {
  return {
    cases_total: 0,
    cases_enabled: 0,
    cases_with_output: 0,
    expected_total: 0,
    expected_found: 0,
    recall: null,
    false_positive_hits: 0,
    missing: [],
    false_positives: [],
    missing_outputs: []
  };
}

async function main() {
  const args = parseArgs(process.argv);
  const corpus = JSON.parse(await readFile(args.corpus, "utf8"));
  if (!Array.isArray(corpus.cases)) throw new Error("corpus.cases must be an array");

  const metrics = zeroMetrics();
  metrics.cases_total = corpus.cases.length;

  for (const testCase of corpus.cases) {
    if (!testCase.enabled) continue;
    metrics.cases_enabled++;

    const file = join(args.findingsDir, findingsFileName(testCase.pr));
    if (!existsSync(file)) {
      metrics.missing_outputs.push({ case: testCase.id, pr: testCase.pr, file });
      continue;
    }

    metrics.cases_with_output++;
    const text = (await readFile(file, "utf8")).toLowerCase();

    for (const expected of testCase.expected_findings) {
      metrics.expected_total++;
      const found = includesAll(text, expected.required_terms);
      const contradicted = includesAny(text, expected.forbidden_terms);

      if (found && !contradicted) {
        metrics.expected_found++;
      } else {
        metrics.missing.push({
          case: testCase.id,
          pr: testCase.pr,
          expected: expected.id,
          category: expected.category,
          severity: expected.severity,
          required_terms: expected.required_terms,
          reason: contradicted ? "forbidden term present" : "required terms not all present"
        });
      }
    }

    for (const term of testCase.known_false_positive_terms ?? []) {
      if (text.includes(term.toLowerCase())) {
        metrics.false_positive_hits++;
        metrics.false_positives.push({ case: testCase.id, pr: testCase.pr, term });
      }
    }
  }

  metrics.recall = metrics.expected_total === 0
    ? null
    : Number((metrics.expected_found / metrics.expected_total).toFixed(4));

  console.log(JSON.stringify(metrics, null, 2));
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
