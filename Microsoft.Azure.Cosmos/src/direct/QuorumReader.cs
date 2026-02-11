//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    //=================================================================================================================
    // Strong read logic:
    //=================================================================================================================
    //
    //              ------------------- PerformPrimaryRead-------------------------------------------------------------
    //              |                       ^                                                                         |
    //        [RetryOnSecondary]            |                                                                         |
    //              |                   [QuorumNotSelected]                                                           |
    //             \/                      |                                                                         \/
    // Start-------------------------->SecondaryQuorumRead-------------[QuorumMet]-------------------------------->Result
    //                                      |                                                                         ^
    //                                  [QuorumSelected]                                                              |
    //                                      |                                                                         |
    //                                      \/                                                                        |
    //                                  PrimaryReadBarrier-------------------------------------------------------------
    //
    //=================================================================================================================
    // BoundedStaleness quorum read logic:
    //=================================================================================================================
    //
    //              ------------------- PerformPrimaryRead-------------------------------------------------------------
    //              |                       ^                                                                         |
    //        [RetryOnSecondary]            |                                                                         |
    //              |                   [QuorumNotSelected]                                                           |
    //             \/                      |                                                                         \/
    // Start-------------------------->SecondaryQuorumRead-------------[QuorumMet]-------------------------------->Result
    //                                      |                                                                         ^
    //                                  [QuorumSelected]                                                              |
    //                                      |                                                                         |
    //                                      |                                                                         |
    //                                      ---------------------------------------------------------------------------
    /// <summary>
    /// QuorumReader wraps the client side quorum logic on top of the StoreReader
    /// </summary>
    internal sealed class QuorumReader
    {
        private const int maxNumberOfReadBarrierReadRetries = 6;
        private const int maxNumberOfPrimaryReadRetries = 6;
        private const int maxNumberOfReadQuorumRetries = 6;
        private const int delayBetweenReadBarrierCallsInMs = 5;

        private const int maxBarrierRetriesForMultiRegion = 30;
        private const int barrierRetryIntervalInMsForMultiRegion = 30;

        private const int maxShortBarrierRetriesForMultiRegion = 4;
        private const int shortbarrierRetryIntervalInMsForMultiRegion = 10;

        private static readonly TimeSpan[] defaultBarrierRequestDelays = GetDefaultBarrierRequestDelays();
        private static readonly TimeSpan totalAllowedBarrierRequestDelay = GetTotalAllowedBarrierRequestDelay();

        private readonly StoreReader storeReader;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly IAuthorizationTokenProvider authorizationTokenProvider;

        public QuorumReader(
            TransportClient transportClient,
            AddressSelector addressSelector,
            StoreReader storeReader,
            IServiceConfigurationReader serviceConfigReader,
            IAuthorizationTokenProvider authorizationTokenProvider)
        {
            this.storeReader = storeReader;
            this.serviceConfigReader = serviceConfigReader;
            this.authorizationTokenProvider = authorizationTokenProvider;
        }

        public async Task<StoreResponse> ReadStrongAsync(
            DocumentServiceRequest entity,
            int readQuorumValue,
            ReadMode readMode)
        {
            int readQuorumRetry = QuorumReader.maxNumberOfReadQuorumRetries;
            bool shouldRetryOnSecondary = false;
            bool hasPerformedReadFromPrimary = false;
            bool includePrimary = false;

            do
            {
                entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                shouldRetryOnSecondary = false;
                using ReadQuorumResult secondaryQuorumReadResult =
                    await this.ReadQuorumAsync(entity, readQuorumValue, includePrimary, readMode);

                switch (secondaryQuorumReadResult.QuorumResult)
                {

                    case ReadQuorumResultKind.QuorumThrottled:  
                        {
                             ReferenceCountedDisposable<StoreResult> storeResult = secondaryQuorumReadResult.GetSelectedResponseAndSkipStoreResultDispose();
                             // Return the throttled response directly
                             StoreResponse response =  storeResult.Target.ToResponse(entity.RequestContext.RequestChargeTracker);
                             return response;
                        }

                    case ReadQuorumResultKind.QuorumMet:
                        {
                            return secondaryQuorumReadResult.GetResponseAndSkipStoreResultDispose();
                        }

                    case ReadQuorumResultKind.QuorumSelected:
                        {
                            DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(
                                entity,
                                this.authorizationTokenProvider,
                                secondaryQuorumReadResult.SelectedLsn,
                                secondaryQuorumReadResult.GlobalCommittedSelectedLsn);

                            (bool isSuccess, StoreResponse throttledResponse) = await this.WaitForReadBarrierAsync(
                                                barrierRequest,
                                                allowPrimary: true,
                                                readQuorum: readQuorumValue,
                                                readBarrierLsn: secondaryQuorumReadResult.SelectedLsn,
                                                targetGlobalCommittedLSN: secondaryQuorumReadResult.GlobalCommittedSelectedLsn,
                                                readMode: readMode);

                            if (throttledResponse != null)
                            {
                                // Handle throttling by delegating to ResourceThrottleRetryPolicy
                                DefaultTrace.TraceInformation(
                                    "ReadStrongAsync: Throttling occurred during read barrier. Returning throttled response. StatusCode: {0}, SubStatusCode: {1}, PkRangeId :{2}.",
                                    throttledResponse.StatusCode,
                                    throttledResponse.SubStatusCode,
                                    throttledResponse.PartitionKeyRangeId);

                                // Return the real 429 response upstream
                                return throttledResponse;
                            }

                            if (isSuccess)
                            {
                                return secondaryQuorumReadResult.GetResponseAndSkipStoreResultDispose();
                            }

                            DefaultTrace.TraceWarning(
                                "QuorumSelected: Could not converge on the LSN {0} GlobalCommittedLSN {3} ReadMode {4} after primary read barrier with read quorum {1} for strong read, Responses: {2}, resourceType: {5}, operationType: {6}",
                                secondaryQuorumReadResult.SelectedLsn,
                                readQuorumValue,
                                secondaryQuorumReadResult,
                                secondaryQuorumReadResult.GlobalCommittedSelectedLsn,
                                readMode,
                                entity.ResourceType,
                                entity.OperationType);

                            entity.RequestContext.UpdateQuorumSelectedStoreResponse(secondaryQuorumReadResult.GetSelectedResponseAndSkipStoreResultDispose());
                            entity.RequestContext.QuorumSelectedLSN = secondaryQuorumReadResult.SelectedLsn;
                            entity.RequestContext.GlobalCommittedSelectedLSN = secondaryQuorumReadResult.GlobalCommittedSelectedLsn;
                        }

                        break;

                    case ReadQuorumResultKind.QuorumNotSelected:
                        {
                            if (hasPerformedReadFromPrimary)
                            {
                                DefaultTrace.TraceWarning("QuorumNotSelected: Primary read already attempted. Quorum could not be selected after retrying on secondaries. ReadStoreResponses: {0}, resourceType: {1}, operationType: {2}",
                                    secondaryQuorumReadResult.ToString(),
                                    entity.ResourceType,
                                    entity.OperationType);

                                throw new GoneException(RMResources.ReadQuorumNotMet +
                                    $", partitionId: {entity.PartitionKeyRangeIdentity}",
                                    SubStatusCodes.Server_ReadQuorumNotMet);
                            }

                            DefaultTrace.TraceWarning("QuorumNotSelected: Quorum could not be selected with read quorum of {0}, resourceType: {1}, operationType: {2}",
                                readQuorumValue,
                                entity.ResourceType,
                                entity.OperationType);

                            using ReadPrimaryResult response = await this.ReadPrimaryAsync(entity, readQuorumValue, false);

                            if (response.IsSuccessful && response.ShouldRetryOnSecondary)
                            {
                                Debug.Assert(false, "QuorumNotSelected: PrimaryResult has both Successful and ShouldRetryOnSecondary flags set");
                                DefaultTrace.TraceCritical("PrimaryResult has both Successful and ShouldRetryOnSecondary flags set. ReadQuorumResult StoreResponses: {0}, resourceType: {1}, operationType: {2}",
                                    secondaryQuorumReadResult.ToString(),
                                    entity.ResourceType,
                                    entity.OperationType);

                            }
                            else if (response.IsSuccessful)
                            {
                                DefaultTrace.TraceInformation("QuorumNotSelected: ReadPrimary successful, resourceType: {0}, operationType: {1}",
                                    entity.ResourceType,
                                    entity.OperationType);

                                return response.GetResponseAndSkipStoreResultDispose();
                            }
                            else if (response.ShouldRetryOnSecondary)
                            {
                                shouldRetryOnSecondary = true;
                                DefaultTrace.TraceWarning("QuorumNotSelected: ReadPrimary did not succeed. Will retry on secondary. ReadQuorumResult StoreResponses: {0}, resourceType: {1}, operationType: {2}",
                                    secondaryQuorumReadResult.ToString(),
                                    entity.ResourceType,
                                    entity.OperationType);

                                hasPerformedReadFromPrimary = true;

                                // We have failed to select a quorum before - could very well happen again
                                // especially with reduced replica set size (1 Primary and 2 Secondaries
                                // left, one Secondary might be unreachable - due to endpoint health like
                                // service-side crashes or network/connectivity issues). Including the
                                // Primary replica even for quorum selection in this case for the retry
                                includePrimary = true;
                            }
                            else
                            {
                                DefaultTrace.TraceWarning("QuorumNotSelected: Could not get successful response from ReadPrimary, resourceType: {0}, operationType: {1}",
                                    entity.ResourceType,
                                    entity.OperationType);

                                throw new GoneException(RMResources.ReadQuorumNotMet, SubStatusCodes.Server_ReadQuorumNotMet);
                            }
                        }

                        break;

                    default:
                        DefaultTrace.TraceCritical("Unknown ReadQuorum result {0}, resourceType: {1}, operationType: {2}",
                            secondaryQuorumReadResult.QuorumResult.ToString(),
                            entity.ResourceType,
                            entity.OperationType);

                        throw new InternalServerErrorException(RMResources.InternalServerError);
                }
            } while (--readQuorumRetry > 0 && shouldRetryOnSecondary);

            DefaultTrace.TraceWarning("Could not complete read quorum with read quorum value of {0}, resourceType: {1}, operationType: {2}",
                readQuorumValue,
                entity.ResourceType,
                entity.OperationType);

            throw new GoneException(
                    string.Format(CultureInfo.CurrentUICulture,
                    RMResources.ReadQuorumNotMet,
                    readQuorumValue),
                    SubStatusCodes.Server_ReadQuorumNotMet);
        }

        internal static TimeSpan[] GetDefaultBarrierRequestDelays()
        {
            TimeSpan[] delays = new TimeSpan[maxNumberOfReadBarrierReadRetries + maxShortBarrierRetriesForMultiRegion + maxBarrierRetriesForMultiRegion];
            for (int i = 0; i < maxNumberOfReadBarrierReadRetries; i++)
            {
                delays[i] = TimeSpan.FromMilliseconds(delayBetweenReadBarrierCallsInMs);
            }

            for (int i = maxNumberOfReadBarrierReadRetries
                ; i < maxNumberOfReadBarrierReadRetries + maxShortBarrierRetriesForMultiRegion
                ; i++)
            {
                delays[i] = TimeSpan.FromMilliseconds(shortbarrierRetryIntervalInMsForMultiRegion);
            }

            for (int i = maxNumberOfReadBarrierReadRetries + maxShortBarrierRetriesForMultiRegion
                ; i < maxNumberOfReadBarrierReadRetries + maxShortBarrierRetriesForMultiRegion + maxBarrierRetriesForMultiRegion
                ; i++)
            {
                delays[i] = TimeSpan.FromMilliseconds(barrierRetryIntervalInMsForMultiRegion);
            }

            return delays;
        }

        internal static TimeSpan GetTotalAllowedBarrierRequestDelay()
        {
            TimeSpan totalAllowedDelay = TimeSpan.Zero;
            foreach (TimeSpan current in GetDefaultBarrierRequestDelays())
            {
                totalAllowedDelay += current;
            }

            return totalAllowedDelay;
        }

        private async Task<ReadQuorumResult> ReadQuorumAsync(
            DocumentServiceRequest entity,
            int readQuorum,
            bool includePrimary,
            ReadMode readMode)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            long readLsn = -1;
            long globalCommittedLSN = -1;
            ReferenceCountedDisposable<StoreResult> storeResult = null;
            StoreResult[] responsesForLogging = null;
            if (entity.RequestContext.QuorumSelectedStoreResponse == null)
            {
                using StoreResultList disposableResponseResult = new(await this.storeReader.ReadMultipleReplicaAsync(
                    entity,
                    includePrimary: includePrimary,
                    replicaCountToRead: readQuorum,
                    requiresValidLsn: true,
                    useSessionToken: false,
                    readMode: readMode));
                IList<ReferenceCountedDisposable<StoreResult>> responseResult = disposableResponseResult.Value;

                responsesForLogging = new StoreResult[responseResult.Count];
                for (int i = 0; i < responseResult.Count; i++)
                {
                    responsesForLogging[i] = responseResult[i].Target;
                }

                int responseCount = responseResult.Count(response => response.Target.IsValid);
                if (responseCount < readQuorum)
                {
                    return new ReadQuorumResult(entity.RequestContext.RequestChargeTracker,
                        ReadQuorumResultKind.QuorumNotSelected, -1, -1, null, responsesForLogging);
                }

                //either request overrides consistency level with strong, or request does not override and account default consistency level is strong
                bool isGlobalStrongReadCandidate =
                    (ReplicatedResourceClient.IsGlobalStrongEnabled() && this.serviceConfigReader.DefaultConsistencyLevel == ConsistencyLevel.Strong) &&
                    (!entity.RequestContext.OriginalRequestConsistencyLevel.HasValue || entity.RequestContext.OriginalRequestConsistencyLevel == ConsistencyLevel.Strong);

                if (isGlobalStrongReadCandidate && readMode != ReadMode.Strong)
                {
                    DefaultTrace.TraceError("Unexpected difference in consistency level isGlobalStrongReadCandidate {0}, ReadMode: {1}",
                        isGlobalStrongReadCandidate, readMode);
                }

                if (this.IsQuorumMet(
                    responseResult,
                    readQuorum,
                    false,
                    isGlobalStrongReadCandidate,
                    out readLsn,
                    out globalCommittedLSN,
                    out storeResult))
                {
                    return new ReadQuorumResult(
                        entity.RequestContext.RequestChargeTracker,
                        ReadQuorumResultKind.QuorumMet,
                        readLsn,
                        globalCommittedLSN,
                        storeResult,
                        responsesForLogging);
                }

                // at this point, if refresh were necessary, we would have refreshed it in ReadMultipleReplicaAsync
                // so set to false here to avoid further refrehses for this request.
                entity.RequestContext.ForceRefreshAddressCache = false;
            }
            else
            {
                readLsn = entity.RequestContext.QuorumSelectedLSN;
                globalCommittedLSN = entity.RequestContext.GlobalCommittedSelectedLSN;
                storeResult = entity.RequestContext.QuorumSelectedStoreResponse.TryAddReference();
            }

            // ReadBarrier required
            DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(entity, this.authorizationTokenProvider, readLsn, globalCommittedLSN);
            (bool isSuccess, StoreResponse throttledResponse) = await this.WaitForReadBarrierAsync(
                                                barrierRequest,
                                                false,
                                                readQuorum,
                                                readLsn,
                                                globalCommittedLSN,
                                                readMode);
            // Handle the throttled response case first
            if (throttledResponse != null)
            {
                // Create a StoreResult that will throw the correct exception
                using (ReferenceCountedDisposable<StoreResult> throttledStoreResult = StoreResult.CreateStoreResult(
                    storeResponse: throttledResponse,
                    responseException: null,
                    requiresValidLsn: false,
                    useLocalLSNBasedHeaders: false,
                    replicaHealthStatuses: null,
                    storePhysicalAddress: null))
                {
                    return new ReadQuorumResult(
                        entity.RequestContext.RequestChargeTracker,
                        ReadQuorumResultKind.QuorumThrottled,
                        readLsn,
                        globalCommittedLSN,
                        throttledStoreResult.TryAddReference(),  // Pass ownership of the throttled result
                        responsesForLogging);
                }
            }

            if (!isSuccess)
            {
                return new ReadQuorumResult(
                    entity.RequestContext.RequestChargeTracker,
                    ReadQuorumResultKind.QuorumSelected,
                    readLsn,
                    globalCommittedLSN,
                    storeResult,
                    responsesForLogging);
            }

            return new ReadQuorumResult(
                entity.RequestContext.RequestChargeTracker,
                ReadQuorumResultKind.QuorumMet,
                readLsn,
                globalCommittedLSN,
                storeResult,
                responsesForLogging);
        }

        /// <summary>
        /// Read and get response from Primary
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="readQuorum"></param>
        /// <param name="useSessionToken"></param>
        /// <returns></returns>
        private async Task<ReadPrimaryResult> ReadPrimaryAsync(
            DocumentServiceRequest entity,
            int readQuorum,
            bool useSessionToken)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            // We would have already refreshed address before reaching here. Avoid performing here.
            entity.RequestContext.ForceRefreshAddressCache = false;
            using ReferenceCountedDisposable<StoreResult> disposableStoreResult = await this.storeReader.ReadPrimaryAsync(
                entity,
                requiresValidLsn: true,
                useSessionToken: useSessionToken);
            StoreResult storeResult = disposableStoreResult.Target;

            // Local helper to create a throttled ReadPrimaryResult
            ReadPrimaryResult CreateReadPrimaryThrottledResult() =>
                new ReadPrimaryResult(
                    requestChargeTracker: entity.RequestContext.RequestChargeTracker,
                    isSuccessful: true,
                    shouldRetryOnSecondary: false,
                    response: disposableStoreResult.TryAddReference());
            
            // even though the response is throttled it has valid metadata so LSN wont be < 0 hence the below IsValidStatusCodeForExceptionlessRetry  && storeResult.LSN < 0 condition wont trigger throttling response so making a check here
            if (storeResult.StatusCode == StatusCodes.TooManyRequests)
            {
                // Let ResourceThrottleRetryPolicy handle 429
                // Instead of throwing an exception, return a result that indicates throttling
                return CreateReadPrimaryThrottledResult();
            }

            if (!storeResult.IsValid)
            {
                ExceptionDispatchInfo.Capture(storeResult.GetException()).Throw();
            }

            if (entity.IsValidStatusCodeForExceptionlessRetry((int)storeResult.StatusCode, storeResult.SubStatusCode)
                && storeResult.LSN < 0)
            {
                // Exceptionless failures should be treated similar to exceptions
                // Validate LSN for cases where there is no exception because the ReadPrimary has requiresValidLsn: true
                return CreateReadPrimaryThrottledResult();
            }

            if (storeResult.CurrentReplicaSetSize <= 0 || storeResult.LSN < 0 || storeResult.QuorumAckedLSN < 0)
            {
                string message = string.Format(CultureInfo.CurrentCulture,
                    "Invalid value received from response header. CurrentReplicaSetSize {0}, StoreLSN {1}, QuorumAckedLSN {2}",
                    storeResult.CurrentReplicaSetSize, storeResult.LSN, storeResult.QuorumAckedLSN);

                // trace critical only if LSN / QuorumAckedLSN are not returned, since replica set size
                // might not be returned if primary is still building the secondary replicas (during churn)
                if (storeResult.CurrentReplicaSetSize <= 0)
                {
                    DefaultTrace.TraceError(message);
                }
                else
                {
                    DefaultTrace.TraceCritical(message);
                }

                // throw exeption instead of returning inconsistent result.
                throw new GoneException(RMResources.ReadQuorumNotMet, SubStatusCodes.Server_ReadQuorumNotMet);
            }

            if (storeResult.CurrentReplicaSetSize > readQuorum)
            {
                DefaultTrace.TraceWarning(
                    "Unexpected response. Replica Set size is {0} which is greater than min value {1}", storeResult.CurrentReplicaSetSize, readQuorum);
                return new ReadPrimaryResult(requestChargeTracker: entity.RequestContext.RequestChargeTracker, isSuccessful: false, shouldRetryOnSecondary: true, response: null);
            }

            // To accomodate for store latency, where an LSN may be acked by not persisted in the store, we compare the quorum acked LSN and store LSN.
            // In case of sync replication, the store LSN will follow the quorum committed LSN
            // In case of async replication (if enabled for bounded staleness), the store LSN can be ahead of the quorum committed LSN if the primary is able write to faster than secondary acks.
            // We pick higher of the 2 LSN and wait for the other to reach that LSN. 
            if (storeResult.LSN != storeResult.QuorumAckedLSN)
            {
                DefaultTrace.TraceWarning("Store LSN {0} and quorum acked LSN {1} don't match", storeResult.LSN, storeResult.QuorumAckedLSN);
                long higherLsn = storeResult.LSN > storeResult.QuorumAckedLSN ? storeResult.LSN : storeResult.QuorumAckedLSN;

                DocumentServiceRequest waitForLsnRequest = await BarrierRequestHelper.CreateAsync(entity, this.authorizationTokenProvider, higherLsn, null);
                PrimaryReadOutcome primaryWaitForLsnResponse = await this.WaitForPrimaryLsnAsync(waitForLsnRequest, higherLsn, readQuorum);
                if (primaryWaitForLsnResponse == PrimaryReadOutcome.QuorumNotMet)
                {
                    return new ReadPrimaryResult(
                        requestChargeTracker: entity.RequestContext.RequestChargeTracker, isSuccessful: false, shouldRetryOnSecondary: false, response: null);
                }
                else if (primaryWaitForLsnResponse == PrimaryReadOutcome.QuorumInconclusive)
                {
                    return new ReadPrimaryResult(
                        requestChargeTracker: entity.RequestContext.RequestChargeTracker, isSuccessful: false, shouldRetryOnSecondary: true, response: null);
                }

                return new ReadPrimaryResult(
                    requestChargeTracker: entity.RequestContext.RequestChargeTracker, isSuccessful: true, shouldRetryOnSecondary: false, response: disposableStoreResult.TryAddReference());
            }

            return new ReadPrimaryResult(
                requestChargeTracker: entity.RequestContext.RequestChargeTracker, isSuccessful: true, shouldRetryOnSecondary: false, response: disposableStoreResult.TryAddReference());
        }

        private async Task<PrimaryReadOutcome> WaitForPrimaryLsnAsync(
            DocumentServiceRequest barrierRequest,
            long lsnToWaitFor,
            int readQuorum)
        {
            int primaryRetries = QuorumReader.maxNumberOfPrimaryReadRetries;

            do // Loop for store and quorum LSN to match
            {
                barrierRequest.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                // We would have already refreshed address before reaching here. Avoid performing here.
                barrierRequest.RequestContext.ForceRefreshAddressCache = false;
                using ReferenceCountedDisposable<StoreResult> storeResult = await this.storeReader.ReadPrimaryAsync(
                    barrierRequest,
                    requiresValidLsn: true,
                    useSessionToken: false);
                if (!storeResult.Target.IsValid)
                {
                    ExceptionDispatchInfo.Capture(storeResult.Target.GetException()).Throw();
                }

                if (storeResult.Target.CurrentReplicaSetSize > readQuorum)
                {
                    DefaultTrace.TraceWarning(
                        "Unexpected response. Replica Set size is {0} which is greater than min value {1}", storeResult.Target.CurrentReplicaSetSize, readQuorum);
                    return PrimaryReadOutcome.QuorumInconclusive;
                }

                if (storeResult.Target.LSN < lsnToWaitFor || storeResult.Target.QuorumAckedLSN < lsnToWaitFor)
                {
                    DefaultTrace.TraceWarning(
                        "Store LSN {0} or quorum acked LSN {1} are lower than expected LSN {2}", storeResult.Target.LSN, storeResult.Target.QuorumAckedLSN, lsnToWaitFor);
                    await Task.Delay(QuorumReader.delayBetweenReadBarrierCallsInMs);

                    continue;
                }

                return PrimaryReadOutcome.QuorumMet;

            } while (--primaryRetries > 0);

            return PrimaryReadOutcome.QuorumNotMet;
        }


        private Task<(bool isSuccess, StoreResponse throttledResponse)> WaitForReadBarrierAsync(
            DocumentServiceRequest barrierRequest,
            bool allowPrimary,
            int readQuorum,
            long readBarrierLsn,
            long targetGlobalCommittedLSN,
            ReadMode readMode)
        {
            if (BarrierRequestHelper.IsOldBarrierRequestHandlingEnabled)
            {
                return this.WaitForReadBarrierOldAsync(barrierRequest, allowPrimary, readQuorum, readBarrierLsn, targetGlobalCommittedLSN, readMode);
            }

            return this.WaitForReadBarrierNewAsync(barrierRequest, allowPrimary, readQuorum, readBarrierLsn, targetGlobalCommittedLSN, readMode);
        }

        // NOTE this is only temporarily kept to have a feature flag
        // (Env variable 'AZURE_COSMOS_OLD_BARRIER_REQUESTS_HANDLING_ENABLED' allowing to fall back
        // This old implementation will be removed (and the environment
        // variable not been used anymore) after some bake time.
        private async Task<(bool isSuccess, StoreResponse throttledResponse)> WaitForReadBarrierOldAsync(
            DocumentServiceRequest barrierRequest,
            bool allowPrimary,
            int readQuorum,
            long readBarrierLsn,
            long targetGlobalCommittedLSN,
            ReadMode readMode)
        {
            int readBarrierRetryCount = QuorumReader.maxNumberOfReadBarrierReadRetries;
            int readBarrierRetryCountMultiRegion = QuorumReader.maxBarrierRetriesForMultiRegion;

            long maxGlobalCommittedLsn = 0;

            while (readBarrierRetryCount-- > 0) // Retry loop
            {
                barrierRequest.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
                using StoreResultList disposableResponses = new(await this.storeReader.ReadMultipleReplicaAsync(
                    barrierRequest,
                    includePrimary: allowPrimary,
                    replicaCountToRead: readQuorum,
                    requiresValidLsn: true,
                    useSessionToken: false,
                    readMode: readMode,
                    checkMinLSN: false,
                    forceReadAll: true));
                IList<ReferenceCountedDisposable<StoreResult>> responses = disposableResponses.Value;
                // Check if all replicas returned 429
                if (responses.Count > 0 && responses.All(response => response.Target.StatusCode == StatusCodes.TooManyRequests))
                {
                    DefaultTrace.TraceInformation(
                                    "WaitForReadBarrierOldAsync: All replicas returned 429 Too Many Requests. Yielding early to ResourceThrottleRetryPolicy.. StatusCode: {0}, SubStatusCode: {1}, PkRangeId :{2}.",
                                     responses[0].Target.StatusCode,
                                     responses[0].Target.SubStatusCode,
                                     responses[0].Target.PartitionKeyRangeId);

                    return (false, responses.First().Target.ToResponse()); // Return the first 429 response
                }

                //pivot to primary if any 410/1022 seen
                if (BarrierRequestHelper.IsGoneLeaseNotFound(responses))
                {
                    bool isPrimarySuccess = await this.TryPrimaryOnlyReadBarrierAsync(
                        barrierRequest,
                        requiresValidLsn: true,
                        readBarrierLsn: readBarrierLsn,
                        targetGlobalCommittedLSN: targetGlobalCommittedLSN,
                        readQuorum: readQuorum,
                        readMode: readMode);

                    if (isPrimarySuccess)
                    {
                        return (true, null);
                    }

                    barrierRequest.RequestContext.ForceRefreshAddressCache = false;
                    await Task.Delay(QuorumReader.delayBetweenReadBarrierCallsInMs);
                    continue;
                }

                long maxGlobalCommittedLsnInResponses = responses.Count > 0 ? responses.Max(response => response.Target.GlobalCommittedLSN) : 0;
                if ((responses.Count(response => response.Target.LSN >= readBarrierLsn) >= readQuorum) &&
                    (!(targetGlobalCommittedLSN > 0) || maxGlobalCommittedLsnInResponses >= targetGlobalCommittedLSN))
                {
                    return (true, null);
                }

                maxGlobalCommittedLsn = Math.Max(maxGlobalCommittedLsn, maxGlobalCommittedLsnInResponses);

                //only refresh on first barrier call, set to false for subsequent attempts.
                barrierRequest.RequestContext.ForceRefreshAddressCache = false;

                if (readBarrierRetryCount == 0)
                {
                    DefaultTrace.TraceInformation("QuorumReader: WaitForReadBarrierAsync - Last barrier for single-region requests. Responses: {0}",
                        string.Join("; ", responses.Select(r => r.Target)));
                }
                else
                {
                    await Task.Delay(QuorumReader.delayBetweenReadBarrierCallsInMs);
                }
            }

            // we will go into global strong read barrier mode for global strong requests after regular barrier calls have been exhausted.
            if (targetGlobalCommittedLSN > 0)
            {
                while (readBarrierRetryCountMultiRegion-- > 0)
                {
                    barrierRequest.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
                    using StoreResultList disposableResponses = new(await this.storeReader.ReadMultipleReplicaAsync(
                        barrierRequest,
                        includePrimary: allowPrimary,
                        replicaCountToRead: readQuorum,
                        requiresValidLsn: true,
                        useSessionToken: false,
                        readMode: readMode,
                        checkMinLSN: false,
                        forceReadAll: true));
                    IList<ReferenceCountedDisposable<StoreResult>> responses = disposableResponses.Value;

                    // pivot to primary if any 410/1022 seen
                    if (BarrierRequestHelper.IsGoneLeaseNotFound(responses))
                    {
                        bool isPrimarySuccess = await this.TryPrimaryOnlyReadBarrierAsync(
                            barrierRequest,
                            requiresValidLsn: true,
                            readBarrierLsn: readBarrierLsn,
                            targetGlobalCommittedLSN: targetGlobalCommittedLSN,
                            readQuorum: readQuorum,
                            readMode: readMode);

                        if (isPrimarySuccess)
                        {
                            return (true, null);
                        }

                        barrierRequest.RequestContext.ForceRefreshAddressCache = false;
                        if ((QuorumReader.maxBarrierRetriesForMultiRegion - readBarrierRetryCountMultiRegion) > QuorumReader.maxShortBarrierRetriesForMultiRegion)
                        {
                            await Task.Delay(QuorumReader.barrierRetryIntervalInMsForMultiRegion);
                        }
                        else
                        {
                            await Task.Delay(QuorumReader.shortbarrierRetryIntervalInMsForMultiRegion);
                        }
                        continue;
                    }

                    long maxGlobalCommittedLsnInResponses = responses.Count > 0 ? responses.Max(response => response.Target.GlobalCommittedLSN) : 0;
                    if ((responses.Count(response => response.Target.LSN >= readBarrierLsn) >= readQuorum) &&
                        maxGlobalCommittedLsnInResponses >= targetGlobalCommittedLSN)
                    {
                        return (true, null);
                    }

                    maxGlobalCommittedLsn = Math.Max(maxGlobalCommittedLsn, maxGlobalCommittedLsnInResponses);

                    //trace on last retry.
                    if (readBarrierRetryCountMultiRegion == 0)
                    {
                        DefaultTrace.TraceInformation("QuorumReader: WaitForReadBarrierAsync - Last barrier for mult-region strong requests. ReadMode {1} Responses: {0}",
                            string.Join("; ", responses.Select(r => r.Target)), readMode);
                    }
                    else
                    {
                        if ((QuorumReader.maxBarrierRetriesForMultiRegion - readBarrierRetryCountMultiRegion) > QuorumReader.maxShortBarrierRetriesForMultiRegion)
                        {
                            await Task.Delay(QuorumReader.barrierRetryIntervalInMsForMultiRegion);
                        }
                        else
                        {
                            await Task.Delay(QuorumReader.shortbarrierRetryIntervalInMsForMultiRegion);
                        }
                    }
                }
            }

            DefaultTrace.TraceInformation("QuorumReader: WaitForReadBarrierAsync - TargetGlobalCommittedLsn: {0}, MaxGlobalCommittedLsn: {1} ReadMode: {2}.",
                targetGlobalCommittedLSN, maxGlobalCommittedLsn, readMode);
            return (false, null);
        }

        private async Task<(bool isSuccess, StoreResponse throttledResponse)> WaitForReadBarrierNewAsync(
            DocumentServiceRequest barrierRequest,
            bool allowPrimary,
            int readQuorum,
            long readBarrierLsn,
            long targetGlobalCommittedLSN,
            ReadMode readMode)
        {
            TimeSpan remainingDelay = totalAllowedBarrierRequestDelay;

            long maxGlobalCommittedLsn = 0;
            bool hasConvergedOnLSN = false;
            int readBarrierRetryCount = 0;
            while(readBarrierRetryCount < defaultBarrierRequestDelays.Length && remainingDelay >= TimeSpan.Zero) // Retry loop
            {
                barrierRequest.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
                ValueStopwatch barrierRequestStopWatch = ValueStopwatch.StartNew();
                using StoreResultList disposableResponses = new(await this.storeReader.ReadMultipleReplicaAsync(
                    barrierRequest,
                    includePrimary: allowPrimary,
                    replicaCountToRead: hasConvergedOnLSN ? 1 : readQuorum, // for GCLSN a single replica is sufficient
                    requiresValidLsn: !hasConvergedOnLSN,
                    useSessionToken: false,
                    readMode: readMode,
                    checkMinLSN: false,
                    forceReadAll: !hasConvergedOnLSN)); // for GCLSN a single replica is sufficient - and requests should be issued sequentially
                barrierRequestStopWatch.Stop();
                IList<ReferenceCountedDisposable<StoreResult>> responses = disposableResponses.Value;
                // Check if all replicas returned 429
                if (responses.Count > 0 && responses.All(response => response.Target.StatusCode == StatusCodes.TooManyRequests))
                {
                    DefaultTrace.TraceInformation(
                                "WaitForReadBarrierNewAsync: All replicas returned 429 Too Many Requests. Yielding early to ResourceThrottleRetryPolicy. StatusCode: {0}, SubStatusCode: {1}, PkRangeId :{2}.",
                                     responses[0].Target.StatusCode,
                                     responses[0].Target.SubStatusCode,
                                     responses[0].Target.PartitionKeyRangeId);
                    return (false, responses.First().Target.ToResponse());  // Yield early if all replicas return 429
                }


                TimeSpan previousBarrierRequestLatency = barrierRequestStopWatch.Elapsed;

                //pivot to primary if any 410/1022 seen
                if (BarrierRequestHelper.IsGoneLeaseNotFound(responses))
                {
                    bool isPrimarySuccess = await this.TryPrimaryOnlyReadBarrierAsync(
                        barrierRequest,
                        requiresValidLsn: !hasConvergedOnLSN,
                        readBarrierLsn: readBarrierLsn,
                        targetGlobalCommittedLSN: targetGlobalCommittedLSN,
                        readQuorum: readQuorum,
                        readMode: readMode);

                    if (isPrimarySuccess)
                    {
                        return (true, null);
                    }

                    barrierRequest.RequestContext.ForceRefreshAddressCache = false;
                }
                else
                {
                    int readBarrierLsnReachedCount = 0;
                    long maxGlobalCommittedLsnInResponses = 0;
                    foreach(ReferenceCountedDisposable<StoreResult> response in responses)
                    {
                        maxGlobalCommittedLsnInResponses = Math.Max(maxGlobalCommittedLsnInResponses, response.Target.GlobalCommittedLSN);
                        if (!hasConvergedOnLSN && response.Target.LSN >= readBarrierLsn)
                        {
                            readBarrierLsnReachedCount++;
                        }
                    }

                    if (!hasConvergedOnLSN && readBarrierLsnReachedCount >= readQuorum)
                    {
                        hasConvergedOnLSN = true;
                    }

                if (hasConvergedOnLSN &&
                    (targetGlobalCommittedLSN <= 0 || maxGlobalCommittedLsnInResponses >= targetGlobalCommittedLSN))
                {
                    return (true, null);
                }

                    maxGlobalCommittedLsn = Math.Max(maxGlobalCommittedLsn, maxGlobalCommittedLsnInResponses);

                    //only refresh on first barrier call, set to false for subsequent attempts.
                    barrierRequest.RequestContext.ForceRefreshAddressCache = false;

                    bool shouldDelay = BarrierRequestHelper.ShouldDelayBetweenHeadRequests(
                        previousBarrierRequestLatency,
                        responses,
                        defaultBarrierRequestDelays[readBarrierRetryCount],
                        out TimeSpan maxDelay);

                    readBarrierRetryCount++;
                    if (readBarrierRetryCount >= defaultBarrierRequestDelays.Length || remainingDelay <= TimeSpan.Zero)
                    {
                        //trace on last retry.
                        DefaultTrace.TraceInformation(
                            "QuorumReader: WaitForReadBarrierAsync - Last barrier request. ReadMode: {0}, " +
                                "HasLSNConverged: {1}, BarrierRequestRetryCount: {2}, Responses: {3}",
                            readMode,
                            hasConvergedOnLSN,
                            readBarrierRetryCount,
                            string.Join("; ", responses.Select(r => r.Target)));
                    }
                    else if (shouldDelay)
                    {
                        TimeSpan delay =maxDelay < remainingDelay ? maxDelay : remainingDelay;
                        await Task.Delay(delay);
                        remainingDelay -= delay;
                    }
                }
            }

            DefaultTrace.TraceInformation("QuorumReader: WaitForReadBarrierAsync - TargetGlobalCommittedLsn: {0}, MaxGlobalCommittedLsn: {1} ReadMode: {2}, HasLSNConverged:{3}.",
                targetGlobalCommittedLSN, maxGlobalCommittedLsn, readMode, hasConvergedOnLSN);
            return (false, null);
        }

        private bool IsQuorumMet(
            IList<ReferenceCountedDisposable<StoreResult>> readResponses,
            int readQuorum,
            bool isPrimaryIncluded,
            bool isGlobalStrongRead,
            out long readLsn,
            out long globalCommittedLSN,
            out ReferenceCountedDisposable<StoreResult> selectedResponse)
        {
            long maxLsn = 0;
            long minLsn = long.MaxValue;
            int replicaCountMaxLsn = 0;
            IEnumerable<ReferenceCountedDisposable<StoreResult>> validReadResponses = readResponses.Where(response => response.Target.IsValid);
            int validResponsesCount = validReadResponses.Count();

            if (validResponsesCount == 0)
            {
                readLsn = 0;
                globalCommittedLSN = -1;
                selectedResponse = null;

                return false;
            }

            long numberOfReadRegions = validReadResponses.Max(res => res.Target.NumberOfReadRegions);
            bool checkForGlobalStrong = isGlobalStrongRead && numberOfReadRegions > 0;

            // Pick any R replicas in the response and check if they are at the same LSN
            foreach (ReferenceCountedDisposable<StoreResult> response in validReadResponses)
            {
                if (response.Target.LSN == maxLsn)
                {
                    replicaCountMaxLsn++;
                }
                else if (response.Target.LSN > maxLsn)
                {
                    replicaCountMaxLsn = 1;
                    maxLsn = response.Target.LSN;
                }

                if (response.Target.LSN < minLsn)
                {
                    minLsn = response.Target.LSN;
                }
            }

            selectedResponse = validReadResponses.Where(s => (s.Target.LSN == maxLsn) && (s.Target.StatusCode < StatusCodes.StartingErrorCode)).FirstOrDefault();
            if (selectedResponse == null)
            {
                selectedResponse = validReadResponses.First(s => s.Target.LSN == maxLsn);
            }

            readLsn = selectedResponse.Target.ItemLSN == -1 ?
                maxLsn : Math.Min(selectedResponse.Target.ItemLSN, maxLsn);
            globalCommittedLSN = checkForGlobalStrong ? readLsn: -1;

            long maxGlobalCommittedLSN = validReadResponses.Max(res => res.Target.GlobalCommittedLSN);

            // quorum is met if one of the following conditions are satisfied:
            // 1. readLsn is greater than zero 
            //    AND the number of responses that have the same LSN as the selected response is greater than or equal to the read quorum
            //    AND if applicable, the max GlobalCommittedLSN of all responses is greater than or equal to the lsn of the selected response.

            // 2. if the request is a point-read request,
            //    AND there are more than one response in the readResponses
            //    AND the LSN of the returned resource of the selected response is less than or equal to the minimum lsn of the all the responses, 
            //    AND if applicable, the LSN of the returned resource of the selected response is less than or equal to the minimum globalCommittedLsn of all the responses.
            //    This means that the returned resource is old enough to have been committed by at least all the received responses, 
            //    which should be larger than or equal to the read quorum, which therefore means we have strong consistency.
            bool isQuorumMet = false;

            if ((readLsn > 0 && replicaCountMaxLsn >= readQuorum) &&
                (!checkForGlobalStrong || maxGlobalCommittedLSN >= maxLsn))
            {
                isQuorumMet = true;
            }

            if(!isQuorumMet && validResponsesCount >= readQuorum && selectedResponse.Target.ItemLSN != -1 &&
                (minLsn != long.MaxValue && selectedResponse.Target.ItemLSN <= minLsn) &&
                (!checkForGlobalStrong || (selectedResponse.Target.ItemLSN <= maxGlobalCommittedLSN)))
            {
                isQuorumMet = true;
            }

            if (!isQuorumMet)
            {
                DefaultTrace.TraceInformation("QuorumReader: MaxLSN {0} ReplicaCountMaxLSN {1} bCheckGlobalStrong {2} MaxGlobalCommittedLSN {3} NumberOfReadRegions {4} SelectedResponseItemLSN {5}",
                    maxLsn, replicaCountMaxLsn, checkForGlobalStrong, maxGlobalCommittedLSN, numberOfReadRegions, selectedResponse.Target.ItemLSN);
            }

            // `selectedResponse` is an out parameter, so ensure it stays alive.
            selectedResponse = selectedResponse.TryAddReference();
            return isQuorumMet;
        }

        /// <summary>
        /// Primary-only barrier check: if primary is also 410/1022, we bail out by rethrowing the original exception.
        /// Adds a single forced-address-refresh retry to handle stale primary mapping in the SDK.
        /// </summary>
        private async Task<bool> TryPrimaryOnlyReadBarrierAsync(
            DocumentServiceRequest barrierRequest,
            bool requiresValidLsn,
            long readBarrierLsn,
            long targetGlobalCommittedLSN,
            int readQuorum,
            ReadMode readMode)
        {
            // Always force refresh before hitting primary to avoid stale primary selection
            barrierRequest.RequestContext.ForceRefreshAddressCache = true;
            using (ReferenceCountedDisposable<StoreResult> primaryResult = await this.storeReader.ReadPrimaryAsync(
                barrierRequest,
                requiresValidLsn: requiresValidLsn,
                useSessionToken: false))
            {
                if (!primaryResult.Target.IsValid || BarrierRequestHelper.IsGoneLeaseNotFound(primaryResult.Target))
                {
                    // Bail out: propagate the error, do not retry further
                    ExceptionDispatchInfo.Capture(primaryResult.Target.GetException()).Throw();
                }

                bool hasRequiredLsn = readBarrierLsn <= 0 || primaryResult.Target.LSN >= readBarrierLsn;
                bool hasRequiredGlobalCommittedLsn = targetGlobalCommittedLSN <= 0 || primaryResult.Target.GlobalCommittedLSN >= targetGlobalCommittedLSN;
                return (hasRequiredLsn && hasRequiredGlobalCommittedLsn);
            }
        }

        #region PrivateClasses
        private enum ReadQuorumResultKind
        {
            QuorumMet,
            QuorumSelected,
            QuorumNotSelected,
            QuorumThrottled
        }

        private abstract class ReadResult : IDisposable
        {
            private readonly ReferenceCountedDisposable<StoreResult> response;
            private readonly RequestChargeTracker requestChargeTracker;
            private protected bool skipStoreResultDispose;

            protected ReadResult(RequestChargeTracker requestChargeTracker, ReferenceCountedDisposable<StoreResult> response)
            {
                this.requestChargeTracker = requestChargeTracker;
                this.response = response;
            }

            public void Dispose()
            {
                if (this.skipStoreResultDispose)
                {
                    return;
                }

                this.response?.Dispose();
            }

            public StoreResponse GetResponseAndSkipStoreResultDispose()
            {
                if (!this.IsValidResult())
                {
                    DefaultTrace.TraceCritical("GetResponse called for invalid result");
                    throw new InternalServerErrorException(RMResources.InternalServerError);
                }

                this.skipStoreResultDispose = true;
                return this.response.Target.ToResponse(requestChargeTracker);
            }

            protected abstract bool IsValidResult();
        }

        private sealed class ReadQuorumResult : ReadResult, IDisposable
        {
            private readonly ReferenceCountedDisposable<StoreResult> selectedResponse;

            /// <summary>
            /// Only for reporting purposes.
            /// Responses in that list will be disposed by the time when they used for reporting.
            /// ToString is expected to work on the disposed StoreResult.
            /// </summary>
            private readonly StoreResult[] storeResponses;

            public ReadQuorumResult(
                RequestChargeTracker requestChargeTracker,
                ReadQuorumResultKind QuorumResult,
                long selectedLsn,
                long globalCommittedSelectedLsn,
                ReferenceCountedDisposable<StoreResult> selectedResponse,
                StoreResult[] storeResponses)
                : base(requestChargeTracker, selectedResponse)
            {
                this.QuorumResult = QuorumResult;
                this.SelectedLsn = selectedLsn;
                this.GlobalCommittedSelectedLsn = globalCommittedSelectedLsn;
                this.selectedResponse = selectedResponse;
                this.storeResponses = storeResponses;
            }

            public ReadQuorumResultKind QuorumResult { get; private set; }

            public long SelectedLsn { get; private set; }

            public long GlobalCommittedSelectedLsn { get; private set; }

            /// <summary>
            /// Response selected to lock on the LSN. This is the response with the highest
            /// LSN
            /// </summary>
            public ReferenceCountedDisposable<StoreResult> GetSelectedResponseAndSkipStoreResultDispose()
            {
                this.skipStoreResultDispose = true;
                return this.selectedResponse.TryAddReference();
            }

            /// <summary>
            /// Reports performed calls information.
            /// </summary>
            public override string ToString()
            {
                if (this.storeResponses == null) return String.Empty;

                // 1 record uses ~1600 chars on average. 2048 will be the eventual capacity builder will come to with extra array re-size operations.
                StringBuilder stringBuilder = new(capacity: 2048 * this.storeResponses.Length);
                foreach (StoreResult storeResult in this.storeResponses)
                {
                    storeResult.AppendToBuilder(stringBuilder);
                }
                return stringBuilder.ToString();
            }

            protected override bool IsValidResult()
            {
                return this.QuorumResult == ReadQuorumResultKind.QuorumMet || this.QuorumResult == ReadQuorumResultKind.QuorumSelected;
            }
        }

        private sealed class ReadPrimaryResult : ReadResult
        {
            public ReadPrimaryResult(RequestChargeTracker requestChargeTracker, bool isSuccessful, bool shouldRetryOnSecondary, ReferenceCountedDisposable<StoreResult> response)
                : base(requestChargeTracker, response)
            {
                this.IsSuccessful = isSuccessful;
                this.ShouldRetryOnSecondary = shouldRetryOnSecondary;
            }

            public bool ShouldRetryOnSecondary { get; private set; }

            public bool IsSuccessful { get; private set; }

            protected override bool IsValidResult()
            {
                return IsSuccessful;
            }
        }

        private enum PrimaryReadOutcome
        {
            QuorumNotMet,       // Primary LSN is not committed.
            QuorumInconclusive, // Secondary replicas are available. Must read R secondary's to deduce current quorum.
            QuorumMet,
        }

        /// <summary>
        /// Wrapper for a collection of StoreResult list with ability to call dispose on all the items but one selected to be the response.
        /// </summary>
        private struct StoreResultList : IDisposable
        {
            public StoreResultList(IList<ReferenceCountedDisposable<StoreResult>> collection)
            {
                this.Value = collection;
            }

            public IList<ReferenceCountedDisposable<StoreResult>> Value { get; set; }

            public void Dispose()
            {
                if (this.Value.Count > 0)
                {
                    foreach (ReferenceCountedDisposable<StoreResult> storeResult in this.Value)
                    {
                        storeResult?.Dispose();
                    }
                }
            }
        }
        #endregion

    }
}
