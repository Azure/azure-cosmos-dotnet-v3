# Distributed Transactions API Review
## Azure Cosmos DB .NET SDK v3

**Document status:** Pre-announcement review draft  
**Namespace:** `Microsoft.Azure.Cosmos`

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
| Custom `CosmosSerializer` on results | Yes — `GetOperationResultAtIndex<T>()` | **No** — raw `ResourceStream` only |
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
    // Adds a point-read to the transaction. All reads execute as a consistent snapshot.
    public abstract DistributedReadTransaction ReadItem(
        string database, string collection,
        PartitionKey partitionKey, string id,
        DistributedTransactionRequestOptions requestOptions = null);
}
```
---

### 4.5 `DistributedTransactionRequestOptions`

```csharp
// Inherits: IfMatchEtag, IfNoneMatchEtag, SessionToken, Properties
public class DistributedTransactionRequestOptions : RequestOptions { }
```

`IfMatchEtag` is wired through to the server envelope and triggers `412 Precondition Failed` if the ETag does not match. The class is otherwise empty.

---

### 4.6 `DistributedTransactionResponse`

```csharp
public class DistributedTransactionResponse
    : IReadOnlyList<DistributedTransactionOperationResult>, IDisposable
{
    // Zero-based indexer — matches the order operations were added to the transaction.
    public virtual DistributedTransactionOperationResult this[int index] { get; }

    public virtual Headers      Headers            { get; }
    public virtual string       ActivityId         { get; }
    public virtual double       RequestCharge      { get; }  // see note below
    public virtual HttpStatusCode StatusCode        { get; }  // promoted from failing op on 207
    public virtual bool         IsSuccessStatusCode { get; }  // 200–299
    public virtual string       ErrorMessage       { get; }
    public virtual int          Count              { get; }
    public virtual Guid         IdempotencyToken   { get; }  // auto-generated; reused across retries

    public virtual IEnumerator<DistributedTransactionOperationResult> GetEnumerator();
    public void Dispose();
}
```

#### Behavior
- **Status code promotion.** For `207 Multi-Status` responses, the status code of the first failing operation (excluding `424 Failed Dependency`) is promoted to `response.StatusCode`. Operations that were aborted due to another operation's failure report `424`.
- **`IdempotencyToken`.** A `Guid` auto-generated at commit time. The same token is reused for any internal retries, so callers can capture it for application-level deduplication across process boundaries.
- **`Dispose()`.** Disposes the `ResourceStream` on every operation result. Do not access `ResourceStream` after the response has been disposed.

---

### 4.7 `DistributedTransactionOperationResult`

```csharp
public class DistributedTransactionOperationResult
{
    public virtual int           Index              { get; }  // zero-based
    public virtual HttpStatusCode StatusCode        { get; }
    public virtual bool          IsSuccessStatusCode { get; }
    public virtual string        ETag               { get; }
    public virtual string        SessionToken       { get; }
    public virtual string        PartitionKeyRangeId { get; }
    public virtual Stream        ResourceStream     { get; }  // populated for Read ops; null for writes
    public virtual double        RequestCharge      { get; }
    public virtual uint          SubStatusCodeValue { get; }
}
```
---

## 5. Idempotency

The SDK auto-generates a `Guid` idempotency token for each `CommitTransactionAsync` call and reuses it across any internal retries. The committed token is returned in `DistributedTransactionResponse.IdempotencyToken`.

**Customer implication:** Idempotency within a single `CommitTransactionAsync` call is handled automatically. For exactly-once semantics across process restarts (e.g., a crash after the server committed but before the client received the response), customers must persist the `IdempotencyToken` before calling commit and present it on retry. The current API does not accept a caller-supplied token — the only way to supply one is to re-call `CommitTransactionAsync`, which always generates a new token.

---

## 6. Session Consistency

After a successful commit the SDK automatically updates its internal session container with the post-transaction session tokens for every affected collection. This ensures subsequent **Session-consistency reads** against any touched container see the committed state without receiving `ReadSessionNotAvailable`.

This bookkeeping is best-effort: a failure to update session tokens is traced and swallowed so that a server-committed transaction is never surfaced as an error.

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

### The gap — response deserialization
**Response results are not deserialized through the registered `CosmosSerializer`.** `ResourceStream` on each operation result contains raw JSON bytes produced by `System.Text.Json` with fixed case-insensitive options. This means:

- Camelcase / snake_case / custom property mappings configured via `CosmosSerializerOptions` are **not applied** when reading results.
- Customers using `Newtonsoft.Json` via a custom `CosmosSerializer` must wrap it manually for result deserialization.

This also means there is **no typed result accessor** equivalent to `TransactionalBatchResponse.GetOperationResultAtIndex<T>()`. Callers must deserialize manually:

```csharp
// Today — custom serializer options are NOT automatically applied
var account = JsonSerializer.Deserialize<Account>(response[0].ResourceStream);

// What is needed
var account = response.GetOperationResultAtIndex<Account>(0).Resource;
```

The internal plumbing to add `GetOperationResultAtIndex<T>()` using the stored serializer reference already exists on the response object. This method should be added before GA.

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

### 11.3 Read transaction — consistent cross-partition snapshot

```csharp
DistributedReadTransaction tx = client.CreateDistributedReadTransaction();

tx.ReadItem("banking", "accounts", new PartitionKey("account-A"), "account-A")
  .ReadItem("banking", "accounts", new PartitionKey("account-B"), "account-B");

using DistributedTransactionResponse response = await tx.CommitTransactionAsync();

if (response.IsSuccessStatusCode)
{
    // No GetOperationResultAtIndex<T>() today — manual deserialization required
    var accountA = JsonSerializer.Deserialize<Account>(response[0].ResourceStream);
    var accountB = JsonSerializer.Deserialize<Account>(response[1].ResourceStream);
}
```

### 11.4 Per-operation error inspection

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

1. **No abort / rollback API.** (TBD)
2. **No read-within-write.** `DistributedWriteTransaction` has no `ReadItem`. Atomic read-modify-write requires separate transactions.
3. **No typed result accessor.** `GetOperationResultAtIndex<T>()` is absent; customers must deserialize `ResourceStream` manually. Custom `CosmosSerializer` settings are not applied to result bodies.
4. **Operation count limit not documented.** The maximum number of operations per transaction is not stated.
5. **Gateway-mode only.** Direct-mode connectivity is not supported.
6. **Same-account only.** Cross-account transactions are not supported.
7. **`DistributedTransactionRequestOptions` is empty.** No operation-level priority level, TTL override, or throughput control group.
8. **No operation count guard.** `TransactionalBatch` enforces a cap of 100 operations; DTX has no documented or enforced cap.
9. **JSON-only wire protocol.** No HybridRow / binary encoding; `TransactionalBatch` uses binary encoding for both directions.
10. **Custom `CosmosSerializer` not applied to response deserialization.** Results always contain raw JSON; serializer settings are honoured only for write request payloads.
11. **Caller-supplied streams are not disposed by the SDK.** The stream lifecycle contract differs from `Container.CreateItemStreamAsync` and must be documented.

---

## 13. Changelog Readiness

- Touches `Microsoft.Azure.Cosmos/src/**` ✅
- Customer-observable new API surface ✅
- Classification: **Features Added**

Suggested entry (under `### Unreleased → Features Added`):

> **Distributed Transactions (preview):** Added `CosmosClient.CreateDistributedWriteTransaction()` and `CosmosClient.CreateDistributedReadTransaction()` to support atomic multi-partition, multi-container transactions within the same Cosmos DB account. Use `CommitTransactionAsync()` to execute all buffered operations as a single unit of work.

---