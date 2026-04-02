# Deep Research: Changing Replication Policy from 4 Replicas to 3 Replicas

## Executive Summary

Reducing `MaxReplicaSetSize` (NMax) from **4 → 3** in Azure Cosmos DB has **significant implications** for quorum logic, write latency, failure tolerance boundaries, and several SDK code paths. The **read quorum remains unchanged at 2**, but the **write quorum drops from 3 → 2**, which fundamentally changes the durability-before-ack guarantee and invalidates assumptions baked into the Bounded Staleness read path.

### Quantified Impact by Consistency Level

| Consistency Level | Primary Impact | Severity | Key Number |
|---|---|---|---|
| **Strong** | Secondary-only quorum has **zero spare** → 1 secondary failure adds **+1-2 round-trips**; secondary-only barrier has **zero margin** | 🔴 High | 0 spare secondaries |
| **Bounded Staleness** | Same as Strong + W=2 permanent violates documented assumption | 🔴 High | W:3→2 permanent |
| **Session** | **25% fewer replicas** tried per session-find cycle → higher SESSION_NOT_FOUND (404/1002) probability; ~30-36 replica reads over 5s vs ~40-48 | 🟡 Medium | −25% attempts |
| **Consistent Prefix** | No meaningful impact | ✅ Low | N/A |
| **Eventual** | No meaningful impact | ✅ Low | N/A |

### Top 3 Risks
1. **Secondary-only barrier at `ReadQuorumAsync:372`** tolerates ZERO secondary failures with NMax=3 (§3.3.4)
2. **Timeout-driven failure detection:** 1 unresponsive secondary → **~6s+ latency penalty** on 100% of Strong/BoundedStaleness reads (vs 67% with NMax=4) — most replica failures are timeouts, not immediate errors (§3.3.1)
3. **Connectivity detection** (`minFailedReplicaCount=3`) only triggers at 100% failure with NMax=3 (§5)

---

## 1. Quorum Math — The Core Impact

### Formula (from `ConsistencyReader.cs:208`)
```
readQuorumValue = maxReplicaCount - (maxReplicaCount / 2)   // integer division
```

### Write Quorum (from `ConsistencyWriter.cs` comments)
```
W = N/2 + 1   // majority quorum
```

### Comparison Table

| Property | NMax = 4 | NMax = 3 | Impact |
|----------|----------|----------|--------|
| **Read Quorum (R)** | 4 - 2 = **2** | 3 - 1 = **2** | ✅ **No change** |
| **Write Quorum (W)** | 4/2 + 1 = **3** | 3/2 + 1 = **2** | ⚠️ **Drops by 1** |
| **Simultaneous Failure Tolerance** | 1 failure | 1 failure | ✅ **No change** |
| **Write Latency** | Wait for 3 acks | Wait for 2 acks | 🔽 **Lower tail latency** |
| **Durability-before-ack** | 3 copies confirmed | 2 copies confirmed | ⚠️ **Less durable at ack time** |

> **Source:** `ConsistencyReader.cs:38-56` — "N = 4 allows for 1 failure; N = 3 allows for 1 failure"

---

## 2. Write Path Impact (`ConsistencyWriter.cs`)

### Local Quorum-Acked Writes
- With NMax=4, the primary waits for **W=3** replicas (including itself) to commit before returning success.
- With NMax=3, only **W=2** acks are needed — the primary + **1 secondary** is sufficient.
- **Effect:** Write latency decreases (fewer acks needed), but if the primary dies after ack and the single secondary that acked also fails before full replication, the write could be lost.

### Globally Strong Writes
- After local quorum ack, `ConsistencyWriter` issues barrier requests until `GlobalCommittedLSN >= LSN`.
- The barrier logic itself is **unaffected** — it still waits for global convergence regardless of local replica count.
- However, the **local ack** that triggers the barrier check is now weaker (W=2 vs W=3).

> **Source:** `ConsistencyWriter.cs:18-49`

---

## 3. Quantified Impact by Consistency Level — SESSION_NOT_FOUND, Quorum, Barrier

This section provides **explicit quantification** of every code path affected by NMax 4→3, organized by consistency level with primary emphasis on SESSION_NOT_FOUND (404/1002), quorum mechanics, and barrier behavior.

### 3.1 Consistency Level → Code Path Mapping

#### Read Path

| Consistency Level | Handler | ReadMode | Quorum? | Read Barrier? | Session Token? | Primary Initially? |
|---|---|---|---|---|---|---|
| **Strong** | `ReadStrongAsync()` | `ReadMode.Strong` | ✅ R=2 | ✅ Yes | ❌ | ❌ Secondary-first |
| **Bounded Staleness** | `ReadStrongAsync()` | `ReadMode.BoundedStaleness` | ✅ R=2 | ✅ Yes | ❌ | ❌ Secondary-first |
| **Session** | `ReadSessionAsync()` | `ReadMode.Any` | ❌ | ❌ | ✅ | ✅ All (incl. primary) |
| **Consistent Prefix** | `ReadAnyAsync()` | `ReadMode.Any` | ❌ | ❌ | ❌ | ✅ All |
| **Eventual** | `ReadAnyAsync()` | `ReadMode.Any` | ❌ | ❌ | ❌ | ✅ All |

> **Source:** `ConsistencyReader.cs:369-388`

#### Write Path

| Consistency Level | Write Quorum (W) | Write Barrier? | Barrier Type | Primary in Write? |
|---|---|---|---|---|
| **Strong** | W = N/2+1 | ✅ Yes (Global Strong) | `WaitForWriteBarrierAsync` — waits for GCLSN | ✅ Always |
| **Bounded Staleness** | W = N/2+1 | ❌ No | N/A | ✅ Always |
| **Session** | W = N/2+1 | ❌ No | N/A | ✅ Always |
| **Consistent Prefix** | W = N/2+1 | ❌ No | N/A | ✅ Always |
| **Eventual** | W = N/2+1 | ❌ No | N/A | ✅ Always |

> **Source:** `ConsistencyWriter.cs:18-49`, `ConsistencyWriter.cs:495-726`

---

### 3.2 SESSION_NOT_FOUND (404/1002) — Quantified Impact

SESSION_NOT_FOUND occurs when a replica's LSN is **lower** than the client's session token LSN (`SubStatusCodes.ReadSessionNotAvailable = 1002`, `StatusCodes.cs:87`).

#### 3.2.1 Session Read Flow — Replica Enumeration

```
ConsistencyReader.ReadSessionAsync()
  → StoreReader.ReadMultipleReplicaAsync(
      includePrimary: true,        ← ALL replicas in pool
      replicaCountToRead: 1,       ← Stop at FIRST valid match
      useSessionToken: true,       ← Validates LSN against session token
      checkMinLSN: true)           ← Enables session token validation
    → AddressSelector resolves ALL replicas
    → AddressEnumerator randomizes order, moves unhealthy to end
    → Loop: try replica one-by-one until session token match
```

> **Source:** `ConsistencyReader.cs:305-312`, `StoreReader.cs:162-408`

**When a replica returns 404/1002:** The loop **CONTINUES** to the next replica. The 404/1002 is captured as `sessionNotFoundStoreResult` (line 347) for potential exceptionless return, and the enumerator advances. The loop only stops when `storeResultList.Count >= 1` or all replicas are exhausted.

