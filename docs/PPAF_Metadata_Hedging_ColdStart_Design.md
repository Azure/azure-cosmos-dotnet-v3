# Design: Cross-Regional Hedging for Cold-Start Metadata Requests

**PPAF – Bounded Cross-Region Hedging for Collection Metadata and Partition Key Range Cache Population**

- **Author:** Debdatta Kunda
- **Date:** 2026-05-29
- **Status:** Draft
- **Related:** PR [#5829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5829) (Gateway hedging kill-switch), Issue [#5642](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5642) (first-attempt HTTP timeout raise)

---

> **Scope update (PR [#5923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5923)).**
> Metadata hedging is **no longer limited to the cold-start window.** It now
> applies to **both** the first-population (cold-start) read **and** steady-state
> **refresh / force-refresh** reads of the two metadata caches
> (`ClientCollectionCache`, `PartitionKeyRangeCache`). The restriction that still
> holds is by **request type** — only `Collection` `Read` and `PartitionKeyRange`
> `ReadFeed` (first page) are hedged. The `IsColdStart` signal is retained for
> **diagnostics only** and no longer gates eligibility; the
> `MetadataHedgeSkipReason.NotColdStart` value is retired (never produced). All
> other amplification safeguards are unchanged (1.5&nbsp;s threshold, per-client
> concurrency budget, one-hedge-per-operation latch, first-page-only PK-range
> gate, cross-region dedup, single-region skip). The historical "ColdStart"
> tokens in the env var (`AZURE_COSMOS_METADATA_HEDGING_FOR_COLDSTART_ENABLED`),
> opt-in parameter, and this document's filename are retained for the broader
> feature. Where the sections below say "cold start only", read them through this
> note. See §6 for the current eligibility list.

---

## 1. Background and Motivation

Cross-region hedging in the Cosmos DB .NET SDK is currently scoped to the data plane only. The `CrossRegionHedgingAvailabilityStrategy` (`Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/CrossRegionHedgingAvailabilityStrategy.cs`) is invoked exclusively from `RequestInvokerHandler.AvailabilityStrategy` for the `RequestMessage` pipeline, and its `ShouldHedge()` method explicitly bails out when `ResourceType != Document`. As a result, the SDK gets no cross-region latency protection for control-plane / metadata operations that must complete before the first document operation can be served.

During cold start, every `CosmosClient` must populate three foundational caches before issuing any data-plane work:

- **Database Account properties** (`GatewayAccountReader`) — establishes regions, write/read endpoints.
- **Container metadata** (`ClientCollectionCache.ReadCollectionAsync`) — used to resolve collection RID, indexing/partitioning settings.
- **Partition Key Range routing map** (`PartitionKeyRangeCache.GetRoutingMapForCollectionAsync`) — used to route by partition key.

All three cache loads flow through the Gateway HTTP path. The HTTP layer applies `HttpTimeoutPolicyControlPlaneRetriableHotPath` whose first-attempt timeout was recently raised from 500 ms to **1 s** (issue [#5642](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5642)). When the primary region's Gateway is healthy, the impact is invisible; but when the primary region is degraded — brownout, partial failure, AAD slowness, transient TLS/connection establishment churn — a single slow metadata response can stall the entire client warm-up by 1 s + the local retry backoff before the next attempt completes. Production telemetry shows this as elevated p99 latency on the first operation issued by a cold client, which is exactly the request that customer SLOs are most sensitive to.

The existing `MetadataRequestThrottleRetryPolicy` already supports cross-region fallback, but only *reactively* (after a failure or `503` / `500` / `Gone+LeaseNotFound` / `Forbidden+DatabaseAccountNotFound` — verified at `MetadataRequestThrottleRetryPolicy.cs:152-161` and `:194-203`) and only *sequentially*. There is no proactive, latency-driven hedging path for metadata reads today.

**Relationship to in-flight work.** This design must compose with two adjacent PRs that touch the same surface:

- **PR [#5780](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5780)** (open, very active) — rewires `MetadataRequestThrottleRetryPolicy` to treat a broader set of failure signals (including `HttpRequestException` and non-user `OperationCanceledException`) as regional failures and call `MarkEndpointUnavailableForRead()` on the failing endpoint. The interaction matters: this design's hedge-loser cancellation **must not** be observed by the retry policy, or every successful hedge would poison the healthy secondary for 5 minutes. The structural guarantee is in §5.7.1.
- **PR [#5829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5829)** (in review) — adds the Gateway-controlled `disableCrossRegionalHedging` kill-switch on `DocumentClient`. This design honors it as the highest-precedence eligibility check (§6 rule 1) and reads it live per-request (no cached copy — §7.1).

---

## 2. Problem Statement

There is no proactive cross-regional hedging mechanism for metadata cache population during cold start. As a result, cold-start clients pay the full first-attempt HTTP timeout (1 s + backoff) any time the primary-region Gateway is slow but not outright failing on critical metadata endpoints (Collection read, PartitionKeyRange ReadFeed). Because Document operations block on these caches, this slow-but-not-failing window directly inflates the client's perceived first-operation latency — the metric most operators and customers use to judge SDK responsiveness.

---

## 3. Goals

- Reduce the metadata tail latency contributed by slow (but non-failing) Gateway responses, by issuing a hedged request to a secondary region after a bounded threshold.
- Apply to metadata reads of the two in-scope caches in **both** the cold-start (first-population) window **and** steady-state refresh / force-refresh reads. (Updated by PR [#5923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5923); the original design scoped this to cold start only.)
- Scope strictly to **two metadata resource types**: `ResourceType.Collection` (Read) and `ResourceType.PartitionKeyRange` (ReadFeed, **first page only**).
- Use a hedge threshold of **~1.5 s** by default — greater than the current first local HTTP retry timeout (1 s in `HttpTimeoutPolicyControlPlaneRetriableHotPath`) — so the local retry attempt is allowed to complete before a cross-region request is issued.
- Coexist cleanly with the existing `MetadataRequestThrottleRetryPolicy` cross-region fallback — no double counting of attempts, no two paths racing to the same secondary region.
- Honor the existing PPAF Gateway kill-switch (`disableCrossRegionalHedging` account property): when the flag is `true`, metadata hedging is also suppressed.
- Emit complete, branch-level diagnostics for supportability (eligible, fired, winner, loser, skip reason).
- Preserve current behavior for single-region accounts and for any read whose primary region responds within the hedge threshold (no hedge is dispatched).

---

## 4. Non-Goals

- Hedging account-properties reads (`GatewayAccountReader`). The account read happens before regions are known and is governed by separate multi-region/retry logic; it is a likely follow-up candidate but is out of scope here.
- Hedging address resolution / `GlobalAddressResolver` requests used by the Direct (TCP) store model.
- Hedging data-plane (`Document`) requests — already covered by the existing `CrossRegionHedgingAvailabilityStrategy`.
- Hedging `PartitionKeyRange` ReadFeed pages beyond the first page (subsequent pages must remain sticky to the winning region to keep ETag/continuation state consistent).
- Hedging on single-region accounts (no hedge target exists).
- Hedging in Direct mode for any metadata path other than the Gateway HTTP path described above.
- Permanent, lifetime-long metadata hedging.
- Database / Offer / User / Trigger / Stored Procedure metadata reads.

---

## 5. Proposed Design

Introduce a dedicated internal helper, **`MetadataHedgingStrategy`**, that wraps a single metadata send attempt with bounded cross-region hedging. The helper is intentionally a separate type from `CrossRegionHedgingAvailabilityStrategy`: the data-plane strategy is wired into the `RequestMessage` pipeline and is gated on `ResourceType.Document`; reusing it would either blur its eligibility checks or require invasive surgery to make it polymorphic across pipelines. A focused, narrowly-scoped helper is safer.

The wrap is applied **inside** the `BackoffRetryUtility` / `MetadataRequestThrottleRetryPolicy` retry loop, not outside it. Each retry attempt is independently eligible for at most one hedge branch, so total attempts are bounded by `retry_count × 1 hedge`, not multiplied combinatorially with retries.

### 5.1 Public-shape configuration

A single new opt-in is added to `CosmosClientOptions`, plus a small public tuning surface that customers will need once the feature defaults on (because a fixed per-client budget of 8 is not appropriate for every workload — e.g., apps that initialize 50+ containers at startup):

```csharp
namespace Microsoft.Azure.Cosmos
{
    public class CosmosClientOptions
    {
        // ... existing members ...

        /// <summary>
        /// Tri-state opt-in / kill-switch for cold-start metadata hedging.
        ///   null  → follow the PPAF default for the current release phase
        ///           (Phase 1: off; Phase 2: on for canary; Phase 3+: on)
        ///   true  → force the feature on (overrides phase default; still honors
        ///           the Gateway kill-switch <c>disableCrossRegionalHedging</c>)
        ///   false → force the feature off (overrides phase default; lets a
        ///           customer back out without downgrading the SDK)
        /// This property is kept across all phases as a permanent operator
        /// override so the Phase 3 "remove the opt-in" step is NOT a binary
        /// break — see §12.
        /// </summary>
        public bool? EnableMetadataHedgingForColdStart { get; set; }

        /// <summary>
        /// Optional tuning for cold-start metadata hedging. Defaults are
        /// derived from <see cref="HttpTimeoutPolicy"/> at startup (see §5.9).
        /// Public from day one so customers can adjust the per-client budget
        /// for high-container-cardinality startups without disabling the
        /// feature entirely.
        /// </summary>
        public MetadataHedgingOptions MetadataHedgingOptions { get; set; }
    }

    /// <summary>
    /// Tuning knobs for cold-start metadata hedging. All properties optional;
    /// any null value falls back to the SDK-derived default.
    /// </summary>
    public sealed class MetadataHedgingOptions
    {
        /// <summary>Time after which the hedge branch is dispatched if the
        /// primary has not produced an acceptable response (per the §5.13
        /// per-branch overlay over <c>RetryUtility.IsRegionalFailure</c>). Default:
        /// <c>firstControlPlaneRetriableTimeout + 500 ms</c> (today: 1.5 s).</summary>
        public TimeSpan? Threshold { get; set; }

        /// <summary>Reserved for future use when MaxHedgeBranchesPerAttempt &gt; 1.
        /// Default: 500 ms.</summary>
        public TimeSpan? ThresholdStep { get; set; }

        /// <summary>Maximum simultaneous hedge branches per attempt.
        /// Default: 1 (one secondary region). Values &gt; 1 are reserved
        /// for a future release.</summary>
        public int MaxHedgeBranchesPerAttempt { get; set; } = 1;

        /// <summary>Per-client cap on in-flight metadata hedges. Default: 8.
        /// Customers initializing many containers at startup may raise this
        /// (e.g., 32) to keep the secondary Gateway from being starved of
        /// hedge slots. Customers in cost-sensitive scenarios may lower it.</summary>
        public int PerClientConcurrencyBudget { get; set; } = 8;
    }
}
```

**Rollout discipline:** `EnableMetadataHedgingForColdStart` is `bool?` (tri-state) and **never removed** in any phase — only its default behavior changes. This guarantees that source/binary code referencing `new CosmosClientOptions { EnableMetadataHedgingForColdStart = true }` keeps compiling and linking across every release that ships this feature. `MetadataHedgingOptions` is public from day one because customers cannot tune `PerClientConcurrencyBudget` from outside the assembly otherwise — the 8-slot default starves any workload that initializes more than 8 cold containers simultaneously and the customer has no recourse short of disabling the feature.

### 5.2 `MetadataHedgingStrategy` — class structure

The helper is a stateless-per-call orchestrator with a small amount of per-client state (the concurrency semaphore and the resolved threshold values).

```csharp
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Bounded cross-region hedging for cold-start metadata cache population.
    /// One instance per CosmosClient. Used by ClientCollectionCache and
    /// PartitionKeyRangeCache. Honors the PPAF Gateway kill-switch.
    /// </summary>
    internal sealed class MetadataHedgingStrategy : IDisposable
    {
        // -------- Configuration --------
        private readonly TimeSpan threshold;          // default: firstCpRetryTimeout + 500ms
        private readonly TimeSpan thresholdStep;      // default: 500ms (currently unused; reserved)
        private readonly int maxBranchesPerAttempt;   // default: 1

        // -------- Dependencies --------
        private readonly IGlobalEndpointManager globalEndpointManager;
        private readonly IStoreModel gatewayStoreModel;
        private readonly Func<bool> isHedgingDisabledByGateway;     // bound to DocumentClient flag (PR #5829)
        private readonly Func<bool> isPpafEnabled;                  // bound to ConnectionPolicy.EnablePartitionLevelFailover
        private readonly bool isOptInEnabled;                       // CosmosClientOptions.EnableMetadataHedgingForColdStart

        // -------- Per-client concurrency budget --------
        private readonly SemaphoreSlim hedgeBudget;                 // size: PerClientConcurrencyBudget

        public MetadataHedgingStrategy(
            IGlobalEndpointManager globalEndpointManager,
            IStoreModel gatewayStoreModel,
            Func<bool> isHedgingDisabledByGateway,
            Func<bool> isPpafEnabled,
            MetadataHedgingOptions options,
            bool isOptInEnabled);

        // -------- Primary API --------

        /// <summary>
        /// Execute a single metadata send with optional cross-region hedging.
        /// Caller supplies a region-targeted send delegate that respects request.RequestContext.LocationEndpointToRoute.
        /// </summary>
        public Task<MetadataHedgingResult> ExecuteAsync(
            DocumentServiceRequest request,
            Func<DocumentServiceRequest, CancellationToken, Task<DocumentServiceResponse>> sendToCurrentlyRoutedEndpoint,
            MetadataHedgingContext hedgeContext,
            ITrace trace,
            CancellationToken cancellationToken);

        // -------- Eligibility (public so callers can short-circuit early) --------

        public MetadataHedgeEligibility EvaluateEligibility(
            DocumentServiceRequest request,
            MetadataHedgingContext hedgeContext);

        public void Dispose();   // disposes SemaphoreSlim
    }

    /// <summary>
    /// Per-logical-operation context shared between the strategy and
    /// MetadataRequestThrottleRetryPolicy. Used to dedupe regions across hedge + retry,
    /// to pin PK-range pagination to the winning region, and to bound the hedge to
    /// at most one dispatch per logical operation (across BackoffRetryUtility retries).
    ///
    /// Thread-safety: all mutable state is guarded as documented per member.
    /// The strategy mutates from the orchestration thread; the retry policy reads
    /// from the BackoffRetryUtility thread; these may overlap with late-arriving
    /// loser continuations on a thread-pool thread.
    /// </summary>
    internal sealed class MetadataHedgingContext
    {
        public bool IsColdStart { get; set; }                    // set by caller from previousValue == null
        public ResourceType ResourceType { get; set; }

        // -------- Set exactly once after the first attempt's winner is decided --------
        // Backed by Interlocked.CompareExchange to give a single-publication guarantee
        // even if a late loser continuation tries to re-publish; first writer wins.
        private Uri winningEndpoint;
        private string winningRegion;
        public Uri WinningEndpoint => Volatile.Read(ref this.winningEndpoint);
        public string WinningRegion => Volatile.Read(ref this.winningRegion);

        // -------- Cross-thread shared, thread-safe --------
        // ConcurrentDictionary<string,byte> keyed on Uri.AbsoluteUri (not
        // ConcurrentDictionary<Uri,byte>) because Uri equality uses the
        // default reference / hash that includes UserInfo, Fragment, and case
        // semantics that have surprised callers in the past. AbsoluteUri is the
        // canonical normalized form used by GlobalEndpointManager throughout
        // LocationCache and avoids any drift between probe-resolved endpoints
        // and the keys stored at hedge dispatch time.
        public ConcurrentDictionary<string, byte> AttemptedEndpoints { get; }
            = new ConcurrentDictionary<string, byte>();

        public bool IsFirstReadFeedPage { get; set; }            // PK-range only; subsequent pages skip hedge

        // -------- Bounded amplification guard (one hedge per logical operation) --------
        // Set to 1 by the first hedge dispatch via Interlocked.Exchange. Subsequent
        // BackoffRetryUtility retries observe this and skip the hedge branch even
        // when IsColdStart is still true (the cache has not yet been populated
        // because the loop has not yet exited successfully). See §6.1.
        private int hasHedgedThisOperation;   // 0 = no, 1 = yes
        public bool HasHedgedThisOperation => Volatile.Read(ref this.hasHedgedThisOperation) == 1;
        internal bool TryMarkHedgedThisOperation()
            => Interlocked.Exchange(ref this.hasHedgedThisOperation, 1) == 0;

        // Single-publication via Interlocked.CompareExchange. First caller wins;
        // late loser continuations that arrive after the winner has already been
        // recorded are no-ops (the second CompareExchange observes a non-null
        // existing value and leaves it intact). The two fields are independently
        // CAS'd but the (endpoint, region) pair is computed by a single caller
        // before invocation, so tearing across them is not possible.
        internal void RecordWinner(Uri endpoint, string region)
        {
            Interlocked.CompareExchange(ref this.winningEndpoint, endpoint, null);
            Interlocked.CompareExchange(ref this.winningRegion,   region,   null);
        }
    }

    internal readonly struct MetadataHedgingResult
    {
        public DocumentServiceResponse Response { get; }
        public Uri WinningEndpoint { get; }
        public string WinningRegion { get; }
        public bool HedgeFired { get; }
        public MetadataHedgeDiagnostics Diagnostics { get; }
    }

    internal enum MetadataHedgeSkipReason
    {
        None,
        OptInDisabled,
        PpafDisabled,
        GatewayKillSwitchOn,
        SingleRegion,
        NotColdStart,
        ResourceTypeNotSupported,
        NotFirstReadFeedPage,
        BudgetExhausted,
        AlreadyHedgedThisOperation,        // see §6.1
        ExcludedRegionLeavesNoTarget,       // see §6 rule 8
        AuthModeNotEligibleForHedge,        // see §5.13
    }

    internal readonly struct MetadataHedgeEligibility
    {
        public bool IsEligible { get; }
        public MetadataHedgeSkipReason SkipReason { get; }
    }

    /// <summary>
    /// Diagnostic record attached to the request's trace. Populated by the strategy
    /// from a single thread for the eligibility/winner fields; <c>LoserOutcome</c>
    /// and <c>HedgeOutcome</c> may be updated from off-orchestration-thread continuations
    /// (BackgroundCleanupAsync) so they are read/written through <see cref="Volatile"/>.
    /// The trace datum is published (via <c>trace.AddDatum</c>) only after the winner
    /// is decided; consumers reading the datum must tolerate these fields updating
    /// later (see §10).
    /// </summary>
    internal sealed class MetadataHedgeDiagnostics
    {
        public bool Eligible { get; set; }
        public MetadataHedgeSkipReason SkipReason { get; set; }
        public string PrimaryRegion { get; set; }
        public string HedgeRegion { get; set; }
        public double ThresholdMs { get; set; }
        public double? HedgeFiredElapsedMs { get; set; }
        public string WinningRegion { get; set; }

        // Recorded by the winner-decision path (§5.3 step 5a / step 6). Values:
        //   "Won"      — hedge was the acceptable winner.
        //   "Auth401"  — hedge returned 401 and was rejected by IsAcceptableWinner.
        //   "Auth403"  — hedge returned plain 403 (sub-status NOT DatabaseAccountNotFound)
        //                and was rejected by IsAcceptableWinner.
        //   null       — hedge did not fire, or primary won outright.
        // Volatile because BackgroundCleanupAsync may also set this from an off-
        // orchestration-thread continuation when the loser was the hedge.
        private string hedgeOutcome;
        public string HedgeOutcome
        {
            get => Volatile.Read(ref this.hedgeOutcome);
            set => Volatile.Write(ref this.hedgeOutcome, value);
        }

        private string loserOutcome;
        public string LoserOutcome
        {
            get => Volatile.Read(ref this.loserOutcome);
            set => Volatile.Write(ref this.loserOutcome, value);
        }
        public int TotalAttempts { get; set; }
    }
}
```

### 5.3 `ExecuteAsync` — control flow

The orchestration mirrors `CrossRegionHedgingAvailabilityStrategy.ExecuteAvailabilityStrategyAsync` but is significantly simpler because (a) only one hedge branch is created per attempt, and (b) the request is already a `DocumentServiceRequest`, not a `RequestMessage` requiring cloning of `RequestOptions`/`Properties`.

**Cancellation model.** Each branch (primary, hedge) and the threshold timer use **separate** `CancellationTokenSource`s, each linked to the caller's `CancellationToken`. The losing branch is cancelled *selectively* (its own CTS), never via a shared linked CTS. This is critical because of how `MetadataRequestThrottleRetryPolicy` interprets cancellation (see §5.7): a hedge-loser `OperationCanceledException` must **not** be observable by the retry policy as a "regional failure" signal, or every successful hedge would call `MarkEndpointUnavailableForRead()` on a healthy secondary and poison it for the 5-minute `LocationCache` TTL. By containing the loser's exception inside `BackgroundCleanupAsync` (below) — never letting it escape `ExecuteAsync` — we guarantee the retry policy only ever sees the winner's outcome.

```csharp
public async Task<MetadataHedgingResult> ExecuteAsync(
    DocumentServiceRequest request,
    Func<DocumentServiceRequest, CancellationToken, Task<DocumentServiceResponse>> sendToCurrentlyRoutedEndpoint,
    MetadataHedgingContext hedgeContext,
    ITrace trace,
    CancellationToken cancellationToken)
{
    MetadataHedgeDiagnostics diag = new MetadataHedgeDiagnostics();

    // ---- 1. Eligibility ----
    MetadataHedgeEligibility eligibility = this.EvaluateEligibility(request, hedgeContext);
    diag.Eligible = eligibility.IsEligible;
    diag.SkipReason = eligibility.SkipReason;
    diag.ThresholdMs = this.threshold.TotalMilliseconds;

    if (!eligibility.IsEligible)
    {
        DocumentServiceResponse primaryOnly = await sendToCurrentlyRoutedEndpoint(request, cancellationToken);
        diag.TotalAttempts = 1;
        diag.PrimaryRegion = this.globalEndpointManager.GetLocation(request.RequestContext.LocationEndpointToRoute);
        diag.WinningRegion = diag.PrimaryRegion;
        trace.AddDatum("Metadata Hedge Context", diag);
        return new MetadataHedgingResult(primaryOnly, request.RequestContext.LocationEndpointToRoute, diag.PrimaryRegion, hedgeFired: false, diag);
    }

    // ---- 2. Acquire concurrency budget (true non-blocking sync check) ----
    // Wait(0) is the cheap synchronous primitive and allocates no continuation task,
    // unlike WaitAsync(TimeSpan.Zero) which always returns a Task.
    if (!this.hedgeBudget.Wait(TimeSpan.Zero))
    {
        diag.SkipReason = MetadataHedgeSkipReason.BudgetExhausted;
        diag.Eligible = false;
        DocumentServiceResponse primaryOnly = await sendToCurrentlyRoutedEndpoint(request, cancellationToken);
        trace.AddDatum("Metadata Hedge Context", diag);
        return new MetadataHedgingResult(primaryOnly, request.RequestContext.LocationEndpointToRoute, /*region*/ null, hedgeFired: false, diag);
    }

    CancellationTokenSource primaryCts = null;
    CancellationTokenSource hedgeCts   = null;
    CancellationTokenSource timerCts   = null;

    try
    {
        primaryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        hedgeCts   = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timerCts   = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // ---- 3. Resolve primary + hedge endpoints ----
        Uri primaryEndpoint = request.RequestContext.LocationEndpointToRoute
                              ?? this.globalEndpointManager.ResolveServiceEndpoint(request);
        ReadOnlyCollection<Uri> applicable = this.globalEndpointManager
            .GetApplicableEndpoints(request, isReadRequest: true);
        // Pick the next preferred secondary (in PreferredLocations order). NOT a
        // proximity-based "closest" — the SDK has no proximity measurement; it
        // honors customer-supplied ordering. ExcludeRegions (if set on the request)
        // are filtered out upstream in GetApplicableEndpoints.
        Uri hedgeEndpoint = applicable.FirstOrDefault(u => !u.Equals(primaryEndpoint));

        if (hedgeEndpoint == null)
        {
            diag.SkipReason = MetadataHedgeSkipReason.SingleRegion;
            diag.Eligible = false;
            DocumentServiceResponse primaryOnly = await sendToCurrentlyRoutedEndpoint(request, cancellationToken);
            trace.AddDatum("Metadata Hedge Context", diag);
            return new MetadataHedgingResult(primaryOnly, primaryEndpoint, this.globalEndpointManager.GetLocation(primaryEndpoint), hedgeFired: false, diag);
        }

        diag.PrimaryRegion = this.globalEndpointManager.GetLocation(primaryEndpoint);
        diag.HedgeRegion   = this.globalEndpointManager.GetLocation(hedgeEndpoint);

        // ---- 4. Launch primary + delayed hedge ----
        DocumentServiceRequest primaryReq = request;   // already routed to primary
        hedgeContext.AttemptedEndpoints.TryAdd(primaryEndpoint.AbsoluteUri, 0);

        Stopwatch sw = Stopwatch.StartNew();
        Task<DocumentServiceResponse> primaryTask = SendOneAsync(sendToCurrentlyRoutedEndpoint, primaryReq, primaryCts.Token);

        Task hedgeTimer = Task.Delay(this.threshold, timerCts.Token);

        Task firstCompleted = await Task.WhenAny(primaryTask, hedgeTimer);

        // ---- 5a. Primary genuinely won before threshold ----
        // Three conditions must all hold:
        //   (i)   primary completed (not the timer)
        //   (ii)  primary RanToCompletion (not faulted/cancelled): if the primary
        //         FAULTS before threshold (HttpRequestException, socket reset,
        //         TaskCanceledException from HttpTimeoutPolicy first-attempt), we
        //         intentionally fall through to dispatch the hedge — fast-fail on
        //         a degraded primary is one of the most common cold-start tail
        //         scenarios this design is meant to mitigate.
        //   (iii) the response is an acceptable winner for the primary branch.
        //         For the primary, this is identical to !IsRegionalFailure(resp);
        //         the per-branch overlay only changes behavior for the hedge.
        if (firstCompleted == primaryTask
            && primaryTask.Status == TaskStatus.RanToCompletion
            && IsAcceptableWinner(primaryTask.Result, HedgeBranch.Primary))
        {
            timerCts.Cancel();              // only cancels the Task.Delay, not anything observable to the retry policy
            diag.TotalAttempts = 1;
            diag.WinningRegion = diag.PrimaryRegion;
            hedgeContext.RecordWinner(primaryEndpoint, diag.PrimaryRegion);
            trace.AddDatum("Metadata Hedge Context", diag);
            return new MetadataHedgingResult(primaryTask.Result, primaryEndpoint, diag.PrimaryRegion, hedgeFired: false, diag);
        }

        // ---- 5b. Threshold elapsed (or primary faulted/transient) → re-check kill switch, dispatch hedge ----
        if (this.isHedgingDisabledByGateway())
        {
            diag.SkipReason = MetadataHedgeSkipReason.GatewayKillSwitchOn;
            // Observe primary entirely; do not throw OCE from this method.
            DocumentServiceResponse primaryLate = await ObserveWinningTaskAsync(primaryTask);
            trace.AddDatum("Metadata Hedge Context", diag);
            return new MetadataHedgingResult(primaryLate, primaryEndpoint, diag.PrimaryRegion, hedgeFired: false, diag);
        }

        DocumentServiceRequest hedgeReq = CloneForHedge(primaryReq, hedgeEndpoint);
        hedgeContext.AttemptedEndpoints.TryAdd(hedgeEndpoint.AbsoluteUri, 0);
        hedgeContext.TryMarkHedgedThisOperation();      // suppresses re-hedge on later BackoffRetryUtility retries (§6.1)
        diag.HedgeFiredElapsedMs = sw.Elapsed.TotalMilliseconds;
        Task<DocumentServiceResponse> hedgeTask = SendOneAsync(sendToCurrentlyRoutedEndpoint, hedgeReq, hedgeCts.Token);

        // ---- 6. Wait for first ACCEPTABLE winner (not just first-completed) ----
        // First-completed semantics would let a fast 503 from the hedge beat a
        // healthy 200 from the primary that lands 2 ms later, regressing the
        // §6.1 "no attempt amplification" claim. Per-branch overlay also rejects
        // 401/plain-403 from the hedge branch (§5.13 hedge-auth guard) so an RBAC
        // misconfiguration in the secondary cannot poison the operation result.
        // Loop until either a branch produces an acceptable winner or both
        // branches have settled.
        Task<DocumentServiceResponse>[] remaining = new[] { primaryTask, hedgeTask };
        Task<DocumentServiceResponse> winner = null;
        Task<DocumentServiceResponse> loser  = null;

        while (true)
        {
            Task<DocumentServiceResponse> finished = await Task.WhenAny(remaining);
            HedgeBranch branch = (finished == primaryTask) ? HedgeBranch.Primary : HedgeBranch.Hedge;

            if (finished.Status == TaskStatus.RanToCompletion
                && IsAcceptableWinner(finished.Result, branch))
            {
                winner = finished;
                loser  = (finished == primaryTask) ? hedgeTask : primaryTask;
                if (branch == HedgeBranch.Hedge && diag.HedgeOutcome == null)
                {
                    diag.HedgeOutcome = "Won";
                }
                break;
            }

            // If the hedge returned with a per-branch reject (401/403), record it for
            // diagnostics before we drop the response.
            if (branch == HedgeBranch.Hedge
                && finished.Status == TaskStatus.RanToCompletion
                && (finished.Result.StatusCode == HttpStatusCode.Unauthorized
                    || finished.Result.StatusCode == HttpStatusCode.Forbidden))
            {
                diag.HedgeOutcome = finished.Result.StatusCode == HttpStatusCode.Unauthorized
                    ? "Auth401"
                    : "Auth403";
            }

            if (remaining.Length == 1)
            {
                // Both branches have now settled and neither produced an acceptable winner.
                // Prefer the primary's outcome (preserves "primary's exception via ExceptionDispatchInfo"
                // contract — §10). If the primary settled transient/faulted but the hedge faulted
                // outright, the primary's transient response is still the surfaced outcome.
                winner = (primaryTask.IsCompleted) ? primaryTask : hedgeTask;
                loser  = (winner == primaryTask) ? hedgeTask : primaryTask;
                break;
            }

            remaining = (finished == primaryTask) ? new[] { hedgeTask } : new[] { primaryTask };
        }

        Uri winningEndpoint  = (winner == primaryTask) ? primaryEndpoint : hedgeEndpoint;
        string winningRegion = this.globalEndpointManager.GetLocation(winningEndpoint);

        diag.WinningRegion = winningRegion;
        diag.TotalAttempts = 2;
        hedgeContext.RecordWinner(winningEndpoint, winningRegion);

        // Cancel ONLY the loser's CTS — never a shared linkedCts. The loser's
        // OperationCanceledException is observed by BackgroundCleanupAsync and
        // never escapes this method, so MetadataRequestThrottleRetryPolicy
        // (post-#5780) cannot misclassify it as a regional failure. See §5.7.
        // OWNERSHIP TRANSFER: the loser's CTS is handed to BackgroundCleanupAsync,
        // which becomes responsible for its disposal. Null out the local ref so
        // the outer finally does not double-dispose it.
        CancellationTokenSource loserCts;
        if (loser == primaryTask)
        {
            loserCts = primaryCts;
            primaryCts = null;          // transfer ownership to BackgroundCleanupAsync
        }
        else
        {
            loserCts = hedgeCts;
            hedgeCts = null;            // transfer ownership to BackgroundCleanupAsync
        }
        loserCts.Cancel();

        // Fire-and-forget cleanup: awaits the loser, disposes its response body
        // (handle leak fix), updates diag.LoserOutcome, and disposes the loser CTS.
        // Swallows OperationCanceledException and any other loser-thrown exception.
        _ = BackgroundCleanupAsync(loser, loserCts, diag);

        trace.AddDatum("Metadata Hedge Context", diag);

        // ObserveWinningTaskAsync re-raises the winner's exception (if any) via
        // ExceptionDispatchInfo.Capture(...).Throw() — preserves the throwing-frame
        // stack across the await boundary. See §5.12 for net472 unwind discipline.
        DocumentServiceResponse winningResponse = await ObserveWinningTaskAsync(winner);
        return new MetadataHedgingResult(winningResponse, winningEndpoint, winningRegion, hedgeFired: true, diag);
    }
    finally
    {
        // Disposes whichever CTSs are still locally owned. The loser's CTS, if
        // any, was nulled out above when transferred to BackgroundCleanupAsync.
        // The winner's CTS and the timer CTS are always still locally owned.
        primaryCts?.Dispose();
        hedgeCts?.Dispose();
        timerCts?.Dispose();
        this.hedgeBudget.Release();
    }
}

// Middle-layer seam — see §5.12 for why this method exists (net472 stack-unwind discipline).
private static async Task<DocumentServiceResponse> SendOneAsync(
    Func<DocumentServiceRequest, CancellationToken, Task<DocumentServiceResponse>> send,
    DocumentServiceRequest req,
    CancellationToken ct)
{
    try
    {
        return await send(req, ct).ConfigureAwait(false);
    }
    catch
    {
        // Yield once so the synchronous exception-unwind stack does not traverse
        // every awaiting hedge frame on net472. See PR #5870 for the precedent.
        await Task.Yield();
        throw;
    }
}

private static async Task<DocumentServiceResponse> ObserveWinningTaskAsync(
    Task<DocumentServiceResponse> winner)
{
    try
    {
        return await winner.ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        // Preserve the throwing-frame call stack across the await boundary.
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        throw; // unreachable
    }
}

private static async Task BackgroundCleanupAsync(
    Task<DocumentServiceResponse> loser,
    CancellationTokenSource loserCts,
    MetadataHedgeDiagnostics diag)
{
    try
    {
        DocumentServiceResponse loserResp = await loser.ConfigureAwait(false);
        try { loserResp?.Dispose(); } catch { /* never let dispose throw out */ }
        diag.LoserOutcome = "CompletedAfterWinner";
    }
    catch (OperationCanceledException)
    {
        diag.LoserOutcome = "Cancelled";
    }
    catch (Exception ex)
    {
        diag.LoserOutcome = $"Faulted({ex.GetType().Name})";
    }
    finally
    {
        loserCts.Dispose();
    }
}
```

`IsAcceptableWinner(response, branch)` is defined in §5.13 and composes `RetryUtility.IsRegionalFailure(...)` with a per-branch overlay that rejects 401 / plain 403 from the **hedge** branch (the RBAC-role-assignment-missing-in-secondary case). For the **primary** branch, `IsAcceptableWinner` is identical to `!IsRegionalFailure(...)` — i.e., a 401 from the primary still surfaces to the caller normally. This shared helper consolidates the 3-to-4 copies of the failure-class list that exist or are being added today across PR #5780 and this design. For 404 we treat it as an acceptable winner regardless of branch — both branches racing to 404 is benign; the first wins.

`CloneForHedge` creates a `DocumentServiceRequest` copy with `request.RequestContext.RouteToLocation(hedgeEndpoint)` set, cloned headers (notably re-signed `Authorization` if the token is bound to URI — see §5.13 for full per-auth-mode handling), and a fresh `ClientRequestStatistics` snapshot to keep diagnostics from the two branches separable.

### 5.4 Wiring point #1 — `ClientCollectionCache.ReadCollectionAsync`

**Before** (current code, `Microsoft.Azure.Cosmos/src/Routing/ClientCollectionCache.cs:230-262`):

```csharp
retryPolicyInstance?.OnBeforeSendRequest(request);

try
{
    using (DocumentServiceResponse response =
        await this.storeModel.ProcessMessageAsync(request))
    {
        ContainerProperties containerProperties = CosmosResource.FromStream<ContainerProperties>(response);
        // ... telemetry ...
        return containerProperties;
    }
}
catch (DocumentClientException ex) { /* ... */ }
```

**After:**

```csharp
retryPolicyInstance?.OnBeforeSendRequest(request);

MetadataHedgingContext hedgeContext = new MetadataHedgingContext
{
    IsColdStart = isFirstPopulation,           // see §5.6 — caller-supplied, NEVER inferred
    ResourceType = ResourceType.Collection,
};

// Share the attempted-endpoints set with the retry policy to dedupe regions.
// `as` (not hard cast) — the retry policy may be wrapped by a decorator or
// substituted with a test double; in those cases hedge still runs, dedup
// degrades to a no-op (see §5.7).
(retryPolicyInstance as MetadataRequestThrottleRetryPolicy)?.AttachHedgeContext(hedgeContext);

try
{
    MetadataHedgingResult result = await this.metadataHedgingStrategy.ExecuteAsync(
        request: request,
        sendToCurrentlyRoutedEndpoint: async (req, ct) =>
        {
            return await this.storeModel.ProcessMessageAsync(req, ct);
        },
        hedgeContext: hedgeContext,
        trace: childTrace,
        cancellationToken: cancellationToken);

    using (DocumentServiceResponse response = result.Response)
    {
        ContainerProperties containerProperties = CosmosResource.FromStream<ContainerProperties>(response);
        // ... telemetry (unchanged) ...
        return containerProperties;
    }
}
catch (DocumentClientException ex) { /* unchanged */ }
```

`isFirstPopulation` is determined by the caller. Implementation detail (and the central correction vs. the spec's earlier draft): the two metadata caches use **different** async-cache primitives, so they need different plumbing:

- **`ClientCollectionCache` uses `AsyncCache<string, ContainerProperties>`** (`CollectionCache.cs:34`), **not** `AsyncCacheNonBlocking`. `AsyncCache.GetAsync` takes `Func<Task<TValue>>` — the factory does **not** see the previous value. Therefore `isColdStart` must be **threaded in explicitly from each caller**: it cannot be inferred from "previous value was null" inside the factory, because `ClientCollectionCache.ResolveByNameAsync(forceRefresh: true)` `TryRemoveIfCompleted`s the key and re-issues `GetAsync` with `obsoleteValue: null`, which looks identical to a true cold start to the factory. Force-refresh must NOT hedge. The concrete mapping is **`isColdStart = !forceRefresh`** — see §5.6 for the walk-through.
- **`PartitionKeyRangeCache` uses `AsyncCacheNonBlocking<…>`** (`PartitionKeyRangeCache.cs:28`). Its factory receives `previousValue`; the cold-start signal can be derived as `previousValue == null` *inside* the factory. (But by convention we still set `isColdStart` on the `MetadataHedgingContext` from the caller, to keep the two paths symmetric.)

**Plumbing change for `ClientCollectionCache` — virtual overload, not a new parameter on the abstract.** `CollectionCache.GetByNameAsync` / `GetByRidAsync` are **`protected abstract`** on the base class (`CollectionCache.cs:211-221`). Adding a parameter to an `abstract` method — *even with a default value* — changes the method's signature; existing subclass overrides (the encryption-mirrored caches in `Microsoft.Azure.Cosmos.Encryption*` and any customer/test subclass) override a method that **no longer exists in that form** and fail to compile. Default values only affect what callers may omit at the call site; they do not make an old override automatically satisfy the new abstract contract.

We therefore add a **new `protected virtual` overload** that takes `isColdStart` and delegates to the existing `abstract` method:

```csharp
internal abstract class CollectionCache : IDisposable
{
    // Existing abstract (unchanged signature — preserves source compat for all subclass overrides).
    protected abstract Task<ContainerProperties> GetByNameAsync(
        string apiVersion, string resourceFullName, IClientSideRequestStatistics csrs,
        ITrace trace, IClientSideRequestStatistics clientSideRequestStatistics, CancellationToken ct);

    // NEW: hedge-aware overload. Default implementation discards isColdStart and
    // calls the existing abstract — encryption subclasses that don't override this
    // simply inherit hedge-disabled behavior, which is the desired default.
    protected virtual Task<ContainerProperties> GetByNameAsync(
        string apiVersion, string resourceFullName, IClientSideRequestStatistics csrs,
        ITrace trace, IClientSideRequestStatistics clientSideRequestStatistics,
        bool isColdStart, CancellationToken ct)
        => this.GetByNameAsync(apiVersion, resourceFullName, csrs, trace, clientSideRequestStatistics, ct);

    // Same pair for GetByRidAsync.
}

internal sealed class ClientCollectionCache : CollectionCache
{
    // Existing abstract override (unchanged).
    protected override Task<ContainerProperties> GetByNameAsync(...) { /* unchanged body */ }

    // NEW: hedge-aware override. Forwards isColdStart into MetadataHedgingContext.
    protected override Task<ContainerProperties> GetByNameAsync(
        ..., bool isColdStart, CancellationToken ct)
        => this.ReadCollectionAsync(resourceFullName, csrs, trace, isColdStart, ct);
}
```

`AsyncCache.GetAsync(key, obsoleteValue: null, factory: () => this.GetByNameAsync(..., isColdStart, ct), ct, forceRefresh: forceRefresh)` invokes the new virtual overload. Encryption-mirrored caches that override only the existing abstract continue to compile and run without modification; they are hedge-disabled by default (their factory invocations route through the base virtual which ignores `isColdStart`). When/if the encryption stack opts in, it overrides the new virtual independently.

### 5.5 Wiring point #2 — `PartitionKeyRangeCache.GetRoutingMapForCollectionAsync`

**Before** (`Microsoft.Azure.Cosmos/src/Routing/PartitionKeyRangeCache.cs:185-237`):

```csharp
do
{
    INameValueCollection headers = new RequestNameValueCollection();
    headers.Set(HttpConstants.HttpHeaders.PageSize, PageSizeString);
    headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);
    if (changeFeedNextIfNoneMatch != null)
    {
        headers.Set(HttpConstants.HttpHeaders.IfNoneMatch, changeFeedNextIfNoneMatch);
    }

    using (DocumentServiceResponse response = await BackoffRetryUtility<DocumentServiceResponse>.ExecuteAsync(
        () => this.ExecutePartitionKeyRangeReadChangeFeedAsync(collectionRid, headers, trace, clientSideRequestStatistics, metadataRetryPolicy),
        retryPolicy: metadataRetryPolicy))
    {
        lastStatusCode = response.StatusCode;
        changeFeedNextIfNoneMatch = response.Headers[HttpConstants.HttpHeaders.ETag];
        // ... build routing map ...
    }
}
while (lastStatusCode != HttpStatusCode.NotModified);
```

**After:**

```csharp
bool isColdStart = previousRoutingMap == null;
MetadataHedgingContext hedgeContext = new MetadataHedgingContext
{
    IsColdStart = isColdStart,
    ResourceType = ResourceType.PartitionKeyRange,
    IsFirstReadFeedPage = true,           // toggled to false after page 1
};
// `as` (not hard cast). If the retry policy is wrapped or substituted with a
// test double, hedge still runs and dedup degrades to a no-op (see §5.7).
(metadataRetryPolicy as MetadataRequestThrottleRetryPolicy)?.AttachHedgeContext(hedgeContext);

do
{
    INameValueCollection headers = /* unchanged */;

    // Page 1: hedge if eligible. Pages 2..N: pin to winning region; no hedge.
    using (DocumentServiceResponse response = await BackoffRetryUtility<DocumentServiceResponse>.ExecuteAsync(
        () => this.ExecutePartitionKeyRangeReadChangeFeedAsync(
            collectionRid,
            headers,
            trace,
            clientSideRequestStatistics,
            metadataRetryPolicy,
            hedgeContext,           // <- new
            cancellationToken),     // <- new: caller-supplied token (NOT CancellationToken.None)
        retryPolicy: metadataRetryPolicy))
    {
        lastStatusCode = response.StatusCode;
        changeFeedNextIfNoneMatch = response.Headers[HttpConstants.HttpHeaders.ETag];
        // ... build routing map (unchanged) ...
    }

    hedgeContext.IsFirstReadFeedPage = false;   // <- subsequent pages skip the hedge branch
}
while (lastStatusCode != HttpStatusCode.NotModified);
```

And the per-page send becomes:

```csharp
private async Task<DocumentServiceResponse> ExecutePartitionKeyRangeReadChangeFeedAsync(
    string collectionRid,
    INameValueCollection headers,
    ITrace trace,
    IClientSideRequestStatistics clientSideRequestStatistics,
    IDocumentClientRetryPolicy retryPolicy,
    MetadataHedgingContext hedgeContext,
    CancellationToken cancellationToken)            // <- new
{
    using (ITrace childTrace = trace.StartChild("Read PartitionKeyRange Change Feed", TraceComponent.Transport, Tracing.TraceLevel.Info))
    using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.ReadFeed, collectionRid, ResourceType.PartitionKeyRange,
                AuthorizationTokenType.PrimaryMasterKey, headers))
    {
        retryPolicy.OnBeforeSendRequest(request);

        // Pages 2..N: route to the winning region from page 1.
        if (!hedgeContext.IsFirstReadFeedPage && hedgeContext.WinningEndpoint != null)
        {
            request.RequestContext.RouteToLocation(hedgeContext.WinningEndpoint);
        }

        // ... existing auth-token plumbing ...

        MetadataHedgingResult result = await this.metadataHedgingStrategy.ExecuteAsync(
            request: request,
            sendToCurrentlyRoutedEndpoint: (req, ct) => this.storeModel.ProcessMessageAsync(req, ct),
            hedgeContext: hedgeContext,
            trace: childTrace,
            cancellationToken: cancellationToken);   // <- propagate caller token; CancellationToken.None
                                                     //    here would defeat caller-driven cancellation
                                                     //    of the entire BackoffRetryUtility loop.

        return result.Response;
    }
}
```

The `cancellationToken` is the customer-supplied token threaded through `GetRoutingMapForCollectionAsync` -> `AsyncCacheNonBlocking.GetAsync(..., ct)` -> the BackoffRetryUtility lambda -> `ExecutePartitionKeyRangeReadChangeFeedAsync(..., ct)` -> `MetadataHedgingStrategy.ExecuteAsync(..., ct)`. Inside `ExecuteAsync`, this token is the parent of all three per-branch CTSs (`primaryCts`/`hedgeCts`/`timerCts` via `CreateLinkedTokenSource`), so customer-cancellation cancels **all** in-flight branches as well as the threshold timer. The previous `CancellationToken.None` value would have made customer cancellation a no-op for the duration of any in-flight metadata read — a correctness regression vs. today's behavior.

### 5.6 Cold-start signal — concrete propagation

The "previousValue == null" signal lives in different places for the two caches.

**`ClientCollectionCache` — `isColdStart = !forceRefresh`.**

`CollectionCache.ResolveByNameAsync(forceRefresh, ...)` is the only entry point into the AsyncCache that exposes the customer-visible "this is a refresh" intent. Internally it calls `AsyncCache.GetAsync(key, obsoleteValue: null, factory, ct, forceRefresh)`. The three reachable cases are:

| Caller intent | `forceRefresh` | Cache state | Factory invoked? | `isColdStart` value | Hedge eligible? |
|---|---|---|---|---|---|
| First-time read | `false` | miss | yes | `true` | yes (cold start) |
| Steady-state read | `false` | hit | **no** | (moot — factory not invoked) | n/a |
| Refresh after `OperationCanceledException` / 410 | `true` | hit-but-stale | yes | `false` | no (force-refresh must not hedge) |

The recipe for the caller is simply `isColdStart = !forceRefresh`. The mid-row "moot" case is correct because `AsyncCache.GetAsync` short-circuits and never invokes the factory on a cache hit; the parameter is computed but never read.

Concretely in `ClientCollectionCache.ResolveByNameAsync` / `ResolveByRidAsync`, the call becomes:

```csharp
return await this.collectionInfoByNameCache.GetAsync(
    key: resourceFullName,
    obsoleteValue: null,
    singleValueInitFunc: () => this.GetByNameAsync(
        apiVersion, resourceFullName, csrs, trace,
        clientSideRequestStatistics,
        isColdStart: !forceRefresh,             // <- the only new bit at the call site
        ct),
    cancellationToken: ct,
    forceRefresh: forceRefresh);
```

Note that "cache evicted then re-populated by a non-force-refresh caller" is also `isColdStart = true` — this is the container-recreate-with-same-name false positive flagged in §5.11; the false positive is acceptable because that incarnation of the container IS a genuine first-population.

**`PartitionKeyRangeCache` — `isColdStart = previousRoutingMap == null`.**

`AsyncCacheNonBlocking.GetAsync` takes `Func<TValue, Task<TValue>>` — the factory **does** receive the previous value. `GetRoutingMapForCollectionAsync` already receives `previousRoutingMap` as a parameter, so:

```csharp
bool isColdStart = previousRoutingMap == null;
```

Subsequent calls with a non-null `previousRoutingMap`, including `ShouldForceRefresh`-driven refreshes, do not hedge.

### 5.7 Coordination with `MetadataRequestThrottleRetryPolicy`

This spec must compose with the in-flight changes proposed by **PR [#5780](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5780)** ("Metadata Retry: Adds Cross-Region Operation-level Retry for Metadata Request Failures"), which rewires the same `MetadataRequestThrottleRetryPolicy` surface this design extends. Post-#5780, the retry policy interprets a broader set of failure signals (`503/500/Gone+LeaseNotFound/Forbidden+DatabaseAccountNotFound`, `HttpRequestException`, and **non-user `OperationCanceledException`**) as "regional failure" and calls `LocationCache.MarkEndpointUnavailableForRead()` on the failing endpoint, deprioritizing it for **all** subsequent reads — point reads, queries, change-feed, etc. — for the 5-minute `LocationCache` TTL.

#### 5.7.1 Critical invariant: hedge-loser cancellation must never reach the retry policy

If the losing hedge branch's `OperationCanceledException` were observed by `MetadataRequestThrottleRetryPolicy`, every successful hedge would call `MarkEndpointUnavailableForRead()` on a perfectly healthy secondary region and poison it for the entire client for 5 minutes. This is enforced structurally in `MetadataHedgingStrategy.ExecuteAsync` (§5.3):

1. **Per-branch CancellationTokenSources.** Each branch has its own CTS; there is no shared `linkedCts` whose `Cancel()` propagates into both branches.
2. **`BackgroundCleanupAsync` is the exclusive observer of the loser.** The loser task is awaited only inside `BackgroundCleanupAsync`, which catches `OperationCanceledException` and every other exception type, records the outcome in `diag.LoserOutcome`, and **never rethrows**. The retry policy is never handed the loser task.
3. **Only the winner's outcome reaches the retry policy.** `ObserveWinningTaskAsync` re-raises the winner's exception (if any) via `ExceptionDispatchInfo`. The retry policy classifies the winner's exception normally — which is correct, because the winner was *not* cancelled.

This is the structural guarantee that makes "hedge a healthy secondary" not poison the secondary for follow-on reads.

##### 5.7.1.1 Acknowledged tradeoff: lost primary regional-failure signal when hedge wins

There is a deliberate, complementary asymmetry to invariant 5.7.1: when the hedge wins on a primary 500/503/`HttpRequestException`/non-user-cancellation, the primary's regional-failure signal is observed only by `BackgroundCleanupAsync` and is intentionally **not** forwarded to `MetadataRequestThrottleRetryPolicy`. This means `LocationCache.MarkEndpointUnavailableForRead(primaryEndpoint)` is **not** called for that operation, even though the primary genuinely failed.

This tradeoff is acceptable because:

- The next metadata or data-plane request issued through `MetadataRequestThrottleRetryPolicy` or `ClientRetryPolicy` will independently observe the primary's degraded state and call `MarkEndpointUnavailableForRead` on the first failure that surfaces to the caller. The 5-minute TTL on the `LocationCache` deprioritization is reached one extra request later — not a correctness problem.
- The alternative — having `BackgroundCleanupAsync` reach into `LocationCache` directly when the loser was the primary — would re-introduce a cross-component coupling that this design has worked hard to avoid (the hedge helper would gain authority over endpoint state, which today is owned exclusively by `ClientRetryPolicy` / `MetadataRequestThrottleRetryPolicy`).
- Telemetry exposes the gap: `azure.cosmosdb.client.metadata_hedging.hedge_wins` growing while `MarkEndpointUnavailableForRead` calls stay flat is the signal that primary-failure-but-hedge-won is happening. Operators see it; the next failing request closes the gap.

##### 5.7.1.2 Race window: hedge dispatch vs. retry-policy regional fallback

When the primary fails *before* the hedge timer fires, `MetadataRequestThrottleRetryPolicy.ShouldRetryAsync` may classify the failure as regional and schedule a same-loop sequential fallback to region B. If the threshold fires within the same scheduling tick, the hedge helper may **also** dispatch to region B. The two paths are not strictly mutually exclusive across the t=threshold ± `Task.Delay` precision window.

Worked example (threshold = 1.5 s):

- t=0 — primary dispatched to region A. Threshold timer armed for t=1.5 s.
- t=1.4 s — primary returns 503. `step 5a`'s `IsAcceptableWinner(primary)` returns `false`; control falls through.
- t=1.4 s+ε — hedge helper observes primary completion ≥ threshold-elapsed semantic; before the timer callback runs, retry-policy's regional fallback (running synchronously inside the same `BackoffRetryUtility` iteration) may have already scheduled the next attempt to region B.
- t=1.5 s — timer fires. `MetadataHedgingContext.HasHedgedThisOperation` is checked via `Interlocked.CompareExchange`; if the next-attempt retry-policy path has already set it, hedge is skipped (`SkipReason=AlreadyHedgedThisOperation`). If not, hedge dispatches to region B and immediately sets the flag, so the *next* retry-policy iteration will observe `HasHedgedThisOperation=true` and skip the second-dispatch path it would otherwise take.

The `MetadataHedgingContext.HasHedgedThisOperation` `Interlocked.CompareExchange` (rule 9 in §6) is the serialization point that closes the race — at most one of {hedge-helper-dispatch, retry-policy-fallback} actually issues a duplicate request to region B per operation. The other observes the flag and short-circuits.

The total wall-clock attempt cap (`MetadataRequestThrottleRetryPolicy`'s existing retry budget) remains the upper bound regardless of which path won the race.

#### 5.7.2 Shared regional-failure classifier

Both this design's `IsAcceptableWinner` predicate (§5.3 / §5.13) and `MetadataRequestThrottleRetryPolicy` need the same status-code set for the *regional-failure* classification. PR #5780's reviewer (xinlian12) asked that the duplicated definitions be consolidated. Add a small static helper:

```csharp
internal static class RetryUtility
{
    /// <summary>
    /// Returns true iff the response/exception represents a regional failure
    /// that should advance the retry policy to a new preferred location.
    /// Used by:
    ///   - MetadataRequestThrottleRetryPolicy.ShouldRetryAsync (post-#5780)
    ///   - MetadataHedgingStrategy.IsAcceptableWinner (this spec, negated for
    ///     the primary branch; composed with the per-branch 401/403 overlay
    ///     for the hedge branch — see §5.13)
    /// </summary>
    internal static bool IsRegionalFailure(
        DocumentServiceResponse responseOrNull,
        Exception exceptionOrNull,
        CancellationToken callerToken)
    {
        if (exceptionOrNull is HttpRequestException) return true;
        if (exceptionOrNull is OperationCanceledException oce
            && !callerToken.IsCancellationRequested) return true;  // non-user cancellation
        if (responseOrNull == null) return false;
        switch (responseOrNull.StatusCode)
        {
            case HttpStatusCode.ServiceUnavailable:                 // 503
            case HttpStatusCode.InternalServerError:                // 500
                return true;
            case HttpStatusCode.Gone:                               // 410
                return responseOrNull.SubStatusCode == SubStatusCodes.LeaseNotFound;
            case HttpStatusCode.Forbidden:                          // 403
                return responseOrNull.SubStatusCode == SubStatusCodes.DatabaseAccountNotFound;
        }
        return false;
    }
}
```

`MetadataRequestThrottleRetryPolicy` consumes `IsRegionalFailure` directly. `MetadataHedgingStrategy.IsAcceptableWinner(response, branch)` (§5.13) composes this helper with a per-branch overlay that rejects 401/plain-403 from the **hedge** branch only. This eliminates the 3-to-4 copies of the failure-class list that exist or are being added today and keeps the per-branch policy localized.

#### 5.7.3 `MetadataRequestThrottleRetryPolicy` extensions

The retry policy gets two changes:

```csharp
internal sealed class MetadataRequestThrottleRetryPolicy : IDocumentClientRetryPolicy
{
    private MetadataHedgingContext hedgeContext;   // optional; null = no hedge in use

    internal void AttachHedgeContext(MetadataHedgingContext context)
    {
        this.hedgeContext = context;
    }

    // (A) Replaces the inline status-code switch with the shared helper above.
    public Task<ShouldRetryResult> ShouldRetryAsync(...)
    {
        bool isRegional = RetryUtility.IsRegionalFailure(response, exception, callerToken);
        // ... existing branch logic, but using `isRegional` instead of inline code list ...
    }

    // (B) Existing IncrementRetryIndexOnUnavailableEndpointForMetadataRead() is
    //     extended to skip indices whose resolved endpoint is already in
    //     hedgeContext.AttemptedEndpoints. See §5.7.4 for the resolve loop.
}
```

#### 5.7.4 `IncrementRetryIndexOnUnavailableEndpointForMetadataRead` — skip-attempted-endpoints loop

Today (`MetadataRequestThrottleRetryPolicy.cs:237-253`) the method is a one-line monotonic counter:

```csharp
private bool IncrementRetryIndexOnUnavailableEndpointForMetadataRead()
{
    this.unavailableEndpointRetryCount++;
    return this.unavailableEndpointRetryCount <= MaxRetryCountForUnavailableEndpoint;
}
```

It never resolves an endpoint. Endpoint resolution happens separately in `OnBeforeSendRequest` via `globalEndpointManager.ResolveServiceEndpoint(request)`. To "skip an attempted endpoint," the method needs a bounded probe loop that *does* resolve, but does **not** mutate `LocationCache` state per probe. The new shape uses `this.request` (the inflight DSR already held by the policy via `OnBeforeSendRequest`) — passing the request as a parameter would invert the existing `IDocumentClientRetryPolicy` surface for no benefit:

```csharp
private bool IncrementRetryIndexOnUnavailableEndpointForMetadataRead()
{
    int maxIndices = this.globalEndpointManager.ReadEndpoints.Count;
    for (int probe = 0; probe < maxIndices; probe++)
    {
        this.unavailableEndpointRetryCount++;
        if (this.unavailableEndpointRetryCount > MaxRetryCountForUnavailableEndpoint) return false;

        // Side-effect: the existing path writes this.retryContext.RetryLocationIndex
        // = this.unavailableEndpointRetryCount before returning true (see existing
        // MetadataRequestThrottleRetryPolicy implementation). Preserve that side-effect
        // exactly so OnBeforeSendRequest on the NEXT BackoffRetryUtility iteration
        // re-routes to the new index.
        this.retryContext.RetryLocationIndex = this.unavailableEndpointRetryCount;

        // Tentatively resolve the would-be endpoint for the new RetryLocationIndex.
        // ResolveServiceEndpoint is read-only against LocationCache (no mutation per probe);
        // this is documented as a hard invariant for any future LocationCache refactor.
        this.request.RequestContext.RouteToLocation(this.unavailableEndpointRetryCount, /*usePreferredLocations*/ true);
        Uri probed = this.globalEndpointManager.ResolveServiceEndpoint(this.request);

        if (this.hedgeContext == null || !this.hedgeContext.AttemptedEndpoints.ContainsKey(probed.AbsoluteUri))
        {
            return true;     // landed on an un-attempted endpoint
        }
        // else: hedge already tried this region; advance.
    }
    return false;            // all preferred regions exhausted
}
```

**Side-effect invariant:** every `return true` path must leave `this.retryContext.RetryLocationIndex == this.unavailableEndpointRetryCount`. The next `BackoffRetryUtility` iteration's `OnBeforeSendRequest` reads this back when re-routing the request, so the probe loop's index advancement must be persisted to the policy's retry-context before returning. `return false` paths leave the index past-the-end, signaling exhaustion to the caller.

If `hedgeContext` is null (no hedge in use, or attached to a wrapped/test-double policy) the loop collapses to the original counter behavior. If all preferred regions are exhausted by hedge attempts, the policy terminates the retry instead of looping on a region the hedge already tried — capping total attempts at `preferred-region-count`.

### 5.8 Hedge execution model — summary

1. Evaluate eligibility ([§6](#6-eligibility-and-precedence-rules)). If ineligible, send to primary only.
2. Acquire one slot from the per-client semaphore using `Wait(TimeSpan.Zero)` (true synchronous non-blocking — no Task allocation). If unavailable → skip hedge with `SkipReason=BudgetExhausted`.
3. Resolve primary endpoint (`request.RequestContext.LocationEndpointToRoute` already set by `MetadataRequestThrottleRetryPolicy.OnBeforeSendRequest`). Pick the next preferred secondary as the hedge target (next in `PreferredLocations` order — not a proximity measurement). Respect `ExcludeRegions` (§6 rule 8).
4. Allocate **per-branch CancellationTokenSources** (`primaryCts`, `hedgeCts`, `timerCts`) **inside the outer `try`**, each linked to the caller token. All three are disposed in the outer `finally` (with a null-guard) — the loser's CTS ownership transfers to `BackgroundCleanupAsync` via local-ref null-out so it is not double-disposed. Send the primary via `SendOneAsync(primaryCts.Token)`; start `Task.Delay(Threshold, timerCts.Token)`.
5. If the primary `RanToCompletion` *and* the response is an acceptable winner for the primary branch (`IsAcceptableWinner(resp, Primary)` == `!IsRegionalFailure(resp)`) *and* the timer has not elapsed → cancel `timerCts` only; return primary as winner. If the primary **faults** before the timer (HttpRequestException, socket reset, fast `TaskCanceledException` from `HttpTimeoutPolicy`), fall through to hedge dispatch — fast-fail on a degraded primary must trigger the hedge, not bypass it.
6. If the timer elapses (or the primary settled transient/faulted) → re-check the Gateway kill-switch; if flipped to true → suppress hedge, await primary via `ObserveWinningTaskAsync`. Otherwise mark `hedgeContext.TryMarkHedgedThisOperation()` and dispatch the hedge via `SendOneAsync(hedgeCts.Token)`.
7. **Wait-for-acceptable-winner loop**: `Task.WhenAny` returns; if the completed task is `RanToCompletion` AND `IsAcceptableWinner(resp, branch)` is `true` for that branch → winner. The per-branch overlay rejects 401/plain-403 from the **hedge** branch (§5.13) so a hedge-401 cannot beat a slow primary-200. If transient/faulted/per-branch-rejected → continue waiting on the other branch. If both branches settle without an acceptable winner → prefer the primary's outcome (§10).
8. Cancel **only the loser's own CTS** (never a shared linked CTS), null out the local ref to that CTS (ownership transfer), and hand the loser to `BackgroundCleanupAsync`, which awaits it, disposes its `DocumentServiceResponse` (handle-leak fix), disposes the loser CTS, and records `LoserOutcome` — and **never rethrows**. This is the structural guarantee that the loser's `OperationCanceledException` cannot reach `MetadataRequestThrottleRetryPolicy` and trigger a spurious `MarkEndpointUnavailableForRead` against the healthy loser region (§5.7.1).
9. Re-raise the winner's exception (if any) via `ObserveWinningTaskAsync` (uses `ExceptionDispatchInfo.Capture(...).Throw()` to preserve the throwing-frame stack across the await boundary — see §5.12 net472 discipline).
10. Always dispose the locally-owned CTSs (with `?.Dispose()` null-guards) and release the semaphore in the outer `finally`.

### 5.9 Threshold derivation

The default hedge threshold is set to `firstControlPlaneRetriableTimeout + 500 ms`. Reading the first timeout from `HttpTimeoutPolicyControlPlaneRetriableHotPath` requires a new accessor — `TimeoutsAndDelays` is `private readonly` today (`HttpTimeoutPolicyControlPlaneRetriableHotPath.cs:28`) and the only public surface is `GetTimeoutEnumerator()` (forward-only) and `TotalRetryCount`. We therefore add:

```csharp
internal abstract class HttpTimeoutPolicy
{
    /// <summary>The request-timeout of the first local attempt.
    /// Used by MetadataHedgingStrategy to derive its default threshold (§5.9)
    /// and by unit tests to assert the §8 invariant
    /// (MetadataHedgeThreshold &gt; firstAttemptTimeout).</summary>
    internal abstract TimeSpan FirstAttemptTimeout { get; }
}

internal sealed class HttpTimeoutPolicyControlPlaneRetriableHotPath : HttpTimeoutPolicy
{
    internal override TimeSpan FirstAttemptTimeout => this.TimeoutsAndDelays[0].requestTimeout;
}
```

`MetadataHedgingStrategy` reads `FirstAttemptTimeout` exactly once at construction. Today that resolves to **1 s + 500 ms = 1.5 s**. The derivation (rather than a hard-coded constant) ensures the invariant *"hedge threshold > first local HTTP retry timeout"* is preserved automatically if the underlying HTTP policy ever changes again (the 500 ms → 1 s bump in issue #5642 is precedent that this can move). The `ThresholdStep` is 500 ms, mirroring the data-plane default.

### 5.10 Cold-start signal (rationale — now diagnostic only)

> **Updated by PR [#5923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5923).**
> The cold-start signal is **no longer an eligibility gate** — both first-time
> populations and refresh / force-refresh reads are eligible (§6 rule 5). The
> signal (`MetadataHedgingContext.IsColdStart`) is still threaded in from the
> caller and recorded in diagnostics/telemetry so support can distinguish cold
> from warm hedges, but it never causes a skip. The discussion below is retained
> for historical context and for how the cold/warm signal is *derived*; the
> conclusions that a force-refresh "would incorrectly hedge" / "do not hedge" no
> longer apply — those reads now hedge by design, bounded by the same threshold,
> budget, one-hedge-per-operation latch, and first-page-only PK-range gate.

Cold start is defined **per cache key**, not process-wide. The two caches have different mechanics and must be handled differently — the *signal* is the same ("first time this key is populated") but the *plumbing* is not:

- **`ClientCollectionCache`** uses `AsyncCache<string, ContainerProperties>` (`CollectionCache.cs:34`). `AsyncCache.GetAsync` takes `Func<Task<TValue>>` — the factory does **not** receive the previous value. The cold-start signal is **threaded in from the caller** as the `isFirstPopulation` flag (`ResolveByNameAsync` passes `false` on the force-refresh path and `true` on first population). This flag is now used purely to set `MetadataHedgingContext.IsColdStart` for diagnostics; eligibility no longer depends on it, so a force-refresh read is hedged on the same terms as a cold-start read.
- **`PartitionKeyRangeCache`** uses `AsyncCacheNonBlocking<…, …>` (`PartitionKeyRangeCache.cs:28`). `AsyncCacheNonBlocking.GetAsync` takes `Func<TValue, Task<TValue>>` — the factory **does** receive the previous value, so `IsColdStart = previousValue == null` is derived inside the factory for diagnostics. Refresh reads (non-null `previousRoutingMap`) are hedged on the first ReadFeed page; subsequent pages stay pinned to the page-1 winner (§5.5).

### 5.11 Concurrency budget (rationale)

At app startup it is common for a single `CosmosClient` to initialize many containers in parallel. Without a limiter, every cold-start collection / PK-range load would emit a hedged request to the secondary region simultaneously, doubling startup pressure on the secondary Gateway. A **per-client `SemaphoreSlim` with a small fixed capacity (default 8 in-flight metadata hedges per client)** bounds this. If the budget is exhausted, the hedge is **skipped** (best-effort) and a skip-reason is recorded in diagnostics; the primary request still runs unmodified. The default budget is exposed on `MetadataHedgingOptions.PerClientConcurrencyBudget` (public — §5.1) so customers initializing many containers at startup can raise it without disabling the feature.

### 5.12 net472 stack-unwind discipline

PR [#5870](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5870) ("Fixes StackOverflow on .NET Framework 4.7.2") established that the data-plane `CrossRegionHedgingAvailabilityStrategy` suffered a real `StackOverflowException` on net472 because synchronous exception unwind through async-await frames consumed ~10 KB per frame and ran out of the 1 MB stack. The trigger was exactly the pattern this design uses: a hedge loser throws `OperationCanceledException` (or any other exception) deep in the HTTP pipeline → unwind traverses every awaiting hedge frame. The fix was surgical: a `try { … } catch { await Task.Yield(); throw; }` on a middle-layer frame, plus `ExceptionDispatchInfo.Capture(...).Throw()` on the re-raise path.

This design adopts the same discipline:

1. **`SendOneAsync` is a dedicated middle-layer seam** (§5.3) that wraps every send-delegate invocation with `try { ... } catch { await Task.Yield(); throw; }`. The `Task.Yield()` posts the continuation to the scheduler and breaks the synchronous unwind into two smaller stack walks.
2. **All re-raises use `ExceptionDispatchInfo.Capture(...).Throw()`** (`ObserveWinningTaskAsync`, §5.3) — never bare `throw exception;`, which loses the original stack.
3. **`BackgroundCleanupAsync`** catches and discards every loser-side exception, so no loser-thrown exception ever traverses an `ExecuteAsync` await frame.
4. **CI regression test:** an emulator-level test on net472 that drives ~50 concurrent cold-start hedges with a cancelling primary, asserting no `StackOverflowException`.

### 5.13 Authentication considerations

`CloneForHedge` re-targets the request to a secondary endpoint. The SDK supports four authentication modes; each interacts with cross-region re-targeting differently:

| Auth mode | Hedge-safe? | What `CloneForHedge` does |
|---|---|---|
| **Master key** (HMAC over `verb + resourceType + resourceId + date`) | Yes | **Reuse the original `Authorization` + `x-ms-date` pair verbatim.** Auth is signed *upstream* of `MetadataHedgingStrategy.ExecuteAsync` (in `ClientCollectionCache.cs:219-227` and `PartitionKeyRangeCache.cs:284-294`, exactly once per request) — `storeModel.ProcessMessageAsync` does **not** re-invoke `GetUserAuthorizationTokenAsync`. The hedge is dispatched at most `Threshold` ≈ 1.5 s after the primary was signed; the master-key HMAC clock-skew tolerance is ~15 minutes, so the original signature is still valid. Refreshing `x-ms-date` without re-running the auth provider would produce an `xdate=T2` + `HMAC(...T1)` mismatch → guaranteed 401 on every master-key hedge. |
| **Resource token** (permission-scoped) | Yes if the token is portable to the readable secondary for the matching resource (the common case). The token is reused verbatim; no re-signing. |
| **AAD `TokenCredential`** (bearer) | Yes for non-sovereign clouds (single audience `https://<account>.documents.azure.com`). For sovereign clouds with per-region audiences, the token must be re-acquired for the secondary's audience; if cached, the cache key is the audience+scope tuple. |
| **RBAC data-plane token** (AAD principal + role assignment) | **Conditional.** If the role assignment exists in both regions, hedge is safe. **If the role assignment is missing in the secondary** (a common misconfiguration), the hedge will always 401. |

**Per-mode handling in `CloneForHedge`:**

```csharp
private DocumentServiceRequest CloneForHedge(DocumentServiceRequest src, Uri hedgeEndpoint)
{
    DocumentServiceRequest clone = src.Clone();    // shallow; deep-clones RequestContext and Headers
    clone.RequestContext.RouteToLocation(hedgeEndpoint);
    clone.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();

    // Headers (notably Authorization and x-ms-date) are deep-cloned by src.Clone() and
    // are reused verbatim. We do NOT touch x-ms-date here: the master-key HMAC was
    // computed over (verb + resourceType + resourceId + xdate) by the upstream auth
    // provider (ClientCollectionCache.cs:219-227 / PartitionKeyRangeCache.cs:284-294)
    // BEFORE the request entered ExecuteAsync. Rewriting x-ms-date now would break that
    // HMAC because storeModel.ProcessMessageAsync does not re-invoke the auth provider.
    // The 1.5 s hedge dispatch is well inside the 15-minute master-key skew tolerance.
    //
    // For AAD/RBAC: the bearer token is already in the Authorization header and is
    // audience-bound to the account (not the region) for non-sovereign clouds, so it
    // is also reused verbatim. Sovereign-cloud per-region audiences are addressed by
    // the upstream token provider's cache key (audience + scope tuple); if the hedge
    // endpoint requires a different audience, the upstream provider re-acquires before
    // re-signing — and again, by that point ExecuteAsync is already running, so the
    // first hedge against a sovereign cloud's per-region audience may 401. The
    // hedge-401/403 guard below catches that case.
    return clone;
}
```

**Hedge-401/403 guard.** Because of the RBAC-role-assignment-missing-in-secondary case (and the sovereign-cloud-audience case noted above), the strategy treats `401 Unauthorized` and plain `403 Forbidden` (sub-status ≠ `DatabaseAccountNotFound`) from the *hedge* branch as a hard skip, not a candidate winner. This is **NOT** enforceable via `IsRegionalFailure` alone (that helper is shared with `MetadataRequestThrottleRetryPolicy`, which must classify a 401 from a *primary* as a non-regional failure so the customer's auth error surfaces normally). The strategy therefore composes `IsRegionalFailure` with a per-branch overlay:

```csharp
private static bool IsAcceptableWinner(DocumentServiceResponse resp, HedgeBranch branch)
{
    // Regional failures are never acceptable winners (both branches).
    if (RetryUtility.IsRegionalFailure(resp, exceptionOrNull: null, callerToken: default)) return false;

    if (branch == HedgeBranch.Hedge)
    {
        // 401 from the hedge branch is almost always a misconfiguration in the
        // secondary (RBAC role assignment missing, sovereign-cloud audience drift).
        // Never let it surface as the operation result while the primary may still
        // return 200. The wait-for-winner loop (§5.3 step 6) continues on the primary.
        if (resp.StatusCode == HttpStatusCode.Unauthorized) return false;

        // Plain 403 from the hedge branch (sub-status ≠ DatabaseAccountNotFound) is
        // also treated as non-winning for the same reason. Forbidden + DatabaseAccountNotFound
        // is already filtered by IsRegionalFailure above (it indicates the hedge endpoint
        // has been failed over and our LocationCache is stale).
        if (resp.StatusCode == HttpStatusCode.Forbidden) return false;
    }

    return true;
}

internal enum HedgeBranch { Primary, Hedge }
```

`§5.3` uses `IsAcceptableWinner(finished.Result, branch)` in both step 5a (primary, branch = Primary) and step 6 (wait-for-winner loop, branch derived from which task `finished` refers to). For the primary branch, `IsAcceptableWinner` is identical to `!IsRegionalFailure(resp, null, ct)` — i.e., a 401 from the primary is still classified as non-regional and surfaces to the caller normally. Diagnostics gain a new field `HedgeOutcome` set to `"Auth401"` / `"Auth403"` whenever the guard fires (separate from `SkipReason`, which is evaluated *pre*-dispatch); `azure.cosmosdb.client.metadata_hedging.fires` growing while `azure.cosmosdb.client.metadata_hedging.hedge_wins` stays at 0 with a non-zero `azure.cosmosdb.client.metadata_hedging.hedge_auth_reject` (§9.1) is the operational signal that role assignments or audience configuration in the secondary need attention.

**Eligibility check.** `EvaluateEligibility` also performs a fast-path skip when `request.AuthorizationTokenType` is a mode known not to be cross-region-portable (none today, but the enum is open for future modes).

---

## 6. Eligibility and Precedence Rules

A metadata request is eligible for hedging only when **ALL** of the following are true:

1. The Gateway flag `disableCrossRegionalHedging` is **NOT** `true`. This flag (introduced in PR [#5829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5829)) takes precedence; when set, both data-plane **AND** metadata hedging are suppressed. Read directly per-request from `DocumentClient.IsHedgingDisabledByGateway` — do **not** introduce a third cached copy of the flag (per PR #5829 review).
2. `ConnectionPolicy.EnablePartitionLevelFailover` is `true` (alignment with PPAF scope — metadata hedging is offered as a PPAF cold-start tail-latency mitigation).
3. `GlobalEndpointManager.ReadEndpoints.Count > 1` — there is a secondary region to hedge to.
4. The request is one of: `ResourceType.Collection` Read, or `ResourceType.PartitionKeyRange` ReadFeed **first page**.
5. *(Updated by PR [#5923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5923) — no longer an eligibility condition.)* The read is **not** required to be a first-time population. Both cold-start (first-population) and steady-state refresh / force-refresh reads are eligible. The `IsColdStart` signal (§5.10) is still threaded in and recorded for diagnostics, but `EvaluateEligibility` no longer skips on it and `MetadataHedgeSkipReason.NotColdStart` is never produced.
6. The Gateway store model path is in use (no Direct address resolution metadata path is in scope).
7. The per-client concurrency budget for metadata hedges has capacity. (`EvaluateEligibility` itself does NOT touch the semaphore — capacity is checked atomically in `ExecuteAsync` step 2 via `Wait(TimeSpan.Zero)`. `EvaluateEligibility` may return `IsEligible=true` and `ExecuteAsync` still skip with `SkipReason=BudgetExhausted` — that is by design.)
8. **`ExcludeRegions` does not leave the secondary set empty.** If `request.RequestContext.ExcludeRegions` filters out every candidate hedge target (including transitively, via PK-range page-2..N pinning to a winning region that is later excluded), `SkipReason=ExcludedRegionLeavesNoTarget` and the primary runs alone. This is a **hard** eligibility rule, not optional — PK-range pages 2..N are pinned to the page-1 winning region (§5.5), so picking an excluded region as the hedge target binds the cached PKR to a region the customer asked the SDK not to use.
9. **`MetadataHedgingContext.HasHedgedThisOperation` is `false`.** A logical operation hedges **at most once** across the entire `BackoffRetryUtility.ExecuteAsync` loop. The first hedge dispatch sets the flag via `Interlocked.Exchange`; subsequent retries observe it and skip with `SkipReason=AlreadyHedgedThisOperation`. See §6.1 for why this is necessary.
10. **Single-master account guard (informational).** For a single-master account, metadata **reads** are still valid against the secondary read regions, so hedging remains correct. The spec explicitly does *not* attempt to hedge any metadata **write** (account-properties write, container CRUD); these are out of scope per [§4 Non-Goals]. Documented here so reviewers from #5780 (which added a similar guard) can confirm symmetry.

If any of (1)–(10) fails, the request is sent to the primary region only and the skip reason is recorded.

### 6.1 Interaction with `MetadataRequestThrottleRetryPolicy`

`MetadataRequestThrottleRetryPolicy` already performs sequential cross-region fallback on `503 / 500 / Gone+LeaseNotFound / Forbidden+DatabaseAccountNotFound` (and, post-PR #5780, on `HttpRequestException` and non-user `OperationCanceledException`). To prevent attempt amplification, the hedge helper and the retry policy share `MetadataHedgingContext`:

- **The "at most once per operation" guarantee is structural.** `MetadataHedgingContext.HasHedgedThisOperation` (§5.2) is set via `Interlocked.Exchange` the moment the first hedge is dispatched. The flag is owned by the *context*, which lives across the entire `BackoffRetryUtility.ExecuteAsync` loop — *not* by the strategy, which is stateless per call. On every subsequent retry attempt, `EvaluateEligibility` checks `HasHedgedThisOperation` (eligibility rule 9) and short-circuits. This is necessary because **`previousValue` is still null on retry** (the cache is only populated after the loop *exits* successfully — until then, every retry inside the same operation sees a null previous value and would otherwise re-hedge). An earlier draft of this spec incorrectly claimed retries re-evaluate to `false` because "the cache key now has a previousValue"; this is wrong — the cache key has no previousValue until the loop exits, hence the explicit guard.
- **On retry, `MetadataRequestThrottleRetryPolicy` advances past attempted endpoints.** `IncrementRetryIndexOnUnavailableEndpointForMetadataRead` (§5.7.4) skips any `RetryLocationIndex` that resolves to an endpoint already in `hedgeContext.AttemptedEndpoints`, preventing the next retry from targeting a region the hedge just used.
- **Total cap:** at most `preferred-region-count` attempts across primary + hedge + retries for a single metadata cache population. With 3 preferred regions: primary + 1 hedge = 2 attempts on the first try; retry advances to region 3 = 3 attempts total; loop terminates.

---

## 7. SDK Responsibilities

- Evaluate eligibility ([§6](#6-eligibility-and-precedence-rules)) **before** creating any hedge timer or task, so ineligible requests pay zero hedge cost and produce a clean skip-reason diagnostic.
- On winning, cancel the **loser's own** `CancellationTokenSource` (never a shared linked CTS). Observe the loser entirely inside `BackgroundCleanupAsync`; never let the loser's exception escape `ExecuteAsync` (§5.7.1).
- Dispose the loser's `DocumentServiceResponse` body in `BackgroundCleanupAsync` to prevent stream-handle leaks (one leak per hedged operation × per-client budget × every cold start adds up quickly).
- On PK-range read feed, return the winning region from the first page to the caller and pin all subsequent pages to that same region by setting `request.RequestContext.RouteToLocation(winningEndpoint)`.
- Coordinate with `MetadataRequestThrottleRetryPolicy` through `MetadataHedgingContext` so attempted regions are deduped across hedge and retry, and so that re-hedge is prevented across retry attempts (§6.1).
- Honor `request.RequestContext.ExcludeRegions` as a hard filter on hedge candidates (§6 rule 8).
- Acquire/release the per-client concurrency budget around the hedge launch (always release on completion, including via `try/finally` on the helper).
- Suppress hedging immediately on transition of `disableCrossRegionalHedging → true`, including timers already scheduled but not yet fired.

### 7.1 Wiring `isHedgingDisabledByGateway` into the cache constructors

`ClientCollectionCache` and `PartitionKeyRangeCache` hold an `IStoreModel` today but not a reference to `DocumentClient`. The `disableCrossRegionalHedging` Gateway flag lives on `DocumentClient` (per PR [#5829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5829)). Wire it as a `Func<bool>` constructor argument on `MetadataHedgingStrategy`, captured from the `DocumentClient` instance at `DocumentClient.Initialize`:

```csharp
// In DocumentClient.Initialize:
Func<bool> isHedgingDisabledByGateway = () => this.IsHedgingDisabledByGateway;
this.metadataHedgingStrategy = new MetadataHedgingStrategy(
    globalEndpointManager: this.globalEndpointManager,
    gatewayStoreModel: this.gatewayStoreModel,
    isHedgingDisabledByGateway: isHedgingDisabledByGateway,
    isPpafEnabled: () => this.ConnectionPolicy.EnablePartitionLevelFailover,
    options: this.ConnectionPolicy.MetadataHedgingOptions ?? new MetadataHedgingOptions(),
    isOptInEnabled: this.ConnectionPolicy.EnableMetadataHedgingForColdStart ?? phaseDefault);

// Pass the same strategy instance to ClientCollectionCache and PartitionKeyRangeCache:
this.collectionCache = new ClientCollectionCache(..., metadataHedgingStrategy: this.metadataHedgingStrategy);
this.partitionKeyRangeCache = new PartitionKeyRangeCache(..., metadataHedgingStrategy: this.metadataHedgingStrategy);
```

The `Func<bool>` indirection is intentional: it reads the live value from `DocumentClient` on each invocation, so a mid-flight flip of the gateway flag is observed immediately (no cached copy on the strategy or the caches).

---

## 8. Threshold and Timeout Considerations

The `HttpTimeoutPolicyControlPlaneRetriableHotPath` today defines three local attempts with timeouts `(1 s, 5 s, 65 s)` and a 1 s gap before the second attempt (per `Microsoft.Azure.Cosmos/src/HttpClient/HttpTimeoutPolicyControlPlaneRetriableHotPath.cs` lines 28–33; the first-attempt value was raised from 500 ms → 1 s by issue #5642). The hedge threshold must remain **greater than** the first local timeout, so that the cheap local retry has a chance to complete before the SDK incurs cross-region network and gateway cost.

**Default values:**

| Knob | Value | Notes |
|---|---|---|
| `Threshold` | `firstControlPlaneRetriableTimeout + 500 ms` | Today: **1.5 s**. Computed at startup, not hard-coded. |
| `ThresholdStep` | `500 ms` | Matches data-plane `CrossRegionHedgingAvailabilityStrategy` default semantics. |
| Max hedge branches per attempt | `1` | One secondary region in the cold-start window; staircase fan-out is intentionally **not** used for metadata. |

An assertion in unit tests will fail-fast if the relationship `MetadataHedgeThreshold > HttpTimeoutPolicy.FirstAttemptTimeout` ever regresses. The assertion uses the new `HttpTimeoutPolicy.FirstAttemptTimeout` accessor introduced in §5.9 — without it the invariant cannot be checked because `TimeoutsAndDelays` is private.

---

## 9. Diagnostics and Operational Usage

Every metadata hedge attempt emits a **`Metadata Hedge Context`** block into the `CosmosDiagnostics` for the owning request (or the trace span for the cache population). Fields include:

| Field | Values |
|---|---|
| `Eligible` | `true` \| `false` |
| `SkipReason` | `NotColdStart` \| `SingleRegion` \| `GatewayDisabled` \| `PpafDisabled` \| `ResourceTypeNotSupported` \| `DirectModeAddressPath` \| `BudgetExhausted` \| `<none>` |
| `Resource` | `Collection` \| `PartitionKeyRange` |
| `PrimaryRegion` | `<region name>` |
| `HedgeRegion` | `<region name \| n/a>` |
| `ThresholdMs` / `ThresholdStepMs` | `<derived values>` |
| `HedgeFiredElapsedMs` | `<ms since attempt start> \| n/a` |
| `WinningRegion` | `<region name>` |
| `LoserOutcome` | `Cancelled` \| `Faulted(<exception type>)` \| `CompletedAfterWinner` \| `n/a` |
| `RetryPolicyFellOverTo` | `<region name> \| n/a` (set by `MetadataRequestThrottleRetryPolicy` when it advances on failure) |
| `TotalAttempts` | `<int>` |

Cosmos on-call engineers and customers can use these fields to confirm the kill-switch took effect, to validate threshold tuning, and to investigate suspected metadata hedge regressions. The fields are also consumed by the SDK's test suite to assert behavior deterministically without reflection.

### 9.1 Telemetry — Metrics (`Meter`) and Events (`EventSource`)

Per-trace diagnostics are sufficient for debugging individual requests but not for monitoring at scale during Phase 2 default-on canary. Two complementary surfaces are emitted: an OpenTelemetry-compatible `Meter` for numeric/histogram aggregation, and the existing SDK `EventSource` for typed per-event tracing.

#### 9.1.1 Metrics (`System.Diagnostics.Metrics.Meter`)

Meter name: **`Azure.Cosmos.Client.MetadataHedging`**.

The meter name follows the established SDK convention (`Azure.Cosmos.Client.Operation`, `Azure.Cosmos.Client.Request` defined in `Microsoft.Azure.Cosmos/src/Telemetry/CosmosDbClientMetrics.cs`). Customers who enable telemetry via the glob `meterProviderBuilder.AddMeter("Azure.Cosmos.Client.*")` pick this up automatically. Instrument names use the existing `azure.cosmosdb.client.*` prefix to match `azure.cosmosdb.client.operation.request_charge`, `azure.cosmosdb.client.active_instance.count`, etc.

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `azure.cosmosdb.client.metadata_hedging.fires` | counter | `{request}` | Number of hedge dispatches (i.e., threshold elapsed and hedge sent). |
| `azure.cosmosdb.client.metadata_hedging.hedge_wins` | counter | `{request}` | Number of operations whose acceptable winner was the hedge branch (not the primary). The win *rate* is the customer-side ratio `hedge_wins / fires`; this counter shape is more conventional than a `(0..1)` histogram and composes correctly across rollups. |
| `azure.cosmosdb.client.metadata_hedging.budget_exhausted` | counter | `{request}` | Number of eligible requests skipped because the per-client semaphore was full. A sustained non-zero value indicates the customer should raise `PerClientConcurrencyBudget`. |
| `azure.cosmosdb.client.metadata_hedging.late_loser` | counter | `{request}` | Loser settled after the winner returned. Sustained large values indicate the loser's HTTP request is not honoring cancellation and is consuming connection-pool capacity beyond the perceived end of the operation. |
| `azure.cosmosdb.client.metadata_hedging.hedge_auth_reject` | counter | `{request}` | Hedge returned 401 or plain 403 and was rejected by the per-branch overlay (§5.13). Sustained non-zero for a tenant strongly suggests RBAC role assignments missing in the secondary or sovereign-cloud audience drift. |
| `azure.cosmosdb.client.metadata_hedging.hedge_fired_elapsed` | histogram | `s` | Distribution of `HedgeFiredElapsedMs` (always ≥ `Threshold`). Used to validate the threshold tuning. |

These instruments are the **primary signal** for Phase 2 (default-on canary) and Phase 3 (full rollout) decisions. Trace-datum log scraping is acceptable for ad-hoc debugging but does not scale to per-region/per-customer monitoring.

#### 9.1.2 Events (`EventSource`)

EventSource emits **strongly-typed per-event records**, not OTel counters — the two surfaces serve different consumers (ETW/`logman` for the EventSource, OTLP/Prometheus for the Meter) and are intentionally not collapsed. The events are added to the existing SDK EventSource (`Microsoft.Azure.Cosmos.CosmosDbEventSource`) under a new keyword `MetadataHedging`:

| Event method | Payload | Triggered when |
|---|---|---|
| `OnMetadataHedgeFired(string primaryRegion, string hedgeRegion, double elapsedMs)` | as named | The hedge timer elapsed and the hedge branch was dispatched. |
| `OnMetadataHedgeWon(string hedgeRegion, double totalElapsedMs)` | as named | The hedge branch was the acceptable winner. |
| `OnMetadataHedgePrimaryWon(string primaryRegion, double totalElapsedMs, bool hedgeFired)` | as named | The primary branch was the acceptable winner. |
| `OnMetadataHedgeSkipped(string skipReason, string resourceType)` | as named | A request was ineligible for hedging — the `SkipReason` enum is the payload. |
| `OnMetadataHedgeAuthReject(string hedgeRegion, int statusCode)` | as named | A hedge response was rejected by the per-branch 401/403 overlay (§5.13). |

EventSource consumers ingest one record per event; the metrics surface aggregates them. There is intentionally no `Counter` directly on `EventSource` — the previous draft conflated these two patterns; this version separates them.

Operationally, no customer action is required to enable the feature in steady state — the feature is PPAF-aligned and ships ON by default for PPAF-enabled accounts in multi-region configurations. The Gateway kill-switch `disableCrossRegionalHedging` (existing) provides the same operator-controlled escape hatch as for data-plane hedging. The `CosmosClientOptions.EnableMetadataHedgingForColdStart` opt-in (tri-state, see §5.1) is a permanent customer-side escape hatch — `null` follows the phase default, `false` forces the feature off without disabling the SDK.

---

## 10. Edge Cases and Risk Analysis

- **Container recreated with the same name:** cache evicts and repopulates. Looks like cold start. Acceptable false positive — the new incarnation is genuinely first-population.
- **404 on Collection Read:** `MetadataRequestThrottleRetryPolicy` does NOT retry 404. Hedge branch may still race; the first 404 (an acceptable winner per §5.13 `IsAcceptableWinner`) wins and surfaces normally. Both branches returning 404 is also fine.
- **Primary returns 500 or 503 before threshold elapses:** `IsAcceptableWinner` returns `false` for the primary (both 500 and 503 are regional failures per `RetryUtility.IsRegionalFailure`). The primary's step-5a early-return path is NOT taken; control falls through to dispatch the hedge after threshold. The wait-for-winner loop then prefers the hedge's acceptable response, OR — if both branches settle without an acceptable winner — surfaces the primary's 500/503 to `MetadataRequestThrottleRetryPolicy` which will then advance via the §5.7.4 attempted-endpoints skip. Note the **asymmetry**: when the hedge wins on a primary-503/500, the primary's regional-failure signal is observed only by `BackgroundCleanupAsync` and never reaches the retry policy, so `LocationCache.MarkEndpointUnavailableForRead(primary)` is not called for that operation. This is a deliberate tradeoff — see §5.7.1.
- **Both hedge branches fault (or both transient):** `ExecuteAsync`'s wait-for-acceptable-winner loop (§5.3 step 6) drains both branches. If neither produces an acceptable response, the **primary**'s outcome is preferred (returned via `ObserveWinningTaskAsync` which re-raises via `ExceptionDispatchInfo` preserving the throwing-frame stack). `MetadataRequestThrottleRetryPolicy` then classifies the primary's failure and advances (with the §5.7.4 attempted-endpoints skip) to the next preferred region.
- **Mid-flight kill-switch flip to `true`:** the helper re-checks `isHedgingDisabledByGateway()` immediately before dispatching the hedge request after the timer fires. If the flag flipped during the wait, the hedge is suppressed and the primary outcome is awaited via `ObserveWinningTaskAsync`.
- **Late loser settles after winner returned:** `BackgroundCleanupAsync` awaits the loser, disposes its `DocumentServiceResponse` body, and updates `diag.LoserOutcome` (volatile field) and the `azure.cosmosdb.client.metadata_hedging.late_loser` counter. The loser's exception (including the `OperationCanceledException` from the selective loser-CTS cancel) never escapes — it cannot reach `MetadataRequestThrottleRetryPolicy` and therefore cannot trigger `MarkEndpointUnavailableForRead` against the loser's region (§5.7.1).
- **PK-range ReadFeed continuation across regions:** explicitly avoided — only the first page hedges; subsequent pages are pinned to the winner. Eliminates ETag/continuation drift across regions.
- **Concurrent cold-start of N collections:** bounded by the per-client semaphore (default 8). Beyond 8, additional cold-start metadata loads skip the hedge with `SkipReason=BudgetExhausted`; they still complete via the primary region. Customers with N > 8 may raise `MetadataHedgingOptions.PerClientConcurrencyBudget` (public — §5.1).
- **Session token / write isolation:** not affected. Both branches issue read-only metadata GETs; no write side-effects in either region.
- **Single-region account:** `ReadEndpoints.Count == 1` → `SkipReason=SingleRegion`, primary-only behavior; no overhead beyond a single dictionary check.
- **All preferred regions in `ExcludeRegions`:** `SkipReason=ExcludedRegionLeavesNoTarget`, primary runs alone.
- **Customer with `EnablePartitionLevelFailover=false`:** out of scope by design; metadata hedging is PPAF-aligned and does not fire.
- **RBAC role assignment missing in secondary:** hedge always 401s; per §5.13, 401/403 from the hedge is classified non-winning so the primary's outcome surfaces unaffected. Operator detects via `azure.cosmosdb.client.metadata_hedging.fires` growing with non-zero `azure.cosmosdb.client.metadata_hedging.hedge_auth_reject` for that customer's traffic shape.

---

## 11. Testing Strategy

- **Unit tests for `MetadataHedgingStrategy`** using a controllable in-process "send" delegate that simulates per-region latency, faults, and cancellations.
- **Eligibility matrix test:** N-axis `[DataRow]` over `(PpafEnabled, KillSwitchOn, ResourceType, AlreadyHedgedThisOperation, ExcludeRegionsBlocksAll)` asserting `Eligible` / `SkipReason` combinations.
- **Cold-start signal test:** first call hedges; second call (cache hit) does not hedge (cache short-circuits before the populate delegate). Note (PR [#5923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5923)): a force-refresh read is **no longer** asserted to skip — it is now hedge-eligible (see the warm-read tests below).
- **Warm / non-cold-start tests (PR #5923):**
  - `MetadataHedgingStrategyTests.EvaluateEligibility_NotColdStart_StillEligible` and `…_PartitionKeyRangeFirstPage_StillEligible` — a warm (`IsColdStart == false`) supported read is `Eligible`.
  - `…_NotColdStart_UnsupportedResource_StillSkipsByType` — the request-type restriction still rejects non-metadata resources on warm reads.
  - `MetadataHedgingStrategyTests.ExecuteAsync_WarmRead_PrimarySlowHedgeFast_HedgeWins` and `…_WarmRead_PrimaryWinsBeforeThreshold_DoesNotDispatchHedge` — warm reads hedge on a slow primary and stay primary-only on a fast primary.
  - `ClientCollectionCacheTests.ClientCollectionCache_WithStrategy_WarmRead_PrimarySlow_HedgeFires_TwoSends` / `…_WarmRead_FastPrimary_SingleSend` — the `forceRefresh: true` (warm) collection-read path hedges only when the primary is slow.
  - `PartitionKeyRangeCacheTests.WithStrategy_WarmRefresh_PrimarySlow_PageOneHedges` / `…_WarmRefresh_FastPrimary_SingleSendPerPage` — the refresh PKRange path hedges page 1 on a slow primary and pins page 2 to the winner.
- **Coordination test:** hedge attempts region A+B; retry policy advances to region C (not A or B); when no untried region remains, retry terminates instead of looping.
- **PK-range pagination test:** page 1 hedges and selects region B; pages 2..N are sent against region B only (asserts `senderCallCount` per region).
- **Budget exhaustion test:** 20 concurrent cold-start collection loads with budget=8 → 8 hedge, 12 skip with `BudgetExhausted`. Then raise `PerClientConcurrencyBudget=32` and confirm all 20 hedge.
- **No-re-hedge-across-retries test:** first attempt hedges (regions A+B both fault), retry attempts region C only (no second hedge); assertion: `azure.cosmosdb.client.metadata_hedging.fires == 1` across the whole operation, even though the cache was never populated (still cold).
- **Loser-cancellation invariant test (Finding #2 regression guard):** primary returns 200 after threshold; hedge returns 200 before primary; assertion: `MarkEndpointUnavailableForRead` is NOT called on the primary's endpoint (use `LocationCache` instrumentation), and no `OperationCanceledException` escapes `MetadataHedgingStrategy.ExecuteAsync`.
- **Loser-disposal test:** assert `DocumentServiceResponse.Dispose` is called exactly once for both winner and loser (no handle leak on `ResponseBody` stream).
- **Threshold-derivation test:** changing `HttpTimeoutPolicy.FirstAttemptTimeout` updates `MetadataHedgeThreshold`; an assertion guards the invariant `Threshold > FirstAttemptTimeout`.
- **Cross-policy-type test (Finding #7 regression guard):** wrap `MetadataRequestThrottleRetryPolicy` in a decorator (or substitute a `Mock<IDocumentClientRetryPolicy>`); assert that hedge still runs (dedup degrades to a no-op) and no `InvalidCastException` is thrown.
- **net472 stack-overflow regression test (Finding #12 / PR #5870 lesson):** 50 concurrent cold-start hedges with a cancelling primary on net472; assertion: no `StackOverflowException`.
- **Diagnostics-shape test:** every observable field listed in [§9](#9-diagnostics-and-operational-usage) and every counter in §9.1 is present and correctly populated for at least one fired-hedge and one skipped-hedge scenario.
- **Data-plane regression test:** existing `CrossRegionHedgingAvailabilityStrategy` data-plane behavior is unchanged by these metadata-hedging additions (the two strategies must not interfere).
- **Emulator integration test** (`Microsoft.Azure.Cosmos.EmulatorTests`): a fault-injection scenario where the primary Gateway pauses for 2 s; assert that the hedge fires and the first document operation completes within ~1.6 s.
- **Hedge 401/403 per-branch overlay test (Finding B2 regression guard):** hedge branch returns `401 Unauthorized`; primary returns 200 after threshold; assertion: `MetadataHedgingResult.WinningResponse.StatusCode == 200`, `diag.HedgeOutcome == "Auth401"`, `azure.cosmosdb.client.metadata_hedging.hedge_auth_reject` incremented by 1. Repeat for `403 Forbidden` (sub-status NOT `DatabaseAccountNotFound`); assert `diag.HedgeOutcome == "Auth403"`. Negative cases: `403 + DatabaseAccountNotFound` from the hedge must classify as regional-failure (existing path) — not auth-reject.
- **Mid-flight kill-switch flip test:** primary in flight, threshold timer running. Flip `DocumentClient.IsHedgingDisabledByGateway` to `true` (via mock); assertion: when the timer fires, the hedge is suppressed (`SkipReason` is set on diag, no hedge `DocumentServiceRequest` is dispatched to the secondary store-model), and the primary outcome is awaited via `ObserveWinningTaskAsync`.
- **Primary 500 → hedge wins, retry-policy still sees 500 on follow-on test (Finding R1 regression guard):** primary returns 500 at t=1.4 s; hedge wins at t=2.0 s with 200; assertion (a): `MetadataHedgingResult.WinningResponse.StatusCode == 200` and the operation succeeds; assertion (b): `LocationCache.MarkEndpointUnavailableForRead(primaryEndpoint)` is NOT called for this operation (validates the deliberate asymmetry in §5.7.1.1); assertion (c): a follow-on data-plane request whose first attempt routes to the same primary observes the next 500 and *then* triggers `MarkEndpointUnavailableForRead` — proving the gap closes within one extra request.
- **Session-token RID-change cleanup preserved test (regression guard for the existing `SessionContainer` invalidation path):** simulate `Collection.ResourceId` change between two cold-start hedged reads of the same collection name (container recreated with same name → §5.11 false positive). Assertion: `SessionContainer.ClearTokenByResourceId(oldRid)` is invoked exactly once, the new RID's session tokens are independently maintained, and no stale-RID session token leaks into the second cold-start's hedge dispatch. This guards the pre-existing collection-recreated-same-name path against accidental coupling to hedge-context state.

---

## 12. Rollout Plan

1. **Phase 1:** Ship behind `CosmosClientOptions.EnableMetadataHedgingForColdStart` as `bool?` (default `null` → off this phase). Internal pre-release testing in TIP / Test cloud. `MetadataHedgingOptions` is public from day one.
2. **Phase 2:** Default-on for PPAF-enabled multi-region clients in canary regions. The phase default for `null` becomes "on". Customers can still force-off by setting `EnableMetadataHedgingForColdStart = false`. Monitor secondary Gateway QPS, P99 first-op latency, the §9.1 instruments (`azure.cosmosdb.client.metadata_hedging.hedge_wins / .fires`, `.budget_exhausted`, `.late_loser`), and `Metadata Hedge Context` diagnostics.
3. **Phase 3:** Default-on everywhere PPAF is enabled (the phase default for `null` becomes "on" globally). **The `EnableMetadataHedgingForColdStart` property is NOT removed** — only its phase default changes. This avoids a source/binary break against customers who set it explicitly to `true` during Phase 1/2. The Gateway kill-switch `disableCrossRegionalHedging` remains as the operator-side escape hatch; the per-client `EnableMetadataHedgingForColdStart = false` remains as the customer-side escape hatch. `MetadataHedgingOptions.PerClientConcurrencyBudget` (public) remains tunable for high-container-cardinality startups.
4. **Phase 4 (follow-up, separate design):** consider extending the same machinery to `GatewayAccountReader` account-properties read, the most visible cold-start metadata operation but the most invasive to hedge safely.

---

## 13. Summary

This design adds bounded, cold-start-only, cross-region hedging for two well-defined metadata cache loads (Collection read and PartitionKeyRange first-page read feed). The threshold is derived from — and remains greater than — the first local HTTP retry timeout, so the cheap local retry runs first and the hedge is only paid when the primary is genuinely slow. The feature coexists with the existing PPAF Gateway kill-switch (`disableCrossRegionalHedging`) for safe operator rollback, dedupes regions with `MetadataRequestThrottleRetryPolicy` to prevent attempt amplification, pins PK-range pagination to a single winning region for ETag consistency, and is bounded by a per-client concurrency budget to protect secondary Gateway capacity during mass cold starts. The result is a low-blast-radius, observable mitigation for one of the most user-visible PPAF cold-start latency tails.
