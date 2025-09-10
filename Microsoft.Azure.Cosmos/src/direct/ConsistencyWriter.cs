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
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;

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
3. If GlobalCommittedLSN == LSN, return response to caller
4. If GlobalCommittedLSN < LSN, cache LSN in request as SelectedGlobalCommittedLSN, and issue barrier requests against any/all replicas.
5. Each barrier response will contain its own LSN and GlobalCommittedLSN, check for any response that satisfies GlobalCommittedLSN >= SelectedGlobalCommittedLSN
6. Return to caller on success.

For Less than Strong accounts with EnableNRegionSynchronousCommit feature enabled:
Business logic: We send single request to primary of the Write region,which will take care of replicating to its secondaries, one of which is XPPrimary. XPPrimary in this case will replicate this request to n read regions, which will ack from within their region.
    In the write region where the original request was sent to , the request returns from the backend once write quorum number of replicas commits the write - but at this time, the response cannot be returned to caller, since linearizability guarantees will be violated.
    ConsistencyWriter will continuously issue barrier head requests against the partition in question, until GlobalNRegionCommittedGLSN is at least as big as the lsn of the original response.
Sequence of steps:
1. After receiving response from primary of write region, look at GlobalNRegionCommittedGLSN and LSN headers.
2. If GlobalNRegionCommittedGLSN == LSN, return response to caller
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
        private readonly IAuthorizationTokenProvider authorizationTokenProvider;
        private readonly bool useMultipleWriteLocations;
        private readonly ISessionRetryOptions sessionRetryOptions;
        private readonly AccountConfigurationProperties accountConfigurationProperties;

        public ConsistencyWriter(
            AddressSelector addressSelector,
            ISessionContainer sessionContainer,
            TransportClient transportClient,
            IServiceConfigurationReader serviceConfigReader,
            IAuthorizationTokenProvider authorizationTokenProvider,
            bool useMultipleWriteLocations,
            bool enableReplicaValidation,
            AccountConfigurationProperties accountConfigurationProperties,
            ISessionRetryOptions sessionRetryOptions = null)
        {
            this.transportClient = transportClient;
            this.addressSelector = addressSelector;
            this.sessionContainer = sessionContainer;
            this.serviceConfigReader = serviceConfigReader;
            this.authorizationTokenProvider = authorizationTokenProvider;
            this.useMultipleWriteLocations = useMultipleWriteLocations;
            this.sessionRetryOptions = sessionRetryOptions;
            this.storeReader = new StoreReader(
                                    transportClient,
                                    addressSelector,
                                    new AddressEnumerator(),
                                    sessionContainer: null,
                                    enableReplicaValidation); //we need store reader only for global strong, no session is needed*/
            this.accountConfigurationProperties = accountConfigurationProperties;
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

            if (request.RequestContext.GlobalStrongWriteStoreResult == null)
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

                WriteBarrierKind barrierKind = this.ComputeBarrierKind(storeResult, request);
                if (barrierKind == WriteBarrierKind.None)
                {
                    // If barrier is not performed, we can return the store result directly.
                    return storeResult.Target.ToResponse();
                }

                await this.TryBarrierRequestForWritesAsync(storeResult, request, barrierKind);

            }
            else
            {
                WriteBarrierKind barrierKind = this.ComputeBarrierKind(request.RequestContext.GlobalStrongWriteStoreResult, request);
                using (DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(request, this.authorizationTokenProvider, null,
                    request.RequestContext.GlobalCommittedSelectedLSN,
                    includeRegionContext: true))
                {
                    Func<StoreResult, long> lsnAttributeSelector = barrierKind == WriteBarrierKind.GlobalStrongWrite ? (sr => sr.GlobalCommittedLSN) : (sr => sr.GlobalNRegionCommittedGLSN);
                    if (!await this.WaitForWriteBarrierAsync(barrierRequest, request.RequestContext.GlobalCommittedSelectedLSN,
                        lsnAttributeSelector))
                    {
                        if (barrierKind == WriteBarrierKind.GlobalStrongWrite)
                        {
                            DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier has not been met for global strong request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                            throw new GoneException(RMResources.GlobalStrongWriteBarrierNotMet, SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet);
                        }
                        else
                        {
                            DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier has not been met for n region synchronous commit request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                            throw new GoneException(RMResources.NRegionCommitSynchronousWriteBarrierNotMet, SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet);
                        }

                    }
                }
            }

            return request.RequestContext.GlobalStrongWriteStoreResult.Target.ToResponse();
        }

        internal bool ShouldPerformWriteBarrierForGlobalStrong(StoreResult storeResult, OperationType operationType)
        {
            if (operationType.IsSkippedForWriteBarrier())
            {
                return false;
            }

            if (storeResult.StatusCode < StatusCodes.StartingErrorCode ||
                storeResult.StatusCode == StatusCodes.Conflict ||
                (storeResult.StatusCode == StatusCodes.NotFound && storeResult.SubStatusCode != SubStatusCodes.ReadSessionNotAvailable) ||
                storeResult.StatusCode == StatusCodes.PreconditionFailed)
            {
                if (this.serviceConfigReader.DefaultConsistencyLevel == ConsistencyLevel.Strong && storeResult.NumberOfReadRegions > 0)
                {
                    return true;
                }
            }

            return false;
        }
