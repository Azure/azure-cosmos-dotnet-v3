//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Client policy is combination of endpoint change retry + throttling retry.
    /// </summary>
    internal sealed class ClientRetryPolicy : IDocumentClientRetryPolicy
    {
        private const int RetryIntervalInMS = 1000; // Once we detect failover wait for 1 second before retrying request.
        private const int MaxRetryCount = 120;
        private const int MaxServiceUnavailableRetryCount = 1;
#if !INTERNAL
        private const int MaxSessionTokenRetryCount = 2;
#else
        private const int MaxSessionTokenRetryCount = 1;
#endif
        private const int MaxCaeRevocationRetryCount = 1;

        // ----- DTX (Distributed Transaction) inner-loop retry constants -----
        // The outer loop (DistributedTransactionCommitter) handles body-bearing isRetriable failures.
        // CRP owns envelope failures with empty body: 408, 449/5352 share one budget; 500/5411-5413 use a separate, tighter budget.
        private const int MaxDtxRetryCount = 10;
        private const int MaxDtxInfraFailureRetryCount = 9;
        private const int DtxInfraFailureMaxExponent = 6;
        private static readonly TimeSpan DtxInfraFailureBaseBackoff = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan DtxInfraFailureMaxBackoff = TimeSpan.FromSeconds(5);

        private readonly IDocumentClientRetryPolicy throttlingRetry;
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly GlobalPartitionEndpointManager partitionKeyRangeLocationCache;
        private readonly bool enableEndpointDiscovery;
        private readonly bool isThinClientEnabled;
#if !INTERNAL
        private readonly bool isHubRegionProcessingEnabled;
#endif
        private readonly AuthorizationTokenProvider authorizationTokenProvider;
        private int failoverRetryCount;

        private int sessionTokenRetryCount;
        private int serviceUnavailableRetryCount;
        private int caeRevocationRetryCount;
        private int distributedTransactionRetryCount;
        private int distributedTransactionInfraFailureRetryCount;
        private bool isReadRequest;
        private bool canUseMultipleWriteLocations;
        private bool isMultiMasterWriteRequest;
        private bool isDtxRequest;
        private Uri locationEndpoint;
        private RetryContext retryContext;
        private DocumentServiceRequest documentServiceRequest;
#if !INTERNAL
        private volatile bool addHubRegionProcessingOnlyHeader;
        private CrossRegionAvailabilityContext crossRegionAvailabilityContext;
#endif

        public ClientRetryPolicy(
            GlobalEndpointManager globalEndpointManager,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache,
            RetryOptions retryOptions,
            bool enableEndpointDiscovery,
            bool isThinClientEnabled,
            bool isHubRegionProcessingEnabled = true,
            AuthorizationTokenProvider authorizationTokenProvider = null)
        {
            this.throttlingRetry = new ResourceThrottleRetryPolicy(
                retryOptions.MaxRetryAttemptsOnThrottledRequests,
                retryOptions.MaxRetryWaitTimeInSeconds);

            this.globalEndpointManager = globalEndpointManager;
            this.partitionKeyRangeLocationCache = partitionKeyRangeLocationCache;
            this.failoverRetryCount = 0;
            this.enableEndpointDiscovery = enableEndpointDiscovery;
            this.sessionTokenRetryCount = 0;
            this.serviceUnavailableRetryCount = 0;
            this.caeRevocationRetryCount = 0;
            this.canUseMultipleWriteLocations = false;
            this.isMultiMasterWriteRequest = false;
            this.isThinClientEnabled = isThinClientEnabled;
#if !INTERNAL
            this.isHubRegionProcessingEnabled = isHubRegionProcessingEnabled;
#endif
            this.authorizationTokenProvider = authorizationTokenProvider;
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="exception">Exception that occurred when the operation was tried</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            this.retryContext = null;
            // Received Connection error (HttpRequestException), initiate the endpoint rediscovery
            if (exception is HttpRequestException _)
            {
                DefaultTrace.TraceWarning("ClientRetryPolicy: Gateway HttpRequestException Endpoint not reachable. Failed Location: {0}; ResourceAddress: {1}",
                    this.documentServiceRequest?.RequestContext?.LocationEndpointToRoute?.ToString() ?? string.Empty,
                    this.documentServiceRequest?.ResourceAddress ?? string.Empty);

                // In the event of the routing gateway having outage on region A, mark the partition as unavailable assuming that the
                // partition has been failed over to region B, when per partition automatic failover is enabled.
                this.TryMarkEndpointUnavailableForPkRange(shouldMarkEndpointUnavailableForPkRange: false);

                // Mark both read and write requests because it gateway exception.
                // This means all requests going to the region will fail.
                return await this.ShouldRetryOnEndpointFailureAsync(
                    isReadRequest: this.isReadRequest,
                    markBothReadAndWriteAsUnavailable: true,
                    forceRefresh: false,
                    retryOnPreferredLocations: true);
            }

            if (exception is DocumentClientException clientException)
            {
                // Today, the only scenario where we would treat a throttling (429) exception as service unavailable is when we
                // get 429 (TooManyRequests) with sub status code 3092 (System Resource Not Available). Note that this is applicable
                // for write requests targeted to a multiple master account. In such case, the 429/3092 will be treated as 503. The
                // reason to keep the code out of the throttling retry policy is that in the near future, the 3092 sub status code
                // might not be a throttling scenario at all and the status code in that case would be different than 429.
                if (this.ShouldMarkEndpointUnavailableOnSystemResourceUnavailableForWrite(
                    clientException.StatusCode,
                    clientException.GetSubStatus()))
                {
                    DefaultTrace.TraceError(
                        "Operation will NOT be retried on local region. Treating SystemResourceUnavailable (429/3092) as ServiceUnavailable (503). Status code: {0}, sub status code: {1}.",
                        StatusCodes.TooManyRequests, SubStatusCodes.SystemResourceUnavailable);

                    return this.TryMarkEndpointUnavailableForPkRangeAndRetryOnServiceUnavailable(
                        isSystemResourceUnavailableForWrite: true);
                }

                ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                    clientException?.StatusCode,
                    clientException?.GetSubStatus(),
                    clientException?.Headers,
                    clientException?.RetryAfter);
                if (shouldRetryResult != null)
                {
                    return shouldRetryResult;
                }
            }

            // Any metadata request will throw a cosmos exception from CosmosHttpClientCore if
            // it receives a 503 service unavailable from gateway. This check is to add retry
            // mechanism for the metadata requests in such cases.
            if (exception is CosmosException cosmosException)
            {
                ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                    cosmosException.StatusCode,
                    cosmosException.Headers.SubStatusCode,
                    cosmosException.Headers,
                    cosmosException.RetryAfter);
                if (shouldRetryResult != null)
                {
                    return shouldRetryResult;
                }
            }

            if (exception is OperationCanceledException)
            {
                DefaultTrace.TraceInformation("ClientRetryPolicy: The operation was cancelled. Not retrying. Retry count = {0}, Endpoint = {1}",
                    this.failoverRetryCount,
                    this.locationEndpoint?.ToString() ?? string.Empty);

                if (this.partitionKeyRangeLocationCache.IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(
                        this.documentServiceRequest))
                {
                    // In the event of a (ppaf + write operation) or (ppcb + read or multi-master write operation) getting timed
                    // out due to cancellation token expiration on region A, mark the partition as unavailable assuming that
                    // the partition has been failed over to region B, when per partition automatic failover is enabled.
                    this.partitionKeyRangeLocationCache.TryMarkEndpointUnavailableForPartitionKeyRange(
                         this.documentServiceRequest);
                }
            }

            return await this.throttlingRetry.ShouldRetryAsync(exception, cancellationToken);
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="ResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public async Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            this.retryContext = null;

            bool hasResponseBody = cosmosResponseMessage?.Content != null;

            ShouldRetryResult shouldRetryResult = await this.ShouldRetryInternalAsync(
                    cosmosResponseMessage?.StatusCode,
                    cosmosResponseMessage?.Headers.SubStatusCode,
                    cosmosResponseMessage?.Headers,
                    cosmosResponseMessage?.Headers.RetryAfter,
                    hasResponseBody);
            if (shouldRetryResult != null)
            {
                return shouldRetryResult;
            }

            // Today, the only scenario where we would treat a throttling (429) exception as service unavailable is when we
            // get 429 (TooManyRequests) with sub status code 3092 (System Resource Not Available). Note that this is applicable
            // for write requests targeted to a multiple master account. In such case, the 429/3092 will be treated as 503. The
            // reason to keep the code out of the throttling retry policy is that in the near future, the 3092 sub status code
            // might not be a throttling scenario at all and the status code in that case would be different than 429.
            if (this.ShouldMarkEndpointUnavailableOnSystemResourceUnavailableForWrite(
                cosmosResponseMessage.StatusCode,
                cosmosResponseMessage?.Headers.SubStatusCode))
            {
                DefaultTrace.TraceError(
                    "Operation will NOT be retried on local region. Treating SystemResourceUnavailable (429/3092) as ServiceUnavailable (503). Status code: {0}, sub status code: {1}.",
                    StatusCodes.TooManyRequests, SubStatusCodes.SystemResourceUnavailable);

                return this.TryMarkEndpointUnavailableForPkRangeAndRetryOnServiceUnavailable(
                    isSystemResourceUnavailableForWrite: true);
            }

            return await this.throttlingRetry.ShouldRetryAsync(cosmosResponseMessage, cancellationToken);
        }

        /// <summary>
        /// Method that is called before a request is sent to allow the retry policy implementation
        /// to modify the state of the request.
        /// </summary>
        /// <param name="request">The request being sent to the service.</param>
        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            this.isDtxRequest = DistributedTransactionConstants.IsDistributedTransactionRequest(
                request.OperationType,
                request.ResourceType);

            // Distributed transaction requests (including reads, which are sent as OperationType.Read)
            // must always route to the write region where the transaction coordinator lives. Treat them
            // as non-read for routing/failover purposes so they are never directed to read-only regions.
            this.isReadRequest = request.IsReadOnlyRequest && !this.isDtxRequest;
            this.canUseMultipleWriteLocations = this.globalEndpointManager.CanUseMultipleWriteLocations(request);
            this.documentServiceRequest = request;
            this.isMultiMasterWriteRequest = !request.IsReadOnlyRequest
                && (this.globalEndpointManager?.CanSupportMultipleWriteLocations(request.ResourceType, request.OperationType) ?? false);

            // clear previous location-based routing directive
            request.RequestContext.ClearRouteToLocation();

            if (this.retryContext != null)
            {
                if (this.retryContext.RouteToHub)
                {
                    request.RequestContext.RouteToLocation(this.globalEndpointManager.GetHubUri());
                }
                else
                {
                    // set location-based routing directive based on request retry context.
                    // For DTX requests, always disable preferred locations so the request enters
                    // the write-region flip-flop branch in LocationCache.ResolveServiceEndpoint,
                    // ensuring it never routes to a read-only region.
                    request.RequestContext.RouteToLocation(
                        this.retryContext.RetryLocationIndex,
                        this.isDtxRequest ? false : this.retryContext.RetryRequestOnPreferredLocations);
                }
            }
            else if (this.isDtxRequest)
            {
                // First attempt (no retry context): disable preferred locations so the request
                // enters the write-region branch in LocationCache.ResolveServiceEndpoint.
                request.RequestContext.RouteToLocation(0, usePreferredLocations: false);
            }

