# Distributed Transactions (DTx) — Test Coverage Matrix

> Purpose: give reviewers and colleagues a single, version-controlled view of what
> DTx behavior is tested, where, and what gaps remain. This file lives next to the
> E2E suites and travels with every DTx PR.
>
> Last updated: see git blame. Branch of record: `jeet1995/dtx-fault-injection-tests`.

## Legend
- ✅ Covered — behavior asserted by one or more tests.
- 🟡 Partial — related coverage exists but the specific assertion below is missing.
- 🔬 Gap (this session) — being added under G1–G10 (see Gap section).
- ⏸ Deferred — known gap intentionally not addressed yet (tracked separately).

## Test files
| File | Kind | ~Count |
| --- | --- | --- |
| `DistributedReadTransactionE2ETests.cs` | E2E | 24 |
| `DistributedWriteTransactionE2ETests.cs` | E2E | 10 |
| `DistributedTransactionE2ETests.cs` | E2E (session tokens) | 15 |
| `DistributedTransactionConditionalE2ETests.cs` | E2E (eTag/conditional) | 14 |
| `DistributedTransactionPrecisionE2ETests.cs` | E2E (precision + non-string PK) | 9 |
| `DistributedTransactionFaultInjectionE2ETests.cs` | E2E (fault injection) | 10 |
| `DistributedTransactionTests.cs` | E2E + serialize | 29 |
| `DistributedReadTransactionCoreTests.cs` | Unit | 30 |
| `DistributedTransactionCommitterTests.cs` | Unit | 74 |
| `DistributedTransactionResponseTests.cs` | Unit | 72 |
| `DistributedTransactionSerializerTests.cs` | Unit | 33 |
| `DistributedWriteTransactionTests.cs` | Unit | 36 |
| `DistributedTransactionConstantsTests.cs` / `...ServerRequestTests.cs` | Unit | 6 |

## Coverage by capability

### Read transactions
| Behavior | Status | Representative tests |
| --- | --- | --- |
| Single-item / multi-item / multi-partition read | ✅ | `ReadTransaction_SingleItem`, `_MultiPartition_MultiItem`, `_CrossPk_LargeFanout` |
| Cross-container / cross-database read | ✅ | `ReadTransaction_CrossContainer`, `_CrossDatabase_AllExist/_MixedExistence` |
| Existence permutations (404 / 424 dependency) | ✅ | `_TwoExistingOneMissing_424_424_404`, `_OneExistingTwoMissing_424_404_404` |
| Auth modes (master key / resource tokens) | ✅ | `_MasterKey`, `_ReadOnlyResourceToken`, `_ReadWriteResourceToken` |
| Zero-op / invalid container guards | ✅ | `_NoOps_Throws`, `_InvalidContainer` |
| Out-of-order / duplicate response index | ✅ | `CommitAsync_OutOfOrderOperationResponses_SortedByIndex`, `_DuplicateOperationIndex_FailsClosed` |

### Write transactions
| Behavior | Status | Representative tests |
| --- | --- | --- |
| Single-PK multi-op, mixed op types | ✅ | `WriteTransaction_SinglePk_MultiOp`, `_MixedOpTypes` |
| Cross-PK 2PC, cross-container, cross-database | ✅ | `_CrossPk_2PC`, `_CrossContainer_2PC`, `_CrossDatabase_2PC` |
| Idempotency token | ✅ | `CommitAsync_SetsIdempotencyTokenHeader`, `_ResponseContainsIdempotencyToken` |
| Two-call / post-failure / concurrent guards | ✅ | `CommitAsync_CalledTwice_Throws`, `_ConcurrentCalls_OnlyOneSucceeds` |

### Partition-key typing / EPK routing  — **PR 2185283**
| Behavior | Status | Representative tests |
| --- | --- | --- |
| String PK precision round-trip | ✅ | `ReadThenWrite_WonkyDocuments_StringPartitionKey` |
| Numeric PK round-trip | ✅ | `_NumericPartitionKey`, `ExtremeScalars_NumericPk` |
| Boolean PK round-trip | ✅ | `_BooleanPartitionKey`, `HeterogeneousShapes_BooleanPk` |
| PK serialization shape (value / null / None sentinel) | ✅ | `CreateItem_SerializedBody_PartitionKey_*` |
| Cross-PK fanout | ✅ | `WriteTransaction_CrossPk_LargeFanout`, `ReadTransaction_CrossPk_MixedExistence` |
| **Hierarchical / multi-hash (sub-partition) PK E2E** | 🔬 G1 | (unit-only today: `ApplyTokens_PartialHierarchicalPartitionKey`) |
| **Numeric EPK precision boundary (1 vs 1.0; > 2^53)** | 🔬 G2 | — |
| **Null-value PK routing E2E** | 🔬 G3 | (unit-only: `ApplyTokens_NoneOrDefaultPartitionKey`) |
| **Type discrimination: string "1" vs number 1 → distinct EPK** | 🔬 G4 | — |

