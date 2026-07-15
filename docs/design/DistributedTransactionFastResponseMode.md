# Distributed Write Transactions: Fast Response Protocol and .NET SDK Contract

## 1. Purpose

This document proposes the API specification for how a `CosmosClient` instance interacts with the Distributed Transactions Coordinator when a write DTX is issued in Fast Response mode.

Fast Response mode lets a distributed write transaction acknowledge back to `CosmosClient` as soon as the coordinator has durably completed the Prepare phase (durable Phase 1), rather than waiting for the terminal Commit/Abort of Phase 2.


## 2. Public API Composability

The goal of composability is to let one public surface disambiguate between the **two modes of a write distributed transaction** — `Standard` and `FastResponse` — without forking the commit API. The commit call, `ExecuteTransactionAsync`, is the same in both modes; the envelope it returns, `DistributedTransactionResponse`, tells the caller which mode the coordinator applied and how far the transaction has progressed.

### 2.1 One envelope disambiguates the two modes

`DistributedTransactionResponse` reports the mode the coordinator recorded for this logical attempt:

```csharp
public enum DistributedTransactionResponseMode
{
    Standard = 0,
    FastResponse = 1,
}
```

- `Standard` — the commit call waits for a terminal `Committed`/`Aborted`; the envelope's operation results are fully populated and the transaction is already resolved when the call returns.
- `FastResponse` — the commit call may return after durable Phase 1 while the transaction is still `InProgress`; the terminal outcome is resolved later.

The mode also appears in the response payload as a top-level `responseMode` field, alongside the per-operation results (`operationResponses`):

```json
{
  "responseMode": "FastResponse",
  "idempotencyToken": "8a9d6f61-f0fb-4ee5-84de-7a7de7fbcf4a",
  "statusCode": 200,
  "subStatusCode": 0,
  "isRetriable": false,
  "operationResponses": [
    {
      "index": 0,
      "statusCode": 201,
      "subStatusCode": 0,
      "Etag": "\"00000000-0000-0000-0000-000000000000\"",
      "requestCharge": 5.71,
      "sessionToken": "0:-1#12",
      "partitionKeyRangeId": "0",
      "resourceBody": {
        "id": "order-1",
        "customerId": "cust-1"
      }
    }
  ]
}
```


## 3. Idempotency Token Contract

Each distributed transaction is identified by an idempotency token (`DistributedTransaction.IdempotencyToken`), which is already implemented in `CosmosClient`. This document's focus is how that token identifies a logical attempt: on a retriable abort, `CosmosClient` resubmits the same operations under a **new** idempotency token (section 4).

## 4. Aborted-Transaction Retry Model

### 4.1 Coordinator signals retriability

When a transaction terminates in `Aborted`, the coordinator tells `CosmosClient` whether the same operations may be safely resubmitted by setting `isRetriable` on the aborted response:

- `isRetriable: true` — the abort is retriable. The coordinator has proven the transaction is durably `Aborted` and fully rolled back, so resubmitting the identical operations as a new logical attempt cannot duplicate a committed write.
- `isRetriable: false` — the abort is terminal for these operations. `CosmosClient` MUST NOT resubmit.

`isRetriable` MUST be interpreted together with `transactionStatus: Aborted`; it is never acted on alone.

### 4.2 Client retries with a new idempotency token

On a retriable abort, `CosmosClient` MAY retry the transaction. Each retry is a new logical attempt and MUST use a **new idempotency token** (new-token resubmission):

- The same serialized operation bytes are reused for every retry.
- The prior token remains terminally `Aborted`.
- The new token is published on `DistributedTransaction.IdempotencyToken` only at the dispatch handoff.

The old token MUST NOT be replayed after a retriable abort — it is terminally aborted.

### 4.3 Retry bounds

`CosmosClient` retries a retriable abort up to a bounded limit, expressed as **both** a maximum retry count and a maximum cumulative duration. Retrying stops as soon as either bound is reached:

- Between retries the SDK waits a bounded backoff delay and honors cancellation.
- When the retry budget is exhausted, the SDK returns the last `Aborted + isRetriable: true` response unchanged.

These bounds are tunable at the client level through `CosmosClientOptions`, mirroring the throttling (429) retry options (`MaxRetryAttemptsOnRateLimitedRequests` / `MaxRetryWaitTimeOnRateLimitedRequests`):

```csharp
public class CosmosClientOptions
{
    // Maximum number of new-token retries on retriable aborted transactions.
    // Analogous to MaxRetryAttemptsOnRateLimitedRequests.
    public int? MaxRetryAttemptsOnAbortedTransactions { get; set; }

    // Maximum cumulative wait time across all abort retries.
    // Analogous to MaxRetryWaitTimeOnRateLimitedRequests.
    public TimeSpan? MaxRetryWaitTimeOnAbortedTransactions { get; set; }
}
```

- `MaxRetryAttemptsOnAbortedTransactions` — caps the retry count; `0` disables automatic abort retries.
- `MaxRetryWaitTimeOnAbortedTransactions` — caps the total time spent retrying, including backoff delays.
- When unset, the SDK applies its default bounds.

### 4.4 Cancellation preserves the latest token

Cancellation is graceful: it takes effect only at a retry boundary, between attempts, never mid-dispatch. The SDK checks the supplied `CancellationToken` before starting the next attempt and before/after each backoff delay, but once an attempt has entered the dispatch path it is allowed to finish. This makes token publication deterministic and lets cancellation preserve the latest token reliably.

Even when the `CancellationToken` has been triggered, `CosmosClient` MUST expose the latest token that reached the dispatch boundary on `DistributedTransaction.IdempotencyToken`, so the latest attempt remains identifiable after cancellation.

- Cancellation observed before the next attempt's dispatch handoff leaves the previously published token as the latest.
- Because cancellation only fires between attempts, the latest published token always corresponds to a completed dispatch.
- Cancellation only stops local SDK work; it never sends an abort and never changes server state.

## 5. FastResponse-Mode Response Handling

In FastResponse mode, `CosmosClient` deserializes the response and inspects only two fields to drive its behavior:

- the transaction's **status code**, and
- the **`isRetriable`** flag.

Together these determine whether the SDK retries the transaction (retriable `Aborted`) or returns the outcome to the caller. No other part of the response body is interpreted for control flow.

`CosmosClient` does **not** extract a session token or an ETag from a FastResponse acknowledgment. As a result:

- **No consistency guarantees** — a FastResponse acknowledgment does not establish session read-your-write state; a subsequent Session-consistency read acquires session state normally from the service.
- **No optimistic concurrency control (OCC) guarantees** — no per-operation ETag is surfaced, so a FastResponse acknowledgment cannot be used as the basis for a conditional (`If-Match`) follow-up write.

To obtain operation results, ETags, or session tokens, use a `Standard`-mode commit, whose terminal response carries the full per-operation results.

> Note: Because FastResponse provides no consistency or OCC guarantees, it is safe to enable only on accounts configured for Eventual consistency. Accounts relying on stronger consistency (for example Session or Bounded Staleness read-your-write semantics) should use `Standard` mode.