#pragma warning disable CS1570
        /** <summary>
        Attempt barrier requests if applicable.
        Cases in which barrier head requests are applicable (refer to comments on the class definition for more details on the whole write protocol.
          For globally strong write:
              1. After receiving response from primary of write region, look at GlobalCommittedLsn and LSN headers.
              2. If GlobalCommittedLSN == LSN, return response to caller
              3. If GlobalCommittedLSN < LSN, cache LSN in request as SelectedGlobalCommittedLSN, and issue barrier requests against any/all replicas.
              4. Each barrier response will contain its own LSN and GlobalCommittedLSN, check for any response that satisfies GlobalCommittedLSN >= SelectedGlobalCommittedLSN
              5. Return to caller on success.
          For less than Strong Accounts and If EnableNRegionSynchronousCommit is enabled for the account:
              1. After receiving response from primary of write region, look at GlobalCommittedLsn and LSN headers.
              2. If GlobalNRegionCommittedGLSN == LSN, return response to caller
              3. If GlobalNRegionCommittedGLSN < LSN && storeResponse.NumberOFReadRegions > 0 , cache LSN in request as SelectedGlobalNRegionCommittedGLSN, and issue barrier requests against any/all replicas.
              4. Each barrier response will contain its own LSN and GlobalNRegionCommittedGLSN, check for any response that satisfies GlobalNRegionCommittedGLSN >= SelectedGlobalNRegionCommittedGLSN
              5. Return to caller on success.
        **/
