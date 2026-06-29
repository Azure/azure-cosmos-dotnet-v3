# Distributed Transactions API Review
## Azure Cosmos DB .NET SDK v3

**Document status:** Pre-announcement review draft  
**Namespace:** `Microsoft.Azure.Cosmos`

> **Changes since last draft** — three in-flight PRs address gaps identified in this review:
> - **PR #5884** (`users/Meghana-Palaparthi/dtx_double_commit_guardrail`): Adds single-use commit guardrail — `CommitTransactionAsync` throws `InvalidOperationException` on any second call.
> - **PR #5885** (`users/Meghana-Palaparthi/dtx_gap_fixes`): Adds `GetOperationResultAtIndex<T>()` typed deserialization, per-operation `SessionToken` on request options, snapshot-isolation doc on read transactions, and `ResourceStream` ownership remarks.
> - **PR #5869** (`users/Meghana-Palaparthi/dtx_consume_server_diagnostics`): Adds `Diagnostics` (`CosmosDiagnostics`) and `DiagnosticString` (coordinator-side) on `DistributedTransactionResponse`; integrates OpenTelemetry tracing for commits.

---

## 1. Executive Summary

The Distributed Transaction (DTX) API extends Azure Cosmos DB's atomicity guarantees beyond the single-partition boundary. While `TransactionalBatch` provides ACID semantics for up to 100 operations on a single partition key within a single container, the new `DistributedWriteTransaction` and `DistributedReadTransaction` APIs allow **multi-partition, multi-container atomic operations** across the same Cosmos DB account.

---

## 2. Comparison with `TransactionalBatch`

| Dimension | `TransactionalBatch` | `DistributedTransaction` |
|---|---|---|
| Partition scope | Single partition key | Multiple partition keys |
| Container scope | Single container | Multiple containers (same account) |
| Write operations | Create, Read, Replace, Upsert, Delete, Patch | Create, Replace, Upsert, Delete, Patch |
| Read operations | ReadItem (within same partition batch) | Dedicated `DistributedReadTransaction` |
| Max operations | 100 | Not documented |
| Commit | `ExecuteAsync()` | `CommitTransactionAsync()` |
| Idempotency token | Not exposed | Auto-generated `Guid`, exposed on response |
| Custom `CosmosSerializer` on results | Yes — `GetOperationResultAtIndex<T>()` | Yes — `GetOperationResultAtIndex<T>()` added in PR #5885 |
| Response type | `TransactionalBatchResponse` | `DistributedTransactionResponse` |

---

## 3. Type Hierarchy

```
DistributedTransaction  (abstract)
│   CommitTransactionAsync(CancellationToken) → Task<DistributedTransactionResponse>
│
├── DistributedWriteTransaction  (abstract)
│       CreateItem<T>(...)      → DistributedWriteTransaction   // fluent builder
│       CreateItemStream(...)   → DistributedWriteTransaction
│       ReplaceItem<T>(...)     → DistributedWriteTransaction
│       ReplaceItemStream(...)  → DistributedWriteTransaction
│       DeleteItem(...)         → DistributedWriteTransaction
│       PatchItem(...)          → DistributedWriteTransaction
│       PatchItemStream(...)    → DistributedWriteTransaction
│       UpsertItem<T>(...)      → DistributedWriteTransaction
│       UpsertItemStream(...)   → DistributedWriteTransaction
│
└── DistributedReadTransaction  (abstract)
        ReadItem(...)           → DistributedReadTransaction    // fluent builder

DistributedTransactionResponse  : IReadOnlyList<DistributedTransactionOperationResult>, IDisposable
DistributedTransactionOperationResult
DistributedTransactionRequestOptions  : RequestOptions

CosmosClient
    CreateDistributedWriteTransaction() → DistributedWriteTransaction
    CreateDistributedReadTransaction()  → DistributedReadTransaction
```

---

## 4. API Specification

### 4.1 `CosmosClient` — factory methods

```csharp
// Creates a write transaction that can span multiple partitions and containers.
public virtual DistributedWriteTransaction CreateDistributedWriteTransaction();

// Creates a read transaction that reads atomically across multiple partitions and containers.
public virtual DistributedReadTransaction CreateDistributedReadTransaction();
```
---