> **Source:** `StoreReader.cs:330-370`

#### 3.2.2 Replica Attempt Count — Direct NMax Dependency

| Metric | NMax=4 | NMax=3 | Delta |
|--------|--------|--------|-------|
| **Total replicas in pool** | 4 (1P + 3S) | 3 (1P + 2S) | **−1 attempt** |
| **Maximum sequential attempts** | 4 | 3 | **−25% fewer chances** |
| **Secondaries tried before primary** | Up to 3 | Up to 2 | **−1 secondary** |

> **Key insight:** Session reads try replicas **one at a time** in randomized health-order until finding a match. With NMax=3, the SDK has **25% fewer replicas** to attempt before declaring SESSION_NOT_FOUND. After a recent write, the replication lag window means fewer secondaries have caught up → higher probability that none of the 2 secondaries match → forced to wait for primary or fail.

#### 3.2.3 Retry Stack — Three Layers of Retry for 404/1002

When all replicas return 404/1002 (session not found), three retry layers activate:

**Layer 1: `SessionTokenMismatchRetryPolicy`** (`SessionTokenMismatchRetryPolicy.cs:127-207`)

| Parameter | Value |
|-----------|-------|
| Total retry budget | **5,000 ms** (5 seconds) |
| Initial backoff | 5 ms |
| Backoff multiplier | 5× |
| Maximum backoff | 500 ms |
| **Backoff sequence** | 0ms → 5ms → 25ms → 125ms → 500ms → 500ms → 500ms... |

Each retry re-executes `ReadSessionAsync()`, which re-enumerates ALL replicas again. With NMax=3, each retry cycle contacts 3 replicas vs 4.

**Over 5 seconds with NMax=4:** ~10-12 full retry cycles × 4 replicas/cycle = **~40-48 replica reads**
**Over 5 seconds with NMax=3:** ~10-12 full retry cycles × 3 replicas/cycle = **~30-36 replica reads** (25% fewer)

**Layer 2: `ClientRetryPolicy.ShouldRetryOnSessionNotAvailable`** (`ClientRetryPolicy.cs:425-477`)

| Account Type | Max Retries | Behavior |
|---|---|---|
| **Single-master** | **1 retry** | Retries once on same region, then gives up |
| **Multi-master** | **1 per region** | Tries each available region; stops after all exhausted |

> `sessionTokenRetryCount > 1` → `NoRetry()` for single-master (line 459)
> `sessionTokenRetryCount > endpoints.Count` → `NoRetry()` for multi-master (line 444)

**Layer 3: `RenameCollectionAwareClientRetryPolicy`** (`RenameCollectionAwareClientRetryPolicy.cs:82-91`)

On name-based requests, 404/1002 may indicate collection recreated. Clears cached session token and retries once.

#### 3.2.4 SESSION_NOT_FOUND Probability Model

Consider a write that just completed on primary. Replication lag to secondaries = Δt.

| Scenario | NMax=4 (3 secondaries) | NMax=3 (2 secondaries) |
|---|---|---|
| P(at least 1 secondary caught up within Δt) | 1 − (1−p)³ | 1 − (1−p)² |
| **If p=0.7 per secondary** | 97.3% success | 91.0% success |
| **If p=0.5 per secondary** | 87.5% success | 75.0% success |
| **If p=0.3 per secondary** | 65.7% success | 51.0% success |

Where p = probability that a given secondary has replicated the write within the read latency window.

**Bottom line:** With NMax=3, the probability of hitting SESSION_NOT_FOUND on the first attempt is **meaningfully higher** during replication lag windows, requiring more retry cycles and increasing P99 latency.

#### 3.2.5 Exceptionless 404/1002 Path

When `UseStatusCodeFor4041002 = true` (`DocumentServiceRequestExtensions.cs:63-67`), the SDK captures the first 404/1002 as `sessionNotFoundStoreResult` (`StoreReader.cs:340-348`) and returns it as a status code instead of throwing `NotFoundException`. This avoids exception overhead but **does not change the retry semantics** — the `SessionTokenMismatchRetryPolicy` still drives retries.

---

### 3.3 Strong Consistency — Quorum & Barrier Quantified

#### 3.3.1 Quorum Read — Initial Phase

```
ReadStrongAsync (QuorumReader.cs:82-254)
  Step 1: ReadQuorumAsync(includePrimary=false, readQuorum=2)
    → StoreReader contacts R=2 replicas from SECONDARIES ONLY
    → IsQuorumMet: needs ≥2 replicas at same LSN (replicaCountMaxLsn >= readQuorum)
```

**Parallel read behavior** (`StoreReader.cs:226-269`):
- First batch: dispatches `min(replicaCountToRead, available)` parallel reads
- If any fail: dispatches remaining replicas one-by-one

| Metric | NMax=4 (3 secondaries) | NMax=3 (2 secondaries) | Impact |
|---|---|---|---|
| Initial parallel reads | 2 of 3 secondaries | 2 of 2 secondaries | Same count |
| **Spare secondaries if 1 fails** | **1 spare** (try 3rd) | **0 spare** → QuorumNotSelected | 🔴 **No recovery** |
| QuorumMet on first attempt (healthy) | ✅ 2 of 3 respond | ✅ 2 of 2 respond | ✅ Same |
| QuorumMet with 1 slow secondary | ✅ 2 of 3 respond (skip slow one) | ❌ Only 1 responds → retry | 🔴 **Extra round-trip** |

> ⚠️ **Timeout-Driven Failure Detection — The Hidden Latency Cost**
>
> Most replica failures are detected through **timeouts, not immediate errors**. The SDK does not learn a secondary is down until the request timeout expires. Key timeout values:
>
> | Timeout | Default | Source |
> |---------|---------|--------|
> | Request timeout (direct mode) | **6 seconds** | `ConnectionPolicy.cs:21` |
> | TCP connection open timeout | **5 seconds** | `DocumentClient.cs:145` |
>
> **Impact on the "+1-2 RT" estimates above:** When StoreReader dispatches 2 parallel reads to secondaries and one secondary is unresponsive:
> - **NMax=4:** 2 of 3 secondaries are read. If the failing secondary is one of the 2 chosen, the SDK waits **up to 6 seconds** for the timeout — but the other chosen secondary may respond fast. If both chosen happen to be healthy (2 of 3 chance), no wait at all. If one is unhealthy, the spare 3rd secondary can be tried after the timeout.
> - **NMax=3:** Both secondaries are read (no choice). If 1 is unresponsive, the SDK **must wait the full timeout (~6s)** before it gets only 1 valid response → QuorumNotSelected → primary fallback → another round-trip.
>
> **Real-world latency for 1 secondary failure:**
>
> | Scenario | NMax=4 | NMax=3 |
> |---|---|---|
> | Failing secondary NOT in initial batch (lucky) | ~normal latency | N/A (both always chosen) |
> | Failing secondary in initial batch | ~6s wait + try spare 3rd | **~6s wait + QuorumNotSelected + primary fallback** |
> | **Probability of hitting timeout** | **2/3** (2 of 3 chosen) | **100%** (always) |
> | **Expected added latency (P50)** | ~4s (2/3 × 6s) | **~6s+ (always)** |
>
> This transforms the "+1-2 RT" cost from a **millisecond-scale** concern into a **multi-second tail latency** event. With NMax=3, **every** Strong/BoundedStaleness read during a single-secondary-failure window pays the full timeout penalty.

