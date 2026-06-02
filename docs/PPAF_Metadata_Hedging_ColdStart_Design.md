# Design: Cross-Regional Hedging for Cold-Start Metadata Requests

**PPAF – Bounded Cross-Region Hedging for Collection Metadata and Partition Key Range Cache Population**

- **Author:** Debdatta Kunda
- **Date:** 2026-05-29
- **Status:** Draft
- **Related:** PR [#5829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5829) (Gateway hedging kill-switch), Issue [#5642](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5642) (first-attempt HTTP timeout raise)

---

## 1. Background and Motivation

Cross-region hedging in the Cosmos DB .NET SDK is currently scoped to the data plane only. The `CrossRegionHedgingAvailabilityStrategy` (`Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/CrossRegionHedgingAvailabilityStrategy.cs`) is invoked exclusively from `RequestInvokerHandler.AvailabilityStrategy` for the `RequestMessage` pipeline, and its `ShouldHedge()` method explicitly bails out when `ResourceType != Document`. As a result, the SDK gets no cross-region latency protection for control-plane / metadata operations that must complete before the first document operation can be served.

During cold start, every `CosmosClient` must populate three foundational caches before issuing any data-plane work:

- **Database Account properties** (`GatewayAccountReader`) — establishes regions, write/read endpoints.
- **Container metadata** (`ClientCollectionCache.ReadCollectionAsync`) — used to resolve collection RID, indexing/partitioning settings.
- **Partition Key Range routing map** (`PartitionKeyRangeCache.GetRoutingMapForCollectionAsync`) — used to route by partition key.

All three cache loads flow through the Gateway HTTP path. The HTTP layer applies `HttpTimeoutPolicyControlPlaneRetriableHotPath` whose first-attempt timeout was recently raised from 500 ms to **1 s** (issue [#5642](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5642)). When the primary region's Gateway is healthy, the impact is invisible; but when the primary region is degraded — brownout, partial failure, AAD slowness, transient TLS/connection establishment churn — a single slow metadata response can stall the entire client warm-up by 1 s + the local retry backoff before the next attempt completes. Production telemetry shows this as elevated p99 latency on the first operation issued by a cold client, which is exactly the request that customer SLOs are most sensitive to.

The existing `MetadataRequestThrottleRetryPolicy` already supports cross-region fallback, but only *reactively* (after a failure or `503` / `Gone+LeaseNotFound` / `Forbidden+DatabaseAccountNotFound`) and only *sequentially*. There is no proactive, latency-driven hedging path for metadata reads today.

---

## 2. Problem Statement

There is no proactive cross-regional hedging mechanism for metadata cache population during cold start. As a result, cold-start clients pay the full first-attempt HTTP timeout (1 s + backoff) any time the primary-region Gateway is slow but not outright failing on critical metadata endpoints (Collection read, PartitionKeyRange ReadFeed). Because Document operations block on these caches, this slow-but-not-failing window directly inflates the client's perceived first-operation latency — the metric most operators and customers use to judge SDK responsiveness.

---

## 3. Goals

- Reduce the cold-start metadata tail latency contributed by slow (but non-failing) Gateway responses, by issuing a hedged request to a secondary region after a bounded threshold.
- Scope strictly to the **cold-start window** — hedging fires only on the first-time population of a given cache entry, never on refresh, force-refresh, or cache-eviction recovery.
- Scope strictly to **two metadata resource types** in the initial release: `ResourceType.Collection` (Read) and `ResourceType.PartitionKeyRange` (ReadFeed, **first page only**).
- Use a hedge threshold of **~1.5 s** by default — greater than the current first local HTTP retry timeout (1 s in `HttpTimeoutPolicyControlPlaneRetriableHotPath`) — so the local retry attempt is allowed to complete before a cross-region request is issued.
- Coexist cleanly with the existing `MetadataRequestThrottleRetryPolicy` cross-region fallback — no double counting of attempts, no two paths racing to the same secondary region.
- Honor the existing PPAF Gateway kill-switch (`disableCrossRegionalHedging` account property): when the flag is `true`, metadata hedging is also suppressed.
- Emit complete, branch-level diagnostics for supportability (eligible, fired, winner, loser, skip reason).
- Preserve current behavior for steady-state (warm) clients and for single-region accounts.

---

## 4. Non-Goals

- Hedging account-properties reads (`GatewayAccountReader`). The account read happens before regions are known and is governed by separate multi-region/retry logic; it is a likely follow-up candidate but is out of scope here.
- Hedging address resolution / `GlobalAddressResolver` requests used by the Direct (TCP) store model.
- Hedging data-plane (`Document`) requests — already covered by the existing `CrossRegionHedgingAvailabilityStrategy`.
- Hedging cache refresh / force-refresh paths. Only the first-time population of a cache entry is in scope.
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

A single new opt-in is added to `CosmosClientOptions` for the staged rollout:

```csharp
namespace Microsoft.Azure.Cosmos
{
    public class CosmosClientOptions
    {
        // ... existing members ...

        /// <summary>
        /// When set to true, the SDK proactively hedges the first-time population of
        /// Collection and PartitionKeyRange metadata caches to a secondary region
        /// after <see cref="MetadataHedgingOptions.Threshold"/> elapses. Hedging is
        /// only applied during cold start (first cache entry population), is bounded
        /// by a per-client concurrency budget, and is suppressed when the Gateway
        /// kill-switch (disableCrossRegionalHedging) is enabled.
        /// Default: false in the first release (opt-in); removed once defaulted on.
        /// </summary>
        public bool EnableMetadataHedgingForColdStart { get; set; }

        /// <summary>
        /// Advanced knobs for metadata hedging. Optional; defaults are derived from
        /// HttpTimeoutPolicyControlPlaneRetriableHotPath at startup.
        /// </summary>
        internal MetadataHedgingOptions MetadataHedgingOptions { get; set; }
    }

    internal sealed class MetadataHedgingOptions
    {
        public TimeSpan? Threshold { get; set; }         // default: firstCpRetryTimeout + 500ms
        public TimeSpan? ThresholdStep { get; set; }     // default: 500ms (unused while max-branches = 1)
        public int MaxHedgeBranchesPerAttempt { get; set; } = 1;
        public int PerClientConcurrencyBudget { get; set; } = 8;
    }
}
```

`MetadataHedgingOptions` stays `internal` for the first release so the field set is not part of the public API surface; only the boolean toggle is public-visible until the design defaults on.

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
    /// and to pin PK-range pagination to the winning region.
    /// </summary>
    internal sealed class MetadataHedgingContext
    {
        public bool IsColdStart { get; set; }                    // set by caller from previousValue == null
        public ResourceType ResourceType { get; set; }
        public Uri WinningEndpoint { get; private set; }         // set by strategy after first attempt
        public string WinningRegion { get; private set; }
        public HashSet<Uri> AttemptedEndpoints { get; }          // shared with retry policy
        public bool IsFirstReadFeedPage { get; set; }            // PK-range only; subsequent pages skip hedge

        internal void RecordWinner(Uri endpoint, string region);
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
    }

    internal readonly struct MetadataHedgeEligibility
    {
        public bool IsEligible { get; }
        public MetadataHedgeSkipReason SkipReason { get; }
    }

    internal sealed class MetadataHedgeDiagnostics
    {
        public bool Eligible { get; set; }
        public MetadataHedgeSkipReason SkipReason { get; set; }
        public string PrimaryRegion { get; set; }
        public string HedgeRegion { get; set; }
        public double ThresholdMs { get; set; }
        public double? HedgeFiredElapsedMs { get; set; }
        public string WinningRegion { get; set; }
        public string LoserOutcome { get; set; }     // "Cancelled" | "Faulted(<type>)" | "CompletedAfterWinner" | "n/a"
        public int TotalAttempts { get; set; }
    }
}
```

### 5.3 `ExecuteAsync` — control flow

The orchestration mirrors `CrossRegionHedgingAvailabilityStrategy.ExecuteAvailabilityStrategyAsync` but is significantly simpler because (a) only one hedge branch is created per attempt, and (b) the request is already a `DocumentServiceRequest`, not a `RequestMessage` requiring cloning of `RequestOptions`/`Properties`.

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

    // ---- 2. Acquire concurrency budget (non-blocking) ----
    if (!await this.hedgeBudget.WaitAsync(TimeSpan.Zero, cancellationToken))
    {
        diag.SkipReason = MetadataHedgeSkipReason.BudgetExhausted;
        diag.Eligible = false;
        DocumentServiceResponse primaryOnly = await sendToCurrentlyRoutedEndpoint(request, cancellationToken);
        trace.AddDatum("Metadata Hedge Context", diag);
        return new MetadataHedgingResult(primaryOnly, request.RequestContext.LocationEndpointToRoute, /*region*/ null, hedgeFired: false, diag);
    }

    try
    {
        // ---- 3. Resolve primary + hedge endpoints ----
        Uri primaryEndpoint = request.RequestContext.LocationEndpointToRoute
                              ?? this.globalEndpointManager.ResolveServiceEndpoint(request);
        ReadOnlyCollection<Uri> applicable = this.globalEndpointManager
            .GetApplicableEndpoints(request, isReadRequest: true);
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
        diag.HedgeRegion = this.globalEndpointManager.GetLocation(hedgeEndpoint);

        // ---- 4. Launch primary + delayed hedge ----
        using (CancellationTokenSource linkedCts =
               CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            DocumentServiceRequest primaryReq = request;   // already routed to primary
            hedgeContext.AttemptedEndpoints.Add(primaryEndpoint);

            Stopwatch sw = Stopwatch.StartNew();
            Task<DocumentServiceResponse> primaryTask = sendToCurrentlyRoutedEndpoint(primaryReq, linkedCts.Token);

            Task hedgeTimer = Task.Delay(this.threshold, linkedCts.Token);
            Task<DocumentServiceResponse> hedgeTask = null;

            Task firstCompleted = await Task.WhenAny(primaryTask, hedgeTimer);

            if (firstCompleted == primaryTask
                && IsNonTransient(primaryTask.Result))
            {
                // ---- 5a. Primary wins before threshold ----
                linkedCts.Cancel();   // cancels hedge timer
                diag.TotalAttempts = 1;
                diag.WinningRegion = diag.PrimaryRegion;
                hedgeContext.RecordWinner(primaryEndpoint, diag.PrimaryRegion);
                trace.AddDatum("Metadata Hedge Context", diag);
                return new MetadataHedgingResult(primaryTask.Result, primaryEndpoint, diag.PrimaryRegion, hedgeFired: false, diag);
            }

            // ---- 5b. Threshold elapsed → re-check kill switch, dispatch hedge ----
            if (this.isHedgingDisabledByGateway())
            {
                diag.SkipReason = MetadataHedgeSkipReason.GatewayKillSwitchOn;
                DocumentServiceResponse primaryLate = await primaryTask;
                trace.AddDatum("Metadata Hedge Context", diag);
                return new MetadataHedgingResult(primaryLate, primaryEndpoint, diag.PrimaryRegion, hedgeFired: false, diag);
            }

            DocumentServiceRequest hedgeReq = CloneForHedge(primaryReq, hedgeEndpoint);
            hedgeContext.AttemptedEndpoints.Add(hedgeEndpoint);
            diag.HedgeFiredElapsedMs = sw.Elapsed.TotalMilliseconds;
            hedgeTask = sendToCurrentlyRoutedEndpoint(hedgeReq, linkedCts.Token);

            // ---- 6. Wait for first non-transient winner ----
            Task<DocumentServiceResponse> winner = await Task.WhenAny(primaryTask, hedgeTask);
            Task<DocumentServiceResponse> loser  = (winner == primaryTask) ? hedgeTask : primaryTask;
            Uri winningEndpoint                   = (winner == primaryTask) ? primaryEndpoint : hedgeEndpoint;
            string winningRegion                  = this.globalEndpointManager.GetLocation(winningEndpoint);

            diag.WinningRegion = winningRegion;
            diag.TotalAttempts = 2;
            hedgeContext.RecordWinner(winningEndpoint, winningRegion);

            linkedCts.Cancel();          // signal the loser to stop
            RecordLoserOutcome(loser, diag);   // fire-and-forget continuation; updates diag

            trace.AddDatum("Metadata Hedge Context", diag);
            return new MetadataHedgingResult(await winner, winningEndpoint, winningRegion, hedgeFired: true, diag);
        }
    }
    finally
    {
        this.hedgeBudget.Release();
    }
}
```

`IsNonTransient` returns `true` for 2xx and for 4xx that are not retriable by `MetadataRequestThrottleRetryPolicy` (i.e., not `503/500/Gone+LeaseNotFound/Forbidden+DatabaseAccountNotFound`). For 404 we treat it as a non-transient winner — both branches racing to 404 is benign; the first wins.

`CloneForHedge` creates a `DocumentServiceRequest` copy with `request.RequestContext.RouteToLocation(hedgeEndpoint)` set, cloned headers (notably re-signed `Authorization` if the token is bound to URI), and a fresh `ClientRequestStatistics` snapshot to keep diagnostics from the two branches separable.

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
    IsColdStart = isFirstPopulation,           // see §5.6
    ResourceType = ResourceType.Collection,
};

// Share the attempted-endpoints set with the retry policy to dedupe regions.
((MetadataRequestThrottleRetryPolicy)retryPolicyInstance)?.AttachHedgeContext(hedgeContext);

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

`isFirstPopulation` is determined by the caller in `GetByNameAsync` / `GetByRidAsync`: those methods are reached through `CollectionCache`'s base async-cache plumbing. The simplest plumbing is to pass `forceRefresh`/`previousValue` through as an additional argument on `ReadCollectionAsync` — the existing signature already plumbs `clientSideRequestStatistics` through, so adding `bool isColdStart` is mechanical. `CollectionCache.ResolveByNameAsync` already knows whether this is a refresh.

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
((MetadataRequestThrottleRetryPolicy)metadataRetryPolicy).AttachHedgeContext(hedgeContext);

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
            hedgeContext),     // <- new
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
    MetadataHedgingContext hedgeContext)
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
            cancellationToken: CancellationToken.None);

        return result.Response;
    }
}
```

### 5.6 Cold-start signal — concrete propagation

The "previousValue == null" signal lives in different places for the two caches:

- **`ClientCollectionCache`**: `CollectionCache.ResolveByNameAsync` / `ResolveByRidAsync` invoke `GetByNameAsync` / `GetByRidAsync` through `AsyncCache.GetAsync`. The cache itself knows whether this is an initial population or a refresh (the latter sets `forceRefresh: true` or passes a non-null `previousValue`). We propagate that bit by adding an `isColdStart` parameter to `ReadCollectionAsync` and the matching base methods, defaulting `false`. Refresh callers explicitly pass `false`; first-time callers pass `true`.

- **`PartitionKeyRangeCache`**: `GetRoutingMapForCollectionAsync` already receives `previousRoutingMap`. `isColdStart = previousRoutingMap == null` is the exact signal — no plumbing change needed beyond setting it on `MetadataHedgingContext`.

### 5.7 Coordination with `MetadataRequestThrottleRetryPolicy`

The retry policy gets one new internal method:

```csharp
internal sealed class MetadataRequestThrottleRetryPolicy : IDocumentClientRetryPolicy
{
    private MetadataHedgingContext hedgeContext;   // optional; null = no hedge in use