### Session tokens  — **PR 2188123**
| Behavior | Status | Representative tests |
| --- | --- | --- |
| Write commit returns canonical per-partition tokens | ✅ | `DtxWrite_CommitReturnsCanonicalSessionTokens`, `_MultiPartition_PerPartitionTokensReturnedByCoordinator` |
| Tokens merged per collection / per database | ✅ | `_MultiContainer_SessionTokensMergedPerCollection`, `_MultiDatabase_...PerDatabase` |
| Write token round-trips into subsequent DTx read | ✅ | `DtxWrite_TokenRoundTrip_IntoSubsequentDtxRead` |
| Read-only response returns tokens | ✅ | `DtxRead_OnlyResponse_ReturnsSessionTokens` |
| Invalid / garbled pkRangeId handled gracefully | ✅ | `DtxRead_WithInvalidPkRangeId_GracefulBehavior`, `_WithGarbledToken_DoesNotCrash` |
| Canonical/malformed/whitespace token preserved (parse) | ✅ | `FromResponseMessage_OperationResult_SessionToken_*` (unit) |
| Per-partition read stamp when routing resolves | ✅ | `CommitTransactionAsync_ReadStampsPerPartitionToken_WhenRoutingMapResolvesRange` (unit) |
| **Multi-partition read-tx: per-op `{pkRangeId}:{token}` prefix, distinct per op (E2E)** | 🔬 G5 | — |
| **Read-tx missing-pkRangeId raw-token fallback (E2E)** | 🔬 G6 | (unit-only: `_MalformedTokenPreservedAsIs`) |
| **Read-tx token consumed by subsequent read (session guarantee, READ side)** | 🔬 G7 | — |

### eTag / conditional operations  — **Objective 4**
| Behavior | Status | Representative tests |
| --- | --- | --- |
| IfMatch / IfNoneMatch serialization (all op types) | ✅ | `DistributedTransactionSerializerTests` eTag block |
| Stale IfMatch → 412; current IfMatch → success | ✅ | `WriteDtx_WithStaleIfMatch_412`, `_WithCurrentIfMatch_Succeeds` |
| IfNoneMatch read semantics (200 / 207 / 304) | ✅ | `ReadDtx_*IfNoneMatch*` |
| Both conditionals set → correct precedence | ✅ | `WriteDtx_BothConditionalsSet_*`, `ReadDtx_BothConditionalsSet_*` |
| Patch filter predicate (satisfied / unsatisfied 412) | ✅ | `WriteDtx_PatchWith[Un]SatisfiedFilterPredicate` |
| Unsatisfied predicate rolls back sibling write | ✅ | `WriteDtx_UnsatisfiedFilterPredicate_RollsBackSiblingWrite_412` |
| ETag deserialized from per-op result | ✅ | `FromResponseMessage_OperationResult_ETag_DeserializesCorrectly` (unit) |
| **eTag optimistic-concurrency round-trip (DTx-returned eTag → next IfMatch)** | 🔬 G8 | — |
| **IfNoneMatch=\* create-guard in write DTx** | 🔬 G9 | — |
| **Multi-op mixed IfMatch (one stale) rollback in write DTx** | 🔬 G10 | 🟡 partial via filter-predicate rollback |

### Fault injection & retry
| Behavior | Status | Representative tests |
| --- | --- | --- |
| Retriable server error (once / thrice / disabled rule) | ✅ | `WriteTransaction_RetriableServerError*` |
| Connect timeout, response delay, cancellation | ✅ | `_PreSendConnectTimeout`, `_CancellationDuringInjectedResponseDelay` |
| Terminal 400 / 452 aborted / 207 multi-status | ✅ | `_Terminal400Mismatch`, `_Aborted452`, `_MultiStatus207` |
| Retry budget (attempt cap 10 / cumulative delay) | ✅ | `CommitTransaction_ExhaustsIsRetriableRetryBudget`, `_ExhaustsCumulativeDelayBudget` |

## Gaps addressed this session (G1–G10)
| ID | Requirement | Source | Target file |
| --- | --- | --- | --- |
| G1 | Hierarchical / multi-hash PK write+read DTx | PR 2185283 | `DistributedTransactionPrecisionE2ETests.cs` |
| G2 | Numeric EPK precision boundary routing | PR 2185283 | `DistributedTransactionPrecisionE2ETests.cs` |
| G3 | Null-value PK routing E2E | PR 2185283 | `DistributedTransactionPrecisionE2ETests.cs` |
| G4 | string "1" vs number 1 → distinct EPK | PR 2185283 | `DistributedTransactionPrecisionE2ETests.cs` |
| G5 | Multi-partition read-tx per-op pkRangeId prefix | PR 2188123 | `DistributedTransactionE2ETests.cs` |
| G6 | Read-tx missing-pkRangeId raw fallback | PR 2188123 | `DistributedTransactionE2ETests.cs` |
| G7 | Read-tx token round-trip (session guarantee) | PR 2188123 | `DistributedTransactionE2ETests.cs` |
| G8 | eTag optimistic-concurrency round-trip | Obj 4 | `DistributedTransactionConditionalE2ETests.cs` |
| G9 | IfNoneMatch=\* create-guard | Obj 4 | `DistributedTransactionConditionalE2ETests.cs` |
| G10 | Multi-op mixed IfMatch rollback | Obj 4 | `DistributedTransactionConditionalE2ETests.cs` |

## Deferred / open
- ⏸ Depth-32 nested-doc Read-DTx returns 408 on cold collection (precision suite). Mitigation: write-DTx warm-up. Root-cause disposition open.
- ⏸ NRegion synchronous read/commit feature-flag resolution (PR 2185283) — pending confirmation whether SDK-observable; if server-internal only, no SDK test applies.
