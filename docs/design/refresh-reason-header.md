# `x-ms-cosmos-refresh-reason` — Generic refresh-reason header

> **Status**: design + implementation (this branch).
> **Branch**: `users/kirankk/cosmos-refresh-reason`
> **PRs**: this repo (SDK core) + companion PR in `Microsoft.Azure.Cosmos.Direct` upstream.

## 1. Problem

Every forced address-cache refresh makes the SDK call `GET …/addresses?…` with
`x-ms-force-refresh: true`. Today the gateway cannot tell **why** the SDK forced
the refresh — server 410? partition split? a TCP connect failure? the SDK's
10-minute suboptimal-replica-set timer? an opportunistic background refresh?

The same gap applies to other forced-refresh egress paths the SDK has (and will
add): the partition-key-range cache refresh that fires when a ChangeFeed
iterator discovers a split/merge and needs to forward its continuation,
`CollectionRoutingMap` refresh, etc. All of these ultimately call into the
gateway with a "force-refresh" signal but carry **no cause hint**.

We attach a new header whose value is drawn from a **closed, design-time-bounded**
enum. Every possible value is known at compile time; no dynamic substrings,
no user-controlled data.

## 2. Scope & generic header design

- **Header name**: **`x-ms-cosmos-refresh-reason`** (intentionally *generic*, not
  `address-refresh-reason`). Identifies the *reason* for any SDK-forced refresh
  egress, irrespective of which cache is being refreshed. The gateway can
  route/aggregate by URL path (`/addresses`, `/pkranges`, `/collections`, …)
  combined with the reason.
- **This change delivers**: the end-to-end mechanism (header + carrier + validator
  + egress) plus the **address-cache** reason vocabulary (§4 below). All tagging
  sites plumbed through in this change are the address-cache force-refresh
  paths.
- **Future extension points** (not in this change, but the design supports them
  with zero additional infrastructure):
  - **PK-range cache refresh on ChangeFeed forward**: when a ChangeFeed iterator
    hits a split/merge and forwards its continuation, the SDK force-refreshes
    the routing map. That egress can emit the same header with a
    `pkr_cache.changefeed_forward` reason added to the same enum.
  - **PK-range cache refresh on query plan rewrite**, **CollectionRoutingMap
    refresh on name-cache miss**, etc. — all additive enum members sharing the
    same header + carrier + validator.
- **Naming convention for future values**: `<cache_or_surface>.<subcause>` —
  **exactly two dot-separated segments**. Existing address values follow this
  shape (`gone.server`, `gone.connect`, `InsufficientReplicas.Quorum`, …).

## 3. Design choices

| Choice | Value | Rationale |
|---|---|---|
| Header name | `x-ms-cosmos-refresh-reason` | Generic across caches; awaiting final service-team confirmation. |
| Wire charset | `[A-Za-z0-9_.]` | RFC-7230 safe, log/metric-pipeline safe. |
| Wire separator | `.` | Unambiguous in log/metric pipelines; `/` is sometimes parsed as path. |
| Max segments | 2 | Keeps metric cardinality bounded and readable. |
| Carrier | `RefreshReason` enum on `DocumentServiceRequestContext` | Strong-typed along the plumbing; stringified only at egress. Single carrier serves all future caches. |
| Egress (this change) | `GatewayAddressCache.Get{Server,Master,ForRangeId}AddressesViaGatewayAsync` | The three address-cache force-refresh egress methods. Future changes add egress in `PartitionKeyRangeCache`, `CollectionRoutingMap`, etc. using the same helper. |
| Opt-in invariant | `GatewayAddressCache.ValidateRefreshReasonPresence` static toggle (default `false`; tests set `true`). If on + `forceRefresh=true` + reason is `Unspecified` → throw `InvalidOperationException`. | Zero prod overhead; automatic regression coverage for any future force-refresh site. |
| Vocabulary closed under | `enum RefreshReason` + `Dictionary<RefreshReason,string>` mapping | Adding a new reason is a reviewable, compile-checked diff. |
| Precedence | `explicitReason` argument > `request.RequestContext.RefreshReason` > `Unspecified` | Lets call-path-local signals override carrier while keeping per-request context as the default. |

## 4. The closed enumeration (address-cache values shipping in this change) — 23 values