    internal void AttachHedgeContext(MetadataHedgingContext context)
    {
        this.hedgeContext = context;
    }

    // Existing IncrementRetryIndexOnUnavailableEndpointForMetadataRead() is modified
    // to skip RetryLocationIndex values that resolve to an endpoint already present in
    // hedgeContext.AttemptedEndpoints. If no untried region remains, return false
    // (terminates retry instead of looping on a region the hedge already exhausted).
}
```

This guarantees: if the hedge tried regions {A, B}, a retry after both branches fail advances to region C — not back to A or B.

### 5.8 Hedge execution model — summary

1. Evaluate eligibility ([§6](#6-eligibility-and-precedence-rules)). If ineligible, send to primary only.
2. Acquire one slot from the per-client semaphore (non-blocking `WaitAsync(TimeSpan.Zero)`). If unavailable → skip hedge with `SkipReason=BudgetExhausted`.
3. Resolve primary endpoint (`request.RequestContext.LocationEndpointToRoute` already set by `MetadataRequestThrottleRetryPolicy.OnBeforeSendRequest`) and pick the first different applicable endpoint as the hedge target.
4. Send the primary request and start a `Task.Delay(Threshold)` timer.
5. If the primary returns a non-transient response before the timer elapses → return that response, cancel timer.
6. If the timer elapses first → re-check the Gateway kill-switch; if flipped to true → suppress hedge, await primary. Otherwise issue the cloned hedge request to the secondary endpoint.
7. `Task.WhenAny(primary, hedge)`: first non-transient response wins; loser is cancelled via the linked CTS. A continuation records loser outcome in diagnostics.
8. If both branches fault → surface the primary's exception via `ExceptionDispatchInfo`. `MetadataRequestThrottleRetryPolicy` then advances (with dedup) and retries.
9. Always release the semaphore in `finally`.

### 5.9 Threshold derivation

The default hedge threshold is set to `firstControlPlaneRetriableTimeout + 500 ms`, where `firstControlPlaneRetriableTimeout` is read from `HttpTimeoutPolicyControlPlaneRetriableHotPath.TimeoutsAndDelays[0]`. Today that resolves to **1 s + 500 ms = 1.5 s**. The derivation (rather than a hard-coded constant) ensures the invariant *"hedge threshold > first local HTTP retry timeout"* is preserved automatically if the underlying HTTP policy ever changes again (the 500 ms → 1 s bump in issue #5642 is precedent that this can move). The `ThresholdStep` is 500 ms, mirroring the data-plane default.

### 5.10 Cold-start signal (rationale)

Cold start is defined **per cache key**, not process-wide:

- **`ClientCollectionCache`**: the request enters the `singleValueInitFunc` path for a never-before-seen collection name/RID. `AsyncCacheNonBlocking` already coalesces concurrent callers, so multiple parallel cold callers for the same collection produce a single underlying init — and therefore at most one hedge.
- **`PartitionKeyRangeCache`**: the `routingMapCache` entry for this collection RID has no previous value (`previousRoutingMap == null` in `GetRoutingMapForCollectionAsync`). Subsequent calls with a `previousRoutingMap`, including `ShouldForceRefresh`-driven refreshes, do not hedge.

A container-recreate-with-same-name scenario will look like "init" to the cache after eviction; this is an acceptable false positive (it is still a true latency-sensitive first population for that incarnation of the container). Diagnostics record the trigger so support can disambiguate if needed.

### 5.11 Concurrency budget (rationale)

At app startup it is common for a single `CosmosClient` to initialize many containers in parallel. Without a limiter, every cold-start collection / PK-range load would emit a hedged request to the secondary region simultaneously, doubling startup pressure on the secondary Gateway. A **per-client `SemaphoreSlim` with a small fixed capacity (default 8 in-flight metadata hedges per client)** bounds this. If the budget is exhausted, the hedge is **skipped** (best-effort) and a skip-reason is recorded in diagnostics; the primary request still runs unmodified.

---

## 6. Eligibility and Precedence Rules

A metadata request is eligible for hedging only when **ALL** of the following are true:

1. The Gateway flag `disableCrossRegionalHedging` is **NOT** `true`. This flag (introduced in PR [#5829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5829)) takes precedence; when set, both data-plane **AND** metadata hedging are suppressed.
2. `ConnectionPolicy.EnablePartitionLevelFailover` is `true` (alignment with PPAF scope — metadata hedging is offered as a PPAF cold-start tail-latency mitigation).
3. `GlobalEndpointManager.ReadEndpoints.Count > 1` — there is a secondary region to hedge to.
4. The request is one of: `ResourceType.Collection` Read, or `ResourceType.PartitionKeyRange` ReadFeed **first page**.
5. The cache is performing a **first-time population** for this cache key (`previousValue` is null).
6. The Gateway store model path is in use (no Direct address resolution metadata path is in scope).
7. The per-client concurrency budget for metadata hedges has capacity.

If any of (1)–(7) fails, the request is sent to the primary region only and the skip reason is recorded.

### 6.1 Interaction with `MetadataRequestThrottleRetryPolicy`

`MetadataRequestThrottleRetryPolicy` already performs sequential cross-region fallback on `503 / 500 / Gone+LeaseNotFound / Forbidden+DatabaseAccountNotFound`. To prevent attempt amplification, the hedge helper exposes the set of regions it has just attempted (primary + hedged) via the `MetadataHedgingContext` that shares state with the retry policy:

- On retry, `MetadataRequestThrottleRetryPolicy` advances `RetryLocationIndex` past any region already attempted by the hedge in the previous attempt, preventing the next retry from targeting the same secondary the hedge just used.
- Hedge fires **at most once per retry attempt**. If the first attempt was hedged and both branches faulted, the retry advances to the next not-yet-attempted preferred region; the retry attempt itself does not re-hedge unless eligibility is re-evaluated `true` (typically `false` because the cache key now has a `previousValue`).
- **Total cap:** at most `preferred-region-count` attempts across primary + hedge + retries for a single metadata cache population.

---

## 7. SDK Responsibilities

- Evaluate eligibility ([§6](#6-eligibility-and-precedence-rules)) **before** creating any hedge timer or task, so ineligible requests pay zero hedge cost and produce a clean skip-reason diagnostic.
- On winning, cancel the loser via the linked `CancellationTokenSource`. Do not let a late-arriving loser failure poison the cache state once the winner has populated the cache.
- On PK-range read feed, return the winning region from the first page to the caller and pin all subsequent pages to that same region by setting `request.RequestContext.RouteToLocation(winningEndpoint)`.
- Coordinate with `MetadataRequestThrottleRetryPolicy` through `MetadataHedgingContext` so attempted regions are deduped across hedge and retry.
- Honor `request.RequestContext.ExcludeRegions` (if/when surfaced for metadata) when computing hedge targets.
- Acquire/release the per-client concurrency budget around the hedge launch (always release on completion, including via `try/finally` on the helper).
- Suppress hedging immediately on transition of `disableCrossRegionalHedging → true`, including timers already scheduled but not yet fired.

---

## 8. Threshold and Timeout Considerations

The `HttpTimeoutPolicyControlPlaneRetriableHotPath` today defines three local attempts with timeouts `(1 s, 5 s, 65 s)` and a 1 s gap before the second attempt (per `Microsoft.Azure.Cosmos/src/HttpClient/HttpTimeoutPolicyControlPlaneRetriableHotPath.cs` lines 28–33; the first-attempt value was raised from 500 ms → 1 s by issue #5642). The hedge threshold must remain **greater than** the first local timeout, so that the cheap local retry has a chance to complete before the SDK incurs cross-region network and gateway cost.

**Default values:**

| Knob | Value | Notes |
|---|---|---|
| `Threshold` | `firstControlPlaneRetriableTimeout + 500 ms` | Today: **1.5 s**. Computed at startup, not hard-coded. |
| `ThresholdStep` | `500 ms` | Matches data-plane `CrossRegionHedgingAvailabilityStrategy` default semantics. |
| Max hedge branches per attempt | `1` | One secondary region in the cold-start window; staircase fan-out is intentionally **not** used for metadata. |

An assertion in unit tests will fail-fast if the relationship `MetadataHedgeThreshold > HttpTimeoutPolicyControlPlaneRetriableHotPath` first timeout ever regresses.

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

Operationally, no customer action is required to enable the feature in steady state — the feature is PPAF-aligned and ships ON by default for PPAF-enabled accounts in multi-region configurations. The Gateway kill-switch `disableCrossRegionalHedging` (existing) provides the same operator-controlled escape hatch as for data-plane hedging. A new `CosmosClientOptions.EnableMetadataHedgingForColdStart` opt-in will gate the first release for staged rollout; the opt-in is removed once telemetry validates the default-on behavior.

---

## 10. Edge Cases and Risk Analysis

- **Container recreated with the same name:** cache evicts and repopulates. Looks like cold start. Acceptable false positive — the new incarnation is genuinely first-population.
- **404 on Collection Read:** `MetadataRequestThrottleRetryPolicy` does NOT retry 404. Hedge branch may still race; the first 404 (a non-transient response) wins and surfaces normally. Both branches returning 404 is also fine.
- **Both hedge branches fault:** helper surfaces primary's exception via `ExceptionDispatchInfo` (preserves throwing-frame stack). `MetadataRequestThrottleRetryPolicy` then advances and retries normally on the next preferred location.
- **Mid-flight kill-switch flip to `true`:** hedge timer is created on the lock-protected eligibility path; the helper re-checks the flag immediately before dispatching the hedge request after the timer fires. If the flag flipped during the wait, the hedge is suppressed and the primary outcome is awaited.
- **PK-range ReadFeed continuation across regions:** explicitly avoided — only the first page hedges; subsequent pages are pinned to the winner. Eliminates ETag/continuation drift across regions.
- **Concurrent cold-start of N collections:** bounded by the per-client semaphore (default 8). Beyond 8, additional cold-start metadata loads skip the hedge with `SkipReason=BudgetExhausted`; they still complete via the primary region.
- **Session token / write isolation:** not affected. Both branches issue read-only metadata GETs; no write side-effects in either region.
- **Single-region account:** `ReadEndpoints.Count == 1` → `SkipReason=SingleRegion`, primary-only behavior; no overhead beyond a single dictionary check.
- **Customer with `EnablePartitionLevelFailover=false`:** out of scope by design; metadata hedging is PPAF-aligned and does not fire.

---

## 11. Testing Strategy

- **Unit tests for `MetadataHedgingStrategy`** using a controllable in-process "send" delegate that simulates per-region latency, faults, and cancellations.
- **Eligibility matrix test:** 3-axis `[DataRow]` over `(PpafEnabled, KillSwitchOn, ResourceType)` asserting `Eligible` / `SkipReason` combinations.
- **Cold-start signal test:** first call hedges; second call (cache hit) does not hedge; force-refresh does not hedge.
- **Coordination test:** hedge attempts region A+B; retry policy advances to region C (not A or B).
- **PK-range pagination test:** page 1 hedges and selects region B; pages 2..N are sent against region B only.
- **Budget exhaustion test:** 20 concurrent cold-start collection loads with budget=8 → 8 hedge, 12 skip with `BudgetExhausted`.
- **Threshold-derivation test:** changing `HttpTimeoutPolicyControlPlaneRetriableHotPath`'s first timeout updates `MetadataHedgeThreshold`; an assertion guards the invariant.
- **Diagnostics-shape test:** every observable field listed in [§9](#9-diagnostics-and-operational-usage) is present and correctly populated for at least one fired-hedge and one skipped-hedge scenario.
- **Emulator integration test** (`Microsoft.Azure.Cosmos.EmulatorTests`): a fault-injection scenario where the primary Gateway pauses for 2 s; assert that the hedge fires and the first document operation completes within ~1.6 s.

---

## 12. Rollout Plan

1. **Phase 1:** Ship behind `CosmosClientOptions.EnableMetadataHedgingForColdStart` (default `false`). Internal pre-release testing in TIP / Test cloud.
2. **Phase 2:** Default-on for PPAF-enabled multi-region clients in canary regions. Monitor secondary Gateway QPS, P99 first-op latency, and `Metadata Hedge Context` diagnostics.
3. **Phase 3:** Default-on everywhere PPAF is enabled. Remove the `CosmosClientOptions` opt-in (keep the gateway kill-switch).
4. **Phase 4 (follow-up, separate design):** consider extending the same machinery to `GatewayAccountReader` account-properties read, the most visible cold-start metadata operation but the most invasive to hedge safely.

---

## 13. Summary

This design adds bounded, cold-start-only, cross-region hedging for two well-defined metadata cache loads (Collection read and PartitionKeyRange first-page read feed). The threshold is derived from — and remains greater than — the first local HTTP retry timeout, so the cheap local retry runs first and the hedge is only paid when the primary is genuinely slow. The feature coexists with the existing PPAF Gateway kill-switch (`disableCrossRegionalHedging`) for safe operator rollback, dedupes regions with `MetadataRequestThrottleRetryPolicy` to prevent attempt amplification, pins PK-range pagination to a single winning region for ETag consistency, and is bounded by a per-client concurrency budget to protect secondary Gateway capacity during mass cold starts. The result is a low-blast-radius, observable mitigation for one of the most user-visible PPAF cold-start latency tails.
