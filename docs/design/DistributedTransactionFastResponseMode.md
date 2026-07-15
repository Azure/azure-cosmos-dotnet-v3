# Distributed Write Transactions: Fast Response Protocol and .NET SDK Contract

## 1. Purpose

This document is the normative contract for account-level Fast Response behavior for distributed write transactions.

It defines:

- the three customer-visible transaction states;
- the existing commit REST operation;
- a new, read-only transaction-status REST operation;
- the exact response body for each operation;
- same-token replay versus new-token resubmission;
- cancellation behavior at every send boundary;
- ETag, session-token, request-charge, and diagnostics behavior; and
- the minimal .NET SDK surface required to expose the protocol.

The keywords **MUST**, **MUST NOT**, **SHOULD**, and **MAY** are normative.

This document does not define distributed read transactions, a server-side transaction-cancel API, or per-request Fast Response configuration.

## 2. Terms

| Term | Meaning |
|---|---|
| Transaction object | The single-use .NET `DistributedTransaction` instance created by the customer. |
| Logical attempt | One coordinator transaction identified by one idempotency token. |
| Wire attempt | One HTTP request. Multiple wire attempts may replay one logical attempt. |
| Same-token replay | Sending the same serialized operation payload with the same idempotency token because the logical attempt's outcome is unresolved. |
| New-token resubmission | Sending the same serialized operation payload with a new idempotency token because the prior logical attempt is terminally `Aborted` and explicitly safe to resubmit. |
| Durable Phase 1 | The coordinator has durably recorded the transaction and all participants have completed the prepare decision required for Phase 2 to continue without the client. |
| Fast Response | The commit call may return after durable Phase 1 instead of waiting for terminal Phase 2. |
| Standard response | The commit call waits for a terminal `Committed` or `Aborted` result, subject to retry-budget exhaustion or cancellation. |
| Known transaction response | A response whose body contains a valid idempotency token, response mode, and one of the three public transaction states. |
| Envelope failure | An HTTP or transport failure that does not contain a trustworthy transaction body. |

## 3. Non-Negotiable Invariants

An implementation is incorrect if any invariant below is violated.

1. Public transaction status has exactly three values: `InProgress`, `Aborted`, and `Committed`.
2. `Committed` and `Aborted` are terminal and immutable.
3. Coordinator-internal `Preparing`, `Committing`, and `Aborting` are all reported publicly as `InProgress`.
4. An HTTP success code does not prove commitment. `TransactionStatus` is authoritative.
5. Fast Response is account-level. The request body does not contain a response-mode override.
6. A Fast Response `202` is sent only after durable Phase 1.
7. After durable Phase 1, coordinator execution continues even if the client disconnects or cancels.
8. A customer cancellation only stops local SDK work. It never sends `AbortDistributedTransaction` and never changes server state.
9. One idempotency token identifies exactly one logical attempt and exactly one serialized operation payload.
10. Same-token replay uses byte-for-byte identical operation payload bytes.
11. New-token resubmission is permitted only after a known terminal `Aborted` response with `isRetriable: true`.
12. Possession of an idempotency token is not authorization to read transaction status.
13. Customer status lookup is read-only. It never invokes coordinator recovery, writes the ledger, dispatches participant work, or sets `RetriggerDTX`.
14. Fast Response and status lookup never synthesize operation results, ETags, or session tokens.
15. A response token mismatch fails closed. The SDK never retries using an untrusted response token.

## 4. Account Configuration

The account configuration has one effective value:

```text
DistributedTransactionResponseMode = Standard | FastResponse
```

Rules:

- The default is `Standard`.
- The service reads the account value when it creates the ledger record for a new idempotency token.
- The service stores the selected mode in that ledger record.
- Every replay and status lookup for that token returns the stored mode, even if account configuration later changes.
- A new-token resubmission is a new logical attempt and snapshots the account value again.
- Failure to read account configuration before creating a logical attempt returns `500/5412 DtcAccountConfigFailure`. No transaction record is assumed to exist.

## 5. Public State Model

```text
                          durable Phase 1
          new request  ---------------------->  InProgress
                                                   |
                                  +----------------+----------------+
                                  |                                 |
                                  v                                 v
                              Committed                          Aborted
                              terminal                           terminal
```

The service MUST NOT expose any other public state.

| Public state | Internal states represented | Terminal | `isRetriable` |
|---|---|---:|---|
| `InProgress` | `Preparing`, `Committing`, `Aborting` | No | `false` on Fast Response acceptance and status lookup; `true` only on a commit response that instructs same-token replay |
| `Aborted` | `Aborted` | Yes | `true` only when complete rollback is proven and identical operations are safe to submit under a new token |
| `Committed` | `Committed` | Yes | Always `false` |

Terminal-state rules:

- A token that has returned `Committed` MUST always return `Committed`.
- A token that has returned `Aborted` MUST always return `Aborted`.
- A terminal response MUST retain the terminal substatus and `isRetriable` classification for the token's retention lifetime.
- `InProgress` MAY later become `Committed` or `Aborted`.
- `Committed` MUST NOT become `Aborted`.
- `Aborted` MUST NOT become `Committed`.

