//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Bounded cross-region hedging for the two metadata cache reads:
    /// <c>Collection</c> <c>Read</c> and <c>PartitionKeyRange</c> <c>ReadFeed</c>
    /// (first page only). One instance per <see cref="CosmosClient"/>, consumed by
    /// <c>ClientCollectionCache</c> and <c>PartitionKeyRangeCache</c>.
    /// </summary>
    /// <remarks>
    /// Guiding principle (see <c>docs/metadata-hedging-simple-design.md</c>): the
    /// primary is authoritative. When the primary region is slow, a single hedge
    /// is dispatched to another region after a fixed threshold; the first
    /// <em>good</em> answer wins. The hedge can only improve latency — it can
    /// never change the outcome the primary would have produced, which is why the
    /// loser's result is simply observed and discarded.
    /// </remarks>
    internal sealed class MetadataHedgingStrategy
    {
        internal const string TraceDatumKey = "Metadata Hedge";

        /// <summary>
        /// Added to the first-attempt control-plane HTTP timeout to derive the hedge
        /// threshold (today 1&#160;s + 500&#160;ms = 1.5&#160;s). This keeps the
        /// threshold strictly between the first (~1&#160;s) and second (~5&#160;s)
        /// HTTP attempt timeouts. Not customer-configurable.
        /// </summary>
        internal static readonly TimeSpan DefaultThresholdStep = TimeSpan.FromMilliseconds(500);

        private readonly IGlobalEndpointManager globalEndpointManager;
        private readonly Func<bool> isPpafEnabled;
        private readonly Func<bool> isCrossRegionalHedgingDisabled;
        private readonly bool? customerOptIn;
        private readonly TimeSpan threshold;

        public MetadataHedgingStrategy(
            IGlobalEndpointManager globalEndpointManager,
            Func<bool> isPpafEnabled,
            bool? customerOptIn,
            TimeSpan threshold,
            Func<bool> isCrossRegionalHedgingDisabled = null)
        {
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
            this.isPpafEnabled = isPpafEnabled ?? (() => false);

            // Gateway operator kill-switch (disableCrossRegionalHedging). Defaults to "not disabled"
            // for callers/tests that do not wire it; read live per request so a runtime toggle
            // takes effect without a client restart.
            this.isCrossRegionalHedgingDisabled = isCrossRegionalHedgingDisabled ?? (() => false);
            this.customerOptIn = customerOptIn;

            if (threshold <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold));
            }

            this.threshold = threshold;
        }

        internal TimeSpan Threshold => this.threshold;

        /// <summary>
        /// Builds the region-targeted send delegate consumed by <see cref="ExecuteAsync"/>:
        /// route the (already-cloned) request to the supplied endpoint, then dispatch it
        /// through the store model. Keeps the safety-critical region-routing in one place,
        /// shared by <c>ClientCollectionCache</c> and <c>PartitionKeyRangeCache</c>.
        /// </summary>
        internal static Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> StoreModelSender(
            IStoreModel storeModel)
        {
            return (request, targetEndpoint, cancellationToken) =>
            {
                if (targetEndpoint != null)
                {
                    request.RequestContext.RouteToLocation(targetEndpoint);
                }

                return storeModel.ProcessMessageAsync(request, cancellationToken);
            };
        }

        /// <summary>
        /// Builds the strategy from the resolved tri-state opt-in, or returns
        /// <c>null</c> when hedging is explicitly disabled (<c>false</c>). When the
        /// opt-in is <c>true</c> or <c>null</c> the strategy is created and the
        /// per-request eligibility check resolves the effective opt-in against the
        /// live PPAF state (<c>null</c> follows PPAF; <c>true</c> forces on) and the
        /// live gateway <c>disableCrossRegionalHedging</c> operator kill-switch.
        /// </summary>
        internal static MetadataHedgingStrategy CreateIfEnabled(
            bool? enableMetadataHedging,
            IGlobalEndpointManager globalEndpointManager,
            Func<bool> isPpafEnabled,
            Func<bool> isCrossRegionalHedgingDisabled = null)
        {
            // Explicit customer kill-switch: hedging is suppressed regardless of PPAF.
            if (enableMetadataHedging == false)
            {
                return null;
            }

            // Fixed, SDK-derived threshold so the "first < threshold < second"
            // invariant is preserved automatically if the timeout policy changes.
            TimeSpan threshold = HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout
                + DefaultThresholdStep;

            return new MetadataHedgingStrategy(
                globalEndpointManager: globalEndpointManager,
                isPpafEnabled: isPpafEnabled,
                customerOptIn: enableMetadataHedging,
                threshold: threshold,
                isCrossRegionalHedgingDisabled: isCrossRegionalHedgingDisabled);
        }

        /// <summary>
        /// Executes a metadata send with optional cross-region hedging. The caller
        /// supplies a region-targeted send delegate that routes the request to the
        /// given endpoint; the strategy itself never mutates the request's routing.
        /// </summary>
        /// <param name="request">The metadata request to send.</param>
        /// <param name="sendToEndpoint">Delegate that routes a request to the supplied endpoint and sends it.</param>
        /// <param name="isFirstReadFeedPage">
        /// For <c>PartitionKeyRange</c> <c>ReadFeed</c>, whether this is the first page. Only the first
        /// page is hedged; later pages are pinned to the winning region by the caller. Ignored for
        /// <c>Collection</c> <c>Read</c>.
        /// </param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        public async Task<MetadataHedgingResult> ExecuteAsync(
            DocumentServiceRequest request,
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> sendToEndpoint,
            bool isFirstReadFeedPage,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (sendToEndpoint == null)
            {
                throw new ArgumentNullException(nameof(sendToEndpoint));
            }

            Uri primaryEndpoint = request.RequestContext?.LocationEndpointToRoute
                                  ?? this.globalEndpointManager.ResolveServiceEndpoint(request);

            // ---- 0. Eligible? If not, send to the primary only. ----
            if (!this.TryGetHedgeEndpoint(request, isFirstReadFeedPage, primaryEndpoint, out Uri hedgeEndpoint))
            {
                DocumentServiceResponse primaryOnly = await sendToEndpoint(request, primaryEndpoint, cancellationToken);
                return this.BuildResult(primaryOnly, primaryEndpoint, hedgeFired: false, hedgeWon: false);
            }

            // ---- 1. Start primary + a threshold timer. ----
            // timerCts is intentionally NOT linked to the caller token: a user cancellation must
            // not complete the timer and be misread as "threshold elapsed" (which would dispatch a
            // phantom hedge). Caller cancellation flows through the send delegate's token instead,
            // and is short-circuited explicitly before any hedge is dispatched (step 3).
            using CancellationTokenSource timerCts = new CancellationTokenSource();
            Task<DocumentServiceResponse> primaryTask = SendCloneAsync(sendToEndpoint, request, primaryEndpoint, cancellationToken);
            Task timerTask = Task.Delay(this.threshold, timerCts.Token);

            // ---- 2. Primary settled before the threshold? ----
            // The primary is authoritative. If it produced a DEFINITIVE outcome — a success, or a
            // non-regional error such as 404 / 409 / 412 (which in production arrives as a FAULTED
            // task, since these metadata reads throw for status >= 400), or a caller cancellation —
            // return it verbatim and never fire a hedge. Only a REGIONAL failure (the region, not
            // the request, is at fault) is worth hedging.
            await Task.WhenAny(primaryTask, timerTask);
            timerCts.Cancel();
            if (primaryTask.IsCompleted && await ClassifyOutcomeAsync(primaryTask) != BranchOutcome.RegionalFailure)
            {
                return await this.BuildAuthoritativeResultAsync(primaryTask, primaryEndpoint, hedgeFired: false);
            }

            // ---- 3. Primary is slow, or hit a regional failure → fire one hedge. ----
            // Short-circuit on caller cancellation first so a cancelled request never spawns a
            // phantom hedge and the OperationCanceledException surfaces promptly.
            cancellationToken.ThrowIfCancellationRequested();
            Task<DocumentServiceResponse> hedgeTask = SendCloneAsync(sendToEndpoint, request, hedgeEndpoint, cancellationToken);

            // ---- 4. Resolve the winner, keeping the primary authoritative throughout. ----
            return await this.ResolveWinnerAsync(primaryTask, hedgeTask, primaryEndpoint, hedgeEndpoint);
        }

        /// <summary>
        /// Resolves the primary-vs-hedge race once both are in flight. The primary is authoritative:
        /// a successful hedge may win the latency race, but the hedge can never override a primary
        /// that has produced a definitive (non-regional) outcome. When neither branch yields a good
        /// answer, the primary's outcome is returned (its exception rethrown) so the caller's retry
        /// policy sees exactly what it would have without hedging.
        /// </summary>
        private async Task<MetadataHedgingResult> ResolveWinnerAsync(
            Task<DocumentServiceResponse> primaryTask,
            Task<DocumentServiceResponse> hedgeTask,
            Uri primaryEndpoint,
            Uri hedgeEndpoint)
        {
            Task<DocumentServiceResponse> firstSettled = await Task.WhenAny(primaryTask, hedgeTask);
            bool firstIsHedge = firstSettled == hedgeTask;

            if (firstIsHedge)
            {
                // A fast, successful hedge wins — unless the primary has ALREADY produced an
                // authoritative (non-regional) outcome, in which case the primary wins and the
                // hedge response is discarded (the hedge must never override the primary's answer).
                if (await ClassifyOutcomeAsync(hedgeTask) == BranchOutcome.Success)
                {
                    if (primaryTask.IsCompleted && await ClassifyOutcomeAsync(primaryTask) != BranchOutcome.RegionalFailure)
                    {
                        ObserveInBackground(hedgeTask);
                        return await this.BuildAuthoritativeResultAsync(primaryTask, primaryEndpoint, hedgeFired: true);
                    }

                    ObserveInBackground(primaryTask);
                    return this.BuildResult(await hedgeTask, hedgeEndpoint, hedgeFired: true, hedgeWon: true);
                }

                // Hedge failed (regional failure, or a definitive / auth error) → it cannot win.
                // Wait for the primary and return its authoritative outcome.
                await SwallowAsync(primaryTask);
                ObserveInBackground(hedgeTask);
                return await this.BuildAuthoritativeResultAsync(primaryTask, primaryEndpoint, hedgeFired: true);
            }

            // Primary settled first.
            if (await ClassifyOutcomeAsync(primaryTask) != BranchOutcome.RegionalFailure)
            {
                // Success or definitive error → authoritative; never overridden by the hedge.
                ObserveInBackground(hedgeTask);
                return await this.BuildAuthoritativeResultAsync(primaryTask, primaryEndpoint, hedgeFired: true);
            }

            // Primary regional failure → a successful hedge may now win; otherwise the primary's
            // (authoritative) regional failure is returned.
            await SwallowAsync(hedgeTask);
            if (await ClassifyOutcomeAsync(hedgeTask) == BranchOutcome.Success)
            {
                ObserveInBackground(primaryTask);
                return this.BuildResult(await hedgeTask, hedgeEndpoint, hedgeFired: true, hedgeWon: true);
            }

            ObserveInBackground(hedgeTask);
            return await this.BuildAuthoritativeResultAsync(primaryTask, primaryEndpoint, hedgeFired: true);
        }

        /// <summary>
        /// Determines whether the request is eligible for hedging and, if so, selects a
        /// hedge endpoint in a different region than the primary (honoring ExcludeRegions).
        /// </summary>
        private bool TryGetHedgeEndpoint(
            DocumentServiceRequest request,
            bool isFirstReadFeedPage,
            Uri primaryEndpoint,
            out Uri hedgeEndpoint)
        {
            hedgeEndpoint = null;

            // Operator kill-switch (gateway disableCrossRegionalHedging): a hard override that
            // suppresses cross-region metadata hedging during a regional incident, regardless of
            // the customer opt-in or PPAF state — mirroring how the data-plane AvailabilityStrategy
            // is disabled by the same flag. Read live so a runtime toggle takes effect without a
            // client restart.
            if (this.isCrossRegionalHedgingDisabled())
            {
                return false;
            }

            // Effective opt-in: an explicit customer value wins; null follows PPAF.
            if (!(this.customerOptIn ?? this.isPpafEnabled()))
            {
                return false;
            }

            // Only the two supported metadata reads are hedged.
            if (!IsSupportedResource(request))
            {
                return false;
            }

            // PKRange ReadFeed hedges on the first page only; later pages are pinned
            // to the winning region by the caller for continuation consistency.
            if (request.ResourceType == ResourceType.PartitionKeyRange && !isFirstReadFeedPage)
            {
                return false;
            }

            // Candidate hedge regions honoring ExcludeRegions, from a SINGLE source used both for
            // the multi-region eligibility check and the hedge-endpoint pick (so the two can never
            // drift). GetApplicableEndpoints returns an availability-ordered list, so the first
            // non-primary entry is the best available cross-region target.
            ReadOnlyCollection<Uri> applicable = this.globalEndpointManager.GetApplicableEndpoints(request, isReadRequest: true);
            if (applicable == null || applicable.Count <= 1)
            {
                return false;
            }

            hedgeEndpoint = applicable.FirstOrDefault(u => u != null && !u.Equals(primaryEndpoint));
            return hedgeEndpoint != null;
        }

        private static bool IsSupportedResource(DocumentServiceRequest request)
        {
            return (request.ResourceType == ResourceType.Collection && request.OperationType == OperationType.Read)
                || (request.ResourceType == ResourceType.PartitionKeyRange && request.OperationType == OperationType.ReadFeed);
        }

        /// <summary>
        /// The outcome class of a completed branch, used to keep the primary authoritative:
        /// only a <see cref="RegionalFailure"/> is worth hedging, and a hedge can never override
        /// a primary <see cref="Success"/> or <see cref="Definitive"/> outcome.
        /// </summary>
        private enum BranchOutcome
        {
            /// <summary>A definitive success: a response with status &lt; 400.</summary>
            Success,

            /// <summary>
            /// A regional failure (503 / 500 / 410+LeaseNotFound / 403+DatabaseAccountNotFound) —
            /// the region, not the request, is at fault, so another region is worth trying.
            /// </summary>
            RegionalFailure,

            /// <summary>
            /// A definitive, authoritative non-regional outcome: a non-regional error (404 / 409 /
            /// 412 / 401 / ...), a caller cancellation, or any other terminal state the primary is
            /// entitled to own. The hedge must never override this.
            /// </summary>
            Definitive,
        }

        /// <summary>
        /// Classifies a COMPLETED branch task. In production these metadata reads throw for status
        /// &gt;= 400, so regional failures arrive as faulted tasks (classified via the exception);
        /// the response-status path is a defensive fallback for exceptionless-retry statuses.
        /// The task is guaranteed complete by the caller, so awaiting it cannot block.
        /// </summary>
        private static async Task<BranchOutcome> ClassifyOutcomeAsync(Task<DocumentServiceResponse> completedTask)
        {
            if (completedTask.IsFaulted)
            {
                return IsRegionalFailureException(completedTask.Exception)
                    ? BranchOutcome.RegionalFailure
                    : BranchOutcome.Definitive;
            }

            if (completedTask.IsCanceled)
            {
                // Authoritative; never overridden by a hedge. A caller cancellation is re-thrown
                // when the primary task is awaited to build the authoritative result.
                return BranchOutcome.Definitive;
            }

            DocumentServiceResponse response = await completedTask;
            if (response == null)
            {
                return BranchOutcome.Definitive;
            }

            if ((int)response.StatusCode < 400)
            {
                return BranchOutcome.Success;
            }

            return IsRegionalFailure(response.StatusCode, response.SubStatusCode)
                ? BranchOutcome.RegionalFailure
                : BranchOutcome.Definitive;
        }

        /// <summary>
        /// Classifies a faulted branch's exception as a regional failure using the same status /
        /// sub-status set as <c>MetadataRequestThrottleRetryPolicy</c>, so metadata hedging and the
        /// metadata retry policy agree on what "the region is at fault" means. Auth failures
        /// (401 / plain 403) are NOT regional, so a misconfigured secondary is treated as a losing
        /// hedge rather than a spurious operation result.
        /// <para>
        /// A bare <see cref="HttpRequestException"/> (connection refused / DNS / TLS reset reaching the
        /// gateway) is also treated as regional: it means the region is unreachable, not that the request
        /// is bad. This mirrors how the data-plane <c>ClientRetryPolicy</c> treats an
        /// <see cref="HttpRequestException"/> as an endpoint failure, so a hard-down primary region becomes
        /// hedgeable and a good hedge can win over it. (A timed-out HTTP attempt does not reach here as an
        /// exception: metadata reads run on the control-plane hot path with <c>ShouldThrow503OnTimeout</c>,
        /// so a timeout already surfaces as a <c>503</c>. A caller cancellation faults the task as
        /// <see cref="TaskStatus.Canceled"/>, which the caller classifies as <c>Definitive</c> before this
        /// method runs.)
        /// </para>
        /// </summary>
        private static bool IsRegionalFailureException(AggregateException aggregate)
        {
            Exception inner = aggregate?.Flatten().InnerException;
            switch (inner)
            {
                case DocumentClientException dce when dce.StatusCode.HasValue:
                    return IsRegionalFailure(dce.StatusCode.Value, dce.GetSubStatus());
                case CosmosException ce:
                    return IsRegionalFailure(ce.StatusCode, (SubStatusCodes)ce.SubStatusCode);
                case HttpRequestException:
                    // Connection failure to the region's gateway (refused / DNS / TLS) => region unreachable.
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Classifies a response as a regional failure — a signal that the region, not the
        /// request, is at fault, so another region is worth trying.
        /// </summary>
        private static bool IsRegionalFailure(HttpStatusCode statusCode, SubStatusCodes subStatus)
        {
            switch (statusCode)
            {
                case HttpStatusCode.ServiceUnavailable: // 503
                case HttpStatusCode.InternalServerError: // 500
                    return true;
                case HttpStatusCode.Gone: // 410 + LeaseNotFound (partition moved)
                    return subStatus == SubStatusCodes.LeaseNotFound;
                case HttpStatusCode.Forbidden: // 403 + DatabaseAccountNotFound (region unavailable)
                    return subStatus == SubStatusCodes.DatabaseAccountNotFound;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Sends a per-branch clone of the request to the given endpoint. Cloning is
        /// required because the primary and hedge run concurrently and route to different
        /// regions — sharing one <see cref="DocumentServiceRequest"/> would let one branch
        /// overwrite the other's target region. The clone is disposed once its send completes
        /// so per-request resources (headers, body stream, pooled buffers) are not leaked
        /// across hedged sends. The returned <see cref="DocumentServiceResponse"/> is
        /// independent of the request and is disposed separately (by the winner's caller, or
        /// by <see cref="ObserveInBackground"/> for the loser).
        /// </summary>
        private static async Task<DocumentServiceResponse> SendCloneAsync(
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> send,
            DocumentServiceRequest request,
            Uri endpoint,
            CancellationToken cancellationToken)
        {
            using DocumentServiceRequest branchRequest = request.Clone();
            return await send(branchRequest, endpoint, cancellationToken);
        }

        /// <summary>
        /// Observes the losing branch so its exception is never unobserved and its response
        /// body is disposed. The loser's outcome is intentionally discarded — a losing hedge
        /// must never influence the returned result nor reach the caller's retry policy.
        /// </summary>
        private static void ObserveInBackground(Task<DocumentServiceResponse> loser)
        {
            _ = loser.ContinueWith(
                static t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        t.Result?.Dispose();
                    }
                    else
                    {
                        // Touch the exception to mark it observed.
                        _ = t.Exception;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private static async Task SwallowAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Observed; the caller decides the outcome from the task's status.
            }
        }

        private MetadataHedgingResult BuildResult(DocumentServiceResponse response, Uri winningEndpoint, bool hedgeFired, bool hedgeWon)
        {
            return new MetadataHedgingResult(response, winningEndpoint, this.SafeGetLocation(winningEndpoint), hedgeFired, hedgeWon);
        }

        /// <summary>
        /// Materializes the primary's authoritative outcome. Awaiting the primary task rethrows its
        /// exception verbatim if it faulted (or an <see cref="OperationCanceledException"/> if it was
        /// cancelled), so the caller's retry policy classifies the real failure exactly as it would
        /// have without hedging. A successful primary simply yields its response. The primary is
        /// never the hedge winner, so <c>hedgeWon</c> is always <c>false</c> here.
        /// </summary>
        private async Task<MetadataHedgingResult> BuildAuthoritativeResultAsync(
            Task<DocumentServiceResponse> primaryTask,
            Uri primaryEndpoint,
            bool hedgeFired)
        {
            DocumentServiceResponse response = await primaryTask;
            return this.BuildResult(response, primaryEndpoint, hedgeFired, hedgeWon: false);
        }

        private string SafeGetLocation(Uri endpoint)
        {
            if (endpoint == null)
            {
                return null;
            }

            try
            {
                return this.globalEndpointManager.GetLocation(endpoint);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Result of <see cref="ExecuteAsync"/>: the winning response, the endpoint (and region)
        /// that produced it, whether a hedge was dispatched (<see cref="HedgeFired"/>), and whether
        /// the hedge's response is the one being returned (<see cref="HedgeWon"/>).
        /// </summary>
        internal readonly struct MetadataHedgingResult
        {
            public MetadataHedgingResult(DocumentServiceResponse response, Uri winningEndpoint, string winningRegion, bool hedgeFired, bool hedgeWon)
            {
                this.Response = response;
                this.WinningEndpoint = winningEndpoint;
                this.WinningRegion = winningRegion;
                this.HedgeFired = hedgeFired;
                this.HedgeWon = hedgeWon;
            }

            public DocumentServiceResponse Response { get; }

            public Uri WinningEndpoint { get; }

            public string WinningRegion { get; }

            /// <summary>Whether a hedge request was dispatched (a latency / telemetry signal).</summary>
            public bool HedgeFired { get; }

            /// <summary>
            /// Whether the hedge's response is the one being returned — i.e. the winning region
            /// differs from the primary. Only when this is <c>true</c> must the caller pin any
            /// follow-on paged reads to <see cref="WinningEndpoint"/> for continuation consistency.
            /// </summary>
            public bool HedgeWon { get; }
        }
    }
}
