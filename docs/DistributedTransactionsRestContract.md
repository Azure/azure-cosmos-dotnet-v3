# Distributed Transactions (DTC) — REST Contract

This document describes the wire contract used by the .NET SDK to execute a **Distributed
Transaction** (DTC) against the Cosmos DB service. A distributed transaction lets a client
commit a batch of point operations that span **multiple containers** atomically, coordinated
server-side by the Distributed Transaction Coordinator (DTC).

**Write** transactions (`CommitDistributedTransaction` / `AbortDistributedTransaction`) and
**read** transactions (`Read`) share the same endpoint and envelope but use **different**
response-code contracts — reads take a consistent multi-container snapshot and can return
`200`/`304`/`207`/`404`/`449`/`408` (see [§6 — read transactions](#aggregate-status-codes--read-transactions)).

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
| **Operation type** | Carried in a **header**, not the verb — one of `CommitDistributedTransaction`, `AbortDistributedTransaction`, `Read` |
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
      "collectionResourceId": "string",   // required — container RID
      "databaseResourceId":   "string",   // required — database RID
      "partitionKey":         <json>,     // required — raw JSON value
      "index":                0,          // required — uint32, the operation's ordinal in the batch
      "resourceBody":         { },        // optional — nested JSON document (writes only)
      "sessionToken":         "string",   // optional
      "ifMatch":              "string",   // optional — ETag for conditional operations
      "operationType":        "Create",   // Create | Replace | Delete | Upsert | Patch | Read
      "resourceType":         "Document"
    }
    // ... one entry per operation in the transaction
  ]
}
```

Notes:
* The `operations` array must be **non-empty**.
* `resourceBody` is emitted only for operations that carry a document payload (writes).
* The request-side conditional-ETag field is named **`ifMatch`** (contrast with the response,
  which uses `eTag` — see §6).

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

### Aggregate status codes — write transactions (commit / abort)

These apply when the operation type is `CommitDistributedTransaction` /
`AbortDistributedTransaction`.

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

Two layers of retry apply:

1. **Inner (transport):** the standard `ClientRetryPolicy` handles envelope/network failures
   (connection, address resolution, region failover) with an empty body.
2. **Outer (semantic):** `DistributedTransactionCommitter.ExecuteCommitWithRetryAsync` retries
   when the JSON body sets `isRetriable: true` (e.g., in-progress `408`, coordinator race `449`,
   throttle `429`). It retries up to `MaxIsRetriableRetryCount` (10) attempts with exponential
   backoff, honoring the server's `x-ms-retry-after-ms` hint when larger than the computed delay,
   and **reuses the same idempotency token** on every attempt.

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
