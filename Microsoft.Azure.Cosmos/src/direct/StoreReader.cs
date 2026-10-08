//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class StoreReader
    {
        private readonly TransportClient transportClient;
        private readonly AddressSelector addressSelector;
        private readonly IAddressEnumerator addressEnumerator;
        private readonly ISessionContainer sessionContainer;
        private readonly bool canUseLocalLSNBasedHeaders;
        private readonly bool isReplicaAddressValidationEnabled;

        public StoreReader(
            TransportClient transportClient,
            AddressSelector addressSelector,
            IAddressEnumerator addressEnumerator,
            ISessionContainer sessionContainer,
            bool enableReplicaValidation)
        {
            this.transportClient = transportClient;
            this.addressSelector = addressSelector;
            this.addressEnumerator = addressEnumerator ?? throw new ArgumentNullException(nameof(addressEnumerator));
            this.sessionContainer = sessionContainer;
            this.canUseLocalLSNBasedHeaders = VersionUtility.IsLaterThan(HttpConstants.Versions.CurrentVersion, HttpConstants.Versions.v2018_06_18);
            this.isReplicaAddressValidationEnabled = enableReplicaValidation;
        }

        // Test hook
        internal string LastReadAddress
        {
            get;
            set;
        }

        /// <summary>
        /// Makes requests to multiple replicas at once and returns responses
        /// </summary>
        /// <param name="entity"> DocumentServiceRequest</param>
        /// <param name="includePrimary">flag to indicate whether to indicate primary replica in the reads</param>
        /// <param name="replicaCountToRead"> number of replicas to read from </param>
        /// <param name="requiresValidLsn"> flag to indicate whether a valid lsn is required to consider a response as valid </param>
        /// <param name="useSessionToken"> flag to indicate whether to use session token </param>
        /// <param name="readMode"> Read mode </param>
        /// <param name="checkMinLSN"> set minimum required session lsn </param>
        /// <param name="forceReadAll"> reads from all available replicas to gather result from readsToRead number of replicas </param>
        /// <returns> ReadReplicaResult which indicates the LSN and whether Quorum was Met / Not Met etc </returns>
        public async Task<IList<ReferenceCountedDisposable<StoreResult>>> ReadMultipleReplicaAsync(
            DocumentServiceRequest entity,
            bool includePrimary,
            int replicaCountToRead,
            bool requiresValidLsn,
            bool useSessionToken,
            ReadMode readMode,
            bool checkMinLSN = false,
            bool forceReadAll = false)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            string originalSessionToken = entity.Headers[HttpConstants.HttpHeaders.SessionToken];
            try
            {
                using ReadReplicaResult readQuorumResult = await this.ReadMultipleReplicasInternalAsync(
                    entity, includePrimary, replicaCountToRead, requiresValidLsn, useSessionToken, readMode, checkMinLSN, forceReadAll);
                if (entity.RequestContext.PerformLocalRefreshOnGoneException &&
                    readQuorumResult.RetryWithForceRefresh &&
                    !entity.RequestContext.ForceRefreshAddressCache)
                {
                    entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                    entity.RequestContext.ForceRefreshAddressCache = true;
                    using ReadReplicaResult readQuorumResultSecondCall = await this.ReadMultipleReplicasInternalAsync(
                        entity,
                        includePrimary: includePrimary,
                        replicaCountToRead: replicaCountToRead,
                        requiresValidLsn: requiresValidLsn,
                        useSessionToken: useSessionToken,
                        readMode: readMode,
                        checkMinLSN: false,
                        forceReadAll: forceReadAll);
                    return readQuorumResultSecondCall.StoreResultList.GetValueAndDereference();
                }

                return readQuorumResult.StoreResultList.GetValueAndDereference();

            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(entity, originalSessionToken);
            }
        }

        /// <summary>
        /// Exceptionless variant of <see cref="ReadMultipleReplicaAsync"/>.
        /// Returns the exception as part of the <see cref="Res{T}"/> instead of throwing.
        /// </summary>
        internal async Task<Res<IList<ReferenceCountedDisposable<StoreResult>>>> TryReadMultipleReplicaAsync(
            DocumentServiceRequest entity,
            bool includePrimary,
            int replicaCountToRead,
            bool requiresValidLsn,
            bool useSessionToken,
            ReadMode readMode,
            bool checkMinLSN = false,
            bool forceReadAll = false)
        {
            if (entity.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception timeoutException))
            {
                return Res.FromException<IList<ReferenceCountedDisposable<StoreResult>>>(timeoutException);
            }

            string originalSessionToken = entity.Headers[HttpConstants.HttpHeaders.SessionToken];
            try
            {
                using ReadReplicaResult readQuorumResult = await this.TryReadMultipleReplicasInternalAsync(
                    entity, includePrimary, replicaCountToRead, requiresValidLsn, useSessionToken, readMode, checkMinLSN, forceReadAll);
                if (readQuorumResult.Exception != null)
                {
                    return Res.FromException<IList<ReferenceCountedDisposable<StoreResult>>>(readQuorumResult.Exception);
                }

                if (entity.RequestContext.PerformLocalRefreshOnGoneException &&
                    readQuorumResult.RetryWithForceRefresh &&
                    !entity.RequestContext.ForceRefreshAddressCache)
                {
                    if (entity.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception elapsed))
                    {
                        return Res.FromException<IList<ReferenceCountedDisposable<StoreResult>>>(elapsed);
                    }

                    entity.RequestContext.ForceRefreshAddressCache = true;
                    using ReadReplicaResult readQuorumResultSecondCall = await this.TryReadMultipleReplicasInternalAsync(
                        entity,
                        includePrimary: includePrimary,
                        replicaCountToRead: replicaCountToRead,
                        requiresValidLsn: requiresValidLsn,
                        useSessionToken: useSessionToken,
                        readMode: readMode,
                        checkMinLSN: false,
                        forceReadAll: forceReadAll);
                    if (readQuorumResultSecondCall.Exception != null)
                    {
                        return Res.FromException<IList<ReferenceCountedDisposable<StoreResult>>>(readQuorumResultSecondCall.Exception);
                    }

                    return Res.Success(readQuorumResultSecondCall.StoreResultList.GetValueAndDereference());
                }

                return Res.Success(readQuorumResult.StoreResultList.GetValueAndDereference());
            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(entity, originalSessionToken);
            }
        }

        public async Task<ReferenceCountedDisposable<StoreResult>> ReadPrimaryAsync(
            DocumentServiceRequest entity,
            bool requiresValidLsn,
            bool useSessionToken)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            string originalSessionToken = entity.Headers[HttpConstants.HttpHeaders.SessionToken];
            try
            {
                using ReadReplicaResult readQuorumResult = await this.ReadPrimaryInternalAsync(
                        entity, 
                        requiresValidLsn, 
                        useSessionToken, 
                        isRetryAfterRefresh: false);
                if (entity.RequestContext.PerformLocalRefreshOnGoneException &&
                    readQuorumResult.RetryWithForceRefresh &&
                    !entity.RequestContext.ForceRefreshAddressCache)
                {
                    entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
                    entity.RequestContext.ForceRefreshAddressCache = true;
                    using ReadReplicaResult readQuorumResultSecondCall = await this.ReadPrimaryInternalAsync(
                            entity, 
                            requiresValidLsn, 
                            useSessionToken,
                            isRetryAfterRefresh: true);
                    return StoreReader.GetStoreResultOrThrowGoneException(readQuorumResultSecondCall);
                }

                return StoreReader.GetStoreResultOrThrowGoneException(readQuorumResult);
            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(entity, originalSessionToken);
            }
        }

        /// <summary>
        /// Exceptionless variant of <see cref="ReadPrimaryAsync"/>.
        /// Returns the exception as part of the <see cref="Res{T}"/> instead of throwing.
        /// </summary>
        internal async Task<Res<ReferenceCountedDisposable<StoreResult>>> TryReadPrimaryAsync(
            DocumentServiceRequest entity,
            bool requiresValidLsn,
            bool useSessionToken)
        {
            if (entity.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception timeoutException))
            {
                return Res.FromException<ReferenceCountedDisposable<StoreResult>>(timeoutException);
            }

            string originalSessionToken = entity.Headers[HttpConstants.HttpHeaders.SessionToken];
            try
            {
                using ReadReplicaResult readQuorumResult = await this.TryReadPrimaryInternalAsync(
                        entity,
                        requiresValidLsn,
                        useSessionToken,
                        isRetryAfterRefresh: false);
                if (readQuorumResult.Exception != null)
                {
                    return Res.FromException<ReferenceCountedDisposable<StoreResult>>(readQuorumResult.Exception);
                }

                if (entity.RequestContext.PerformLocalRefreshOnGoneException &&
                    readQuorumResult.RetryWithForceRefresh &&
                    !entity.RequestContext.ForceRefreshAddressCache)
                {
                    if (entity.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception elapsed))
                    {
                        return Res.FromException<ReferenceCountedDisposable<StoreResult>>(elapsed);
                    }

                    entity.RequestContext.ForceRefreshAddressCache = true;
                    using ReadReplicaResult readQuorumResultSecondCall = await this.TryReadPrimaryInternalAsync(
                            entity,
                            requiresValidLsn,
                            useSessionToken,
                            isRetryAfterRefresh: true);
                    if (readQuorumResultSecondCall.Exception != null)
                    {
                        return Res.FromException<ReferenceCountedDisposable<StoreResult>>(readQuorumResultSecondCall.Exception);
                    }

                    return StoreReader.TryGetStoreResult(readQuorumResultSecondCall);
                }

                return StoreReader.TryGetStoreResult(readQuorumResult);
            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(entity, originalSessionToken);
            }
        }

        private static ReferenceCountedDisposable<StoreResult> GetStoreResultOrThrowGoneException(ReadReplicaResult readReplicaResult)
        {
            StoreResultList storeResultList = readReplicaResult.StoreResultList;
            if (storeResultList.Count == 0)
            {
                throw new GoneException(RMResources.Gone, SubStatusCodes.Server_NoValidStoreResponse);
            }

            return storeResultList.GetFirstStoreResultAndDereference();
        }

        private static Res<ReferenceCountedDisposable<StoreResult>> TryGetStoreResult(ReadReplicaResult readReplicaResult)
        {
            StoreResultList storeResultList = readReplicaResult.StoreResultList;
            if (storeResultList.Count == 0)
            {
                return Res.FromException<ReferenceCountedDisposable<StoreResult>>(
                    new GoneException(RMResources.Gone, SubStatusCodes.Server_NoValidStoreResponse));
            }

            return Res.Success(storeResultList.GetFirstStoreResultAndDereference());
        }

        /// <summary>
        /// Makes requests to multiple replicas at once and returns responses
        /// </summary>
        /// <param name="entity"> DocumentServiceRequest</param>
        /// <param name="includePrimary">flag to indicate whether to indicate primary replica in the reads</param>
        /// <param name="replicaCountToRead"> number of replicas to read from </param>
        /// <param name="requiresValidLsn"> flag to indicate whether a valid lsn is required to consider a response as valid </param>
        /// <param name="useSessionToken"> flag to indicate whether to use session token </param>
        /// <param name="readMode"> Read mode </param>
        /// <param name="checkMinLSN"> set minimum required session lsn </param>
        /// <param name="forceReadAll"> will read from all available replicas to put together result from readsToRead number of replicas </param>
        /// <returns> ReadReplicaResult which indicates the LSN and whether Quorum was Met / Not Met etc </returns>
        private async Task<ReadReplicaResult> ReadMultipleReplicasInternalAsync(DocumentServiceRequest entity,
            bool includePrimary,
            int replicaCountToRead,
            bool requiresValidLsn,
            bool useSessionToken,
            ReadMode readMode,
            bool checkMinLSN = false,
            bool forceReadAll = false)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            using StoreResultList storeResultList = new(new List<ReferenceCountedDisposable<StoreResult>>(replicaCountToRead));
            ReferenceCountedDisposable<StoreResult> sessionNotFoundStoreResult = null; // For 404/1002 exceptionless scenario

            string requestedCollectionRid = entity.RequestContext.ResolvedCollectionRid;

            (IReadOnlyList<TransportAddressUri> resolveApiResults, IReadOnlyList<string> replicaHealthStatuses) = await this.addressSelector.ResolveAllTransportAddressUriAsync(
                     entity,
                     includePrimary,
                     entity.RequestContext.ForceRefreshAddressCache);

            ISessionToken requestSessionToken = null;
            if (useSessionToken)
            {
                SessionTokenHelper.SetPartitionLocalSessionToken(entity, this.sessionContainer);
                if (checkMinLSN)
                {
                    requestSessionToken = entity.RequestContext.SessionToken;
                }
            }
            else
            {
                entity.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
            }

            if (resolveApiResults.Count < replicaCountToRead)
            {
                if (!entity.RequestContext.ForceRefreshAddressCache)
                {
                    return new ReadReplicaResult(retryWithForceRefresh: true, responses: storeResultList.GetValueAndDereference());
                }

                return new ReadReplicaResult(retryWithForceRefresh: false, responses: storeResultList.GetValueAndDereference());
            }

            int replicasToRead = replicaCountToRead;

            string clientVersion = entity.Headers[HttpConstants.HttpHeaders.Version];
            bool enforceSessionCheck = !string.IsNullOrEmpty(clientVersion) && VersionUtility.IsLaterThan(clientVersion, HttpConstants.VersionDates.v2016_05_30);

            this.UpdateContinuationTokenIfReadFeedOrQuery(entity);

            bool hasGoneException = false;
            bool hasCancellationException = false;
            Exception cancellationException = null;
            Exception exceptionToThrow = null;
            SubStatusCodes subStatusCodeForException = SubStatusCodes.Unknown;
            IEnumerator<TransportAddressUri> uriEnumerator = this.addressEnumerator
                                                            .GetTransportAddresses(transportAddressUris: resolveApiResults,
                                                                                   failedEndpoints: entity.RequestContext.FailedEndpoints,
                                                                                   replicaAddressValidationEnabled: this.isReplicaAddressValidationEnabled)
                                                            .GetEnumerator();

            // Loop until we have the read quorum number of valid responses or if we have read all the replicas
            while (replicasToRead > 0 && uriEnumerator.MoveNext())
            {
                entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
                Dictionary<Task<(StoreResponse response, DateTime endTime)>, (TransportAddressUri, DateTime startTime)> readStoreTasks = new Dictionary<Task<(StoreResponse response, DateTime endTime)>, (TransportAddressUri, DateTime startTime)>();

                do
                {
                    readStoreTasks.Add(this.ReadFromStoreAsync(
                            physicalAddress: uriEnumerator.Current,
                            request: entity),
                        (uriEnumerator.Current, DateTime.UtcNow));

                    if (!forceReadAll && readStoreTasks.Count == replicasToRead)
                    {
                        break;
                    }
                } while (uriEnumerator.MoveNext());

                try
                {
                    await Task.WhenAll(readStoreTasks.Keys);
                }
                catch (Exception exception)
                {
                    exceptionToThrow = exception;

                    // Get SubStatusCode
                    if (exception is DocumentClientException documentClientException)
                    {
                        subStatusCodeForException = documentClientException.GetSubStatus();
                    }

                    //All task exceptions are visited below.
                    if (exception is DocumentClientException dce && 
                        (dce.StatusCode == HttpStatusCode.NotFound
                            || dce.StatusCode == HttpStatusCode.Conflict
                            || (int)dce.StatusCode == (int)StatusCodes.TooManyRequests))
                    {
                        // Only trace message for common scenarios to avoid the overhead of computing the stack trace.
                        DefaultTrace.TraceInformation("StoreReader.ReadMultipleReplicasInternalAsync exception thrown: StatusCode: {0}; SubStatusCode:{1}; Exception.Message: {2}",
                            dce.StatusCode,
                            dce.Headers?.Get(WFConstants.BackendHeaders.SubStatus),
                            dce.Message);
                    }
                    else
                    {
                        DefaultTrace.TraceInformation("StoreReader.ReadMultipleReplicasInternalAsync exception thrown: Exception: {0}", exception.Message);
                    }
                }

                foreach (KeyValuePair<Task<(StoreResponse response, DateTime endTime)>, (TransportAddressUri uri, DateTime startTime)> readTaskValuePair in readStoreTasks)
                {
                    Task<(StoreResponse response, DateTime endTime)> readTask = readTaskValuePair.Key;
                    (StoreResponse storeResponse, DateTime endTime) = readTask.Status == TaskStatus.RanToCompletion ? readTask.Result : (null, DateTime.UtcNow);
                    Exception storeException = readTask.Exception?.InnerException;
                    TransportAddressUri targetUri = readTaskValuePair.Value.uri;

                    if (storeException != null)
                    {
                        entity.RequestContext.AddToFailedEndpoints(storeException, targetUri);
                    }

                    // IsCanceled can be true with storeException being null if the async call
                    // gets canceled before it gets scheduled.
                    if (readTask.IsCanceled || storeException is OperationCanceledException)
                    {
                        hasCancellationException = true;
                        cancellationException ??= storeException;
                        continue;
                    }

                    using (ReferenceCountedDisposable<StoreResult> disposableStoreResult = StoreResult.CreateStoreResult(
                        storeResponse,
                        storeException, 
                        requiresValidLsn,
                        this.canUseLocalLSNBasedHeaders && readMode != ReadMode.Strong,
                        replicaHealthStatuses,
                        targetUri.Uri))
                    {
                        StoreResult storeResult = disposableStoreResult.Target;
                        entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.RequestCharge);

                        // Stash max CRSS for centralized scale-up detection
                        // in ReplicatedResourceClient.
                        if (storeResult.CurrentReplicaSetSize
                            > entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse)
                        {
                            entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse =
                                storeResult.CurrentReplicaSetSize;
                        }

                        if (storeResponse != null)
                        {
                            entity.RequestContext.ClientRequestStatistics.AppendContactedReplica(targetUri);
                        }

                        if (storeException != null && storeException.InnerException is TransportException)
                        {
                            entity.RequestContext.ClientRequestStatistics.AppendFailedReplica(targetUri);
                        }

                        entity.RequestContext.ClientRequestStatistics.RecordResponse(
                            entity,
                            storeResult,
                            readTaskValuePair.Value.startTime,
                            endTime);

                        if (storeResult.Exception != null)
                        {
                            StoreResult.VerifyCanContinueOnException(storeResult.Exception);
                        }

                        // Valid results including exceptionless failures with required valid LSN
                        if (storeResult.IsValid
                            && (!entity.IsValidStatusCodeForExceptionlessRetry((int)storeResult.StatusCode, storeResult.SubStatusCode) || !requiresValidLsn || storeResult.LSN > 0))
                        {
                            if (requestSessionToken == null
                                 || (storeResult.SessionToken != null && requestSessionToken.IsValid(storeResult.SessionToken))
                                 || (!enforceSessionCheck && storeResult.StatusCode != StatusCodes.NotFound))
                            {
                                storeResultList.Add(disposableStoreResult.TryAddReference());
                            }

                            // Exceptionless: Capture the 404/1002 
                            // Will be used later once all the replicas enumration is complete
                            if (storeResultList.Count == 0
                                && sessionNotFoundStoreResult == null
                                && entity.IsValidRequestFor4041002() 
                                && storeResult.StatusCode == StatusCodes.NotFound && storeResult.SubStatusCode == SubStatusCodes.ReadSessionNotAvailable)
                            {
                                sessionNotFoundStoreResult = disposableStoreResult.TryAddReference();
                            }
                        }

                        hasGoneException |= storeResult.StatusCode == StatusCodes.Gone && storeResult.SubStatusCode != SubStatusCodes.NameCacheIsStale;
                    }

                    // Perform address refresh in the background as soon as we hit a GoneException
                    if (hasGoneException && !entity.RequestContext.PerformedBackgroundAddressRefresh)
                    {
                        this.addressSelector.StartBackgroundAddressRefresh(entity);
                        entity.RequestContext.PerformedBackgroundAddressRefresh = true;
                    }
                }

                if (storeResultList.Count >= replicaCountToRead)
                {
                    return new ReadReplicaResult(false, storeResultList.GetValueAndDereference());
                }

                // Remaining replicas
                replicasToRead = replicaCountToRead - storeResultList.Count;
            }

            if (storeResultList.Count < replicaCountToRead)
            {
                DefaultTrace.TraceInformation("Could not get quorum number of responses. " +
                    "ValidResponsesReceived: {0} ResponsesExpected: {1}, ResolvedAddressCount: {2}, ResponsesString: {3}",
                    storeResultList.Count, replicaCountToRead, resolveApiResults.Count, String.Join(";", storeResultList.GetValue()));

                if (hasGoneException)
                {
                    if (!entity.RequestContext.PerformLocalRefreshOnGoneException)
                    {
                        // If we are not supposed to act upon GoneExceptions here, just throw them
                        throw new GoneException(exceptionToThrow, subStatusCodeForException);
                    }
                    else if (!entity.RequestContext.ForceRefreshAddressCache)
                    {
                        // We could not obtain valid read quorum number of responses even when we went through all the secondary addresses
                        // Attempt force refresh and start over again.
                        return new ReadReplicaResult(retryWithForceRefresh: true, responses: storeResultList.GetValueAndDereference());
                    }
                }
                else if (hasCancellationException)
                {
                    // We did not get the required number of responses and we encountered task cancellation on some/all of the store read tasks.
                    // We propagate the first cancellation exception we've found, or a new OperationCanceledException if none.
                    // The latter case can happen when Task.IsCanceled = true.
                    throw cancellationException ?? new OperationCanceledException();
                }
                else if (sessionNotFoundStoreResult != null)
                {
                    using (sessionNotFoundStoreResult)
                    {
                        storeResultList.Add(sessionNotFoundStoreResult.TryAddReference());
                    }
                }
            }

            return new ReadReplicaResult(false, storeResultList.GetValueAndDereference());
        }

        /// <summary>
        /// Exceptionless variant of <see cref="ReadMultipleReplicasInternalAsync"/>.
        /// Returns errors via <see cref="ReadReplicaResult.Exception"/> instead of throwing.
        /// </summary>
        private async Task<ReadReplicaResult> TryReadMultipleReplicasInternalAsync(
            DocumentServiceRequest entity,
            bool includePrimary,
            int replicaCountToRead,
            bool requiresValidLsn,
            bool useSessionToken,
            ReadMode readMode,
            bool checkMinLSN = false,
            bool forceReadAll = false)
        {
            if (entity.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception timeoutEx))
            {
                return new ReadReplicaResult(timeoutEx);
            }

            using StoreResultList storeResultList = new(new List<ReferenceCountedDisposable<StoreResult>>(replicaCountToRead));
            ReferenceCountedDisposable<StoreResult> sessionNotFoundStoreResult = null;

            string requestedCollectionRid = entity.RequestContext.ResolvedCollectionRid;

            Res<(IReadOnlyList<TransportAddressUri>, IReadOnlyList<string>)> addressRes =
                await this.addressSelector.NonThrowingResolveAllTransportAddressUriAsync(
                     entity,
                     includePrimary,
                     entity.RequestContext.ForceRefreshAddressCache);
            if (!addressRes.IsSuccess)
            {
                return new ReadReplicaResult(addressRes.Exception);
            }

            (IReadOnlyList<TransportAddressUri> resolveApiResults, IReadOnlyList<string> replicaHealthStatuses) = addressRes.Value;

            ISessionToken requestSessionToken = null;
            Res<bool> sessionTokenResult = this.TryApplySessionToken(entity, useSessionToken);
            if (!sessionTokenResult.IsSuccess)
            {
                return new ReadReplicaResult(sessionTokenResult.Exception);
            }

            if (useSessionToken && checkMinLSN)
            {
                requestSessionToken = entity.RequestContext.SessionToken;
            }

            if (resolveApiResults.Count < replicaCountToRead)
            {
                if (!entity.RequestContext.ForceRefreshAddressCache)
                {
                    return new ReadReplicaResult(retryWithForceRefresh: true, responses: storeResultList.GetValueAndDereference());
                }

                return new ReadReplicaResult(retryWithForceRefresh: false, responses: storeResultList.GetValueAndDereference());
            }

            int replicasToRead = replicaCountToRead;

            string clientVersion = entity.Headers[HttpConstants.HttpHeaders.Version];
            bool enforceSessionCheck = !string.IsNullOrEmpty(clientVersion) && VersionUtility.IsLaterThan(clientVersion, HttpConstants.VersionDates.v2016_05_30);

            Res<bool> continuationResult = this.TryUpdateContinuationTokenIfReadFeedOrQuery(entity);
            if (!continuationResult.IsSuccess)
            {
                return new ReadReplicaResult(continuationResult.Exception);
            }

            bool hasGoneException = false;
            bool hasCancellationException = false;
            Exception cancellationException = null;
            Exception exceptionToThrow = null;
            SubStatusCodes subStatusCodeForException = SubStatusCodes.Unknown;
            IEnumerator<TransportAddressUri> uriEnumerator = this.addressEnumerator
                                                            .GetTransportAddresses(transportAddressUris: resolveApiResults,
                                                                                   failedEndpoints: entity.RequestContext.FailedEndpoints,
                                                                                   replicaAddressValidationEnabled: this.isReplicaAddressValidationEnabled)
                                                            .GetEnumerator();

            while (replicasToRead > 0 && uriEnumerator.MoveNext())
            {
                if (entity.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception elapsed))
                {
                    return new ReadReplicaResult(elapsed);
                }

                Dictionary<Task<(Res<StoreResponse> result, DateTime endTime)>, (TransportAddressUri, DateTime startTime)> readStoreTasks =
                    new Dictionary<Task<(Res<StoreResponse> result, DateTime endTime)>, (TransportAddressUri, DateTime startTime)>();

                do
                {
                    readStoreTasks.Add(this.TryReadFromStoreAsync(
                            physicalAddress: uriEnumerator.Current,
                            request: entity),
                        (uriEnumerator.Current, DateTime.UtcNow));

                    if (!forceReadAll && readStoreTasks.Count == replicasToRead)
                    {
                        break;
                    }
                } while (uriEnumerator.MoveNext());

                await Task.WhenAll(readStoreTasks.Keys);

                foreach (KeyValuePair<Task<(Res<StoreResponse> result, DateTime endTime)>, (TransportAddressUri uri, DateTime startTime)> readTaskValuePair in readStoreTasks)
                {
                    Task<(Res<StoreResponse> result, DateTime endTime)> readTask = readTaskValuePair.Key;
                    (Res<StoreResponse> storeResponseResult, DateTime endTime) = readTask.Result;
                    StoreResponse storeResponse = storeResponseResult.IsSuccess ? storeResponseResult.Value : null;
                    Exception storeException = storeResponseResult.IsSuccess ? null : storeResponseResult.Exception;
                    TransportAddressUri targetUri = readTaskValuePair.Value.uri;

                    if (storeException != null)
                    {
                        entity.RequestContext.AddToFailedEndpoints(storeException, targetUri);

                        exceptionToThrow = storeException;
                        if (storeException is DocumentClientException documentClientException)
                        {
                            subStatusCodeForException = documentClientException.GetSubStatus();
                        }
                    }

                    if (storeException is OperationCanceledException)
                    {
                        hasCancellationException = true;
                        cancellationException ??= storeException;
                        continue;
                    }

                    using (ReferenceCountedDisposable<StoreResult> disposableStoreResult = StoreResult.CreateStoreResult(
                        storeResponse,
                        storeException,
                        requiresValidLsn,
                        this.canUseLocalLSNBasedHeaders && readMode != ReadMode.Strong,
                        replicaHealthStatuses,
                        targetUri.Uri))
                    {
                        StoreResult storeResult = disposableStoreResult.Target;
                        entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.RequestCharge);

                        if (storeResult.CurrentReplicaSetSize
                            > entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse)
                        {
                            entity.RequestContext.MaxCurrentReplicaSetSizeFromResponse =
                                storeResult.CurrentReplicaSetSize;
                        }

                        if (storeResponse != null)
                        {
                            entity.RequestContext.ClientRequestStatistics.AppendContactedReplica(targetUri);
                        }

                        if (storeException != null && storeException.InnerException is TransportException)
                        {
                            entity.RequestContext.ClientRequestStatistics.AppendFailedReplica(targetUri);
                        }

                        entity.RequestContext.ClientRequestStatistics.RecordResponse(
                            entity,
                            storeResult,
                            readTaskValuePair.Value.startTime,
                            endTime);

                        if (storeResult.Exception != null && !StoreResult.CanContinueOnException(storeResult.Exception))
                        {
                            return new ReadReplicaResult(storeResult.Exception);
                        }

                        if (storeResult.IsValid
                            && (!entity.IsValidStatusCodeForExceptionlessRetry((int)storeResult.StatusCode, storeResult.SubStatusCode) || !requiresValidLsn || storeResult.LSN > 0))
                        {
                            bool requestSessionTokenIsValid = false;
                            if (requestSessionToken != null && storeResult.SessionToken != null)
                            {
                                Res<bool> sessionTokenValidRes = StoreReader.TryIsSessionTokenValid(requestSessionToken, storeResult.SessionToken);
                                if (!sessionTokenValidRes.IsSuccess)
                                {
                                    return new ReadReplicaResult(sessionTokenValidRes.Exception);
                                }

                                requestSessionTokenIsValid = sessionTokenValidRes.Value;
                            }

                            if (requestSessionToken == null
                                 || (storeResult.SessionToken != null && requestSessionTokenIsValid)
                                 || (!enforceSessionCheck && storeResult.StatusCode != StatusCodes.NotFound))
                            {
                                storeResultList.Add(disposableStoreResult.TryAddReference());
                            }

                            if (storeResultList.Count == 0
                                && sessionNotFoundStoreResult == null
                                && entity.IsValidRequestFor4041002()
                                && storeResult.StatusCode == StatusCodes.NotFound && storeResult.SubStatusCode == SubStatusCodes.ReadSessionNotAvailable)
                            {
                                sessionNotFoundStoreResult = disposableStoreResult.TryAddReference();
                            }
                        }

                        hasGoneException |= storeResult.StatusCode == StatusCodes.Gone && storeResult.SubStatusCode != SubStatusCodes.NameCacheIsStale;
                    }

                    if (hasGoneException && !entity.RequestContext.PerformedBackgroundAddressRefresh)
                    {
                        this.addressSelector.StartBackgroundAddressRefresh(entity);
                        entity.RequestContext.PerformedBackgroundAddressRefresh = true;
                    }
                }

                if (storeResultList.Count >= replicaCountToRead)
                {
                    return new ReadReplicaResult(false, storeResultList.GetValueAndDereference());
                }

                replicasToRead = replicaCountToRead - storeResultList.Count;
            }

            if (storeResultList.Count < replicaCountToRead)
            {
                DefaultTrace.TraceInformation("Could not get quorum number of responses. " +
                    "ValidResponsesReceived: {0} ResponsesExpected: {1}, ResolvedAddressCount: {2}, ResponsesString: {3}",
                    storeResultList.Count, replicaCountToRead, resolveApiResults.Count, String.Join(";", storeResultList.GetValue()));

                if (hasGoneException)
                {
                    if (!entity.RequestContext.PerformLocalRefreshOnGoneException)
                    {
                        return new ReadReplicaResult(new GoneException(exceptionToThrow, subStatusCodeForException));
                    }
                    else if (!entity.RequestContext.ForceRefreshAddressCache)
                    {
                        return new ReadReplicaResult(retryWithForceRefresh: true, responses: storeResultList.GetValueAndDereference());
                    }
                }
                else if (hasCancellationException)
                {
                    return new ReadReplicaResult(cancellationException ?? new OperationCanceledException());
                }
                else if (sessionNotFoundStoreResult != null)
                {
                    using (sessionNotFoundStoreResult)
                    {
                        storeResultList.Add(sessionNotFoundStoreResult.TryAddReference());
                    }
                }
            }

            return new ReadReplicaResult(false, storeResultList.GetValueAndDereference());
        }

        private async Task<ReadReplicaResult> ReadPrimaryInternalAsync(
            DocumentServiceRequest entity,
            bool requiresValidLsn,
            bool useSessionToken,
            bool isRetryAfterRefresh)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            TransportAddressUri primaryUri = await this.addressSelector.ResolvePrimaryTransportAddressUriAsync(
                          entity,
                          entity.RequestContext.ForceRefreshAddressCache);

            if (useSessionToken)
            {
                SessionTokenHelper.SetPartitionLocalSessionToken(entity, this.sessionContainer);
            }
            else
            {
                // Remove whatever session token can be there in headers.
                // We don't need it. If it is global - backend will not understand it.
                // But there's no point in producing partition local session token.
                entity.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
            }

            DateTime startTimeUtc = DateTime.UtcNow;
            StrongBox<DateTime?> endTimeUtc = new ();
            using ReferenceCountedDisposable<StoreResult> storeResult = await GetResult(entity, requiresValidLsn, primaryUri, endTimeUtc);
            entity.RequestContext.ClientRequestStatistics.RecordResponse(
                entity,
                storeResult.Target,
                startTimeUtc,
                endTimeUtc.Value ?? DateTime.UtcNow);

            entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.Target.RequestCharge);

            if (storeResult.Target.Exception != null)
            {
                StoreResult.VerifyCanContinueOnException(storeResult.Target.Exception);
            }

            if (storeResult.Target.StatusCode == StatusCodes.Gone && storeResult.Target.SubStatusCode != SubStatusCodes.NameCacheIsStale)
            {
                if (isRetryAfterRefresh ||
                    !entity.RequestContext.PerformLocalRefreshOnGoneException ||
                    entity.RequestContext.ForceRefreshAddressCache)
                {
                    // We can throw the exception if we have already performed an address refresh or if PerformLocalRefreshOnGoneException is false
                    throw new GoneException(RMResources.Gone, storeResult.Target.SubStatusCode);
                }

                return new ReadReplicaResult(true, new List<ReferenceCountedDisposable<StoreResult>>());
            }

            return new ReadReplicaResult(false, new ReferenceCountedDisposable<StoreResult>[] { storeResult.TryAddReference() });
        }

        /// <summary>
        /// Exceptionless variant of <see cref="ReadPrimaryInternalAsync"/>.
        /// Returns errors via <see cref="ReadReplicaResult.Exception"/> instead of throwing.
        /// </summary>
        private async Task<ReadReplicaResult> TryReadPrimaryInternalAsync(
            DocumentServiceRequest entity,
            bool requiresValidLsn,
            bool useSessionToken,
            bool isRetryAfterRefresh)
        {
            if (entity.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception timeoutEx))
            {
                return new ReadReplicaResult(timeoutEx);
            }

            Res<TransportAddressUri> primaryUriRes = await this.addressSelector.NonThrowingResolvePrimaryTransportAddressUriAsync(
                          entity,
                          entity.RequestContext.ForceRefreshAddressCache);
            if (!primaryUriRes.IsSuccess)
            {
                return new ReadReplicaResult(primaryUriRes.Exception);
            }

            TransportAddressUri primaryUri = primaryUriRes.Value;

            Res<bool> sessionTokenResult = this.TryApplySessionToken(entity, useSessionToken);
            if (!sessionTokenResult.IsSuccess)
            {
                return new ReadReplicaResult(sessionTokenResult.Exception);
            }

            DateTime startTimeUtc = DateTime.UtcNow;
            StrongBox<DateTime?> endTimeUtc = new ();
            using ReferenceCountedDisposable<StoreResult> storeResult = await TryGetResult(entity, requiresValidLsn, primaryUri, endTimeUtc);
            entity.RequestContext.ClientRequestStatistics.RecordResponse(
                entity,
                storeResult.Target,
                startTimeUtc,
                endTimeUtc.Value ?? DateTime.UtcNow);

            entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.Target.RequestCharge);

            if (storeResult.Target.Exception != null && !StoreResult.CanContinueOnException(storeResult.Target.Exception))
            {
                return new ReadReplicaResult(storeResult.Target.Exception);
            }

            if (storeResult.Target.StatusCode == StatusCodes.Gone && storeResult.Target.SubStatusCode != SubStatusCodes.NameCacheIsStale)
            {
                if (isRetryAfterRefresh ||
                    !entity.RequestContext.PerformLocalRefreshOnGoneException ||
                    entity.RequestContext.ForceRefreshAddressCache)
                {
                    return new ReadReplicaResult(new GoneException(RMResources.Gone, storeResult.Target.SubStatusCode));
                }

                return new ReadReplicaResult(true, new List<ReferenceCountedDisposable<StoreResult>>());
            }

            return new ReadReplicaResult(false, new ReferenceCountedDisposable<StoreResult>[] { storeResult.TryAddReference() });
        }

        private async Task<ReferenceCountedDisposable<StoreResult>> GetResult(DocumentServiceRequest entity, bool requiresValidLsn, TransportAddressUri primaryUri, StrongBox<DateTime?> endTimeUtc)
        {
            ReferenceCountedDisposable<StoreResult> storeResult;
            List<string> primaryReplicaHealthStatus = new ()
            {
                primaryUri
                .GetCurrentHealthState()
                .GetHealthStatusDiagnosticString(),
            };

            try
            {
                this.UpdateContinuationTokenIfReadFeedOrQuery(entity);
                (StoreResponse storeResponse, DateTime storeResponseEndTimeUtc) = await this.ReadFromStoreAsync(
                    primaryUri,
                    entity);

                endTimeUtc.Value = DateTime.UtcNow;

                storeResult = StoreResult.CreateStoreResult(
                    storeResponse,
                    null,
                    requiresValidLsn,
                    this.canUseLocalLSNBasedHeaders,
                    replicaHealthStatuses: primaryReplicaHealthStatus,
                    primaryUri.Uri);
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceInformation("Exception {0} is thrown while doing Read Primary", exception.Message);
                storeResult = StoreResult.CreateStoreResult(
                    null,
                    exception,
                    requiresValidLsn,
                    this.canUseLocalLSNBasedHeaders,
                    replicaHealthStatuses: primaryReplicaHealthStatus,
                    primaryUri.Uri);
            }

            return storeResult;
        }

        /// <summary>
        /// Exceptionless variant of <see cref="GetResult"/>. Uses <see cref="TryReadFromStoreAsync"/>.
        /// </summary>
        private async Task<ReferenceCountedDisposable<StoreResult>> TryGetResult(DocumentServiceRequest entity, bool requiresValidLsn, TransportAddressUri primaryUri, StrongBox<DateTime?> endTimeUtc)
        {
            List<string> primaryReplicaHealthStatus = new ()
            {
                primaryUri
                .GetCurrentHealthState()
                .GetHealthStatusDiagnosticString(),
            };

            Res<bool> continuationResult = this.TryUpdateContinuationTokenIfReadFeedOrQuery(entity);
            if (!continuationResult.IsSuccess)
            {
                DefaultTrace.TraceInformation("Exception {0} is thrown while doing Read Primary", continuationResult.Exception.Message);
                return StoreResult.CreateStoreResult(
                    null,
                    continuationResult.Exception,
                    requiresValidLsn,
                    this.canUseLocalLSNBasedHeaders,
                    replicaHealthStatuses: primaryReplicaHealthStatus,
                    primaryUri.Uri);
            }

            (Res<StoreResponse> storeResponseResult, DateTime endTime) = await this.TryReadFromStoreAsync(
                primaryUri,
                entity);

            endTimeUtc.Value = endTime;

            StoreResponse storeResponse = storeResponseResult.IsSuccess ? storeResponseResult.Value : null;
            Exception storeException = storeResponseResult.IsSuccess ? null : storeResponseResult.Exception;

            if (storeException != null)
            {
                DefaultTrace.TraceInformation("Exception {0} is thrown while doing Read Primary", storeException.Message);
            }

            try
            {
                return StoreResult.CreateStoreResult(
                    storeResponse,
                    storeException,
                    requiresValidLsn,
                    this.canUseLocalLSNBasedHeaders,
                    replicaHealthStatuses: primaryReplicaHealthStatus,
                    primaryUri.Uri);
            }
            catch (Exception exception)
            {
                // Mirrors the throwing GetResult, which converts any failure while building the
                // store result (for example a malformed backend header failing to parse) into a
                // store result carrying the exception, rather than letting it abort the read.
                ExceptionlessEscapeTrace.TraceEscape(
                    ExceptionlessEscapeTrace.StoreReaderPrimaryResult,
                    exception,
                    retryable: null);

                DefaultTrace.TraceInformation("Exception {0} is thrown while doing Read Primary", exception.Message);
                return StoreResult.CreateStoreResult(
                    null,
                    exception,
                    requiresValidLsn,
                    this.canUseLocalLSNBasedHeaders,
                    replicaHealthStatuses: primaryReplicaHealthStatus,
                    primaryUri.Uri);
            }
        }

        private async Task<(StoreResponse, DateTime endTime)> ReadFromStoreAsync(
            TransportAddressUri physicalAddress,
            DocumentServiceRequest request)
        {
            request.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
            this.LastReadAddress = physicalAddress.ToString();

            switch (request.OperationType)
            {
                case OperationType.Read:
                case OperationType.Head:
                case OperationType.HeadFeed:
                case OperationType.SqlQuery:
                case OperationType.ExecuteJavaScript:
#if !COSMOSCLIENT
                case OperationType.MetadataCheckAccess:
                case OperationType.ExternalPreBackupSync:
                case OperationType.CheckExternalBackupStatus:
                case OperationType.CheckExternalBackupRestoreStatus:
                case OperationType.CheckExternalPreBackupStatus:
                case OperationType.GetStorageAuthToken:
#endif
                    {
                        StoreResponse result = await this.transportClient.InvokeResourceOperationAsync(
                            physicalAddress,
                            request);
                        return (result, DateTime.UtcNow);
                    }

                case OperationType.ReadFeed:
                case OperationType.Query:
                    {
                        QueryRequestPerformanceActivity activity = CustomTypeExtensions.StartActivity(request);
                        StoreResponse result = await StoreReader.CompleteActivity(this.transportClient.InvokeResourceOperationAsync(
                            physicalAddress,
                            request),
                            activity);
                        return (result, DateTime.UtcNow);
                    }
                default:
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unexpected operation type {0}", request.OperationType));
            }
        }

        /// <summary>
        /// Exceptionless variant of <see cref="ReadFromStoreAsync"/>.
        /// Returns a <see cref="Result{T}"/> instead of throwing on transport errors.
        /// This method must never fault its Task.
        /// </summary>
        private async Task<(Res<StoreResponse> result, DateTime endTime)> TryReadFromStoreAsync(
            TransportAddressUri physicalAddress,
            DocumentServiceRequest request)
        {
            try
            {
                if (request.RequestContext.TimeoutHelper.TryGetGoneOrCancelledException(out Exception timeoutException))
                {
                    return (Res.FromException<StoreResponse>(timeoutException), DateTime.UtcNow);
                }

                this.LastReadAddress = physicalAddress.ToString();

                Res<StoreResponse> result;
                switch (request.OperationType)
                {
                    case OperationType.Read:
                    case OperationType.Head:
                    case OperationType.HeadFeed:
                    case OperationType.SqlQuery:
                    case OperationType.ExecuteJavaScript:
#if !COSMOSCLIENT
                    case OperationType.MetadataCheckAccess:
                    case OperationType.ExternalPreBackupSync:
                    case OperationType.CheckExternalBackupStatus:
                    case OperationType.CheckExternalBackupRestoreStatus:
                    case OperationType.CheckExternalPreBackupStatus:
                    case OperationType.GetStorageAuthToken:
#endif
                        {
                            result = await this.transportClient.TryInvokeResourceOperationAsync(
                                physicalAddress,
                                request);
                            break;
                        }

                    case OperationType.ReadFeed:
                    case OperationType.Query:
                        {
                            QueryRequestPerformanceActivity activity = CustomTypeExtensions.StartActivity(request);
                            result = await this.transportClient.TryInvokeResourceOperationAsync(
                                physicalAddress,
                                request);
                            if (activity != null)
                            {
                                activity.ActivityComplete(result.IsSuccess);
                            }

                            break;
                        }
                    default:
                        return (Res.FromException<StoreResponse>(
                            new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unexpected operation type {0}", request.OperationType))),
                            DateTime.UtcNow);
                }

                return (result, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                // Hard boundary: this method must never fault its Task.
                return (Res.FromException<StoreResponse>(ex), DateTime.UtcNow);
            }
        }

        private void UpdateContinuationTokenIfReadFeedOrQuery(DocumentServiceRequest request)
        {
            if (request.OperationType != OperationType.ReadFeed &&
                request.OperationType != OperationType.Query)
            {
                return;
            }

            string continuation = request.Continuation;
            if (continuation != null)
            {
                int firstSemicolonPosition = continuation.IndexOf(';');
                // IndexOf returns -1 if ';' is not found
                if (firstSemicolonPosition < 0)
                {
                    return;
                }

                int semicolonCount = 1;
                for (int i = firstSemicolonPosition + 1; i < continuation.Length; i++)
                {
                    if (continuation[i] == ';')
                    {
                        semicolonCount++;
                        if (semicolonCount >= 3)
                        {
                            break;
                        }
                    }
                }

                if (semicolonCount < 3)
                {
                    throw new BadRequestException(string.Format(
                        CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        continuation,
                        HttpConstants.HttpHeaders.Continuation));
                }

                request.Continuation = continuation.Substring(0, firstSemicolonPosition);
            }
        }

        /// <summary>
        /// Exceptionless wrapper around <see cref="ISessionToken.IsValid"/>.
        /// </summary>
        /// <remarks>
        /// Both <c>SimpleSessionToken.IsValid</c> and <c>VectorSessionToken.IsValid</c> throw
        /// <c>ArgumentNullException</c> when the two tokens are different implementations. The
        /// argument is not actually null in that case, it is the wrong type, and a client can
        /// reach it: a request session token parsed from a client supplied header can be a
        /// SimpleSessionToken while the replica responds with a VectorSessionToken. This was
        /// observed escaping to RequestRetryUtility.TryProcessRequestAsync in a test environment.
        ///
        /// Only the exceptionless read loop routes through here, so the resulting status is
        /// unchanged; the throwing loop still calls IsValid directly. Note that the underlying
        /// mismatch surfacing as a 500 rather than a 400 is a separate pre-existing issue on both
        /// paths and is deliberately not changed here.
        /// </remarks>
        private static Res<bool> TryIsSessionTokenValid(ISessionToken requestSessionToken, ISessionToken responseSessionToken)
        {
            try
            {
                return Res.Success(requestSessionToken.IsValid(responseSessionToken));
            }
            catch (Exception exception)
            {
                return Res.FromException<bool>(exception);
            }
        }

        /// <summary>
        /// Applies the session token handling that the throwing read paths perform inline,
        /// returning any failure as a <see cref="Res{T}"/> failure instead of throwing.
        /// </summary>
        /// <remarks>
        /// <c>SetPartitionLocalSessionToken</c> reaches <c>GetLocalSessionToken</c>, which throws
        /// <c>BadRequestException</c> for a malformed client supplied session token, so a client
        /// can otherwise drive throw volume on the exceptionless read path. It is a shared helper
        /// with many callers elsewhere, so rather than duplicating it the call is contained here
        /// and converted at the boundary, matching ConsistencyWriter.TryApplySessionToken on the
        /// write path.
        /// </remarks>
        private Res<bool> TryApplySessionToken(DocumentServiceRequest entity, bool useSessionToken)
        {
            try
            {
                if (useSessionToken)
                {
                    SessionTokenHelper.SetPartitionLocalSessionToken(entity, this.sessionContainer);
                }
                else
                {
                    entity.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
                }

                return Res.Success(true);
            }
            catch (Exception exception)
            {
                return Res.FromException<bool>(exception);
            }
        }

        /// <summary>
        /// Exceptionless variant of <see cref="UpdateContinuationTokenIfReadFeedOrQuery"/>.
        /// Returns the <see cref="BadRequestException"/> as a <see cref="Res{T}"/> failure instead
        /// of throwing. The continuation token is client supplied, so a malformed value is
        /// client-triggerable and would otherwise let a caller drive throw volume on the
        /// exceptionless path.
        /// </summary>
        private Res<bool> TryUpdateContinuationTokenIfReadFeedOrQuery(DocumentServiceRequest request)
        {
            if (request.OperationType != OperationType.ReadFeed &&
                request.OperationType != OperationType.Query)
            {
                return Res.Success(true);
            }

            string continuation = request.Continuation;
            if (continuation != null)
            {
                int firstSemicolonPosition = continuation.IndexOf(';');
                // IndexOf returns -1 if ';' is not found
                if (firstSemicolonPosition < 0)
                {
                    return Res.Success(true);
                }

                int semicolonCount = 1;
                for (int i = firstSemicolonPosition + 1; i < continuation.Length; i++)
                {
                    if (continuation[i] == ';')
                    {
                        semicolonCount++;
                        if (semicolonCount >= 3)
                        {
                            break;
                        }
                    }
                }

                if (semicolonCount < 3)
                {
                    return Res.FromException<bool>(new BadRequestException(string.Format(
                        CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        continuation,
                        HttpConstants.HttpHeaders.Continuation)));
                }

                request.Continuation = continuation.Substring(0, firstSemicolonPosition);
            }

            return Res.Success(true);
        }

        private static async Task<StoreResponse> CompleteActivity(Task<StoreResponse> task, QueryRequestPerformanceActivity activity)
        {
            if (activity == null)
            {
                return await task;
            }
            else
            {
                StoreResponse response;
                try
                {
                    response = await task;
                }
                catch
                {
                    activity.ActivityComplete(false);
                    throw;
                }

                activity.ActivityComplete(true);
                return response;
            }
        }

        #region PrivateResultClasses
        private sealed class ReadReplicaResult : IDisposable
        {
            public ReadReplicaResult(bool retryWithForceRefresh, IList<ReferenceCountedDisposable<StoreResult>> responses)
            {
                this.RetryWithForceRefresh = retryWithForceRefresh;
                this.StoreResultList = new(responses);
            }

            public ReadReplicaResult(Exception exception)
            {
                this.Exception = exception;
                this.StoreResultList = new(new ReferenceCountedDisposable<StoreResult>[0]);
            }

            public bool RetryWithForceRefresh { get; private set; }

            public Exception Exception { get; private set; }

            public StoreResultList StoreResultList { get; private set; }

            public void Dispose()
            {
                this.StoreResultList.Dispose();
            }
        }

        /// <summary>
        /// Disposable list of StoreResult object with ability to skip first object disposal or skip disposal for entire list.
        /// </summary>
        private class StoreResultList : IDisposable
        {
            private IList<ReferenceCountedDisposable<StoreResult>> value;

            public StoreResultList(IList<ReferenceCountedDisposable<StoreResult>> collection)
            {
                this.value = collection ?? throw new ArgumentNullException();
            }

            public void Add(ReferenceCountedDisposable<StoreResult> storeResult)
            {
                this.GetValueOrThrow().Add(storeResult);
            }

            public int Count => this.GetValueOrThrow().Count;

            public ReferenceCountedDisposable<StoreResult> GetFirstStoreResultAndDereference()
            {
                IList<ReferenceCountedDisposable<StoreResult>> value = this.GetValueOrThrow();
                if (value.Count > 0)
                {
                    ReferenceCountedDisposable<StoreResult> result = value[0];
                    this.value[0] = null;
                    return result;
                }

                return null;
            }

            public IList<ReferenceCountedDisposable<StoreResult>> GetValue() => this.GetValueOrThrow();

            public IList<ReferenceCountedDisposable<StoreResult>> GetValueAndDereference()
            {
                IList<ReferenceCountedDisposable<StoreResult>> response = this.GetValueOrThrow();
                this.value = null;
                return response;
            }

            public void Dispose()
            {
                if (this.value != null)
                {
                    for (int i = 0; i < this.value.Count; i++)
                    {
                        this.value[i]?.Dispose();
                    }
                }
            }

            private IList<ReferenceCountedDisposable<StoreResult>> GetValueOrThrow()
            {
                if (this.value == null || (this.value.Count > 0 && this.value[0] == null))
                {
                    throw new InvalidOperationException("Call on the StoreResultList with deferenced collection");
                }

                return this.value;
            }
        }
        #endregion
    }
}
