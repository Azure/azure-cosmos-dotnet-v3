# Metadata Hedging — Simple Design (v1)

> A deliberately minimal design for cross-region hedging of the two metadata
> cache reads. The goal is something small enough to read in one sitting and
> implement in a single file, while preserving the one invariant that keeps it
> safe. Optional hardening (concurrency budget, region-dedup, active loser
> cancellation) is deferred to [Phase 2](#12-phase-2--optional-hardening) and is
> **not** required for a correct v1.

> **See also:** For user-facing behavior (enablement, threshold, the status codes
> that trigger a hedge, and diagnostics), see the **Metadata Hedging** section of
> [`Cross Region Request Hedging.md`](./Cross%20Region%20Request%20Hedging.md#metadata-hedging).
> This document is the internal design/implementation reference.

---

## 1. Goals & Non-Goals

**Goal:** when a metadata read is slow on the primary region, send a second copy
to another region after a short delay and return whichever good answer arrives
first — so a single slow region does not stall cache population (cold start) or
cache refresh (warm path).

**Non-goals (v1):**
- Not a general availability strategy — data-plane hedging already exists.
- Not a retry mechanism — it races, it does not add retry attempts.
- Not a correctness fallback — it never turns a real error into a success.

---

## 2. Guiding Principle (the whole design in one line)

> **The primary is authoritative. The hedge can only *win* by being fast and
> good; it can never change the outcome the primary would have produced.**

Everything below follows from this. Because the hedge can only ever *improve
latency* and never *alter the result*, we do not need most of the machinery a
symmetric two-way race would require.

---

## 3. Scope

Hedging applies to exactly two metadata reads:

| Cache | Operation | Notes |
|---|---|---|
| `ClientCollectionCache` | `Collection` `Read` | cold-start and refresh |
| `PartitionKeyRangeCache` | `PartitionKeyRange` `ReadFeed` | **first page only** |

Everything else (documents, other control-plane reads, subsequent PKRange pages)
is out of scope and takes the existing single-region path unchanged.

---

## 4. Enablement

Resolved **once** at client construction into a tri-state `bool?`
(`enableMetadataHedging`), then honored per request.

| Package | Env var unset (`null`) | `AZURE_COSMOS_METADATA_HEDGING_ENABLED=true` | `=false` |
|---|---|---|---|
| **Preview** | **ON** | ON | OFF |
| **GA** | follow account PPAF state | ON | OFF |

Resolution (pseudocode):

```csharp
// ConfigurationManager
public static bool? GetMetadataHedgingOptIn()
{
    string v = Environment.GetEnvironmentVariable("AZURE_COSMOS_METADATA_HEDGING_ENABLED");
    if (bool.TryParse(v, out bool parsed)) return parsed;   // true / false wins

#if PREVIEW
    return true;      // preview default: ON
#else
    return null;      // GA default: follow PPAF (decided per request)
#endif
}

// per-request effective decision
bool hedgingEnabled = optIn ?? isPpafEnabled();   // null → follow live PPAF
```

- `false` is a hard kill-switch: the strategy is not even constructed.
- `true` or `null` constructs the strategy; the per-request check applies
  `optIn ?? isPpafEnabled()`.

---

## 5. Threshold

A single fixed value, computed once at startup:

```
threshold = 1.5 s   (default)
```

**Invariant:** the threshold must sit strictly between the first and second
control-plane HTTP attempt timeouts:

```
firstAttemptTimeout  <  threshold  <  secondAttemptTimeout
   (~1 s)                  1.5 s            (~5 s)
```

Rationale: we only want to hedge *after* the first local HTTP attempt has had its
chance (so we don't hedge on normal latency), but *before* the second attempt's
long timeout (so we still cut the tail). Deriving it as
`firstAttemptTimeout + step` keeps the invariant true automatically if the
timeout policy changes. Not customer-configurable in v1.

---

## 6. The Algorithm

One method. No loop, one timer CTS, no ownership transfer.

```csharp
async Task<Response> ExecuteAsync(
    Request request,
    Func<Request, Uri, CancellationToken, Task<Response>> send,   // routes to the given region
    CancellationToken ct)
{
    // 0. Eligible? (see §7) If not, just send to the primary and return.
    if (!IsEligible(request, out Uri primary, out Uri hedge))
        return await send(request, primary, ct);

    // 1. Start primary + a threshold timer.
    using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    Task<Response> primaryTask = send(request.Clone(), primary, ct);
    Task timer = Task.Delay(this.threshold, timerCts.Token);

    // 2. Did the primary SETTLE before the threshold? The primary is authoritative, so any
    //    DEFINITIVE outcome it produced wins with no hedge. In production these metadata reads
    //    THROW for status >= 400, so a definitive error (404/409/412/...) arrives as a FAULTED
    //    task — we classify the outcome, not a response, and only a REGIONAL failure is hedged.
    await Task.WhenAny(primaryTask, timer);
    timerCts.Cancel();                           // stop the timer
    if (primaryTask.IsCompleted && Classify(primaryTask) != RegionalFailure)
        return await primaryTask;                // success or definitive error → authoritative

    // 3. Primary is slow, or hit a regional failure → fire ONE hedge.
    //    Short-circuit a caller cancel first so a cancelled request never spawns a phantom hedge.
    ct.ThrowIfCancellationRequested();
    Task<Response> hedgeTask = send(request.Clone(), hedge, ct);

    // 4. Resolve the race, keeping the primary authoritative throughout:
    //    - a fast, successful hedge may WIN the latency race, but
    //    - it can never override a primary that has ALREADY produced a definitive outcome, and
    //    - when neither branch is good, the primary's outcome is returned (its exception rethrown)
    //      so the caller's retry policy sees exactly what it would have without hedging.
    return await ResolveWinnerAsync(primaryTask, hedgeTask, primary, hedge);
}
```

Helpers:

```csharp
// Fire-and-forget: prevents unobserved-task exceptions and disposes the loser's body.
static void ObserveInBackground(Task<Response> loser)
    => _ = loser.ContinueWith(
        t => { if (t.Status == TaskStatus.RanToCompletion) t.Result?.Dispose(); },
        TaskScheduler.Default);

static async Task SwallowAsync(Task t)
{
    try { await t; } catch { /* observed; decision handled by caller */ }
}
```

That is the entire orchestration. Note what is **absent** compared to the current
implementation: no `primaryCts`/`hedgeCts`, no CTS ownership transfer, no
`BackgroundCleanupAsync` with budget hand-off, no `while(true)` winner loop, no
`HedgeBranch` enum. The loser is simply *abandoned* (observed) rather than
actively cancelled — its own HTTP timeout ends it. For a metadata GET that costs
at most one extra in-flight request for a bounded time, which is an acceptable v1
tradeoff (see [§9](#9-what-we-deliberately-leave-out-in-v1)).

---

## 7. Eligibility & the two "good" predicates

**Eligible** (all must hold, else send primary only):

1. gateway `disableCrossRegionalHedging` is false (operator kill-switch, read live)
2. `enableMetadataHedging != false`
3. `optIn ?? isPpafEnabled()` is true
4. resource is `Collection`+`Read` or `PartitionKeyRange`+`ReadFeed` (first page)
5. there are ≥ 2 read regions available after `ExcludeRegions` (from the SAME
   `GetApplicableEndpoints` list used to pick the hedge endpoint, so eligibility and
   selection can never drift)

**Outcome classification** — this is the only place the primary/hedge asymmetry lives.
The critical detail: in production these metadata reads **throw** `DocumentClientException`
for every status ≥ 400 (the gateway only returns error responses when `UseStatusCodeForFailures`
is set, which neither cache sets), so the primary's authoritative answer arrives as a **task
fault**, not a response. We therefore classify the completed *task* — via its exception when
faulted, or its status when it is one of the rare exceptionless-retry responses:

```csharp
enum BranchOutcome { Success, RegionalFailure, Definitive }

// Classify a COMPLETED branch. The task is guaranteed complete by the caller.
static async Task<BranchOutcome> Classify(Task<Response> t)
{
    if (t.IsFaulted)   return IsRegionalFailure(t.Exception) ? RegionalFailure : Definitive;
    if (t.IsCanceled)  return Definitive;                 // authoritative; rethrown on await
    Response r = await t;
    if (r == null || ((int)r.StatusCode >= 400 && !IsRegionalFailure(r.StatusCode, r.SubStatus)))
        return Definitive;
    return (int)r.StatusCode < 400 ? Success : RegionalFailure;
}
```

The primary/hedge asymmetry is then just two rules in `ResolveWinnerAsync`:

- **Primary** — any outcome that is not a `RegionalFailure` (i.e. `Success` or a definitive
  error) is its real, authoritative answer and is returned verbatim; a hedge can never override it.
- **Hedge** — may only *win* with a `Success`. A hedge `RegionalFailure`, definitive error, or
  cross-region auth reject (401 / plain 403, which are **not** regional) makes it a losing hedge,
  so a misconfigured secondary can never surface a spurious error as the operation result.

Worked cases:

| Primary | Hedge | Result | Why |
|---|---|---|---|
| 200 fast | — | primary | won before threshold, no hedge |
| slow→200 | 200 | first good | latency win |
| 503 (thrown) | 200 | hedge | primary regional failure, hedge clean |
| 404 fast (thrown) | — | primary (404) | definitive error before threshold → no hedge |
| slow→409 (thrown, first) | in-flight | primary (409) | primary settled with a definitive answer → hedge cannot override |
| 503 (thrown) | 401 (thrown) | primary (503) | hedge auth reject not "good"; primary authoritative → retry policy handles 503 |
| 503 (thrown) | 503 (thrown) | primary (503) | neither good → primary outcome rethrown |

Regional-failure classification (503/500, 410+LeaseNotFound, 403+DatabaseAccountNotFound) is
the SAME status/sub-status set as `MetadataRequestThrottleRetryPolicy`, so metadata hedging and
the metadata retry policy agree on what "the region is at fault" means. A bare
`HttpRequestException` (connection refused / DNS / TLS reaching the gateway) is additionally
treated as regional (the region is unreachable, not the request bad), mirroring the data-plane
`ClientRetryPolicy`, so a hard-down primary region is hedgeable and a good hedge can win over it.
There is no separate `MetadataRegionalFailureClassifier` type; the check is a small private
static shared by the exception and response paths.

---

## 8. Wiring

Construct one strategy per client, inject into the two caches, dispose on client
dispose:

```csharp
// DocumentClient init
this.metadataHedgingStrategy = MetadataHedgingStrategy.CreateIfEnabled(
    enableMetadataHedging: this.enableMetadataHedging,        // from ConfigurationManager
    globalEndpointManager: this.GlobalEndpointManager,
    isPpafEnabled: () => this.ConnectionPolicy.EnablePartitionLevelFailover,
    isCrossRegionalHedgingDisabled: () => this.disableCrossRegionalHedging); // operator kill-switch

new ClientCollectionCache(..., metadataHedgingStrategy);
new PartitionKeyRangeCache(..., metadataHedgingStrategy);
```

The strategy holds no disposable state (no per-branch CTS, no semaphore), so it is **not**
`IDisposable` — there is nothing to dispose on client dispose. The only disposable is each
losing branch's response body, which is released by `ObserveInBackground`.

Each cache call site is a simple wrap over a region-targeted send delegate:

```csharp
if (this.metadataHedgingStrategy != null)
{
    var result = await this.metadataHedgingStrategy.ExecuteAsync(
        request,
        sendToEndpoint: MetadataHedgingStrategy.StoreModelSender(storeModel), // routes clone → endpoint
        isFirstReadFeedPage: ...,
        ct);
    return Materialize(result.Response);
}
return Materialize(await storeModel.ProcessMessageAsync(request));   // strategy off → unchanged
```

`PartitionKeyRangeCache` passes the "is this the first page?" signal; pages 2..N skip hedging
entirely. Crucially, later pages are pinned to the winning region **only when the hedge actually
won** (`result.HedgeWon`, i.e. the winning region differs from the primary). When the primary
won — whether or not a hedge fired — pages 2..N stay on the normal per-page resolution path so
the metadata retry policy can still fail over across regions.

---

## 9. What We Deliberately Leave Out in v1

| Left out | Consequence | Why it's safe to defer |
|---|---|---|
| **Active loser cancellation** (per-branch CTS) | loser runs until its own HTTP timeout | metadata GET, bounded cost; observed so no unobserved-exception |
| **Per-client concurrency budget** | many simultaneous cold starts could each hedge | naturally bounded by "# containers warming at once"; add budget if telemetry shows secondary pressure |
| **Region-dedup with the retry policy** (`AttemptedEndpoints`) | a subsequent retry might re-hit a region the hedge used | bounded by retry count; correctness unaffected (primary authoritative) |
| **`HasHedgedThisOperation` latch** | in theory a retry could hedge again | each hedge is still one-primary-one-secondary and bounded; revisit with the budget |

Each of these is an *optimization or a guardrail*, not a correctness
requirement. The one true invariant — **the loser never influences the result and
never reaches the retry policy** — is preserved for free here, because we simply
return the primary's outcome when there's no clean hedge win and we swallow the
loser task. There is no code path by which a losing hedge can mark a region
unavailable, because the loser's outcome is never inspected.

---

## 10. Telemetry (minimal)

v1 emits a single per-request trace datum, keyed `"Metadata Hedge"`, on the metadata read's
trace:

```
HedgeFired={true|false}; HedgeWon={true|false}; WinningRegion={region}
```

- `HedgeFired` — a hedge request was dispatched (the threshold elapsed or the primary hit a
  regional failure).
- `HedgeWon` — the hedge's response is the one returned (the winning region differs from the
  primary); this is also the signal that pins later PKRange pages.
- `WinningRegion` — the region that produced the returned answer.

This datum is attached by both cache call sites (`ClientCollectionCache` and
`PartitionKeyRangeCache`). Standalone `hedge_fired` / `hedge_won` / `hedge_skipped` metric events
and a `budget_exhausted` counter are deferred to Phase 2 (they arrive with the concurrency budget).

---

## 11. Testing

- **Primary fast** → hedge never fires; exactly one send.
- **Primary slow, hedge good** → hedge wins (`HedgeWon` true), result is the hedge's.
- **Primary 503 (thrown), hedge 200** → hedge wins over the regional failure.
- **Primary 503 (thrown), hedge 401 (thrown)** → primary's 503 is rethrown (hedge auth
  reject is not "good"; primary authoritative).
- **Fast definitive primary error (404, thrown)** → rethrown, NO hedge fires (core invariant).
- **Slow primary settles first with a definitive error (409, thrown)** → rethrown even though a
  hedge is in flight (hedge cannot override the primary's produced answer).
- **Hedge fired but primary won** → `HedgeFired` true, `HedgeWon` false (later PKRange pages
  are NOT pinned).
- **Single region / PPAF-off + null opt-in** → skipped, primary-only.
- **Explicit opt-in true, PPAF off** → hedges anyway.
- **Operator kill-switch (`disableCrossRegionalHedging`)** → suppresses hedging even with a
  slow primary + PPAF on.
- **Caller cancels mid-flight** → `OperationCanceledException` surfaces; no phantom hedge; no hang.
- **Loser drains after winner** → losing response body disposed.
- **Threshold invariant** → unit test asserts `first < threshold < second` (both bounds).

---

## 12. Phase 2 — Optional Hardening

Add only if telemetry justifies it, each independently:

1. **Concurrency budget** — a per-client `SemaphoreSlim(n)`; `TryWait(0)` before
   dispatching the hedge, release when it settles. Protects the secondary during
   mass cold start / correlated brownout. First thing to add.
2. **Active loser cancellation** — give each branch its own linked CTS and cancel
   the loser on win, to stop the wasted in-flight request sooner.
3. **Region-dedup** — share attempted endpoints with
   `MetadataRequestThrottleRetryPolicy` so a post-hedge retry skips regions
   already tried. (If added, wire it on **both** cache paths, not just PKRange.)

Each is a bolt-on that does not change the core algorithm in §6.

---

## 13. Summary

The simple design is: *send to primary; if it's slow, also send to one other
region; return the first clean answer, with the primary always authoritative.*
The hedge can only make things faster, never different — which is what lets us
drop the CTS choreography, the background budget hand-off, and the winner loop.
Start here; add the Phase 2 guardrails only when data says you need them.
