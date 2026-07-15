# DTX FastResponseMode SDK API Specification

## Deliverable and Scope

This phase produces this single Markdown specification only. It does not modify SDK code, service code, tests, samples, or the changelog.

The specification defines account-level FastResponseMode for distributed write transactions without replacing the existing API. `CommitTransactionAsync` may return after durable Phase 1, while customers use the returned idempotency token to query the transaction's exact coordinator state.

Read transactions, per-request FastResponse configuration, and implementation are out of scope.

## Public API

Keep the existing commit API:

```csharp
Task<DistributedTransactionResponse> CommitTransactionAsync(
    CancellationToken cancellationToken = default);
```

Add status lookup on `CosmosClient`:

```csharp
Task<DistributedTransactionResponse> GetTransactionStatusAsync(
    Guid idempotencyToken,
    CancellationToken cancellationToken = default);
```

Add the public status contract:

```csharp
public enum DistributedTransactionStatus
{
    Unknown = 0,
    Preparing,
    Committing,
    Aborting,
    Aborted,
    Committed,
}
```

Extend `DistributedTransactionResponse` with:

```csharp
public DistributedTransactionStatus TransactionStatus { get; }
public bool IsTerminal { get; } // true only for Committed and Aborted
```

Expose the current token on the transaction before and during commit:

```csharp
public Guid IdempotencyToken { get; }
```

The transaction creates its initial token before the first wire request. If the SDK starts a new logical attempt after a retriable terminal abort, this property is updated before that attempt is sent. This lets callers recover the latest observable token after cancellation or an exception.

`IsSuccessStatusCode` remains an HTTP-level property. It must not be documented as proof that the transaction committed: FastResponse acceptance is HTTP 202 and status lookup is HTTP 200 for every known state. `TransactionStatus` is authoritative.

Do not add `ScheduleTransactionAsync`; account configuration changes the completion point of the existing `CommitTransactionAsync`.

## Wire Contract

Every DTX response payload, including legacy terminal responses, FastResponse acceptance, and status lookup, carries:

```json
{
  "transactionStatus": "Committing",
  "idempotencyToken": "00000000-0000-0000-0000-000000000000",
  "isRetriable": false,
  "operationResponses": []
}
```

- The payload token is canonical; the existing response header remains for compatibility.
- A payload/header/request-token mismatch fails closed and is never used to start a new-token retry.
- During rolling deployment, the SDK may infer only unambiguous legacy terminal states (`200` => `Committed`, `452` => `Aborted`). FastResponse acceptance requires an explicit status.
- Unknown future status strings map to `Unknown` and are not retried as terminal aborts.
- `GetTransactionStatusAsync(Guid.Empty)` throws `ArgumentException`.
- Status lookup uses the same account-scoped DTX endpoint, a dedicated status operation type, no transaction body, and the token header.
- A known token returns HTTP 200 with its coordinator state. An unknown or expired token returns HTTP 404 with `TransactionStatus.Unknown`.
- The service must document the account-scoped token retention window; status tracking is guaranteed only within that window.

## Status Semantics

| Status | Terminal | Commit response | Status lookup | Operation results | ETag/session token |
|---|---:|---|---|---|---|
| `Preparing` | No | Not a FastResponse completion point; legacy processing may continue internally | 200 | None | None |
| `Committing` | No | 202 after durable Phase 1 in FastResponseMode | 200 | None | None |
| `Aborting` | No | Continue internal resolution; do not acknowledge as complete | 200 | None | None |
| `Aborted` | Yes | 452; retry only when the service explicitly sets `isRetriable=true` | 200 | Commit may include abort results; status lookup is always status-only | None |
| `Committed` | Yes | 200 in legacy mode | 200 | Commit includes terminal results; status lookup is always status-only | Present only on the terminal commit response |
| `Unknown` | No | Invalid/unrecognized response | 404 for unknown/expired token, or an error response | None | None |

FastResponse intentionally does not supply final ETags or session tokens:

- The initial 202 response has `Count == 0`.
- Status lookup always has `Count == 0`, including `Committed`.
- `DistributedTransactionOperationResult` remains the terminal commit-result shape; no placeholder or stale ETag/session token is created.
- Session-container merging runs only when a terminal commit response contains real operation results.
- FastResponse polling confirms outcome but does not establish SDK session read-your-write state. This limitation must be explicit in API documentation.

## Retry Model

Treat retries as two distinct classes under the existing retry count and cumulative-delay budgets.

