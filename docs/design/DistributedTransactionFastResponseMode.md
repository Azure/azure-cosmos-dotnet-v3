# Distributed Write Transactions: Fast Response Protocol and .NET SDK Contract

## 1. Purpose

This document proposes the API specification for how a `CosmosClient` instance interacts with the Distributed Transactions Coordinator when a write DTX is issued in Fast Response mode.

Fast Response mode lets a distributed write transaction acknowledge back to `CosmosClient` as soon as the coordinator has durably completed the Prepare phase (durable Phase 1), rather than waiting for the terminal Commit/Abort of Phase 2.


## 2. Public API Composability

The goal of composability is to let one public surface disambiguate between the **two modes of a write distributed transaction** — `Standard` and `FastResponse` — without forking the commit API. The commit call, `CommitTransactionAsync`, is the same in both modes; the envelope it returns, `DistributedTransactionResponse`, tells the caller which mode the coordinator applied and how far the transaction has progressed. Reading a transaction's status later is a separate, standalone API (`GetTransactionStatusAsync`, section 4) with **its own return type**, rather than a variant of commit.

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
- `FastResponse` — the commit call may return after durable Phase 1 with `202/InProgress`; the terminal outcome is resolved later.

The mode also appears in the response payload as a top-level `responseMode` field, alongside the transaction status and the per-operation results (`operationResponses`):

```json
{
  "transactionStatus": "InProgress",
  "responseMode": "FastResponse",
  "idempotencyToken": "8a9d6f61-f0fb-4ee5-84de-7a7de7fbcf4a",
  "statusCode": 202,
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


### 2.2 Transaction status is its own API with its own return type

Determining the final outcome of a Fast Response transaction is not a property of the commit envelope — it is a distinct read operation with a dedicated return type. `GetTransactionStatusAsync` performs a single read-only lookup of the coordinator ledger for one idempotency token and returns a `DistributedTransactionStatusResponse` (not a `DistributedTransactionResponse`). This keeps the two shapes cleanly separated: the commit envelope enumerates per-operation results, whereas the status response never carries operation results, ETags, or session tokens.

```csharp
public class DistributedTransactionStatusResponse
{
    public DistributedTransactionStatus TransactionStatus { get; }
    public DistributedTransactionResponseMode ResponseMode { get; }
    public Guid IdempotencyToken { get; }

    // Status/substatus of the transaction itself (its outcome).
    public HttpStatusCode TransactionStatusCode { get; }
    public int TransactionSubStatusCode { get; }

    // Status/substatus of the transaction-status read operation.
    public HttpStatusCode StatusCode { get; }
    public int SubStatusCode { get; }

    public CosmosDiagnostics Diagnostics { get; }
}
```

- `StatusCode` / `SubStatusCode` — the HTTP status and substatus of the transaction-status read operation itself (a known status lookup is HTTP `200`).
- `TransactionStatusCode` / `TransactionSubStatusCode` — the transaction's own outcome code and substatus (for example `202` for `InProgress`, `452/5421` for `Aborted`, `200` for `Committed`).
- `TransactionStatus` — the coordinator's authoritative view: `InProgress`, `Aborted`, or `Committed`.

These two pairs are independent: the read operation can succeed (`StatusCode == 200`) while reporting a transaction that aborted (`TransactionStatusCode == 452`).

Because the status API has its own return type, callers never infer commitment from an HTTP code or from an enumerable of operation results, and the commit path is free to keep its result-oriented shape unchanged.


## 3. Idempotency Token Contract

Each distributed transaction is identified by an idempotency token (`DistributedTransaction.IdempotencyToken`), which is already implemented in `CosmosClient`. This document's focus is how that token is used to track a transaction's outcome: the customer passes it to `GetTransactionStatusAsync` (section 4) to look up the transaction's current status.

## 4. Transaction-Status REST Contract

### 4.1 Purpose

This operation reads the coordinator ledger state for one idempotency token. It does not wait for a terminal state and does not poll internally.

### 4.2 Public state model

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

The service MUST NOT expose any other public state. The collapse from internal coordinator states to these three public states MAY be performed by either the service or `CosmosClient`.

| Public state | Internal states represented | Terminal | `isRetriable` |
|---|---|---:|---|
| `InProgress` | `Preparing`, `Committing` | No | `false` on Fast Response acceptance and status lookup; `true` only on a commit response that instructs same-token replay |
| `Aborted` | `Aborting`, `Aborted` | Yes | `true` only when complete rollback is proven and identical operations are safe to submit under a new token |
| `Committed` | `Committed` | Yes | Always `false` |

Terminal-state rules:

- A token that has returned `Committed` MUST always return `Committed`.
- A token that has returned `Aborted` MUST always return `Aborted`.
- A terminal response MUST retain the terminal substatus and `isRetriable` classification for the token's retention lifetime.
- `InProgress` MAY later become `Committed` or `Aborted`.
- `Committed` MUST NOT become `Aborted`.
- `Aborted` MUST NOT become `Committed`.

### 4.3 Request

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

- Authorization uses the same semantics as a read item operation.
- Master-key and supported account-level Microsoft Entra data-plane authorization are accepted.
- The front end authorizes operation `ReadDistributedTransactionStatus` on the account-level `DistributedTransactionBatch` resource.
- Resource-token authorization is not supported by this v1 status API because one token may represent operations across multiple databases and containers.
- Unsupported resource-token authorization returns `403`.
- A valid idempotency token does not bypass authorization.
- Authentication and authorization happen before ledger lookup.
- Unauthorized callers receive `401` or `403` without revealing whether the token exists.

The status read MUST be routed to the account's primary (write) region — or, for multi-writer accounts, the hub region — because that is where the coordinator ledger is authoritative. This matches the routing of the distributed-transaction commit operation.

### 4.4 Success response body

Every known token returns HTTP `200` (the status of the read operation itself) with a body carrying the transaction's own outcome in `statusCode`/`subStatusCode`:

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

The response therefore carries two independent status pairs: the transaction-status read operation's HTTP `200`/`0`, and the transaction's own `statusCode`/`subStatusCode` (`202` for `InProgress`, `452/5421` for `Aborted`, `200` for `Committed`).

## 5. Aborted-Transaction Retry Model

### 5.1 Coordinator signals retriability

When a transaction terminates in `Aborted`, the coordinator tells `CosmosClient` whether the same operations may be safely resubmitted by setting `isRetriable` on the aborted response:

- `isRetriable: true` — the abort is retriable. The coordinator has proven the transaction is durably `Aborted` and fully rolled back, so resubmitting the identical operations as a new logical attempt cannot duplicate a committed write.
- `isRetriable: false` — the abort is terminal for these operations. `CosmosClient` MUST NOT resubmit.

`isRetriable` MUST be interpreted together with `transactionStatus: Aborted`; it is never acted on alone.

### 5.2 Client retries with a new idempotency token

On a retriable abort, `CosmosClient` MAY retry the transaction. Each retry is a new logical attempt and MUST use a **new idempotency token** (new-token resubmission):

- The same serialized operation bytes are reused for every retry.
- The prior token remains terminally `Aborted` and stays queryable via the status API.
- The new token is published on `DistributedTransaction.IdempotencyToken` only at the dispatch handoff.

The old token MUST NOT be replayed after a retriable abort — it is terminally aborted.

### 5.3 Retry bounds

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

### 5.4 Cancellation preserves the latest token

Cancellation is graceful: it takes effect only at a retry boundary, between attempts, never mid-dispatch. The SDK checks the supplied `CancellationToken` before starting the next attempt and before/after each backoff delay, but once an attempt has entered the dispatch path it is allowed to finish. This makes token publication deterministic and lets cancellation preserve the latest token reliably.

Even when the `CancellationToken` has been triggered, `CosmosClient` MUST expose the latest token that reached the dispatch boundary on `DistributedTransaction.IdempotencyToken`, so the customer can still query the transaction's status after cancellation.

- Cancellation observed before the next attempt's dispatch handoff leaves the previously published token as the latest.
- Because cancellation only fires between attempts, the latest published token always corresponds to a completed dispatch; the customer can query its status (which may legitimately return `404` if that request never reached the service).
- Cancellation only stops local SDK work; it never sends an abort and never changes server state.

## 6. CosmosClient Interaction Sequence

The end-to-end flow a customer follows with a Fast Response transaction:


### 6.1 Example: read/query in conjunction with the status API

A read or query that depends on a Fast Response transaction should run only after the status API confirms the transaction `Committed`:

```csharp
DistributedTransaction transaction = container.CreateDistributedTransaction();
transaction.CreateItem(new Order { Id = "order-1", CustomerId = "cust-1" });

