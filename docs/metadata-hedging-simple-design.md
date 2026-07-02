# Metadata Hedging — Simple Design (v1)

> A deliberately minimal design for cross-region hedging of the two metadata
> cache reads. The goal is something small enough to read in one sitting and
> implement in a single file, while preserving the one invariant that keeps it
> safe. Optional hardening (concurrency budget, region-dedup, active loser
> cancellation) is deferred to [Phase 2](#12-phase-2--optional-hardening) and is
> **not** required for a correct v1.

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

    // 2. Did the primary finish, well, before the threshold? Then we're done.
    if (await Task.WhenAny(primaryTask, timer) == primaryTask
        && primaryTask.Status == TaskStatus.RanToCompletion
        && PrimaryIsGood(primaryTask.Result))
    {
        timerCts.Cancel();                       // stop the timer
        return primaryTask.Result;               // hedge never fired
    }

    // 3. Primary is slow (or already failing) → fire the hedge.
    timerCts.Cancel();
    Task<Response> hedgeTask = send(request.Clone(), hedge, ct);

    // 4. Return the first GOOD answer; primary stays authoritative.
    Task<Response> first = await Task.WhenAny(primaryTask, hedgeTask);
    Task<Response> other = (first == primaryTask) ? hedgeTask : primaryTask;

    if (IsGood(first, isHedge: first == hedgeTask))
        return Winner(first, other);             // first good answer wins

    // first wasn't good — wait for the other, then decide.
    await SwallowAsync(other);
    if (IsGood(other, isHedge: other == hedgeTask))
        return Winner(other, first);

    // Neither was good → return the PRIMARY's outcome (authoritative).
    return await primaryTask;                     // re-throws primary's exception if it faulted
}

// Return the winner's result and let the loser drain harmlessly in the background.
Response Winner(Task<Response> winner, Task<Response> loser)
{
    ObserveInBackground(loser);                   // dispose its response, swallow its exception
    return winner.Result;
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

1. `enableMetadataHedging != false`
2. `optIn ?? isPpafEnabled()` is true
3. resource is `Collection`+`Read` or `PartitionKeyRange`+`ReadFeed` (first page)
4. there are ≥ 2 read regions available after `ExcludeRegions`

**The "good" predicates** — this is the only place the primary/hedge asymmetry
lives, and it is two tiny rules:

```csharp
// Primary is authoritative: any non-regional-failure response is its real answer.
static bool PrimaryIsGood(Response r)
    => !MetadataRegionalFailureClassifier.IsRegionalFailure(r);

// Hedge may only WIN with a clean answer, so a misconfigured secondary
// (e.g. region-scoped RBAC returning 401/403) can never surface a spurious error.
static bool HedgeIsGood(Response r)
    => (int)r.StatusCode < 400 || r.StatusCode == HttpStatusCode.NotFound;

static bool IsGood(Task<Response> t, bool isHedge)
    => t.Status == TaskStatus.RanToCompletion
       && (isHedge ? HedgeIsGood(t.Result) : PrimaryIsGood(t.Result));
```

Worked cases:

| Primary | Hedge | Result | Why |
|---|---|---|---|
| 200 fast | — | primary | won before threshold, no hedge |
| slow→200 | 200 | first good | latency win |
| 503 | 200 | hedge | primary failing, hedge clean |
| 400 | — | primary | 400 is a real answer, not a regional failure → no hedge |
| 503 | 401 | primary (503) | hedge not "good"; primary authoritative → retry policy handles 503 |
| 503 | 503 | primary (503) | neither good → primary outcome |

`MetadataRegionalFailureClassifier.IsRegionalFailure` is the existing shared
classifier (503/500, 410+LeaseNotFound, 403+DatabaseAccountNotFound,
`HttpRequestException`, non-user `OperationCanceledException`).

---

## 8. Wiring

Construct one strategy per client, inject into the two caches, dispose on client
dispose:

```csharp
// DocumentClient init
this.metadataHedgingStrategy = MetadataHedgingStrategy.CreateIfEnabled(
    enableMetadataHedging: this.enableMetadataHedging,        // from ConfigurationManager
    globalEndpointManager: this.GlobalEndpointManager,
    isPpafEnabled: () => this.ConnectionPolicy.EnablePartitionLevelFailover);

new ClientCollectionCache(..., metadataHedgingStrategy);
new PartitionKeyRangeCache(..., metadataHedgingStrategy);
```

Each cache call site is a simple wrap:

```csharp
if (this.metadataHedgingStrategy != null)
{
    Response r = await this.metadataHedgingStrategy.ExecuteAsync(
        request,
        send: (req, endpoint, ct) => { req.RouteToLocation(endpoint); return storeModel.ProcessMessageAsync(req, ct); },
        ct);
    return Materialize(r);
}
return Materialize(await storeModel.ProcessMessageAsync(request));   // strategy off → unchanged
```

`PartitionKeyRangeCache` passes the "is this the first page?" signal; pages 2..N
skip hedging entirely (they call the store model directly).

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

Three events + one per-request datum are enough for v1:

- `hedge_fired` — threshold elapsed, hedge dispatched.
- `hedge_won` — the hedge produced the returned answer.
- `hedge_skipped{reason}` — ineligible / single region / disabled.
- Per-request trace datum `"Metadata Hedge"`: `{ eligible, primaryRegion,
  hedgeRegion, thresholdMs, hedgeFired, winner }`.

(A `budget_exhausted` counter arrives with the Phase 2 budget.)

---

## 11. Testing

- **Primary fast** → hedge never fires (`hedge_fired` == 0).
- **Primary slow, hedge good** → hedge wins, result is the hedge's.
- **Primary 503, hedge 200** → hedge wins.
- **Primary 503, hedge 401** → returns 503 (primary authoritative; hedge auth
  suppressed).
- **Single region** → skipped, primary-only.
- **Caller cancels mid-flight** → `OperationCanceledException` surfaces; no hang.
- **Loser drains after winner** → loser response disposed, no unobserved-task
  exception (assert via `TaskScheduler.UnobservedTaskException`).
- **Threshold invariant** → unit test asserts `first < threshold < second`.

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