#pragma warning restore CS1570
        private async Task<bool> TryBarrierRequestForWritesAsync(ReferenceCountedDisposable<StoreResult> storeResult, DocumentServiceRequest request, WriteBarrierKind barrierKind)
        {
            long lsn = storeResult.Target.LSN;
            long globalCommitLsnToBeTracked = -1;
            Func<StoreResult, long> lsnAttributeSelector = null;

            //No need to run any barriers.
            if (barrierKind == WriteBarrierKind.None)
            {
                return true;
            }

            string warningMessage;
            if (barrierKind == WriteBarrierKind.GlobalStrongWrite)
            {
                globalCommitLsnToBeTracked = storeResult.Target.GlobalCommittedLSN;
                warningMessage = "ConsistencyWriter: LSN {0} or GlobalCommittedLsn {1} is not set for global strong request";

                DefaultTrace.TraceInformation("ConsistencyWriter: globalCommittedLsn {0}, lsn {1}", globalCommitLsnToBeTracked, lsn);
                lsnAttributeSelector = sr => sr.GlobalCommittedLSN;
            }
            else
            {
                globalCommitLsnToBeTracked = storeResult.Target.GlobalNRegionCommittedGLSN;
                warningMessage = "ConsistencyWriter: LSN {0} or globalNRegionCommittedLsn {1} is not set for less than strong request with EnableNRegionSynchronousCommit property enabled ";
                lsnAttributeSelector = sr => sr.GlobalNRegionCommittedGLSN;
            }

            if (lsn == -1 || globalCommitLsnToBeTracked == -1)
            {
                DefaultTrace.TraceWarning(warningMessage, lsn, globalCommitLsnToBeTracked);
                // Service Generated because no lsn and glsn set by service
                throw new GoneException(RMResources.Gone, SubStatusCodes.ServerGenerated410);
            }

            request.RequestContext.GlobalCommittedSelectedLSN = lsn;
            request.RequestContext.GlobalStrongWriteStoreResult = storeResult;

            //if necessary we would have already refreshed cache by now.
            request.RequestContext.ForceRefreshAddressCache = false;

            //barrier only if necessary, i.e. when write region completes write, but read regions have not.
            if (globalCommitLsnToBeTracked < lsn)
            {
#pragma warning disable SA1001 // Commas should be spaced correctly
                using (DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(request,
                    this.authorizationTokenProvider, null,
                    request.RequestContext.GlobalCommittedSelectedLSN,
                    includeRegionContext: true))
                {
                    if (!await this.WaitForWriteBarrierAsync(barrierRequest, request.RequestContext.GlobalCommittedSelectedLSN, lsnAttributeSelector))
                    {
                        if (barrierKind == WriteBarrierKind.GlobalStrongWrite)
                        {
                            DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier has not been met for global strong request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                            throw new GoneException(RMResources.GlobalStrongWriteBarrierNotMet, SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet);
                        }
                        else
                        {
                            DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier has not been met for n region synchronous commit request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                            throw new GoneException(RMResources.NRegionCommitSynchronousWriteBarrierNotMet, SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet);
                        }
                    }
                }
#pragma warning restore SA1001 // Commas should be spaced correctly
            }
            return true;
        }

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
        private async Task<bool> WaitForWriteBarrierOldAsync(DocumentServiceRequest barrierRequest, long selectedGlobalCommittedLsn,
             Func<StoreResult, long> lsnAttributeSelector)
        {
            int writeBarrierRetryCount = ConsistencyWriter.maxNumberOfWriteBarrierReadRetries;

            long maxGlobalCommittedLsnReceived = 0;
            while (writeBarrierRetryCount-- > 0)
            {
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

                if (responses != null && responses.Any(response => lsnAttributeSelector(response.Target) >= selectedGlobalCommittedLsn))
                {
                    return true;
                }

                //get max global committed lsn from current batch of responses, then update if greater than max of all batches.
                long maxGlobalCommittedLsn = responses != null ? responses.Select(s => lsnAttributeSelector(s.Target)).DefaultIfEmpty(0).Max() : 0;
                maxGlobalCommittedLsnReceived = maxGlobalCommittedLsnReceived > maxGlobalCommittedLsn ? maxGlobalCommittedLsnReceived : maxGlobalCommittedLsn;

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

            DefaultTrace.TraceInformation("ConsistencyWriter: Highest global committed lsn received for write barrier call is {0}", maxGlobalCommittedLsnReceived);

            return false;
        }

        private async Task<bool> WaitForWriteBarrierNewAsync(
            DocumentServiceRequest barrierRequest,
            long selectedGlobalCommittedLsn,
            Func<StoreResult, long> lsnAttributeSelector)
        {
            TimeSpan remainingDelay = totalAllowedBarrierRequestDelay;

            int writeBarrierRetryCount = 0;
            long maxGlobalCommittedLsnReceived = 0;
#pragma warning disable SA1108
            while (writeBarrierRetryCount < defaultBarrierRequestDelays.Length && remainingDelay >= TimeSpan.Zero) // Retry loop
#pragma warning restore SA1108
            {
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

                TimeSpan previousBarrierRequestLatency = barrierRequestStopWatch.Elapsed;
                long maxGlobalCommittedLsn = 0;
                if (responses != null)
                {
                    foreach (ReferenceCountedDisposable<StoreResult> response in responses)
                    {
                        long selectedLsn = lsnAttributeSelector(response.Target);
                        if (selectedLsn >= selectedGlobalCommittedLsn)
                        {
                            return true;
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

            DefaultTrace.TraceInformation("ConsistencyWriter: Highest global committed lsn received for write barrier call is {0}", maxGlobalCommittedLsnReceived);

            return false;
        }

        public enum WriteBarrierKind
        {
            None = 0,                   // No barrier needed
            GlobalStrongWrite = 1,      // Barrier for global strong consistency writes
            NRegionSynchronousCommit = 2 // Barrier for N-region synchronous commit writes
        }

        private WriteBarrierKind ComputeBarrierKind(ReferenceCountedDisposable<StoreResult> storeResult, DocumentServiceRequest request)
        {
            if (ReplicatedResourceClient.IsGlobalStrongEnabled() && this.ShouldPerformWriteBarrierForGlobalStrong(storeResult.Target, request.OperationType))
            {
                return WriteBarrierKind.GlobalStrongWrite;
            }
            else if (this.serviceConfigReader.DefaultConsistencyLevel != ConsistencyLevel.Strong
                 && storeResult.Target.GlobalNRegionCommittedGLSN != -1
                 && this.accountConfigurationProperties.EnableNRegionSynchronousCommit && storeResult.Target.NumberOfReadRegions > 0)
            {
                return WriteBarrierKind.NRegionSynchronousCommit;
            }
            return WriteBarrierKind.None;
        }
    }
}