#### 3.3.2 IsQuorumMet — Two Conditions (`QuorumReader.cs:876-968`)

**Condition 1** (line 947): `replicaCountMaxLsn >= readQuorum` — at least R replicas at **exact same** max LSN
**Condition 2** (line 953): `itemLSN <= minLSN` of all R responses — point-read staleness fallback

Both require **R=2 valid responses**. The quorum value is identical for NMax=3 and NMax=4. The impact is entirely about **how many replicas are available** to produce those 2 responses.

#### 3.3.3 QuorumNotSelected → Primary Fallback → Retry Loop

When secondary-only read fails to meet quorum:

```
QuorumNotSelected (line 167)
  → ReadPrimaryAsync (line 186)
    → If CurrentReplicaSetSize > readQuorum: shouldRetryOnSecondary=true (line 491)
    → Set includePrimary=true (line 220)
    → Retry ReadQuorumAsync with primary included
```

**Retry budget:** `maxNumberOfReadQuorumRetries = 6` (`QuorumReader.cs:54`)

| Scenario | NMax=4 | NMax=3 | Latency Cost |
|---|---|---|---|
| **All healthy** | 1 RT (2 of 3 secondaries) | 1 RT (2 of 2 secondaries) | Same |
| **1 secondary unresponsive** | 2/3 chance: ~6s timeout + try spare 3rd; 1/3 chance: no impact | **100% chance: ~6s timeout** + QuorumNotSelected → ReadPrimary → retry | **+6-7s (always)** vs **+4s avg (sometimes)** |
| **1 secondary returning errors** | 1 RT (immediate fail → try 3rd secondary) | 2-3 RT (QuorumNotSelected → primary fallback) | **+1-2 RT** (ms-scale) |
| **2 secondaries down** | ~6s timeout → primary fallback | ~6s timeout → primary fallback | Same |

> **Critical:** The code at `QuorumReader.cs:215-220` was written for the "reduced replica set" case (1P + 2S with 1S unreachable). With NMax=3, this describes the **normal single-failure case**, not an edge case.

#### 3.3.4 Read Barrier — Two Call Sites with Different Primary Inclusion

**Barrier Call Site 1: ReadStrongAsync line 124-130** — After QuorumSelected

```csharp
WaitForReadBarrierAsync(barrierRequest, allowPrimary: true, readQuorum: 2, ...)
```

| Parameter | Old Implementation | New Implementation |
|---|---|---|
| Phase 1 retries | **6 retries × 5ms** = 30ms | Up to 40 retries, adaptive delay |
| Phase 2 retries (GCLSN) | **30 retries** (10ms×4 + 30ms×26) = 820ms | Included in Phase 1 |
| **Total max wait** | **~850ms** | **~970ms** |
| Replicas read per retry | `readQuorum=2`, forceReadAll (all replicas) | Same |
| Success condition | `count(LSN >= barrierLsn) >= 2` AND `maxGCLSN >= target` | Same |

| Metric | NMax=4 (all replicas) | NMax=3 (all replicas) | Impact |
|---|---|---|---|
| Replicas in barrier pool | 4 (primary included) | 3 (primary included) | |
| Need 2 at target LSN | 2 of 4 → **2 spare** | 2 of 3 → **1 spare** | 🟡 Less margin |
| 1 replica lagging | Still 3 available, 2 can meet LSN | 2 available, need both to meet LSN | 🟡 **Tighter** |

**Barrier Call Site 2: ReadQuorumAsync line 370-376** — Secondary-only barrier

```csharp
WaitForReadBarrierAsync(barrierRequest, allowPrimary: false, readQuorum: 2, ...)
```

| Metric | NMax=4 (secondaries only) | NMax=3 (secondaries only) | Impact |
|---|---|---|---|
| Replicas in barrier pool | **3 secondaries** | **2 secondaries** | |
| Need 2 at target LSN | 2 of 3 → **1 spare** | 2 of 2 → **0 spare** | 🔴 **Zero margin** |
| 1 secondary lagging | Still 2 can converge → barrier succeeds | Only 1 at target → **barrier FAILS** | 🔴 **Barrier failure** |
| Barrier failure consequence | Falls to `TryPrimaryOnlyReadBarrierAsync` | Same fallback | **+1 extra barrier cycle** |

> **This is the single most fragile path with NMax=3.** The secondary-only barrier at `ReadQuorumAsync:372` tolerates **zero** secondary failures.

#### 3.3.5 Write Barrier (Global Strong Only)

```csharp
WaitForWriteBarrierAsync(barrierRequest, includePrimary: true, replicaCountToRead: 1)
```

Only activates when `IsGlobalStrongEnabled() && DefaultConsistencyLevel == Strong`.

| Parameter | Value |
|---|---|
| Replicas needed | **ANY 1** with `GlobalCommittedLSN >= writeLSN` |
| Primary included | ✅ Always |
| Old: retries | 30 retries (10ms×4 + 30ms×26) |
| New: retries | Adaptive, ~970ms total |

| Metric | NMax=4 | NMax=3 | Impact |
|---|---|---|---|
| Pool for GCLSN check | 4 replicas | 3 replicas | 🟢 Minor — only needs 1 |
| 1 replica down | 3 remaining, any 1 suffices | 2 remaining, any 1 suffices | 🟢 Low |

---

### 3.4 Bounded Staleness — Quorum & Barrier Quantified

**Identical code path to Strong** — both call `ReadStrongAsync()` with `readQuorumValue=2`. All quorum and barrier numbers from §3.3 apply directly.

**Additional concern — W=2 as permanent state:**

The critical comment at `ConsistencyReader.cs:222-231`:
```
"we are always running with majority quorum w = 3 (or 2 during quorum downshift).
 This means that the primary will always be part of the write quorum..."
```

| State | NMax=4 | NMax=3 |
|---|---|---|
| **Write quorum (W)** | W=3 (normal), W=2 (downshift) | **W=2 (always)** |
| **W=2 was** | Temporary degraded state | **Permanent normal state** |
| **Comment accuracy** | ✅ Correct | ❌ **Misleading** — "always w=3" is false |

The Bounded Staleness read path **assumes W=3** to guarantee that reads from secondaries see committed writes. With W=2 permanent:
- A write acked by primary + 1 secondary is "committed"
- If that 1 secondary is the one NOT read by the quorum check, the read may not see the write
- **Monotonic read guarantee still holds** (primary is always in W=2), but the **safety margin is zero**

**No write barrier** for Bounded Staleness — barrier is read-only.

---

### 3.5 Session Consistency — SESSION_NOT_FOUND Quantified

(Detailed in §3.2 above. Summary of key numbers here.)

| Metric | NMax=4 | NMax=3 | Impact |
|---|---|---|---|
| **Replicas tried per ReadSessionAsync call** | **4** (3S + 1P) | **3** (2S + 1P) | 🟡 25% fewer |
| **Secondaries before primary** | Up to 3 | Up to 2 | 🟡 Fewer secondary chances |
| **SessionTokenMismatchRetryPolicy budget** | 5,000 ms | 5,000 ms | ✅ Same |
| **Replica reads over 5s retry** | ~40-48 | ~30-36 | 🟡 25% fewer total |
| **ClientRetryPolicy (single-master)** | 1 region retry | 1 region retry | ✅ Same |
| **ClientRetryPolicy (multi-master)** | 1 retry per region | 1 retry per region | ✅ Same |
| **Quorum used?** | ❌ No | ❌ No | ✅ N/A |
| **Barrier used?** | ❌ No | ❌ No | ✅ N/A |

