//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
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
        private readonly bool? customerOptIn;
        private readonly TimeSpan threshold;

        public MetadataHedgingStrategy(
            IGlobalEndpointManager globalEndpointManager,
            Func<bool> isPpafEnabled,
            bool? customerOptIn,
            TimeSpan threshold)
        {
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
            this.isPpafEnabled = isPpafEnabled ?? (() => false);
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
        /// live PPAF state (<c>null</c> follows PPAF; <c>true</c> forces on).
        /// </summary>
        internal static MetadataHedgingStrategy CreateIfEnabled(
            bool? enableMetadataHedging,
            IGlobalEndpointManager globalEndpointManager,
            Func<bool> isPpafEnabled)
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
                threshold: threshold);
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
                return this.BuildResult(primaryOnly, primaryEndpoint, hedgeFired: false);
            }

            // ---- 1. Start primary + a threshold timer. ----
            // timerCts is intentionally NOT linked to the caller token: a user
            // cancellation must not complete the timer and be misread as "threshold
            // elapsed" (which would dispatch a phantom hedge). Caller cancellation
            // flows through the send delegate's token instead.
            using CancellationTokenSource timerCts = new CancellationTokenSource();
            Task<DocumentServiceResponse> primaryTask = SendCloneAsync(sendToEndpoint, request, primaryEndpoint, cancellationToken);
            Task timerTask = Task.Delay(this.threshold, timerCts.Token);

            // ---- 2. Primary finished (well) before the threshold → done, no hedge. ----
            Task firstToComplete = await Task.WhenAny(primaryTask, timerTask);
            if (firstToComplete == primaryTask && primaryTask.Status == TaskStatus.RanToCompletion)
            {
                DocumentServiceResponse primaryResponse = await primaryTask;
                if (IsGoodResponse(primaryResponse, isHedge: false))
                {
                    timerCts.Cancel();
                    return this.BuildResult(primaryResponse, primaryEndpoint, hedgeFired: false);
                }
            }

            // ---- 3. Primary is slow (or already failing) → fire one hedge. ----
            timerCts.Cancel();
            Task<DocumentServiceResponse> hedgeTask = SendCloneAsync(sendToEndpoint, request, hedgeEndpoint, cancellationToken);

            // ---- 4. Return the first GOOD answer; the primary stays authoritative. ----
            Task<DocumentServiceResponse> firstSettled = await Task.WhenAny(primaryTask, hedgeTask);
            bool firstIsHedge = firstSettled == hedgeTask;
            if (firstSettled.Status == TaskStatus.RanToCompletion)
            {
                DocumentServiceResponse firstResponse = await firstSettled;
                if (IsGoodResponse(firstResponse, firstIsHedge))
                {
                    ObserveInBackground(firstIsHedge ? primaryTask : hedgeTask);
                    return this.BuildResult(
                        firstResponse,
                        firstIsHedge ? hedgeEndpoint : primaryEndpoint,
                        hedgeFired: true);
                }
            }

            // First was not good — wait for the other branch, then decide.
            Task<DocumentServiceResponse> other = firstIsHedge ? primaryTask : hedgeTask;
            await SwallowAsync(other);
            bool otherIsHedge = !firstIsHedge;
            if (other.Status == TaskStatus.RanToCompletion)
            {
                DocumentServiceResponse otherResponse = await other;
                if (IsGoodResponse(otherResponse, otherIsHedge))
                {
                    ObserveInBackground(firstSettled);
                    return this.BuildResult(
                        otherResponse,
                        otherIsHedge ? hedgeEndpoint : primaryEndpoint,
                        hedgeFired: true);
                }
            }

            // Neither branch was good → return the PRIMARY's outcome (authoritative).
            // Awaiting the primary re-throws its exception if it faulted, so the
            // caller's retry policy classifies the real failure normally.
            ObserveInBackground(hedgeTask);
            DocumentServiceResponse primaryOutcome = await primaryTask;
            return this.BuildResult(primaryOutcome, primaryEndpoint, hedgeFired: true);
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

            // Need at least two read regions to hedge across.
            ReadOnlyCollection<Uri> readEndpoints = this.globalEndpointManager.ReadEndpoints;
            if (readEndpoints == null || readEndpoints.Count <= 1)
            {
                return false;
            }

            // Choose a hedge endpoint distinct from the primary, respecting ExcludeRegions.
            ReadOnlyCollection<Uri> applicable = this.globalEndpointManager.GetApplicableEndpoints(request, isReadRequest: true);
            hedgeEndpoint = applicable?.FirstOrDefault(u => u != null && !u.Equals(primaryEndpoint));
            return hedgeEndpoint != null;
        }

        private static bool IsSupportedResource(DocumentServiceRequest request)
        {
            return (request.ResourceType == ResourceType.Collection && request.OperationType == OperationType.Read)
                || (request.ResourceType == ResourceType.PartitionKeyRange && request.OperationType == OperationType.ReadFeed);
        }

        /// <summary>
        /// A response is a &quot;good&quot; winner only if it is not a regional failure. For the
        /// hedge branch, cross-region auth failures (401 / plain 403) are additionally rejected
        /// so a misconfigured secondary (e.g. region-scoped RBAC) can never surface a spurious
        /// error as the operation result.
        /// </summary>
        private static bool IsGoodResponse(DocumentServiceResponse response, bool isHedge)
        {
            if (response == null)
            {
                return false;
            }

            if (isHedge
                && (response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden))
            {
                return false;
            }

            return !IsRegionalFailure(response.StatusCode, response.SubStatusCode);
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
        /// overwrite the other's target region.
        /// </summary>
        private static Task<DocumentServiceResponse> SendCloneAsync(
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> send,
            DocumentServiceRequest request,
            Uri endpoint,
            CancellationToken cancellationToken)
        {
            DocumentServiceRequest branchRequest = request.Clone();
            return send(branchRequest, endpoint, cancellationToken);
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

        private MetadataHedgingResult BuildResult(DocumentServiceResponse response, Uri winningEndpoint, bool hedgeFired)
        {
            return new MetadataHedgingResult(response, winningEndpoint, this.SafeGetLocation(winningEndpoint), hedgeFired);
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
        /// that produced it, and whether a hedge was dispatched.
        /// </summary>
        internal readonly struct MetadataHedgingResult
        {
            public MetadataHedgingResult(DocumentServiceResponse response, Uri winningEndpoint, string winningRegion, bool hedgeFired)
            {
                this.Response = response;
                this.WinningEndpoint = winningEndpoint;
                this.WinningRegion = winningRegion;
                this.HedgeFired = hedgeFired;
            }

            public DocumentServiceResponse Response { get; }

            public Uri WinningEndpoint { get; }

            public string WinningRegion { get; }

            public bool HedgeFired { get; }
        }
    }
}