## 6. Idempotency Token Contract

### 6.1 Format and ownership

- The token is a non-empty UUID serialized in standard `D` format.
- The SDK creates the initial token before the first commit wire request.
- The request sends it in `x-ms-cosmos-idempotency-token`.
- Every known transaction response sends the same token in:
  - `x-ms-cosmos-idempotency-token`; and
  - the JSON `idempotencyToken` property.
- The JSON value is canonical.
- UUID comparison is by parsed 128-bit value, not string casing.

### 6.2 Payload binding

The service binds the first accepted request payload to the token.

For a later request carrying the same token:

- identical canonical operation payload continues or replays that logical attempt;
- different operations, order, resource identity, conditional headers, partition key, or body returns `400/5422 DtcRetryOperationsMismatch`;
- `400/5422` is not retriable.

The SDK MUST serialize the operation payload once per `CommitTransactionAsync` invocation and reuse those exact bytes for every same-token replay and new-token resubmission.

### 6.3 Token publication on the transaction object

`DistributedTransaction.IdempotencyToken` exposes the token of the latest logical attempt that reached the send boundary.

The SDK MUST use this sequence for a new-token resubmission:

```text
1. Receive terminal Aborted + isRetriable=true for currentToken.
2. Verify retry budgets.
3. Wait the selected retry delay.
4. Check cancellation.
5. Create candidateToken.
6. Enter the request-dispatch path.
7. Publish candidateToken to DistributedTransaction.IdempotencyToken.
8. Send the request with candidateToken and the original serialized bytes.
```

Steps 7 and 8 are one dispatch handoff. Cancellation before step 6 leaves the old token published. An exception or cancellation after the handoff leaves the candidate token published so the customer can query it. A published candidate that did not reach the service may legitimately return `404`.

## 7. Commit REST Contract

### 7.1 Request

```http
POST /operations/dtc HTTP/1.1
Authorization: <standard Cosmos DB authorization>
x-ms-date: <RFC 7231 date>
x-ms-version: <supported service version>
Content-Type: application/json
x-ms-cosmos-idempotency-token: <non-empty UUID>
x-ms-cosmos-operation-type: CommitDistributedTransaction
x-ms-cosmos-resource-type: DistributedTransactionBatch
```

Authorization metadata:

| Field | Value |
|---|---|
| HTTP verb | `POST` |
| Authorization resource type | `distributedtransactionbatch` |
| Authorization resource link | empty string |
| Request path | exactly `/operations/dtc`, ignoring only path casing and leading/trailing slash normalization |

The customer MUST NOT send `RetriggerDTX`.

### 7.2 Request body

The top-level JSON object has one required property:

```json
{
  "operations": [
    {
      "databaseName": "db",
      "collectionName": "container",
      "id": "item-1",
      "collectionResourceId": "rid",
      "databaseResourceId": "rid",
      "partitionKey": ["pk"],
      "index": 0,
      "resourceBody": {
        "id": "item-1",
        "pk": "pk"
      },
      "sessionToken": "0:-1#1",
      "ifMatch": "\"etag\"",
      "ifNoneMatch": "*",
      "operationType": "Create",
      "resourceType": "Document"
    }
  ]
}
```

Per-operation rules:

| Property | Required | Rule |
|---|---:|---|
| `databaseName` | Yes | Non-empty database name. |
| `collectionName` | Yes | Non-empty container name. |
| `id` | Operation-dependent | Required when the document operation requires an item id. |
| `collectionResourceId` | Yes on the wire after SDK resolution | Resolved container RID. |
| `databaseResourceId` | Yes on the wire after SDK resolution | Resolved database RID. |
| `partitionKey` | Yes | Canonical JSON partition-key value. |
| `index` | Yes | Unique unsigned integer. All indices MUST form the complete range `0..N-1`. |
| `resourceBody` | Operation-dependent | Required for create, replace, upsert, and patch forms that require a body. |
| `sessionToken` | No | Existing per-container session token captured before commit. |
| `ifMatch` | No | Existing conditional-write value. |
| `ifNoneMatch` | No | Existing conditional-write value. |
| `operationType` | Yes | Supported distributed write operation name. |
| `resourceType` | Yes | `Document`. |

The request MUST contain at least one operation.

### 7.3 Service processing

The service follows this order:

```text
1. Validate method and exact path.
2. Authenticate and authorize the caller.
3. Reject customer use of RetriggerDTX.
4. Validate required DTX headers and parse the non-empty UUID.
5. Parse and validate the operation body.
6. Load the ledger record for the token.
7. If a record exists:
   a. verify payload identity;
   b. return or continue the stored logical attempt;
   c. use the response mode stored in that record.
8. If no record exists:
   a. read account response-mode configuration;
   b. create the ledger record with token, payload identity, participants, and response mode;
   c. execute Phase 1.
9. If Phase 1 cannot establish a durable decision, return the applicable failure.
10. After durable Phase 1:
    a. Standard: continue waiting for terminal Phase 2;
    b. FastResponse: durably schedule Phase 2 and return 202/InProgress.
11. Continue Phase 2 independently of the client connection.
```