DistributedTransactionResponse commit = await transaction.CommitTransactionAsync(cancellationToken);
Guid idempotencyToken = transaction.IdempotencyToken;

if (commit.ResponseMode == DistributedTransactionResponseMode.FastResponse)
{
    // Poll the standalone status API until the transaction reaches a terminal state.
    DistributedTransactionStatusResponse status;
    do
    {
        status = await cosmosClient.GetTransactionStatusAsync(idempotencyToken, cancellationToken);
        if (status.TransactionStatus == DistributedTransactionStatus.InProgress)
        {
            await Task.Delay(pollDelay, cancellationToken);
        }
    }
    while (status.TransactionStatus == DistributedTransactionStatus.InProgress);

    if (status.TransactionStatus == DistributedTransactionStatus.Committed)
    {
        // The transaction is durable; a dependent point read is now safe.
        ItemResponse<Order> read = await container.ReadItemAsync<Order>(
            id: "order-1",
            partitionKey: new PartitionKey("cust-1"),
            cancellationToken: cancellationToken);

        // ...or a dependent query.
        using FeedIterator<Order> iterator = container.GetItemQueryIterator<Order>(
            new QueryDefinition("SELECT * FROM c WHERE c.customerId = @cid")
                .WithParameter("@cid", "cust-1"));
        while (iterator.HasMoreResults)
        {
            FeedResponse<Order> page = await iterator.ReadNextAsync(cancellationToken);
            // process page
        }
    }
}
```

## 7. FastResponse-Mode Response Handling

In FastResponse mode, `CosmosClient` deserializes the response and inspects only two fields to drive its behavior:

- the transaction's **status code**, and
- the **`isRetriable`** flag.

Together these determine whether the SDK retries the transaction (retriable `Aborted`) or returns the outcome to the caller. No other part of the response body is interpreted for control flow.

`CosmosClient` does **not** extract a session token or an ETag from a FastResponse acknowledgment. As a result:

- **No consistency guarantees** — a FastResponse acknowledgment does not establish session read-your-write state; a subsequent Session-consistency read acquires session state normally from the service.
- **No optimistic concurrency control (OCC) guarantees** — no per-operation ETag is surfaced, so a FastResponse acknowledgment cannot be used as the basis for a conditional (`If-Match`) follow-up write.

To obtain operation results, ETags, or session tokens, use a `Standard`-mode commit, whose terminal response carries the full per-operation results.

> Note: Because FastResponse provides no consistency or OCC guarantees, it is safe to enable only on accounts configured for Eventual consistency. Accounts relying on stronger consistency (for example Session or Bounded Staleness read-your-write semantics) should use `Standard` mode.