**Session reads have NO quorum or barrier dependency.** The impact is purely in the **number of replicas available** to find a session token match, which increases 404/1002 probability during replication lag.

---

### 3.6 Consistent Prefix — Quantified

| Metric | NMax=4 | NMax=3 | Impact |
|---|---|---|---|
| Replicas contacted | **1** (any, primary included) | **1** (any, primary included) | ✅ Same |
| Quorum | ❌ None | ❌ None | ✅ N/A |
| Barrier | ❌ None | ❌ None | ✅ N/A |
| Session token | ❌ Not used | ❌ Not used | ✅ N/A |
| 1 replica down | 3 remaining | 2 remaining | 🟢 Trivial |

**No meaningful impact.** Reads any single replica with no consistency checks.

---

### 3.7 Eventual Consistency — Quantified

| Metric | NMax=4 | NMax=3 | Impact |
|---|---|---|---|
| Replicas contacted | **1** (any, primary included) | **1** (any, primary included) | ✅ Same |
| Quorum | ❌ None | ❌ None | ✅ N/A |
| Barrier | ❌ None | ❌ None | ✅ N/A |
| Session token | ❌ Not used | ❌ Not used | ✅ N/A |
| 1 replica down | 3 remaining | 2 remaining | 🟢 Trivial |

**No meaningful impact.** Identical to Consistent Prefix.

---

### 3.8 Cross-Consistency Summary — Read vs Write Impact

#### READ Impact (NMax 4→3)

| Dimension | Strong | Bounded Staleness | Session | Consistent Prefix | Eventual |
|---|---|---|---|---|---|
| **Read quorum (healthy)** | ✅ R=2 same | ✅ R=2 same | N/A (no quorum) | N/A | N/A |
| **Read quorum (1S timeout failure)** | 🔴 **~6s+ penalty** (0 spare, must wait full timeout) | 🔴 **~6s+ penalty** (0 spare) | N/A | N/A | N/A |
| **Read quorum (1S error failure)** | 🟡 +1-2 RT ms-scale (0 spare, immediate detection) | 🟡 +1-2 RT ms-scale | N/A | N/A | N/A |
| **Read barrier (primary-inclusive)** | 🟡 1 spare vs 2 | 🟡 1 spare vs 2 | N/A (no barrier) | N/A | N/A |
| **Read barrier (secondary-only)** | 🔴 Zero margin | 🔴 Zero margin | N/A | N/A | N/A |
| **SESSION_NOT_FOUND rate** | N/A | N/A | 🟡 +12-25% more likely | N/A | N/A |
| **SESSION_NOT_FOUND retries** | N/A | N/A | 🟡 25% fewer reads/cycle | N/A | N/A |
| **Replica pool for reads** | 3S→2S initially | 3S→2S initially | 4→3 total | 4→3 total | 4→3 total |
| **1 replica timeout probability** | 🔴 100% hit (2 of 2) | 🔴 100% hit (2 of 2) | 🟡 Higher (3 of 3 vs 4 of 4) | 🟡 Higher | 🟡 Higher |
| **Single replica failure** | 🟢 Still functional | 🟢 Still functional | 🟢 Still functional | 🟢 Trivial | 🟢 Trivial |

> ⚠️ **Timeout-driven failure is the dominant failure mode.** Most replica failures (network partitions, process crashes, GC pauses) manifest as unresponsive replicas — NOT immediate errors. The SDK default request timeout is **6 seconds** (`ConnectionPolicy.cs:21`). With NMax=3, every Strong/BoundedStaleness read during a single-secondary-failure window **always** waits the full ~6s timeout (100% probability), vs NMax=4 where there's a 1/3 chance of avoiding the failing secondary entirely. See §3.3.1 for detailed analysis.

#### WRITE Impact (NMax 4→3)

| Dimension | Strong | Bounded Staleness | Session | Consistent Prefix | Eventual |
|---|---|---|---|---|---|
| **Write quorum (W)** | 🟡 W:3→2 | 🔴 W:3→2 permanent* | 🟡 W:3→2 | 🟡 W:3→2 | 🟡 W:3→2 |
| **Durability at ack** | 🟡 2 copies vs 3 | 🟡 2 copies vs 3 | 🟡 2 copies vs 3 | 🟡 2 copies vs 3 | 🟡 2 copies vs 3 |
| **Write latency** | 🟢 Lower (2 acks) | 🟢 Lower (2 acks) | 🟢 Lower (2 acks) | 🟢 Lower (2 acks) | 🟢 Lower (2 acks) |
| **Write barrier (Global Strong)** | 🟢 Low impact** | N/A | N/A | N/A | N/A |
| **Data loss window** | 🟡 Wider | 🟡 Wider | 🟡 Wider | 🟡 Wider | 🟡 Wider |

\* Bounded Staleness read path **assumes** W=3 as normal state (`ConsistencyReader.cs:222-231`). W=2 was documented as "quorum downshift" edge case — now permanent.
\*\* Write barrier (`WaitForWriteBarrierAsync`) only needs ANY 1 replica with GCLSN match; `includePrimary=true` always. Pool shrinks 4→3 but only needs 1.

#### Combined: Read-Region-Only Scenario (§13)

When only read regions change to NMax=3 (write region stays NMax=4):

| Dimension | Strong | Bounded Staleness | Session | Consistent Prefix | Eventual |
|---|---|---|---|---|---|
| **READ in read region** | 🔴 Same as above | 🔴 Same as above | 🟡 Same as above | ✅ Trivial | ✅ Trivial |
| **READ in write region** | ✅ No change | ✅ No change | ✅ No change | ✅ No change | ✅ No change |
| **WRITE (all regions)** | ✅ **No change** | ✅ **No change** | ✅ **No change** | ✅ **No change** | ✅ **No change** |
| **Suboptimal partition** | 🟡 Always triggered in read regions (3 < global 4) | 🟡 Same | 🟡 Same | 🟡 Same | 🟡 Same |

**Legend:** 🔴 = significant degradation, 🟡 = moderate degradation, 🟢 = minimal/positive, ✅ = no change, N/A = not applicable

---

## 4. QuorumReader — Detailed Quorum Logic (`QuorumReader.cs`)

### `IsQuorumMet` (lines 876-968)
- Compares response LSNs from multiple replicas.
- The `readQuorum` parameter comes from `ConsistencyReader` — stays at 2.
- **No change needed.**

### Primary Read Validation (lines 470-496)
```csharp
if (storeResult.CurrentReplicaSetSize > readQuorum)
{
    // Unexpected — retry on secondary
    return new ReadPrimaryResult(isSuccessful: false, shouldRetryOnSecondary: true);
}
```
- `CurrentReplicaSetSize` is the **actual current N** (dynamic, between NMin and NMax).
- With NMax=3, `CurrentReplicaSetSize` maxes at 3. Read quorum is 2.
- When N=3 > R=2, this will trigger the "unexpected response" path (secondary retry), **same as today with N=4 > R=2**.
- When N=2 (degraded), N=R=2, so primary read path is taken directly.
- **No functional change**, but the "unexpected" branch fires less often since the gap (N-R) is smaller (1 vs 2).