> Format: **`EnumValueName` → wire value `"..."`** — enum name first, wire value
> second. Wire values are **flat** (two dot-segments max). The old
> `.transport.` and `.substatus.` intermediate segments were dropped.
> The enum `RefreshReason` is **generic** and will accumulate values from future
> caches (PK-range-cache ChangeFeed-forward, collection-cache name-stale, …).

### Sentinel

| # | Enum | Wire | Notes |
|---|---|---|---|
| 0 | `Unspecified` | `unspecified` | Defensive default. Must never appear on the wire in production once all call sites are tagged. The opt-in validator enforces this invariantly in tests. |

### Group A — Real 410 from the server (no transport synthesis)

| # | Enum | Wire | Notes |
|---|---|---|---|
| 1 | `GoneServer` | `gone.server` | `GoneException` surfaced by the server over RNTBD with **no inner `TransportException`**. Authoritative "this replica no longer owns this PKR" signal. Set at the retry-policy / StoreReader level. |

### Group B — Gone with server-provided substatus (routing-topology changes)

> **Note.** Substatus codes 1007 (split), 1008 (partition migration), 1000
> (name-cache stale), and `PartitionKeyRangeGoneException` typically drive a
> **PK-range / collection-cache** refresh rather than an address-cache refresh —
> the SDK's response is usually "re-resolve the PKR/container" first, and only
> if that produces new physical endpoints does the address cache get touched.
> These enum values exist so that *when* an address-cache refresh does happen
> on the back of such a Gone (cache-miss cascade), the cause is attributable;
> and they pre-position the generic enum for the upcoming PK-range-cache
> egress tagging (where most of these will actually surface).

| # | Enum | Wire | Notes |
|---|---|---|---|
| 2 | `GoneCompletingSplit` | `gone.completing_split` | `PartitionKeyRangeIsSplittingException`, SubStatus 1007. |
| 3 | `GoneCompletingPartitionMigration` | `gone.completing_partition_migration` | `PartitionIsMigratingException`, SubStatus 1008. |
| 4 | `GoneNameCacheStale` | `gone.name_cache_stale` | `InvalidPartitionException`, SubStatus 1000. |
| 5 | `GonePartitionKeyRangeGone` | `gone.partition_key_range_gone` | `PartitionKeyRangeGoneException`. |

### Group C — Gone synthesized by the SDK's transport layer

> Pairs of (`*Failed`, `*Timeout`) in `TransportErrorCode` are intentionally
> collapsed into one enum value because the gateway's reaction is the same.

| # | Enum | Wire | Covers `TransportErrorCode` | Notes |
|---|---|---|---|---|
| 6 | `GoneUnknown` | `gone.unknown` | `Unknown`, `ChannelOpenFailed`, `ChannelOpenTimeout`, `RequestTimeout` | All four are explicit *default/catch-all* codes per `TransportErrorCode.cs` comments. A spike in `gone.unknown` is itself an actionable signal. |
| 7 | `GoneDnsResolution` | `gone.dns_resolution` | `DnsResolutionFailed/Timeout` | Client couldn't resolve the replica's hostname. |
| 8 | `GoneConnect` | `gone.connect` | `ConnectFailed/Timeout` | TCP handshake failed — server closed the listening socket or is unreachable. **This is the bucket that today gets silently mis-attributed**; any network-level refusal/drop for a *new* connection shows up here. |
| 9 | `GoneSslNegotiation` | `gone.ssl_negotiation` | `SslNegotiationFailed/Timeout` | TLS handshake failed after TCP came up. |
| 10 | `GoneNegotiationTimeout` | `gone.negotiation_timeout` | `TransportNegotiationTimeout` | RNTBD-level parameter negotiation timed out *after* TLS succeeded. |
| 11 | `GoneChannelMultiplexerClosed` | `gone.channel_multiplexer_closed` | `ChannelMultiplexerClosed` | Client-side RNTBD dispatcher stopped accepting new requests. |
| 12 | `GoneSend` | `gone.send` | `SendFailed/Timeout` | Request bytes couldn't be pushed to the wire. |
| 13 | `GoneSendLockTimeout` | `gone.send_lock_timeout` | `SendLockTimeout` | **Kept standalone** — internal send-lock contention on the client. Distinguishes "we're saturated sending" from "we can't reach the server". |
| 14 | `GoneReceive` | `gone.receive` | `ReceiveFailed/Timeout` | Request was sent but the response didn't come back. |
| 15 | `GoneReceiveStreamClosed` | `gone.receive_stream_closed` | `ReceiveStreamClosed` | **Server-initiated clean close** of the TCP stream while the client was awaiting a response. Distinct from `ReceiveFailed` because it's a graceful FIN. |
| 16 | `GoneConnectionBroken` | `gone.connection_broken` | `ConnectionBroken` | Underlying connection marked unusable (typically sticky-applied from a prior failure). |
| 17 | `GoneChannelWaitingToOpenTimeout` | `gone.channel_waiting_to_open_timeout` | `ChannelWaitingToOpenTimeout` | **Slot-wait timeout** (`MaxConcurrentOpeningConnectionCount` saturated). The request couldn't even start opening a channel. |
| 18 | `GoneWriteNotSent` | `gone.write_not_sent` | — | Write-request Gone-synthesis branch where `DocumentServiceRequest.UserRequestSent == false` regardless of the inner transport code. Server never saw the write, so the 410 is safe to retry. Tagged in upstream Direct. |