#if !INTERNAL
            // Initialize CrossRegionAvailabilityContext from Properties if not already set.
            // In hedging scenarios, Properties carries the shared context instance injected by
            // CrossRegionHedgingAvailabilityStrategy before cloning.
            if (this.crossRegionAvailabilityContext == null
                && request.Properties != null
                && request.Properties.TryGetValue(CrossRegionAvailabilityContext.PropertyKey, out object ctxObj)
                && ctxObj is CrossRegionAvailabilityContext sharedCtx)
            {
                this.crossRegionAvailabilityContext = sharedCtx;
            }

            // Hub-region cache + header handling for single-master reads.
            // On a retry (sessionTokenRetryCount > 0), check the per-partition hub cache. On HIT,
            // route directly to the cached hub URI — the warm-cache fast path (2 wires instead of
            // discovery). The hub-region-processing-only header is attached only when the cold-cache
            // flag is set (after 2 × 404/1002, or propagated via the shared hedge context), so it
            // never appears on wire 1 or on a warm-cache wire 2.
            bool hubHeaderFlagSet = this.addHubRegionProcessingOnlyHeader
                || this.crossRegionAvailabilityContext?.ShouldAddHubRegionProcessingOnlyHeader == true;

            if (this.isHubRegionProcessingEnabled
                && request.IsReadOnlyRequest
                && (this.sessionTokenRetryCount > 0 || hubHeaderFlagSet))
            {
                bool pkRangeLocationCacheHit = this.partitionKeyRangeLocationCache.TryAddPartitionLevelLocationOverride(
                    request, checkHubRegionOverrideInCache: true);

                if (hubHeaderFlagSet)
                {
                    request.Headers[HttpConstants.HttpHeaders.ShouldProcessOnlyInHubRegion] = bool.TrueString;
                }

                if (pkRangeLocationCacheHit)
                {
                    this.locationEndpoint = request.RequestContext.LocationEndpointToRoute;
                    return;
                }
            }