---

## 5. Retry and Connectivity Detection

### `GoneAndRetryWithRetryPolicy.cs:29` and `GoneAndRetryWithRequestRetryPolicy.cs:61`
```csharp
private const int minFailedReplicaCountToConsiderConnectivityIssue = 3;
```
- If ≥3 replicas fail, the SDK declares a connectivity issue and throws `ServiceUnavailableException`.
- **With NMax=3:** If **all 3 replicas fail**, this triggers. With NMax=4, it triggered when 3 of 4 failed (still 1 alive).
- **Impact:** The threshold now equals the total replica count. The SDK will only declare connectivity issues when *every* replica has failed, meaning it will retry longer before surfacing the error.
- ⚠️ **This is a behavioral change** — the connectivity detection threshold was designed with 4 replicas in mind (3 of 4 = 75% failure). With 3 replicas, it becomes 3 of 3 = 100% failure.

---

## 6. Address Cache — Suboptimal Partition Detection (`GatewayAddressCache.cs`)

### User Resource Check (lines 304-308)
```csharp
if (addressInfos.Count < serviceConfigReader.UserReplicationPolicy.MaxReplicaSetSize)
{
    // Suboptimal partition — fewer addresses than expected
}
```

### System Resource Check (lines 590-595)
Same pattern with `SystemReplicationPolicy.MaxReplicaSetSize`.

**Impact:**
- With NMax=4, getting 3 addresses triggers suboptimal detection → SDK forces a refresh.
- With NMax=3, getting 2 addresses triggers it, but getting 3 does not.
- **Fewer false-positive suboptimal detections** during normal operation.
- But during actual degradation (partition loses a replica), the "normal" address count of 2 replicas is closer to the failure threshold.

---

## 7. Test Infrastructure Impact

### Unit Tests (`GatewayAddressCacheTests.cs:36`)
```csharp
private readonly int targetReplicaSetSize = 4;
```
- Tests mock `MaxReplicaSetSize = 4` for both user and system replication policies.
- **All these tests would need updating** to reflect NMax=3.

### Emulator Tests (`TestCommon.cs`)
- Already use `MaxReplicaSetSize = 3`, `MinReplicaSetSize = 2`, `AsyncReplication = true`.
- These tests **already validate 3-replica behavior** and would not need changes.

### Account Properties JSON (`GatewayAccountReaderTests.cs:112-113`)
- Hardcoded JSON: `"maxReplicasetSize": 4`
- Would need updating.

---

## 8. Configuration Flow

```
Server (Account Properties JSON)
  → AccountProperties.ReplicationPolicy / SystemReplicationPolicy
    → CosmosAccountServiceConfiguration (IServiceConfigurationReader bridge)
      → ConsistencyReader.GetMaxReplicaSetSize()
        → readQuorumValue = maxReplicaCount - (maxReplicaCount / 2)
      → GatewayAddressCache (suboptimal detection)
```

- `ReplicationPolicy.DefaultMaxReplicaSetSize = 4` (`ReplicationPolicy.cs:16`) is the **SDK-side default**, but actual values come from the server's account properties JSON.
- Changing the server-side policy automatically propagates to the SDK through account reads.
- The SDK-side default constant is only used when no server value is available.

---

## 9. Primary Exclusion Scenarios — Impact of Fewer Secondaries

This is the **most availability-sensitive dimension** of the 4→3 change. Multiple code paths deliberately **exclude the primary** from initial reads and barrier validations, relying on secondaries alone. With NMax=4 there are 3 secondaries; with NMax=3 there are only 2.

### 9.1 The Read Flow (Secondary-First Pattern)

The strong/bounded-staleness read path in `QuorumReader.ReadStrongAsync` (line 82-254) follows a strict **secondary-first** pattern:

```
Step 1: Read R=2 replicas from SECONDARIES ONLY (includePrimary=false, line 90)
Step 2: Check quorum on those secondaries
Step 3a: QuorumMet → return result
Step 3b: QuorumSelected → issue barrier (allowPrimary=true, line 126)
Step 3c: QuorumNotSelected → fall back to ReadPrimary → may retry with includePrimary=true (line 220)
```

**Why primary is excluded initially:** To avoid monotonic read guarantee violations — the primary may have uncommitted writes ahead of secondaries (especially under async replication), so reading from primary could return a value that a subsequent secondary-only read cannot reproduce.

### 9.2 Scenario Analysis: Initial Secondary-Only Read

| | NMax=4 (3 secondaries) | NMax=3 (2 secondaries) |
|--|--|--|
| **Healthy** | Read 2 of 3 secondaries → quorum easily met | Read 2 of 2 secondaries → quorum met, **zero margin** |
| **1 secondary down** | Read 2 of 2 remaining → quorum met | Read 1 of 1 remaining → **quorum NOT met** → fallback to primary |
| **2 secondaries down** | Read 1 of 1 remaining → quorum NOT met → primary fallback | All secondaries gone → **immediate primary fallback** |

> **Key insight:** With NMax=3, **any single secondary failure** forces the read path into the primary fallback (QuorumNotSelected → ReadPrimaryAsync), adding latency and reducing read throughput. With NMax=4, the system tolerates 1 secondary failure without primary involvement.

### 9.3 Barrier Requests — Two Distinct Behaviors

There are **two barrier call sites** with different `allowPrimary` settings:

#### 9.3.1 Barrier after QuorumSelected (ReadStrongAsync, line 124-130)
```csharp
(bool isSuccess, StoreResponse throttledResponse) = await this.WaitForReadBarrierAsync(
    barrierRequest,
    allowPrimary: true,   // ← Primary CAN participate
    readQuorum: readQuorumValue, ...);
```
- **Triggers when:** Secondary quorum picked an LSN but couldn't confirm it's committed.
- **Primary is included** — impact of 4→3 is minimal here since primary augments the secondary pool.

#### 9.3.2 Barrier within ReadQuorumAsync (line 370-376)
```csharp
(bool isSuccess, StoreResponse throttledResponse) = await this.WaitForReadBarrierAsync(
    barrierRequest,
    false,                // ← PRIMARY EXCLUDED
    readQuorum,
    readLsn, ...);
```
- **Triggers when:** QuorumSelected on a retry path, needs LSN convergence from secondaries only.
- **Primary is excluded** — this is the **fragile path with NMax=3**.

| | NMax=4 (3 secondaries) | NMax=3 (2 secondaries) |
|--|--|--|
| **Healthy** | Barrier needs 2 of 3 secondaries at target LSN | Barrier needs 2 of 2 secondaries at target LSN — **no margin** |
| **1 secondary slow/down** | Still 2 available → barrier can succeed | Only 1 available → **barrier fails** → falls to `TryPrimaryOnlyReadBarrierAsync` |

### 9.4 The Retry Loop — "Reduced Replica Set" Comment

The code at `QuorumReader.cs:215-220` explicitly acknowledges the reduced-secondary scenario:

