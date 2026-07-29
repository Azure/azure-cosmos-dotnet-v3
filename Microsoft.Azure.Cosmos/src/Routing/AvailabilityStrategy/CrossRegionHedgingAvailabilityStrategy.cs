//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Hedging availability strategy. Once threshold time is reached, 
    /// the SDK will send out an additional request to a remote region in parallel
    /// if the first hedging request or the original has not returned after the step time, 
    /// additional hedged requests will be sent out there is a response or all regions are exausted.
    /// </summary>
    internal class CrossRegionHedgingAvailabilityStrategy : AvailabilityStrategyInternal
    {
        private const string HedgeContext = "Hedge Context";
        private const string HedgeConfig = "Hedge Config";
        private const string ResponseRegion = "Response Region";

        /// <summary>
        /// Internal property key set on hedged (non-primary) write requests when PPAF is enabled.
        /// When present, the ClientRetryPolicy will skip updating the per-partition failover cache
        /// on error responses to prevent speculative hedge responses from poisoning the cache and
        /// causing RU amplification. On successful (2xx) responses, the cache IS updated to record
        /// that the primary region should be failed over for this partition.
        /// </summary>
        internal const string SuppressPPAFCacheUpdateKey = "x-ms-suppress-ppaf-cache-update";

        /// <summary>
        /// Internal property key storing the primary write endpoint URI on hedged PPAF write requests.
        /// When a hedged request succeeds, the ClientRetryPolicy uses this to mark the primary endpoint
        /// as unavailable for the partition, so future requests route directly to the successful region.
        /// </summary>
        internal const string PPAFHedgePrimaryEndpointKey = "x-ms-ppaf-hedge-primary-endpoint";

        /// <summary>
        /// Internal property key storing the exact endpoint a hedged (non-primary) PPAF write arm must be
        /// dispatched against. The endpoint is selected from the same topology snapshot the hedge fan-out
        /// was computed from, so <see cref="Routing.GlobalEndpointManager.ResolveServiceEndpoint"/> can route
        /// the arm directly instead of re-resolving the region name through the preferred-location filter
        /// (which can silently collapse a non-preferred hedge target back onto the primary write endpoint).
        /// </summary>
        internal const string PPAFHedgeTargetEndpointKey = "x-ms-ppaf-hedge-target-endpoint";

        /// <summary>
        /// Latency threshold which activates the first region hedging 
        /// </summary>
        public TimeSpan Threshold { get; private set; }

        /// <summary>
        /// When the SDK will send out additional hedging requests after the initial hedging request
        /// </summary>
        public TimeSpan ThresholdStep { get; private set; }

        /// <summary>
        /// Whether hedging for write requests on accounts with multi-region writes is enabled.
        /// Note that this does come with the caveat that there will be more 409 / 412 errors thrown by the SDK.
        /// This is expected and applications that adopt this feature should be prepared to handle these exceptions.
        /// Application might not be able to be deterministic on Create vs Replace in the case of Upsert Operations
        /// </summary>
        public bool EnableMultiWriteRegionHedge { get; private set; }

        /// <summary>
        /// Internal flag to indicate if this is the default strategy used by the SDK when enabling
        /// PPAF for clients without customer defined availability strategy.
        /// </summary>
        public bool IsSDKDefaultStrategyForPPAF { get; private set; }

        private readonly string HedgeConfigText;

        /// <summary>
        /// Constructor for hedging availability strategy
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="thresholdStep"></param>
        /// <param name="enableMultiWriteRegionHedge"></param>
        /// <param name="isSDKDefaultStrategy"></param>
        public CrossRegionHedgingAvailabilityStrategy(
            TimeSpan threshold,
            TimeSpan? thresholdStep,
            bool enableMultiWriteRegionHedge = false,
            bool isSDKDefaultStrategy = false)
        {
            if (threshold <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold));
            }

            if (thresholdStep <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(thresholdStep));
            }

            this.Threshold = threshold;
            this.ThresholdStep = thresholdStep ?? TimeSpan.FromMilliseconds(-1);
            this.EnableMultiWriteRegionHedge = enableMultiWriteRegionHedge;
            this.IsSDKDefaultStrategyForPPAF = isSDKDefaultStrategy;

            this.HedgeConfigText = $"t:{this.Threshold.TotalMilliseconds}ms, s:{this.ThresholdStep.TotalMilliseconds}ms, w:{this.EnableMultiWriteRegionHedge}";
        }

        /// <inheritdoc/>
        internal override bool Enabled()
        {
            return true;
        }

        /// <summary>
        /// This method determines if the request should be sent with a hedging availability strategy.
        /// Read requests on document resources are always hedged. Write requests are hedged under two
        /// mutually exclusive contracts:
        /// <list type="bullet">
        /// <item>
        /// Multi-write (multi-master) accounts hedge writes only when the caller explicitly opted in via
        /// <see cref="EnableMultiWriteRegionHedge"/>. PPAF never overrides that opt-out, because duplicate
        /// write arms surface additional 409 / 412 responses the caller must be prepared to handle.
        /// </item>
        /// <item>
        /// Single-master accounts hedge writes only when PPAF write hedging is active for this execution
        /// (PPAF enabled on the account and not disabled through AZURE_COSMOS_PPAF_WRITE_HEDGING_ENABLED).
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="request"></param>
        /// <param name="client"></param>
        /// <param name="ppafWriteHedgingEnabled">
        /// The immutable per-execution PPAF write-hedging decision computed by
        /// <see cref="ExecuteAvailabilityStrategyAsync"/>.
        /// </param>
        /// <returns>whether the request should be a hedging request.</returns>
        internal bool ShouldHedge(RequestMessage request, CosmosClient client, bool ppafWriteHedgingEnabled)
        {
            //Only use availability strategy for document point operations
            if (request.ResourceType != ResourceType.Document)
            {
                return false;
            }

            //check to see if it is a not a read-only request/ if multimaster writes are enabled
            if (!OperationTypeExtensions.IsReadOperation(request.OperationType))
            {
                if (client.DocumentClient.GlobalEndpointManager.CanSupportMultipleWriteLocations(
                    request.ResourceType,
                    request.OperationType))
                {
                    // Multi-write account: the explicit option is the only contract. Honoring PPAF here
                    // would let write hedging fan out for a client that explicitly opted out.
                    return this.EnableMultiWriteRegionHedge;
                }

                // Single-master account: PPAF write hedging is the only contract. Hedged writes target
                // the account read regions, which are the PPAF write-failover targets.
                return ppafWriteHedgingEnabled;
            }

            return true;
        }

        /// <summary>
        /// Execute the hedging availability strategy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <param name="applicationProvidedCancellationToken"></param>
        /// <returns>The response after executing cross region hedging</returns>
        internal override async Task<ResponseMessage> ExecuteAvailabilityStrategyAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CosmosClient client,
            RequestMessage request,
            CancellationToken applicationProvidedCancellationToken)
        {
            bool ppafWriteHedgingEnabled = client.DocumentClient.ConnectionPolicy.EnablePartitionLevelFailover
                && ConfigurationManager.IsPpafWriteHedgingEnabled();
            if (!this.ShouldHedge(request, client, ppafWriteHedgingEnabled)
                || client.DocumentClient.GlobalEndpointManager.ReadEndpoints.Count == 1)
            {
                return await sender(request, applicationProvidedCancellationToken);
            }
            
            ITrace trace = request.Trace;

            using (CancellationTokenSource hedgeRequestsCancellationTokenSource = 
                CancellationTokenSource.CreateLinkedTokenSource(applicationProvidedCancellationToken))
            {
                using (CloneableStream clonedBody = (CloneableStream)(request.Content == null
                    ? null
                    : await StreamExtension.AsClonableStreamAsync(request.Content)))
                {
                    bool isReadRequest = OperationTypeExtensions.IsReadOperation(request.OperationType);

                    // Immutable per-execution PPAF context. It is computed once here, from a single
                    // topology snapshot, and passed down the hedge path. It must never be read from
                    // instance state: the strategy instance is shared across concurrent executions and
                    // clients, so a field could be flipped by another execution across an await.
                    PPAFWriteHedgeContext ppafWriteHedgeContext = ppafWriteHedgingEnabled && !isReadRequest
                        ? PPAFWriteHedgeContext.TryCreate(client, request.RequestOptions?.ExcludeRegions)
                        : null;

                    // For PPAF write hedging, use all account-level read regions (consistent with
                    // GlobalPartitionEndpointManagerCore's use of AccountReadEndpoints for PPAF failover).
                    // GetApplicableRegions filters through EffectivePreferredLocations, which could
                    // drop valid hedge targets not in the user's PreferredLocations.
                    IReadOnlyCollection<string> hedgeRegions = ppafWriteHedgeContext != null
                        ? ppafWriteHedgeContext.HedgeRegions
                        : client.DocumentClient.GlobalEndpointManager
                            .GetApplicableRegions(request.RequestOptions?.ExcludeRegions, isReadRequest);

                    List<Task> requestTasks = new List<Task>(hedgeRegions.Count + 1);

                    HedgingResponse hedgeResponse = null;

                    // Inject a shared CrossRegionAvailabilityContext into Properties before the clone loop.
                    // RequestMessage.Clone() shallow-copies Properties, so all hedged clones share the same
                    // context instance — enabling hub region header propagation across hedged requests.
                    request.Properties[CrossRegionAvailabilityContext.PropertyKey] = new CrossRegionAvailabilityContext();

                    //Send out hedged requests
                    for (int requestNumber = 0; requestNumber < hedgeRegions.Count; requestNumber++)
                    {
                        TimeSpan awaitTime = requestNumber == 0 ? this.Threshold : this.ThresholdStep;

                        using (CancellationTokenSource timerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(applicationProvidedCancellationToken))
                        {
                            CancellationToken timerToken = timerTokenSource.Token;
                            using (Task hedgeTimer = Task.Delay(awaitTime, timerToken))
                            {
                                Task<HedgingResponse> requestTask = this.CloneAndSendAsync(
                                        sender: sender,
                                        request: request,
                                        clonedBody: clonedBody,
                                        hedgeRegions: hedgeRegions,
                                        requestNumber: requestNumber,
                                        trace: trace,
                                        hedgeRequestsCancellationTokenSource: hedgeRequestsCancellationTokenSource,
                                        ppafWriteHedgeContext: ppafWriteHedgeContext);

                                requestTasks.Add(requestTask);
                                requestTasks.Add(hedgeTimer);

                                Task completedTask;
                                do
                                {
                                    completedTask = await Task.WhenAny(requestTasks);
                                    requestTasks.Remove(completedTask);
                                }
                                while (
                                    completedTask == hedgeTimer &&
                                    // Ignore hedge timer signals if either the e2e timeout is hit 
                                    // or the hedgeTimer task failed (or more commonly since this is a linked CTS was cancelled)
                                    // in both of these cases we do not want to spawn new hedge requests
                                    // but just consolidate the outcome of previous requests
                                    (!completedTask.IsCompleted || applicationProvidedCancellationToken.IsCancellationRequested));

                                if (completedTask == hedgeTimer)
                                {
                                    continue;
                                }

                                requestTasks.Remove(hedgeTimer);
                                timerTokenSource.Cancel();

                                if (completedTask.IsFaulted || completedTask.IsCanceled)
                                {
                                    requestTasks.Remove(hedgeTimer);
                                    timerTokenSource.Cancel();

                                    if (applicationProvidedCancellationToken.IsCancellationRequested)
                                    {
                                        await (Task<HedgingResponse>)completedTask;
                                    }

                                    continue;
                                }

                                hedgeResponse = await (Task<HedgingResponse>)completedTask;
                                if (hedgeResponse.IsNonTransient)
                                {
                                    hedgeRequestsCancellationTokenSource.Cancel();

                                    ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        HedgeConfig,
                                        this.HedgeConfigText);

                                    // Only set Hedge Context when actual hedging occurred (requestNumber > 0).
                                    // When requestNumber == 0, the primary responded before the threshold.
                                    if (requestNumber > 0)
                                    {
                                        //Take is not inclusive, so we need to add 1 to the request number which starts at 0
                                        ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                            HedgeContext,
                                            hedgeRegions.Take(requestNumber + 1));
                                    }

                                    // Note that the target region can be seperate than the actual region that serviced the request depending on the scenario
                                    ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        ResponseRegion,
                                        hedgeResponse.TargetRegionName);

                                    CrossRegionHedgingAvailabilityStrategy.PublishPPAFCacheUpdateForWinner(
                                        hedgeResponse,
                                        client.DocumentClient.PartitionKeyRangeLocation);

                                    return hedgeResponse.ResponseMessage;
                                }
                            }
                        }
                    }

                    //Wait for a good response from the hedged requests/primary request
                    Exception lastException = null;
                    while (requestTasks.Any())
                    {
                        Task completedTask = await Task.WhenAny(requestTasks);
                        requestTasks.Remove(completedTask);
                        if (completedTask.IsFaulted)
                        {
                            AggregateException innerExceptions = completedTask.Exception.Flatten();
                            lastException = innerExceptions.InnerExceptions.FirstOrDefault();
                            continue;
                        }

                        if (completedTask.IsCanceled)
                        {
                            lastException = new OperationCanceledException();
                            continue;
                        }

                        hedgeResponse = await (Task<HedgingResponse>)completedTask;
                        if (hedgeResponse.IsNonTransient || requestTasks.Count == 0)
                        {
                            hedgeRequestsCancellationTokenSource.Cancel();
                            ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        HedgeConfig,
                                        this.HedgeConfigText);
                            ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                HedgeContext,
                                hedgeRegions);
                            ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                ResponseRegion,
                                hedgeResponse.TargetRegionName);

                            CrossRegionHedgingAvailabilityStrategy.PublishPPAFCacheUpdateForWinner(
                                hedgeResponse,
                                client.DocumentClient.PartitionKeyRangeLocation);

                            return hedgeResponse.ResponseMessage;
                        }
                    }

                    if (lastException != null)
                    {
                        // Use ExceptionDispatchInfo to preserve the original throwing-frame stack
                        // trace. `throw lastException;` would reset the StackTrace property to the
                        // current frame, which defeats the throw-vs-throw-ex preservation work in
                        // CloneAndSendAsync / RequestSenderAndResultCheckAsync.
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(lastException).Throw();
                    }

                    if (hedgeResponse == null)
                    {
                        if (applicationProvidedCancellationToken.IsCancellationRequested)
                        {
                            throw new CosmosOperationCanceledException(new OperationCanceledException(), trace);
                        }

                        throw new InvalidOperationException("Cross-region hedging completed without producing a response.");
                    }

                    CrossRegionHedgingAvailabilityStrategy.PublishPPAFCacheUpdateForWinner(
                        hedgeResponse,
                        client.DocumentClient.PartitionKeyRangeLocation);

                    return hedgeResponse.ResponseMessage;
                }
            }
        }

        private async Task<HedgingResponse> CloneAndSendAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            CloneableStream clonedBody,
            IReadOnlyCollection<string> hedgeRegions,
            int requestNumber,
            ITrace trace,
            CancellationTokenSource hedgeRequestsCancellationTokenSource,
            PPAFWriteHedgeContext ppafWriteHedgeContext)
        {
            RequestMessage clonedRequest;

            using (clonedRequest = request.Clone(
                trace,
                clonedBody))
            {
                clonedRequest.RequestOptions ??= new RequestOptions();

                //we do not want to exclude any regions for the primary request
                if (requestNumber > 0)
                {
                    List<string> excludeRegions = new List<string>(hedgeRegions);
                    excludeRegions.RemoveAt(requestNumber);
                    clonedRequest.RequestOptions.ExcludeRegions = excludeRegions;

                    // For PPAF write hedging: suppress partition-level failover cache updates
                    // on hedged (non-primary) error responses. Without this, hedged request errors
                    // poison the PPAF cache, causing all subsequent requests for the same
                    // partition to think the primary region failed over—triggering more hedging
                    // and amplifying RU consumption. The successful arm's cache update is deferred
                    // until after winner arbitration (see PublishPPAFCacheUpdateForWinner) so a
                    // late-completing losing arm can never overwrite the winner's override.
                    // The hedge target endpoint is pinned from the topology snapshot the fan-out was
                    // computed from so this arm cannot be re-resolved back onto the primary region.
                    if (ppafWriteHedgeContext != null)
                    {
                        clonedRequest.Properties[CrossRegionHedgingAvailabilityStrategy.SuppressPPAFCacheUpdateKey] = true;
                        clonedRequest.Properties[CrossRegionHedgingAvailabilityStrategy.PPAFHedgePrimaryEndpointKey] =
                            ppafWriteHedgeContext.PrimaryWriteEndpoint;

                        Uri hedgeTargetEndpoint = ppafWriteHedgeContext.GetHedgeTargetEndpoint(requestNumber);
                        if (hedgeTargetEndpoint != null)
                        {
                            clonedRequest.Properties[CrossRegionHedgingAvailabilityStrategy.PPAFHedgeTargetEndpointKey] = hedgeTargetEndpoint;
                        }
                    }

                    // Hedging-Detection API: this code path is only reached AFTER the
                    // previous loop iteration's threshold delay elapsed without primary-wins
                    // cancellation. Tag the upcoming dispatch as Hedging so the downstream
                    // dispatch site records it with the correct reason. If this method
                    // is never invoked for a given requestNumber (e.g., primary wins under
                    // the threshold), no phantom Hedging entry is produced — see AC2/AC13
                    // and design doc §12 "no phantom entries".
                    clonedRequest.Properties[HedgingDetectionState.DispatchReasonPropertyKey] =
                        RequestedRegionReason.Hedging;
                }

                try
                {
                    return await this.RequestSenderAndResultCheckAsync(
                        sender,
                        clonedRequest,
                        hedgeRegions.ElementAt(requestNumber),
                        hedgeRequestsCancellationTokenSource,
                        trace);
                }
                catch
                {
                    // .NET Framework workaround: when an exception is thrown deep in the request
                    // pipeline (e.g. CosmosOperationCanceledException raised after the hedge CTS is
                    // signalled), it propagates synchronously back through every awaiting async
                    // method. On .NET Framework 4.7.2 each awaiter consumes ~10KB of stack on the
                    // exception path, which can blow the managed stack when the request pipeline
                    // is deep. Yielding here forces the rethrow to resume on a fresh stack via the
                    // threadpool, breaking the synchronous propagation chain. This is a no-op on
                    // .NET Core / .NET 5+ (which already optimize this) beyond a single threadpool
                    // dispatch. See https://github.com/dotnet/runtime for the underlying issue.
                    await Task.Yield();
                    throw;
                }
            }
        }

        private async Task<HedgingResponse> RequestSenderAndResultCheckAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            string targetRegionName,
            CancellationTokenSource hedgeRequestsCancellationTokenSource,
            ITrace trace)
        {
            try
            {
                ResponseMessage response = await sender.Invoke(request, hedgeRequestsCancellationTokenSource.Token);

                // ShouldRetryAsync is only called on error responses (AbstractRetryHandler
                // short-circuits on success), so the PPAF cache update for successful hedged
                // writes cannot go through the retry policy pipeline. It is captured here as a
                // candidate and published by the caller only for the arm that actually wins,
                // so a slower arm completing after arbitration cannot overwrite the winner.
                PPAFCacheUpdateCandidate ppafCacheUpdate = response.IsSuccessStatusCode
                    ? CrossRegionHedgingAvailabilityStrategy.TryCreatePPAFCacheUpdateCandidate(request)
                    : null;

                if (IsFinalResult((int)response.StatusCode, (int)response.Headers.SubStatusCode))
                {
                    if (!hedgeRequestsCancellationTokenSource.IsCancellationRequested)
                    {
                        // App has not reached e2e timeout - we can cancel any still remaining
                        // hedge requests since we have a final response now
                        hedgeRequestsCancellationTokenSource.Cancel();
                    }

                    return new HedgingResponse(true, response, targetRegionName, ppafCacheUpdate);
                }

                return new HedgingResponse(false, response, targetRegionName, ppafCacheUpdate);
            }
            catch (OperationCanceledException oce) when (hedgeRequestsCancellationTokenSource.IsCancellationRequested)
            {
                // hedgeRequestsCancellationTokenSource is a linked cancellation token source - so, would also signal
                // cancellation on e2e timeout via app provided CT
                throw new CosmosOperationCanceledException(oce, trace);
            }
            catch (Exception ex)
            {
                if (DiagnosticsHandlerHelper.ShouldTrace(System.Diagnostics.TraceEventType.Error))
                {
                    DefaultTrace.TraceError("Exception thrown while executing cross region hedging availability strategy: {0}", ex.Message);
                }

                throw;
            }
        }

        private static bool IsFinalResult(int statusCode, int subStatusCode)
        {
            //All 1xx, 2xx, and 3xx status codes should be treated as final results
            if (statusCode < (int)HttpStatusCode.BadRequest)
            {
                return true;
            }

            //Status codes that indicate non-transient timeouts
            if (statusCode == (int)HttpStatusCode.BadRequest
                || statusCode == (int)HttpStatusCode.Conflict
                || statusCode == (int)HttpStatusCode.MethodNotAllowed
                || statusCode == (int)HttpStatusCode.PreconditionFailed
                || statusCode == (int)HttpStatusCode.RequestEntityTooLarge
                || statusCode == (int)HttpStatusCode.Unauthorized)
            {
                return true;
            }

            //404 - Not found is a final result as the document was not yet available
            //after enforcing the consistency model
            //All other errors should be treated as possibly transient errors
            return statusCode == (int)HttpStatusCode.NotFound && subStatusCode == (int)SubStatusCodes.Unknown;
        }

        /// <summary>
        /// Captures the information needed to update the partition-level failover cache for a
        /// successful hedged PPAF write, without applying it. The update must not be applied here:
        /// at this point the arm has not yet been arbitrated as the winner, and a losing arm that
        /// completes after arbitration would otherwise overwrite the winner's override.
        /// This has to be captured from the hedging strategy (not from ClientRetryPolicy.ShouldRetryAsync)
        /// because AbstractRetryHandler short-circuits on success and never invokes ShouldRetryAsync
        /// for successful responses.
        /// </summary>
        private static PPAFCacheUpdateCandidate TryCreatePPAFCacheUpdateCandidate(RequestMessage request)
        {
            if (request?.DocumentServiceRequest?.Properties == null)
            {
                return null;
            }

            // Only update the PPAF write cache for write requests
            if (OperationTypeExtensions.IsReadOperation(request.OperationType))
            {
                return null;
            }

            if (!request.DocumentServiceRequest.Properties.TryGetValue(
                    CrossRegionHedgingAvailabilityStrategy.PPAFHedgePrimaryEndpointKey, out object primaryEndpointObj)
                || primaryEndpointObj is not Uri primaryEndpoint)
            {
                return null;
            }

            // The successful endpoint is the one the hedged request was routed to
            Uri successfulEndpoint = request.DocumentServiceRequest.RequestContext?.LocationEndpointToRoute;
            if (successfulEndpoint == null)
            {
                return null;
            }

            return new PPAFCacheUpdateCandidate(
                request.DocumentServiceRequest,
                primaryEndpoint,
                successfulEndpoint);
        }

        /// <summary>
        /// Applies the deferred partition-level failover cache update for the arm that won
        /// arbitration. Called exactly once per hedged execution, after a winner has been chosen,
        /// so that a slower losing arm can never overwrite the winning region's override.
        /// </summary>
        private static void PublishPPAFCacheUpdateForWinner(
            HedgingResponse hedgeResponse,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache)
        {
            PPAFCacheUpdateCandidate candidate = hedgeResponse?.PPAFCacheUpdate;
            if (candidate == null || partitionKeyRangeLocationCache == null)
            {
                return;
            }

            // Directly set the cache to point to the successful endpoint rather than
            // marking the primary as failed and iterating sequentially. This ensures
            // that in multi-region scenarios (e.g., Primary=A, Read=B, Read=C where C
            // succeeds), the cache points to C, not B.
            partitionKeyRangeLocationCache.TrySetPartitionLevelLocationOverrideForSuccessfulHedge(
                candidate.Request,
                candidate.PrimaryEndpoint,
                candidate.SuccessfulEndpoint);
        }

        /// <summary>
        /// Immutable, per-execution PPAF write hedging context. Computed once, up front, from a
        /// single topology snapshot so that every hedge arm is bound to the exact endpoint it was
        /// fanned out for. Holding this as a local (rather than as instance state on the strategy)
        /// is what makes the strategy safe to share across concurrent executions.
        /// </summary>
        private sealed class PPAFWriteHedgeContext
        {
            private readonly IReadOnlyList<Uri> hedgeTargetEndpoints;

            private PPAFWriteHedgeContext(
                Uri primaryWriteEndpoint,
                IReadOnlyList<string> hedgeRegions,
                IReadOnlyList<Uri> hedgeTargetEndpoints)
            {
                this.PrimaryWriteEndpoint = primaryWriteEndpoint;
                this.HedgeRegions = hedgeRegions;
                this.hedgeTargetEndpoints = hedgeTargetEndpoints;
            }

            /// <summary>
            /// The write endpoint the primary (non-hedged) arm targets, captured from the same
            /// snapshot as the hedge targets.
            /// </summary>
            public Uri PrimaryWriteEndpoint { get; }

            /// <summary>
            /// The hedge regions in fan-out order. Index-aligned with the pinned endpoints.
            /// </summary>
            public IReadOnlyList<string> HedgeRegions { get; }

            /// <summary>
            /// Builds the context, or returns null when PPAF write hedging cannot be applied
            /// (for example, when the account topology yields no usable hedge targets). Callers
            /// fall back to the standard region-name based hedging path when null is returned.
            /// </summary>
            public static PPAFWriteHedgeContext TryCreate(CosmosClient client, IReadOnlyList<string> excludeRegions)
            {
                GlobalEndpointManager globalEndpointManager = client.DocumentClient.GlobalEndpointManager;

                ReadOnlyCollection<AccountLevelReadRegion> accountLevelReadRegions =
                    globalEndpointManager.GetApplicableAccountLevelReadRegions(
                        excludeRegions,
                        out ReadOnlyCollection<string> _);

                if (accountLevelReadRegions == null || accountLevelReadRegions.Count == 0)
                {
                    return null;
                }

                ReadOnlyCollection<Uri> writeEndpoints = globalEndpointManager.WriteEndpoints;
                if (writeEndpoints == null || writeEndpoints.Count == 0)
                {
                    return null;
                }

                List<string> hedgeRegions = new List<string>(accountLevelReadRegions.Count);
                List<Uri> hedgeTargetEndpoints = new List<Uri>(accountLevelReadRegions.Count);
                foreach (AccountLevelReadRegion accountLevelReadRegion in accountLevelReadRegions)
                {
                    hedgeRegions.Add(accountLevelReadRegion.Region);
                    hedgeTargetEndpoints.Add(accountLevelReadRegion.Endpoint);
                }

                return new PPAFWriteHedgeContext(
                    writeEndpoints[0],
                    hedgeRegions.AsReadOnly(),
                    hedgeTargetEndpoints.AsReadOnly());
            }

            /// <summary>
            /// The endpoint this hedge arm must be pinned to. Null when the endpoint for the
            /// region was not resolvable, in which case the arm falls back to normal routing.
            /// </summary>
            public Uri GetHedgeTargetEndpoint(int requestNumber)
            {
                return requestNumber >= 0 && requestNumber < this.hedgeTargetEndpoints.Count
                    ? this.hedgeTargetEndpoints[requestNumber]
                    : null;
            }
        }

        /// <summary>
        /// A deferred partition-level failover cache update produced by a successful hedge arm.
        /// It is only applied for the arm that wins arbitration.
        /// </summary>
        private sealed class PPAFCacheUpdateCandidate
        {
            public PPAFCacheUpdateCandidate(
                DocumentServiceRequest request,
                Uri primaryEndpoint,
                Uri successfulEndpoint)
            {
                this.Request = request;
                this.PrimaryEndpoint = primaryEndpoint;
                this.SuccessfulEndpoint = successfulEndpoint;
            }

            public DocumentServiceRequest Request { get; }

            public Uri PrimaryEndpoint { get; }

            public Uri SuccessfulEndpoint { get; }
        }

        private sealed class HedgingResponse
        {
            public readonly bool IsNonTransient;
            public readonly ResponseMessage ResponseMessage;
            public readonly string TargetRegionName;

            public HedgingResponse(
                bool isNonTransient,
                ResponseMessage responseMessage,
                string targetRegionName,
                PPAFCacheUpdateCandidate ppafCacheUpdate = null)
            {
                this.IsNonTransient = isNonTransient;
                this.ResponseMessage = responseMessage;
                this.TargetRegionName = targetRegionName;
                this.PPAFCacheUpdate = ppafCacheUpdate;
            }

            /// <summary>
            /// The PPAF cache update this arm would like applied if — and only if — it wins.
            /// </summary>
            public PPAFCacheUpdateCandidate PPAFCacheUpdate { get; }
        }
    }

    /// <summary>
    /// Mutable, thread-safe context shared across hedged request clones via the Properties dictionary.
    /// When the primary request's ClientRetryPolicy sets the hub region flag after 2x 404/1002,
    /// hedged requests (with their own ClientRetryPolicy instances) pick up the flag immediately.
    /// </summary>
    internal sealed class CrossRegionAvailabilityContext
    {
        /// <summary>
        /// Well-known key used to store/retrieve this context from Properties dictionary.
        /// </summary>
        internal const string PropertyKey = "CrossRegionAvailabilityContext";

        /// <summary>
        /// Thread-safe flag indicating that the hub region processing header should be added.
        /// Written by the primary request's ClientRetryPolicy after 2x 404/1002,
        /// read by hedged request ClientRetryPolicy instances in OnBeforeSendRequest.
        /// </summary>
        internal volatile bool ShouldAddHubRegionProcessingOnlyHeader;
    }
}
