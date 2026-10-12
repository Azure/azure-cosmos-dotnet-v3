# Cosmos Design Docs — Curated Path → Chapter Map

> Used by the `PR Review Router` (Stage 3) and the `cosmos-pr-reviewer`
> agent to ground domain findings in the canonical
> [`tvaron3/cosmosdb-design-docs`](https://github.com/tvaron3/cosmosdb-design-docs)
> repo (local clone: `~/Data/microsoft/sandbox/cosmosdb-design-docs/`).
>
> **How it's used**
>
> 1. Router resolves changed-file paths to one or more entries below.
> 2. Router runs `git -C ~/Data/microsoft/sandbox/cosmosdb-design-docs pull --ff-only origin main` (best-effort; non-fatal on failure).
> 3. Router also runs a keyword auto-grep on the PR title + body
>    against chapter `# ` and `## ` headings — top 2 keyword-matched
>    chapters are merged with the curated matches.
> 4. The unique union (≤ 4 chapters) is added to
>    `ACTIVE_REVIEW_CONTEXT.design_doc_chapters` as `## `-section
>    excerpts (capped at 400 tokens total).
> 5. `cosmos-pr-reviewer` (and `Engineering Quality Reviewer` for §5
>    findings) MUST cite `cosmosdb-design-docs/<chapter>.md#<anchor>`
>    in every domain-level finding, using the injected sections.

---

## Chapter inventory (current)

The `default_anchor` column is what the router cites when a curated path match fires but no specific `##` heading was matched by keyword. It is also the deterministic tie-breaker when the keyword auto-grep matches multiple `##` headings inside the same chapter.

| # | File | Default anchor | What it covers |
|---|---|---|---|
| 00 | `00-overview.md` | `overview` | TOC + index |
| 01 | `01-high-level-architecture.md` | `high-level-architecture` | Service-level architecture overview |
| 02 | `02-gateway-layer.md` | `gateway-layer` | Gateway routing, 410 (Gone) handling |
| 03 | `03-backend-storage-engine.md` | `backend-storage-engine` | Backend, ESE, copy modes, partition reseeding |
| 04 | `04-partitioning.md` | `partitioning` | Logical/physical partitions, SDK-side terminology |
| 05 | `05-replication-consistency.md` | `replication-and-consistency` | Consistency levels, ConsistencyWriter/Reader, **VectorSessionToken**, replication internals |
| 06 | `06-transport-layer-rntbd.md` | `transport-layer-rntbd` | Direct vs Gateway connectivity, RNTBD |
| 07 | `07-query-engine.md` | `query-engine` | `ExecuteQuery` core entry point, query execution |
| 08 | `08-indexing-engine.md` | `indexing-engine` | Indexing internals |
| 09 | `09-global-distribution.md` | `global-distribution` | Multi-region routing |
| 10 | `10-control-plane.md` | `control-plane` | ARM, RP, provisioning workflows, region management, DNS |
| 11 | `11-request-units-throttling.md` | `request-units-and-throttling` | RU charging, throttling |
| 12 | `12-change-feed.md` | `change-feed` | Change feed, partition merge + StartFrom timestamp |
| 13 | `13-multi-api-surface.md` | `multi-api-surface` | Multi-API (SQL/Mongo/Cassandra/Gremlin/Table) surface |
| 14 | `14-key-source-locations.md` | `key-source-locations` | Where canonical service code lives |
| 15 | `15-fabric-native-cosmosdb.md` | `fabric-native-cosmosdb` | Fabric-native accounts + mirroring |
| 16 | `16-session-tokens.md` | `session-tokens` | Session token format, cache, lifecycle, gateway vs direct, per-operation behavior |
| 17 | `17-status-codes-and-sdk-retries.md` | `status-codes-and-sdk-retries` | 403/410/429/449/503 + network errors + retry policies |

**Anchor selection algorithm (router Stage 3b uses this verbatim):**

1. If the curated path → chapter rule fires AND no keyword matched a `##` heading inside that chapter → cite `<chapter>.md#<default_anchor>` from the table above.
2. If the keyword auto-grep matched exactly one `##` heading in a chapter → cite that anchor.
3. If multiple `##` headings in the same chapter matched → pick the FIRST one in document order, NOT the longest match. (Deterministic; the document order is stable in git.)
4. If the keyword map below provides an explicit `→ chapter#anchor` for a token → that anchor wins over algorithmic selection.
5. If the file has no anchors at all (rare; some short chapters) → cite the chapter file with no `#anchor` suffix and add a router note.

---

## Curated path → chapter map (per SDK)

> Path patterns are matched as case-insensitive substrings against
> changed-file paths in the PR. Multiple matches union into the
> chapter list.

### .NET (`Azure/azure-cosmos-dotnet-v3`)

| Path substring | Chapters |
|---|---|
| `SessionToken`, `SessionContainer`, `VectorSessionToken` | `16-session-tokens.md`, `05-replication-consistency.md` |
| `Retry`, `Resiliency`, `RetryWith`, `RetryPolicy` | `17-status-codes-and-sdk-retries.md` |
| `ConsistencyLevel`, `Consistency`, `ConsistencyWriter`, `ConsistencyReader` | `05-replication-consistency.md`, `16-session-tokens.md` |
| `Partition`, `PartitionKeyRange`, `Routing`, `CollectionRoutingMap` | `04-partitioning.md` |
| `Query`, `QueryPlan`, `Documents.Query`, `OrderBy`, `GroupBy`, `Distinct` | `07-query-engine.md`, `04-partitioning.md` |
| `ChangeFeed`, `ChangeFeedProcessor`, `LeaseManagement`, `Bootstrapping` | `12-change-feed.md` |
| `Direct`, `RntbdConnection`, `Rntbd2`, `TransportClient` | `06-transport-layer-rntbd.md` |
| `Gateway`, `GatewayStoreModel`, `GatewayAddressCache` | `02-gateway-layer.md`, `06-transport-layer-rntbd.md` |
| `LocationCache`, `GlobalEndpointManager`, `Region` | `09-global-distribution.md`, `02-gateway-layer.md` |
| `RequestUnits`, `Throughput`, `Throttle`, `429` | `11-request-units-throttling.md`, `17-status-codes-and-sdk-retries.md` |
| `Authorization`, `AuthorizationToken`, `Encryption` | `02-gateway-layer.md` |
| `Diagnostics`, `Tracing`, `OpenTelemetry`, `ActivitySource` | `02-gateway-layer.md` (correlation flows) |

### Java (`Azure/azure-sdk-for-java`, area `sdk/cosmos`)

| Path substring | Chapters |
|---|---|
| `SessionContainer`, `ISessionToken`, `VectorSessionToken`, `SimpleSessionToken` | `16-session-tokens.md`, `05-replication-consistency.md` |
| `Retry`, `RetryPolicy`, `ResourceThrottleRetryPolicy`, `RetryWith` | `17-status-codes-and-sdk-retries.md` |
| `ConsistencyLevel`, `ConsistencyReader`, `ConsistencyWriter` | `05-replication-consistency.md`, `16-session-tokens.md` |
| `RoutingMap`, `PartitionKeyRange`, `PartitionKeyInternal` | `04-partitioning.md` |
| `query`, `QueryPlan`, `OrderBy`, `GroupBy`, `Distinct` | `07-query-engine.md`, `04-partitioning.md` |
| `changeFeed`, `LeaseStoreManager`, `Bootstrap` | `12-change-feed.md` |
| `Rntbd`, `direct`, `TransportClient`, `ChannelPool` | `06-transport-layer-rntbd.md` |
| `Gateway`, `GatewayServiceConfigurationReader` | `02-gateway-layer.md`, `06-transport-layer-rntbd.md` |
| `LocationCache`, `GlobalEndpointManager` | `09-global-distribution.md` |
| `Throughput`, `RequestRateTooLargeException`, `429` | `11-request-units-throttling.md`, `17-status-codes-and-sdk-retries.md` |
| `tracing`, `Diagnostics`, `cosmosDiagnostics`, `OTel` | `02-gateway-layer.md` |

### Python (`Azure/azure-sdk-for-python`, area `sdk/cosmos`)

| Path substring | Chapters |
|---|---|
| `session_token`, `_session.py` | `16-session-tokens.md`, `05-replication-consistency.md` |
| `_retry`, `retry_policy`, `_resource_throttle` | `17-status-codes-and-sdk-retries.md` |
| `consistency` | `05-replication-consistency.md`, `16-session-tokens.md` |
| `partition_key`, `_routing` | `04-partitioning.md` |
| `query`, `_execution_context`, `_orderby`, `_groupby` | `07-query-engine.md`, `04-partitioning.md` |
| `change_feed`, `_lease`, `_changefeed` | `12-change-feed.md` |
| `_global_endpoint`, `_location_cache` | `09-global-distribution.md` |
| `throughput`, `_throttle`, `429` | `11-request-units-throttling.md`, `17-status-codes-and-sdk-retries.md` |
| `_diagnostics`, `_logging` | `02-gateway-layer.md` |

### Go (`Azure/azure-sdk-for-go`, area `sdk/data/azcosmos`)

| Path substring | Chapters |
|---|---|
| `session`, `session_token`, `session_container` | `16-session-tokens.md`, `05-replication-consistency.md` |
| `retry`, `retry_policy` | `17-status-codes-and-sdk-retries.md` |
| `consistency` | `05-replication-consistency.md`, `16-session-tokens.md` |
| `partition`, `partition_key`, `routing` | `04-partitioning.md` |
| `query`, `query_plan` | `07-query-engine.md`, `04-partitioning.md` |
| `change_feed` | `12-change-feed.md` |
| `endpoint`, `location_cache`, `global_endpoint` | `09-global-distribution.md` |

### Rust (`Azure/azure-sdk-for-rust`, area `sdk/cosmos`; also `azure-cosmos-client-engine`)

| Path substring | Chapters |
|---|---|
| `session_token`, `session_cache` | `16-session-tokens.md`, `05-replication-consistency.md` |
| `retry`, `retry_policy` | `17-status-codes-and-sdk-retries.md` |
| `consistency` | `05-replication-consistency.md`, `16-session-tokens.md` |
| `partition`, `partition_key`, `routing` | `04-partitioning.md` |
| `query`, `query_plan` | `07-query-engine.md`, `04-partitioning.md` |
| `change_feed` | `12-change-feed.md` |
| `endpoint`, `location` | `09-global-distribution.md` |
| `client-engine/`  (any) | `04-partitioning.md`, `07-query-engine.md`, `17-status-codes-and-sdk-retries.md` |

### Spark (`Azure/azure-sdk-for-java`, area `sdk/cosmos/azure-cosmos-spark*`)

| Path substring | Chapters |
|---|---|
| `ChangeFeed`, `ChangeFeedDataSource`, `IncrementalChangeFeed` | `12-change-feed.md` |
| `Throughput`, `BulkWriter`, `RU` | `11-request-units-throttling.md`, `17-status-codes-and-sdk-retries.md` |
| `Partition`, `PartitionRange`, `Routing` | `04-partitioning.md` |
| `Query`, `Filter`, `Projection`, `Catalog` | `07-query-engine.md`, `04-partitioning.md` |
| `Diagnostics`, `Telemetry` | `02-gateway-layer.md` |

### Kafka Connect (`microsoft/kafka-connect-cosmosdb`)

| Path substring | Chapters |
|---|---|
| `SourceConnector`, `ChangeFeed`, `Lease` | `12-change-feed.md` |
| `SinkConnector`, `Throughput`, `RU` | `11-request-units-throttling.md`, `17-status-codes-and-sdk-retries.md` |
| `Partition`, `Topic`, `Routing` | `04-partitioning.md` |

### Spring Data (`microsoft/spring-data-cosmosdb` / `Azure/azure-spring-boot`)

| Path substring | Chapters |
|---|---|
| `Repository`, `Query`, `QueryAnnotation` | `07-query-engine.md` |
| `Pageable`, `ChangeFeed` | `12-change-feed.md` |
| `Auditing`, `Diagnostics` | `02-gateway-layer.md` |

---

## Keyword auto-grep — `keyword → chapter#anchor` map

> Used by the router as the deterministic keyword → chapter routing
> table. Each token, when matched against the PR title or body
> (case-insensitive substring; tokens ≥ 4 chars unless they're known
> short codes like `429`, `410`), maps to exactly one chapter and
> anchor. Earlier rows win on conflict.

| Keyword token | → Chapter#anchor |
|---|---|
| `gateway` | `02-gateway-layer.md#gateway-layer` |
| `routing` | `02-gateway-layer.md#routing` |
| `address cache` | `02-gateway-layer.md#address-cache` |
| `410` | `17-status-codes-and-sdk-retries.md#410-gone` |
| `gone` | `17-status-codes-and-sdk-retries.md#410-gone` |
| `backend` | `03-backend-storage-engine.md#backend-storage-engine` |
| `storage engine` | `03-backend-storage-engine.md#backend-storage-engine` |
| `ese` | `03-backend-storage-engine.md#ese` |
| `partition reseeding` | `03-backend-storage-engine.md#partition-reseeding` |
| `partitioning` | `04-partitioning.md#partitioning` |
| `partition key` | `04-partitioning.md#partition-key` |
| `physical partition` | `04-partitioning.md#physical-partition` |
| `consistency` | `05-replication-consistency.md#consistency-levels` |
| `consistency level` | `05-replication-consistency.md#consistency-levels` |
| `consistencywriter` | `05-replication-consistency.md#consistencywriter` |
| `consistencyreader` | `05-replication-consistency.md#consistencyreader` |
| `vectorsessiontoken` | `05-replication-consistency.md#vectorsessiontoken` |
| `replication` | `05-replication-consistency.md#replication-internals` |
| `rntbd` | `06-transport-layer-rntbd.md#rntbd` |
| `transport` | `06-transport-layer-rntbd.md#transport-layer-rntbd` |
| `direct mode` | `06-transport-layer-rntbd.md#direct-mode` |
| `channelpool` | `06-transport-layer-rntbd.md#channelpool` |
| `query plan` | `07-query-engine.md#query-plan` |
| `executequery` | `07-query-engine.md#executequery` |
| `orderby` | `07-query-engine.md#orderby` |
| `groupby` | `07-query-engine.md#groupby` |
| `distinct` | `07-query-engine.md#distinct` |
| `indexing` | `08-indexing-engine.md#indexing-engine` |
| `index policy` | `08-indexing-engine.md#index-policy` |
| `composite index` | `08-indexing-engine.md#composite-index` |
| `global distribution` | `09-global-distribution.md#global-distribution` |
| `multi-region` | `09-global-distribution.md#multi-region-routing` |
| `location cache` | `09-global-distribution.md#location-cache` |
| `global endpoint` | `09-global-distribution.md#global-endpoint` |
| `control plane` | `10-control-plane.md#control-plane` |
| `resource provider` | `10-control-plane.md#resource-provider` |
| `provisioning` | `10-control-plane.md#provisioning` |
| `region management` | `10-control-plane.md#region-management` |
| `request units` | `11-request-units-throttling.md#request-units` |
| `throttle` | `11-request-units-throttling.md#throttling` |
| `throttling` | `11-request-units-throttling.md#throttling` |
| `429` | `11-request-units-throttling.md#throttling` |
| `change feed` | `12-change-feed.md#change-feed` |
| `lease` | `12-change-feed.md#lease` |
| `leasestoremanager` | `12-change-feed.md#leasestoremanager` |
| `partition merge` | `12-change-feed.md#partition-merge` |
| `startfrom` | `12-change-feed.md#startfrom` |
| `multi-api` | `13-multi-api-surface.md#multi-api-surface` |
| `sql api` | `13-multi-api-surface.md#sql-api` |
| `mongo api` | `13-multi-api-surface.md#mongo-api` |
| `fabric native` | `15-fabric-native-cosmosdb.md#fabric-native-cosmosdb` |
| `fabric mirroring` | `15-fabric-native-cosmosdb.md#fabric-mirroring` |
| `session token` | `16-session-tokens.md#session-tokens` |
| `session container` | `16-session-tokens.md#session-container` |
| `session cache` | `16-session-tokens.md#session-cache` |
| `403` | `17-status-codes-and-sdk-retries.md#403-forbidden` |
| `449` | `17-status-codes-and-sdk-retries.md#449-retry-with` |
| `503` | `17-status-codes-and-sdk-retries.md#503-service-unavailable` |
| `retrywith` | `17-status-codes-and-sdk-retries.md#449-retry-with` |
| `retry policy` | `17-status-codes-and-sdk-retries.md#retry-policies` |
| `status code` | `17-status-codes-and-sdk-retries.md#status-codes-and-sdk-retries` |
| `network error` | `17-status-codes-and-sdk-retries.md#network-errors` |

**Verification step.** Anchors above are derived from chapter file `##` headings using GitHub's anchor convention (lowercase, spaces → `-`, drop punctuation). Before citing in a finding, the reviewer should `view ~/Data/microsoft/sandbox/cosmosdb-design-docs/<chapter>.md` and confirm the anchor exists. If the anchor name has drifted, fall back to the chapter's `default_anchor` from the inventory table and append `(anchor drift; verify)` to the citation.

---

## Maintenance rules

- When a new chapter is added to `cosmosdb-design-docs`, add a row to the inventory **and** add at least one curated entry per active SDK.
- When a finding cites a chapter that does not exist in the local clone, the router asks the user to run `git -C ~/Data/microsoft/sandbox/cosmosdb-design-docs pull` and retries.
- When the reviewer suggests a new concept that should be documented, the suggestion finding includes the proposed chapter name (existing or new).
- Per `copilot-instructions.md`: prefer extending an existing numbered chapter over creating a new file.
