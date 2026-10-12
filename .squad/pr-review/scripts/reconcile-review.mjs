#!/usr/bin/env node
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const LOCAL_ROOT = join(SCRIPT_DIR, "..");
const USER_ROOT = join(process.env.HOME || "", ".copilot", "session-state", "pr-review");
const DEFAULT_ROOT = process.env.PR_REVIEW_ROOT || (existsSync(LOCAL_ROOT) ? LOCAL_ROOT : USER_ROOT);
const STOP_WORDS = new Set([
  "the", "and", "that", "this", "with", "from", "have", "has", "for", "are",
  "was", "were", "into", "will", "would", "should", "could", "there", "their",
  "when", "where", "which", "because", "about", "after", "before", "review",
  "finding", "issue", "code", "file", "line", "test", "tests"
]);

function parseArgs(argv) {
  const args = {};
  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--pr") args.pr = argv[++i];
    else if (arg === "--agent-findings") args.agentFindings = argv[++i];
    else if (arg === "--human-comments") args.humanComments = argv[++i];
    else if (arg === "--out") args.out = argv[++i];
    else if (arg === "--help" || arg === "-h") {
      console.log("Usage: reconcile-review.mjs --pr owner/repo#N --agent-findings file.md --human-comments comments.txt [--out report.json]");
      process.exit(0);
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }
  if (!args.pr || !args.agentFindings || !args.humanComments) {
    throw new Error("Missing required --pr, --agent-findings, or --human-comments");
  }
  if (!args.out) {
    args.out = join(DEFAULT_ROOT, "reconciliation", `${args.pr.replace(/[\\/#]/g, "_")}.json`);
  }
  return args;
}

function keywords(text) {
  const counts = new Map();
  for (const token of text.toLowerCase().match(/[a-z][a-z0-9_/-]{4,}/g) ?? []) {
    if (STOP_WORDS.has(token)) continue;
    counts.set(token, (counts.get(token) ?? 0) + 1);
  }
  return new Set([...counts.entries()].filter(([, count]) => count >= 2).map(([token]) => token));
}

function sampleMissingTerms(human, agent) {
  return [...human].filter((term) => !agent.has(term)).slice(0, 50);
}

async function main() {
  const args = parseArgs(process.argv);
  if (!existsSync(args.agentFindings)) throw new Error(`agent findings not found: ${args.agentFindings}`);
  if (!existsSync(args.humanComments)) throw new Error(`human comments not found: ${args.humanComments}`);

  const agentText = await readFile(args.agentFindings, "utf8");
  const humanText = await readFile(args.humanComments, "utf8");
  const agent = keywords(agentText);
  const human = keywords(humanText);
  const overlap = [...human].filter((term) => agent.has(term));
  const missed = sampleMissingTerms(human, agent);

  const report = {
    version: 1,
    pr: args.pr,
    generated_at: new Date().toISOString(),
    summary: {
      agent_terms: agent.size,
      human_terms: human.size,
      overlap_terms: overlap.length,
      missed_terms: missed.length
    },
    missed_human_issues: missed,
    possible_false_positives: [...agent].filter((term) => !human.has(term)).slice(0, 50),
    notes: "Keyword reconciliation is a triage aid, not ground truth. Promote only manually verified misses into memory or SDK packs."
  };

  await mkdir(dirname(args.out), { recursive: true });
  await writeFile(args.out, JSON.stringify(report, null, 2), "utf8");
  console.log(args.out);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