#endif
            // Resolve and pin the endpoint for the request. Per-region thin client gate: route to the proxy only
            // when the regional endpoint the request would use is probe-healthy; otherwise pin the gateway
            // endpoint. 
            Uri thinClientCandidate = this.isThinClientEnabled
                && ThinClientStoreModel.IsThinClientRoutable(this.globalEndpointManager, request)
                ? this.globalEndpointManager.GetThinClientEndpointCandidate(request)
                : null;

            this.locationEndpoint = thinClientCandidate != null
                && this.globalEndpointManager.IsProxyEndpointHealthy(thinClientCandidate)
                ? this.globalEndpointManager.ResolveThinClientEndpoint(request)
                : this.globalEndpointManager.ResolveServiceEndpoint(request);

            request.RequestContext.RouteToLocation(this.locationEndpoint);

            // Force UsePreferredLocations=false for DTX after endpoint pinning so that any
            // downstream re-resolution (e.g. on retry) stays on the write-region branch.
            if (this.isDtxRequest)
            {
                request.RequestContext.RouteToLocation(
                    this.retryContext?.RetryLocationIndex ?? 0,
                    usePreferredLocations: false);
            }

            // Hedging-Detection API: tag the upcoming dispatch reason on Properties so that
            // the downstream dispatch site (TransportHandler / GatewayStoreModel) can append
            // a RequestedRegion entry with the correct reason. Only override when this is a
            // genuine retry attempt — first-attempt dispatches default to Initial (set by
            // the dispatch site), and hedge-arm dispatches have their Hedging reason set by
            // CrossRegionHedgingAvailabilityStrategy before reaching this policy.
            //
            // Hedging preservation invariant: when a hedge arm itself triggers a retry (e.g.
            // 410 Gone / 449), this method is re-entered with retryContext != null on the
            // same cloned RequestMessage. The Hedging value previously seeded by
            // CrossRegionHedgingAvailabilityStrategy.CloneAndSendAsync must NOT be silently
            // overwritten with OperationRetry / RegionFailover, otherwise the hedge origin
            // is lost from the GetRequestedRegions() sequence. Preserve the existing value
            // if it is already Hedging.
            if (this.retryContext != null && request.Properties != null)
            {
                bool alreadyTaggedAsHedging =
                    request.Properties.TryGetValue(Tracing.HedgingDetectionState.DispatchReasonPropertyKey, out object existingReasonObj)
                    && existingReasonObj is RequestedRegionReason existingReason
                    && existingReason == RequestedRegionReason.Hedging;

                if (!alreadyTaggedAsHedging)
                {
                    RequestedRegionReason reason = this.retryContext.RetryRequestOnPreferredLocations
                        ? RequestedRegionReason.RegionFailover
                        : RequestedRegionReason.OperationRetry;
                    request.Properties[Tracing.HedgingDetectionState.DispatchReasonPropertyKey] = reason;
                }
            }
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            INameValueCollection responseHeaders,
            TimeSpan? retryAfter = null,
            bool hasResponseBody = false)
        {
            return await this.ShouldRetryInternalAsync(
                statusCode,
                subStatusCode,
                responseHeaders?.Get(HttpConstants.HttpHeaders.WwwAuthenticate),
                retryAfter,
                hasResponseBody);
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            Headers responseHeaders,
            TimeSpan? retryAfter = null,
            bool hasResponseBody = false)
        {
            return await this.ShouldRetryInternalAsync(
                statusCode,
                subStatusCode,
                responseHeaders?[HttpConstants.HttpHeaders.WwwAuthenticate],
                retryAfter,
                hasResponseBody);
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            string wwwAuthenticateHeaderValue,
            TimeSpan? retryAfter = null,
            bool hasResponseBody = false)
        {
            if (!statusCode.HasValue
                && (!subStatusCode.HasValue
                || subStatusCode.Value == SubStatusCodes.Unknown))
            {
                return null;
            }

            // Received request timeout
            if (statusCode == HttpStatusCode.RequestTimeout)
            {
                DefaultTrace.TraceWarning("ClientRetryPolicy: RequestTimeout. Failed Location: {0}; ResourceAddress: {1}",
                    this.documentServiceRequest?.RequestContext?.LocationEndpointToRoute?.ToString() ?? string.Empty,
                    this.documentServiceRequest?.ResourceAddress ?? string.Empty);

                // For DTX commits, a 408 from the coordinator means "transaction in-progress" — NOT
                // an endpoint reachability problem. Marking the endpoint unavailable here would poison
                // routing for non-DTX traffic sharing the same partition-key-range cache.
                if (!this.isDtxRequest)
                {
                    // Mark the partition key range as unavailable to retry future request on a new region.
                    this.TryMarkEndpointUnavailableForPkRange(shouldMarkEndpointUnavailableForPkRange: false);
                }
            }

            // Received 403.3 on write region or a read region, initiate the endpoint rediscovery
            if (statusCode == HttpStatusCode.Forbidden
                && subStatusCode == SubStatusCodes.WriteForbidden)
            {
                // A 403.3 can be returned for both read or write requests. The read request will return a 403.3 only when
                // the region, with the hub region processing only header determines that it is not the current hub region
                // for the partition. In either of the case, we mark the endpoint unavailable for the partition key range.
                // If we exhaust all the region level mark down for the partition key range, then we will mark the endpoint
                // unavailable for writes in that region.
                if (this.TryMarkEndpointUnavailableForPkRange(
                    shouldMarkEndpointUnavailableForPkRange: true))
                {
                    return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                }

                DefaultTrace.TraceWarning("ClientRetryPolicy: Endpoint not writable. Refresh cache and retry. Failed Location: {0}; ResourceAddress: {1}",
                    this.documentServiceRequest?.RequestContext?.LocationEndpointToRoute?.ToString() ?? string.Empty,
                    this.documentServiceRequest?.ResourceAddress ?? string.Empty);

                if (this.globalEndpointManager.IsMultimasterMetadataWriteRequest(this.documentServiceRequest))
                {
                    bool forceRefresh = false;

                    if (this.retryContext != null && this.retryContext.RouteToHub)
                    {
                        forceRefresh = true;
                        
                    }

                    ShouldRetryResult retryResult = await this.ShouldRetryOnEndpointFailureAsync(
                        isReadRequest: false,
                        markBothReadAndWriteAsUnavailable: false,
                        forceRefresh: forceRefresh,
                        retryOnPreferredLocations: false,
                        overwriteEndpointDiscovery: true);

                    if (retryResult.ShouldRetry)
                    {
                        this.retryContext.RouteToHub = true;
                    }
                    
                    return retryResult;
                }

                // Note: This can be triggered by the read requests as well. In that case, we will set the isReadRequest to
                // false to ensure that we mark the endpoint unavailable for writes only.
                return await this.ShouldRetryOnEndpointFailureAsync(
                    isReadRequest: false,
                    markBothReadAndWriteAsUnavailable: false,
                    forceRefresh: true,
                    retryOnPreferredLocations: false);
            }

            // Regional endpoint is not available yet for reads (e.g. add/ online of region is in progress)
            if (statusCode == HttpStatusCode.Forbidden
                && subStatusCode == SubStatusCodes.DatabaseAccountNotFound
                && (this.isReadRequest || this.canUseMultipleWriteLocations))
            {
                DefaultTrace.TraceWarning("ClientRetryPolicy: Endpoint not available for reads. Refresh cache and retry. Failed Location: {0}; ResourceAddress: {1}",
                    this.documentServiceRequest?.RequestContext?.LocationEndpointToRoute?.ToString() ?? string.Empty,
                    this.documentServiceRequest?.ResourceAddress ?? string.Empty);

                //Retry policy will retry on the next preffered region as the original requert region is not accepting requests
                return await this.ShouldRetryOnEndpointFailureAsync(
                    isReadRequest: this.isReadRequest,
                    markBothReadAndWriteAsUnavailable: false,
                    forceRefresh: false,
                    retryOnPreferredLocations: true);
            }

            if (statusCode == HttpStatusCode.NotFound && subStatusCode == SubStatusCodes.ReadSessionNotAvailable)
            {
                return this.ShouldRetryOnSessionNotAvailable(this.documentServiceRequest);
            }

            // Received 503 due to client connect timeout or Gateway
            if (statusCode == HttpStatusCode.ServiceUnavailable)
            {
                return this.TryMarkEndpointUnavailableForPkRangeAndRetryOnServiceUnavailable(
                    isSystemResourceUnavailableForWrite: false);
            }

            // Recieved 500 status code or lease not found.
            // DTX requests (including read DTX whose IsReadOnlyRequest is true) defer to the
            // DTX-specific retry classifier below so the dedicated 500/5411-5413 budget applies
            // instead of generic endpoint-unavailable retry.
            // Note: 410/1022 (LeaseNotFound) is not emitted by DTX coordinator; this branch never hit for DTX.
            if ((statusCode == HttpStatusCode.InternalServerError && this.isReadRequest && !this.isDtxRequest)
                || (statusCode == HttpStatusCode.Gone && subStatusCode == SubStatusCodes.LeaseNotFound))
            {
                return this.ShouldRetryOnUnavailableEndpointStatusCodes();
            }

            if (statusCode == HttpStatusCode.Unauthorized
                && (subStatusCode == SubStatusCodes.AadTokenRevoked
                    || !string.IsNullOrEmpty(wwwAuthenticateHeaderValue)))
            {
                return this.HandleUnauthorizedResponse(wwwAuthenticateHeaderValue);
            }

            if (this.isDtxRequest)
            {
                return this.ShouldRetryDtxRequest(statusCode, subStatusCode, retryAfter, hasResponseBody);
            }

            return null;
        }

        private ShouldRetryResult HandleUnauthorizedResponse(string wwwAuthenticateHeaderValue)
        {
            if (!(this.authorizationTokenProvider is AuthorizationTokenProviderTokenCredential tokenProvider)
                || this.documentServiceRequest == null)
            {
                return null;
            }

            if (this.caeRevocationRetryCount >= ClientRetryPolicy.MaxCaeRevocationRetryCount)
            {
                DefaultTrace.TraceWarning(
                    "ClientRetryPolicy: Token revocation max retry count ({0}) exceeded. Not retrying.",
                    ClientRetryPolicy.MaxCaeRevocationRetryCount);
                return ShouldRetryResult.NoRetry();
            }

            if (!tokenProvider.TryHandleTokenRevocation(
                HttpStatusCode.Unauthorized,
                wwwAuthenticateHeaderValue))
            {
                return null;
            }

            this.caeRevocationRetryCount++;
            DefaultTrace.TraceInformation(
                "ClientRetryPolicy: AAD token revocation handled. Retrying with fresh token. RetryCount={0}",
                this.caeRevocationRetryCount);
            return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
        }

        private async Task<ShouldRetryResult> ShouldRetryOnEndpointFailureAsync(
            bool isReadRequest,
            bool markBothReadAndWriteAsUnavailable,
            bool forceRefresh,
            bool retryOnPreferredLocations,
            bool overwriteEndpointDiscovery = false)
        {
            if (this.failoverRetryCount > MaxRetryCount || (!this.enableEndpointDiscovery && !overwriteEndpointDiscovery))
            {
                DefaultTrace.TraceInformation("ClientRetryPolicy: ShouldRetryOnEndpointFailureAsync() Not retrying. Retry count = {0}, Endpoint = {1}", 
                    this.failoverRetryCount,
                    this.locationEndpoint?.ToString() ?? string.Empty);
                return ShouldRetryResult.NoRetry();
            }

            this.failoverRetryCount++;

            if (this.locationEndpoint != null && !overwriteEndpointDiscovery)
            {
                if (isReadRequest || markBothReadAndWriteAsUnavailable)
                {
                    this.globalEndpointManager.MarkEndpointUnavailableForRead(this.locationEndpoint);
                }
                
                if (!isReadRequest || markBothReadAndWriteAsUnavailable)
                {
                    this.globalEndpointManager.MarkEndpointUnavailableForWrite(this.locationEndpoint);
                }
            }

            TimeSpan retryDelay = TimeSpan.Zero;
            if (!isReadRequest)
            {
                DefaultTrace.TraceInformation("ClientRetryPolicy: Failover happening. retryCount {0}", this.failoverRetryCount);

                if (this.failoverRetryCount > 1)
                {
                    //if retried both endpoints, follow regular retry interval.
                    retryDelay = TimeSpan.FromMilliseconds(ClientRetryPolicy.RetryIntervalInMS);
                }
            }
            else
            {
                retryDelay = TimeSpan.FromMilliseconds(ClientRetryPolicy.RetryIntervalInMS);
            }

            await this.globalEndpointManager.RefreshLocationAsync(forceRefresh);

            int retryLocationIndex = this.failoverRetryCount; // Used to generate a round-robin effect
            if (retryOnPreferredLocations)
            {
                retryLocationIndex = 0; // When the endpoint is marked as unavailable, it is moved to the bottom of the preferrence list
            }

            this.retryContext = new RetryContext
            {
                RetryLocationIndex = retryLocationIndex,
                RetryRequestOnPreferredLocations = retryOnPreferredLocations,
            };

            return ShouldRetryResult.RetryAfter(retryDelay);
        }

        private ShouldRetryResult ShouldRetryOnSessionNotAvailable(DocumentServiceRequest request)
        {
            this.sessionTokenRetryCount++;

            if (!this.enableEndpointDiscovery)
            {
                // if endpoint discovery is disabled, the request cannot be retried anywhere else
                return ShouldRetryResult.NoRetry();
            }
            else
            {
                if (this.canUseMultipleWriteLocations)
                {
                    ReadOnlyCollection<Uri> endpoints = this.globalEndpointManager.GetApplicableEndpoints(request, this.isReadRequest);
                    if (this.sessionTokenRetryCount > endpoints.Count)
                    {
                        // When use multiple write locations is true and the request has been tried 
                        // on all locations, then don't retry the request
                        return ShouldRetryResult.NoRetry();
                    }
                    else
                    {
                        this.retryContext = new RetryContext()
                        {
                            RetryLocationIndex = this.sessionTokenRetryCount,
                            RetryRequestOnPreferredLocations = true
                        };

                        return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                    }
                }
                else
                {
#if !INTERNAL
                    // Hub region discovery: only for single-master accounts AND read-only requests.
                    // The hub-region-processing-only header is meaningful only on reads (the backend
                    // routes reads to the partition's hub based on this header); writes already go to
                    // the write region by default and the header has no defined semantics for them.
                    if (this.isHubRegionProcessingEnabled
                        && request.IsReadOnlyRequest
                        && this.sessionTokenRetryCount >= MaxSessionTokenRetryCount)
                    {
                        this.addHubRegionProcessingOnlyHeader = true;

                        // Propagate to shared context so hedged requests
                        // (running in parallel with their own ClientRetryPolicy)
                        // pick up the hub region header immediately.
                        if (this.crossRegionAvailabilityContext != null)
                        {
                            this.crossRegionAvailabilityContext.ShouldAddHubRegionProcessingOnlyHeader = true;
                        }
                    }
#endif

                    if (this.sessionTokenRetryCount > MaxSessionTokenRetryCount)
                    {
                        return ShouldRetryResult.NoRetry();
                    }
                    else
                    {
                        // Single-master 404/1002 retry. Set retryContext to the write region as
                        // a fallback. OnBeforeSendRequest will do a cache lookup first: warm cache
                        // HIT routes wire 2 to the cached hub (no header); MISS falls through to
                        // this fallback (write region, no header) until the 2 × 404/1002 threshold
                        // triggers the hub-region-processing-only header on the next wire.
                        this.retryContext = new RetryContext
                        {
                            RetryLocationIndex = 0,
                            RetryRequestOnPreferredLocations = false
                        };

                        return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to mark the endpoint associated with the current partition key range as unavailable and determines if
        /// a retry should be performed due to a ServiceUnavailable (503) response. This method is invoked when a 503
        /// Service Unavailable response is received, indicating that the service might be temporarily unavailable.
        /// It optionally marks the partition key range as unavailable, which will influence future routing decisions.
        /// </summary>
        /// <param name="isSystemResourceUnavailableForWrite">A boolean flag indicating whether the endpoint for the
        /// current partition key range should be marked as unavailable, if the failure happened due to system
        /// resource unavailability.</param>
        /// <returns>An instance of <see cref="ShouldRetryResult"/> indicating whether the operation should be retried.</returns>
        private ShouldRetryResult TryMarkEndpointUnavailableForPkRangeAndRetryOnServiceUnavailable(
            bool isSystemResourceUnavailableForWrite)
        {
            DefaultTrace.TraceWarning("ClientRetryPolicy: ServiceUnavailable. Refresh cache and retry. Failed Location: {0}; ResourceAddress: {1}",
                this.documentServiceRequest?.RequestContext?.LocationEndpointToRoute?.ToString() ?? string.Empty,
                this.documentServiceRequest?.ResourceAddress ?? string.Empty);

            this.TryMarkEndpointUnavailableForPkRange(isSystemResourceUnavailableForWrite);

            return this.ShouldRetryOnUnavailableEndpointStatusCodes();
        }

        /// <summary>
        /// For a ServiceUnavailable (503.0) we could be having a timeout from Direct/TCP locally or a request to Gateway request with a similar response due to an endpoint not yet available.
        /// We try and retry the request only if there are other regions available. The retry logic is applicable for single master write accounts as well.
        /// Other status codes include InternalServerError (500.0) and LeaseNotFound (410.1022).
        /// </summary>
        private ShouldRetryResult ShouldRetryOnUnavailableEndpointStatusCodes()
        {
            if (this.serviceUnavailableRetryCount++ >= ClientRetryPolicy.MaxServiceUnavailableRetryCount)
            {
                DefaultTrace.TraceInformation($"ClientRetryPolicy: ShouldRetryOnServiceUnavailable() Not retrying. Retry count = {this.serviceUnavailableRetryCount}.");
                return ShouldRetryResult.NoRetry();
            }

            if (!this.canUseMultipleWriteLocations
                    && !this.isReadRequest
                    && !this.partitionKeyRangeLocationCache.IsPartitionLevelAutomaticFailoverEnabled())
            {
                // Write requests on single master cannot be retried if partition level failover is disabled.
                // This means there are no other regions available to serve the writes.
                return ShouldRetryResult.NoRetry();
            }

            int availablePreferredLocations = this.globalEndpointManager.PreferredLocationCount;

            if (availablePreferredLocations <= 1)
            {
                // No other regions to retry on
                DefaultTrace.TraceInformation($"ClientRetryPolicy: ShouldRetryOnServiceUnavailable() Not retrying. No other regions available for the request. AvailablePreferredLocations = {availablePreferredLocations}.");
                return ShouldRetryResult.NoRetry();
            }

            DefaultTrace.TraceInformation($"ClientRetryPolicy: ShouldRetryOnServiceUnavailable() Retrying. Received on endpoint {this.locationEndpoint}, IsReadRequest = {this.isReadRequest}.");

            // Retrying on second PreferredLocations
            // RetryCount is used as zero-based index
            this.retryContext = new RetryContext()
            {
                RetryLocationIndex = this.serviceUnavailableRetryCount,
                RetryRequestOnPreferredLocations = true
            };

            return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
        }

        /// <summary>
        /// Attempts to mark the endpoint associated with the current partition key range as unavailable
        /// which will influence future routing decisions.
        /// </summary>
        /// <param name="shouldMarkEndpointUnavailableForPkRange">A boolean flag indicating if the endpoint should be marked as unavailable for the pk-range. If true,
        /// the endpoint will be marked unavailable is either 1) for the pk-range of a multi master write request, bypassing the circuit breaker check
        /// or 2) for the pk-range when a read request received a 403.3 with the hub region header.</param>
        /// <returns>A boolean flag indicating whether the endpoint was marked as unavailable.</returns>
        private bool TryMarkEndpointUnavailableForPkRange(
            bool shouldMarkEndpointUnavailableForPkRange)
        {
            if (this.documentServiceRequest != null
                && (shouldMarkEndpointUnavailableForPkRange
                || this.IsRequestEligibleForPerPartitionAutomaticFailover()
                || this.IsRequestEligibleForPartitionLevelCircuitBreaker()))
            {
                // Mark the partition as unavailable.
                // Let the ClientRetry logic decide if the request should be retried
                return this.partitionKeyRangeLocationCache.TryMarkEndpointUnavailableForPartitionKeyRange(
                    request: this.documentServiceRequest);
            }

            return false;
        }

        /// <summary>
        /// Returns a boolean flag indicating if the endpoint should be marked as unavailable
        /// due to a 429 response with a sub status code of 3092 (system resource unavailable).
        /// This is applicable for write requests targeted for multi master accounts.
        /// </summary>
        /// <param name="statusCode">An instance of <see cref="HttpStatusCode"/> containing the status code.</param>
        /// <param name="subStatusCode">An instance of <see cref="SubStatusCodes"/> containing the sub status code.</param>
        /// <returns>A boolean flag indicating is the endpoint should be marked as unavailable.</returns>
        private bool ShouldMarkEndpointUnavailableOnSystemResourceUnavailableForWrite(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode)
        {
            return this.isMultiMasterWriteRequest
                && statusCode.HasValue
                && (int)statusCode.Value == (int)StatusCodes.TooManyRequests
                && subStatusCode == SubStatusCodes.SystemResourceUnavailable;
        }

        /// <summary>
        /// Determines if a request is eligible for per-partition automatic failover.
        /// A request is eligible if it is a write request, partition level failover is enabled,
        /// and the global endpoint manager cannot use multiple write locations for the request.
        /// </summary>
        /// <returns>True if the request is eligible for per-partition automatic failover, otherwise false.</returns>
        private bool IsRequestEligibleForPerPartitionAutomaticFailover()
        {
            return this.partitionKeyRangeLocationCache.IsRequestEligibleForPerPartitionAutomaticFailover(
                this.documentServiceRequest);
        }

        /// <summary>
        /// Determines if a request is eligible for partition-level circuit breaker.
        /// This method checks if the request is a read-only request or a multi master write request, if partition-level circuit breaker is enabled,
        /// and if the partition key range location cache indicates that the partition can fail over based on the number of request failures.
        /// </summary>
        /// <returns>
        /// True if the read request is eligible for partition-level circuit breaker, otherwise false.
        /// </returns>
        private bool IsRequestEligibleForPartitionLevelCircuitBreaker()
        {
            return this.partitionKeyRangeLocationCache.IsRequestEligibleForPartitionLevelCircuitBreaker(this.documentServiceRequest)
                        && this.partitionKeyRangeLocationCache.IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(this.documentServiceRequest);
        }

        // DTX retry classifier. The coordinator distinguishes envelope failures (no body) from semantic
        // failures (body with per-op results + isRetriable). Body-bearing responses defer to the outer
        // DistributedTransactionCommitter loop; otherwise the inner loop owns retry along one of two
        // shapes: coordinator-retriable (408/449) or infrastructure failure (500/5411-5413).
        private ShouldRetryResult ShouldRetryDtxRequest(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            TimeSpan? retryAfter,
            bool hasResponseBody)
        {
            int statusCodeValue = (int?)statusCode ?? 0;
            int subStatusCodeValue = (int?)subStatusCode ?? 0;

            bool isCoordinatorRetriable =
                statusCodeValue == (int)HttpStatusCode.RequestTimeout
                || (statusCodeValue == (int)StatusCodes.RetryWith && subStatusCode == SubStatusCodes.DtcCoordinatorRaceConflict)
                || (statusCodeValue == (int)StatusCodes.TooManyRequests && subStatusCode == SubStatusCodes.RUBudgetExceeded);

            bool isInfraFailure =
                statusCodeValue == (int)HttpStatusCode.InternalServerError
                && (subStatusCode == SubStatusCodes.DtcLedgerFailure
                    || subStatusCode == SubStatusCodes.DtcAccountConfigFailure
                    || subStatusCode == SubStatusCodes.DtcDispatchFailure);

            // Body-bearing response carries per-op isRetriable in JSON. The outer DistributedTransactionCommitter
            // loop owns retry; defer to avoid inner×outer amplification.
            if (hasResponseBody && isCoordinatorRetriable)
            {
                DefaultTrace.TraceInformation("ClientRetryPolicy: DTX response body present (Status={0}, SubStatus={1}). Deferring to outer loop.", statusCodeValue, subStatusCodeValue);
                return ShouldRetryResult.NoRetry();
            }

            if (isCoordinatorRetriable)
            {
                // 429/3200 without body — ResourceThrottleRetryPolicy handles it via Retry-After.
                if (statusCodeValue == (int)StatusCodes.TooManyRequests)
                {
                    return null;
                }

                int attempt = this.distributedTransactionRetryCount++;
                return this.RetryDtxWithBudget(
                    attempt,
                    ClientRetryPolicy.MaxDtxRetryCount,
                    retryAfter ?? TimeSpan.FromMilliseconds(ClientRetryPolicy.RetryIntervalInMS),
                    statusCodeValue,
                    subStatusCodeValue);
            }

            if (isInfraFailure)
            {
                int attempt = this.distributedTransactionInfraFailureRetryCount++;
                return this.RetryDtxWithBudget(
                    attempt,
                    ClientRetryPolicy.MaxDtxInfraFailureRetryCount,
                    DistributedTransactionRetryHelpers.ComputeBackoff(
                        attempt,
                        ClientRetryPolicy.DtxInfraFailureBaseBackoff,
                        ClientRetryPolicy.DtxInfraFailureMaxBackoff,
                        ClientRetryPolicy.DtxInfraFailureMaxExponent),
                    statusCodeValue,
                    subStatusCodeValue);
            }

            // 452/5421 (Aborted) and unrecognized codes fall through to the outer loop / default policy.
            return null;
        }

        private ShouldRetryResult RetryDtxWithBudget(int attempt, int cap, TimeSpan delay, int statusCode, int subStatusCode)
        {
            if (attempt >= cap)
            {
                DefaultTrace.TraceInformation("ClientRetryPolicy: DTX retry budget exhausted. attempt={0}, cap={1}, Status={2}, SubStatus={3}.",
                    attempt, cap, statusCode, subStatusCode);
                return ShouldRetryResult.NoRetry();
            }

            DefaultTrace.TraceWarning("ClientRetryPolicy: DTX retriable response (Status={0}, SubStatus={1}, attempt={2}, delayMs={3}). Retrying. Failed Location: {4}",
                statusCode,
                subStatusCode,
                attempt,
                (int)delay.TotalMilliseconds,
                this.documentServiceRequest?.RequestContext?.LocationEndpointToRoute?.ToString() ?? string.Empty);

            return ShouldRetryResult.RetryAfter(delay);
        }

        private sealed class RetryContext
        {
            public int RetryLocationIndex { get; set; }
            public bool RetryRequestOnPreferredLocations { get; set; }

            public bool RouteToHub { get; set; }
        }
    }
}