```csharp
// We have failed to select a quorum before - could very well happen again
// especially with reduced replica set size (1 Primary and 2 Secondaries
// left, one Secondary might be unreachable - due to endpoint health like
// service-side crashes or network/connectivity issues). Including the
// Primary replica even for quorum selection in this case for the retry
includePrimary = true;
```

With NMax=3, this comment describes the **normal healthy state** (1P + 2S), not a degraded state. The scenario it worries about (1 unreachable secondary out of 2) is the **baseline single-failure case** for NMax=3.

**Flow with NMax=3 and 1 secondary down:**
1. `ReadQuorumAsync(includePrimary=false)` → reads 1 of 2 secondaries → **QuorumNotSelected** (need 2, got 1)
2. `ReadPrimaryAsync()` → primary returns `CurrentReplicaSetSize=2 > readQuorum=2` → `ShouldRetryOnSecondary=true`
3. Set `includePrimary=true`, retry
4. `ReadQuorumAsync(includePrimary=true)` → reads from primary + 1 remaining secondary → quorum met

**This adds one full extra round-trip** compared to NMax=4 where the initial secondary-only read would succeed.

### 9.5 Write Barriers — Not Affected

Write barriers in `ConsistencyWriter` always use `includePrimary: true` (lines 525, 638), so the primary is always part of the target set. The 4→3 change has no impact on write barrier replica selection.

### 9.6 Address Resolution Mechanics

When `includePrimary=false`, `AddressSelector.cs:41-43` returns `NonPrimaryReplicaTransportAddressUris` instead of `ReplicaTransportAddressUris`:

```csharp
return includePrimary
    ? (partitionPerProtocolAddress.ReplicaTransportAddressUris, ...)
    : (partitionPerProtocolAddress.NonPrimaryReplicaTransportAddressUris, ...);
```

With NMax=3, `NonPrimaryReplicaTransportAddressUris` contains at most **2 entries** — exactly the read quorum. There is zero buffer for unhealthy/slow secondaries before triggering the primary fallback path.

### 9.7 Summary: Primary Exclusion Impact Matrix

| Phase | allowPrimary | NMax=4 margin | NMax=3 margin | Severity |
|-------|-------------|---------------|---------------|----------|
| **Initial quorum read** | ❌ false | 1 extra secondary | **0 margin** | 🔴 High |
| **Barrier in ReadQuorumAsync** | ❌ false | 1 extra secondary | **0 margin** | 🔴 High |
| **Barrier after QuorumSelected** | ✅ true | 1 extra (all replicas) | 0 extra (all replicas) | 🟡 Medium |
| **Retry with includePrimary** | ✅ true | N/A (fallback) | N/A (fallback) | ✅ Low |
| **Write barriers** | ✅ true | N/A | N/A | ✅ Low |

### 9.8 Availability Implication

With NMax=4, **Strong and Bounded Staleness reads tolerate 1 secondary failure transparently** — no primary involvement, no extra round-trips, no degraded throughput.

