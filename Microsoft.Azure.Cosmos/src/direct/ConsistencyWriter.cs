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

     */

    [SuppressMessage("", "AvoidMultiLineComments", Justification = "Multi line business logic")]
    internal sealed class ConsistencyWriter
    {
        private const int maxNumberOfWriteBarrierReadRetries = 30;
        private const int delayBetweenWriteBarrierCallsInMs = 30;

        private const int maxShortBarrierRetriesForMultiRegion = 4;
        private const int shortbarrierRetryIntervalInMsForMultiRegion = 10;

        private readonly StoreReader storeReader;
        private readonly TransportClient transportClient;
        private readonly AddressSelector addressSelector;
        private readonly ISessionContainer sessionContainer;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly IAuthorizationTokenProvider authorizationTokenProvider;
        private readonly bool useMultipleWriteLocations;

        public ConsistencyWriter(
            AddressSelector addressSelector,
            ISessionContainer sessionContainer,
            TransportClient transportClient,
            IServiceConfigurationReader serviceConfigReader,
            IAuthorizationTokenProvider authorizationTokenProvider,
            bool useMultipleWriteLocations)
        {
            this.transportClient = transportClient;
            this.addressSelector = addressSelector;
            this.sessionContainer = sessionContainer;
            this.serviceConfigReader = serviceConfigReader;
            this.authorizationTokenProvider = authorizationTokenProvider;
            this.useMultipleWriteLocations = useMultipleWriteLocations;
            this.storeReader = new StoreReader(
                                    transportClient,
                                    addressSelector,
                                    sessionContainer: null); //we need store reader only for global strong, no session is needed*/
        }

        // Test hook
        internal string LastWriteAddress
        {
            get;
            private set;
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
                    retryPolicy: new SessionTokenMismatchRetryPolicy(),
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
                request.RequestContext.ClientRequestStatistics.ContactedReplicas = partitionPerProtocolAddress.ReplicaUris.ToList();

                Uri primaryUri = partitionPerProtocolAddress.GetPrimaryUri(request);
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

                StoreResult storeResult = null;
                try
                {
                    response = await this.transportClient.InvokeResourceOperationAsync(primaryUri, request);

                    storeResult = StoreResult.CreateStoreResult(response, null, true, false, primaryUri);

                }
                catch (Exception ex)
                {
                    storeResult = StoreResult.CreateStoreResult(null, ex, true, false, primaryUri);

                    if (ex is DocumentClientException)
                    {
                        DocumentClientException dce = (DocumentClientException)ex;
                        string value = dce.Headers[HttpConstants.HttpHeaders.WriteRequestTriggerAddressRefresh];
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            int result;
                            if (int.TryParse(dce.Headers.GetValues(HttpConstants.HttpHeaders.WriteRequestTriggerAddressRefresh)[0],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out result) && result == 1)
                            {
                                this.StartBackgroundAddressRefresh(request);
                            }
                        }
                    }
                }

                if (storeResult == null)
                {
                    Debug.Assert(false, "StoreResult cannot be null at this point.");
                    DefaultTrace.TraceCritical("ConsistencyWriter did not get storeResult!");
                    throw new InternalServerErrorException();
                }

                request.RequestContext.ClientRequestStatistics.RecordResponse(
                    request,
                    storeResult);

                if (ReplicatedResourceClient.IsGlobalStrongEnabled() && this.ShouldPerformWriteBarrierForGlobalStrong(storeResult))
                {
                    long lsn = storeResult.LSN;
                    long globalCommittedLsn = storeResult.GlobalCommittedLSN;

                    if (lsn == -1 || globalCommittedLsn == -1)
                    {
                        DefaultTrace.TraceWarning("ConsistencyWriter: LSN {0} or GlobalCommittedLsn {1} is not set for global strong request",
                            lsn, globalCommittedLsn);
                        throw new GoneException(RMResources.Gone);
                    }

                    request.RequestContext.GlobalStrongWriteStoreResult = storeResult;
                    request.RequestContext.GlobalCommittedSelectedLSN = lsn;

                    //if necessary we would have already refreshed cache by now.
                    request.RequestContext.ForceRefreshAddressCache = false;

                    DefaultTrace.TraceInformation("ConsistencyWriter: globalCommittedLsn {0}, lsn {1}", globalCommittedLsn, lsn);
                    //barrier only if necessary, i.e. when write region completes write, but read regions have not.
                    if (globalCommittedLsn < lsn)
                    {
                        using (DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(request, this.authorizationTokenProvider, null, request.RequestContext.GlobalCommittedSelectedLSN))
                        {
                            if (!await this.WaitForWriteBarrierAsync(barrierRequest, request.RequestContext.GlobalCommittedSelectedLSN))
                            {
                                DefaultTrace.TraceError("ConsistencyWriter: Write barrier has not been met for global strong request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                                throw new GoneException(RMResources.GlobalStrongWriteBarrierNotMet);
                            }
                        }
                    }
                }
                else
                {
                    return storeResult.ToResponse();
                }
            }
            else
            {
                using (DocumentServiceRequest barrierRequest = await BarrierRequestHelper.CreateAsync(request, this.authorizationTokenProvider, null, request.RequestContext.GlobalCommittedSelectedLSN))
                {
                    if (!await this.WaitForWriteBarrierAsync(barrierRequest, request.RequestContext.GlobalCommittedSelectedLSN))
                    {
                        DefaultTrace.TraceWarning("ConsistencyWriter: Write barrier has not been met for global strong request. SelectedGlobalCommittedLsn: {0}", request.RequestContext.GlobalCommittedSelectedLSN);
                        throw new GoneException(RMResources.GlobalStrongWriteBarrierNotMet);
                    }
                }
            }

            return request.RequestContext.GlobalStrongWriteStoreResult.ToResponse();
        }

        private bool ShouldPerformWriteBarrierForGlobalStrong(StoreResult storeResult)
        {
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

        private async Task<bool> WaitForWriteBarrierAsync(DocumentServiceRequest barrierRequest, long selectedGlobalCommittedLsn)
        {
            int writeBarrierRetryCount = ConsistencyWriter.maxNumberOfWriteBarrierReadRetries;

            long maxGlobalCommittedLsnReceived = 0;
            while (writeBarrierRetryCount-- > 0)
            {
                barrierRequest.RequestContext.TimeoutHelper.ThrowTimeoutIfElapsed();
                IList<StoreResult> responses = await this.storeReader.ReadMultipleReplicaAsync(
                    barrierRequest,
                    includePrimary: true,
                    replicaCountToRead: 1, // any replica with correct globalCommittedLsn is good enough
                    requiresValidLsn: false,
                    useSessionToken: false,
                    readMode: ReadMode.Strong,
                    checkMinLSN: false,
                    forceReadAll: false);

                if (responses != null && responses.Any(response => response.GlobalCommittedLSN >= selectedGlobalCommittedLsn))
                {
                    return true;
                }

                //get max global committed lsn from current batch of responses, then update if greater than max of all batches.
                long maxGlobalCommittedLsn = responses != null ? responses.Select(s => s.GlobalCommittedLSN).DefaultIfEmpty(0).Max() : 0;
                maxGlobalCommittedLsnReceived = maxGlobalCommittedLsnReceived > maxGlobalCommittedLsn ? maxGlobalCommittedLsnReceived : maxGlobalCommittedLsn;

                //only refresh on first barrier call, set to false for subsequent attempts.
                barrierRequest.RequestContext.ForceRefreshAddressCache = false;

                //trace on last retry.
                if (writeBarrierRetryCount == 0)
                {
                    DefaultTrace.TraceInformation("ConsistencyWriter: WaitForWriteBarrierAsync - Last barrier multi-region strong. Responses: {0}",
                        string.Join("; ", responses));
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

        private void StartBackgroundAddressRefresh(DocumentServiceRequest request)
        {
            try
            {
                this.addressSelector.ResolvePrimaryUriAsync(request, true).ContinueWith((task) =>
                {
                    if (task.IsFaulted)
                    {
                        DefaultTrace.TraceWarning(
                            "Background refresh of the primary address failed with {0}", task.Exception.ToString());
                    }
                });
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceWarning("Background refresh of the primary address failed with {0}", exception.ToString());
            }
        }
    }
}
