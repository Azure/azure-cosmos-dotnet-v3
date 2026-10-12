#!/usr/bin/env node
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";

const SEVERITIES = new Set(["blocker", "major", "minor", "nit"]);
const CATEGORIES = new Set([
  "correctness", "code_quality", "testing", "perf", "security", "api",
  "parity", "design_alignment", "agentic", "style", "other"
]);
const ABSENCE_PATTERN = /\b(missing|no test|no benchmark|no changelog|no doc|diverges from|breaks convention|should have a test|should benchmark|add a test|add a benchmark)\b/i;

function usage() {
  console.log("Usage: validate-structured-findings.mjs <review-output.md> [--strict]");
}

function parseArgs(argv) {
  const args = { strict: false, files: [] };
  for (let i = 2; i < argv.length; i++) {
    if (argv[i] === "--strict") args.strict = true;
    else if (argv[i] === "--help" || argv[i] === "-h") {
      usage();
      process.exit(0);
    } else {
      args.files.push(argv[i]);
    }
  }
  if (args.files.length === 0) throw new Error("Missing review output file");
  return args;
}

function extractBlocks(text) {
  const blocks = [];
  const regex = /```structured-findings\s*\n([\s\S]*?)```/g;
  let match;
  while ((match = regex.exec(text)) !== null) {
    blocks.push(match[1].trim());
  }
  return blocks;
}

function unquote(value) {
  const trimmed = value.trim();
  if ((trimmed.startsWith('"') && trimmed.endsWith('"')) || (trimmed.startsWith("'") && trimmed.endsWith("'"))) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
}

function parseScalar(value) {
  const trimmed = value.trim();
  if (trimmed === "[]") return [];
  if (/^\d+$/.test(trimmed)) return Number(trimmed);
  if (trimmed.startsWith("[") && trimmed.endsWith("]")) {
    return trimmed.slice(1, -1).split(",").map((v) => unquote(v.trim())).filter(Boolean);
  }
  return unquote(trimmed);
}

function parseSimpleYamlList(block) {
  const trimmed = block.trim();
  if (trimmed === "[]" || trimmed === "structured-findings: []") return [];
  if (trimmed.startsWith("[")) return JSON.parse(trimmed);

  const lines = block.split(/\r?\n/);
  const findings = [];
  let current = null;
  let multilineKey = null;
  let multilineIndent = 0;

  for (const rawLine of lines) {
    if (!rawLine.trim() || rawLine.trim() === "structured-findings:") continue;

    const listMatch = /^-\s+([^:]+):\s*(.*)$/.exec(rawLine);
    if (listMatch) {
      current = {};
      findings.push(current);
      multilineKey = null;
      current[listMatch[1].trim()] = parseScalar(listMatch[2]);
      continue;
    }

    if (!current) continue;

    if (multilineKey) {
      const indent = rawLine.match(/^ */)?.[0].length ?? 0;
      if (indent > multilineIndent || rawLine.trim() === "") {
        current[multilineKey] += `${current[multilineKey] ? "\n" : ""}${rawLine.slice(Math.min(indent, multilineIndent + 2))}`;
        continue;
      }
      multilineKey = null;
    }

    const kv = /^(\s*)([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$/.exec(rawLine);
    if (!kv) continue;
    const key = kv[2];
    const value = kv[3];
    if (value.trim() === "|") {
      multilineKey = key;
      multilineIndent = kv[1].length;
      current[key] = "";
    } else {
      current[key] = parseScalar(value);
    }
  }

  return findings;
}

function tagsOf(finding) {
  if (Array.isArray(finding.tags)) return finding.tags;
  if (typeof finding.tags === "string") return finding.tags.split(/[, ]+/).filter(Boolean);
  return [];
}

function validateFinding(finding, index) {
  const errors = [];
  for (const key of ["id", "severity", "category", "file", "message"]) {
    if (finding[key] === undefined || finding[key] === "") errors.push(`finding[${index}] missing required key '${key}'`);
  }
  if (finding.severity && !SEVERITIES.has(finding.severity)) errors.push(`finding[${index}] invalid severity '${finding.severity}'`);
  if (finding.category && !CATEGORIES.has(finding.category)) errors.push(`finding[${index}] invalid category '${finding.category}'`);
  if (typeof finding.message === "string" && finding.message.length > 400) errors.push(`finding[${index}] message is too long (${finding.message.length})`);

  const tags = tagsOf(finding);
  const absenceClaim = ABSENCE_PATTERN.test(finding.message ?? "");
  const verified = Boolean(finding.verified_by_grep || finding.evidence?.match(/\b(grep|rg|gh search|view|find)\b/i));
  if (absenceClaim && !verified && !tags.includes("unverified-pattern-suggestion")) {
    errors.push(`finding[${index}] absence/divergence claim lacks verified_by_grep/evidence and unverified-pattern-suggestion tag`);
  }

  if (finding.category === "design_alignment" && !finding.design_doc_citation && !tags.includes("propose-design-doc-extension")) {
    errors.push(`finding[${index}] design_alignment finding lacks design_doc_citation`);
  }

  return errors;
}

async function main() {
  const args = parseArgs(process.argv);
  const report = { files: [], ok: true };

  for (const file of args.files) {
    if (!existsSync(file)) throw new Error(`File not found: ${file}`);
    const text = await readFile(file, "utf8");
    const blocks = extractBlocks(text);
    const fileReport = { file, blocks: blocks.length, findings: 0, errors: [] };

    if (blocks.length === 0) {
      fileReport.errors.push("missing structured-findings block");
    } else {
      for (const block of blocks) {
        let findings;
        try {
          findings = parseSimpleYamlList(block);
        } catch (error) {
          fileReport.errors.push(`parse error: ${error.message}`);
          continue;
        }
        if (!Array.isArray(findings)) {
          fileReport.errors.push("structured block did not parse to an array");
          continue;
        }
        fileReport.findings += findings.length;
        findings.forEach((finding, index) => {
          fileReport.errors.push(...validateFinding(finding, index));
        });
      }
    }

    if (args.strict && fileReport.errors.length > 0) report.ok = false;
    report.files.push(fileReport);
  }

  if (report.files.some((f) => f.errors.length > 0)) report.ok = false;
  console.log(JSON.stringify(report, null, 2));
  if (!report.ok) process.exit(1);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