With NMax=3, **any single secondary failure** causes:
- **~6s+ timeout wait** (most failures are timeout-detected, not immediate errors)
- Primary fallback on initial read (extra round-trip after timeout)
- Potential barrier failure on secondary-only barriers (another retry cycle)
- Overall read latency increase of **~6-7 seconds** for timeout-detected failures (vs ~4s average with NMax=4)
- Increased load on primary (now serving both writes AND strong/bounded reads)
- **100% of requests hit the timeout penalty** (vs ~67% with NMax=4 where 1/3 of the time the failing secondary isn't in the initial batch)

This is the **most significant operational difference** between 4 and 3 replicas.

---

## 10. Risk Assessment (Updated with Quantified Findings)

### Low Risk ✅
| Area | Quantified Basis | Reference |
|------|-----------------|-----------|
| Read quorum value | Unchanged at R=2 for all levels | §3.1 |
| Strong/BoundedStaleness reads (**all healthy**) | 2 of 2 secondaries satisfy quorum — same 1 RT | §3.3.3 |
| Consistent Prefix & Eventual | No quorum, no barrier, no session — completely unaffected | §3.6, §3.7 |
| Simultaneous failure tolerance | Still 1 failure for both NMax values | §1 |
| Emulator tests | Already testing with 3 replicas | §7 |
| Write barriers (Global Strong) | Only needs 1 replica with GCLSN match, primary always included | §3.3.5 |

### Medium Risk ⚠️
| Area | Quantified Impact | Reference |
|------|-------------------|-----------|
| **SESSION_NOT_FOUND probability** | **25% fewer replicas tried per cycle** (3 vs 4); 25% fewer total replica reads over 5s retry budget (~30-36 vs ~40-48) | §3.2.2, §3.2.4 |
| **Session read during replication lag** | At p=0.5/secondary: 75% success vs 87.5% → **12.5pp more SESSION_NOT_FOUND** | §3.2.4 |
| Read barrier (primary-inclusive, Call Site 1) | 1 spare replica instead of 2; single lagging replica requires both remaining to converge | §3.3.4 |
| Bounded Staleness W=2 permanent | Comments assume W=3 normal; W=2 now permanent, zero safety margin for monotonic reads | §3.4 |
| Write durability at ack time | Only 2 confirmed copies vs 3 — wider data loss window on double failure | §2 |
| Address cache suboptimal detection | Fewer replicas before "suboptimal" triggers — narrower margin | §6 |
| Unit test mocks | Many tests hardcode NMax=4 | §7 |

### High Risk 🔴
| Area | Quantified Impact | Reference |
|------|-------------------|-----------|
| **Secondary-only quorum (1S timeout)** | **~6s+ latency penalty** on 100% of requests (timeout-detected failures); 0 spare secondaries → must wait full request timeout before QuorumNotSelected → primary fallback | §3.3.1, §3.3.3 |
| **Secondary-only quorum (1S error)** | **+1-2 extra round-trips (ms-scale)** for immediately-detected failures; same primary fallback path but faster detection | §3.3.3 |
| **Secondary-only barrier (Call Site 2)** | **Zero margin** — need 2 of 2 secondaries at target LSN; any 1 secondary lagging → barrier FAILS → extra barrier cycle | §3.3.4 |
| **Primary hotspot under degradation** | 1 secondary down → primary serves reads + writes → throughput collapse risk; every Strong/BoundedStaleness read adds ~6s+ (timeout) or 1-2 RT (error) | §3.3.3, §9.4 |
| **Timeout hit probability** | NMax=4: ~67% chance of including failing secondary in initial batch; NMax=3: **100% always** | §3.3.1 |
| **Connectivity detection blind spot** | `minFailedReplicaCount=3` equals total replicas with NMax=3 → connectivity issues detected only at **100% failure** (was 75% with NMax=4) | §5 |
| **Write quorum drop** | W=3→W=2 means writes succeed with fewer acks — potential data loss window widens; was treated as degraded state in code comments | §3.4 |

---

## 11. Recommendations (Prioritized by Risk)

### P0 — Must Fix Before NMax=3 Rollout

1. **Lower `minFailedReplicaCountToConsiderConnectivityIssue` from 3 to 2** — With NMax=3, the current threshold means connectivity issues are detected only at 100% failure. Lowering to 2 restores the ~67% detection ratio.
   > Files: `GoneAndRetryWithRetryPolicy.cs:29`, `GoneAndRetryWithRequestRetryPolicy.cs:61`

2. **Evaluate `allowPrimary=false` in ReadQuorumAsync:372** — This secondary-only barrier has **zero failure tolerance** with NMax=3. Consider changing to `allowPrimary=true` or making it NMax-aware. Analyze impact on monotonic read guarantees before changing.
   > File: `QuorumReader.cs:370-376`

### P1 — High Priority

3. **Measure SESSION_NOT_FOUND increase** — Instrument session read paths to track 404/1002 rates before/after NMax=3 rollout. Expected increase: 12-25% more initial session misses depending on replication lag distribution.
   > Files: `StoreReader.cs:330-370`, `ConsistencyReader.cs:299-353`

4. **Stress-test 1-secondary-down scenario for Strong/BoundedStaleness** — Quantify the +1-2 RT latency impact in production-like conditions. This scenario goes from "handled gracefully" (NMax=4) to "every request affected" (NMax=3).
   > File: `QuorumReader.cs:167-220`

5. **Update Bounded Staleness comments** — `ConsistencyReader.cs:222-231` explicitly assumes W=3 normal state. Must be updated to reflect W=2 as permanent to prevent future misunderstanding.
   > File: `ConsistencyReader.cs:222-231`

### P2 — Important

6. **Consider adaptive `SessionTokenMismatchRetryPolicy` for NMax=3** — Since each retry cycle contacts 25% fewer replicas, consider slightly increasing retry count or reducing backoff to compensate.
   > File: `SessionTokenMismatchRetryPolicy.cs:18-21`

7. **Update test mocks** — Many unit tests hardcode NMax=4. Add parallel test configurations with NMax=3 to catch regressions.
   > Files: `GatewayAddressCacheTests.cs`, `GatewayAccountReaderTests.cs`, emulator `TestCommon.cs`

8. **Monitor write tail latency** — W=2 means writes are faster on average, but the reduced durability window should be measured against actual failure rates.

9. **Primary hotspot alerting** — Add monitoring for primary read load under degraded conditions (1 secondary down). With NMax=3, primary becomes the bottleneck for every Strong/BoundedStaleness read.

---

## 12. Affected Files

| File | Impact |
|------|--------|
| `src/direct/ReplicationPolicy.cs:16-17` | Default constants (SDK-side only) |
| `src/direct/ConsistencyReader.cs:207-232` | Quorum calculation + Bounded Staleness assumptions |
| `src/direct/ConsistencyWriter.cs:18-49` | Write quorum behavior documentation |
| `src/direct/QuorumReader.cs:82-254` | ReadStrongAsync — secondary-first read + primary fallback logic |
| `src/direct/QuorumReader.cs:292-417` | ReadQuorumAsync — secondary-only barrier (allowPrimary=false at line 372) |
| `src/direct/QuorumReader.cs:470-496` | Primary read validation against CurrentReplicaSetSize |
| `src/direct/QuorumReader.cs:574-873` | WaitForReadBarrierAsync — old and new barrier implementations |
| `src/direct/StoreReader.cs:55-90` | ReadMultipleReplicaAsync — includePrimary parameter |
| `src/direct/AddressSelector.cs:34-43` | ResolveAllTransportAddressUriAsync — NonPrimaryReplicaTransportAddressUris |
| `src/direct/GoneAndRetryWithRetryPolicy.cs:29` | Connectivity detection threshold |
| `src/direct/GoneAndRetryWithRequestRetryPolicy.cs:61` | Connectivity detection threshold |
| `src/Routing/GatewayAddressCache.cs:304-308, 590-595` | Suboptimal partition detection |
| `src/Resource/Settings/AccountProperties.cs:225-232` | Server-side policy bridge |
| `tests/.../GatewayAddressCacheTests.cs:36` | Test mock: `targetReplicaSetSize = 4` |
| `tests/.../GatewayAccountReaderTests.cs:112-113` | Test JSON: `maxReplicasetSize: 4` |

---

## 13. Read-Region-Only Scenario: NMax=3 in Read Regions, NMax=4 in Write Region

> **Assumption:** Only read region replication policy changes to 3 replicas. Write region retains 4 replicas. The account-level `MaxReplicaSetSize` remains 4.

### 13.1 Architecture Context

The SDK uses a **single global** `MaxReplicaSetSize` from `AccountProperties.ReplicationPolicy` (`CosmosAccountServiceConfiguration.cs:34-36`). This is NOT per-region. However, the **actual replica addresses** returned by the gateway are per-region — a read region partition returns only 3 addresses even though the global setting says 4.

This creates a **mismatch** between:
- **Quorum calculation:** Uses global MaxReplicaSetSize=4 → `readQuorumValue = 4 - 2 = 2`
- **Actual replicas available:** 3 in read regions, 4 in write region

### 13.2 What is ELIMINATED (vs Full NMax=3 Analysis)

| Concern | Why It's Eliminated |
|---------|---------------------|
| ✅ **Write quorum drop (W:3→2)** | Write region retains 4 replicas → W=3 unchanged |
| ✅ **Write durability reduction** | 3 confirmed copies before ack (unchanged) |
| ✅ **Bounded Staleness W=3 assumption** | `ConsistencyReader.cs:222-231` comment remains correct — W=3 in write region |
| ✅ **Write barrier impact** | Write barriers execute in write region (NMax=4) — unchanged |
| ✅ **Write latency change** | Still waits for W=3 acks (unchanged) |
| ✅ **Global Strong write barrier** | Executes against write region replicas (unchanged) |

**These were 3 of the 5 High Risk items in §10. All eliminated.**

### 13.3 What REMAINS (Read Region Impacts)

#### 13.3.1 Secondary-Only Quorum — Zero Spare (Strong/BoundedStaleness)

**Still applies.** When SDK routes a Strong/BoundedStaleness read to a read region:
- Read region has 3 replicas (1 primary + 2 secondaries)
- `ReadStrongAsync` starts with `includePrimary=false` → 2 secondaries available
- readQuorum=2 → needs both secondaries → **zero spare**
- 1 secondary failure → QuorumNotSelected → +1-2 extra round-trips

| Scenario | Write Region (NMax=4) | Read Region (NMax=3) |
|----------|----------------------|---------------------|
| Secondaries available | 3 | **2** |
| Spare for quorum (R=2) | 1 | **0** |
| 1 secondary failure cost | 0 extra RT | **+1-2 RT** |

**Severity: 🔴 High** (unchanged from full analysis)

#### 13.3.2 Secondary-Only Barrier — Zero Margin

**Still applies.** Barrier at `ReadQuorumAsync:372` (`allowPrimary=false`) in read region:
- 2 secondaries, need 2 at target LSN → **zero margin**
- Any single secondary lagging → barrier fails → extra cycle

**Severity: 🔴 High** (unchanged)

#### 13.3.3 SESSION_NOT_FOUND — 25% Fewer Attempts in Read Region

**Still applies.** When session read is routed to read region:
- 3 replicas tried vs 4 → 25% fewer chances to find session token match
- During cross-region replication lag (write region → read region), session tokens may not have propagated to read region replicas yet

| Metric | Write Region (NMax=4) | Read Region (NMax=3) |
|--------|----------------------|---------------------|
| Replicas tried | 4 | **3** |
| SessionTokenMismatch retries over 5s | ~40-48 reads | **~30-36 reads** |

**Note:** Session reads in read regions are already more susceptible to 404/1002 due to cross-region replication lag. Fewer replicas compounds this.

**Severity: 🟡 Medium** (unchanged)

#### 13.3.4 Suboptimal Partition Detection — ALWAYS Triggered ⚠️ NEW

**This is a NEW concern unique to the read-region-only scenario.**

`GatewayAddressCache.cs:304-308`:
```csharp
int targetReplicaSetSize = this.serviceConfigReader.UserReplicationPolicy.MaxReplicaSetSize; // = 4
if (addresses.AllAddresses.Count() < targetReplicaSetSize) // 3 < 4 = TRUE ALWAYS
{
    this.suboptimalServerPartitionTimestamps.TryAdd(partitionKeyRangeIdentity, DateTime.UtcNow);
}
```

With global MaxReplicaSetSize=4 but read region returning 3 addresses:
- **EVERY partition in every read region** is permanently marked suboptimal
- This triggers background address refresh attempts that will never resolve (3 is the correct count)
- Generates unnecessary gateway traffic and log noise

**Severity: 🟡 Medium** (new — not in original analysis)

#### 13.3.5 StoreReader Address Mismatch Detection

`StoreReader.cs:197-205`:
```csharp
if (resolveApiResults.Count < replicaCountToRead) // for forceReadAll barrier reads
{
    return new ReadReplicaResult(retryWithForceRefresh: true, ...);
}
```

For **quorum reads** (replicaCountToRead=2): 3 replicas ≥ 2 → ✅ No issue
For **barrier reads with forceReadAll**: This reads all available replicas. With 3 available and readQuorum=2, this works but the "all" is 3 instead of 4.

**Severity: 🟢 Low** (quorum reads unaffected; barriers use actual count)

#### 13.3.6 Connectivity Detection Blind Spot

**Still applies** in read regions:
- `minFailedReplicaCountToConsiderConnectivityIssue = 3` = total replicas in read region
- Connectivity issue detected only at 100% read-region failure

**Severity: 🟡 Medium** (same as before, but scoped to read regions only)

### 13.4 Consolidated Read-Region-Only Risk Summary

| Risk | Severity | Scope | Notes |
|------|----------|-------|-------|
| Secondary-only quorum zero spare | 🔴 High | Read region Strong/BoundedStaleness | +1-2 RT on any 1 secondary failure |
| Secondary-only barrier zero margin | 🔴 High | Read region Strong/BoundedStaleness | Barrier fails on any 1 secondary lag |
| Primary hotspot under degradation | 🔴 High | Read region | 1 secondary down → primary serves all reads |
| **Suboptimal partition always triggered** | 🟡 Medium | **All read region partitions** | **NEW: 3 < 4 global mismatch** |
| SESSION_NOT_FOUND increase | 🟡 Medium | Read region session reads | 25% fewer attempts |
| Connectivity detection blind spot | 🟡 Medium | Read region | 100% failure threshold |
| Write quorum (W) | ✅ Eliminated | N/A | Write region stays NMax=4 |
| Write durability | ✅ Eliminated | N/A | W=3 unchanged |
| Bounded Staleness W assumption | ✅ Eliminated | N/A | W=3 comment remains valid |
| Write barrier | ✅ Eliminated | N/A | Write region unchanged |

### 13.5 Net Assessment: Read-Region-Only vs Full NMax=3

| Dimension | Full NMax=3 | Read-Region-Only | Improvement |
|-----------|-------------|-------------------|-------------|
| High Risk items | 5 | **3** | −40% |
| Medium Risk items | 7 | **4** (1 new) | −43% |
| Write path impact | Significant (W:3→2) | **None** | ✅ Eliminated |
| Read path impact (in read regions) | Identical | **Identical** | ⚠️ No improvement |
| Read path impact (in write region) | Impacted | **None** | ✅ Eliminated |
| New concerns | None | **Suboptimal partition always triggered** | ⚠️ New |

### 13.6 Recommendations Specific to Read-Region-Only Scenario

1. **P0: Address the suboptimal partition false positive** — `GatewayAddressCache.cs:304-308` will mark ALL read-region partitions as suboptimal permanently. Either make the comparison per-region aware, or accept increased gateway traffic.

2. **P0: Lower `minFailedReplicaCountToConsiderConnectivityIssue` to 2 for read regions** — Or make it relative to actual replica count instead of hardcoded.

3. **P1: Evaluate `allowPrimary=false` at `ReadQuorumAsync:372`** — Zero margin in read regions (same recommendation as full analysis).

4. **P1: Monitor SESSION_NOT_FOUND rates per region** — Expect increase in read regions vs write regions, useful as a signal for the NMax change.

5. **P2: Consider routing Strong/BoundedStaleness reads to write region under degradation** — When a read region has a secondary failure, routing to write region (NMax=4, 3 secondaries) avoids the primary fallback penalty.

---

## 14. Key Code References

```
ConsistencyReader.cs:38-56    — Replica set theory, simultaneous failures
ConsistencyReader.cs:58-61    — Quorum formula definitions (W, R)
ConsistencyReader.cs:103      — "Availability for Bounded Staleness (for NMax = 4 and NMin = 2)"
ConsistencyReader.cs:207-208  — readQuorumValue = maxReplicaCount - (maxReplicaCount / 2)
ConsistencyReader.cs:222-231  — Bounded Staleness assumes W=3 majority quorum
ConsistencyWriter.cs:18-49    — Write path documentation
QuorumReader.cs:82-254        — ReadStrongAsync: secondary-first read pattern
QuorumReader.cs:90             — includePrimary = false (initial state)
QuorumReader.cs:126            — allowPrimary: true (barrier after QuorumSelected)
QuorumReader.cs:215-220        — Comment + includePrimary=true on retry after primary fallback
QuorumReader.cs:292-417        — ReadQuorumAsync: quorum calculation + secondary-only barrier
QuorumReader.cs:370-376        — WaitForReadBarrierAsync(allowPrimary=false) — fragile with NMax=3
QuorumReader.cs:470-496        — Primary read validation with CurrentReplicaSetSize
QuorumReader.cs:574-873        — Barrier implementations (old and new)
StoreReader.cs:55-90           — ReadMultipleReplicaAsync with includePrimary flag
AddressSelector.cs:34-43       — NonPrimaryReplicaTransportAddressUris selection
ReplicationPolicy.cs:16-17    — DefaultMaxReplicaSetSize = 4, DefaultMinReplicaSetSize = 3
GoneAndRetryWithRetryPolicy.cs:29              — minFailedReplicaCount = 3
GoneAndRetryWithRequestRetryPolicy.cs:61       — minFailedReplicaCount = 3
GatewayAddressCache.cs:304-308, 590-595        — Suboptimal partition detection
CosmosAccountServiceConfiguration.cs:34-36     — Global ReplicationPolicy from AccountProperties
StoreReader.cs:197-205                         — Address count < replicaCountToRead mismatch check
AddressEnumerator.cs:35-56                     — Health-based replica ordering
SessionTokenMismatchRetryPolicy.cs:18-21       — 5s budget, 5ms→500ms backoff
ClientRetryPolicy.cs:425-477                   — ShouldRetryOnSessionNotAvailable per region
```
