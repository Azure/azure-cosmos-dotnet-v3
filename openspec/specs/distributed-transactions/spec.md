# Distributed Transactions

> **Status**: This spec is evolving alongside active development. The distributed transactions feature is under active implementation. Update this spec as the design solidifies.

## Purpose

Distributed transactions extend the Cosmos DB .NET SDK to support cross-partition transactional operations. Unlike `TransactionalBatch` which is scoped to a single partition key, distributed transactions coordinate atomic operations across multiple partitions.

## Current State

The distributed transactions feature is being actively developed with the following components:

### Key Source Files

- `Microsoft.Azure.Cosmos/src/DistributedTransaction/` — Core DTS implementation
- Includes: `DistributedTransaction.cs`, `DistributedWriteTransaction.cs`, `DistributedTransactionCommitter.cs`

### Known Implementation Details

1. **DTS routing**: Centralized request routing with constants for operation types and resource types.
2. **Operation type serialization**: Custom serialization for DTS-specific operation types.
3. **Partition key serialization**: Support for partition key serialization across transaction boundaries.
4. **Direct package integration**: Requires specific `Microsoft.Azure.Cosmos.Direct` package versions for DTS support.

## Requirements (Preliminary)

### Requirement: Cross-Partition Atomicity

The SDK SHALL support atomic operations across multiple partition key ranges.

**When** a distributed transaction is committed, all operations SHALL commit or roll back together, even across partition key ranges.

### Requirement: Coordination Protocol

The SDK SHALL coordinate distributed transactions via a two-phase commit-like protocol.

**When** a distributed transaction is executed, the SDK SHALL coordinate with the service using a two-phase commit-like protocol.

### Requirement: Resource Type Scope

The SDK SHALL scope distributed transactions to document operations.

**When** operations are added to a distributed transaction, only `ResourceType.Document` items SHALL be supported.

## Open Questions

- What are the size and operation count limits?
- What is the latency overhead compared to single-partition batch?
- How does DTS interact with availability strategies (hedging)?
- What retry semantics apply to distributed transactions?
- What consistency levels are supported?

## References

- Source: `Microsoft.Azure.Cosmos/src/DistributedTransaction/`
- Related PRs: #5624, #5607, #5619, #5615, #5576