### 7.4 Common commit response body

Every known commit response has this shape:

```json
{
  "transactionStatus": "InProgress",
  "responseMode": "FastResponse",
  "idempotencyToken": "8a9d6f61-f0fb-4ee5-84de-7a7de7fbcf4a",
  "statusCode": 202,
  "subStatusCode": 0,
  "isRetriable": false,
  "diagnosticString": "Transaction is in progress.",
  "requestCharge": 12.5,
  "operationResponses": []
}
```

Required-property rules:

| Property | Type | Rule |
|---|---|---|
| `transactionStatus` | string | Exactly `InProgress`, `Aborted`, or `Committed`. Case-sensitive. |
| `responseMode` | string | Exactly `Standard` or `FastResponse`. Case-sensitive. |
| `idempotencyToken` | string | Valid UUID equal to request and response-header token. |
| `statusCode` | integer | Equal to the commit response HTTP status. |
| `subStatusCode` | integer | `0` when no substatus applies. |
| `isRetriable` | boolean | Interpreted with `transactionStatus`; never interpreted alone. |
| `diagnosticString` | string | Human-readable, bounded diagnostic text. Not parsed for control flow. |
| `requestCharge` | number | Charge accumulated by the service for this wire response. |
| `operationResponses` | array | Exact shape is state-dependent below. |

The same idempotency token MUST also be returned in `x-ms-cosmos-idempotency-token`.

### 7.5 Commit response matrix

| HTTP/substatus | Body state | Mode | Terminal | Operation results | SDK classification |
|---|---|---|---:|---:|---|
| `200/0` | `Committed` | Stored mode | Yes | Exactly `N` | Return terminal success. |
| `202/0` | `InProgress` | `FastResponse` | No | Zero | Return immediately. Do not retry. |
| `408/0` | `InProgress` | Stored mode | No | Zero | Same-token replay within budget. |
| `449/5352` | `InProgress` when body is present | Stored mode | No | Zero | Same-token replay within budget. |
| `452/0` | `Aborted` | Stored mode | Yes | Exactly `N` | Return terminal abort. |
| `452/5421` | `Aborted` | Stored mode | Yes | Exactly `N` | New-token resubmission within budget. |
| `400/5422` | No trusted transaction state required | N/A | N/A | Not used | Return protocol error. Never retry. |
| `429/3200` | Normally no trusted body | N/A | Unknown | Not used | Same-token replay using `Retry-After` and throttle budget. |
| `500/5411` | Normally no trusted body | N/A | Unknown | Not used | Same-token infrastructure retry. |
| `500/5412` | Normally no trusted body | N/A | Unknown | Not used | Same-token infrastructure retry. |
| `500/5413` | Normally no trusted body | N/A | Unknown | Not used | Same-token infrastructure retry. |
| `500/5423` | No trusted state | N/A | Unknown | Not used | Return fail-closed server-contract error. |
| `500/5424` | No trusted state | N/A | Unknown | Not used | Return fail-closed server-contract error. |

`202` is valid only with `InProgress`, `FastResponse`, `isRetriable: false`, and zero operation responses.

A replay of a Fast Response token MAY return:

- `202/InProgress` while the stored logical attempt remains nonterminal;
- `200/Committed` after it commits; or
- `452/Aborted` after it aborts.

The response mode remains `FastResponse` in all three cases.

### 7.6 Operation results by state

#### `Committed`

- `operationResponses` contains exactly one result for every request operation.
- Result indices form a complete permutation of `0..N-1`.
- The SDK reorders results by `index`.
- A successful write result carries the final session token when supplied by the backend.
- A create, replace, upsert, or patch result carries the final ETag when supplied by the backend.
- A delete result MAY omit ETag.
- Per-operation request charge includes service-side Phase 1, Phase 2, and coordinator-discarded retry work attributed to that operation.
- The response-level request charge includes all service-side work performed for that wire response.

#### `Aborted`

- `operationResponses` contains exactly one result for every request operation.
- The operation that caused the abort retains its actual failure status and substatus.
- An operation that prepared successfully but was rolled back is rewritten to `453/5415 DtcOperationRolledBack`.
- Every operation result has no session token.
- Every operation result has no customer-meaningful ETag.
- Request charge includes prepare and abort work.

#### `InProgress`

- `operationResponses` is an empty array.
- No operation ETag is present.
- No operation session token is present.
- The SDK reports `Count == 0`.

## 8. Transaction-Status REST Contract

### 8.1 Purpose

This operation reads the coordinator ledger state for one idempotency token. It does not wait for a terminal state and does not poll internally.

### 8.2 Request