### Group D — Forced refresh *not* driven by a Gone

| # | Enum | Wire | Notes |
|---|---|---|---|
| 19 | `InsufficientReplicasQuorum` | `InsufficientReplicas.Quorum` | Barrier requests: the known replica set is too small for the requested consistency — *before* any 410. Pure SDK-side decision: "we don't think we have enough replicas, refresh the set." Tagged in `ConsistencyWriter`, `QuorumReader`. |
| 20 | `InsufficientReplicasSuboptimalTimer` | `InsufficientReplicas.SuboptimalTimer` | `GatewayAddressCache` suboptimal-server timer and master-suboptimal: if `address_count < MaxReplicaSetSize` for 10 minutes, force a refresh. Typically indicates lingering stale-partial cache state. |
| 21 | `ReplicaHealthUnhealthyLongLived` | `ReplicaHealth.unhealthyLongLived` | `GatewayAddressCache` on-demand revalidation when a URI has been `Unhealthy` for ≥ 1 minute. |
| 22 | `ConnectionEventServerClosed` | `connection_event.server_closed` | `MarkAddressesToUnhealthyAsync` driven by `Dispatcher.RaiseConnectionEvent`. Covers both `ReceiveStreamClosed → ReadEof` and `ReceiveFailed → ReadFailure` connection events (distinct from the request-path versions in Group C — these come from the async listener). Tagged in upstream Direct. |

## 5. Changed files

### This repo (SDK core)

| File | Change |
|---|---|
| `Microsoft.Azure.Cosmos/src/Routing/RefreshReason.cs` *(new)* | Generic `internal enum RefreshReason` with 23 explicitly-numbered members. |
| `Microsoft.Azure.Cosmos/src/Routing/RefreshReasonExtensions.cs` *(new)* | `WireValues` dictionary (single source of truth), `ToHeaderValue`, `FromTransportErrorCode`, `ClassifyGoneFromException`. |
| `Microsoft.Azure.Cosmos/src/Routing/GatewayAddressCache.cs` | `ValidateRefreshReasonPresence` toggle; `EmitRefreshReasonHeader` helper; optional `explicitReason` on `GetMasterAddressesViaGatewayAsync`, `GetServerAddressesViaGatewayAsync`, `GetAddressesForRangeIdAsync`. Suboptimal-timer and master-suboptimal tagged. Unhealthy-URI background refresh tagged via `explicitReason`. |
| `Microsoft.Azure.Cosmos/src/direct/HttpConstants.cs` | Added `CosmosRefreshReason` header constant. |
| `Microsoft.Azure.Cosmos/src/direct/DocumentServiceRequestContext.cs` | Added `RefreshReason` carrier property (default `Unspecified`), cloned in `Clone()`. |
| `Microsoft.Azure.Cosmos/src/direct/ConsistencyWriter.cs` | Barrier request tagged `InsufficientReplicasQuorum`. |
| `Microsoft.Azure.Cosmos/src/direct/QuorumReader.cs` | Barrier request tagged `InsufficientReplicasQuorum`. |
| `Microsoft.Azure.Cosmos/src/direct/StoreReader.cs` | Gone-retry paths (2) tagged via `ClassifyPriorGone` helper that walks prior `StoreResult.Exception` for `TransportException` then falls back to substatus mapping. |
| `Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Routing/RefreshReasonFormatterTests.cs` *(new)* | 12 MSTest cases: enum coverage, wire-regex conformance, round-tripping, transport-code mapping, header emission. |

### Upstream `Microsoft.Azure.Cosmos.Direct` (companion PR)

