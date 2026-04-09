//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
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
        private bool ppafEnabled = false;

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
        /// This availability strategy can only be used if the request is a read-only request on a document request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="client"></param>
        /// <returns>whether the request should be a hedging request.</returns>
        internal bool ShouldHedge(RequestMessage request, CosmosClient client)
        {
            //Only use availability strategy for document point operations
            if (request.ResourceType != ResourceType.Document)
            {
                return false;
            }

            //check to see if it is a not a read-only request/ if multimaster writes are enabled
            if (!OperationTypeExtensions.IsReadOperation(request.OperationType))
            {
                if (this.EnableMultiWriteRegionHedge
                    && client.DocumentClient.GlobalEndpointManager.CanSupportMultipleWriteLocations(request.ResourceType, request.OperationType))
                {
                    return true;
                }

                // PPAF single-master: hedge writes using read regions as failover targets
                if (this.ppafEnabled)
                {
                    return true;
                }

                return false;
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
            this.ppafEnabled = client.DocumentClient.ConnectionPolicy.EnablePartitionLevelFailover;
            if (!this.ShouldHedge(request, client)
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

                    // For PPAF write hedging, use all account-level read regions (consistent with
                    // GlobalPartitionEndpointManagerCore's use of AccountReadEndpoints for PPAF failover).
                    // GetApplicableRegions filters through EffectivePreferredLocations, which could
                    // drop valid hedge targets not in the user's PreferredLocations.
                    IReadOnlyCollection<string> hedgeRegions = this.ppafEnabled && !isReadRequest
                        ? client.DocumentClient.GlobalEndpointManager
                            .GetApplicableAccountLevelReadRegions(request.RequestOptions?.ExcludeRegions)
                        : client.DocumentClient.GlobalEndpointManager
                            .GetApplicableRegions(request.RequestOptions?.ExcludeRegions, isReadRequest);

                    List<Task> requestTasks = new List<Task>(hedgeRegions.Count + 1);

                    // Capture the primary write endpoint for PPAF write hedging. When a hedged
                    // request succeeds, this is used to mark the primary as unavailable in the
                    // PPAF cache so future requests route directly to the successful region.
                    Uri ppafPrimaryWriteEndpoint = this.ppafEnabled && !isReadRequest
                        ? client.DocumentClient.GlobalEndpointManager.WriteEndpoints[0]
                        : null;

                    HedgingResponse hedgeResponse = null;

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
                                        ppafPrimaryWriteEndpoint: ppafPrimaryWriteEndpoint,
                                        partitionKeyRangeLocationCache: client.DocumentClient.PartitionKeyRangeLocation);

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
                            return hedgeResponse.ResponseMessage;
                        }
                    }

                    if (lastException != null)
                    {
                        throw lastException;
                    }

                    if (hedgeResponse == null)
                    {
                        if (applicationProvidedCancellationToken.IsCancellationRequested)
                        {
                            throw new CosmosOperationCanceledException(new OperationCanceledException(), trace);
                        }

                        throw new InvalidOperationException("Cross-region hedging completed without producing a response.");
                    }

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
            Uri ppafPrimaryWriteEndpoint,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache)
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
                    // and amplifying RU consumption. On success, the primary endpoint is used
                    // to update the cache so future requests route directly to the successful region.
                    if (this.ppafEnabled
                        && !OperationTypeExtensions.IsReadOperation(request.OperationType)
                        && ppafPrimaryWriteEndpoint != null)
                    {
                        clonedRequest.Properties[CrossRegionHedgingAvailabilityStrategy.SuppressPPAFCacheUpdateKey] = true;
                        clonedRequest.Properties[CrossRegionHedgingAvailabilityStrategy.PPAFHedgePrimaryEndpointKey] = ppafPrimaryWriteEndpoint;
                    }
                }

                return await this.RequestSenderAndResultCheckAsync(
                    sender,
                    clonedRequest,
                    hedgeRegions.ElementAt(requestNumber),
                    hedgeRequestsCancellationTokenSource, 
                    trace,
                    partitionKeyRangeLocationCache);
            }
        }

        private async Task<HedgingResponse> RequestSenderAndResultCheckAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            string targetRegionName,
            CancellationTokenSource hedgeRequestsCancellationTokenSource,
            ITrace trace,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache)
        {
            try
            {
                ResponseMessage response = await sender.Invoke(request, hedgeRequestsCancellationTokenSource.Token);

                // ShouldRetryAsync is only called on error responses (AbstractRetryHandler
                // short-circuits on success), so the PPAF cache update for successful hedged
                // writes must happen here, outside the retry policy pipeline.
                if (response.IsSuccessStatusCode)
                {
                    CrossRegionHedgingAvailabilityStrategy.TryUpdatePPAFCacheOnSuccessfulHedge(
                        request,
                        partitionKeyRangeLocationCache);
                }

                if (IsFinalResult((int)response.StatusCode, (int)response.Headers.SubStatusCode))
                {
                    if (!hedgeRequestsCancellationTokenSource.IsCancellationRequested)
                    {
                        // App has not reached e2e timeout - we can cancel any still remaining
                        // hedge requests since we have a final response now
                        hedgeRequestsCancellationTokenSource.Cancel();
                    }

                    return new HedgingResponse(true, response, targetRegionName);
                }

                return new HedgingResponse(false, response, targetRegionName);
            }
            catch (OperationCanceledException oce) when (hedgeRequestsCancellationTokenSource.IsCancellationRequested)
            {
                // hedgeRequestsCancellationTokenSource is a linked cancellation token source - so, would also signal
                // cancellation on e2e timeout via app provided CT
                throw new CosmosOperationCanceledException(oce, trace);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception thrown while executing cross region hedging availability strategy: {0}", ex.Message);
                throw ex;
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
        /// When a hedged PPAF write request receives a successful response, updates the
        /// partition-level failover cache to mark the primary write endpoint as unavailable.
        /// This causes future requests for the same partition to route directly to the region
        /// where the hedge succeeded, avoiding unnecessary hedging round-trips.
        /// This must be called from the hedging strategy (not from ClientRetryPolicy.ShouldRetryAsync)
        /// because AbstractRetryHandler short-circuits on success and never invokes ShouldRetryAsync
        /// for successful responses.
        /// </summary>
        private static void TryUpdatePPAFCacheOnSuccessfulHedge(
            RequestMessage request,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache)
        {
            if (request?.DocumentServiceRequest?.Properties == null
                || partitionKeyRangeLocationCache == null)
            {
                return;
            }

            if (!request.DocumentServiceRequest.Properties.TryGetValue(
                    CrossRegionHedgingAvailabilityStrategy.PPAFHedgePrimaryEndpointKey, out object primaryEndpointObj)
                || primaryEndpointObj is not Uri primaryEndpoint)
            {
                return;
            }

            // Temporarily set the request's routed endpoint to the primary so that
            // TryMarkEndpointUnavailableForPartitionKeyRange records the primary as
            // the failed location, routing future requests to the successful hedge region.
            Uri originalEndpoint = request.DocumentServiceRequest.RequestContext?.LocationEndpointToRoute;
            request.DocumentServiceRequest.RequestContext.RouteToLocation(primaryEndpoint);

            partitionKeyRangeLocationCache.TryMarkEndpointUnavailableForPartitionKeyRange(
                request.DocumentServiceRequest);

            // Restore the original endpoint for clean request state
            if (originalEndpoint != null)
            {
                request.DocumentServiceRequest.RequestContext.RouteToLocation(originalEndpoint);
            }
        }

        private sealed class HedgingResponse
        {
            public readonly bool IsNonTransient;
            public readonly ResponseMessage ResponseMessage;
            public readonly string TargetRegionName;

            public HedgingResponse(bool isNonTransient, ResponseMessage responseMessage, string targetRegionName)
            {
                this.IsNonTransient = isNonTransient;
                this.ResponseMessage = responseMessage;
                this.TargetRegionName = targetRegionName;
            }
        }
    }
}
