# Security Review Routing Policy

Dispatch a dedicated security review when changed files, PR title/body, labels, or SDK pack risk paths touch security-sensitive areas.

## Trigger signals

- auth, authorization, token, identity, credentials
- key, encryption, crypto, certificate, Key Vault
- request signing or headers carrying secrets
- logging/diagnostics involving PII, tokens, connection strings, keys
- dependency updates with security impact
- CI/release/supply-chain files for security-sensitive packages
- network/proxy/TLS/certificate handling

## Finding bar

Security findings must be concrete:

- exploitable or production-realistic risk
- clear trust boundary
- evidence from changed code
- suggested mitigation

Do not flag theoretical defense-in-depth suggestions as blockers when primary defenses are intact. Downgrade or suppress speculative risks.

## Output

Security findings use category `security` and severity:

- `blocker`: credential leak, auth bypass, exploitable injection, insecure crypto, high-confidence supply-chain break
- `major`: realistic security regression or missing validation at a trust boundary
- `minor`/`nit`: hardening suggestions with no immediate exploit path
