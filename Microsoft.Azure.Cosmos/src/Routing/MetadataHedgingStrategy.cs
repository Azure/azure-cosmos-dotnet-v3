//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
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
    /// Bounded cross-region hedging for cold-start metadata cache population.
    /// One instance per <see cref="CosmosClient"/>. Consumed by
    /// <c>ClientCollectionCache</c> and <c>PartitionKeyRangeCache</c>.
    /// </summary>
    /// <remarks>
    /// Design: <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c>. Wired into
    /// <c>ClientCollectionCache</c> and <c>PartitionKeyRangeCache</c> for
    /// cold-start metadata cache reads.
    /// </remarks>
    internal sealed class MetadataHedgingStrategy : IDisposable
    {
        internal const string TraceDatumKey = "Metadata Hedge Context";

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
            MetadataHedgingOptions options)
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
            this.perClientConcurrencyBudget = Math.Max(1, options?.PerClientConcurrencyBudget ?? MetadataHedgingOptions.DefaultPerClientConcurrencyBudget);
            this.hedgeBudget = new SemaphoreSlim(this.perClientConcurrencyBudget, this.perClientConcurrencyBudget);
        }

        internal TimeSpan Threshold => this.threshold;

        internal int PerClientConcurrencyBudget => this.perClientConcurrencyBudget;

        /// <summary>
        /// Resolves the tri-state
        /// <see cref="CosmosClientOptions.EnableMetadataHedgingForColdStart"/>
        /// to a concrete opt-in <see cref="bool"/>. When the customer leaves
        /// the property <c>null</c>, cold-start metadata hedging follows the
        /// account's PPAF (Per-Partition Automatic Failover) state — enabled by
        /// default when PPAF is enabled, disabled otherwise. An explicit
        /// <c>true</c> enables hedging even when PPAF is disabled, and an
        /// explicit <c>false</c> disables it regardless of PPAF — see design
        /// §5.1.
        /// </summary>
        internal static bool ResolveOptIn(bool? customerOptIn, bool isPpafEnabled)
        {
            return customerOptIn ?? isPpafEnabled;
        }

        /// <summary>
        /// Builds the strategy from the customer-supplied
        /// <see cref="CosmosClientOptions.EnableMetadataHedgingForColdStart"/>
        /// tri-state and optional <see cref="MetadataHedgingOptions"/>. Returns
        /// <c>null</c> only when the customer explicitly disabled hedging
        /// (<c>false</c>). When the property is <c>true</c> or left <c>null</c>
        /// the strategy is created and the per-request eligibility check
        /// resolves the effective opt-in against the live PPAF state (a
        /// <c>null</c> property follows PPAF; an explicit <c>true</c> enables
        /// hedging even when PPAF is disabled). The Gateway kill-switch is
        /// hard-wired to <c>false</c> in Phase 1; see design §5.1.
        /// </summary>
        internal static MetadataHedgingStrategy CreateIfEnabled(
            bool? enableMetadataHedgingForColdStart,
            MetadataHedgingOptions options,
            IGlobalEndpointManager globalEndpointManager,
            Func<bool> isPpafEnabled)
        {
            // Explicit customer kill-switch: hedging is suppressed regardless of PPAF.
            if (enableMetadataHedgingForColdStart == false)
            {
                return null;
            }

            MetadataHedgingOptions effectiveOptions = options ?? new MetadataHedgingOptions();
            TimeSpan threshold = effectiveOptions.Threshold
                ?? (HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout
                    + MetadataHedgingOptions.DefaultThresholdStep);

            return new MetadataHedgingStrategy(
                globalEndpointManager: globalEndpointManager,
                isHedgingDisabledByGateway: () => false,
                isPpafEnabled: isPpafEnabled,
                customerOptIn: enableMetadataHedgingForColdStart,
                threshold: threshold,
                options: effectiveOptions);
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

            // When the customer leaves the opt-in null, cold-start metadata hedging
            // follows the live PPAF state. An explicit opt-in of true bypasses this gate.
            if (!ResolveOptIn(this.customerOptIn, this.isPpafEnabled()))
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.PpafDisabled);
            }

            if (!hedgeContext.IsColdStart)
            {
                return MetadataHedgeEligibility.Skip(MetadataHedgeSkipReason.NotColdStart);
            }

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
            ReadOnlyCollection<Uri> applicable = this.globalEndpointManager.GetApplicableEndpoints(request, isReadRequest: true);
            Uri hedgeEndpoint = applicable.FirstOrDefault(u => u != null && !u.Equals(primaryEndpoint));

            if (hedgeEndpoint == null)
            {
                this.hedgeBudget.Release();
                diag.Eligible = false;
                diag.SkipReason = MetadataHedgeSkipReason.SingleRegion;
                CosmosDbEventSource.MetadataHedgeSkipped(diag.SkipReason.ToString(), diag.ResourceType);
                return await this.PrimaryOnlyAsync(request, primaryEndpoint, sendToEndpoint, hedgeContext, diag, trace, cancellationToken);
            }

            diag.HedgeRegion = this.SafeGetLocation(hedgeEndpoint);

            CancellationTokenSource primaryCts = null;
            CancellationTokenSource hedgeCts = null;
            CancellationTokenSource timerCts = null;
            bool budgetReleased = false;

            try
            {
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

                    if (branch == HedgeBranch.Hedge
                        && finished.Status == TaskStatus.RanToCompletion
                        && finished.Result != null
                        && (finished.Result.StatusCode == HttpStatusCode.Unauthorized
                            || finished.Result.StatusCode == HttpStatusCode.Forbidden))
                    {
                        diag.HedgeOutcome = finished.Result.StatusCode == HttpStatusCode.Unauthorized
                            ? "Auth401"
                            : "Auth403";
                        MetadataHedgingMeter.RecordHedgeAuthReject(diag.HedgeRegion, (int)finished.Result.StatusCode);
                        CosmosDbEventSource.MetadataHedgeAuthReject(diag.HedgeRegion, (int)finished.Result.StatusCode);
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
        /// <see cref="RetryUtility.IsRegionalFailure"/>. A 401 / plain 403
        /// from the hedge branch is rejected — see design §5.13.
        /// </summary>
        internal static bool IsAcceptableWinner(DocumentServiceResponse response, HedgeBranch branch)
        {
            if (response == null)
            {
                return false;
            }

            if (RetryUtility.IsRegionalFailure(
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
    }
}