```http
GET /operations/dtc/status HTTP/1.1
Authorization: <standard Cosmos DB authorization>
x-ms-date: <RFC 7231 date>
x-ms-version: <supported service version>
x-ms-cosmos-idempotency-token: <non-empty UUID>
x-ms-cosmos-operation-type: ReadDistributedTransactionStatus
x-ms-cosmos-resource-type: DistributedTransactionBatch
Content-Length: 0
```

The request body MUST be empty.

Authorization metadata:

| Field | Value |
|---|---|
| HTTP verb | `GET` |
| Authorization resource type | `distributedtransactionbatch` |
| Authorization resource link | empty string |
| Request path | exactly `/operations/dtc/status`, ignoring only path casing and leading/trailing slash normalization |

For master-key authorization, the canonical signature payload is:

```text
get
distributedtransactionbatch

{lowercase x-ms-date}

```

The final blank line is part of the signature payload.

Authorization rules:

- Master-key and supported account-level Microsoft Entra data-plane authorization are accepted.
- The front end authorizes operation `ReadDistributedTransactionStatus` on the account-level `DistributedTransactionBatch` resource.
- Resource-token authorization is not supported by this v1 status API because one token may represent operations across multiple databases and containers.
- Unsupported resource-token authorization returns `403`.
- A valid idempotency token does not bypass authorization.
- Authentication and authorization happen before ledger lookup.
- Unauthorized callers receive `401` or `403` without revealing whether the token exists.

The customer MUST NOT send `RetriggerDTX`. If it is present after successful authorization, the service returns `400` and performs no ledger or coordinator mutation.

### 8.3 Service processing

The handler performs exactly this algorithm:

```text
1. Validate GET and exact /operations/dtc/status path.
2. Authenticate and authorize the account-level status-read operation.
3. Reject RetriggerDTX.
4. Require x-ms-cosmos-idempotency-token.
5. Parse it as a non-empty UUID; otherwise return 400.
6. Perform one read-only ledger lookup.
7. If no unexpired record exists, return 404.
8. Read the stored internal state, response mode, terminal substatus,
   terminal retryability, and diagnostic classification.
9. Collapse Preparing, Committing, or Aborting to InProgress.
10. Return 200 with the status body.
```

The status handler MUST NOT:

- call the recovery handler;
- update record version, timestamps, status, or retry metadata;
- dispatch prepare, commit, or abort work;
- create a record for an unknown token;
- treat an empty request body as a recovery signal; or
- return operation results.

### 8.4 Success response body

Every known token returns HTTP `200` with:

```json
{
  "transactionStatus": "Aborted",
  "responseMode": "FastResponse",
  "idempotencyToken": "8a9d6f61-f0fb-4ee5-84de-7a7de7fbcf4a",
  "statusCode": 452,
  "subStatusCode": 5421,
  "isRetriable": true,
  "diagnosticString": "Transaction aborted because HLC clock skew exceeded the configured limit."
}
```

Status-body rules:

| Property | Rule |
|---|---|
| `transactionStatus` | Current collapsed public state. |
| `responseMode` | Mode stored when this logical attempt was created. |
| `idempotencyToken` | Requested token. |
| `statusCode` | Transaction outcome code: `202` for `InProgress`, `452` for `Aborted`, `200` for `Committed`. This is not the status API's HTTP code, which is `200`. |
| `subStatusCode` | Stored terminal cause, or `0`. |
| `isRetriable` | Stored transaction resubmission classification. Always `false` for `InProgress` and `Committed`. |
| `diagnosticString` | Stored/bounded transaction diagnostic. Not parsed for control flow. |

The status body MUST NOT contain `operationResponses`.

For `InProgress`, the HTTP response SHOULD include `Retry-After` with the service-recommended minimum delay before the next customer poll. `GetTransactionStatusAsync` returns that header but does not automatically poll.

### 8.5 Status response matrix

| HTTP/substatus | Meaning | Body | SDK action |
|---|---|---|---|
| `200/0` | Token is known | Required status body | Return one `DistributedTransactionResponse` with `Count == 0`. |
| `400/0` | Missing, empty, or malformed token; non-empty body; forbidden recovery header; invalid method/path contract | Standard error body, no transaction state | Return/throw normal bad-request behavior. |
| `401/0` | Authentication failed | Standard error body | Return/throw normal unauthorized behavior. |
| `403/0` | Caller lacks status-read authorization or used unsupported resource-token auth | Standard error body | Return/throw normal forbidden behavior. |
| `404/0` | Token was never accepted or its terminal record expired | Standard error body, no transaction state | Return response with null transaction status/mode. |
| `408/0` | Status read timed out | No trusted transaction body | Retry the GET with the same token under normal point-read timeout policy. |
| `429/*` | Status read was throttled | No trusted transaction body | Retry the GET with the same token using `Retry-After` and normal throttle policy. |
| `500/5411` | Ledger read failed | No trusted transaction body | Retry the GET with the same token under the DTX infrastructure budget. |
| Other `5xx` | Unclassified service failure | No trusted transaction body | Apply normal idempotent-read retry policy; never create a new token. |

