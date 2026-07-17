# Distributed Transactions (DTC) — REST Contract

This document describes the wire contract used by the .NET SDK to execute a **Distributed
Transaction** (DTC) against the Cosmos DB service. A distributed transaction lets a client
commit a batch of point operations that span **multiple containers** atomically, coordinated
server-side by the Distributed Transaction Coordinator (DTC).

**Write** transactions (the SDK issues `CommitDistributedTransaction`) and **read**
transactions (`Read`) share the same endpoint and envelope but use **different**
response-code contracts. Writes commit a batch atomically and can return
`200`/`452`/`449`/`408`/`429`/`400`/`403`/`500`
(see [§6 — write transactions](#aggregate-status-codes--write-transactions-commit)); reads
take a consistent multi-container snapshot and can return
`200`/`304`/`207`/`404`/`449`/`408`
(see [§6 — read transactions](#aggregate-status-codes--read-transactions)). The two sets
overlap on the shared codes but each has an exclusive one: `452` (abort) is **write-only**
and `207` (multi-status) is **read-only**. CosmosClient **never** issues
`AbortDistributedTransaction`: abort is decided and driven server-side by the coordinator,
and the client observes an aborted transaction only as the `452` terminal response (§5).

The contract is a single logical HTTP/REST call: the SDK `POST`s a JSON batch of operations to
an account-level endpoint and receives a JSON document describing the per-operation outcome.

> Source of truth for the SDK side is
> [`Microsoft.Azure.Cosmos/src/DistributedTransaction/`](../Microsoft.Azure.Cosmos/src/DistributedTransaction).
> The relevant types are `DistributedTransactionCommitter`, `DistributedTransactionServerRequest`,
> `DistributedTransactionSerializer`, and `DistributedTransactionResponse`.

---

## 1. Endpoint (URI)

| | |
|---|---|
| **Path** | `/operations/dtc` |
| **Scope** | Account-level (no `dbs/{db}/colls/{coll}` segments — a transaction may span many containers) |
| **Built from** | `Paths.OperationsPathSegment + "/" + Paths.Operations_Dtc` → `"operations" + "/" + "dtc"` |
| **SDK source** | `DistributedTransactionCommitter.ResourceUri` |

The service rejects any DTC request whose path is not exactly `/operations/dtc` with
`400 Bad Request`.

## 2. Action (method + operation)

| | |
|---|---|
| **HTTP method** | `POST` (always) |
| **Resource type** | `DistributedTransactionBatch` |
| **Operation type** | Carried in a **header**, not the verb. CosmosClient sets `CommitDistributedTransaction` (write) or `Read` (read); it **never** sets `AbortDistributedTransaction` — that operation type is coordinator-only (see §1) |
| **SDK source** | `DistributedTransactionCommitter.ExecuteCommitAsync` → `ProcessResourceOperationStreamAsync(resourceType: ResourceType.DistributedTransactionBatch, operationType: this.operationType, ...)` |

The SDK always issues the request in **gateway mode** (`RequestMessage.UseGatewayMode = true`);
it is never sent direct-to-replica.

## 3. Request headers

Three headers are added by `DistributedTransactionCommitter.EnrichRequestMessage`;
the remaining two are supplied by the shared client pipeline and are **not**
DTX-specific. A port must emit the first three from its own DTX layer and rely on
the common client stack (auth + activity-id) for the last two.

| Header | Constant | Example value | Set by | Purpose |
|---|---|---|---|---|
| `x-ms-cosmos-operation-type` | `HttpConstants.HttpHeaders.OperationType` | `CommitDistributedTransaction` (write) / `Read` (read) | `EnrichRequestMessage` | The transaction action (commit / read). Value is the `OperationType` enum member name via `.ToOperationTypeString()` |
| `x-ms-cosmos-resource-type` | `HttpConstants.HttpHeaders.ResourceType` | `DistributedTransactionBatch` | `EnrichRequestMessage` | The resource type. Value is the `ResourceType` enum member name via `.ToResourceTypeString()` |
| `x-ms-cosmos-idempotency-token` | `HttpConstants.HttpHeaders.IdempotencyToken` | `<GUID>` | `EnrichRequestMessage` | Stable across retries; the server uses it to deduplicate |
| `x-ms-activity-id` | `HttpConstants.HttpHeaders.ActivityId` | `<GUID>` | Common pipeline (`GatewayStoreClient`) | Request correlation; sender-generated, echoed back by the backend |
| `authorization` | `HttpConstants.HttpHeaders.Authorization` | `<token>` | Common pipeline (`TransportHandler`) | Standard Cosmos auth (master key / RBAC token). Emitted lowercase on the wire |

The **idempotency token** is generated once per transaction
(`DistributedTransactionServerRequest.IdempotencyToken`) and is **reused on every retry** of the
same transaction, which is what makes retries safe.

## 4. Request body

* **Media type:** `application/json` (UTF-8).
* **Serializer:** `DistributedTransactionSerializer.SerializeRequest`.
* **Shape:** a single object with an `operations` array. Per-transaction metadata
  (idempotency token, operation type, resource type) travels in **headers**, not the body.

```jsonc
{
  "operations": [
    {
      "databaseName":         "string",   // required
      "collectionName":       "string",   // required
      "id":                   "string",   // required — document id (always emitted)
      "collectionResourceId": "string",   // emitted when present — container RID
      "databaseResourceId":   "string",   // emitted when present — database RID
      "partitionKey":         <json>,     // required — raw JSON value, one per operation
      "index":                0,          // required — uint32, the operation's ordinal in the batch
      "resourceBody":         { },        // optional — nested JSON document (writes only)
      "sessionToken":         "string",   // optional
      "ifMatch":              "string",   // optional — ETag; If-Match for conditional writes
      "ifNoneMatch":          "string",   // optional — If-None-Match for conditional operations
      "operationType":        "Create",   // Create | Replace | Delete | Upsert | Patch | Read
      "resourceType":         "Document"
    }
    // ... one entry per operation in the transaction
  ]
}
```

Notes:
* The `operations` array must be **non-empty**.
* `partitionKey` is **required** and always emitted, one per operation. Every public DTX read/write
  API takes a non-nullable `PartitionKey`, and the SDK unconditionally derives its JSON form before
  serialization, so it is present on every user operation. (The null-guard in
  `DistributedTransactionSerializer` is defensive only.)
* `collectionResourceId` and `databaseResourceId` are emitted only when the SDK has resolved them for
  the operation; they are omitted otherwise (both are serialized conditionally by
  `DistributedTransactionSerializer`).
* `resourceBody` is emitted only for operations that carry a document payload (writes).
* `ifMatch` and `ifNoneMatch` are both optional and emitted only when the operation specifies the
  corresponding condition. The request-side conditional-ETag field is named **`ifMatch`** (contrast
  with the response, which uses `Etag` — see §6).
* A `Patch` operation whose `DistributedTransactionPatchItemRequestOptions.FilterPredicate` is set
  serializes an extra **`condition`** field — a SQL predicate string (e.g.
  `from c where c.status = 'pending'`) — **inside that operation's `resourceBody`**, *not* as a
  top-level operation field. The server evaluates the predicate atomically before applying the patch;
  if it is unsatisfied the operation fails with **`412` PreconditionFailed** and the whole transaction
  is not committed. The field is absent when no filter predicate is set, and coexists with an
  operation-level `ifMatch` when both are supplied.

## 5. Response headers

| Header | Example value | Surfaced on SDK as |
|---|---|---|
| `x-ms-cosmos-idempotency-token` | `<GUID>` | `DistributedTransactionResponse.IdempotencyToken` (echoes the request) |
| `x-ms-activity-id` | `<GUID>` | `DistributedTransactionResponse.ActivityId` |
| *(HTTP status line)* | `200` | `DistributedTransactionResponse.StatusCode` |
| `x-ms-substatus` | `0` | `DistributedTransactionResponse.SubStatusCode` |
| `x-ms-request-charge` | `12.34` | `DistributedTransactionResponse.RequestCharge` (total RU across all phases) |
| `x-ms-retry-after-ms` | `1000` | `Headers.RetryAfter` — used as the retry backoff hint |

## 6. Response body

* **Media type:** `application/json` (UTF-8).
* **Parser:** `DistributedTransactionResponse` (`PopulateFromJsonContentAsync` →
  `DistributedTransactionOperationResult.FromJson`).
* **Shape:** an aggregate transaction outcome plus a per-operation array.

```jsonc
{
  "idempotencyToken": "guid",      // echoes the request token
  "statusCode":       200,         // aggregate transaction status (see table below)
  "subStatusCode":    0,
  "requestCharge":    12.34,       // total RU consumed across all phases (double)
  "isRetriable":      false,       // whether the SDK should retry with the same idempotency token
  "diagnosticString": "Transaction committed successfully",
  "operationResponses": [          // empty [] for in-progress / aborted-with-no-ops
    {
      "index":               0,                 // matches the request operation index
      "statusCode":          200,
      "subStatusCode":       0,
      "sessionToken":        "pkRangeId:LSN",    // canonical session token (pkRangeId-prefixed)
      "Etag":                "string",
      "requestCharge":       2.0,
      "isRetriable":         false,
      "partitionKeyRangeId": "string",           // optional
      "localLsn":            123,                 // optional — emitted by the coordinator, not consumed by the SDK (see §6)
      "resourceBody":        { }                  // optional — nested JSON (reads / created docs)
    }
    // ... one entry per operation, keyed by `index`
  ]
}
```

Notes:
* The response-side ETag field is named **`Etag`** (the coordinator emits it with this exact
  casing, matching the SDK constant `DistributedTransactionSerializer.ResponseETag`). The SDK
  reads response fields **case-insensitively** (`DistributedTransactionOperationResult.TryGetProperty`
  uses `StringComparison.OrdinalIgnoreCase`), so the request/response ETag naming difference
  (request-side `ifMatch` vs response-side `Etag`) is harmless.
* Each per-operation `sessionToken` is captured into the client's `SessionContainer` after a
  successful response (`DistributedTransactionCommitter.MergeSessionTokens`) so subsequent
  session-consistency reads on the affected containers see the latest token.
* Several top-level fields are **redundant with the HTTP response envelope**: `idempotencyToken`,
  `statusCode`, `subStatusCode`, and `requestCharge` are also carried in the status line and
  response headers (§5). The SDK reads these four authoritatively from the **envelope/headers**; it
  consumes only `isRetriable`, `diagnosticString`, and `operationResponses` from the JSON body.
* The per-operation `isRetriable` and `localLsn` fields are emitted by the coordinator but are
  **not consumed by the SDK** — the outer-loop retry decision is driven solely by the top-level
  `isRetriable`.
* `operationResponses` may arrive in an order **different** from the request; the SDK reorders
  entries by each one's `index` so that `response[i]` is always the *i*-th submitted operation. The
  `index` values must form a **complete permutation of `0..n-1`** (a `HasIndex` flag disambiguates a
  genuine `0` from a defaulted-missing one); if any index is missing, duplicated, or out of range the
  payload is uninterpretable and the SDK **fails closed with `500`** — the per-op results are
  discarded and replaced with uniform error placeholders, while the envelope's
  `isRetriable`/`diagnosticString` are preserved.

### Aggregate status codes — write transactions (commit)

These apply when the operation type is `CommitDistributedTransaction` — the only write
operation type CosmosClient issues. A `452` here is how the client **observes** a
coordinator-driven abort; the client never issues `AbortDistributedTransaction` itself.

| `statusCode` | Meaning | `isRetriable` |
|---|---|---|
| `200 OK` | Transaction committed; per-operation results included | `false` |
| `452` | Transaction aborted; per-op results included — voted-No ops keep their original code, prepared (voted-Yes) ops are rewritten to `453` (sub-status `5415`, `DtcOperationRolledBack`) | App-dependent |
| `408 Request Timeout` | Stuck; coordinator retries exhausted (empty body) | `true` |
| `449` | Coordinator race conflict (sub-status `5352`; empty body; carries `Retry-After`) | `true` |
| `400` | Validation failure (sub-status `5405`–`5410`; empty body) | `false` |
| `403` | Write forbidden (e.g., wrong write region) | `false` |
| `429` | Throttled (sub-status `3200`; empty body) | `true` |
| `500` | Infrastructure failure (sub-status `5411`–`5413`; empty body) | `true` |

> `207` is **never** returned for write transactions.

### Aggregate status codes — read transactions

Read distributed transactions (`CreateDistributedReadTransaction`, operation type
`Read`) use a **two-phase snapshot-validation** protocol and a **different** response
contract from writes.

* **Phase 1** reads content + log LSN from each participant.
* **Phase 2** re-validates the LSNs (LSN-only) to confirm a consistent snapshot across
  all participants.

The coordinator retries internally (Phase 1 in-flight `449`, Phase 2 LSN drift / in-flight)
until it reaches a terminal outcome or exhausts its retry budget; the SDK only ever sees the
terminal outcome.

| `statusCode` | When | Body | `isRetriable` |
|---|---|---|---|
| `200 OK` | Completed; **every** per-op code is `200` | per-op, with document bodies | `false` |
| `304 Not Modified` | Completed; **every** per-op code is `304` (etag `IfNoneMatch` matched) | per-op, no document bodies | `false` |
| `207 Multi-Status` | Completed or failed; **≥2 distinct** surfaced per-op codes | per-op | `false` |
| `404 Not Found` | Phase 1 failure; the lone surfaced code is `404` | per-op | `false` |
| `449` | Phase 1 in-flight exhaustion; the lone surfaced code is `449` | per-op | `true` |
| `408 Request Timeout` | **Phase 2 failure**, or an unconfirmed snapshot | **empty** | `true` |

Up-front validation errors (`400` / `429` / `500`) are shared with the write contract above.
The aggregate sub-status is always `0`; per-op sub-statuses travel in the per-op detail.

**Per-operation semantics (reads):**

| per-op code | Meaning | Effect on the transaction |
|---|---|---|
| `200` | Read OK (content + LSN) | success |
| `304` | NotModified (`IfNoneMatch` matched) | success (no body) |
| `404` | NotFound | **non-retryable hard failure — fails the whole transaction, no retries** |
| `449` | RetryWith (in-flight write observed) | retry from Phase 1 (bounded by the retry budget) |
| `424` | FailedDependency (a sibling op failed) | neutral; not promoted |
| other (`412`, `5xx`, …) | hard failure | fails the transaction |

> **`404` is a failure, not a valid read outcome.** A point read of a missing document
> fails the entire read transaction with no retries. (Previously `404` was treated as a
> per-op success; the server later removed it from the success set, which is now `{200, 304}`.)

> **Snapshot consistency / read-your-write:** a read distributed transaction observes a
> **consistent point-in-time snapshot** whose LSN is chosen at transaction start. On a slow or
> lagging account that snapshot LSN can briefly trail a *just-committed* write, so an
> immediately-prior write may **transiently** surface as a per-op `404` (and, with another op
> genuinely missing, collapse the whole tx to envelope `404` with no `424` rewrite). Re-driving
> the read transaction lets the snapshot advance past the write, after which the op surfaces
> normally (`200` → `424` on a failed mixed-existence read). This is a timing characteristic of
> the consistent-snapshot read, not a lost write.

**Failure-response op rewrite (Phase 1 only):** when a read transaction fails in Phase 1,
every individually-successful op (`200`/`304`) is rewritten to **`424` FailedDependency**
(sub-status `0`) and its document body is **stripped** — those ops did not contribute to a
confirmed snapshot, so the SDK must not surface them as successful or carry their now-stale
body. Ops with real failure codes keep their status **and** body.

* **Phase 1 failure** (hard failure incl. `404`, or Phase 1 in-flight `449` exhaustion):
  per-op detail is surfaced *after* the `424` rewrite; the envelope is the **promotion of the
  remaining non-`424` codes** — a single distinct code is promoted as-is (all-`404` → `404`,
  all-`449` → `449`), **≥2 distinct → `207`**. `isRetriable` follows the promoted code
  (`449`/`503`/`408`/`429`/`410`/`500`/`502` → retriable; `404`/`207` → not).
* **Phase 2 failure** (hard failure, LSN mismatch, or Phase 2 in-flight exhaustion): the
  envelope is **`408` with an empty body** — no per-op detail is surfaced (intentionally
  asymmetric with Phase 1; a Phase 2 in-flight `449` is **not** surfaced per-op).

**Worked examples (reads):**

| Phase 1 per-op | Result |
|---|---|
| `[200, 404, 200, 200]` | 200s → `424`; remaining `{404}` → envelope **`404`**; SDK sees `[424, 404, 424, 424]`, no bodies |
| `[200, 404, 200, 412]` | remaining `{404, 412}` (2 distinct) → envelope **`207`** |
| `[200, 449]` (in-flight exhaustion) | 200 → `424`; remaining `{449}` → envelope **`449`** (retriable) |
| `[200, 449]` in **Phase 2**, budget exhausted | envelope **`408`**, empty body (the `449` is not surfaced) |

**Client-side status promotion (`207` is unwrapped, never surfaced raw).** The `207`
above is an *on-the-wire envelope*. CosmosClient does **not** hand a raw `207` back to
the caller: when it receives a `207 Multi-Status`, it scans the per-op results and
**promotes the first per-op result that is neither `424 FailedDependency` nor a success**
(i.e. the first status `>= 400` that is not `424`), inheriting **both** that op's
`statusCode` **and** its `subStatusCode` as the response's terminal status. The first
match wins; the raw `207` is discarded. This mirrors the batch path
(`TransactionalBatchResponse`).

```
finalStatus, finalSubStatus = 207, 0
if envelope == 207:
    for op in results:                       # in wire order
        if op.statusCode != 424 and op.statusCode >= 400:
            finalStatus    = op.statusCode   # e.g. 412, 404, 5xx
            finalSubStatus = op.subStatusCode
            break                            # FIRST real error wins
```

So for the `[200, 404, 200, 412]` example the wire envelope is `207`, but the caller
observes **`404`** (the first non-`424` error op, in wire order) with that op's
sub-status — not `207`. Because promotion inherits the per-op sub-status, retry
classification (§8) runs against the **promoted** op's `statusCode`/`subStatusCode`, not
against `207` (which is itself non-retriable).

## 7. Payload type summary

| Aspect | Value |
|---|---|
| Request body | `application/json` (UTF-8) — single object with `operations[]` |
| Response body | `application/json` (UTF-8) — single object with `operationResponses[]` |
| Metadata placement | Transaction-level metadata (idempotency token, operation type, status, RU, retry hint) travels in **headers**; per-operation detail travels in the **JSON body** |

## 8. Retry model

Two layers of retry apply, and **which layer owns a retriable response is decided by
whether that response carries a JSON body.** The coordinator deliberately distinguishes
a *semantic* failure (a per-op body with `isRetriable`) from an *envelope* failure (an
empty body): the same status code — e.g. `449` or `429` — is handled very differently
depending on which shape arrives.

1. **Outer (semantic):** `DistributedTransactionCommitter.ExecuteCommitWithRetryAsync`
   owns every **body-bearing** retriable response — the JSON sets `isRetriable: true`
   (in-progress `408`, coordinator race `449`, throttle `429`). It **reuses the same
   idempotency token** on every attempt, which is what makes the retries safe.
2. **Inner (`ClientRetryPolicy`):** owns **bodyless** responses. Beyond the usual
   transport failures (connection, region failover) its DTX
   classifier (`ShouldRetryDtxRequest`) also owns the empty-body coordinator and
   infrastructure codes, splitting them across three distinct budgets (below). It
   defers to the outer loop for any body-bearing coordinator code to avoid
   inner × outer retry amplification.

### Retry routing by status code and body

| Envelope | Sub-status | Body? | Owner | Budget |
|---|---|---|---|---|
| `408` / `449` | `5352` (`449`) | **yes** | Outer committer loop | 10 attempts / 30 s |
| `429` | `3200` | **yes** | Outer committer loop | 10 attempts / 30 s |
| `408` / `449` | `5352` (`449`) | **no** | Inner `RetryDtxWithBudget` (coordinator) | 10 attempts, 1 s (or server `Retry-After`) |
| `429` | `3200` | **no** | Shared `ResourceThrottleRetryPolicy` | 9 attempts / 30 s, honors `x-ms-retry-after-ms` |
| `500` | `5411`–`5413` | **no** | Inner `RetryDtxWithBudget` (infra) | 9 attempts, exp backoff 100 ms → 5 s |
| `452` | `5421` (Aborted) | either | *no inner retry* — terminal to the classifier | — |

> **Why `429`/`3200` splits from `449`/`5352`.** Both are *coordinator-retriable*, but a
> **bodyless** throttle is intentionally routed away from the DTX budget and into the
> shared `ResourceThrottleRetryPolicy` — the same policy that governs every other
> throttled Cosmos request — so DTX throttling honors the account's
> `MaxRetryAttemptsOnRateLimitedRequests` / `MaxRetryWaitTimeOnRateLimitedRequests`
> knobs and the server's `x-ms-retry-after-ms` hint. A bodyless `449`/`5352` (or `408`),
> by contrast, stays on the dedicated coordinator budget. The classifier keys this off
> the presence of a real response body, so the coordinator must **omit** the body on a
> throttle it wants the shared policy to absorb (a zero-length body counts as "no body").

**Outer (semantic) loop.** It stops and returns the last response when any of these holds:

* the response is a success status, or its body has `isRetriable: false`;
* the attempt count reaches `MaxIsRetriableRetryCount` (**10**); or
* the **cumulative planned delay** would exceed `MaxCumulativeRetryDelay` (**30 s**).

Each backoff delay is `max(serverHint, computedBackoff)`, where `serverHint` is the
`x-ms-retry-after-ms` header (used only when it exceeds the local delay) and `computedBackoff` is a
bounded exponential — `retryBaseDelay · 2^min(attempt, RetryMaxExponent)` with **±25 % jitter**
(`DistributedTransactionRetryHelpers.ComputeBackoff`). With the default 1 s base and
`RetryMaxExponent = 5` (a ~32 s per-attempt ceiling before jitter), the 30 s cumulative budget is
normally the binding limit — roughly 4–5 retries — rather than the 10-attempt cap.

**Inner (bodyless) budgets.** A bodyless coordinator code (`408`, or `449`/`5352`) retries
via `RetryDtxWithBudget` up to `MaxDtxRetryCount` (**10**) attempts, each after the server's
`Retry-After` if present else a flat **1 s** (`RetryIntervalInMS`). A bodyless infrastructure
failure (`500` with `5411`–`5413` — ledger / account-config / dispatch) uses a separate,
tighter budget: `MaxDtxInfraFailureRetryCount` (**9**) attempts with an exponential backoff of
**100 ms → 5 s** (`ComputeBackoff`, max exponent 6). A bodyless `429`/`3200` is not handled
by either DTX budget — the classifier returns control to the shared `ResourceThrottleRetryPolicy`.

The bodyless `429`/`3200` fall-through above depends on `ClientRetryPolicy` treating a **non-null but
zero-length** response stream as "no body" for DTX requests
(`hasResponseBody = content != null && (!isDtxRequest || !content.CanSeek || content.Length > 0)`): a
bodyless throttle that arrives as an empty (seekable, `Length == 0`) stream is classified as bodyless
and routed to the shared `ResourceThrottleRetryPolicy` (honoring the account's rate-limit knobs)
rather than the **outer** commit loop. A non-seekable stream is conservatively counted as having a
body.

### Why a `408` is retriable for DTX (and how the two 408 shapes are told apart)

For an ordinary write a `408` is **unsafe** to retry: the request may have committed
server-side while only the acknowledgement was lost, so a blind retry risks applying the
mutation twice. This is true even for — in fact, especially for — a **bodyless `408` that is a
genuine transport-level timeout**, where the client has no way to know whether the mutation
landed. DTX sidesteps this entirely — every request carries a stable
`x-ms-cosmos-idempotency-token` (see [§3](#3-request-headers)) that is **reused verbatim on
every retry** (`ExecuteCommitWithRetryAsync` logs *"Retrying with idempotency token …"*), and
the coordinator **keys the transaction record on that token**: the first request to arrive
creates the ledger record, and any later request bearing the same token resolves to the
**existing** record (create-first, then read-on-conflict) instead of starting a second
transaction — a request with an empty token is rejected outright. A replay therefore can never
double-apply; it converges on the one transaction and returns its authoritative state
(committed → `200`, aborted → `452`, still in-flight → `408` again). So a `408` is safe to
retry **regardless of whether the original attempt reached the coordinator**, which is exactly
what makes the bodyless transport-timeout case safe here even though it would be unsafe for an
ordinary write.

Given that safety, the client does **not** need to prove a `408` came from the coordinator
versus the transport — it only needs to pick the right retry loop, and it keys that off the
**response body**, not the origin (`ShouldRetryDtxRequest`, gated by
`hasResponseBody = cosmosResponseMessage?.Content != null`):

* **Body-bearing `408`** — a *semantic* coordinator response whose JSON carries per-op detail
  and `isRetriable`. `ShouldRetryDtxRequest` returns `NoRetry()` so the **outer** committer
  loop owns it (reading `isRetriable` from the body). This prevents inner × outer amplification.
* **Bodyless `408`** — an *envelope* failure. This covers both a transport-generated timeout
  (the request may never have reached the coordinator) **and** a coordinator that returns a
  bare `408` envelope after exhausting its own internal retries (see the write-transaction
  table above). Either way it is handled inline by the **inner** `RetryDtxWithBudget`
  (`MaxDtxRetryCount`), because the idempotency token makes the blind replay safe.

For **read** transactions the retriable envelope codes are `408` (Phase 2 failure / unconfirmed
snapshot) and `449` (Phase 1 in-flight exhaustion); `200`/`304`/`207`/`404` are terminal and are
not retried by the outer loop. Because a `404` (or any other hard per-op code) fails the whole
read with no retries, a missing document surfaces immediately — it is **not** a successful per-op
read outcome (see [§6 — read transactions](#aggregate-status-codes--read-transactions)).

### A `449`'s retry owner depends on body, not sub-status

The two retry layers classify a `449` off **different** signals, and a correct port
must preserve that split rather than collapse it into a single rule:

* The **outer** committer loop is sub-status-agnostic — it retries any response whose
  **body** sets `isRetriable: true` (`DistributedTransactionCommitter.cs` line 116,
  reading the top-level flag parsed in `DistributedTransactionResponse.cs` lines
  378–382).
* The **inner** `ClientRetryPolicy` classifier only treats a `449` as
  coordinator-retriable when its sub-status is exactly `5352`
  (`DtcCoordinatorRaceConflict`, `ClientRetryPolicy.cs` line 877). A `449` with any
  other sub-status is not coordinator-retriable there, so it falls through to the
  catch-all (`return null`) and is terminal to the inner classifier.

This split is safe **only because** it mirrors the coordinator's actual `449`
vocabulary:

* A **bodyless** `449` is *always* the coordinator race conflict and *always* carries
  sub-status `5352` (see the
  [write-transaction table](#aggregate-status-codes--write-transactions)), so the inner
  `5352` gate matches the coordinator's entire bodyless-`449` surface.
* A **body-bearing** `449` — e.g. a read transaction's Phase 1 in-flight exhaustion,
  whose aggregate sub-status is `0`/`Unknown`, **not** `5352` (§6: *"the aggregate
  sub-status is always `0`"*) — carries `isRetriable: true`, so the **outer** loop
  retries it regardless of sub-status.

**Consequence for a read `449`/`Unknown`:** it is **not** dropped. Even though its
aggregate sub-status is `0` (which fails the inner `5352` gate), it is body-bearing
with `isRetriable: true`, so the outer loop retries it. The inner `5352` gate only ever
fires for the bodyless coordinator-race `449`.

---

## Sequence overview

```
Application
   │  DistributedWriteTransaction.CommitAsync / DistributedReadTransaction
   ▼
DistributedTransactionCommitter.CommitTransactionAsync
   │  1. Resolve collection RIDs
   │  2. DistributedTransactionServerRequest.CreateAsync  → JSON body { "operations": [...] }
   ▼
ExecuteCommitWithRetryAsync ──► ExecuteCommitAsync
   │  POST /operations/dtc  (UseGatewayMode = true)
   │  headers: x-ms-cosmos-operation-type / -resource-type / -idempotency-token
   │  body:    application/json
   ▼
Cosmos DB service (gateway → Distributed Transaction Coordinator)
   │  executes the 2-phase commit, returns aggregate + per-op JSON
   ▼
DistributedTransactionResponse.FromResponseMessageAsync
   │  status / substatus / RU / activityId  ← response headers
   │  isRetriable / diagnosticString / per-op results ← JSON body
   │  MergeSessionTokens → SessionContainer
   ▼
DistributedTransactionResponse  (indexable by operation, with RequestCharge, Diagnostics)
```
