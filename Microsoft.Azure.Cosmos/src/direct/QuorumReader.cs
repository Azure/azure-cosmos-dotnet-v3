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
            do
            {
                entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                shouldRetryOnSecondary = false;
                ReadQuorumResult secondaryQuorumReadResult =
                    await this.ReadQuorumAsync(entity, readQuorumValue, false, readMode);
                switch (secondaryQuorumReadResult.QuorumResult)
                {
                    case ReadQuorumResultKind.QuorumMet:
                        {
                            return secondaryQuorumReadResult.GetResponse();
                        }

                    case ReadQuorumResultKind.QuorumSelected:
                        {
                            DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(
                                entity, 
                                this.authorizationTokenProvider, 
                                secondaryQuorumReadResult.SelectedLsn,
                                secondaryQuorumReadResult.GlobalCommittedSelectedLsn);
                            if (await this.WaitForReadBarrierAsync(
                                    barrierRequest, 
                                    allowPrimary: true, 
                                    readQuorum: readQuorumValue,
                                    readBarrierLsn: secondaryQuorumReadResult.SelectedLsn, 
                                    targetGlobalCommittedLSN: secondaryQuorumReadResult.GlobalCommittedSelectedLsn, 
                                    readMode: readMode))
                            {
                                return secondaryQuorumReadResult.GetResponse();
                            }

                            DefaultTrace.TraceWarning(
                                "QuorumSelected: Could not converge on the LSN {0} GlobalCommittedLSN {3} after primary read barrier with read quorum {1} for strong read, Responses: {2}",
                                secondaryQuorumReadResult.SelectedLsn, 
                                readQuorumValue,
                                String.Join(";", secondaryQuorumReadResult.StoreResponses),
                                secondaryQuorumReadResult.GlobalCommittedSelectedLsn);

                            entity.RequestContext.QuorumSelectedStoreResponse = secondaryQuorumReadResult.SelectedResponse;
                            entity.RequestContext.StoreResponses = secondaryQuorumReadResult.StoreResponses;
                            entity.RequestContext.QuorumSelectedLSN = secondaryQuorumReadResult.SelectedLsn;
                            entity.RequestContext.GlobalCommittedSelectedLSN = secondaryQuorumReadResult.GlobalCommittedSelectedLsn;
                        }

                        break;

                    case ReadQuorumResultKind.QuorumNotSelected:
                        {
                            if (hasPerformedReadFromPrimary)
                            {
                                DefaultTrace.TraceWarning("QuorumNotSelected: Primary read already attempted. Quorum could not be selected after retrying on secondaries.");
                                throw new GoneException(RMResources.ReadQuorumNotMet);
                            }

                            DefaultTrace.TraceWarning("QuorumNotSelected: Quorum could not be selected with read quorum of {0}", readQuorumValue);
                            ReadPrimaryResult response = await this.ReadPrimaryAsync(entity, readQuorumValue, false);

                            if (response.IsSuccessful && response.ShouldRetryOnSecondary)
                            {
                                Debug.Assert(false, "QuorumNotSelected: PrimaryResult has both Successful and ShouldRetryOnSecondary flags set");
                                DefaultTrace.TraceCritical("PrimaryResult has both Successful and ShouldRetryOnSecondary flags set");
                            }
                            else if (response.IsSuccessful)
                            {
                                DefaultTrace.TraceInformation("QuorumNotSelected: ReadPrimary successful");
                                return response.GetResponse();
                            }
                            else if (response.ShouldRetryOnSecondary)
                            {
                                shouldRetryOnSecondary = true;
                                DefaultTrace.TraceWarning("QuorumNotSelected: ReadPrimary did not succeed. Will retry on secondary.");
                                hasPerformedReadFromPrimary = true;
                            }
                            else
                            {
                                DefaultTrace.TraceWarning("QuorumNotSelected: Could not get successful response from ReadPrimary");
                                throw new GoneException(RMResources.ReadQuorumNotMet);
                            }
                        }

                        break;

                    default:
                        DefaultTrace.TraceCritical("Unknown ReadQuorum result {0}", secondaryQuorumReadResult.QuorumResult.ToString());
                        throw new InternalServerErrorException(RMResources.InternalServerError);
                }
            } while (--readQuorumRetry > 0 && shouldRetryOnSecondary);

            DefaultTrace.TraceWarning("Could not complete read quorum with read quorum value of {0}", readQuorumValue);

            throw new GoneException(
                    string.Format(CultureInfo.CurrentUICulture,
                    RMResources.ReadQuorumNotMet,
                    readQuorumValue));
        }

        public async Task<StoreResponse> ReadBoundedStalenessAsync(
            DocumentServiceRequest entity,
            int readQuorumValue)
        {
            int readQuorumRetry = QuorumReader.maxNumberOfReadQuorumRetries;
            bool shouldRetryOnSecondary = false;
            bool hasPerformedReadFromPrimary = false;
            do
            {
                entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                shouldRetryOnSecondary = false;
                ReadQuorumResult secondaryQuorumReadResult = await this.ReadQuorumAsync(
                    entity, readQuorumValue, false, ReadMode.BoundedStaleness);
                switch (secondaryQuorumReadResult.QuorumResult)
                {
                    case ReadQuorumResultKind.QuorumMet:
                        {
                            return secondaryQuorumReadResult.GetResponse();
                        }

                    // We do not perform the read barrier on Primary for BoundedStalenss as it has a 
                    // potential to be always caught up in case of async replication
                    case ReadQuorumResultKind.QuorumSelected:

                        DefaultTrace.TraceWarning(
                            "QuorumSelected: Could not converge on LSN {0} after barrier with QuorumValue {1} " +
                            "Will not perform barrier call on Primary for BoundedStaleness, Responses: {2}",
                            secondaryQuorumReadResult.SelectedLsn, readQuorumValue, String.Join(";", secondaryQuorumReadResult.StoreResponses));

                        entity.RequestContext.QuorumSelectedStoreResponse = secondaryQuorumReadResult.SelectedResponse;
                        entity.RequestContext.StoreResponses = secondaryQuorumReadResult.StoreResponses;
                        entity.RequestContext.QuorumSelectedLSN = secondaryQuorumReadResult.SelectedLsn;
                        break;

                    case ReadQuorumResultKind.QuorumNotSelected:
                        {
                            if (hasPerformedReadFromPrimary)
                            {
                                DefaultTrace.TraceWarning("QuorumNotSelected: Primary read already attempted. Quorum could not be selected after " +
                                    "retrying on secondaries.");
                                throw new GoneException(RMResources.ReadQuorumNotMet);
                            }

                            DefaultTrace.TraceWarning("QuorumNotSelected: Quorum could not be selected with read quorum of {0}", readQuorumValue);
                            ReadPrimaryResult response = await this.ReadPrimaryAsync(entity, readQuorumValue, false);

                            if (response.IsSuccessful && response.ShouldRetryOnSecondary)
                            {
                                Debug.Assert(false, "QuorumNotSelected: PrimaryResult has both Successful and ShouldRetryOnSecondary flags set");
                                DefaultTrace.TraceCritical("QuorumNotSelected: PrimaryResult has both Successful and ShouldRetryOnSecondary flags set");
                            }
                            else if (response.IsSuccessful)
                            {
                                DefaultTrace.TraceInformation("QuorumNotSelected: ReadPrimary successful");
                                return response.GetResponse();
                            }
                            else if (response.ShouldRetryOnSecondary)
                            {
                                shouldRetryOnSecondary = true;
                                DefaultTrace.TraceWarning("QuorumNotSelected: ReadPrimary did not succeed. Will retry on secondary.");
                                hasPerformedReadFromPrimary = true;
                            }
                            else
                            {
                                DefaultTrace.TraceWarning("QuorumNotSelected: Could not get successful response from ReadPrimary");
                                throw new GoneException(RMResources.ReadQuorumNotMet);
                            }
                        }
                        break;

                    default:
                        DefaultTrace.TraceCritical("Unknown ReadQuorum result {0}", secondaryQuorumReadResult.QuorumResult.ToString());
                        throw new InternalServerErrorException(RMResources.InternalServerError);
                }
            } while (--readQuorumRetry > 0 && shouldRetryOnSecondary);

            DefaultTrace.TraceError("Could not complete read quorum with read quorum value of {0}, RetryCount: {1}",
                readQuorumValue,
                QuorumReader.maxNumberOfReadQuorumRetries);

            throw new GoneException(
                    string.Format(CultureInfo.CurrentUICulture,
                    RMResources.ReadQuorumNotMet,
                    readQuorumValue));
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
            StoreResult storeResult = null;
            List<string> storeResponses = null;
            if (entity.RequestContext.QuorumSelectedStoreResponse == null)
            {
                IList<StoreResult> responseResult = await this.storeReader.ReadMultipleReplicaAsync(
                    entity, 
                    includePrimary: includePrimary,
                    replicaCountToRead: readQuorum, 
                    requiresValidLsn: true,
                    useSessionToken: false, 
                    readMode: readMode);

                storeResponses = responseResult.Select(response => response.ToString()).ToList();

                int responseCount = responseResult.Count(response => response.IsValid);
                if (responseCount < readQuorum)
                {
                    return new ReadQuorumResult(entity.RequestContext.RequestChargeTracker,
                        ReadQuorumResultKind.QuorumNotSelected, -1, -1, null, storeResponses);
                }

                //either request overrides consistency level with strong, or request does not override and account default consistency level is strong
                bool isGlobalStrongReadCandidate =
                    (ReplicatedResourceClient.IsGlobalStrongEnabled() && this.serviceConfigReader.DefaultConsistencyLevel == ConsistencyLevel.Strong) &&
                    (!entity.RequestContext.OriginalRequestConsistencyLevel.HasValue || entity.RequestContext.OriginalRequestConsistencyLevel == ConsistencyLevel.Strong);

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
                        storeResponses);
                }

                // at this point, if refresh were necessary, we would have refreshed it in ReadMultipleReplicaAsync
                // so set to false here to avoid further refrehses for this request.
                entity.RequestContext.ForceRefreshAddressCache = false;
            }
            else
            {
                readLsn = entity.RequestContext.QuorumSelectedLSN;
                globalCommittedLSN = entity.RequestContext.GlobalCommittedSelectedLSN;
                storeResult = entity.RequestContext.QuorumSelectedStoreResponse;
                storeResponses = entity.RequestContext.StoreResponses;
            }

            // ReadBarrier required
            DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(entity, this.authorizationTokenProvider, readLsn, globalCommittedLSN);
            if (!await this.WaitForReadBarrierAsync(barrierRequest, false, readQuorum, readLsn, globalCommittedLSN, readMode))
            {
                return new ReadQuorumResult(
                    entity.RequestContext.RequestChargeTracker,
                    ReadQuorumResultKind.QuorumSelected,
                    readLsn,
                    globalCommittedLSN,
                    storeResult,
                    storeResponses);
            }

            return new ReadQuorumResult(
                entity.RequestContext.RequestChargeTracker,
                ReadQuorumResultKind.QuorumMet,
                readLsn,
                globalCommittedLSN,
                storeResult,
                storeResponses);
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
            StoreResult storeResult = await this.storeReader.ReadPrimaryAsync(
                entity, 
                requiresValidLsn: true, 
                useSessionToken: useSessionToken);
            if (!storeResult.IsValid)
            {
                ExceptionDispatchInfo.Capture(storeResult.GetException()).Throw();
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
                throw new GoneException(RMResources.ReadQuorumNotMet);
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
                    requestChargeTracker: entity.RequestContext.RequestChargeTracker, isSuccessful: true, shouldRetryOnSecondary: false, response: storeResult);
            }

            return new ReadPrimaryResult(
                requestChargeTracker: entity.RequestContext.RequestChargeTracker, isSuccessful: true, shouldRetryOnSecondary: false, response: storeResult);
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
                StoreResult storeResult = await this.storeReader.ReadPrimaryAsync(
                    barrierRequest, 
                    requiresValidLsn: true, 
                    useSessionToken: false);
                if (!storeResult.IsValid)
                {
                    ExceptionDispatchInfo.Capture(storeResult.GetException()).Throw();

                }

                if (storeResult.CurrentReplicaSetSize > readQuorum)
                {
                    DefaultTrace.TraceWarning(
                        "Unexpected response. Replica Set size is {0} which is greater than min value {1}", storeResult.CurrentReplicaSetSize, readQuorum);
                    return PrimaryReadOutcome.QuorumInconclusive;
                }

                if (storeResult.LSN < lsnToWaitFor || storeResult.QuorumAckedLSN < lsnToWaitFor)
                {
                    DefaultTrace.TraceWarning(
                        "Store LSN {0} or quorum acked LSN {1} are lower than expected LSN {2}", storeResult.LSN, storeResult.QuorumAckedLSN, lsnToWaitFor);

                    await Task.Delay(QuorumReader.delayBetweenReadBarrierCallsInMs);

                    continue;
                }

                return PrimaryReadOutcome.QuorumMet;

            } while (--primaryRetries > 0);

            return PrimaryReadOutcome.QuorumNotMet;
        }

        private async Task<bool> WaitForReadBarrierAsync(
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
                IList<StoreResult> responses = await this.storeReader.ReadMultipleReplicaAsync(
                    barrierRequest,
                    includePrimary: allowPrimary,
                    replicaCountToRead: readQuorum,
                    requiresValidLsn: true, 
                    useSessionToken: false, 
                    readMode: readMode, 
                    checkMinLSN: false, 
                    forceReadAll: true);

                long maxGlobalCommittedLsnInResponses = responses.Count > 0 ? responses.Max(response => response.GlobalCommittedLSN) : 0;
                if ((responses.Count(response => response.LSN >= readBarrierLsn) >= readQuorum) &&
                    (!(targetGlobalCommittedLSN > 0) || maxGlobalCommittedLsnInResponses >= targetGlobalCommittedLSN))
                {
                    return true;
                }

                maxGlobalCommittedLsn = maxGlobalCommittedLsn > maxGlobalCommittedLsnInResponses ?
                    maxGlobalCommittedLsn : maxGlobalCommittedLsnInResponses;

                //only refresh on first barrier call, set to false for subsequent attempts.
                barrierRequest.RequestContext.ForceRefreshAddressCache = false;

                if (readBarrierRetryCount == 0)
                {
                    DefaultTrace.TraceInformation("QuorumReader: WaitForReadBarrierAsync - Last barrier for single-region requests. Responses: {0}",
                        string.Join("; ", responses));
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
                    IList<StoreResult> responses = await this.storeReader.ReadMultipleReplicaAsync(
                        barrierRequest, 
                        includePrimary: allowPrimary, 
                        replicaCountToRead: readQuorum,
                        requiresValidLsn: true, 
                        useSessionToken: false,
                        readMode: readMode, 
                        checkMinLSN: false, 
                        forceReadAll: true);

                    long maxGlobalCommittedLsnInResponses = responses.Count > 0 ? responses.Max(response => response.GlobalCommittedLSN) : 0;
                    if ((responses.Count(response => response.LSN >= readBarrierLsn) >= readQuorum) &&
                        maxGlobalCommittedLsnInResponses >= targetGlobalCommittedLSN)
                    {
                        return true;
                    }

                    maxGlobalCommittedLsn = maxGlobalCommittedLsn > maxGlobalCommittedLsnInResponses ?
                        maxGlobalCommittedLsn : maxGlobalCommittedLsnInResponses;

                    //trace on last retry.
                    if (readBarrierRetryCountMultiRegion == 0)
                    {
                        DefaultTrace.TraceInformation("QuorumReader: WaitForReadBarrierAsync - Last barrier for mult-region strong requests. Responses: {0}", 
                            string.Join("; ", responses));
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

            DefaultTrace.TraceInformation("QuorumReader: WaitForReadBarrierAsync - TargetGlobalCommittedLsn: {0}, MaxGlobalCommittedLsn: {1}.", targetGlobalCommittedLSN, maxGlobalCommittedLsn);
            return false;
        }

        private bool IsQuorumMet(
            IList<StoreResult> readResponses,
            int readQuorum,
            bool isPrimaryIncluded,
            bool isGlobalStrongRead,
            out long readLsn,
            out long globalCommittedLSN,
            out StoreResult selectedResponse)
        {
            long maxLsn = 0;
            long minLsn = long.MaxValue;
            int replicaCountMaxLsn = 0;
            IEnumerable<StoreResult> validReadResponses = readResponses.Where(response => response.IsValid);
            int validResponsesCount = validReadResponses.Count();

            if (validResponsesCount == 0)
            {
                readLsn = 0;
                globalCommittedLSN = -1;
                selectedResponse = null;

                return false;
            }

            long numberOfReadRegions = validReadResponses.Max(res => res.NumberOfReadRegions);
            bool checkForGlobalStrong = isGlobalStrongRead && numberOfReadRegions > 0;

            // Pick any R replicas in the response and check if they are at the same LSN
            foreach (StoreResult response in validReadResponses)
            {
                if (response.LSN == maxLsn)
                {
                    replicaCountMaxLsn++;
                }
                else if (response.LSN > maxLsn)
                {
                    replicaCountMaxLsn = 1;
                    maxLsn = response.LSN;
                }

                if (response.LSN < minLsn)
                {
                    minLsn = response.LSN;
                }
            }

            selectedResponse = validReadResponses.Where(s => (s.LSN == maxLsn) && (s.StatusCode < StatusCodes.StartingErrorCode)).FirstOrDefault();
            if (selectedResponse == null)
            {
                selectedResponse = validReadResponses.First(s => s.LSN == maxLsn);
            }

            readLsn = selectedResponse.ItemLSN == -1 ? 
                maxLsn : Math.Min(selectedResponse.ItemLSN, maxLsn);
            globalCommittedLSN = checkForGlobalStrong ? readLsn: -1;

            long maxGlobalCommittedLSN = validReadResponses.Max(res => res.GlobalCommittedLSN);
            
            DefaultTrace.TraceInformation("QuorumReader: MaxLSN {0} ReplicaCountMaxLSN {1} bCheckGlobalStrong {2} MaxGlobalCommittedLSN {3} NumberOfReadRegions {4} SelectedResponseItemLSN {5}",
                maxLsn, replicaCountMaxLsn, checkForGlobalStrong, maxGlobalCommittedLSN, numberOfReadRegions, selectedResponse.ItemLSN);
            
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

            if(!isQuorumMet && validResponsesCount >= readQuorum && selectedResponse.ItemLSN != -1 &&
                (minLsn != long.MaxValue && selectedResponse.ItemLSN <= minLsn) &&
                (!checkForGlobalStrong || (selectedResponse.ItemLSN <= maxGlobalCommittedLSN)))
            {
                isQuorumMet = true;    
            }
            
            return isQuorumMet;
        }

        #region PrivateClasses
        private enum ReadQuorumResultKind
        {
            QuorumMet,
            QuorumSelected,
            QuorumNotSelected
        }

        private abstract class ReadResult
        {
            private readonly StoreResult response;
            private readonly RequestChargeTracker requestChargeTracker;

            protected ReadResult(RequestChargeTracker requestChargeTracker, StoreResult response)
            {
                this.requestChargeTracker = requestChargeTracker;
                this.response = response;
            }

            public StoreResponse GetResponse()
            {
                if (!this.IsValidResult())
                {
                    DefaultTrace.TraceCritical("GetResponse called for invalid result");
                    throw new InternalServerErrorException(RMResources.InternalServerError);
                }

                return this.response.ToResponse(requestChargeTracker);
            }

            protected abstract bool IsValidResult();
        }

        private sealed class ReadQuorumResult : ReadResult
        {
            public ReadQuorumResult(
                RequestChargeTracker requestChargeTracker,
                ReadQuorumResultKind QuorumResult,
                long selectedLsn,
                long globalCommittedSelectedLsn,
                StoreResult selectedResponse,
                List<string> storeResponses)
                : base(requestChargeTracker, selectedResponse)
            {
                this.QuorumResult = QuorumResult;
                this.SelectedLsn = selectedLsn;
                this.GlobalCommittedSelectedLsn = globalCommittedSelectedLsn;
                this.SelectedResponse = selectedResponse;
                this.StoreResponses = storeResponses;
            }

            public ReadQuorumResultKind QuorumResult { get; private set; }

            /// <summary>
            /// Response selected to lock on the LSN. This is the response with the highest
            /// LSN
            /// </summary>
            public StoreResult SelectedResponse { get; private set; }

            /// <summary>
            /// All store responses from Quorum Read.
            /// </summary>
            public List<string> StoreResponses { get; private set; }

            public long SelectedLsn { get; private set; }

            public long GlobalCommittedSelectedLsn { get; private set; }

            protected override bool IsValidResult()
            {
                return this.QuorumResult == ReadQuorumResultKind.QuorumMet || this.QuorumResult == ReadQuorumResultKind.QuorumSelected;
            }
        }

        private sealed class ReadPrimaryResult : ReadResult
        {
            public ReadPrimaryResult(RequestChargeTracker requestChargeTracker, bool isSuccessful, bool shouldRetryOnSecondary, StoreResult response)
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

        #endregion
    }
}