> The `src/direct/` edits above also live in the upstream repo; without the
> upstream PR merging, the shipped `Microsoft.Azure.Cosmos.Direct.dll` won't
> carry the carrier or the header constant. Additionally, the upstream PR adds
> tagging at the following sites (not reachable from this repo's mirror):

| File | Change |
|---|---|
| `rntbd2/TransportClient.cs` | Every Gone-synthesis branch: `request.RequestContext.RefreshReason = RefreshReasonExtensions.FromTransportErrorCode(te.ErrorCode);`. Write-request `!UserRequestSent` branch → `GoneWriteNotSent`. |
| `GoneAndRetryWithRequestRetryPolicy.cs`, `GoneOnlyRequestRetryPolicy.cs` | At the `forceRefreshAddressCache = true` site, derive reason from exception type when still `Unspecified` (covers `GoneServer` and the Group B substatus cases). |
| `AddressSelector.StartBackgroundAddressRefresh` | Propagate `request.RequestContext.RefreshReason` onto the cloned request so the background refresh inherits the originating Gone tag. |
| `ConnectionStateMuxListener.cs` (or wherever `Dispatcher.RaiseConnectionEvent` lands) | Pass the connection-event kind through to `MarkAddressesToUnhealthyAsync`; both `ReadEof` and `ReadFailure` → `ConnectionEventServerClosed`. |

## 6. Test surface

| Test type | Coverage |
|---|---|
| Unit (shipped in this change) | Enum coverage (every member has a `WireValues` entry), wire-regex conformance, exhaustive `TransportErrorCode` mapping, per-call-site tagging assertions, `EmitRefreshReasonHeader` precedence. |
| Emulator / fault-injection (follow-up) | `[AssemblyInitialize]` sets `ValidateRefreshReasonPresence = true`. Per-cause tests inject specific faults (ConnectFailed, DnsResolutionFailed, SslNegotiationFailed, ReceiveStreamClosed, server 410, server 410+substatus, insufficient quorum, time-advance for suboptimal-timer and unhealthy-URI) and assert the **specific** expected wire value. |

## 7. Execution status

| Phase | Status |
|---|---|
| **Phase 1** — Foundation (enum, extensions, header const, carrier, 12 unit tests) | ✅ committed `df11e6bd9` |
| **Phase 2** — GatewayAddressCache egress wiring (validator + helper + optional `explicitReason` on 3 methods) | ✅ committed `682caf40e` |
| **Phase 3** — Call-site tagging (5 sites in this repo + 2 StoreReader Gone-retry paths with classifier) | ✅ committed `a97a893ae` |
| **Phase 4** — Emulator/fault-injection tests, exhaustive transport-code reflection test, docs polish | 🔲 pending |
| **Upstream Direct companion PR** — tagging in `TransportClient`, retry policies, `AddressSelector`, connection-event listener | 🔲 pending |

## 8. Out of scope

- Service-side handling of the new header (gateway repo).
- Adding reason values for **non-address** caches (PK-range-cache
  ChangeFeed-forward, CollectionRoutingMap name-stale, …). The infrastructure
  here supports them additively — a future change just adds enum members and
  tags the relevant egress sites.
- Changing how forced refresh itself works (this is purely additive telemetry).
- Encryption sub-packages.

## 9. Risks & open considerations

1. **Direct package divergence** — most of the remaining tagging (Group A/B/C Gone
   origins) lives in `src/direct/` and the shipped
   `Microsoft.Azure.Cosmos.Direct.dll` won't carry those tags until the parallel
   upstream PR merges. Until then, the reasons surfaced by this branch alone
   will be the *SDK-observed* classification at the StoreReader retry point
   (transport-synth 410 classification via `TransportException` chain-walk)
   plus the 5 non-Gone sites.
2. **Header naming** — awaiting service-team confirmation that
   `x-ms-cosmos-refresh-reason` is acceptable.
3. **Retry overwrites** — each retry overwrites the reason on the `RequestContext`
   (precedence: `explicitReason` > most-recent `RequestContext.RefreshReason`).
   Desired behavior.
4. **New transport error codes upstream** — `FromTransportErrorCode` falls back
   to `GoneUnknown` for unknown codes. A reflection-based exhaustive test is
   planned in Phase 4 to catch newly added codes at build time.
5. **New force-refresh call sites in the future** — the opt-in
   `ValidateRefreshReasonPresence` covers them invariantly in tests.
