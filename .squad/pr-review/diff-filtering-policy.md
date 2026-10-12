# Diff Filtering and Noise Policy

Reviewers should spend deep reasoning on files that can affect behavior, users, release artifacts, or operational safety.

## Classifications

| Class | Examples | Policy |
|---|---|---|
| Human-written source | SDK source, tests, scripts | Full review |
| Public contract | APIView, docs, samples, changelog, generated public API | Full review |
| Release artifact | package manifests, CI templates, deployment/release scripts | Full review via CI Artifact Reviewer |
| Generated code | files with generated markers, swagger outputs | Scan for model/API/schema impact; full review if public surface changes |
| Noise | lock files, vendored/minified bundles, source maps | Summarize unless dependency/security/release impact exists |
| Baselines/snapshots | query baselines, diagnostics snapshots | Review semantic diff and ensure generator/source changed consistently |
| Migrations/schema | DB/schema migrations, release metadata | Never silently skip; full review |

## Context manifest

Record skipped or summarized files under `context_manifest.gaps` with severity `info` unless the skipped file could affect release/security; then use `warning`.

## Do not skip

- security/auth/encryption/identity paths
- generated files that ship to customers
- package manifests and release templates
- migrations/schema/baseline changes
- docs/changelog/APIView for public API changes
