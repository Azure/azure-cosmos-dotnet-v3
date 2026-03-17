# Azure Cosmos DB .NET SDK — Feature Specifications

This directory contains behavioral specifications for all major features of the Azure Cosmos DB .NET SDK v3. Each spec uses [EARS notation](https://en.wikipedia.org/wiki/Easy_Approach_to_Requirements_Syntax) (WHEN/THEN/SHALL) and structured scenarios that are testable, reviewable, and consumable by AI agents.

## How to Use These Specs

- **Before implementing a feature**: Read the relevant spec to understand requirements and edge cases
- **During code review**: Validate PRs against the spec's scenarios and requirements
- **For AI agents**: Reference specs as authoritative context for implementation, testing, and review tasks
- **For onboarding**: Use specs to understand how each SDK feature is supposed to behave

## Spec Format

Each spec follows a consistent structure:
- **Purpose**: What the feature does at a high level
- **Requirements**: Behavioral requirements using RFC 2119 keywords (SHALL, MUST, SHOULD, MAY)
- **Scenarios**: Concrete Given/When/Then examples for each requirement
- **Key Source Files**: Links to implementation code

## Index by Area

### Data Operations
| Spec | Description |
|------|-------------|
| [CRUD Operations](crud-operations/spec.md) | Point reads, creates, upserts, replaces, deletes, patches, ReadMany |
| [Query Execution](query-execution/spec.md) | SQL queries, LINQ, cross-partition, pagination, FeedIterator |
| [Change Feed](change-feed/spec.md) | Change feed iterator, processor, estimator, modes, start positions |
| [Bulk and Batch](bulk-and-batch/spec.md) | TransactionalBatch (atomic) and bulk execution (throughput-optimized) |

### Routing & Availability
| Spec | Description |
|------|-------------|
| [Retry and Failover](retry-and-failover/spec.md) | Throttling retry (429), region failover, PPAF, Gone (410) handling |
| [Cross-Region Hedging](cross-region-hedging/spec.md) | AvailabilityStrategy, threshold-based hedging, response selection |
| [Partitioning](partitioning/spec.md) | Partition keys, hierarchical keys, FeedRange, partition routing |

### Transport & Configuration
| Spec | Description |
|------|-------------|
| [Transport and Connectivity](transport-and-connectivity/spec.md) | Gateway vs Direct mode, TCP tuning, endpoint discovery |
| [Client Configuration](client-configuration/spec.md) | CosmosClient lifecycle, authentication, custom handlers, builder |
| [Consistency and Session](consistency-and-session/spec.md) | Five consistency levels, session token management |

### Serialization & Diagnostics
| Spec | Description |
|------|-------------|
| [Serialization](serialization/spec.md) | CosmosSerializer, JSON.NET, System.Text.Json, LINQ serialization |
| [Diagnostics and Tracing](diagnostics-and-tracing/spec.md) | CosmosDiagnostics, trace tree, OpenTelemetry, metrics |

### Security
| Spec | Description |
|------|-------------|
| [Client-Side Encryption](client-side-encryption/spec.md) | Encryption keys, policies, transparent encrypt/decrypt |
| [Resource Management](resource-management/spec.md) | Database/container CRUD, throughput, indexing, TTL, vector/full-text search |

## Creating New Specs

For new features, use the OpenSpec workflow:
```
/opsx:propose <feature-name>
```

This creates a change folder in `openspec/changes/<feature-name>/` with proposal, specs, design, and tasks. When the feature ships and the change is archived, its delta specs merge into the appropriate spec file here.

## Related Documentation

- [SdkDesignGuidelines.md](../../SdkDesignGuidelines.md) — public API contract rules
- [docs/SdkDesign.md](../../docs/SdkDesign.md) — SDK architecture overview
- [docs/](../../docs/) — existing design documents
- [openspec/config.yaml](../config.yaml) — OpenSpec project configuration and rules