| Condition | Meaning | Token behavior | SDK action |
|---|---|---|---|
| Empty-body transport/coordinator retry, 408, 449/5352, or supported DTX infrastructure failure | Outcome is uncertain or transaction is still progressing | Reuse token | Replay the identical serialized request |
| Body-bearing nonterminal response marked retriable | Same transaction is unresolved | Reuse token | Replay/poll the same attempt |
| `Aborted` + `isRetriable=true`, including 452/5421 | Previous transaction is terminal and fully rolled back | Generate a new token | Submit identical operations as a new logical attempt |
| `Aborted` + `isRetriable=false` | Terminal customer-visible abort | Keep token | Return response |
| `Committing` accepted response | FastResponse successfully scheduled irreversible Phase 2 | Keep token | Return immediately; do not retry |

New-token retry applies only to distributed write transactions. It depends on the service guarantee that a retriable `Aborted` state has no committed writes and is safe to resubmit.

The response and transaction object expose the latest attempt's token. Older tokens remain queryable and report `Aborted`. If retry budgets are exhausted, return the last response unchanged; `IsRetriable` describes the coordinator classification, not permission to call commit twice on the same transaction object.

The existing single-use commit guard remains. All same-token and new-token retries occur inside the one `CommitTransactionAsync` invocation.

## Diagnostics

- Keep one parent trace and aggregate every wire attempt.
- Add per-attempt fields for transaction status, status/substatus, retry class (`same-token-replay` or `new-token-resubmit`), logical-attempt number, and wire-attempt number.
- Record token transitions so an exception's diagnostics and the transaction object's current token identify the latest attempt.
- Preserve prior-attempt diagnostics when the final response is 202, 200, 452, or a retry-budget result.
- Status lookup receives its own normal `CosmosDiagnostics`; it does not append to the original commit response.

## Future Implementation Impact (Informational)

- `DistributedTransaction.cs` and write core would expose and safely update the current token while preserving single-use behavior.
- `DistributedTransactionServerRequest.cs`: accept an explicit token and create a new request identity without rematerializing or changing serialized operations.
- `DistributedTransactionCommitter.cs`: separate same-token replay from terminal-abort new-token resubmission; return 202 without retrying; merge session tokens only for real terminal operation results.
- `DistributedTransactionResponse.cs`: parse status and payload token, add `IsTerminal`, permit valid zero-result responses, and provide a status-only parsing path.
- `CosmosClient.cs` plus client core/context plumbing: add `GetTransactionStatusAsync`.
- Serializer/constants and operation-type mappings: add status/token payload fields and the status operation.
- OpenTelemetry: report the final HTTP status and transaction status without treating 202 as committed.
- Public XML docs and samples: explain account-level behavior, 202 semantics, token recovery, polling, retention, and the FastResponse ETag/session limitation.

These are impact notes for estimating and reviewing a future implementation, not tasks authorized by this specification phase.

## Required Conformance Scenarios

Any future SDK/service implementation must prove:

1. Response parsing: all enum values, unknown value, legacy terminal inference, token mismatch, missing FastResponse status, zero-result 202/lookup responses, and `IsTerminal`.
2. Legacy commit: 200 `Committed` keeps ordered operation results, ETags, session-token merge, and existing API compatibility.
3. FastResponse commit: 202 `Committing`, current token exposed on transaction and response, zero results, no ETag/session merge, and no retry.
4. Status lookup: every known state, 404 unknown/expired token, cancellation, diagnostics, zero results for terminal states, and `Guid.Empty` validation.
5. Same-token retries: identical body/token across uncertain and nonterminal retries; diagnostics aggregate all attempts.
6. New-token retries: retriable 452/5421 generates a distinct token before resubmission, keeps the body identical, exposes the latest token, and leaves the old token queryable as aborted.
7. Non-retriable abort and exhausted budgets: no extra attempt; final status, token, `IsRetriable`, and diagnostics are preserved.
8. Exception/cancellation after send: the transaction object's token identifies the latest sent attempt.
9. E2E: account-level legacy versus FastResponse behavior, 202-to-committed polling, 202-to-aborted polling, retriable abort followed by acceptance/commit, and token-retention expiry.

## Service Dependencies

- Durable Phase-1 completion must precede the 202 `Committing` response.
- Phase 2 must continue independently after the client receives 202 or disconnects.
- Every state transition must be queryable by token for the documented retention window.
- Terminal states must be immutable and replayable.
- `isRetriable=true` on `Aborted` must guarantee complete rollback and safe new-token resubmission.
- The response payload must include status and token consistently across commit and status operations.