Status lookup never performs new-token resubmission. `Aborted + isRetriable: true` is returned to the caller as historical state.

### 8.6 Retention

- Nonterminal records MUST NOT expire.
- A terminal record is guaranteed queryable for at least 24 hours from the durable terminal transition.
- The service MAY retain terminal records longer.
- After guaranteed retention expires, lookup MAY return `404`.
- `404` deliberately does not distinguish an unknown token from an expired token.

## 9. Customer Status Lookup Is Not Recovery

The existing internal recovery path and the new customer status path are different operations.

| Concern | Customer status lookup | Internal recovery |
|---|---|---|
| Path | `GET /operations/dtc/status` | Existing control-plane path |
| Authorization | Customer account-level data-plane authorization | System/control-plane authorization |
| `RetriggerDTX` | Forbidden | Required signal |
| Body | Empty | Empty |
| Ledger access | Read only | Read/write |
| Participant dispatch | Never | May prepare/commit/abort |
| State mutation | Never | Expected |
| SDK exposure | `CosmosClient.GetTransactionStatusAsync` | None |

No implementation may route status lookup to recovery as an optimization.

## 10. Retry Model

### 10.1 Classification order

The SDK classifies a commit result in this order:

```text
1. If cancellation is requested, stop local work.
2. If no trustworthy transaction body exists, classify the transport/HTTP envelope.
3. Validate token, status, response mode, HTTP/body consistency, and result shape.
4. If validation fails, fail closed. Do not use response-controlled retry fields.
5. If state is Committed, return.
6. If state is InProgress and HTTP is 202, return Fast Response acceptance.
7. If state is InProgress and isRetriable is true, replay the same token.
8. If state is Aborted and isRetriable is false, return.
9. If state is Aborted and isRetriable is true, resubmit with a new token.
10. Any other combination is a protocol error.
```

`isRetriable` MUST NOT be acted on until the transaction state is validated.

### 10.2 Retry table

| Observed result | Outcome certainty | Retry kind | Token | Payload |
|---|---|---|---|---|
| Retryable transport exception before a trustworthy response | Unknown | Same-token replay | Same | Same bytes |
| Empty-body `408` | Unknown/in progress | Same-token replay | Same | Same bytes |
| Empty-body `449/5352` | Coordinator race | Same-token replay | Same | Same bytes |
| Empty-body `429/3200` | RU budget unavailable | Same-token replay | Same | Same bytes |
| Empty-body `500/5411`, `500/5412`, or `500/5413` | Infrastructure failure; decision not trusted | Same-token replay | Same | Same bytes |
| Valid `InProgress + isRetriable:true` body | Logical attempt unresolved | Same-token replay | Same | Same bytes |
| Valid `202/InProgress + isRetriable:false` | Fast Response accepted | None | Keep | N/A |
| Valid `Aborted + isRetriable:false` | Terminal rollback | None | Keep | N/A |
| Valid `Aborted + isRetriable:true` | Terminal rollback and safe resubmission | New-token resubmission | New UUID | Same bytes |
| Valid `Committed` | Terminal commit | None | Keep | N/A |
| Malformed/mismatched known response | Untrusted | None | Keep last trusted token | N/A |

### 10.3 `452/5421` rule

`452/5421 DtcHlcClockSkewAborted` is the initial new-token retry case.

The coordinator may set `isRetriable: true` only after:

1. the transaction is durably `Aborted`;
2. every prepared participant has been rolled back or is proven to have no committed transaction write;
3. replaying the same customer operations as a new logical attempt cannot duplicate a committed write.

The SDK MUST NOT replay `452/5421` with the old token. The old token is terminally aborted.

Any future retriable abort substatus uses the same rule only if it satisfies the same rollback guarantee and returns `transactionStatus: Aborted` plus `isRetriable: true`.

### 10.4 Budgets and delay

Retry loops remain bounded.

#### Envelope retry budgets

| Class | Maximum retries | Delay |
|---|---:|---|
| Empty-body `408` and `449/5352` | 10 | Server hint, otherwise 1 second |
| Empty-body `500/5411-5413` | 9 | Exponential from 100 ms, exponent capped at 6, pre-jitter delay capped at 5 seconds |
| `429/3200` | Customer `RetryOptions` throttle budget | `Retry-After` through `ResourceThrottleRetryPolicy` |

#### Body-bearing semantic retry budget

Same-token body retries and new-token retriable-abort resubmissions share one budget:

- maximum 10 retries after the initial body-bearing response;
- maximum 30 seconds cumulative planned delay;
- local backoff `1 second * 2^attempt`;
- exponent capped at 5;
- multiplicative jitter in `[0.75, 1.25]`;
- selected delay is `max(server Retry-After, local backoff)`;
- the SDK checks the cumulative-delay budget before sleeping;
- budget exhaustion returns the last valid response unchanged.

