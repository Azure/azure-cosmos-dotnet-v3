//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Collections;

    /*
     ConsistencyLevel      Replication Mode         Desired ReadMode
     -------------------   --------------------     ---------------------------------------------------------------------------
     Strong                  Synchronous              Read from Read Quorum
                             Asynchronous             Not supported
                             
     Bounded Staleness       Synchronous              Read from Read Quorum
                             Asynchronous             Read from Read Quorum. Performing read barrier on Primary is unsupported.
                            
     Session                 Sync/Async               Read Any (With LSN Cookie) 
                                                      Default to Primary as last resort (which should succeed always)
                                                      
     Eventual                Sync/Async               Read Any 
     
     Client does validation of unsupported combinations.    
     
     
     Preliminaries
     =============
     1. We do primary copy/single master replication. 
     2. We do sync or async replication depending on the value of DefaultConsistencyLevel on a database account.     
     If the database account is configured with DefaultConsistencyLevel = Strong, we do sync replication. By default, for all other values of DefaultConsistencyLevel, we do asynchronous replication.    
     
     Replica set
     ===========
     We define N as the current number of replicas protecting a partition.
     At any given point, the value of N can fluctuate between NMax and NMin. 
     NMax is called the target replica set size and NMin is called the minimum write availability set size. 
     NMin and NMax are statically defined whereas N is dynamic.
     Dynamic replica set is great for dealing with successive failures. 
     Since N fluctuates between NMax and NMin, the value of N at the time of calculation of W may not be the same when R is calculated. 
     This is a side effect of dynamic quorum and requires careful consideration.
     
     NMin = 2, NMax >= 3     
     
     Simultaneous Failures
     =====================
     In general N replicas imply 2f+1 simultaneous failures
     N = 5 allows for 2  simultaneous failures
     N = 4 allows for 1 failure
     N = 3 allows for 1 failure
     N < 3 allows for 0 failures     
      
     Quorums
     =======
     W = Write Quorum = Number of replicas which acknowledge a write before the primary can ack the client. It is majority set i.e. N/2 + 1
     R = Read Quorum = Set of replicas such that there is non-empty intersection between W and R that constitute N i.e. R = N -W + 1

     For sync replication, W is used as a majority quorum. 
     For async replication, W = 1. We have two LSNs, one is quorum acknowledged LSN (LSN-Q) and another is what is visible to the client (LSN-C). 
     LSN-Q is the stable LSN which corresponds to the write quorum of Windows Fabric. LSN-C is unstable and corresponds to W=1.  
     
     Assumptions
     ===========
     Nmin <= N <= Nmax
     W >= N/2 + 1
     R = N -W + 1
     
     N from read standpoint means number of address from BE which is returning successful response.
     Successful reponse: Any BE response containing LSN response header is considered successful reponse. Typically every response other than 410 is treated as succesful response.
      
     Strong Consistency
     ==================
     Strong Read requires following guarantees. 
     * Read value is the latest that has been written. If a write operation finished. Any subsequent reads should see that value.
     * Monotonic guarantee. Any read that starts after a previous read operation, should see atleast return equal or higher version of the value.
     
     To perform strong read we require that atleast R i.e. Read Quorum number of replicas have the value committed. To acheve that such read :
     * Read R replicas. If they have the same LSN, use the read result
     * If they don't have the same LSN, we will either return the result with the highest LSN observed from those R replicas, after ensuring that LSN 
       becomes available with R replicas.
     * Secondary replicas are always preferred for reading. If R secondaries have returned the result but cannot agree on the resulting LSN, we can include Primary to satisfy read quorum.
     * If we only have R replicas (i.e. N==R), we include primary in reading the result and validate N==R.
     
     Bounded Staleness 
     =================
     Sync Replication: 
     Bounded staleness uses the same logic as Strong for cases where the server is using sync replication.
     
     Async Replication: 
     For async replication, we make sure that we do not use the Primary as barrier for read quorum. This is because Primary is always going to run ahead (async replication uses W=1 on Primary).
     Using primary would voilate the monotonic read guarantees when we fall back to reading from secondary in the subsequent reads as they are always running slower as compared to Primary.

     Session
     =======
     We read from secondaries one by one until we find a match for the client's session token (LSN-C). 
     We go to primary as a last resort which should satisfy LSN-C.
     
     Availability for Bounded Staleness (for NMax = 4 and NMin = 2):
     When there is a partition, the minority quorum can remain available for read as long as N >= 1 
     When there is a partition, the minority quorum can remain available for writes as long as N >= 2

     Eventual
     ========
     We can read from any replicas.
     
     Availability for Bounded Staleness (for NMax = 4 and NMin = 2):
     When there is a partition, the minority quorum can remain available for read as long as N >= 1 
     When there is a partition, the minority quorum can remain available for writes as long as N >= 2

     Read Retry logic   
     -----------------
     For Any NonQuorum Reads(A.K.A ReadAny); AddressCache is refreshed for following condition.
      1) No Secondary Address is found in Address Cache.
      2) Chosen Secondary Returned GoneException/EndpointNotFoundException.
      
     For Quorum Read address cache is refreshed on following condition.
      1) We found only R secondary where R < RMAX.
      2) We got GoneException/EndpointNotFoundException on all the secondary we contacted.
     
     */
    /// <summary>
    /// ConsistencyReader has a dependency on both StoreReader and QuorumReader. For Bounded Staleness and Strong Consistency, it uses the Quorum Reader
    /// to converge on a read from read quorum number of replicas. 
    /// For Session and Eventual Consistency, it directly uses the store reader.
    /// </summary>
    [SuppressMessage("", "AvoidMultiLineComments", Justification = "Multi line business logic")]
    internal sealed class ConsistencyReader
    {
        private const int maxNumberOfSecondaryReadRetries = 3;

        private readonly AddressSelector addressSelector;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly IAuthorizationTokenProvider authorizationTokenProvider;
        private readonly StoreReader storeReader;
        private readonly QuorumReader quorumReader;

        public ConsistencyReader(
            AddressSelector addressSelector,
            ISessionContainer sessionContainer,
            TransportClient transportClient,
            IServiceConfigurationReader serviceConfigReader,
            IAuthorizationTokenProvider authorizationTokenProvider)
        {
            this.addressSelector = addressSelector;
            this.serviceConfigReader = serviceConfigReader;
            this.authorizationTokenProvider = authorizationTokenProvider;
            this.storeReader = new StoreReader(transportClient, addressSelector, sessionContainer);
            this.quorumReader = new QuorumReader(transportClient, addressSelector, this.storeReader, serviceConfigReader, authorizationTokenProvider);
        }

        // Test Hook
        public string LastReadAddress
        {
            get
            {
                return this.storeReader.LastReadAddress;
            }
            set
            {
                this.storeReader.LastReadAddress = value;
            }
        }

        public Task<StoreResponse> ReadAsync(
            DocumentServiceRequest entity,
            TimeoutHelper timeout,
            bool isInRetry,
            bool forceRefresh,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!isInRetry)
            {
                timeout.ThrowTimeoutIfElapsed();
            }
            else
            {
                timeout.ThrowGoneIfElapsed();
            }
            
            entity.RequestContext.TimeoutHelper = timeout;

            if (entity.RequestContext.RequestChargeTracker == null)
            {
                entity.RequestContext.RequestChargeTracker = new RequestChargeTracker();
            }

            if(entity.RequestContext.ClientRequestStatistics == null)
            {
                entity.RequestContext.ClientRequestStatistics = new ClientSideRequestStatistics();
            }

            entity.RequestContext.ForceRefreshAddressCache = forceRefresh;

            ConsistencyLevel targetConsistencyLevel;
            bool useSessionToken;
            ReadMode desiredReadMode = this.DeduceReadMode(entity, out targetConsistencyLevel, out useSessionToken);

            int maxReplicaCount = this.GetMaxReplicaSetSize(entity);
            int readQuorumValue = maxReplicaCount - (maxReplicaCount / 2);

            switch (desiredReadMode)
            {
                case ReadMode.Primary:
                    return this.ReadPrimaryAsync(entity, useSessionToken);

                case ReadMode.Strong:
                    entity.RequestContext.PerformLocalRefreshOnGoneException = true;
                    return this.quorumReader.ReadStrongAsync(entity, readQuorumValue, desiredReadMode);

                case ReadMode.BoundedStaleness:
                    entity.RequestContext.PerformLocalRefreshOnGoneException = true;

                    // for bounded staleness, we are defaulting to read strong for local region reads. 
                    // this can be done since we are always running with majority quorum w = 3 (or 2 during quorum downshift).
                    // This means that the primary will always be part of the write quorum, and 
                    // therefore can be included for barrier reads. 

                    // NOTE: this assumes that we are running with SYNC replication (i.e. majority quorum).
                    // When we run on a minority write quorum(w=2), to ensure monotonic read guarantees 
                    // we always contact two secondary replicas and exclude primary. 
                    // However, this model significantly reduces availability and available throughput for serving reads for bounded staleness during reconfiguration.
                    // Therefore, to ensure monotonic read guarantee from any replica set we will just use regular quorum read(R=2) since our write quorum is always majority(W=3)
                    return this.quorumReader.ReadStrongAsync(entity, readQuorumValue, desiredReadMode);

                case ReadMode.Any:
                    if (targetConsistencyLevel == ConsistencyLevel.Session)
                    {
                        return BackoffRetryUtility<StoreResponse>.ExecuteAsync(
                            callbackMethod: () => this.ReadSessionAsync(entity, desiredReadMode),
                            retryPolicy: new SessionTokenMismatchRetryPolicy(),
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        return this.ReadAnyAsync(entity, desiredReadMode);
                    }

                default:
                    throw new InvalidOperationException();
            }
        }

        async private Task<StoreResponse> ReadPrimaryAsync(
            DocumentServiceRequest entity,
            bool useSessionToken)
        {
            
            StoreResult response = await this.storeReader.ReadPrimaryAsync(
                    entity,
                    requiresValidLsn: false,
                    useSessionToken: useSessionToken);
            return response.ToResponse();
        }

        async private Task<StoreResponse> ReadAnyAsync(
            DocumentServiceRequest entity,
            ReadMode readMode)
        {
            IList<StoreResult> responses = await this.storeReader.ReadMultipleReplicaAsync(
                    entity,
                    includePrimary: true,
                    replicaCountToRead: 1,
                    requiresValidLsn: false,
                    useSessionToken: false,
                    readMode: readMode);

            if(responses.Count == 0)
            {
                throw new GoneException(RMResources.Gone);
            }

            return responses[0].ToResponse();
        }

        private async Task<StoreResponse> ReadSessionAsync(
            DocumentServiceRequest entity,
            ReadMode readMode)
        {
            entity.RequestContext.TimeoutHelper.ThrowGoneIfElapsed();

            IList<StoreResult> responses = await this.storeReader.ReadMultipleReplicaAsync(
                    entity:entity,
                    includePrimary:true, 
                    replicaCountToRead:1,
                    requiresValidLsn:true,
                    useSessionToken:true,
                    checkMinLSN: true,
                    readMode: readMode);

            if (responses.Count > 0)
            {
                try
                {
                    StoreResponse storeResponse = responses[0].ToResponse(entity.RequestContext.RequestChargeTracker);
                    if (storeResponse.Status == (int)HttpStatusCode.NotFound && entity.IsValidStatusCodeForExceptionlessRetry(storeResponse.Status))
                    {
                        if (entity.RequestContext.SessionToken != null && responses[0].SessionToken != null && !entity.RequestContext.SessionToken.IsValid(responses[0].SessionToken))
                        {
                            DefaultTrace.TraceInformation("Convert to session read exception, request {0} Session Lsn {1}, responseLSN {2}", entity.ResourceAddress, entity.RequestContext.SessionToken.ConvertToString(), responses[0].LSN);

                            INameValueCollection headers = new DictionaryNameValueCollection();
                            headers.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString());
                            throw new NotFoundException(RMResources.ReadSessionNotAvailable, headers);
                        }
                    }
                    return storeResponse;
                }
                catch (NotFoundException notFoundException)
                {
                    if (entity.RequestContext.SessionToken != null && responses[0].SessionToken != null && !entity.RequestContext.SessionToken.IsValid(responses[0].SessionToken))
                    {
                        DefaultTrace.TraceInformation("Convert to session read exception, request {0} Session Lsn {1}, responseLSN {2}", entity.ResourceAddress, entity.RequestContext.SessionToken.ConvertToString(), responses[0].LSN);
                        notFoundException.Headers.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString());
                    }
                    throw notFoundException;
                }
            }

            INameValueCollection responseHeaders = new DictionaryNameValueCollection();
            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus,  ((int)SubStatusCodes.ReadSessionNotAvailable).ToString());
            ISessionToken requestSessionToken = entity.RequestContext.SessionToken;
            DefaultTrace.TraceInformation("Fail the session read {0}, request session token {1}", entity.ResourceAddress, requestSessionToken == null ? "<empty>" : requestSessionToken.ConvertToString());
            throw new NotFoundException(RMResources.ReadSessionNotAvailable, responseHeaders);
        }

        private ReadMode DeduceReadMode(DocumentServiceRequest request, out ConsistencyLevel targetConsistencyLevel, out bool useSessionToken)
        {
            targetConsistencyLevel = RequestHelper.GetConsistencyLevelToUse(this.serviceConfigReader, request);

            useSessionToken = targetConsistencyLevel == ConsistencyLevel.Session;

            if (request.DefaultReplicaIndex.HasValue)
            {
                // Don't use session token - this is used by internal scenarios which technically don't intend session read when they target
                // request to specific replica.
                useSessionToken = false;
                return ReadMode.Primary;  //Let the addressResolver decides which replica to connect to.
            }

            switch (targetConsistencyLevel)
            {
                case ConsistencyLevel.Eventual:
                    return ReadMode.Any;

                case ConsistencyLevel.ConsistentPrefix:
                    return ReadMode.Any;

                case ConsistencyLevel.Session:
                    return ReadMode.Any;

                case ConsistencyLevel.BoundedStaleness:
                    return ReadMode.BoundedStaleness;

                case ConsistencyLevel.Strong:
                    return ReadMode.Strong;

                default:
                    throw new InvalidOperationException();
            }
        }

        public int GetMaxReplicaSetSize(DocumentServiceRequest entity)
        {
            bool isMasterResource = ReplicatedResourceClient.IsReadingFromMaster(entity.ResourceType, entity.OperationType);
            if (isMasterResource)
            {
                return this.serviceConfigReader.SystemReplicationPolicy.MaxReplicaSetSize;
            }
            else
            {
                return this.serviceConfigReader.UserReplicationPolicy.MaxReplicaSetSize;
            }
        }

        public int GetMinReplicaSetSize(DocumentServiceRequest entity)
        {
            bool isMasterResource = ReplicatedResourceClient.IsReadingFromMaster(entity.ResourceType, entity.OperationType);
            if (isMasterResource)
            {
                return this.serviceConfigReader.SystemReplicationPolicy.MinReplicaSetSize;
            }
            else
            {
                return this.serviceConfigReader.UserReplicationPolicy.MinReplicaSetSize;
            }
        }
    }
}
