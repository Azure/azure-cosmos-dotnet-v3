//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Bounded cross-region hedging for metadata cache reads (both cold-start
    /// first-population and steady-state refresh reads). One instance per
    /// <see cref="CosmosClient"/>. Consumed by <c>ClientCollectionCache</c>
    /// (<c>Collection</c> <c>Read</c>) and <c>PartitionKeyRangeCache</c>
    /// (<c>PartitionKeyRange</c> <c>ReadFeed</c>, first page only).
    /// </summary>
    /// <remarks>
    /// Design: <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c>. Hedging is
    /// restricted to the supported metadata request types but is NOT limited to
    /// cold start; refresh reads are hedged on the same terms. The historical
    /// "ColdStart" tokens in the env var / opt-in / design-doc names are retained
    /// for the broader feature.
    /// </remarks>
    internal sealed class MetadataHedgingStrategy : IDisposable
    {
        internal const string TraceDatumKey = "Metadata Hedge Context";

        /// <summary>
        /// Per-client concurrency budget for in-flight metadata hedges
        /// (design §5.11). Not customer-configurable.
        /// </summary>
        internal const int DefaultPerClientConcurrencyBudget = 8;

        /// <summary>
        /// Added to <c>HttpTimeoutPolicy.FirstAttemptTimeout</c> to derive the
        /// fixed hedge threshold (today 1&#160;s + 500&#160;ms = 1.5&#160;s).
        /// Not customer-configurable. See design §5.1 / §5.9.
        /// </summary>
        internal static readonly TimeSpan DefaultThresholdStep = TimeSpan.FromMilliseconds(500);

        private readonly IGlobalEndpointManager globalEndpointManager;
        private readonly Func<bool> isHedgingDisabledByGateway;
        private readonly Func<bool> isPpafEnabled;
        private readonly bool? customerOptIn;
        private readonly TimeSpan threshold;
        private readonly SemaphoreSlim hedgeBudget;
        private readonly int perClientConcurrencyBudget;
        private bool disposed;

        public MetadataHedgingStrategy(
            IGlobalEndpointManager globalEndpointManager,
            Func<bool> isHedgingDisabledByGateway,
            Func<bool> isPpafEnabled,
            bool? customerOptIn,
            TimeSpan threshold,
            int perClientConcurrencyBudget = DefaultPerClientConcurrencyBudget)
        {
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
            this.isHedgingDisabledByGateway = isHedgingDisabledByGateway ?? (() => false);
            this.isPpafEnabled = isPpafEnabled ?? (() => false);
            this.customerOptIn = customerOptIn;

            if (threshold <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold));
            }

            this.threshold = threshold;
            this.perClientConcurrencyBudget = Math.Max(1, perClientConcurrencyBudget);
            this.hedgeBudget = new SemaphoreSlim(this.perClientConcurrencyBudget, this.perClientConcurrencyBudget);
        }

        internal TimeSpan Threshold => this.threshold;

        internal int PerClientConcurrencyBudget => this.perClientConcurrencyBudget;

        /// <summary>
        /// Resolves the tri-state metadata hedging opt-in (resolved
        /// from the <c>AZURE_COSMOS_METADATA_HEDGING_ENABLED</c>
        /// environment variable) to a concrete opt-in <see cref="bool"/>. When
        /// the opt-in is left <c>null</c>, metadata hedging follows the
        /// account's PPAF (Per-Partition Automatic Failover) state — enabled by
        /// default when PPAF is enabled, disabled otherwise. An explicit
        /// <c>true</c> enables hedging even when PPAF is disabled, and an
        /// explicit <c>false</c> disables it regardless of PPAF — see design
        /// §5.1. (The env-var name retains the historical "COLDSTART" token; the
        /// feature now covers refresh reads too.)
        /// </summary>
        internal static bool ResolveOptIn(bool? customerOptIn, bool isPpafEnabled)
        {
            return customerOptIn ?? isPpafEnabled;
        }

        /// <summary>
        /// Builds the strategy from the resolved metadata hedging
        /// tri-state opt-in (from the
        /// <c>AZURE_COSMOS_METADATA_HEDGING_ENABLED</c> environment
        /// variable). Returns <c>null</c> only when hedging is explicitly
        /// disabled (<c>false</c>). When the opt-in is <c>true</c> or
        /// left <c>null</c> the strategy is created and the per-request
        /// eligibility check resolves the effective opt-in against the live PPAF
        /// state (a <c>null</c> opt-in follows PPAF; an explicit <c>true</c>
        /// enables hedging even when PPAF is disabled). The hedge threshold is a
        /// fixed SDK-derived value (<c>FirstAttemptTimeout + 500&#160;ms</c>,
        /// today 1.5&#160;s) and is not customer-configurable. The Gateway
        /// kill-switch is hard-wired to <c>false</c> in Phase 1; see design §5.1.
        /// </summary>
        internal static MetadataHedgingStrategy CreateIfEnabled(
            bool? enableMetadataHedgingForColdStart,
            IGlobalEndpointManager globalEndpointManager,
            Func<bool> isPpafEnabled)
        {
            // Explicit customer kill-switch: hedging is suppressed regardless of PPAF.
            if (enableMetadataHedgingForColdStart == false)
            {
                return null;
            }

            // Fixed, SDK-derived threshold. Not customer-configurable so the
            // 1.5s (FirstAttemptTimeout + 500ms) contract is always honored.
            TimeSpan threshold = HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout
                + DefaultThresholdStep;

            return new MetadataHedgingStrategy(
                globalEndpointManager: globalEndpointManager,
                isHedgingDisabledByGateway: () => false,
                isPpafEnabled: isPpafEnabled,
                customerOptIn: enableMetadataHedgingForColdStart,
                threshold: threshold,
                perClientConcurrencyBudget: DefaultPerClientConcurrencyBudget);
        }

        internal int AvailableBudget => this.hedgeBudget.CurrentCount;

        /// <summary>
        /// Evaluate hedging eligibility for a single send attempt. Does NOT
        /// touch the concurrency semaphore — see design §6 rule 7.
        /// </summary>
        public MetadataHedgeEligibility EvaluateEligibility(
            DocumentServiceRequest request,
            MetadataHedgingContext hedgeContext)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (hedgeContext == null)
            {
                throw new ArgumentNullException(nameof(hedgeContext));
            }

            // A live strategy is only constructed when hedging is enabled
            // (see CreateIfEnabled), so customerOptIn is never false here in
            // production — this guard is defense-in-depth for direct (test) construction.
            if (this.customerOptIn == false)
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.OptInDisabled);
            }

            if (this.isHedgingDisabledByGateway())
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.GatewayKillSwitchOn);
            }

            // When the customer leaves the opt-in null, metadata hedging follows the
            // live PPAF state. An explicit opt-in of true bypasses this gate.
            if (!ResolveOptIn(this.customerOptIn, this.isPpafEnabled()))
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.PpafDisabled);
            }

            // Hedging is intentionally NOT gated on cold start. Both the
            // first-population (cold-start) read and steady-state refresh reads of
            // the two metadata caches are eligible — the request-type restriction
            // below (IsSupportedResource) is what keeps the surface to metadata
            // reads. hedgeContext.IsColdStart is retained for diagnostics only.
            if (hedgeContext.HasHedgedThisOperation)
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.AlreadyHedgedThisOperation);
            }

            if (!IsSupportedResource(request))
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.ResourceTypeNotSupported);
            }

            if (request.ResourceType == ResourceType.PartitionKeyRange && !hedgeContext.IsFirstReadFeedPage)
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.NotFirstReadFeedPage);
            }

            if (this.globalEndpointManager.ReadEndpoints == null || this.globalEndpointManager.ReadEndpoints.Count <= 1)
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.SingleRegion);
            }

            ReadOnlyCollection<Uri> applicable = this.globalEndpointManager.GetApplicableEndpoints(request, isReadRequest: true);
            if (applicable == null || applicable.Count <= 1)
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.ExcludedRegionLeavesNoTarget);
            }

            return MetadataHedgeEligibility.Eligible();
        }

        /// <summary>
        /// Execute a single metadata send with optional cross-region hedging.
        /// The caller supplies a region-targeted send delegate that uses the
        /// supplied target <see cref="Uri"/> to route the request — the
        /// strategy itself never mutates <c>request.RequestContext</c>.
        /// </summary>
        public async Task<MetadataHedgingResult> ExecuteAsync(
            DocumentServiceRequest request,
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> sendToEndpoint,
            MetadataHedgingContext hedgeContext,
            ITrace trace,
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

            if (hedgeContext == null)
            {
                throw new ArgumentNullException(nameof(hedgeContext));
            }

            MetadataHedgeDiagnostics diag = new MetadataHedgeDiagnostics
            {
                ResourceType = request.ResourceType.ToString(),
                ThresholdMs = this.threshold.TotalMilliseconds,
            };

            // ---- 1. Eligibility ----
            MetadataHedgeEligibility eligibility = this.EvaluateEligibility(request, hedgeContext);
            diag.Eligible = eligibility.IsEligible;
            diag.SkipReason = eligibility.SkipReason;

            Uri primaryEndpoint = request.RequestContext?.LocationEndpointToRoute
                                  ?? this.globalEndpointManager.ResolveServiceEndpoint(request);
            diag.PrimaryRegion = this.SafeGetLocation(primaryEndpoint);

            if (!eligibility.IsEligible)
            {
                CosmosDbEventSource.MetadataHedgeSkipped(diag.SkipReason.ToString(), diag.ResourceType);
                return await this.PrimaryOnlyAsync(request, primaryEndpoint, sendToEndpoint, hedgeContext, diag, trace, cancellationToken);
            }

            // ---- 2. Concurrency budget — true non-blocking sync check ----
            if (!this.hedgeBudget.Wait(TimeSpan.Zero))
            {
                diag.Eligible = false;
                diag.SkipReason = MetadataHedgeSkipReason.BudgetExhausted;
                MetadataHedgingMeter.RecordBudgetExhausted(diag.ResourceType);
                CosmosDbEventSource.MetadataHedgeSkipped(diag.SkipReason.ToString(), diag.ResourceType);
                return await this.PrimaryOnlyAsync(request, primaryEndpoint, sendToEndpoint, hedgeContext, diag, trace, cancellationToken);
            }

            // ---- 3. Resolve hedge endpoint ----
            CancellationTokenSource primaryCts = null;
            CancellationTokenSource hedgeCts = null;
            CancellationTokenSource timerCts = null;
            bool budgetReleased = false;

            try
            {
                // Endpoint resolution runs inside the try so that a throw from
                // GetApplicableEndpoints (e.g. a concurrent location-cache refresh/failover) or a
                // null result runs the finally and releases the budget permit acquired above,
                // instead of leaking it for the lifetime of the CosmosClient.
                ReadOnlyCollection<Uri> applicable = this.globalEndpointManager.GetApplicableEndpoints(request, isReadRequest: true);
                Uri hedgeEndpoint = applicable?.FirstOrDefault(u => u != null && !u.Equals(primaryEndpoint));

                if (hedgeEndpoint == null)
                {
                    // PrimaryOnlyAsync is awaited inside the try; the finally below releases the permit.
                    diag.Eligible = false;
                    diag.SkipReason = MetadataHedgeSkipReason.SingleRegion;
                    CosmosDbEventSource.MetadataHedgeSkipped(diag.SkipReason.ToString(), diag.ResourceType);
                    return await this.PrimaryOnlyAsync(request, primaryEndpoint, sendToEndpoint, hedgeContext, diag, trace, cancellationToken);
                }

                diag.HedgeRegion = this.SafeGetLocation(hedgeEndpoint);

                primaryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                hedgeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                hedgeContext.AttemptedEndpoints.TryAdd(primaryEndpoint.AbsoluteUri, 0);

                Stopwatch sw = Stopwatch.StartNew();
                Task<DocumentServiceResponse> primaryTask = SendOneAsync(sendToEndpoint, request, primaryEndpoint, primaryCts.Token);
                Task hedgeTimer = Task.Delay(this.threshold, timerCts.Token);

                Task firstCompleted = await Task.WhenAny(primaryTask, hedgeTimer);

                // ---- 5a. Primary genuinely won before threshold ----
                if (firstCompleted == primaryTask
                    && primaryTask.Status == TaskStatus.RanToCompletion
                    && IsAcceptableWinner(primaryTask.Result, HedgeBranch.Primary))
                {
                    timerCts.Cancel();
                    diag.TotalAttempts = 1;
                    diag.WinningRegion = diag.PrimaryRegion;
                    hedgeContext.RecordWinner(primaryEndpoint);
                    trace?.AddDatum(TraceDatumKey, diag);
                    return new MetadataHedgingResult(primaryTask.Result, primaryEndpoint, diag.PrimaryRegion, hedgeFired: false, diag);
                }

                // ---- 5b. Threshold elapsed (or primary faulted): re-check kill-switch ----
                if (this.isHedgingDisabledByGateway())
                {
                    diag.Eligible = false;
                    diag.SkipReason = MetadataHedgeSkipReason.GatewayKillSwitchOn;
                    diag.TotalAttempts = 1;
                    diag.WinningRegion = diag.PrimaryRegion;
                    CosmosDbEventSource.MetadataHedgeSkipped(diag.SkipReason.ToString(), diag.ResourceType);
                    DocumentServiceResponse primaryLate = await ObserveWinningTaskAsync(primaryTask);
                    hedgeContext.RecordWinner(primaryEndpoint);
                    trace?.AddDatum(TraceDatumKey, diag);
                    return new MetadataHedgingResult(primaryLate, primaryEndpoint, diag.PrimaryRegion, hedgeFired: false, diag);
                }

                // ---- 6. Dispatch hedge ----
                hedgeContext.AttemptedEndpoints.TryAdd(hedgeEndpoint.AbsoluteUri, 0);
                hedgeContext.TryMarkHedgedThisOperation();
                diag.HedgeFiredElapsedMs = sw.Elapsed.TotalMilliseconds;
                MetadataHedgingMeter.RecordFire(diag.PrimaryRegion, diag.HedgeRegion, diag.HedgeFiredElapsedMs.Value);
                CosmosDbEventSource.MetadataHedgeFired(diag.PrimaryRegion, diag.HedgeRegion, diag.HedgeFiredElapsedMs.Value);
                Task<DocumentServiceResponse> hedgeTask = SendOneAsync(sendToEndpoint, request, hedgeEndpoint, hedgeCts.Token);

                // ---- 7. Wait for first ACCEPTABLE winner ----
                Task<DocumentServiceResponse>[] remaining = new[] { primaryTask, hedgeTask };
                Task<DocumentServiceResponse> winner = null;
                Task<DocumentServiceResponse> loser = null;

                while (true)
                {
                    Task<DocumentServiceResponse> finished = await Task.WhenAny(remaining);
                    HedgeBranch branch = (finished == primaryTask) ? HedgeBranch.Primary : HedgeBranch.Hedge;

                    if (finished.Status == TaskStatus.RanToCompletion
                        && IsAcceptableWinner(finished.Result, branch))
                    {
                        winner = finished;
                        loser = (finished == primaryTask) ? hedgeTask : primaryTask;
                        if (branch == HedgeBranch.Hedge && diag.HedgeOutcome == null)
                        {
                            diag.HedgeOutcome = "Won";
                        }

                        break;
                    }

                    if (branch == HedgeBranch.Hedge)
                    {
                        // Classify a cross-region auth reject from BOTH a completed task whose
                        // DocumentServiceResponse carries 401/403 AND a faulted task whose thrown
                        // DocumentClientException carries 401/403 (the GatewayStoreModel path), so the
                        // auth signal is never lost just because the store model threw instead of returned.
                        // finished.Result is only read on the RanToCompletion branch (the task is known
                        // complete here), so it never blocks.
                        DocumentServiceResponse hedgeResponse = finished.Status == TaskStatus.RanToCompletion
                            ? finished.Result
                            : null;
                        HttpStatusCode? authStatus = TryGetHedgeAuthRejectStatus(hedgeResponse, finished.Exception);
                        if (authStatus.HasValue)
                        {
                            diag.HedgeOutcome = authStatus.Value == HttpStatusCode.Unauthorized
                                ? "Auth401"
                                : "Auth403";
                            MetadataHedgingMeter.RecordHedgeAuthReject(diag.HedgeRegion, (int)authStatus.Value);
                            CosmosDbEventSource.MetadataHedgeAuthReject(diag.HedgeRegion, (int)authStatus.Value);
                        }
                    }

                    if (remaining.Length == 1)
                    {
                        // Both branches settled without an acceptable winner. Prefer the primary's
                        // outcome so MetadataRequestThrottleRetryPolicy classifies the failure
                        // normally — see design §10.
                        winner = primaryTask.IsCompleted ? primaryTask : hedgeTask;
                        loser = (winner == primaryTask) ? hedgeTask : primaryTask;
                        break;
                    }

                    remaining = (finished == primaryTask)
                        ? new[] { hedgeTask }
                        : new[] { primaryTask };
                }

                Uri winningEndpoint = (winner == primaryTask) ? primaryEndpoint : hedgeEndpoint;
                string winningRegion = (winner == primaryTask) ? diag.PrimaryRegion : diag.HedgeRegion;

                diag.WinningRegion = winningRegion;
                diag.TotalAttempts = 2;
                hedgeContext.RecordWinner(winningEndpoint);

                double totalElapsedMs = sw.Elapsed.TotalMilliseconds;
                if (winner == primaryTask)
                {
                    CosmosDbEventSource.MetadataHedgePrimaryWon(diag.PrimaryRegion, totalElapsedMs, hedgeFired: true);
                }
                else
                {
                    MetadataHedgingMeter.RecordHedgeWin(diag.HedgeRegion);
                    CosmosDbEventSource.MetadataHedgeWon(diag.HedgeRegion, totalElapsedMs);
                }

                // Transfer ownership of the loser CTS to BackgroundCleanupAsync; null out
                // the local ref so the outer finally does not double-dispose.
                CancellationTokenSource loserCts;
                if (loser == primaryTask)
                {
                    loserCts = primaryCts;
                    primaryCts = null;
                }
                else
                {
                    loserCts = hedgeCts;
                    hedgeCts = null;
                }

                loserCts.Cancel();

                string loserRegion = (loser == primaryTask) ? diag.PrimaryRegion : diag.HedgeRegion;

                // Seed a non-null placeholder so a trace serialized before the
                // background cleanup completes reads "Pending" rather than null.
                diag.LoserOutcome = "Pending";

                // Fire-and-forget cleanup. Awaits the loser, disposes its response body
                // and CTS, and updates diag.LoserOutcome. Swallows OCE and any other
                // loser-thrown exception — see design §5.7.1.
                _ = BackgroundCleanupAsync(loser, loserCts, diag, this.hedgeBudget, loserRegion);
                budgetReleased = true;       // cleanup releases the budget

                trace?.AddDatum(TraceDatumKey, diag);

                DocumentServiceResponse winningResponse = await ObserveWinningTaskAsync(winner);
                return new MetadataHedgingResult(winningResponse, winningEndpoint, winningRegion, hedgeFired: true, diag);
            }
            finally
            {
                primaryCts?.Dispose();
                hedgeCts?.Dispose();
                timerCts?.Dispose();
                if (!budgetReleased)
                {
                    this.hedgeBudget.Release();
                }
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.hedgeBudget.Dispose();
        }

        /// <summary>
        /// Builds the region-targeted send delegate consumed by
        /// <see cref="ExecuteAsync"/>: route the (already-cloned) request to the
        /// supplied target endpoint, then dispatch it through the store model.
        /// Shared by <c>ClientCollectionCache</c> and <c>PartitionKeyRangeCache</c>
        /// so the safety-critical region-routing logic lives in exactly one place.
        /// </summary>
        internal static Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> StoreModelSender(
            IStoreModel storeModel)
        {
            return (request, targetEndpoint, ct) =>
            {
                if (targetEndpoint != null)
                {
                    request.RequestContext.RouteToLocation(targetEndpoint);
                }

                return storeModel.ProcessMessageAsync(request);
            };
        }

        /// <summary>
        /// Per-branch acceptable-winner predicate composed over
        /// <see cref="MetadataRegionalFailureClassifier.IsRegionalFailure"/>. A 401 / plain 403
        /// from the hedge branch is rejected — see design §5.13.
        /// </summary>
        internal static bool IsAcceptableWinner(DocumentServiceResponse response, HedgeBranch branch)
        {
            if (response == null)
            {
                return false;
            }

            if (MetadataRegionalFailureClassifier.IsRegionalFailure(
                statusCode: response.StatusCode,
                subStatus: response.SubStatusCode,
                exception: null,
                callerToken: CancellationToken.None))
            {
                return false;
            }

            if (branch == HedgeBranch.Hedge
                && (response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Classifies a settled hedge branch as a cross-region auth reject (401 / plain 403).
        /// Handles BOTH a completed branch whose <paramref name="response"/> carries the status AND
        /// a faulted branch whose thrown <see cref="DocumentClientException"/> (surfaced via
        /// <paramref name="faultException"/>) carries the status — the GatewayStoreModel path throws
        /// 401/403 rather than returning a response. Returns <c>null</c> when neither applies.
        /// See design §5.13.
        /// </summary>
        internal static HttpStatusCode? TryGetHedgeAuthRejectStatus(
            DocumentServiceResponse response,
            AggregateException faultException)
        {
            HttpStatusCode? status = response?.StatusCode;
            if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden)
            {
                return status;
            }

            if (faultException != null)
            {
                foreach (Exception inner in faultException.InnerExceptions)
                {
                    if (inner is DocumentClientException dce
                        && (dce.StatusCode == HttpStatusCode.Unauthorized
                            || dce.StatusCode == HttpStatusCode.Forbidden))
                    {
                        return dce.StatusCode;
                    }
                }
            }

            return null;
        }

        private static bool IsSupportedResource(DocumentServiceRequest request)
        {
            return (request.ResourceType == ResourceType.Collection && request.OperationType == OperationType.Read)
                || (request.ResourceType == ResourceType.PartitionKeyRange && request.OperationType == OperationType.ReadFeed);
        }

        /// <summary>
        /// Middle-layer seam — see design §5.12 for the net472 stack-unwind
        /// discipline that requires the <c>await Task.Yield()</c> on the
        /// rethrow path.
        /// </summary>
        private static async Task<DocumentServiceResponse> SendOneAsync(
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> send,
            DocumentServiceRequest request,
            Uri targetEndpoint,
            CancellationToken ct)
        {
            // Clone per branch. The primary and hedge sends run concurrently and the
            // caller's send delegate routes via RouteToLocation; sharing one
            // DocumentServiceRequest would let one branch overwrite the other's target
            // region (no hedge benefit + corrupted region telemetry). DocumentServiceRequest.Clone
            // produces an independent RequestContext. Supported metadata reads are
            // body-less GET / ReadFeed, so the clone owns no stream to dispose.
            DocumentServiceRequest branchRequest = request.Clone();
            try
            {
                return await send(branchRequest, targetEndpoint, ct).ConfigureAwait(false);
            }
            catch
            {
                await Task.Yield();
                throw;
            }
        }

        private static async Task<DocumentServiceResponse> ObserveWinningTaskAsync(Task<DocumentServiceResponse> winner)
        {
            try
            {
                return await winner.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
                throw;
            }
        }

        private static async Task BackgroundCleanupAsync(
            Task<DocumentServiceResponse> loser,
            CancellationTokenSource loserCts,
            MetadataHedgeDiagnostics diag,
            SemaphoreSlim hedgeBudget,
            string loserRegion)
        {
            try
            {
                DocumentServiceResponse loserResp = await loser.ConfigureAwait(false);
                try
                {
                    loserResp?.Dispose();
                }
                catch
                {
                    // never let response-body dispose escape cleanup
                }

                diag.LoserOutcome = "CompletedAfterWinner";
                MetadataHedgingMeter.RecordLateLoser(loserRegion, diag.LoserOutcome);
            }
            catch (OperationCanceledException)
            {
                diag.LoserOutcome = "Cancelled";
            }
            catch (Exception ex)
            {
                diag.LoserOutcome = $"Faulted({ex.GetType().Name})";
                MetadataHedgingMeter.RecordLateLoser(loserRegion, diag.LoserOutcome);
            }
            finally
            {
                try
                {
                    loserCts.Dispose();
                }
                catch
                {
                    // CTS double-dispose is safe; this is a belt-and-braces guard.
                }

                try
                {
                    hedgeBudget.Release();
                }
                catch
                {
                    // SemaphoreSlim disposed during shutdown is the only realistic source.
                }
            }
        }

        private async Task<MetadataHedgingResult> PrimaryOnlyAsync(
            DocumentServiceRequest request,
            Uri primaryEndpoint,
            Func<DocumentServiceRequest, Uri, CancellationToken, Task<DocumentServiceResponse>> sendToEndpoint,
            MetadataHedgingContext hedgeContext,
            MetadataHedgeDiagnostics diag,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            DocumentServiceResponse response = await sendToEndpoint(request, primaryEndpoint, cancellationToken);
            diag.TotalAttempts = 1;
            diag.WinningRegion = diag.PrimaryRegion;
            hedgeContext.RecordWinner(primaryEndpoint);
            trace?.AddDatum(TraceDatumKey, diag);
            return new MetadataHedgingResult(response, primaryEndpoint, diag.PrimaryRegion, hedgeFired: false, diag);
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
        /// Reason a metadata hedge was not dispatched. Recorded in
        /// <see cref="MetadataHedgeDiagnostics"/> for supportability.
        /// </summary>
        internal enum MetadataHedgeSkipReason
        {
            None,
            OptInDisabled,
            PpafDisabled,
            GatewayKillSwitchOn,
            SingleRegion,

            // Retained for wire/diagnostic compatibility. Hedging is no longer
            // gated on cold start, so EvaluateEligibility never produces this
            // value; refresh reads are eligible on the same terms as cold start.
            NotColdStart,
            ResourceTypeNotSupported,
            NotFirstReadFeedPage,
            BudgetExhausted,
            AlreadyHedgedThisOperation,
            ExcludedRegionLeavesNoTarget,
            AuthModeNotEligibleForHedge,
        }

        /// <summary>
        /// Identifies the branch (primary or hedge) that produced a candidate
        /// metadata-hedge winner. Used to compose the per-branch overlay in
        /// <see cref="IsAcceptableWinner"/>.
        /// </summary>
        internal enum HedgeBranch
        {
            Primary,
            Hedge,
        }

        /// <summary>
        /// Output of <see cref="EvaluateEligibility"/>.
        /// </summary>
        internal readonly struct MetadataHedgeEligibility
        {
            public bool IsEligible { get; }

            public MetadataHedgeSkipReason SkipReason { get; }

            public MetadataHedgeEligibility(bool isEligible, MetadataHedgeSkipReason skipReason)
            {
                this.IsEligible = isEligible;
                this.SkipReason = skipReason;
            }

            public static MetadataHedgeEligibility Eligible() => new MetadataHedgeEligibility(true, MetadataHedgeSkipReason.None);

            public static MetadataHedgeEligibility Skip(MetadataHedgeSkipReason reason) => new MetadataHedgeEligibility(false, reason);
        }

        /// <summary>
        /// Result of <see cref="ExecuteAsync"/>.
        /// </summary>
        internal readonly struct MetadataHedgingResult
        {
            public DocumentServiceResponse Response { get; }

            public Uri WinningEndpoint { get; }

            public string WinningRegion { get; }

            public bool HedgeFired { get; }

            public MetadataHedgeDiagnostics Diagnostics { get; }

            public MetadataHedgingResult(
                DocumentServiceResponse response,
                Uri winningEndpoint,
                string winningRegion,
                bool hedgeFired,
                MetadataHedgeDiagnostics diagnostics)
            {
                this.Response = response;
                this.WinningEndpoint = winningEndpoint;
                this.WinningRegion = winningRegion;
                this.HedgeFired = hedgeFired;
                this.Diagnostics = diagnostics;
            }
        }

        /// <summary>
        /// Diagnostic record attached to the request's trace. Fields populated by
        /// the orchestration thread for the eligibility / winner outcome;
        /// <see cref="LoserOutcome"/> / <see cref="HedgeOutcome"/> may be updated
        /// later from the background-cleanup continuation and are read/written via
        /// <see cref="Volatile"/>.
        /// </summary>
        internal sealed class MetadataHedgeDiagnostics
        {
            private string hedgeOutcome;
            private string loserOutcome;

            public bool Eligible { get; set; }

            public MetadataHedgeSkipReason SkipReason { get; set; }

            public string ResourceType { get; set; }

            public string PrimaryRegion { get; set; }

            public string HedgeRegion { get; set; }

            public double ThresholdMs { get; set; }

            public double? HedgeFiredElapsedMs { get; set; }

            public string WinningRegion { get; set; }

            public int TotalAttempts { get; set; }

            public string HedgeOutcome
            {
                get => Volatile.Read(ref this.hedgeOutcome);
                set => Volatile.Write(ref this.hedgeOutcome, value);
            }

            public string LoserOutcome
            {
                get => Volatile.Read(ref this.loserOutcome);
                set => Volatile.Write(ref this.loserOutcome, value);
            }
        }

        /// <summary>
        /// Per-logical-operation context shared between
        /// <see cref="MetadataHedgingStrategy"/> and
        /// <c>MetadataRequestThrottleRetryPolicy</c>. Carries the cold-start signal
        /// (diagnostics only — hedging is not gated on it), the dedupe set, the
        /// winner, the &quot;hedged this operation&quot; latch, and the first-page
        /// flag for PK-range pagination. See
        /// <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §5.2 / §6.1.
        /// </summary>
        internal sealed class MetadataHedgingContext
        {
            private Uri winningEndpoint;
            private int hasHedgedThisOperation;

            /// <summary>
            /// Whether this read is the first-population (cold-start) read of the
            /// cache. Recorded for diagnostics/telemetry only; it does NOT gate
            /// hedging eligibility — refresh reads hedge on the same terms.
            /// </summary>
            public bool IsColdStart { get; set; }

            public bool IsFirstReadFeedPage { get; set; } = true;

            public ConcurrentDictionary<string, byte> AttemptedEndpoints { get; }
                = new ConcurrentDictionary<string, byte>();

            public Uri WinningEndpoint => Volatile.Read(ref this.winningEndpoint);

            public bool HasHedgedThisOperation => Volatile.Read(ref this.hasHedgedThisOperation) == 1;

            /// <summary>
            /// Single-publication of the winning endpoint. Late loser continuations
            /// that try to re-publish observe a non-null existing value and leave it
            /// intact.
            /// </summary>
            internal void RecordWinner(Uri endpoint)
            {
                Interlocked.CompareExchange(ref this.winningEndpoint, endpoint, null);
            }

            /// <summary>
            /// Returns <c>true</c> if this caller is the first to mark the operation
            /// as having dispatched a hedge. Subsequent callers (across
            /// <c>BackoffRetryUtility</c> retries) observe <c>false</c> and skip.
            /// </summary>
            internal bool TryMarkHedgedThisOperation()
                => Interlocked.Exchange(ref this.hasHedgedThisOperation, 1) == 0;
        }
    }
}
