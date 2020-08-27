//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// ReplicatedResourceClient uses the ConsistencyReader to make requests to backend
    /// </summary>
    internal sealed class ReplicatedResourceClient
    {
        private const string EnableGlobalStrongConfigurationName = "EnableGlobalStrong";
        private const int GoneAndRetryWithRetryTimeoutInSeconds = 30;
        private const int StrongGoneAndRetryWithRetryTimeoutInSeconds = 60;

        private readonly TimeSpan minBackoffForFallingBackToOtherRegions = TimeSpan.FromSeconds(1);

        private readonly AddressSelector addressSelector;
        private readonly IAddressResolver addressResolver;
        private readonly ConsistencyReader consistencyReader;
        private readonly ConsistencyWriter consistencyWriter;
        private readonly Protocol protocol;
        private readonly TransportClient transportClient;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly bool enableReadRequestsFallback;
        private readonly bool useMultipleWriteLocations;
        private readonly bool detectClientConnectivityIssues;
        private readonly RetryWithConfiguration retryWithConfiguration;

        private static readonly Lazy<bool> enableGlobalStrong = new Lazy<bool>(() => {
            bool isGlobalStrongEnabled = true;
#if !(NETSTANDARD15 || NETSTANDARD16)
#if NETSTANDARD20
        // GetEntryAssembly returns null when loaded from native netstandard2.0
        if (System.Reflection.Assembly.GetEntryAssembly() != null)
        {
#endif
            string isGlobalStrongEnabledConfig = System.Configuration.ConfigurationManager.AppSettings[ReplicatedResourceClient.EnableGlobalStrongConfigurationName];
            if (!string.IsNullOrEmpty(isGlobalStrongEnabledConfig))
            {
                if (!bool.TryParse(isGlobalStrongEnabledConfig, out isGlobalStrongEnabled))
                {
                    return false;
                }
            }
#if NETSTANDARD20
        }
#endif
#endif
            return isGlobalStrongEnabled;
        });

        public ReplicatedResourceClient(
            IAddressResolver addressResolver,
            ISessionContainer sessionContainer,
            Protocol protocol,
            TransportClient transportClient,
            IServiceConfigurationReader serviceConfigReader,
            IAuthorizationTokenProvider authorizationTokenProvider,
            bool enableReadRequestsFallback,
            bool useMultipleWriteLocations,
            bool detectClientConnectivityIssues,
            RetryWithConfiguration retryWithConfiguration = null)
        {
            this.addressResolver = addressResolver;
            this.addressSelector = new AddressSelector(addressResolver, protocol);
            if (protocol != Protocol.Https && protocol != Protocol.Tcp)
            {
                throw new ArgumentOutOfRangeException("protocol");
            }

            this.protocol = protocol;
            this.transportClient = transportClient;
            this.serviceConfigReader = serviceConfigReader;

            this.consistencyReader = new ConsistencyReader(
                this.addressSelector,
                sessionContainer,
                transportClient,
                serviceConfigReader,
                authorizationTokenProvider);
            this.consistencyWriter = new ConsistencyWriter(
                this.addressSelector,
                sessionContainer,
                transportClient,
                serviceConfigReader,
                authorizationTokenProvider,
                useMultipleWriteLocations);
            this.enableReadRequestsFallback = enableReadRequestsFallback;
            this.useMultipleWriteLocations = useMultipleWriteLocations;
            this.detectClientConnectivityIssues = detectClientConnectivityIssues;
            this.retryWithConfiguration = retryWithConfiguration;
        }

        #region Test hooks
        public string LastReadAddress
        {
            get
            {
                return this.consistencyReader.LastReadAddress;
            }

            set
            {
                this.consistencyReader.LastReadAddress = value;
            }
        }

        public string LastWriteAddress
        {
            get
            {
                return this.consistencyWriter.LastWriteAddress;
            }
        }

        public bool ForceAddressRefresh
        {
            get;
            set;
        }

        /// <summary>
        /// Overrides retry policy timeout for testing purposes.
        /// </summary>
        public int? GoneAndRetryWithRetryTimeoutInSecondsOverride
        {
            get;
            set;
        }
        #endregion

        public Task<StoreResponse> InvokeAsync(DocumentServiceRequest request, Func<DocumentServiceRequest, Task> prepareRequestAsyncDelegate = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Func<GoneAndRetryRequestRetryPolicyContext, Task<StoreResponse>> funcDelegate = async (GoneAndRetryRequestRetryPolicyContext contextArguments) =>
            {
                if (prepareRequestAsyncDelegate != null)
                {
                    await prepareRequestAsyncDelegate(request);
                }

                request.Headers[HttpConstants.HttpHeaders.ClientRetryAttemptCount] = contextArguments.ClientRetryCount.ToString(CultureInfo.InvariantCulture);
                request.Headers[HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest] = contextArguments.RemainingTimeInMsOnClientRequest.TotalMilliseconds.ToString();

                return await this.InvokeAsync(
                    request,
                    new TimeoutHelper(contextArguments.RemainingTimeInMsOnClientRequest, cancellationToken),
                    contextArguments.IsInRetry,
                    contextArguments.ForceRefresh || this.ForceAddressRefresh,
                    cancellationToken);
            };

            Func<GoneAndRetryRequestRetryPolicyContext, Task<StoreResponse>> inBackoffFuncDelegate = null;

            //we will enable fallback to other regions if the following conditions are met:
            // 1. request is a read operation AND
            // 2. enableReadRequestsFallback is set to true. (can only ever be true if direct mode, on client)
            // 3. write requests that can be retried
            if ((request.OperationType.IsReadOperation() && this.enableReadRequestsFallback) ||
                this.CheckWriteRetryable(request))
            {
                IClientSideRequestStatistics sharedStatistics = null;

                if (request.RequestContext.ClientRequestStatistics == null)
                {
                    sharedStatistics = new ClientSideRequestStatistics();
                    request.RequestContext.ClientRequestStatistics = sharedStatistics;
                }
                else
                {
                    sharedStatistics = request.RequestContext.ClientRequestStatistics;
                }

                //clone all new requests from this fresh clone, as this isnt polluted by interim state.
                //also create shared statistics object for use by all clones.
                DocumentServiceRequest freshRequest = request.Clone();

                inBackoffFuncDelegate = async (GoneAndRetryRequestRetryPolicyContext retryContext) =>
                {
                    DocumentServiceRequest requestClone = freshRequest.Clone();
                    requestClone.RequestContext.ClientRequestStatistics = sharedStatistics;

                    if (prepareRequestAsyncDelegate != null)
                    {
                        await prepareRequestAsyncDelegate(requestClone);
                    }

                    DefaultTrace.TraceInformation("Executing inBackoffAlternateCallbackMethod on regionIndex {0}", retryContext.RegionRerouteAttemptCount);
                    requestClone.RequestContext.RouteToLocation(
                        retryContext.RegionRerouteAttemptCount, // regionRerouteRetryCount
                        usePreferredLocations: true);

                    return await RequestRetryUtility.ProcessRequestAsync<GoneOnlyRequestRetryPolicyContext, DocumentServiceRequest, StoreResponse>(
                        (GoneOnlyRequestRetryPolicyContext innerRetryContext) => this.InvokeAsync(
                            requestClone,
                            new TimeoutHelper(
                                innerRetryContext.RemainingTimeInMsOnClientRequest, // timeout
                                cancellationToken),
                            innerRetryContext.IsInRetry, // isInRetry
                            innerRetryContext.ForceRefresh, // forceRefresh
                            cancellationToken),
                        prepareRequest: () => {
                            requestClone.RequestContext.ClientRequestStatistics?.RecordRequest(requestClone);
                            return requestClone;
                            },
                        policy: new GoneOnlyRequestRetryPolicy<StoreResponse>(
                            retryContext.TimeoutForInBackoffRetryPolicy), // backoffTime
                        cancellationToken: cancellationToken);
                };
            }

            int retryTimeout = this.serviceConfigReader.DefaultConsistencyLevel == ConsistencyLevel.Strong ?
                ReplicatedResourceClient.StrongGoneAndRetryWithRetryTimeoutInSeconds :
                ReplicatedResourceClient.GoneAndRetryWithRetryTimeoutInSeconds;

            // Used on test hooks
            if (this.GoneAndRetryWithRetryTimeoutInSecondsOverride.HasValue)
            {
                retryTimeout = this.GoneAndRetryWithRetryTimeoutInSecondsOverride.Value;
            }

            return RequestRetryUtility.ProcessRequestAsync<GoneAndRetryRequestRetryPolicyContext, DocumentServiceRequest, StoreResponse>(
                funcDelegate,
                prepareRequest: () => {
                    request.RequestContext.ClientRequestStatistics?.RecordRequest(request);
                    return request;
                },
                policy: new GoneAndRetryWithRequestRetryPolicy<StoreResponse>(
                    waitTimeInSecondsOverride: retryTimeout,
                    minBackoffForRegionReroute: this.minBackoffForFallingBackToOtherRegions,
                    detectConnectivityIssues: this.detectClientConnectivityIssues,
                    retryWithConfiguration: this.retryWithConfiguration),
                inBackoffAlternateCallbackMethod: inBackoffFuncDelegate,
                minBackoffForInBackoffCallback: this.minBackoffForFallingBackToOtherRegions,
                cancellationToken: cancellationToken);
        }

        private Task<StoreResponse> InvokeAsync(DocumentServiceRequest request, TimeoutHelper timeout, bool isInRetry, bool forceRefresh, CancellationToken cancellationToken)
        {
            if (request.OperationType == OperationType.ExecuteJavaScript)
            {
                if (request.IsReadOnlyScript) return this.consistencyReader.ReadAsync(request, timeout, isInRetry, forceRefresh, cancellationToken);
                else return this.consistencyWriter.WriteAsync(request, timeout, forceRefresh, cancellationToken);
            }
            else if (request.OperationType.IsWriteOperation())
            {
                return this.consistencyWriter.WriteAsync(request, timeout, forceRefresh, cancellationToken);
            }
            else if (request.OperationType.IsReadOperation())
            {
                return this.consistencyReader.ReadAsync(request, timeout, isInRetry, forceRefresh, cancellationToken);
            }
#if !COSMOSCLIENT
            else if (request.OperationType == OperationType.Throttle
                || request.OperationType == OperationType.PreCreateValidation
                || request.OperationType == OperationType.OfferPreGrowValidation)
            {
                return this.HandleThrottlePreCreateOrOfferPreGrowAsync(request, forceRefresh);
            }
#endif
            else
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unexpected operation type {0}", request.OperationType));
            }
        }

        private async Task<StoreResponse> HandleThrottlePreCreateOrOfferPreGrowAsync(DocumentServiceRequest request, bool forceRefresh)
        {
            DocumentServiceRequest requestReplica = DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Database,
                request.RequestAuthorizationTokenType);

            PartitionAddressInformation addressInfo = await this.addressResolver.ResolveAsync(requestReplica, forceRefresh, CancellationToken.None);
            Uri primaryUri = addressInfo.GetPrimaryUri(requestReplica, this.protocol);

            return await this.transportClient.InvokeResourceOperationAsync(primaryUri, request);
        }

        private bool CheckWriteRetryable(DocumentServiceRequest request)
        {
            bool isRetryable = false;

            if (this.useMultipleWriteLocations)
            {
                if ((request.OperationType == OperationType.Execute && request.ResourceType == ResourceType.StoredProcedure) ||
                    (request.OperationType.IsWriteOperation() && request.ResourceType == ResourceType.Document))
                {
                    isRetryable = true;
                }
            }

            return isRetryable;
        }

        internal static bool IsGlobalStrongEnabled()
        {
            bool isGlobalStrongEnabled = true;
#if DOCDBCLIENT
#if !(NETSTANDARD15 || NETSTANDARD16)
            isGlobalStrongEnabled = ReplicatedResourceClient.enableGlobalStrong.Value;
#endif
#endif
            return isGlobalStrongEnabled;
        }

        internal static bool IsReadingFromMaster(ResourceType resourceType, OperationType operationType)
        {
            if (resourceType == ResourceType.Offer ||
                resourceType == ResourceType.Database ||
                resourceType == ResourceType.User ||
                resourceType == ResourceType.ClientEncryptionKey ||
                resourceType == ResourceType.UserDefinedType ||
                resourceType == ResourceType.Permission ||
                resourceType == ResourceType.DatabaseAccount ||
                resourceType == ResourceType.Snapshot ||
                resourceType == ResourceType.RoleAssignment ||
                resourceType == ResourceType.RoleDefinition ||
#if !COSMOSCLIENT
                resourceType == ResourceType.Topology ||
                (resourceType == ResourceType.PartitionKeyRange && operationType != OperationType.GetSplitPoint
                    && operationType != OperationType.GetSplitPoints && operationType != OperationType.AbortSplit) ||
#else
                resourceType == ResourceType.PartitionKeyRange ||
#endif
                (resourceType == ResourceType.Collection && (operationType == OperationType.ReadFeed || operationType == OperationType.Query || operationType == OperationType.SqlQuery)))
            {
                return true;
            }

            return false;
        }

        internal static bool IsSessionTokenRequired(
            ResourceType resourceType,
            OperationType operationType)
        {
            // Stored procedures CRUD operations are done on master. Stored procedures execute are not a master operation.
            return !ReplicatedResourceClient.IsMasterResource(resourceType) &&
                   !ReplicatedResourceClient.IsStoredProcedureCrudOperation(resourceType, operationType) &&
                   operationType != OperationType.QueryPlan;
        }

        internal static bool IsStoredProcedureCrudOperation(
            ResourceType resourceType,
            OperationType operationType)
        {
            return resourceType == ResourceType.StoredProcedure &&
                   operationType != Documents.OperationType.ExecuteJavaScript;
        }

        internal static bool IsMasterResource(ResourceType resourceType)
        {
            if (resourceType == ResourceType.Offer ||
                resourceType == ResourceType.Database ||
                resourceType == ResourceType.User ||
                resourceType == ResourceType.ClientEncryptionKey ||
                resourceType == ResourceType.UserDefinedType ||
                resourceType == ResourceType.Permission ||
#if !COSMOSCLIENT
                resourceType == ResourceType.Topology ||
#endif
                resourceType == ResourceType.DatabaseAccount ||
                resourceType == ResourceType.PartitionKeyRange ||
                resourceType == ResourceType.Collection ||
                resourceType == ResourceType.Snapshot ||
                resourceType == ResourceType.RoleAssignment ||
                resourceType == ResourceType.RoleDefinition ||
                resourceType == ResourceType.Trigger ||
                resourceType == ResourceType.UserDefinedFunction)
            {
                return true;
            }

            return false;
        }
    }
}