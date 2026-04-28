# Azure Cosmos DB .NET SDK — Feature Specifications

Behavioral specifications for all major features of the Azure Cosmos DB .NET SDK v3. Each spec uses [EARS notation](https://en.wikipedia.org/wiki/Easy_Approach_to_Requirements_Syntax) (WHEN/THEN/SHALL) and includes public API surface, reference tables, and cross-spec interaction links.

## How to Use These Specs

- **Before implementing a feature**: Read the relevant spec to understand requirements and edge cases
- **During code review**: Validate PRs against the spec's requirements
- **For AI agents**: Reference specs as authoritative context for implementation, testing, and review tasks
- **For onboarding**: Use specs to understand how each SDK feature is supposed to behave

## Index by Area

### Data Operations

| Spec | Description |
|------|-------------|
| [CRUD Operations](crud-operations/spec.md) | Point reads, creates, upserts, replaces, deletes, ReadMany |
| [Patch Operations](patch-operations/spec.md) | Partial document modifications (add, remove, replace, set, increment, move) |
| [Query and LINQ](query-and-linq/spec.md) | SQL queries, LINQ, cross-partition, pagination, FeedIterator |
| [Change Feed](change-feed/spec.md) | Change feed iterator, processor, estimator, modes, start positions |
| [Batch and Transactional](batch-and-transactional/spec.md) | TransactionalBatch (atomic) and bulk execution (throughput-optimized) |
| [Distributed Transactions](distributed-transactions/spec.md) | Cross-partition transactional operations (evolving) |

### Routing & Availability

| Spec | Description |
|------|-------------|
| [Retry and Failover](retry-and-failover/spec.md) | Throttling retry (429), region failover, PPAF, Gone (410) handling |
| [Cross-Region Hedging](cross-region-hedging/spec.md) | AvailabilityStrategy, threshold-based hedging, response selection |
| [Partition Keys](partition-keys/spec.md) | Partition keys, hierarchical keys, FeedRange, partition routing |

### Transport & Configuration

| Spec | Description |
|------|-------------|
| [Transport and Connectivity](transport-and-connectivity/spec.md) | Gateway vs Direct mode, TCP tuning, endpoint discovery |
| [Client and Configuration](client-and-configuration/spec.md) | CosmosClient lifecycle, authentication, custom handlers, builder |
| [Consistency and Session](consistency-and-session/spec.md) | Five consistency levels, session token management |
| [Handler Pipeline](handler-pipeline/spec.md) | Chain-of-responsibility request pipeline, handler ordering |

### Serialization & Diagnostics

| Spec | Description |
|------|-------------|
| [Serialization](serialization/spec.md) | CosmosSerializer, JSON.NET, System.Text.Json, LINQ serialization |
| [Diagnostics and Observability](diagnostics-and-observability/spec.md) | CosmosDiagnostics, trace tree, OpenTelemetry, metrics, DiagnosticsVerbosity (Summary/Detailed) |

### Security & Management

| Spec | Description |
|------|-------------|
| [Client-Side Encryption](client-side-encryption/spec.md) | Encryption keys, policies, transparent encrypt/decrypt |
| [Container and Database Management](container-and-database-management/spec.md) | Database/container CRUD, throughput, indexing |

## Related Documentation

- [OpenSpec README](../README.md) — Developer guide and workflow
- [SdkDesignGuidelines.md](../../SdkDesignGuidelines.md) — Public API contract rules
- [docs/SdkDesign.md](../../docs/SdkDesign.md) — SDK architecture overview
- [openspec/config.yaml](../config.yaml) — OpenSpec project configuration and rules