### 4.2 `DistributedTransaction` — base class

```csharp
public abstract class DistributedTransaction
{
    /// <summary>Commits all buffered operations as a single atomic transaction.</summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if CommitTransactionAsync has already been called on this instance.
    ///   Even a failed or cancelled call permanently consumes the instance — construct
    ///   a new transaction for each retry attempt. (PR #5884)
    /// </exception>
    public abstract Task<DistributedTransactionResponse> CommitTransactionAsync(
        CancellationToken cancellationToken = default);
}
```
---

### 4.3 `DistributedWriteTransaction` — write operations

All methods use a **fluent builder** pattern (each returns `this`). Operations are buffered and sent together at `CommitTransactionAsync`.

```csharp
public abstract class DistributedWriteTransaction : DistributedTransaction
{
    // Typed variants — SDK serializes T using the registered CosmosSerializer
    public abstract DistributedWriteTransaction CreateItem<T>(
        string database, string collection,
        PartitionKey partitionKey, string id, T resource,
        DistributedTransactionRequestOptions requestOptions = null);

    public abstract DistributedWriteTransaction ReplaceItem<T>(
        string database, string collection,
        PartitionKey partitionKey, string id, T resource,
        DistributedTransactionRequestOptions requestOptions = null);

    public abstract DistributedWriteTransaction UpsertItem<T>(
        string database, string collection,
        PartitionKey partitionKey, string id, T resource,
        DistributedTransactionRequestOptions requestOptions = null);

    public abstract DistributedWriteTransaction PatchItem(
        string database, string collection,
        PartitionKey partitionKey, string id,
        IReadOnlyList<PatchOperation> patchOperations,
        DistributedTransactionRequestOptions requestOptions = null);

    public abstract DistributedWriteTransaction DeleteItem(
        string database, string collection,
        PartitionKey partitionKey, string id,
        DistributedTransactionRequestOptions requestOptions = null);

    // Stream variants — caller provides pre-serialized JSON; SDK does not transform bytes
    public abstract DistributedWriteTransaction CreateItemStream(
        string database, string collection,
        PartitionKey partitionKey, string id, Stream streamPayload,
        DistributedTransactionRequestOptions requestOptions = null);

    public abstract DistributedWriteTransaction ReplaceItemStream(
        string database, string collection,
        PartitionKey partitionKey, string id, Stream streamPayload,
        DistributedTransactionRequestOptions requestOptions = null);

    public abstract DistributedWriteTransaction UpsertItemStream(
        string database, string collection,
        PartitionKey partitionKey, string id, Stream streamPayload,
        DistributedTransactionRequestOptions requestOptions = null);

    public abstract DistributedWriteTransaction PatchItemStream(
        string database, string collection,
        PartitionKey partitionKey, string id, Stream streamPayload,
        DistributedTransactionRequestOptions requestOptions = null);
}
```

---

### 4.4 `DistributedReadTransaction` — read operations

```csharp
public abstract class DistributedReadTransaction : DistributedTransaction
{
    // Adds a point-read to the transaction.
    // All reads execute under snapshot isolation — results reflect a consistent
    // point in time across all participating partitions. (PR #5885)
    public abstract DistributedReadTransaction ReadItem(
        string database, string collection,
        PartitionKey partitionKey, string id,
        DistributedTransactionRequestOptions requestOptions = null);
}
```
---

### 4.5 `DistributedTransactionRequestOptions`

```csharp
public class DistributedTransactionRequestOptions : RequestOptions
{
    // Inherited: IfMatchEtag, IfNoneMatchEtag, Properties

    // Per-operation session token in {partitionKeyRangeId}:{lsn} format. (PR #5885)
    // Because a distributed transaction spans multiple partitions, each operation
    // supplies its own token rather than a single commit-level token.
    // Obtain from a prior DistributedTransactionOperationResult.SessionToken or from
    // another SDK response that targeted the same partition.
    public string SessionToken { get; set; }
}
```

`IfMatchEtag` is wired through to the server envelope and triggers `412 Precondition Failed` if the ETag does not match.

---

### 4.6 `DistributedTransactionResponse`

