# Comment Quality Gate

Before a finding is surfaced, it must pass this gate.

## Required fields

- Concrete claim
- File/line or process-level location
- Why it matters
- Evidence
- Suggested fix or decision requested
- Severity that matches impact
- Required vs optional status

## Tone requirements

- Comment on code and consequences, not the author.
- Use questions when context may be missing.
- Use direct statements for proven defects.
- Avoid sarcasm, blame, or "obvious" language.
- Prefer "Consider..." for optional improvements and "This can..." for concrete risks.

## Suppression/downgrade rules

- Style-only preference without style-guide/local-convention evidence -> suppress or `nit`.
- Finding already covered by existing PR comment -> suppress unless adding stronger evidence.
- Absence claim without verification evidence -> `nit` + `unverified-pattern-suggestion`.
- Comment without consequence -> suppress.
- Multiple comments with same root cause -> merge.

## Final review shape

The final review should be sparse and high-signal. If all findings are optional, the verdict should not block.
