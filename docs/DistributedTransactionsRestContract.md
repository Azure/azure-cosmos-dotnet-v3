# Distributed Transactions (DTC) — REST Contract

This document describes the wire contract used by the .NET SDK to execute a **Distributed
Transaction** (DTC) against the Cosmos DB service. A distributed transaction lets a client
commit a batch of point operations that span **multiple containers** atomically, coordinated
server-side by the Distributed Transaction Coordinator (DTC).

**Write** transactions (the SDK issues `CommitDistributedTransaction`) and **read**
transactions (`Read`) share the same endpoint and envelope but use **different**
response-code contracts — reads take a consistent multi-container snapshot and can return
`200`/`304`/`207`/`404`/`449`/`408` (see [§6 — read transactions](#aggregate-status-codes--read-transactions)).
CosmosClient **never** issues `AbortDistributedTransaction`: abort is decided and driven
server-side by the coordinator, and the client observes an aborted transaction only as the
`452` terminal response (§5).

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

Set by `DistributedTransactionCommitter.EnrichRequestMessage`:

| Header | Constant | Example value | Purpose |
|---|---|---|---|
| `x-ms-cosmos-operation-type` | `HttpHeaders.OperationType` | `CommitDistributedTransaction` | The transaction action (commit / abort / read) |
| `x-ms-cosmos-resource-type` | `HttpHeaders.ResourceType` | `DistributedTransactionBatch` | Resource type |
| `x-ms-cosmos-idempotency-token` | `HttpHeaders.IdempotencyToken` | `<GUID>` | Stable across retries; the server uses it to deduplicate |
| `x-ms-activity-id` | `HttpHeaders.ActivityId` | `<GUID>` | Request correlation |
| `Authorization` | — | — | Standard Cosmos auth (master key / RBAC token) |

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
      "id":                   "string",   // document id
      "collectionResourceId": "string",   // emitted when present — container RID
      "databaseResourceId":   "string",   // emitted when present — database RID
      "partitionKey":         <json>,     // emitted when present — raw JSON value
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
* `collectionResourceId`, `databaseResourceId`, and `partitionKey` are emitted only when the SDK
  has resolved them for the operation; they are omitted otherwise (all three are serialized
  conditionally by `DistributedTransactionSerializer`).
* `resourceBody` is emitted only for operations that carry a document payload (writes).
* `ifMatch` and `ifNoneMatch` are both optional and emitted only when the operation specifies the
  corresponding condition. The request-side conditional-ETag field is named **`ifMatch`** (contrast
  with the response, which uses `eTag` — see §6).

## 5. Response headers

| Header | Example value | Surfaced on SDK as |
|---|---|---|
| `x-ms-cosmos-idempotency-token` | `<GUID>` | `DistributedTransactionResponse.IdempotencyToken` (echoes the request) |
| `x-ms-activity-id` | `<GUID>` | `DistributedTransactionResponse.ActivityId` |
| *(HTTP status line)* | `200` | `DistributedTransactionResponse.StatusCode` |
| `x-ms-substatus` | `0` | `DistributedTransactionResponse.SubStatusCode` |
| `x-ms-request-charge` | `12.34` | `DistributedTransactionResponse.RequestCharge` (total RU across all phases) |
| `x-ms-retry-after-ms` | `1000` | `Headers.RetryAfter` — used as the retry backoff hint |

> **Activity-id caveat:** when the request is routed through the thin-client proxy, the
> `x-ms-activity-id` on the response may differ from the activity-id the server logged
> internally. For correlating a specific transaction across client and server traces, prefer the
> **idempotency token**, which is stable and reliably echoed.

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
      "eTag":                "string",
      "requestCharge":       2.0,
      "isRetriable":         false,
      "partitionKeyRangeId": "string",           // optional
      "localLsn":            123,                 // optional
      "resourceBody":        { }                  // optional — nested JSON (reads / created docs)
    }
    // ... one entry per operation, keyed by `index`
  ]
}
```

Notes:
* The response-side ETag field is named **`eTag`**. The SDK reads response fields
  **case-insensitively** (`DistributedTransactionOperationResult.TryGetProperty` uses
  `StringComparison.OrdinalIgnoreCase`), so the request/response ETag casing difference
  (`ifMatch` vs `eTag` vs the SDK constant `Etag`) is harmless.
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
contract from writes. Reworked server-side in
[CosmosDB PR 2154806](https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB/pullrequest/2154806)
("[DTC] Rework distributed read transaction response codes").

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
> per-op success; PR 2154806 removed it from the success set, which is now `{200, 304}`.)

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
   transport failures (connection, address resolution, region failover) its DTX
   classifier (`ShouldRetryDtxRequest`) also owns the empty-body coordinator and
   infrastructure codes, splitting them across three distinct budgets (below). It
   defers to the outer loop for any body-bearing coordinator code to avoid
   inner × outer retry amplification.

### Retry routing by status code and body

| Envelope | Sub-status | Body? | Owner | Budget |
|---|---|---|---|---|
| `408` / `449` | `5352` (`449`) | **yes** | Outer committer loop | 10 attempts / 120 s |
| `429` | `3200` | **yes** | Outer committer loop | 10 attempts / 120 s |
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
* the **cumulative planned delay** would exceed `MaxCumulativeRetryDelay` (**120 s**).

Each backoff delay is `max(serverHint, computedBackoff)`, where `serverHint` is the
`x-ms-retry-after-ms` header (used only when it exceeds the local delay) and `computedBackoff` is a
bounded exponential — `retryBaseDelay · 2^min(attempt, RetryMaxExponent)` with **±25 % jitter**
(`DistributedTransactionRetryHelpers.ComputeBackoff`). With the default 1 s base and
`RetryMaxExponent = 5` (a ~32 s per-attempt ceiling before jitter), the 120 s cumulative budget is
normally the binding limit — roughly 7–8 retries — rather than the 10-attempt cap.

**Inner (bodyless) budgets.** A bodyless coordinator code (`408`, or `449`/`5352`) retries
via `RetryDtxWithBudget` up to `MaxDtxRetryCount` (**10**) attempts, each after the server's
`Retry-After` if present else a flat **1 s** (`RetryIntervalInMS`). A bodyless infrastructure
failure (`500` with `5411`–`5413` — ledger / account-config / dispatch) uses a separate,
tighter budget: `MaxDtxInfraFailureRetryCount` (**9**) attempts with an exponential backoff of
**100 ms → 5 s** (`ComputeBackoff`, max exponent 6). A bodyless `429`/`3200` is not handled
by either DTX budget — the classifier returns control to the shared `ResourceThrottleRetryPolicy`.

For **read** transactions the retriable envelope codes are `408` (Phase 2 failure / unconfirmed
snapshot) and `449` (Phase 1 in-flight exhaustion); `200`/`304`/`207`/`404` are terminal and are
not retried by the outer loop. Because a `404` (or any other hard per-op code) fails the whole
read with no retries, a missing document surfaces immediately — it is **not** a successful per-op
read outcome (see [§6 — read transactions](#aggregate-status-codes--read-transactions)).

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