```csharp
public class DistributedTransactionResponse
    : IReadOnlyList<DistributedTransactionOperationResult>, IDisposable
{
    // Zero-based indexer — matches the order operations were added to the transaction.
    public virtual DistributedTransactionOperationResult this[int index] { get; }

    public virtual Headers          Headers             { get; }
    public virtual string           ActivityId          { get; }
    public virtual double           RequestCharge       { get; }
    public virtual HttpStatusCode   StatusCode          { get; }  // promoted from failing op on 207
    public virtual bool             IsSuccessStatusCode { get; }  // 200–299
    public virtual string           ErrorMessage        { get; }
    public virtual int              Count               { get; }
    public virtual Guid             IdempotencyToken    { get; }  // auto-generated; reused across internal retries
    public virtual bool             IsRetriable         { get; }

    // Client-side diagnostics across the full retry loop (address resolution,
    // network, retries, latency). Non-null when returned from CommitTransactionAsync. (PR #5869)
    public virtual CosmosDiagnostics Diagnostics        { get; }

    // Coordinator-side diagnostic string describing the transaction outcome.
    // Surfaced inside ErrorMessage on failed responses. (PR #5869)
    public virtual string           DiagnosticString    { get; }

    public virtual IEnumerator<DistributedTransactionOperationResult> GetEnumerator();
    public void Dispose();
}
```

