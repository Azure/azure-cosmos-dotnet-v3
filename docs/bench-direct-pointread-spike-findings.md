# Bench-direct-pointread — Stage 2 spike findings

Ran `--verify-spike` end-to-end against a 3-PKR routing map with a real unmodified `CosmosClient` configured for Direct mode. Goal: prove that a name-based `ReadItemStreamAsync` can succeed with **no** emulator, **no** live account, **no** network — only a single `HttpMessageHandler` and a single `TransportClient` subclass.

## Result

✅ **Spike passed.** `ReadItemStreamAsync -> 200 OK`, with exactly:
- 1 account hit (`GET /`)
- 1 container hit (`GET /dbs/{dbName}/colls/{collName}`)
- 1 × `/pkranges` 200 + 1 × 304 (change-feed loop exits on the second turn)
- 1 × `/addresses` for the one PKR the PK routes to
- 1 transport `InvokeStoreAsync` call returning 200
- 0 unknown URLs hit the throw-on-unexpected branch

This confirms the plan’s Stage 3–5 are on the right track; no pivot to Stage 2B needed.

## URLs the SDK hits during cold-start (name-based `ReadItemStreamAsync`)

Exhaustively enumerated — anything not listed here will trip the handler’s throw-on-unknown branch, so Stage 3 can safely keep that assertion enabled.

1. `GET {regionEndpoint}/` — account (`AccountProperties` JSON).
2. `GET {regionEndpoint}/dbs/{dbName}/colls/{collName}` — container metadata by name (`CollectionCache.ResolveByNameAsync`). **Never** by RID on the first point read.
3. `GET {regionEndpoint}/dbs/{dbRid}/colls/{collRid}/pkranges` — by RID. First call: 200 with feed body + `ETag`. Second call: 304. SDK is happy with any stable ETag string.
4. `GET {regionEndpoint}//addresses/?$resolveFor=...&$filter=protocol eq rntbd&$partitionKeyRangeIds={id}` — by RID + PKR-id. **Must** echo the requested PKR id or `PartitionKeyRangeGoneException`. Query-string parameter is `$partitionKeyRangeIds` (CSV).

The SDK does **not** call `/dbs/{dbName}` alone for a point read (no database-level lookup). It also does **not** call `/clientconfig` — telemetry is off by default — and does not issue any `/offers` traffic for a read.

## Non-obvious JSON fields that were required

- `AccountProperties` must include the full `Consistency`, `SystemReplicationPolicy`, `ReplicationPolicy`, `ReadPolicy` objects, and at least one `readableLocations` + `writableLocations` entry whose `databaseAccountEndpoint` matches the region used by the handler. The `MockSetupsHelper.SetupSingleRegionAccount` template at `tests/Microsoft.Azure.Cosmos.Tests/PartitionKeyRangeFailoverTests/MockSetupsHelper.cs:61-93` is sufficient verbatim.
- `ContainerProperties` must set partition-key `Version = V2` — otherwise `CollectionRoutingMap` rejects the EPK format produced for V2 hash.
- The address feed JSON uses **`Addresss`** (triple-s) as the list property name, not `Addresses`. This is the wire-level on-the-box serialization name. The `Address` class in `Microsoft.Azure.Documents` already serializes with the right property names via its own attributes, so `JArray.FromObject(List<Address>)` is the reliable way to build the feed.
- `PhysicalUri` on each replica must be a **parseable** URI (e.g. `rntbd://host:port/apps/.../replicas/id/`). Returning anything that fails `new Uri(...)` produces an `ArgumentNullException` deep inside `GatewayAddressCache.ToPartitionAddressAndRange`.
- The stub `StoreResponse` for a `Read` must include an `LSN` backend header; reuse `MockRequestHelper.GetStoreResponse`'s existing Read branch, which already sets it.

## Environment surprises

- `COSMOS_DISABLE_IMDS_ACCESS=true` must be set **before** the `CosmosClient` is constructed. Setting it in the handler won’t help — the SDK reads it at static-init time in `DocumentClient`.
- `MockRequestHelper`'s static constructor reads `samplepayload.json` from the **current working directory**. When launching via `dotnet run`, CWD is the project folder, not `bin/Release/net6.0/`. The spike sets `Directory.SetCurrentDirectory(typeof(Program).Assembly.Location)` up-front; Stage 5's benchmark must do the same (or copy the file to a path the helper resolves).
- `TransportClientHandlerFactory` is invoked exactly once per client, not per region/partition. Returning the same stub instance is correct.

## Risks surfaced (for Stage 3)

- None blocking. The spike was on the happy path; Stage 3 will additionally exercise cold-paths like `ReadAccountAsync`, `container.ReadContainerAsync`, `GetFeedRangesAsync`, and will have to keep the handler's "unknown URL" branch satisfied across all of them. Based on the URL set above, there is nothing unexpected to add.
- Address pre-warm at 17,329 ranges (Stage 4) is still unvalidated; this spike used a 3-PKR map.
