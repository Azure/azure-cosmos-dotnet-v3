//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
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
            ISessionContainer sessionContainer)
        {
            this.transportClient = transportClient;
            this.addressSelector = addressSelector;
            this.addressEnumerator = addressEnumerator ?? throw new ArgumentNullException(nameof(addressEnumerator));
            this.sessionContainer = sessionContainer;
            this.canUseLocalLSNBasedHeaders = VersionUtility.IsLaterThan(HttpConstants.Versions.CurrentVersion, HttpConstants.Versions.v2018_06_18);
            this.isReplicaAddressValidationEnabled = Helpers.GetEnvironmentVariableAsBool(
                name: Constants.EnvironmentVariables.ReplicaConnectivityValidationEnabled,
                defaultValue: false);
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

        private static ReferenceCountedDisposable<StoreResult> GetStoreResultOrThrowGoneException(ReadReplicaResult readReplicaResult)
        {
            StoreResultList storeResultList = readReplicaResult.StoreResultList;
            if (storeResultList.Count == 0)
            {
                throw new GoneException(RMResources.Gone, SubStatusCodes.Server_NoValidStoreResponse);
            }

            return storeResultList.GetFirstStoreResultAndDereference();
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

            string requestedCollectionRid = entity.RequestContext.ResolvedCollectionRid;

            IReadOnlyList<TransportAddressUri> resolveApiResults = await this.addressSelector.ResolveAllTransportAddressUriAsync(
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
            IEnumerable<TransportAddressUri> transportAddresses = this.addressEnumerator
                                                            .GetTransportAddresses(transportAddressUris: resolveApiResults,
                                                                                   failedEndpoints: entity.RequestContext.FailedEndpoints,
                                                                                   replicaAddressValidationEnabled: this.isReplicaAddressValidationEnabled);

            // The replica health status of the transport address uri will change eventually with the motonically increasing time.
            // However, the purpose of this list is to capture the health status snapshot at this moment.
            IEnumerable<string> replicaHealthStatuses = transportAddresses
                .Select(x => x.GetCurrentHealthState().GetHealthStatusDiagnosticString());

            IEnumerator<TransportAddressUri> uriEnumerator = transportAddresses.GetEnumerator();

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
                        // Include the full exception for other scenarios for troubleshooting
                        DefaultTrace.TraceInformation("StoreReader.ReadMultipleReplicasInternalAsync exception thrown: Exception: {0}", exception);
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

                        if (storeResponse != null)
                        {
                            entity.RequestContext.ClientRequestStatistics.ContactedReplicas.Add(targetUri);
                        }

                        if (storeException != null && storeException.InnerException is TransportException)
                        {
                            entity.RequestContext.ClientRequestStatistics.FailedReplicas.Add(targetUri);
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

                        if (storeResult.IsValid)
                        {
                            if (requestSessionToken == null
                                || (storeResult.SessionToken != null && requestSessionToken.IsValid(storeResult.SessionToken))
                                || (!enforceSessionCheck && storeResult.StatusCode != StatusCodes.NotFound))
                            {
                                storeResultList.Add(disposableStoreResult.TryAddReference());
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
                DefaultTrace.TraceInformation("Exception {0} is thrown while doing Read Primary", exception);
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

            public bool RetryWithForceRefresh { get; private set; }

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