#### Behavior
- **Status code promotion.** For `207 Multi-Status` responses, the status code of the first failing operation (excluding `424 Failed Dependency`) is promoted to `response.StatusCode`. Operations that were aborted due to another operation's failure report `424`.
- **`IdempotencyToken`.** A `Guid` auto-generated at commit time and reused across any internal retries within the call. The token is returned for observability and correlation only — the API does not accept a caller-supplied token, so this cannot be used for exactly-once semantics across process restarts.
- **`Diagnostics`** *(PR #5869)*. Client-side `CosmosDiagnostics` covering the full retry loop and per-attempt spans. Non-null in normal usage.
- **`DiagnosticString`** *(PR #5869)*. Coordinator-side diagnostic from the server. On failed responses, it is also appended to `ErrorMessage` so customers see it in surfaced exceptions and logs without an extra accessor.
- **`Dispose()`.** Disposes the `ResourceStream` on every operation result. Do not access `ResourceStream` after the response has been disposed.

---

### 4.7 `DistributedTransactionOperationResult`

```csharp
public class DistributedTransactionOperationResult
{
    public virtual int            Index               { get; }  // zero-based
    public virtual HttpStatusCode StatusCode          { get; }
    public virtual bool           IsSuccessStatusCode { get; }
    public virtual string         ETag                { get; }
    public virtual string         SessionToken        { get; }
    public virtual string         PartitionKeyRangeId { get; }

    // Populated for Read ops; null for writes.
    // Owned by the parent DistributedTransactionResponse — do not access after Dispose(). (PR #5885)
    public virtual Stream         ResourceStream      { get; }

    public virtual double         RequestCharge       { get; }
    public virtual uint           SubStatusCodeValue  { get; }
}
```

### 4.8 `DistributedTransactionOperationResult<T>` *(PR #5885)*

```csharp
public class DistributedTransactionOperationResult<T> : DistributedTransactionOperationResult
{
    // The deserialized resource. Uses the CosmosSerializer registered in CosmosClientOptions.
    // Reading this property does NOT advance or consume ResourceStream — a snapshot is used internally.
    public virtual T Resource { get; }
}
```

Call `response.GetOperationResultAtIndex<T>(index)` to get a typed result:

```csharp
// Uses the registered CosmosSerializer — custom property mappings are honoured.
Account accountA = response.GetOperationResultAtIndex<Account>(0).Resource;
```
---

## 5. Idempotency & Single-Use Contract

### Auto-generated idempotency token
The SDK auto-generates a `Guid` idempotency token for each `CommitTransactionAsync` call and reuses it across any internal retries. The committed token is returned in `DistributedTransactionResponse.IdempotencyToken`.

**Customer implication:** Idempotency within a single `CommitTransactionAsync` call is handled automatically. The returned token is for observability and correlation only — it lets callers correlate retries and log lines with server-side state. The API does **not** accept a caller-supplied idempotency token, so there is no built-in exactly-once guarantee across process restarts. If a process crashes after the server commits but before the SDK receives the response, the application is responsible for detecting and reconciling that state (e.g., by querying expected post-commit invariants before retrying).

### Single-use commit guardrail *(PR #5884)*
`CommitTransactionAsync` may be called **exactly once** per transaction instance. A second call — even after a failure or cancellation — throws `InvalidOperationException`. This is intentional: each call would generate a new `Guid` idempotency token, bypassing the server-side duplicate detection that the auto-generated token provides.

**Retry pattern:** construct a new transaction instance for each attempt.

```csharp
// ❌ Wrong — second call throws InvalidOperationException
DistributedTransactionResponse r1 = await tx.CommitTransactionAsync();
DistributedTransactionResponse r2 = await tx.CommitTransactionAsync();  // throws!

// ✅ Correct — new instance per attempt
DistributedWriteTransaction BuildTx(CosmosClient client) =>
    client.CreateDistributedWriteTransaction()
          .ReplaceItem("db", "col", new PartitionKey("pk"), "id", updatedItem);

DistributedTransactionResponse response = await BuildTx(client).CommitTransactionAsync();
if (!response.IsSuccessStatusCode)
{
    // When the outcome is unknown (cancellation / network timeout),
    // verify committed state before retrying to avoid duplicate writes.
    await VerifyStateAsync(client);
    response = await BuildTx(client).CommitTransactionAsync();
}
```

---

## 6. Session Consistency

After a successful commit the SDK automatically updates its internal session container with the post-transaction session tokens for every affected collection. This ensures subsequent **Session-consistency reads** against any touched container see the committed state without receiving `ReadSessionNotAvailable`.

This bookkeeping is best-effort: a failure to update session tokens is traced and swallowed so that a server-committed transaction is never surfaced as an error.

### Per-operation session tokens *(PR #5885)*
`DistributedTransactionRequestOptions.SessionToken` accepts a per-operation token in `{partitionKeyRangeId}:{lsn}` format. Because a distributed transaction spans multiple partitions, a single commit-level token is insufficient — supply a token per operation to enforce read-your-own-writes for a specific partition:

```csharp
tx.ReplaceItem(
    database: "db", collection: "col",
    partitionKey: new PartitionKey("pk"), id: "id",
    resource: updatedItem,
    requestOptions: new DistributedTransactionRequestOptions
    {
        SessionToken = previousResult.SessionToken  // e.g. "0:1234"
    });
```

---

## 7. Error Behavior

| Scenario | `response.StatusCode` | Per-operation status |
|---|---|---|
| All operations succeed | `200 OK` | All `2xx` |
| One operation fails (commit aborted) | Promoted from first failing op (e.g. `409 Conflict`) | Failing op: `4xx/5xx`; others: `424 Failed Dependency` |
| Argument validation failure | n/a — throws `ArgumentNullException` / `ArgumentOutOfRangeException` before any network call | — |
| `CancellationToken` cancelled | n/a — throws `OperationCanceledException` | — |

**`424 Failed Dependency`** on non-failing operations is the indicator that the transaction was atomically aborted. Customers should check `response.IsSuccessStatusCode` first, then inspect individual results only when it is `false`.

---

## 8. Custom Deserialization & `CosmosSerializer` Integration

### What works today
For **typed (`<T>`) write operations**, the `CosmosSerializer` registered in `CosmosClientOptions` is honoured: `CreateItem<T>`, `ReplaceItem<T>`, etc. serialize the resource using the customer's serializer before sending.

### Typed result deserialization *(PR #5885)*
`GetOperationResultAtIndex<T>()` on `DistributedTransactionResponse` now returns a `DistributedTransactionOperationResult<T>` with a `Resource` property deserialized via the registered `CosmosSerializer`. The method uses an internal stream snapshot so `ResourceStream` remains fully readable afterward:

```csharp
// Custom serializer settings (e.g. camelCase, Newtonsoft) are applied automatically
Account account = response.GetOperationResultAtIndex<Account>(0).Resource;

// ResourceStream is still readable after the above call
Stream raw = response[0].ResourceStream;
```

### Remaining gap — `XxxStream` write variants
For stream-based write methods (`CreateItemStream`, `ReplaceItemStream`, etc.) the caller is responsible for serialization — the SDK passes bytes through unchanged. This must be documented in XML comments.

---

## 9. Stream API — Ownership & Lifecycle

### Write-side: `<Write-op>Stream` methods

Streams passed to `CreateItemStream`, `ReplaceItemStream`, etc. are:

- **Read eagerly and buffered** in full before the first network call (during `CommitTransactionAsync`).
- **Not disposed by the SDK.** Ownership remains with the caller. Do not dispose or reuse the stream before `CommitTransactionAsync` returns. After it returns, the bytes have been fully consumed and the stream may be safely disposed.

This differs from `Container.CreateItemStreamAsync`, which disposes the stream. The DTX policy must be documented explicitly in XML comments.

### Response-side: `ResourceStream`

Each `DistributedTransactionOperationResult.ResourceStream` is a **seekable, read-only `MemoryStream`**:
- `CanSeek == true` — callers can reset `Position = 0` for a second read pass.
- Owned by the parent `DistributedTransactionResponse` and disposed with it.
- Not wrapped in `CloneableStream` (unlike `TransactionalBatch` results). There is only one stream position — concurrent readers must coordinate or copy bytes out.

**Safe pattern for deferred access:**

```csharp
byte[] body;
using (DistributedTransactionResponse response = await tx.CommitTransactionAsync())
{
    if (response.IsSuccessStatusCode)
        body = ((MemoryStream)response[0].ResourceStream).ToArray();
}
// body is safe to use after the response is disposed
```

---

## 10. Binary Encoding Support

The DTX wire protocol is **JSON-only** in both directions. There is no HybridRow / binary encoding support.

Binary encoding for the `operations/dtc` endpoint is not planned in the current implementation. If this changes, it should be noted in the public release notes.

---

## 11. Usage Patterns

### 11.1 Write transaction — cross-container transfer

```csharp
using CosmosClient client = new CosmosClient(connectionString);

DistributedWriteTransaction tx = client.CreateDistributedWriteTransaction();

tx.CreateItem(
    database: "banking", collection: "accounts",
    partitionKey: new PartitionKey("account-A"), id: "account-A",
    resource: new Account { Id = "account-A", Balance = 900 })
  .ReplaceItem(
    database: "banking", collection: "accounts",
    partitionKey: new PartitionKey("account-B"), id: "account-B",
    resource: new Account { Id = "account-B", Balance = 1100 })
  .CreateItem(
    database: "banking", collection: "audit",
    partitionKey: new PartitionKey("2026-05-15"), id: Guid.NewGuid().ToString(),
    resource: new AuditEvent { From = "account-A", To = "account-B", Amount = 100 });

using DistributedTransactionResponse response = await tx.CommitTransactionAsync();

if (!response.IsSuccessStatusCode)
    Console.WriteLine($"Transaction failed: {response.StatusCode} – {response.ErrorMessage}");
```

### 11.2 Conditional replace with ETag

```csharp
DistributedWriteTransaction tx = client.CreateDistributedWriteTransaction();

tx.ReplaceItem(
    database: "inventory", collection: "items",
    partitionKey: new PartitionKey("sku-999"), id: "sku-999",
    resource: updatedItem,
    requestOptions: new DistributedTransactionRequestOptions
    {
        IfMatchEtag = existingEtag   // returns 412 Precondition Failed if ETag doesn't match
    });

using DistributedTransactionResponse response = await tx.CommitTransactionAsync();
```

### 11.3 Read transaction — consistent snapshot across partitions

```csharp
DistributedReadTransaction tx = client.CreateDistributedReadTransaction();

// All reads execute under snapshot isolation (PR #5885)
tx.ReadItem("banking", "accounts", new PartitionKey("account-A"), "account-A")
  .ReadItem("banking", "accounts", new PartitionKey("account-B"), "account-B");

using DistributedTransactionResponse response = await tx.CommitTransactionAsync();

if (response.IsSuccessStatusCode)
{
    // GetOperationResultAtIndex<T>() uses the registered CosmosSerializer (PR #5885)
    Account accountA = response.GetOperationResultAtIndex<Account>(0).Resource;
    Account accountB = response.GetOperationResultAtIndex<Account>(1).Resource;
}
```

### 11.4 Per-operation session token *(PR #5885)*

```csharp
DistributedWriteTransaction tx = client.CreateDistributedWriteTransaction();
tx.ReplaceItem(
    database: "inventory", collection: "items",
    partitionKey: new PartitionKey("sku-999"), id: "sku-999",
    resource: updatedItem,
    requestOptions: new DistributedTransactionRequestOptions
    {
        SessionToken = previousResult.SessionToken  // "{pkRangeId}:{lsn}"
    });

using DistributedTransactionResponse response = await tx.CommitTransactionAsync();
```

### 11.5 Per-operation error inspection

```csharp
using DistributedTransactionResponse response = await tx.CommitTransactionAsync();

if (!response.IsSuccessStatusCode)
{
    foreach (DistributedTransactionOperationResult result in response)
    {
        if (!result.IsSuccessStatusCode && (int)result.StatusCode != 424)
        {
            Console.WriteLine(
                $"Operation {result.Index} failed: {result.StatusCode} / sub={result.SubStatusCodeValue}");
        }
    }
}
```

---

## 12. Known Limitations

1. **No abort / rollback API.** By design — transactions can only be committed. Abandoning a transaction means simply not calling `CommitTransactionAsync`; the server will time out any uncommitted state.
2. **No read-within-write.** `DistributedWriteTransaction` has no `ReadItem`. Atomic read-modify-write requires separate transactions.
3. **Transaction instance is single-use** *(PR #5884)*. A second call to `CommitTransactionAsync` always throws `InvalidOperationException`. Build a new transaction for each retry.
4. **Operation count limit not documented.** The maximum number of operations per transaction is not stated.
5. **Gateway-mode only.** Direct-mode connectivity is not supported.
6. **Same-account only.** Cross-account transactions are not supported.
7. **Limited `DistributedTransactionRequestOptions`.** No operation-level priority, TTL override, or throughput control group.
8. **No operation count guard.** `TransactionalBatch` enforces a cap of 100 operations; DTX has no documented or enforced cap.
9. **JSON-only wire protocol.** No HybridRow / binary encoding; `TransactionalBatch` uses binary encoding for both directions.
10. **Custom `CosmosSerializer` not applied to `XxxStream` write variants.** `GetOperationResultAtIndex<T>()` honours the serializer *(resolved in PR #5885)*; however for stream-based write variants the caller is responsible for serialization.
11. **Caller-supplied streams are not disposed by the SDK.** The stream lifecycle contract differs from `Container.CreateItemStreamAsync` and must be documented in XML comments.

---

## 13. Open Questions for API Review

1. **`database` + `collection` strings vs `Container` object.** Should operations accept a `Container` reference instead of loose strings for type safety and metadata reuse?

2. **Caller-supplied idempotency token.** Should `CommitTransactionAsync` accept an optional `Guid idempotencyToken` for exactly-once semantics across process restarts?

3. **Operation count cap.** Should there be a documented and enforced maximum? What is the server-side limit?

4. **`SubStatusCodes` enum visibility.** Should the `SubStatusCodes` enum be made public so customers can match known values without magic numbers?

5. **Mixed read/write transaction.** Is this on the roadmap? Combining `ReadItem` and write operations in one transaction is required for atomic read-modify-write.

6. **Stream disposal contract.** Should the SDK dispose `streamPayload` after consuming it (matching `Container.CreateItemStreamAsync`), or explicitly document that it does not?

---

## 14. Changelog Readiness

- Touches `Microsoft.Azure.Cosmos/src/**` ✅
- Customer-observable new API surface ✅
- Classification: **Features Added**

Suggested entry (under `### Unreleased → Features Added`):

> **Distributed Transactions (preview):** Added `CosmosClient.CreateDistributedWriteTransaction()` and `CosmosClient.CreateDistributedReadTransaction()` to support atomic multi-partition, multi-container transactions within the same Cosmos DB account. Use `CommitTransactionAsync()` to execute all buffered operations as a single unit of work.

---