Separate budgets MUST NOT multiply into an unbounded retry loop. Diagnostics record which loop performed each retry.

### 10.5 Commit retry pseudocode

```text
serializedBody = SerializeExactlyOnce(operations)
currentToken = transaction.IdempotencyToken

while true:
    response = SendCommit(serializedBody, currentToken)

    if response has no trusted transaction envelope:
        apply envelope retry policy with currentToken
        if policy stops: return/throw response
        continue

    ValidateKnownResponse(response, currentToken)

    switch response.TransactionStatus:
        Committed:
            return response

        InProgress:
            if response.HttpStatus == 202:
                assert response.ResponseMode == FastResponse
                assert response.IsRetriable == false
                return response

            assert response.IsRetriable == true
            if semantic budget exhausted:
                return response
            DelayWithCancellation()
            continue // same currentToken

        Aborted:
            if response.IsRetriable == false:
                return response

            if semantic budget exhausted:
                return response

            DelayWithCancellation()
            candidateToken = NewNonEmptyUuid()
            PublishAtDispatchBoundary(candidateToken)
            currentToken = candidateToken
            continue // identical serializedBody
```

The transaction object remains single-use. All retries above occur inside the one `CommitTransactionAsync` call.

## 11. Cancellation Contract

Cancellation is local control flow, not a distributed abort protocol.

| Cancellation point | Wire request sent? | Server effect | Published token | SDK result |
|---|---:|---|---|---|
| Before commit begins | No | None | Initial token remains available; lookup normally returns `404` | Throw `OperationCanceledException`. |
| During RID resolution or serialization | No commit request | None | Initial token remains available; lookup normally returns `404` | Throw `OperationCanceledException`. |
| Before first dispatch handoff | No | None | Initial token | Throw `OperationCanceledException`. |
| During an in-flight commit request | Maybe/yes | Coordinator may create or continue the logical attempt | Token used by that request | Stop waiting and throw `OperationCanceledException`. Do not send abort. |
| During same-token retry delay | No next request | Existing logical attempt continues | Existing token | Cancel delay; do not send next replay. |
| After retriable abort but before new-token dispatch handoff | No new attempt | Old attempt remains terminally aborted | Old token | Cancel delay/work; do not publish candidate token. |
| During/after new-token dispatch handoff | Maybe/yes | New logical attempt may exist | New token | Throw `OperationCanceledException`; caller may query new token. |
| After receiving `202` | Commit call already completed | Phase 2 continues | Accepted token | Cancellation cannot revoke the returned transaction. |
| During status lookup | Maybe/yes read only | No transaction mutation | Requested token | Cancel only that lookup. |
| Between customer polls | No | No transaction mutation | Requested token | Customer chooses whether to poll again. |

The SDK MUST NOT call `AbortDistributedTransaction` in response to a `CancellationToken`.

There is no customer-facing server-side cancellation endpoint in this phase.

## 12. Response Validation and Fail-Closed Rules

For a known commit or status response, the SDK validates:

1. `idempotencyToken` parses as a non-empty UUID.
2. Request token, JSON token, and response-header token are equal.
3. `transactionStatus` is one of the three exact strings.
4. `responseMode` is one of the two exact strings.
5. HTTP status, body `statusCode`, state, retryability, and operation-result count match the relevant table.
6. Commit operation indices are complete, unique, and in range.
7. Status lookup contains no operation results.

Failure behavior:

- Do not infer a new token from a malformed response.
- Do not perform new-token resubmission.
- Do not merge ETags or session tokens.
- Return a fail-closed SDK response or exception using the existing invalid-server-response pattern.
- Preserve diagnostics and the last locally trusted token.

Rolling-deployment compatibility:

- A legacy commit `200` may infer `Committed`.
- A legacy commit `452` may infer `Aborted`.
- A legacy terminal commit response may infer `Standard`.
- `202` requires explicit `InProgress` and `FastResponse`; no legacy inference is allowed.
- Status lookup requires all status-body fields; no legacy inference is allowed.
- Unknown future status or response-mode strings are not mapped to a current value.

## 13. ETag and Session-Token Semantics

| Response | Operation count | ETag | Session token | SDK session-container merge |
|---|---:|---|---|---:|
| Standard commit `Committed` | `N` | Final per-operation ETag when applicable | Final per-operation token when supplied | Yes |
| Standard/Fast replay commit `Aborted` | `N` | None customer-meaningful | None | No |
| Fast commit `202/InProgress` | `0` | None | None | No |
| Status `InProgress` | `0` | None | None | No |
| Status `Aborted` | `0` | None | None | No |
| Status `Committed` | `0` | None | None | No |

Consequences:

- Polling a Fast Response token to `Committed` confirms transaction outcome only.
- Polling does not provide the final operation results.
- Polling does not provide final ETags.
- Polling does not establish SDK session read-your-write state.
- A subsequent Session-consistency read may need to acquire session state normally from the service.
- `DistributedTransactionOperationResult` remains the operation-result type and receives no response-mode property.
- `ResponseMode` belongs on `DistributedTransactionResponse` because status-only responses have no operation results.

