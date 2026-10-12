# Reviewability and Confidence Policy

Every review should distinguish "no issue found" from "not enough confidence to know."

## Reviewability score

Compute qualitatively:

- **High**: small/medium PR, clear intent/spec, tests available, relevant files reviewed, peer/design context available.
- **Medium**: some context missing or PR moderately broad, but core behavior reviewed.
- **Low**: very large PR, unclear intent, missing spec/tests/CI/peer context, or multiple agents failed.

## Large PR guidance

Recommend split/staged review when:

- changed files span multiple unrelated subsystems
- file count is high enough that line-by-line confidence is unrealistic
- generated + source + release changes are mixed
- broad refactor is combined with behavior change

## Required output

Final review should include:

- reviewability score
- per-dimension confidence where useful
- major context gaps
- whether a split/follow-up PR is recommended

Low confidence is not automatically a blocker, but it should affect verdict for risky PRs.
