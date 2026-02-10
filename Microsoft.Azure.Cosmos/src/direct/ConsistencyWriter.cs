//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Documents.RntbdConstants;

    /*

ConsistencyWriter has two modes for writing - local quorum-acked write and globally strong write.

The determination of whether a request is a local quorum-acked write or a globally strong write is through several factors:
1. Request.RequestContext.OriginalRequestConsistencyLevel - ensure that original request's consistency level, if set, is strong.
2. Default consistency level of the accoutn should be strong.
3. Number of read regions returned by write response > 0.

For quorum-acked write:
  We send single request to primary of a single partition, which will take care of replicating to its secondaries. Once write quorum number of replicas commits the write, the write request returns to the user with success. There is no additional handling for this case.

For globally strong write:
  Similarly, we send single request to primary of write region, which will take care of replicating to its secondaries, one of which is XPPrimary. XPPrimary will then replicate to all remote regions, which will all ack from within their region. In the write region, the request returns from the backend once write quorum number of replicas commits the write - but at this time, the response cannot be returned to caller, since linearizability guarantees will be violated. ConsistencyWriter will continuously issue barrier head requests against the partition in question, until GlobalCommittedLsn is at least as big as the lsn of the original response.
1. Issue write request to write region
2. Receive response from primary of write region, look at GlobalCommittedLsn and LSN headers.
3. If GlobalCommittedLSN = LSN, return response to caller
4. If GlobalCommittedLSN < LSN, cache LSN in request as SelectedGlobalCommittedLSN, and issue barrier requests against any/all replicas.
5. Each barrier response will contain its own LSN and GlobalCommittedLSN, check for any response that satisfies GlobalCommittedLSN >= SelectedGlobalCommittedLSN
6. Return to caller on success.

For Less than Strong accounts with EnableNRegionSynchronousCommit feature enabled:
Business logic: We send single request to primary of the Write region,which will take care of replicating to its secondaries, one of which is XPPrimary. XPPrimary in this case will replicate this request to n read regions, which will ack from within their region.
    In the write region where the original request was sent to , the request returns from the backend once write quorum number of replicas commits the write - but at this time, the response cannot be returned to caller, since linearizability guarantees will be violated.
    ConsistencyWriter will continuously issue barrier head requests against the partition in question, until GlobalNRegionCommittedGLSN is at least as big as the lsn of the original response.
Sequence of steps:
1. After receiving response from primary of write region, look at GlobalNRegionCommittedGLSN and LSN headers.
2. If GlobalNRegionCommittedGLSN = LSN, return response to caller
3. If GlobalNRegionCommittedGLSN < LSN && storeResponse.NumberOFReadRegions > 0 , cache LSN in request as SelectedGlobalNRegionCommittedGLSN, and issue barrier requests against any/all replicas.
4. Each barrier response will contain its own LSN and GlobalNRegionCommittedGLSN, check for any response that satisfies GlobalNRegionCommittedGLSN >= SelectedGlobalNRegionCommittedGLSN
5. Return to caller on success.
     */

    [SuppressMessage("", "AvoidMultiLineComments", Justification = "Multi line business logic")]
    internal sealed class ConsistencyWriter
    {
        private const int maxNumberOfWriteBarrierReadRetries = 30;
        private const int delayBetweenWriteBarrierCallsInMs = 30;

        private const int maxShortBarrierRetriesForMultiRegion = 4;
        private const int shortbarrierRetryIntervalInMsForMultiRegion = 10;

        private static readonly TimeSpan shortDelayBetweenWriteBarrierCallsForMultipleRegions = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan defaultDelayBetweenWriteBarrierCalls = TimeSpan.FromMilliseconds(30);
        private static readonly TimeSpan[] defaultBarrierRequestDelays = GetDefaultBarrierRequestDelays();
        private static readonly TimeSpan totalAllowedBarrierRequestDelay = GetTotalAllowedBarrierRequestDelay();

        private readonly StoreReader storeReader;
        private readonly TransportClient transportClient;
        private readonly AddressSelector addressSelector;
        private readonly ISessionContainer sessionContainer;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly IServiceConfigurationReaderExtension serviceConfigurationReaderExtension;
        private readonly IAuthorizationTokenProvider authorizationTokenProvider;
        private readonly bool useMultipleWriteLocations;
        private readonly ISessionRetryOptions sessionRetryOptions;

        public ConsistencyWriter(
            AddressSelector addressSelector,
            ISessionContainer sessionContainer,
            TransportClient transportClient,
            IServiceConfigurationReader serviceConfigReader,
            IAuthorizationTokenProvider authorizationTokenProvider,
            bool useMultipleWriteLocations,
            bool enableReplicaValidation,
            ISessionRetryOptions sessionRetryOptions = null)
        {
            this.transportClient = transportClient;
            this.addressSelector = addressSelector;
            this.sessionContainer = sessionContainer;
            this.serviceConfigReader = serviceConfigReader;
            this.serviceConfigurationReaderExtension = serviceConfigReader as IServiceConfigurationReaderExtension;
            this.authorizationTokenProvider = authorizationTokenProvider;
            this.useMultipleWriteLocations = useMultipleWriteLocations;
            this.sessionRetryOptions = sessionRetryOptions;
            this.storeReader = new StoreReader(
                                    transportClient,
                                    addressSelector,
                                    new AddressEnumerator(),
                                    sessionContainer: null,
                                    enableReplicaValidation); //we need store reader only for global strong, no session is needed*/
        }

        // Test hook
        internal string LastWriteAddress
        {
            get;
            private set;
        }

        private static TimeSpan[] GetDefaultBarrierRequestDelays()
        {
            TimeSpan[] delays = new TimeSpan[maxShortBarrierRetriesForMultiRegion + maxNumberOfWriteBarrierReadRetries];

            for (int i = 0; i < maxShortBarrierRetriesForMultiRegion; i++)
            {
                delays[i] = shortDelayBetweenWriteBarrierCallsForMultipleRegions;
            }

            for (int i = maxShortBarrierRetriesForMultiRegion
                ; i < maxShortBarrierRetriesForMultiRegion + maxNumberOfWriteBarrierReadRetries
                ; i++)
            {
                delays[i] = defaultDelayBetweenWriteBarrierCalls;
            }

            return delays;
        }

        private static TimeSpan GetTotalAllowedBarrierRequestDelay()
        {
            TimeSpan totalAllowedDelay = TimeSpan.Zero;
            foreach (TimeSpan current in GetDefaultBarrierRequestDelays())
            {
                totalAllowedDelay += current;
            }

            return totalAllowedDelay;
        }

        public async Task<StoreResponse> WriteAsync(
            DocumentServiceRequest entity,
            TimeoutHelper timeout,
            bool forceRefresh,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            timeout.ThrowTimeoutIfElapsed();
            string sessionToken = entity.Headers[HttpConstants.HttpHeaders.SessionToken];
            try
            {
                // RequestRetryUtility vs BackoffRetryUtility: is purely for safe flighting purpose only
                // Post flighting can be fully pivoted to RequestRetryUtility and remove BackoffRetryUtility below
                if (entity.UseStatusCodeFor4041002
                    && entity.IsValidRequestFor4041002())
                {
                    return await RequestRetryUtility.ProcessRequestAsync<DocumentServiceRequest, StoreResponse>(
                        executeAsync: () => this.WritePrivateAsync(entity, timeout, forceRefresh),
                        prepareRequest: () => entity,
                        policy: new SessionTokenMismatchRetryPolicy(
                            sessionRetryOptions: this.sessionRetryOptions),
                        cancellationToken: cancellationToken);
                }

                return await BackoffRetryUtility<StoreResponse>.ExecuteAsync(
                    callbackMethod: () => this.WritePrivateAsync(entity, timeout, forceRefresh),
                    retryPolicy: new SessionTokenMismatchRetryPolicy(
                        sessionRetryOptions: this.sessionRetryOptions),
                    cancellationToken: cancellationToken);
            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(entity, sessionToken);
            }
        }

        private async Task<StoreResponse> WritePrivateAsync(
            DocumentServiceRequest request,
            TimeoutHelper timeout,
            bool forceRefresh)
        {
            timeout.ThrowTimeoutIfElapsed();

            request.RequestContext.TimeoutHelper = timeout;

            if (request.RequestContext.RequestChargeTracker == null)
            {
                request.RequestContext.RequestChargeTracker = new RequestChargeTracker();
            }

            if (request.RequestContext.ClientRequestStatistics == null)
            {
                request.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            }

            request.RequestContext.ForceRefreshAddressCache = forceRefresh;

            if (request.RequestContext.CachedWriteStoreResult == null)
            {
                StoreResponse response = null;

                string requestedCollectionRid = request.RequestContext.ResolvedCollectionRid;

                PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.addressSelector.ResolveAddressesAsync(request, forceRefresh);

                if (!string.IsNullOrEmpty(requestedCollectionRid) && !string.IsNullOrEmpty(request.RequestContext.ResolvedCollectionRid))
                {
                    if (!requestedCollectionRid.Equals(request.RequestContext.ResolvedCollectionRid))
                    {
                        this.sessionContainer.ClearTokenByResourceId(requestedCollectionRid);
                    }
                }

                // the transportclient relies on this contacted replicas being present *before* the request is made
                // TODO: Can we not rely on this inversion of dependencies.
                request.RequestContext.ClientRequestStatistics.ContactedReplicas = partitionPerProtocolAddress.ReplicaTransportAddressUris.ToList();

                TransportAddressUri primaryUri = partitionPerProtocolAddress.GetPrimaryAddressUri(request);
                this.LastWriteAddress = primaryUri.ToString();

                if ((this.useMultipleWriteLocations || request.OperationType == OperationType.Batch) &&
                    RequestHelper.GetConsistencyLevelToUse(this.serviceConfigReader, request) == ConsistencyLevel.Session)
                {
                    // Set session token to ensure session consistency for write requests
                    // 1. when writes can be issued to multiple locations
                    // 2. When we have Batch requests, since it can have Reads in it.
                    SessionTokenHelper.SetPartitionLocalSessionToken(request, this.sessionContainer);
                }
                else
                {
                    // When writes can only go to single location, there is no reason
                    // to session session token to the server.
                    SessionTokenHelper.ValidateAndRemoveSessionToken(request);
                }

                DateTime startTimeUtc = DateTime.UtcNow;
                ReferenceCountedDisposable<StoreResult> storeResult = null;
                try
                {
                    response = await this.transportClient.InvokeResourceOperationAsync(primaryUri, request);

                    storeResult = StoreResult.CreateStoreResult(
                        storeResponse: response,
                        responseException: null,
                        requiresValidLsn: true,
                        useLocalLSNBasedHeaders: false,
                        replicaHealthStatuses: primaryUri.GetCurrentHealthState().GetHealthStatusDiagnosticsAsReadOnlyEnumerable(),
                        storePhysicalAddress: primaryUri.Uri);

                    request.RequestContext.ClientRequestStatistics.RecordResponse(
                        request: request,
                        storeResult: storeResult.Target,
                        startTimeUtc: startTimeUtc,
                        endTimeUtc: DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    storeResult = StoreResult.CreateStoreResult(
                        storeResponse: null,
                        responseException: ex,
                        requiresValidLsn: true,
                        useLocalLSNBasedHeaders: false,
                        replicaHealthStatuses: primaryUri.GetCurrentHealthState().GetHealthStatusDiagnosticsAsReadOnlyEnumerable(),
                        storePhysicalAddress: primaryUri.Uri);

                    request.RequestContext.ClientRequestStatistics.RecordResponse(
                        request: request,
                        storeResult: storeResult.Target,
                        startTimeUtc: startTimeUtc,
                        endTimeUtc: DateTime.UtcNow);

                    if (ex is DocumentClientException)
                    {
                        DocumentClientException dce = (DocumentClientException)ex;
                        StoreResult.VerifyCanContinueOnException(dce);
                        string value = dce.Headers[HttpConstants.HttpHeaders.WriteRequestTriggerAddressRefresh];
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            int result;
                            if (int.TryParse(dce.Headers.GetValues(HttpConstants.HttpHeaders.WriteRequestTriggerAddressRefresh)[0],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out result) && result == 1)
                            {
                                this.addressSelector.StartBackgroundAddressRefresh(request);
                            }
                        }
                    }
                }

                if (storeResult?.Target is null)
                {
                    Debug.Assert(false, "StoreResult cannot be null at this point.");
                    DefaultTrace.TraceCritical("ConsistencyWriter did not get storeResult!");
                    throw new InternalServerErrorException();
                }

                BarrierType barrierType = this.ComputeBarrierType(storeResult, request);
                if (barrierType == BarrierType.None)
                {
                    // If barrier is not performed, we can return the store result directly.
                    return storeResult.Target.ToResponse();
                }

                await this.PerformBarriersForWritesAsync(storeResult, request, barrierType);
            }
            else
            {
                BarrierType barrierType = this.ComputeBarrierType(request.RequestContext.CachedWriteStoreResult, request);
                Func<StoreResult, long> lsnAttributeSelector = barrierType == BarrierType.GlobalStrongWrite ? (sr => sr.GlobalCommittedLSN) : (sr => sr.GlobalNRegionCommittedGLSN);

                await this.CreateAndWaitForWriteBarrierAsync(request, barrierType, lsnAttributeSelector);
            }

            return request.RequestContext.CachedWriteStoreResult.Target.ToResponse();
        }

        internal bool ShouldPerformWriteBarrierForGlobalStrong(
            StoreResult storeResult,
            DocumentServiceRequest incomingRequest)
        {
            #if !COSMOSCLIENT
            bool skipSettingStrongConsistencyHeaderForWrites = false;

            // For write requests sending the header to skip setting strong consistency header, we should not set consistency level header.
            if (bool.TryParse(incomingRequest.Headers[HttpConstants.HttpHeaders.SkipSettingStrongConsistencyHeaderForWrites], out skipSettingStrongConsistencyHeaderForWrites) &&
                skipSettingStrongConsistencyHeaderForWrites)
            {
                return false;
            }
            #endif

            if (incomingRequest.OperationType.IsSkippedForWriteBarrier())
            {
                return false;
            }

            ConsistencyLevel consistencyLevel = this.serviceConfigReader.DefaultConsistencyLevel;
            if (this.serviceConfigurationReaderExtension != null &&
                this.serviceConfigurationReaderExtension.TryGetConsistencyLevel(incomingRequest, out ConsistencyLevel consistencyLevelOverride))
            {
                // Allow overriding consistency level for specific resource type and operation type
                // This is currently done for meta-data resources for PPAF enabled accounts where consistency level is overridden to Strong if account consistency is less than Strong.
                // Strong consistency is crucial to avoid partial updates and ensure that operations reflect a consistent state.
                DefaultTrace.TraceInformation(
                    "ConsistencyWriter: ConsistencyLevel is overridden from {0} to {1} for resourceType {2} and operationType {3}",
                    consistencyLevel,
                    consistencyLevelOverride,
                    incomingRequest.ResourceType.ToResourceTypeString(),
                    incomingRequest.OperationType.ToOperationTypeString());
                consistencyLevel = consistencyLevelOverride;
            }

            if (storeResult.StatusCode < StatusCodes.StartingErrorCode ||
                storeResult.StatusCode == StatusCodes.Conflict ||
                (storeResult.StatusCode == StatusCodes.NotFound && storeResult.SubStatusCode != SubStatusCodes.ReadSessionNotAvailable) ||
                storeResult.StatusCode == StatusCodes.PreconditionFailed)
            {
                if (consistencyLevel == ConsistencyLevel.Strong && storeResult.NumberOfReadRegions > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempt barrier requests if applicable.
        /// Cases in which barrier head requests are applicable (refer to comments on the class definition for more details on the whole write protocol.
        /// For globally strong write:
        /// 1. After receiving response from primary of write region, look at GlobalCommittedLsn and LSN headers.
        ///     2. If GlobalCommittedLSN = LSN, return response to caller
        ///      3. If GlobalCommittedLSN &lt; LSN, cache LSN in request as SelectedGlobalCommittedLSN, and issue barrier requests against any/all replicas.
        ///      4. Each barrier response will contain its own LSN and GlobalCommittedLSN, check for any response that satisfies GlobalCommittedLSN &gt;= SelectedGlobalCommittedLSN
        ///      5. Return to caller on success.
        ///  For less than Strong Accounts and If EnableNRegionSynchronousCommit is enabled for the account:
        ///      1. After receiving response from primary of write region, look at GlobalCommittedLsn and LSN headers.
        ///      2. If GlobalNRegionCommittedGLSN = LSN, return response to caller
        ///      3. If GlobalNRegionCommittedGLSN &lt; LSN &amp;&amp; storeResponse.NumberOFReadRegions &gt; 0 , cache LSN in request as SelectedGlobalNRegionCommittedGLSN, and issue barrier requests against any/all replicas.
        ///      4. Each barrier response will contain its own LSN and GlobalNRegionCommittedGLSN, check for any response that satisfies GlobalNRegionCommittedGLSN &gt;= SelectedGlobalNRegionCommittedGLSN
        ///      5. Return to caller on success.
        /// </summary>
        /// <param name="storeResult"></param>
        /// <param name="request"></param>
        /// <param name="barrierType"></param>
        /// <returns></returns>
        /// <exception cref="GoneException"></exception>
        private async Task<bool> PerformBarriersForWritesAsync(
            ReferenceCountedDisposable<StoreResult> storeResult,
            DocumentServiceRequest request,
            BarrierType barrierType)
        {
            long lsn = storeResult.Target.LSN;
            long globalCommitLsnToBeTracked = -1;
            Func<StoreResult, long> lsnAttributeSelector = null;

            //No need to run any barriers.
            if (barrierType == BarrierType.None)
            {
                return true;
            }

            string warningMessage = string.Empty;
            switch (barrierType)
            {
                case BarrierType.GlobalStrongWrite:
                    {
                        request.RequestContext.GlobalStrongWriteEndpoint = request.RequestContext.LocationEndpointToRoute;
                        globalCommitLsnToBeTracked = storeResult.Target.GlobalCommittedLSN;
                        warningMessage = "ConsistencyWriter: LSN {0} or GlobalCommittedLsn {1} is not set for global strong request";

                        DefaultTrace.TraceInformation("ConsistencyWriter: globalCommittedLsn {0}, lsn {1}", globalCommitLsnToBeTracked, lsn);
                        lsnAttributeSelector = sr => sr.GlobalCommittedLSN;
                        break;
                    }

                case BarrierType.NRegionSynchronousCommit:
                    {
                        globalCommitLsnToBeTracked = storeResult.Target.GlobalNRegionCommittedGLSN;
                        warningMessage = "ConsistencyWriter: LSN {0} or globalNRegionCommittedLsn {1} is not set for less than strong request with EnableNRegionSynchronousCommit property enabled ";

                        DefaultTrace.TraceInformation("ConsistencyWriter: globalNRegionCommittedLsn {0}, lsn {1}", globalCommitLsnToBeTracked, lsn);
                        lsnAttributeSelector = sr => sr.GlobalNRegionCommittedGLSN;
                        break;
                    }

                default:
                    break;
            }

            if (lsn == -1 || globalCommitLsnToBeTracked == -1)
            {
                DefaultTrace.TraceWarning(warningMessage, lsn, globalCommitLsnToBeTracked);
                // Service Generated because no lsn and glsn set by service
                throw new GoneException(RMResources.Gone, SubStatusCodes.ServerGenerated410);
            }

            request.RequestContext.CachedWriteStoreResult = storeResult;
            request.RequestContext.GlobalCommittedSelectedLSN = lsn;
            //if necessary we would have already refreshed cache by now.
            request.RequestContext.ForceRefreshAddressCache = false;

            //barrier only if necessary, i.e. when write region completes write, but read regions have not.
            if (globalCommitLsnToBeTracked < lsn)
            {
                await this.CreateAndWaitForWriteBarrierAsync(request, barrierType, lsnAttributeSelector);
            }

            return true;
        }

        /// <summary>
        /// Issues a barrier request and waits until the write barrier condition is met for the specified consistency level.
        /// This method is used to ensure that a write operation is fully committed across the required replicas or regions
        /// before returning success to the caller. If the barrier condition is not met within the allowed retries or time,
        /// a GoneException is thrown to indicate the write barrier was not satisfied.
        /// </summary>
        private async Task CreateAndWaitForWriteBarrierAsync(
            DocumentServiceRequest request,
            BarrierType barrierType,
            Func<StoreResult, long> lsnAttributeSelector = null)
        {
            //No need to run any barriers.
            if (barrierType == BarrierType.None)
            {
                return;
            }

            using (DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(
                request: request,
                authorizationTokenProvider: this.authorizationTokenProvider,
                targetLsn: null,
                targetGlobalCommittedLsn: request.RequestContext.GlobalCommittedSelectedLSN,
                includeRegionContext: true))
            {
                if (!await this.WaitForWriteBarrierAsync(barrierRequest, request.RequestContext.GlobalCommittedSelectedLSN, lsnAttributeSelector))
                {
                    if (barrierType == BarrierType.GlobalStrongWrite)
                    {
                        DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier has not been met for global strong request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                        throw new GoneException(RMResources.GlobalStrongWriteBarrierNotMet, SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet);
                    }
                    else
                    {
                        DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier has not been met for n region synchronous commit request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                        throw new GoneException(RMResources.NRegionCommitWriteBarrierNotMet, SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet);
                    }
                }
            }
        }

        /// <summary>
        /// Waits for the write barrier condition to be met by issuing barrier requests to replicas.
        /// This method determines which barrier handling implementation to use (old or new) based on feature flags,
        /// and delegates the actual waiting logic. It returns true if the required global committed LSN is observed
        /// within the allowed retries and time, otherwise returns false.
        /// </summary>
        private Task<bool> WaitForWriteBarrierAsync(
            DocumentServiceRequest barrierRequest,
            long selectedGlobalCommittedLsn,
            Func<StoreResult, long> lsnAttributeSelector)
        {
            if (BarrierRequestHelper.IsOldBarrierRequestHandlingEnabled)
            {
                return this.WaitForWriteBarrierOldAsync(barrierRequest, selectedGlobalCommittedLsn, lsnAttributeSelector);
            }

            return this.WaitForWriteBarrierNewAsync(barrierRequest, selectedGlobalCommittedLsn, lsnAttributeSelector);
        }

        // NOTE this is only temporarily kept to have a feature flag
        // (Env variable 'AZURE_COSMOS_OLD_BARRIER_REQUESTS_HANDLING_ENABLED' allowing to fall back
        // This old implementation will be removed (and the environment
        // variable not been used anymore) after some bake time.
        private async Task<bool> WaitForWriteBarrierOldAsync(
            DocumentServiceRequest barrierRequest,
            long selectedGlobalCommittedLsn,
            Func<StoreResult, long> lsnAttributeSelector)
        {
            int writeBarrierRetryCount = ConsistencyWriter.maxNumberOfWriteBarrierReadRetries;
            bool lastAttemptWasThrottled = false;
            long maxGlobalCommittedLsnReceived = 0;
            while (writeBarrierRetryCount-- > 0)
            {
                this.ValidateGlobalStrongWriteEndpoint(barrierRequest);

                barrierRequest.RequestContext.TimeoutHelper.ThrowTimeoutIfElapsed();
                IList<ReferenceCountedDisposable<StoreResult>> responses = await this.storeReader.ReadMultipleReplicaAsync(
                    barrierRequest,
                    includePrimary: true,
                    replicaCountToRead: 1, // any replica with correct globalCommittedLsn is good enough
                    requiresValidLsn: false,
                    useSessionToken: false,
                    readMode: ReadMode.Strong,
                    checkMinLSN: false,
                    forceReadAll: false);

                if (BarrierRequestHelper.IsGoneLeaseNotFound(responses))
                {
                    // Try primary with force refresh. If primary also fails with 410/1022, this will throw and bail out.
                    bool isSuccess = await this.TryPrimaryOnlyWriteBarrierAsync(barrierRequest, selectedGlobalCommittedLsn);
                    if (isSuccess)
                    {
                        return true;
                    }
                }
                lastAttemptWasThrottled = false;
                if (responses != null && responses.Any(response => lsnAttributeSelector(response.Target) >= selectedGlobalCommittedLsn))
                {
                    // Check if all replicas returned 429, but don't exit early.
                    if (responses.Count > 0 && responses.All(response => response.Target.StatusCode == StatusCodes.TooManyRequests))
                    {
                        DefaultTrace.TraceInformation(
                                    "WaitForWriteBarrierOldAsync: All replicas returned 429 Too Many Requests. Continuing retries. StatusCode: {0}, SubStatusCode: {1}, PkRangeId :{2}.",
                                    responses[0].Target.StatusCode,
                                    responses[0].Target.SubStatusCode,
                                    responses[0].Target.PartitionKeyRangeId);
                        lastAttemptWasThrottled = true;
                    }

                    // Check if any response satisfies the barrier condition
                    if (responses.Any(response => response.Target.GlobalCommittedLSN >= selectedGlobalCommittedLsn))
                    {
                        return (true); // Barrier condition met
                    }
                }

                //get max global committed lsn from current batch of responses, then update if greater than max of all batches.
                long maxGlobalCommittedLsn = responses != null ? responses.Select(s => lsnAttributeSelector(s.Target)).DefaultIfEmpty(0).Max() : 0;
                maxGlobalCommittedLsnReceived = Math.Max(maxGlobalCommittedLsnReceived, maxGlobalCommittedLsn);

                //only refresh on first barrier call, set to false for subsequent attempts.
                barrierRequest.RequestContext.ForceRefreshAddressCache = false;

                //trace on last retry.
                if (writeBarrierRetryCount == 0)
                {
                    DefaultTrace.TraceInformation("ConsistencyWriter: WaitForWriteBarrierAsync - Last barrier multi-region strong. Responses: {0}",
                        string.Join("; ", responses.Select(r => r.Target)));
                }
                else
                {
                    if ((ConsistencyWriter.maxNumberOfWriteBarrierReadRetries - writeBarrierRetryCount) > ConsistencyWriter.maxShortBarrierRetriesForMultiRegion)
                    {
                        await Task.Delay(ConsistencyWriter.delayBetweenWriteBarrierCallsInMs);
                    }
                    else
                    {
                        await Task.Delay(ConsistencyWriter.shortbarrierRetryIntervalInMsForMultiRegion);
                    }
                }
            }
            if (lastAttemptWasThrottled)
            {
                DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier failed after all retries due to consistent throttling (429). Throwing RequestTimeoutException (408).");
                throw new RequestTimeoutException(RMResources.RequestTimeout, SubStatusCodes.Server_WriteBarrierThrottled);
            }
            DefaultTrace.TraceInformation("ConsistencyWriter: Highest global committed lsn received for write barrier call is {0}", maxGlobalCommittedLsnReceived);

            return false; // Barrier condition not met
        }

        private void ValidateGlobalStrongWriteEndpoint(DocumentServiceRequest barrierRequest)
        {
            // validate that a regional failover has not occurred since the initial write.
            Uri currentEndpoint = barrierRequest.RequestContext.LocationEndpointToRoute;
            if (barrierRequest.RequestContext.GlobalStrongWriteEndpoint != null &&
                barrierRequest.RequestContext.GlobalStrongWriteEndpoint != currentEndpoint)
            {
                DefaultTrace.TraceError(
                    "ConsistencyWriter: Failover detected during strong consistency write. Original write was to endpoint '{0}', but retry is targeting endpoint '{1}'. Failing request.",
                    barrierRequest.RequestContext.GlobalStrongWriteEndpoint,
                    currentEndpoint);

                throw new RequestTimeoutException(
                  string.Format(
                      CultureInfo.CurrentUICulture,
                      "The write operation was initiated in region with endpoint '{0}' but a regional failover occurred. The current attempt is to endpoint '{1}'. The state of the write is ambiguous.",
                      barrierRequest.RequestContext.GlobalStrongWriteEndpoint,
                      currentEndpoint), SubStatusCodes.WriteRegionBarrierChangedMidOperation);
            }
        }


        private async Task<bool> WaitForWriteBarrierNewAsync(
            DocumentServiceRequest barrierRequest,
            long selectedGlobalCommittedLsn,
            Func<StoreResult, long> lsnAttributeSelector)
        {
            TimeSpan remainingDelay = totalAllowedBarrierRequestDelay;

            int writeBarrierRetryCount = 0;
            long maxGlobalCommittedLsnReceived = 0;
            bool lastAttemptWasThrottled = false;
            while (writeBarrierRetryCount < defaultBarrierRequestDelays.Length && remainingDelay >= TimeSpan.Zero) // Retry loop
            {
                this.ValidateGlobalStrongWriteEndpoint(barrierRequest);
                barrierRequest.RequestContext.TimeoutHelper.ThrowTimeoutIfElapsed();

                ValueStopwatch barrierRequestStopWatch = ValueStopwatch.StartNew();
                IList<ReferenceCountedDisposable<StoreResult>> responses = await this.storeReader.ReadMultipleReplicaAsync(
                    barrierRequest,
                    includePrimary: true,
                    replicaCountToRead: 1, // any replica with correct globalCommittedLsn is good enough
                    requiresValidLsn: false,
                    useSessionToken: false,
                    readMode: ReadMode.Strong,
                    checkMinLSN: false,
                    forceReadAll: false);
                barrierRequestStopWatch.Stop();

                // Pivot to primary-only if any 410/1022 seen
                if (BarrierRequestHelper.IsGoneLeaseNotFound(responses))
                {
                    bool isSuccess = await this.TryPrimaryOnlyWriteBarrierAsync(barrierRequest, selectedGlobalCommittedLsn);
                    if (isSuccess) return true;

                    barrierRequest.RequestContext.ForceRefreshAddressCache = false;
                }

                TimeSpan previousBarrierRequestLatency = barrierRequestStopWatch.Elapsed;
                long maxGlobalCommittedLsn = 0;
                lastAttemptWasThrottled = false;
                if (responses != null)
                {
                    // Check if all replicas returned 429, but don't exit early.
                    if (responses.Count > 0 && responses.All(response => response.Target.StatusCode == StatusCodes.TooManyRequests))
                    {
                        DefaultTrace.TraceInformation(
                                    "WaitForWriteBarrierNewAsync: All replicas returned 429 Too Many Requests. Continuing retries. StatusCode: {0}, SubStatusCode: {1}, PkRangeId :{2}.",
                                    responses[0].Target.StatusCode,
                                    responses[0].Target.SubStatusCode,
                                    responses[0].Target.PartitionKeyRangeId);
                        lastAttemptWasThrottled = true;
                    }

                    foreach (ReferenceCountedDisposable<StoreResult> response in responses)
                    {
                        long selectedLsn = lsnAttributeSelector(response.Target);
                        if (selectedLsn >= selectedGlobalCommittedLsn)
                        {
                            return true; // Barrier condition met
                        }

                        if (selectedLsn >= maxGlobalCommittedLsn)
                        {
                            maxGlobalCommittedLsn = selectedLsn;
                        }
                     }
                }

                //get max global committed lsn from current batch of responses, then update if greater than max of all batches.
                maxGlobalCommittedLsnReceived = Math.Max(maxGlobalCommittedLsnReceived, maxGlobalCommittedLsn);

                //only refresh on first barrier call, set to false for subsequent attempts.
                barrierRequest.RequestContext.ForceRefreshAddressCache = false;

                bool shouldDelay = BarrierRequestHelper.ShouldDelayBetweenHeadRequests(
                    previousBarrierRequestLatency,
                    responses,
                    defaultBarrierRequestDelays[writeBarrierRetryCount],
                    out TimeSpan delay);

                writeBarrierRetryCount++;
                if (writeBarrierRetryCount >= defaultBarrierRequestDelays.Length || remainingDelay < delay)
                {
                    //trace on last retry.
                    DefaultTrace.TraceInformation("ConsistencyWriter: WaitForWriteBarrierAsync - Last barrier multi-region strong. Target GCLSN: {0}, Max. GCLSN received: {1}, Responses: {2}",
                        selectedGlobalCommittedLsn,
                        maxGlobalCommittedLsn,
                        string.Join("; ", responses.Select(r => r.Target)));

                    break;
                }
                else if (shouldDelay)
                {
                    await Task.Delay(delay);
                    remainingDelay -= delay;
                }
            }
            if (lastAttemptWasThrottled)
            {
                DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier failed after all retries due to consistent throttling (429). Throwing RequestTimeoutException (408).");
                throw new RequestTimeoutException(RMResources.RequestTimeout, SubStatusCodes.Server_WriteBarrierThrottled );
            }
            DefaultTrace.TraceInformation("ConsistencyWriter: Highest global committed lsn received for write barrier call is {0}", maxGlobalCommittedLsnReceived);

            return false;
        }

        /// <summary>
        /// Primary-only write-barrier check. If primary is 410/1022 we bail out (throw) to let region failover happen.
        /// Adds a single forced-address-refresh retry to handle stale primary mapping in the SDK.
        /// </summary>
        private async Task<bool> TryPrimaryOnlyWriteBarrierAsync(
            DocumentServiceRequest barrierRequest,
            long selectedGlobalCommittedLsn)
        {
            this.ValidateGlobalStrongWriteEndpoint(barrierRequest);

            // Always force refresh before hitting primary to avoid stale primary selection
            barrierRequest.RequestContext.ForceRefreshAddressCache = true;
            using (ReferenceCountedDisposable<StoreResult> primaryResult = await this.storeReader.ReadPrimaryAsync(
                barrierRequest,
                requiresValidLsn: false,
                useSessionToken: false))
            {
                if (!primaryResult.Target.IsValid || BarrierRequestHelper.IsGoneLeaseNotFound(primaryResult.Target))
                {
                    // Bail out: propagate the error, do not retry further
                    ExceptionDispatchInfo.Capture(primaryResult.Target.GetException()).Throw();
                }

                return (primaryResult.Target.GlobalCommittedLSN >= selectedGlobalCommittedLsn);
            }
        }

        /// <summary>
        /// Determines the type of write barrier required for a given store result and request.
        /// Returns BarrierType.GlobalStrongWrite if global strong consistency is enabled and required,
        /// BarrierType.NRegionSynchronousCommit if N-region synchronous commit is applicable,
        /// or BarrierType.None if no barrier is needed.
        /// </summary>
        private BarrierType ComputeBarrierType(
            ReferenceCountedDisposable<StoreResult> storeResult,
            DocumentServiceRequest request)
        {
            if (ReplicatedResourceClient.IsGlobalStrongEnabled() && this.ShouldPerformWriteBarrierForGlobalStrong(storeResult.Target, request))
            {
                return BarrierType.GlobalStrongWrite;
            }
            else if (this.ShouldPerformWriteBarrierForLessThanStrong(storeResult.Target))
            {
                return BarrierType.NRegionSynchronousCommit;
            }

            return BarrierType.None;
        }

        /// <summary>
        /// Determines whether a write barrier should be performed for single-master accounts with less than strong consistency
        /// and the N-Region Synchronous Commit feature enabled. Returns true if the account's default consistency
        /// is less than strong, the store result contains a valid GlobalNRegionCommittedGLSN, the feature is enabled,
        /// and there are read regions present.
        /// </summary>
        internal bool ShouldPerformWriteBarrierForLessThanStrong(
            StoreResult storeResult)
        {
            IServiceConfigurationReaderVnext serviceConfigurationReaderVNext = this.serviceConfigReader as IServiceConfigurationReaderVnext;
            bool enableNRegionSynchronousCommit = false;

            if (serviceConfigurationReaderVNext != null)
            {
                enableNRegionSynchronousCommit = serviceConfigurationReaderVNext.EnableNRegionSynchronousCommit;
            }

            return this.serviceConfigReader.DefaultConsistencyLevel != ConsistencyLevel.Strong
                 && storeResult.GlobalNRegionCommittedGLSN != -1
                 && !this.useMultipleWriteLocations
                 && enableNRegionSynchronousCommit
                 && storeResult.NumberOfReadRegions > 0;
        }
    }
}