## 14. Request-Charge Semantics

- Standard terminal commit charge includes service-side prepare, terminal Phase 2, and service-side discarded retries represented by that response.
- Aborted terminal commit charge includes prepare and abort work represented by that response.
- Fast `202` charge includes work completed before the acknowledgment; it does not promise to expose future Phase-2 charge.
- A status lookup's `x-ms-request-charge` and SDK `RequestCharge` describe the status-read request, not the original commit.
- SDK retries are separately visible in diagnostics. The final `RequestCharge` property is not defined as the sum of prior HTTP responses discarded by the SDK.

## 15. Diagnostics

One `CommitTransactionAsync` invocation uses one parent trace and one `CosmosDiagnostics` tree.

Every wire attempt records:

| Field | Meaning |
|---|---|
| `logicalAttempt` | Zero-based token generation number. Increment only for new-token resubmission. |
| `wireAttempt` | Zero-based HTTP attempt number across the whole commit invocation. |
| `idempotencyToken` | Token sent on that wire attempt. |
| `httpStatusCode` | HTTP result when available. |
| `subStatusCode` | HTTP substatus when available. |
| `transactionStatus` | Parsed public state when trusted. |
| `responseMode` | Parsed stored response mode when trusted. |
| `retryClass` | `none`, `same-token-replay`, or `new-token-resubmit`. |
| `retryDelayMs` | Planned delay before the next attempt. |
| `retryBudget` | Budget class and remaining count/delay. |

Rules:

- Diagnostics retain all attempts when the final result is `202`, `200`, `452`, budget exhaustion, exception, or cancellation.
- Token transitions are explicit trace events.
- Coordinator diagnostic strings are bounded before logging.
- Status lookup creates its own normal `CosmosDiagnostics`; it never appends to the original commit diagnostics.

## 16. .NET SDK Surface

### 16.1 Existing commit API

Keep the existing method:

```csharp
Task<DistributedTransactionResponse> CommitTransactionAsync(
    CancellationToken cancellationToken = default);
```

Do not add `ScheduleTransactionAsync`.

### 16.2 Status lookup

Add:

```csharp
Task<DistributedTransactionResponse> GetTransactionStatusAsync(
    Guid idempotencyToken,
    CancellationToken cancellationToken = default);
```

Argument behavior:

- `Guid.Empty` throws `ArgumentException` before sending a request.
- A canceled token before dispatch throws `OperationCanceledException`.
- The method performs one logical status read, subject only to normal idempotent-read retries.
- It does not poll until terminal.

### 16.3 Public enums

```csharp
public enum DistributedTransactionStatus
{
    InProgress = 0,
    Aborted = 1,
    Committed = 2,
}

public enum DistributedTransactionResponseMode
{
    Standard = 0,
    FastResponse = 1,
}
```

### 16.4 `DistributedTransaction`

Add:

```csharp
public Guid IdempotencyToken { get; }
```

The value follows the publication contract in section 6.3.

### 16.5 `DistributedTransactionResponse`

Add:

```csharp
public DistributedTransactionStatus? TransactionStatus { get; }
public DistributedTransactionResponseMode? ResponseMode { get; }
public bool IsTerminal { get; }
```

Rules:

- `TransactionStatus` and `ResponseMode` are non-null only for a trusted transaction response.
- They are null for ordinary HTTP errors such as status lookup `404`.
- `IsTerminal` is true only for `Committed` and `Aborted`.
- `IsTerminal` is false for `InProgress` and null status.
- `StatusCode` remains the actual HTTP response code:
  - commit Fast Response: `202`;
  - known status lookup: `200`, regardless of transaction state.
- `IsSuccessStatusCode` remains HTTP-only and MUST NOT be documented as “transaction committed.”
- `IsRetriable` is the coordinator transaction classification. Its XML documentation MUST NOT say that every `true` value is safe with the same token.
- `Count == 0` for Fast Response and all status lookups.

## 17. Service Implementation Requirements

### 17.1 Front end

The front end must:

1. add exact recognition for `/operations/dtc/status`;
2. map `GET` plus `x-ms-cosmos-operation-type: ReadDistributedTransactionStatus` to a dedicated operation type;
3. map resource type to `DistributedTransactionBatch`;
4. authorize with `distributedtransactionbatch` and empty resource link;
5. reject unsupported methods, malformed tokens, bodies, and customer recovery headers;
6. route status reads to a dedicated read-only handler;
7. preserve existing `/operations/dtc` commit routing.

### 17.2 Protocol mappings

Add `ReadDistributedTransactionStatus` consistently to:

- shared `OperationType`;
- RNTBD operation type;
- HTTP-to-operation formatter;
- transport serialization mapping;
- operation-name string mapping;
- telemetry/activity classification; and
- coordinator request dispatch.

It MUST NOT alias the internal recovery operation.

### 17.3 Coordinator and ledger

