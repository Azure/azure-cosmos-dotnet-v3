//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class StoreReader
    {
        private readonly TransportClient transportClient;
        private readonly AddressSelector addressSelector;
        private readonly IAddressEnumerator addressEnumerator;
        private readonly ISessionContainer sessionContainer;
        private readonly bool canUseLocalLSNBasedHeaders;

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
        public async Task<IList<StoreResult>> ReadMultipleReplicaAsync(
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
                ReadReplicaResult readQuorumResult = await this.ReadMultipleReplicasInternalAsync(
                    entity, includePrimary, replicaCountToRead, requiresValidLsn, useSessionToken, readMode, checkMinLSN, forceReadAll);
                if (entity.RequestContext.PerformLocalRefreshOnGoneException &&
                    readQuorumResult.RetryWithForceRefresh &&
                    !entity.RequestContext.ForceRefreshAddressCache)
                {
                    entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                    entity.RequestContext.ForceRefreshAddressCache = true;
                    readQuorumResult = await this.ReadMultipleReplicasInternalAsync(
                        entity,
                        includePrimary: includePrimary,
                        replicaCountToRead: replicaCountToRead,
                        requiresValidLsn: requiresValidLsn,
                        useSessionToken: useSessionToken,
                        readMode: readMode,
                        checkMinLSN: false,
                        forceReadAll: forceReadAll);
                }

                return readQuorumResult.Responses;

            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(entity, originalSessionToken);
            }
        }

        public async Task<StoreResult> ReadPrimaryAsync(
            DocumentServiceRequest entity,
            bool requiresValidLsn,
            bool useSessionToken)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            string originalSessionToken = entity.Headers[HttpConstants.HttpHeaders.SessionToken];
            try
            {
                ReadReplicaResult readQuorumResult = await this.ReadPrimaryInternalAsync(
                    entity, requiresValidLsn, useSessionToken);
                if (entity.RequestContext.PerformLocalRefreshOnGoneException &&
                    readQuorumResult.RetryWithForceRefresh &&
                    !entity.RequestContext.ForceRefreshAddressCache)
                {
                    entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                    entity.RequestContext.ForceRefreshAddressCache = true;
                    readQuorumResult = await this.ReadPrimaryInternalAsync(entity, requiresValidLsn, useSessionToken);
                }

                if (readQuorumResult.Responses.Count == 0)
                {
                    throw new GoneException(RMResources.Gone);
                }

                return readQuorumResult.Responses[0];
            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(entity, originalSessionToken);
            }

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

            IReadOnlyList<TransportAddressUri> resolveApiResults = await this.GetTransportAddressesAndClearSessionContainerIfCollectionRecreate(
                entity,
                includePrimary);

            ISessionToken requestSessionToken = this.GetRequestSessionTokenAndRemoveIfNotRequired(
                entity: entity,
                useSessionToken: useSessionToken,
                checkMinLSN: checkMinLSN);

            List<StoreResult> responseResult = new List<StoreResult>(replicaCountToRead);
            if (resolveApiResults.Count < replicaCountToRead)
            {
                if (!entity.RequestContext.ForceRefreshAddressCache)
                {
                    return new ReadReplicaResult(retryWithForceRefresh: true, responses: responseResult);
                }

                return new ReadReplicaResult(retryWithForceRefresh: false, responses: responseResult);
            }

            int replicasToRead = replicaCountToRead;
            bool enforceSessionCheck = EnforceSessionCheckBasedOnApiVersion(entity);

            this.UpdateContinuationTokenIfReadFeedOrQuery(entity);

            IEnumerator<TransportAddressUri> uriEnumerator;

            if (forceReadAll)
            {
                replicasToRead = resolveApiResults.Count;
                // All the replicas are being read so no reason to give a 
                // random order
                uriEnumerator = resolveApiResults.GetEnumerator();
            }
            else
            {
                uriEnumerator = this.addressEnumerator.GetTransportAddresses(resolveApiResults).GetEnumerator();
            }

            (bool isSuccess, TransportAddressUri transportAddressUri) ConcurrentGetTransportAddressUri()
            {
                lock (uriEnumerator)
                {
                    if (uriEnumerator.MoveNext())
                    {
                        return (true, uriEnumerator.Current);
                    }

                    return (false, null);
                }
            }

            

           Task<(StoreResult, OperationCanceledException, bool)>[] readStoreTasks = new Task<(StoreResult, OperationCanceledException, bool)>[replicasToRead];
            // Loop until we have the read quorum number of valid responses or if we have read all the replicas
            for (int i = 0; i < readStoreTasks.Length; i++)
            {
                entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

                readStoreTasks[i] = this.MultipleTaskHelperAsync(
                    entity,
                    ConcurrentGetTransportAddressUri,
                    readMode,
                    requiresValidLsn,
                    requestSessionToken,
                    enforceSessionCheck);
            }

            bool hasGoneException = false;
            Exception cancellationException = null;
            Exception exceptionToThrow = null;
            foreach (Task<(StoreResult, OperationCanceledException, bool)> task in readStoreTasks)
            {
                (StoreResult storeResult, OperationCanceledException operationCanceledException, bool hasGoneExceptionResponse) = await task;
                hasGoneException |= hasGoneExceptionResponse;

                if(operationCanceledException != null)
                {
                    cancellationException = operationCanceledException;
                }
                
                if(storeResult != null)
                {
                    responseResult.Add(storeResult);
                }
            }

            if (responseResult.Count < replicaCountToRead)
            {
                DefaultTrace.TraceInformation("Could not get quorum number of responses. " +
                    "ValidResponsesReceived: {0} ResponsesExpected: {1}, ResolvedAddressCount: {2}, ResponsesString: {3}",
                    responseResult.Count, replicaCountToRead, resolveApiResults.Count, String.Join(";", responseResult));

                if (hasGoneException)
                {
                    if (!entity.RequestContext.PerformLocalRefreshOnGoneException)
                    {
                        // If we are not supposed to act upon GoneExceptions here, just throw them
                        throw new GoneException(exceptionToThrow);
                    }
                    else if (!entity.RequestContext.ForceRefreshAddressCache)
                    {
                        // We could not obtain valid read quorum number of responses even when we went through all the secondary addresses
                        // Attempt force refresh and start over again.
                        return new ReadReplicaResult(retryWithForceRefresh: true, responses: responseResult);
                    }
                }
                else if (cancellationException != null)
                {
                    // We did not get the required number of responses and we encountered task cancellation on some/all of the store read tasks.
                    // We propagate the first cancellation exception we've found, or a new OperationCanceledException if none.
                    // The latter case can happen when Task.IsCanceled = true.
                    throw cancellationException ?? new OperationCanceledException();
                }
            }

            return new ReadReplicaResult(false, responseResult);
        }

        private async Task<(StoreResult storeResult, OperationCanceledException operationCanceled, bool hasGoneException)> MultipleTaskHelperAsync(
            DocumentServiceRequest entity,
            Func<(bool, TransportAddressUri)> TryGetTransportAddress,
            ReadMode readMode,
            bool requiresValidLsn,
            ISessionToken requestSessionToken,
            bool enforceSessionCheck)
        {
            bool hasGoneException = false;
            while (true)
            {
                (bool isSuccess, TransportAddressUri transportAddressUri) = TryGetTransportAddress();
                if (!isSuccess)
                {
                    return (null, null, hasGoneException);
                }

                StoreResult storeResult;
                try
                {
                    StoreResponse storeResponse = await this.ReadFromStoreAsync(
                            physicalAddress: transportAddressUri,
                            request: entity);

                    storeResult = StoreResult.CreateStoreResult(
                       storeResponse,
                       null,
                       requiresValidLsn,
                       this.canUseLocalLSNBasedHeaders && readMode != ReadMode.Strong,
                       transportAddressUri.Uri);

                    if (storeResponse != null)
                    {
                        lock (entity.RequestContext.ClientRequestStatistics.ContactedReplicas)
                        {
                            entity.RequestContext.ClientRequestStatistics.ContactedReplicas.Add(transportAddressUri.Uri);
                        }
                    }
                }
                catch (Exception exception)
                {
                    //All task exceptions are visited below.
                    DefaultTrace.TraceInformation("Exception {0} is thrown while doing readMany", exception);

                    // IsCanceled can be true with storeException being null if the async call
                    // gets canceled before it gets scheduled.
                    if (exception is OperationCanceledException operationCanceledException)
                    {
                        continue;
                    }

                    storeResult = StoreResult.CreateStoreResult(
                        null,
                        exception,
                        requiresValidLsn,
                        this.canUseLocalLSNBasedHeaders && readMode != ReadMode.Strong,
                        transportAddressUri.Uri);

                    if (exception != null && exception is TransportException)
                    {
                        lock (entity.RequestContext.ClientRequestStatistics.FailedReplicas)
                        {
                            entity.RequestContext.ClientRequestStatistics.FailedReplicas.Add(transportAddressUri.Uri);
                        }
                    }
                }

                entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.RequestCharge);
                entity.RequestContext.ClientRequestStatistics.RecordResponse(entity, storeResult);

                if (storeResult.IsValid)
                {
                    if (requestSessionToken == null
                        || (storeResult.SessionToken != null && requestSessionToken.IsValid(storeResult.SessionToken))
                        || (!enforceSessionCheck && storeResult.StatusCode != StatusCodes.NotFound))
                    {
                        return (storeResult, null, hasGoneException);
                    }
                }

                hasGoneException |= storeResult.StatusCode == StatusCodes.Gone && storeResult.SubStatusCode != SubStatusCodes.NameCacheIsStale;
            }
        }

        private static bool EnforceSessionCheckBasedOnApiVersion(DocumentServiceRequest entity)
        {
            string clientVersion = entity.Headers[HttpConstants.HttpHeaders.Version];
            bool enforceSessionCheck = !string.IsNullOrEmpty(clientVersion) && VersionUtility.IsLaterThan(clientVersion, HttpConstants.VersionDates.v2016_05_30);
            return enforceSessionCheck;
        }

        private ISessionToken GetRequestSessionTokenAndRemoveIfNotRequired(
            DocumentServiceRequest entity,
            bool useSessionToken,
            bool checkMinLSN)
        {
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

            return requestSessionToken;
        }

        private async Task<IReadOnlyList<TransportAddressUri>> GetTransportAddressesAndClearSessionContainerIfCollectionRecreate(
            DocumentServiceRequest entity,
            bool includePrimary)
        {
            string requestedCollectionRid = entity.RequestContext.ResolvedCollectionRid;

            IReadOnlyList<TransportAddressUri> resolveApiResults = await this.addressSelector.ResolveAllTransportAddressUriAsync(
                     entity,
                     includePrimary,
                     entity.RequestContext.ForceRefreshAddressCache);

            if (!string.IsNullOrEmpty(requestedCollectionRid) && !string.IsNullOrEmpty(entity.RequestContext.ResolvedCollectionRid))
            {
                if (!requestedCollectionRid.Equals(entity.RequestContext.ResolvedCollectionRid))
                {
                    this.sessionContainer.ClearTokenByResourceId(requestedCollectionRid);
                }
            }

            return resolveApiResults;
        }

        private async Task<ReadReplicaResult> ReadPrimaryInternalAsync(
            DocumentServiceRequest entity,
            bool requiresValidLsn,
            bool useSessionToken)
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

            Exception storeTaskException = null;
            StoreResponse storeResponse = null;
            try
            {
                this.UpdateContinuationTokenIfReadFeedOrQuery(entity);
                storeResponse = await this.ReadFromStoreAsync(
                    primaryUri,
                    entity);
            }
            catch (Exception exception)
            {
                storeTaskException = exception;
                DefaultTrace.TraceInformation("Exception {0} is thrown while doing Read Primary", exception);
            }

            StoreResult storeResult = StoreResult.CreateStoreResult(
                storeResponse,
                storeTaskException, requiresValidLsn,
                this.canUseLocalLSNBasedHeaders,
                primaryUri.Uri);


            entity.RequestContext.ClientRequestStatistics.RecordResponse(entity, storeResult);

            entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.RequestCharge);

            if (storeResult.StatusCode == StatusCodes.Gone && storeResult.SubStatusCode != SubStatusCodes.NameCacheIsStale)
            {
                return new ReadReplicaResult(true, new List<StoreResult>());
            }

            return new ReadReplicaResult(false, new StoreResult[] { storeResult });
        }

        private async Task<StoreResponse> ReadFromStoreAsync(
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
#endif
                    {
                        return await this.transportClient.InvokeResourceOperationAsync(
                        physicalAddress,
                        request);
                    }

                case OperationType.ReadFeed:
                case OperationType.Query:
                    {
                        QueryRequestPerformanceActivity activity = CustomTypeExtensions.StartActivity(request);
                        return await StoreReader.CompleteActivity(this.transportClient.InvokeResourceOperationAsync(
                            physicalAddress,
                            request),
                            activity);
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

        private void StartBackgroundAddressRefresh(DocumentServiceRequest request)
        {
            try
            {
                // ResolveAllTransportAddressUriAsync can modify the DSR properties in the background refresh.
                // DSR is not thread safe which can lead to race conditions. Clone the request to prevent 
                // any race condition
                DocumentServiceRequest clonedRequest = request.Clone();
                this.addressSelector.ResolveAllTransportAddressUriAsync(clonedRequest, true, true).ContinueWith((task) =>
                {
                    if (task.IsFaulted)
                    {
                        DefaultTrace.TraceWarning(
                            "Background refresh of the addresses failed with {0}", task.Exception.ToString());
                    }
                });
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceWarning("Background refresh of the addresses failed with {0}", exception.ToString());
            }
        }

        #region PrivateResultClasses
        private sealed class ReadReplicaResult
        {
            public ReadReplicaResult(bool retryWithForceRefresh, IList<StoreResult> responses)
            {
                this.RetryWithForceRefresh = retryWithForceRefresh;
                this.Responses = responses;
            }

            public bool RetryWithForceRefresh { get; private set; }

            public IList<StoreResult> Responses { get; private set; }
        }

        #endregion
    }
}
