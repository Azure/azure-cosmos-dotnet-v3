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
        private readonly ISessionContainer sessionContainer;
        private readonly bool canUseLocalLSNBasedHeaders;

        [ThreadStatic]
        private static Random random;

        public StoreReader(
            TransportClient transportClient,
            AddressSelector addressSelector,
            ISessionContainer sessionContainer)
        {
            this.transportClient = transportClient;
            this.addressSelector = addressSelector;
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

            List<StoreResult> responseResult = new List<StoreResult>();

            string requestedCollectionRid = entity.RequestContext.ResolvedCollectionRid;

            List<Uri> resolveApiResults = (await this.addressSelector.ResolveAllUriAsync(
                     entity,
                     includePrimary,
                     entity.RequestContext.ForceRefreshAddressCache))
                     .ToList();

            if (!string.IsNullOrEmpty(requestedCollectionRid) && !string.IsNullOrEmpty(entity.RequestContext.ResolvedCollectionRid))
            {
                if (!requestedCollectionRid.Equals(entity.RequestContext.ResolvedCollectionRid))
                {
                    this.sessionContainer.ClearTokenByResourceId(requestedCollectionRid);
                }
            }

            int resolvedAddressCount = resolveApiResults.Count;

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
                    return new ReadReplicaResult(retryWithForceRefresh: true , responses: responseResult);
                }

                return new ReadReplicaResult(retryWithForceRefresh: false, responses: responseResult);
            }

            int replicasToRead = replicaCountToRead;

            string clientVersion = entity.Headers[HttpConstants.HttpHeaders.Version];
            bool enforceSessionCheck = !string.IsNullOrEmpty(clientVersion) && VersionUtility.IsLaterThan(clientVersion, HttpConstants.VersionDates.v2016_05_30);

            bool hasGoneException = false;
            bool hasCancellationException = false;
            Exception cancellationException = null;
            Exception exceptionToThrow = null;
            // Loop until we have the read quorum number of valid responses or if we have read all the replicas
            while (replicasToRead > 0 && resolveApiResults.Count > 0)
            {
                entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();
                Dictionary<Task<StoreResponse>, Uri> readStoreTasks = new Dictionary<Task<StoreResponse>, Uri>();
                int uriIndex = StoreReader.GenerateNextRandom(resolveApiResults.Count);
                this.UpdateContinuationTokenIfReadFeedOrQuery(entity);

                while (resolveApiResults.Count > 0)
                {
                    uriIndex = uriIndex % resolveApiResults.Count;

                    readStoreTasks.Add(this.ReadFromStoreAsync(
                            physicalAddress: resolveApiResults[uriIndex],
                            request: entity), 
                        resolveApiResults[uriIndex]);
                    resolveApiResults.RemoveAt(uriIndex);

                    if(!forceReadAll && readStoreTasks.Count == replicasToRead)
                    {
                        break;
                    }
                }

                replicasToRead = readStoreTasks.Count >= replicasToRead ? 0 : replicasToRead - readStoreTasks.Count;

                try
                {
                    await Task.WhenAll(readStoreTasks.Keys);
                }
                catch (Exception exception)
                {
                    //All task exceptions are visited below.
                    DefaultTrace.TraceInformation("Exception {0} is thrown while doing readMany", exception);
                    exceptionToThrow = exception;
                }

                foreach (Task<StoreResponse> readTask in readStoreTasks.Keys)
                {
                    StoreResponse storeResponse = readTask.Status == TaskStatus.RanToCompletion ? readTask.Result : null;
                    Exception storeException = readTask.Exception?.InnerException;

                    // IsCanceled can be true with storeException being null if the async call
                    // gets canceled before it gets scheduled.
                    if (readTask.IsCanceled || storeException is OperationCanceledException)
                    {
                        hasCancellationException = true;
                        cancellationException ??= storeException;
                        continue;
                    }

                    Uri targetUri = readStoreTasks[readTask];

                    StoreResult storeResult = StoreResult.CreateStoreResult(
                        storeResponse,
                        storeException, requiresValidLsn,
                        this.canUseLocalLSNBasedHeaders && readMode != ReadMode.Strong,
                        targetUri);

                    entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.RequestCharge);

                    if (storeResponse != null)
                    {
                        entity.RequestContext.ClientRequestStatistics.ContactedReplicas.Add(targetUri);
                    }

                    if (storeException != null && storeException.InnerException is TransportException)
                    {
                        entity.RequestContext.ClientRequestStatistics.FailedReplicas.Add(targetUri);
                    }

                    entity.RequestContext.ClientRequestStatistics.RecordResponse(entity, storeResult);

                    if (storeResult.IsValid)
                    {
                        if (requestSessionToken == null
                            || (storeResult.SessionToken != null && requestSessionToken.IsValid(storeResult.SessionToken))
                            || (!enforceSessionCheck && storeResult.StatusCode != StatusCodes.NotFound))
                        {
                            responseResult.Add(storeResult);
                        }
                    }

                    hasGoneException |= storeResult.StatusCode == StatusCodes.Gone && storeResult.SubStatusCode != SubStatusCodes.NameCacheIsStale;
                }
                if (responseResult.Count >= replicaCountToRead)
                {
                    if (hasGoneException && !entity.RequestContext.PerformedBackgroundAddressRefresh)
                    {
                        this.StartBackgroundAddressRefresh(entity);
                        entity.RequestContext.PerformedBackgroundAddressRefresh = true;
                    }

                    return new ReadReplicaResult(false, responseResult);
                }

                // Remaining replicas
                replicasToRead = replicaCountToRead - responseResult.Count;
            }

            if (responseResult.Count < replicaCountToRead)
            {
                DefaultTrace.TraceInformation("Could not get quorum number of responses. " +
                    "ValidResponsesReceived: {0} ResponsesExpected: {1}, ResolvedAddressCount: {2}, ResponsesString: {3}",
                    responseResult.Count, replicaCountToRead, resolvedAddressCount, String.Join(";", responseResult));

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
                else if (hasCancellationException)
                {
                    // We did not get the required number of responses and we encountered task cancellation on some/all of the store read tasks.
                    // We propagate the first cancellation exception we've found, or a new OperationCanceledException if none.
                    // The latter case can happen when Task.IsCanceled = true.
                    throw cancellationException ?? new OperationCanceledException();
                }
            }

            return new ReadReplicaResult(false, responseResult);
        }

        private async Task<ReadReplicaResult> ReadPrimaryInternalAsync(
            DocumentServiceRequest entity,
            bool requiresValidLsn,
            bool useSessionToken)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            Uri primaryUri = await this.addressSelector.ResolvePrimaryUriAsync(
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
                primaryUri);

            entity.RequestContext.ClientRequestStatistics.RecordResponse(entity, storeResult);

            entity.RequestContext.RequestChargeTracker.AddCharge(storeResult.RequestCharge);

            if (storeResult.StatusCode == StatusCodes.Gone && storeResult.SubStatusCode != SubStatusCodes.NameCacheIsStale)
            {
                return new ReadReplicaResult(true, new List<StoreResult>());
            }

            return new ReadReplicaResult(false, new StoreResult[] { storeResult });
        }

        private async Task<StoreResponse> ReadFromStoreAsync(
            Uri physicalAddress,
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
                this.addressSelector.ResolveAllUriAsync(request, true, true).ContinueWith((task)=>
                {
                    if(task.IsFaulted)
                    {
                        DefaultTrace.TraceWarning(
                            "Background refresh of the addresses failed with {0}", task.Exception.ToString());
                    }
                });
            }
            catch(Exception exception)
            {
                DefaultTrace.TraceWarning("Background refresh of the addresses failed with {0}", exception.ToString());
            }
        }

        private static int GenerateNextRandom(int maxValue)
        {
            if (StoreReader.random == null)
            {
                // Generate random numbers with a seed so that not all the threads without random available
                // start producing the same sequence.
                StoreReader.random = CustomTypeExtensions.GetRandomNumber();
            }

            return StoreReader.random.Next(maxValue);
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