The ledger record must persist:

- idempotency token;
- canonical request-payload identity/hash;
- participant metadata needed by execution/recovery;
- internal transaction state;
- stored response mode;
- terminal substatus;
- terminal `isRetriable`;
- bounded terminal diagnostic classification;
- creation time; and
- terminal transition time used for retention.

The coordinator must implement a read-only lookup that returns this metadata without mutating the record.

### 17.4 Response builder

The response builder must:

- add `transactionStatus`, `responseMode`, and payload `idempotencyToken`;
- emit `202/InProgress` only for durable Fast Response acceptance;
- preserve existing terminal operation-result construction;
- emit no operation responses for status lookup;
- persist and replay terminal retryability/substatus; and
- emit matching request/header/payload tokens.

## 18. SDK Implementation Requirements

The .NET SDK must:

1. serialize operations once and retain reusable immutable bytes;
2. separate logical-attempt token state from immutable operation payload;
3. publish token transitions at the dispatch boundary;
4. separate envelope retry from body-bearing semantic retry;
5. add new-token resubmission for validated retriable aborts;
6. keep the transaction object single-use;
7. parse and validate the common transaction fields before retry decisions;
8. allow valid zero-result `202` and status responses;
9. use a status-only parser path that has no request-operation-count requirement;
10. merge session tokens only for terminal commit responses with real operation results;
11. aggregate diagnostics across all commit retries; and
12. issue `GET /operations/dtc/status` with no body for status lookup.

## 19. Required Conformance Tests

### 19.1 State and response parsing

- Parse each of the three exact status strings.
- Parse both response modes.
- Reject unknown/case-variant status and mode strings.
- Reject empty/malformed/mismatched payload and header tokens.
- Infer only legacy `200 => Committed`, `452 => Aborted`, and terminal `Standard`.
- Reject `202` without explicit `InProgress` and `FastResponse`.
- Verify `IsTerminal` for every nullable status value.

### 19.2 Standard commit

- `200/Committed` returns exactly `N` ordered results.
- Final ETags and session tokens are retained where applicable.
- Session-container merge occurs.
- `452/Aborted` rewrites successful prepared operations to `453/5415`.
- Aborted results contain no session token or customer ETag.
- `408/InProgress` has zero results and replays the same token.

### 19.3 Fast Response commit

- `202` is impossible before durable Phase 1.
- `202` contains `InProgress`, `FastResponse`, matching token, `isRetriable:false`, and zero results.
- The SDK returns `202` without another commit request.
- Coordinator Phase 2 continues after client disconnect.
- Same-token replay after lost `202` can observe `202`, `200`, or `452`.

### 19.4 Status lookup

- Exact GET path and headers route to the status handler.
- POST, PUT, DELETE, wrong path, non-empty body, malformed token, and `RetriggerDTX` are rejected.
- Authentication occurs before token existence is disclosed.
- Master-key and supported Entra authorization succeed.
- Resource-token authorization returns `403`.
- Known internal `Preparing`, `Committing`, and `Aborting` each return `200/InProgress`.
- Known terminal states return `200/Committed` or `200/Aborted`.
- Unknown and expired tokens both return indistinguishable `404`.
- Every success has zero operation results, ETags, and session tokens.
- Repeated status reads do not change ledger record version/state and dispatch no participant request.
- Status cancellation changes no transaction state.

### 19.5 Same-token replay

- Transport uncertainty reuses token and exact bytes.
- Empty-body `408`, `449/5352`, `429/3200`, and `500/5411-5413` use the correct budget.
- Valid retriable `InProgress` body reuses token.
- Diagnostics contain every wire attempt.

### 19.6 New-token resubmission

- `452/5421` produces a distinct non-empty token.
- Serialized operation bytes are identical.
- Old token remains queryable as `Aborted`.
- New token is published only at dispatch handoff.
- Cancellation before handoff retains the old token.
- Cancellation/exception after handoff exposes the new token.
- Shared semantic count and cumulative-delay budgets stop retries.
- Budget exhaustion returns the last valid `Aborted + isRetriable:true` response unchanged.

### 19.7 Metadata and compatibility

- Standard committed responses preserve current ETag/session behavior.
- Fast Response polling to `Committed` never fabricates ETags/session tokens.
- Status `StatusCode` is HTTP `200` while body `statusCode` reflects transaction outcome.
- Account-mode changes do not change an existing token's stored response mode.
- A new-token resubmission snapshots the current account mode.
- Terminal state and retryability remain stable for the guaranteed 24-hour retention window.

## 20. Acceptance Gate

The feature is not implementation-complete until all of the following are true:

- every normative state/code combination has a service test and SDK test;
- the status path is proven read-only;
- cancellation tests cover every row in section 11;
- retriable abort tests prove rollback before new-token resubmission;
- diagnostics identify token transitions and both retry classes;
- Fast Response documentation explicitly states the ETag/session limitation; and
- no customer path can set or reach internal recovery behavior.
