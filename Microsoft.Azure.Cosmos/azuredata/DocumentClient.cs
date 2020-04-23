//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using ContainerProperties = global::Azure.Cosmos.CosmosContainerProperties;

    /// <summary>
    /// Provides a client-side logical representation for the Azure Cosmos DB service.
    /// This client is used to configure and execute requests against the service.
    /// </summary>
    /// <threadSafety>
    /// This type is thread safe.
    /// </threadSafety>
    /// <remarks>
    /// The service client that encapsulates the endpoint and credentials and connection policy used to access the Azure Cosmos DB service.
    /// It is recommended to cache and reuse this instance within your application rather than creating a new instance for every operation.
    ///
    /// <para>
    /// When your app uses DocumentClient, you should call its IDisposable.Dispose implementation when you are finished using it.
    /// Depending on your programming technique, you can do this in one of two ways:
    /// </para>
    ///
    /// <para>
    /// 1. By using a language construct such as the using statement in C#.
    /// The using statement is actually a syntactic convenience.
    /// At compile time, the language compiler implements the intermediate language (IL) for a try/catch block.
    /// <code language="c#">
    /// <![CDATA[
    /// using (IDocumentClient client = new DocumentClient(new Uri("endpoint"), "authKey"))
    /// {
    ///     ...
    /// }
    /// ]]>
    /// </code>
    /// </para>
    ///
    /// <para>
    /// 2. By wrapping the call to the IDisposable.Dispose implementation in a try/catch block.
    /// The following example replaces the using block in the previous example with a try/catch/finally block.
    /// <code language="c#">
    /// <![CDATA[
    /// IDocumentClient client = new DocumentClient(new Uri("endpoint"), "authKey"))
    /// try{
    ///     ...
    /// }
    /// finally{
    ///     if (client != null) client.Dispose();
    /// }
    /// ]]>
    /// </code>
    /// </para>
    ///
    /// </remarks>
    internal partial class DocumentClient : IDisposable, IAuthorizationTokenProvider, IDocumentClient, IDocumentClientInternal
    {
        private const string AllowOverrideStrongerConsistency = "AllowOverrideStrongerConsistency";
        private const string MaxConcurrentConnectionOpenConfig = "MaxConcurrentConnectionOpenRequests";
        private const string IdleConnectionTimeoutInSecondsConfig = "IdleConnectionTimeoutInSecondsConfig";
        private const string OpenConnectionTimeoutInSecondsConfig = "OpenConnectionTimeoutInSecondsConfig";
        private const string TransportTimerPoolGranularityInSecondsConfig = "TransportTimerPoolGranularityInSecondsConfig";
        private const string EnableTcpChannelConfig = "CosmosDbEnableTcpChannel";
        private const string MaxRequestsPerChannelConfig = "CosmosDbMaxRequestsPerTcpChannel";
        private const string TcpPartitionCount = "CosmosDbTcpPartitionCount";
        private const string MaxChannelsPerHostConfig = "CosmosDbMaxTcpChannelsPerHost";
        private const string RntbdReceiveHangDetectionTimeConfig = "CosmosDbTcpReceiveHangDetectionTimeSeconds";
        private const string RntbdSendHangDetectionTimeConfig = "CosmosDbTcpSendHangDetectionTimeSeconds";
        private const string EnableCpuMonitorConfig = "CosmosDbEnableCpuMonitor";

        ////The MAC signature found in the HTTP request is not the same as the computed signature.Server used following string to sign
        ////The input authorization token can't serve the request. Please check that the expected payload is built as per the protocol, and check the key being used. Server used the following payload to sign
        private const string MacSignatureString = "to sign";

        private const int MaxConcurrentConnectionOpenRequestsPerProcessor = 25;
        private const int DefaultMaxRequestsPerRntbdChannel = 30;
        private const int DefaultRntbdPartitionCount = 1;
        private const int DefaultMaxRntbdChannelsPerHost = ushort.MaxValue;
        private const int DefaultRntbdReceiveHangDetectionTimeSeconds = 65;
        private const int DefaultRntbdSendHangDetectionTimeSeconds = 10;
        private const bool DefaultEnableCpuMonitor = true;

        private readonly IDictionary<string, List<PartitionKeyAndResourceTokenPair>> resourceTokens;
        private ConnectionPolicy connectionPolicy;
        private RetryPolicy retryPolicy;
        private bool allowOverrideStrongerConsistency = false;
        private int maxConcurrentConnectionOpenRequests = Environment.ProcessorCount * MaxConcurrentConnectionOpenRequestsPerProcessor;
        private int openConnectionTimeoutInSeconds = 5;
        private int idleConnectionTimeoutInSeconds = -1;
        private int timerPoolGranularityInSeconds = 1;
        private bool enableRntbdChannel = true;
        private int maxRequestsPerRntbdChannel = DefaultMaxRequestsPerRntbdChannel;
        private int rntbdPartitionCount = DefaultRntbdPartitionCount;
        private int maxRntbdChannels = DefaultMaxRntbdChannelsPerHost;
        private int rntbdReceiveHangDetectionTimeSeconds = DefaultRntbdReceiveHangDetectionTimeSeconds;
        private int rntbdSendHangDetectionTimeSeconds = DefaultRntbdSendHangDetectionTimeSeconds;
        private bool enableCpuMonitor = DefaultEnableCpuMonitor;

        //Auth
        private IComputeHash authKeyHashFunction;

        //Consistency
        private Documents.ConsistencyLevel? desiredConsistencyLevel;

        private CosmosAccountServiceConfiguration accountServiceConfiguration;

        private ClientCollectionCache collectionCache;

        private PartitionKeyRangeCache partitionKeyRangeCache;

        internal HttpMessageHandler httpMessageHandler;

        //Private state.
        private bool isSuccessfullyInitialized;
        private bool isDisposed;
        private object initializationSyncLock;  // guards initializeTask

        // creator of TransportClient is responsible for disposing it.
        private IStoreClientFactory storeClientFactory;
        private HttpClient mediaClient;

        // Flag that indicates whether store client factory must be disposed whenever client is disposed.
        // Setting this flag to false will result in store client factory not being disposed when client is disposed.
        // This flag is used to allow shared store client factory survive disposition of a document client while other clients continue using it.
        private bool isStoreClientFactoryCreatedInternally;

        //Based on connectivity mode we will either have ServerStoreModel / GatewayStoreModel here.
        private IStoreModel storeModel;
        //We will always have GatewayStoreModel for certain Master operations(viz: Collection CRUD)
        private IStoreModel gatewayStoreModel;

        //Id counter.
        private static int idCounter;
        //Trace Id.
        private int traceId;

        //SessionContainer.
        internal ISessionContainer sessionContainer;

        private readonly bool hasAuthKeyResourceToken;
        private readonly string authKeyResourceToken = string.Empty;

        private DocumentClientEventSource eventSource;
        private GlobalEndpointManager globalEndpointManager;
        private bool useMultipleWriteLocations;

        internal Task initializeTask;

        private event EventHandler<SendingRequestEventArgs> sendingRequest;
        private event EventHandler<ReceivedResponseEventArgs> receivedResponse;
        private Func<TransportClient, TransportClient> transportClientHandlerFactory;

        //Callback for on execution of scalar LINQ queries event.
        //This callback is meant for tests only.
        private Action<IQueryable> onExecuteScalarQueryCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified Azure Cosmos DB service endpoint, key, and connection policy for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="serviceEndpoint">
        /// The service endpoint to use to create the client.
        /// </param>
        /// <param name="authKey">
        /// The list of Permission objects to use to create the client.
        /// </param>
        /// <param name="connectionPolicy">
        /// (Optional) The connection policy for the client. If none is passed, the default is used <see cref="ConnectionPolicy"/>
        /// </param>
        /// <param name="desiredConsistencyLevel">
        /// (Optional) This can be used to weaken the database account consistency level for read operations.
        /// If this is not set the database account consistency level will be used for all requests.
        /// </param>
        /// <remarks>
        /// The service endpoint and the authorization key can be obtained from the Azure Management Portal.
        /// The authKey used here is encrypted for privacy when being used, and deleted from computer memory when no longer needed
        /// <para>
        /// Using Direct connectivity, wherever possible, is recommended
        /// </para>
        /// </remarks>
        /// <seealso cref="Uri"/>
        /// <seealso cref="SecureString"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        public DocumentClient(Uri serviceEndpoint,
                              SecureString authKey,
                              ConnectionPolicy connectionPolicy = null,
                              Documents.ConsistencyLevel? desiredConsistencyLevel = null)
        {
            if (authKey == null)
            {
                throw new ArgumentNullException("authKey");
            }

            if (authKey != null)
            {
                this.authKeyHashFunction = new SecureStringHMACSHA256Helper(authKey);
            }

            this.Initialize(serviceEndpoint, connectionPolicy, desiredConsistencyLevel);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified service endpoint, an authorization key (or resource token) and a connection policy
        /// for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="serviceEndpoint">The service endpoint to use to create the client.</param>
        /// <param name="authKeyOrResourceToken">The authorization key or resource token to use to create the client.</param>
        /// <param name="connectionPolicy">(Optional) The connection policy for the client.</param>
        /// <param name="desiredConsistencyLevel">(Optional) The default consistency policy for client operations.</param>
        /// <remarks>
        /// The service endpoint can be obtained from the Azure Management Portal.
        /// If you are connecting using one of the Master Keys, these can be obtained along with the endpoint from the Azure Management Portal
        /// If however you are connecting as a specific Azure Cosmos DB User, the value passed to <paramref name="authKeyOrResourceToken"/> is the ResourceToken obtained from the permission feed for the user.
        /// <para>
        /// Using Direct connectivity, wherever possible, is recommended.
        /// </para>
        /// </remarks>
        /// <seealso cref="Uri"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        public DocumentClient(Uri serviceEndpoint,
                              string authKeyOrResourceToken,
                              ConnectionPolicy connectionPolicy = null,
                              Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint, authKeyOrResourceToken, sendingRequestEventArgs: null, connectionPolicy: connectionPolicy, desiredConsistencyLevel: desiredConsistencyLevel)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified service endpoint, an authorization key (or resource token) and a connection policy
        /// for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="serviceEndpoint">The service endpoint to use to create the client.</param>
        /// <param name="authKeyOrResourceToken">The authorization key or resource token to use to create the client.</param>
        /// <param name="handler">The HTTP handler stack to use for sending requests (e.g., HttpClientHandler).</param>
        /// <param name="connectionPolicy">(Optional) The connection policy for the client.</param>
        /// <param name="desiredConsistencyLevel">(Optional) The default consistency policy for client operations.</param>
        /// <remarks>
        /// The service endpoint can be obtained from the Azure Management Portal.
        /// If you are connecting using one of the Master Keys, these can be obtained along with the endpoint from the Azure Management Portal
        /// If however you are connecting as a specific Azure Cosmos DB User, the value passed to <paramref name="authKeyOrResourceToken"/> is the ResourceToken obtained from the permission feed for the user.
        /// <para>
        /// Using Direct connectivity, wherever possible, is recommended.
        /// </para>
        /// </remarks>
        /// <seealso cref="Uri"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        public DocumentClient(Uri serviceEndpoint,
                              string authKeyOrResourceToken,
                              HttpMessageHandler handler,
                              ConnectionPolicy connectionPolicy = null,
                              Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint, authKeyOrResourceToken, sendingRequestEventArgs: null, connectionPolicy: connectionPolicy, desiredConsistencyLevel: desiredConsistencyLevel, handler: handler)
        {
        }

        public DocumentClient(Uri serviceEndpoint,
                              string authKeyOrResourceToken,
                              JsonSerializerSettings serializerSettings,
                              ConnectionPolicy connectionPolicy = null,
                              Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint, authKeyOrResourceToken, (HttpMessageHandler)null, connectionPolicy, desiredConsistencyLevel)
        {
        }

        public DocumentClient(
            Uri serviceEndpoint,
            IList<Documents.Permission> permissionFeed,
            ConnectionPolicy connectionPolicy = null,
            Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint,
                    GetResourceTokens(permissionFeed),
                    connectionPolicy,
                    desiredConsistencyLevel)
        {
        }

        public DocumentClient(Uri serviceEndpoint,
                              SecureString authKey,
                              JsonSerializerSettings serializerSettings,
                              ConnectionPolicy connectionPolicy = null,
                              Documents.ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint, authKey, connectionPolicy, desiredConsistencyLevel)
        {            
        }

        internal DocumentClient(Uri serviceEndpoint,
                              string authKeyOrResourceToken,
                              EventHandler<SendingRequestEventArgs> sendingRequestEventArgs,
                              ConnectionPolicy connectionPolicy = null,
                              Documents.ConsistencyLevel? desiredConsistencyLevel = null,
                              JsonSerializerSettings serializerSettings = null,
                              ApiType apitype = ApiType.None,
                              EventHandler<ReceivedResponseEventArgs> receivedResponseEventArgs = null,
                              HttpMessageHandler handler = null,
                              ISessionContainer sessionContainer = null,
                              bool? enableCpuMonitor = null,
                              Func<TransportClient, TransportClient> transportClientHandlerFactory = null,
                              IStoreClientFactory storeClientFactory = null)
        {
            if (authKeyOrResourceToken == null)
            {
                throw new ArgumentNullException("authKeyOrResourceToken");
            }

            if (sendingRequestEventArgs != null)
            {
                this.sendingRequest += sendingRequestEventArgs;
            }

            this.ApiType = apitype;

            if (receivedResponseEventArgs != null)
            {
                this.receivedResponse += receivedResponseEventArgs;
            }

            if (AuthorizationHelper.IsResourceToken(authKeyOrResourceToken))
            {
                this.hasAuthKeyResourceToken = true;
                this.authKeyResourceToken = authKeyOrResourceToken;
            }
            else
            {
                this.authKeyHashFunction = new StringHMACSHA256Hash(authKeyOrResourceToken);
            }

            this.transportClientHandlerFactory = transportClientHandlerFactory;

            this.Initialize(
                serviceEndpoint: serviceEndpoint,
                connectionPolicy: connectionPolicy,
                desiredConsistencyLevel: desiredConsistencyLevel,
                handler: handler,
                sessionContainer: sessionContainer,
                enableCpuMonitor: enableCpuMonitor,
                storeClientFactory: storeClientFactory);
        }

        private static List<ResourceToken> GetResourceTokens(IList<Documents.Permission> permissionFeed)
        {
            if (permissionFeed == null)
            {
                throw new ArgumentNullException("permissionFeed");
            }

            return permissionFeed.Select(
                permission => new ResourceToken
                {
                    ResourceLink = permission.ResourceLink,
                    ResourcePartitionKey = permission.ResourcePartitionKey != null ? permission.ResourcePartitionKey.InternalKey.ToObjectArray() : null,
                    Token = permission.Token
                }).ToList();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified Azure Cosmos DB service endpoint, a list of <see cref="ResourceToken"/> objects and a connection policy.
        /// </summary>
        /// <param name="serviceEndpoint">The service endpoint to use to create the client.</param>
        /// <param name="resourceTokens">A list of <see cref="ResourceToken"/> objects to use to create the client.</param>
        /// <param name="connectionPolicy">(Optional) The <see cref="Microsoft.Azure.Cosmos.ConnectionPolicy"/> to use for this connection.</param>
        /// <param name="desiredConsistencyLevel">(Optional) The default consistency policy for client operations.</param>
        /// <exception cref="System.ArgumentNullException">If <paramref name="resourceTokens"/> is not supplied.</exception>
        /// <exception cref="System.ArgumentException">If <paramref name="resourceTokens"/> is not a valid permission link.</exception>
        /// <remarks>
        /// If no <paramref name="connectionPolicy"/> is provided, then the default <see cref="Microsoft.Azure.Cosmos.ConnectionPolicy"/> will be used.
        /// Using Direct connectivity, wherever possible, is recommended.
        /// </remarks>
        /// <seealso cref="Uri"/>
        /// <seealso cref="CosmosPermission"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        internal DocumentClient(Uri serviceEndpoint,
                              IList<ResourceToken> resourceTokens,
                              ConnectionPolicy connectionPolicy = null,
                              Documents.ConsistencyLevel? desiredConsistencyLevel = null)
        {
            if (resourceTokens == null)
            {
                throw new ArgumentNullException("resourceTokens");
            }

            this.resourceTokens = new Dictionary<string, List<PartitionKeyAndResourceTokenPair>>();

            foreach (ResourceToken resourceToken in resourceTokens)
            {
                bool isNameBasedRequest = false;
                bool isFeedRequest = false;
                string resourceTypeString;
                string resourceIdOrFullName;
                if (!PathsHelper.TryParsePathSegments(resourceToken.ResourceLink, out isFeedRequest, out resourceTypeString, out resourceIdOrFullName, out isNameBasedRequest))
                {
                    throw new ArgumentException(RMResources.BadUrl, "resourceToken.ResourceLink");
                }

                List<PartitionKeyAndResourceTokenPair> tokenList;
                if (!this.resourceTokens.TryGetValue(resourceIdOrFullName, out tokenList))
                {
                    tokenList = new List<PartitionKeyAndResourceTokenPair>();
                    this.resourceTokens.Add(resourceIdOrFullName, tokenList);
                }

                tokenList.Add(new PartitionKeyAndResourceTokenPair(
                    resourceToken.ResourcePartitionKey != null ? PartitionKeyInternal.FromObjectArray(resourceToken.ResourcePartitionKey, true) : PartitionKeyInternal.Empty,
                    resourceToken.Token));
            }

            if (!this.resourceTokens.Any())
            {
                throw new ArgumentException("permissionFeed");
            }

            string firstToken = resourceTokens.First().Token;

            if (AuthorizationHelper.IsResourceToken(firstToken))
            {
                this.hasAuthKeyResourceToken = true;
                this.authKeyResourceToken = firstToken;
                this.Initialize(serviceEndpoint, connectionPolicy, desiredConsistencyLevel);
            }
            else
            {
                this.authKeyHashFunction = new StringHMACSHA256Hash(firstToken);
                this.Initialize(serviceEndpoint, connectionPolicy, desiredConsistencyLevel);
            }
        }

        /// <summary>
        /// Initializes a new instance of the Microsoft.Azure.Cosmos.DocumentClient class using the
        /// specified Azure Cosmos DB service endpoint, a dictionary of resource tokens and a connection policy.
        /// </summary>
        /// <param name="serviceEndpoint">The service endpoint to use to create the client.</param>
        /// <param name="resourceTokens">A dictionary of resource ids and resource tokens.</param>
        /// <param name="connectionPolicy">(Optional) The connection policy for the client.</param>
        /// <param name="desiredConsistencyLevel">(Optional) The default consistency policy for client operations.</param>
        /// <remarks>Using Direct connectivity, wherever possible, is recommended</remarks>
        /// <seealso cref="Uri"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        [Obsolete("Please use the constructor that takes a permission list or a resource token list.")]
        public DocumentClient(Uri serviceEndpoint,
            IDictionary<string, string> resourceTokens,
            ConnectionPolicy connectionPolicy = null,
            Documents.ConsistencyLevel? desiredConsistencyLevel = null)
        {
            if (resourceTokens == null)
            {
                throw new ArgumentNullException("resourceTokens");
            }

            if (resourceTokens.Count() == 0)
            {
                throw new DocumentClientException(RMResources.InsufficientResourceTokens, null, null);
            }

            this.resourceTokens = resourceTokens.ToDictionary(
                pair => pair.Key,
                pair => new List<PartitionKeyAndResourceTokenPair> { new PartitionKeyAndResourceTokenPair(PartitionKeyInternal.Empty, pair.Value) });

            string firstToken = resourceTokens.ElementAt(0).Value;
            if (string.IsNullOrEmpty(firstToken))
            {
                throw new DocumentClientException(RMResources.InsufficientResourceTokens, null, null);
            }

            if (AuthorizationHelper.IsResourceToken(firstToken))
            {
                this.hasAuthKeyResourceToken = true;
                this.authKeyResourceToken = firstToken;
                this.Initialize(serviceEndpoint, connectionPolicy, desiredConsistencyLevel);
            }
            else
            {
                this.authKeyHashFunction = new StringHMACSHA256Hash(firstToken);
                this.Initialize(serviceEndpoint, connectionPolicy, desiredConsistencyLevel);
            }
        }

        /// <summary>
        /// Internal constructor purely for unit-testing
        /// </summary>
        internal DocumentClient(Uri serviceEndpoint,
                      string authKey)
        {
            // do nothing 
            this.ServiceEndpoint = serviceEndpoint;
        }

        internal virtual async Task<ClientCollectionCache> GetCollectionCacheAsync()
        {
            await this.EnsureValidClientAsync();
            return this.collectionCache;
        }

        internal virtual async Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync()
        {
            await this.EnsureValidClientAsync();
            return this.partitionKeyRangeCache;
        }

        internal GlobalAddressResolver AddressResolver { get; private set; }

        internal event EventHandler<SendingRequestEventArgs> SendingRequest
        {
            add
            {
                this.sendingRequest += value;
            }
            remove
            {
                this.sendingRequest -= value;
            }
        }

        internal GlobalEndpointManager GlobalEndpointManager
        {
            get { return this.globalEndpointManager; }
        }

        internal virtual void Initialize(Uri serviceEndpoint,
            ConnectionPolicy connectionPolicy = null,
            Documents.ConsistencyLevel? desiredConsistencyLevel = null,
            HttpMessageHandler handler = null,
            ISessionContainer sessionContainer = null,
            bool? enableCpuMonitor = null,
            IStoreClientFactory storeClientFactory = null)
        {
            if (serviceEndpoint == null)
            {
                throw new ArgumentNullException("serviceEndpoint");
            }

            DefaultTrace.InitEventListener();

#if !(NETSTANDARD15 || NETSTANDARD16) 
#if NETSTANDARD20
            // GetEntryAssembly returns null when loaded from native netstandard2.0
            if (System.Reflection.Assembly.GetEntryAssembly() != null)
            {
#endif
                // For tests we want to allow stronger consistency during construction or per call
                string allowOverrideStrongerConsistencyConfig = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.AllowOverrideStrongerConsistency];
                if (!string.IsNullOrEmpty(allowOverrideStrongerConsistencyConfig))
                {
                    if (!bool.TryParse(allowOverrideStrongerConsistencyConfig, out this.allowOverrideStrongerConsistency))
                    {
                        this.allowOverrideStrongerConsistency = false;
                    }
                }

                // We might want to override the defaults sometime
                string maxConcurrentConnectionOpenRequestsOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.MaxConcurrentConnectionOpenConfig];
                if (!string.IsNullOrEmpty(maxConcurrentConnectionOpenRequestsOverrideString))
                {
                    int maxConcurrentConnectionOpenRequestOverrideInt = 0;
                    if (Int32.TryParse(maxConcurrentConnectionOpenRequestsOverrideString, out maxConcurrentConnectionOpenRequestOverrideInt))
                    {
                        this.maxConcurrentConnectionOpenRequests = maxConcurrentConnectionOpenRequestOverrideInt;
                    }
                }

                string openConnectionTimeoutInSecondsOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.OpenConnectionTimeoutInSecondsConfig];
                if (!string.IsNullOrEmpty(openConnectionTimeoutInSecondsOverrideString))
                {
                    int openConnectionTimeoutInSecondsOverrideInt = 0;
                    if (Int32.TryParse(openConnectionTimeoutInSecondsOverrideString, out openConnectionTimeoutInSecondsOverrideInt))
                    {
                        this.openConnectionTimeoutInSeconds = openConnectionTimeoutInSecondsOverrideInt;
                    }
                }

                string idleConnectionTimeoutInSecondsOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.IdleConnectionTimeoutInSecondsConfig];
                if (!string.IsNullOrEmpty(idleConnectionTimeoutInSecondsOverrideString))
                {
                    int idleConnectionTimeoutInSecondsOverrideInt = 0;
                    if (Int32.TryParse(idleConnectionTimeoutInSecondsOverrideString, out idleConnectionTimeoutInSecondsOverrideInt))
                    {
                        this.idleConnectionTimeoutInSeconds = idleConnectionTimeoutInSecondsOverrideInt;
                    }
                }

                string transportTimerPoolGranularityInSecondsOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.TransportTimerPoolGranularityInSecondsConfig];
                if (!string.IsNullOrEmpty(transportTimerPoolGranularityInSecondsOverrideString))
                {
                    int timerPoolGranularityInSecondsOverrideInt = 0;
                    if (Int32.TryParse(transportTimerPoolGranularityInSecondsOverrideString, out timerPoolGranularityInSecondsOverrideInt))
                    {
                        // timeoutgranularity specified should be greater than min(5 seconds)
                        if (timerPoolGranularityInSecondsOverrideInt > this.timerPoolGranularityInSeconds)
                        {
                            this.timerPoolGranularityInSeconds = timerPoolGranularityInSecondsOverrideInt;
                        }
                    }
                }

                string enableRntbdChannelOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.EnableTcpChannelConfig];
                if (!string.IsNullOrEmpty(enableRntbdChannelOverrideString))
                {
                    bool enableRntbdChannel = false;
                    if (bool.TryParse(enableRntbdChannelOverrideString, out enableRntbdChannel))
                    {
                        this.enableRntbdChannel = enableRntbdChannel;
                    }
                }

                string maxRequestsPerRntbdChannelOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.MaxRequestsPerChannelConfig];
                if (!string.IsNullOrEmpty(maxRequestsPerRntbdChannelOverrideString))
                {
                    int maxRequestsPerChannel = DocumentClient.DefaultMaxRequestsPerRntbdChannel;
                    if (int.TryParse(maxRequestsPerRntbdChannelOverrideString, out maxRequestsPerChannel))
                    {
                        this.maxRequestsPerRntbdChannel = maxRequestsPerChannel;
                    }
                }

                string rntbdPartitionCountOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.TcpPartitionCount];
                if (!string.IsNullOrEmpty(rntbdPartitionCountOverrideString))
                {
                    int rntbdPartitionCount = DocumentClient.DefaultRntbdPartitionCount;
                    if (int.TryParse(rntbdPartitionCountOverrideString, out rntbdPartitionCount))
                    {
                        this.rntbdPartitionCount = rntbdPartitionCount;
                    }
                }

                string maxRntbdChannelsOverrideString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.MaxChannelsPerHostConfig];
                if (!string.IsNullOrEmpty(maxRntbdChannelsOverrideString))
                {
                    int maxRntbdChannels = DefaultMaxRntbdChannelsPerHost;
                    if (int.TryParse(maxRntbdChannelsOverrideString, out maxRntbdChannels))
                    {
                        this.maxRntbdChannels = maxRntbdChannels;
                    }
                }

                string rntbdReceiveHangDetectionTimeSecondsString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.RntbdReceiveHangDetectionTimeConfig];
                if (!string.IsNullOrEmpty(rntbdReceiveHangDetectionTimeSecondsString))
                {
                    int rntbdReceiveHangDetectionTimeSeconds = DefaultRntbdReceiveHangDetectionTimeSeconds;
                    if (int.TryParse(rntbdReceiveHangDetectionTimeSecondsString, out rntbdReceiveHangDetectionTimeSeconds))
                    {
                        this.rntbdReceiveHangDetectionTimeSeconds = rntbdReceiveHangDetectionTimeSeconds;
                    }
                }

                string rntbdSendHangDetectionTimeSecondsString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.RntbdSendHangDetectionTimeConfig];
                if (!string.IsNullOrEmpty(rntbdSendHangDetectionTimeSecondsString))
                {
                    int rntbdSendHangDetectionTimeSeconds = DefaultRntbdSendHangDetectionTimeSeconds;
                    if (int.TryParse(rntbdSendHangDetectionTimeSecondsString, out rntbdSendHangDetectionTimeSeconds))
                    {
                        this.rntbdSendHangDetectionTimeSeconds = rntbdSendHangDetectionTimeSeconds;
                    }
                }

                if (enableCpuMonitor.HasValue)
                {
                    this.enableCpuMonitor = enableCpuMonitor.Value;
                }
                else
                {
                    string enableCpuMonitorString = System.Configuration.ConfigurationManager.AppSettings[DocumentClient.EnableCpuMonitorConfig];
                    if (!string.IsNullOrEmpty(enableCpuMonitorString))
                    {
                        bool enableCpuMonitorFlag = DefaultEnableCpuMonitor;
                        if (bool.TryParse(enableCpuMonitorString, out enableCpuMonitorFlag))
                        {
                            this.enableCpuMonitor = enableCpuMonitorFlag;
                        }
                    }
                }
#if NETSTANDARD20
            }
#endif            
#endif

            // ConnectionPolicy always overrides appconfig
            if (this.ConnectionPolicy != null)
            {
                if (this.ConnectionPolicy.IdleTcpConnectionTimeout.HasValue)
                {
                    this.idleConnectionTimeoutInSeconds = (int)this.ConnectionPolicy.IdleTcpConnectionTimeout.Value.TotalSeconds;
                }

                if (this.ConnectionPolicy.OpenTcpConnectionTimeout.HasValue)
                {
                    this.openConnectionTimeoutInSeconds = (int)this.ConnectionPolicy.OpenTcpConnectionTimeout.Value.TotalSeconds;
                }

                if (this.ConnectionPolicy.MaxRequestsPerTcpConnection.HasValue)
                {
                    this.maxRequestsPerRntbdChannel = this.ConnectionPolicy.MaxRequestsPerTcpConnection.Value;
                }

                if (this.ConnectionPolicy.MaxTcpPartitionCount.HasValue)
                {
                    this.rntbdPartitionCount = this.ConnectionPolicy.MaxTcpPartitionCount.Value;
                }

                if (this.ConnectionPolicy.MaxTcpConnectionsPerEndpoint.HasValue)
                {
                    this.maxRntbdChannels = this.ConnectionPolicy.MaxTcpConnectionsPerEndpoint.Value;
                }
            }

            this.ServiceEndpoint = serviceEndpoint.OriginalString.EndsWith("/", StringComparison.Ordinal) ? serviceEndpoint : new Uri(serviceEndpoint.OriginalString + "/");

            this.connectionPolicy = connectionPolicy ?? ConnectionPolicy.Default;

#if !NETSTANDARD16
            ServicePoint servicePoint = ServicePointManager.FindServicePoint(this.ServiceEndpoint);
            servicePoint.ConnectionLimit = this.connectionPolicy.MaxConnectionLimit;
#endif

            this.globalEndpointManager = new GlobalEndpointManager(this, this.connectionPolicy);

            this.httpMessageHandler = new HttpRequestMessageHandler(this.sendingRequest, this.receivedResponse, handler);

            this.mediaClient = new HttpClient(this.httpMessageHandler);

            this.mediaClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            this.mediaClient.AddUserAgentHeader(this.connectionPolicy.UserAgentContainer);

            this.mediaClient.AddApiTypeHeader(this.ApiType);

            // Set requested API version header that can be used for
            // version enforcement.
            this.mediaClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            this.mediaClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept,
                RuntimeConstants.MediaTypes.Any);

            if (sessionContainer != null)
            {
                this.sessionContainer = sessionContainer;
            }
            else
            {
                this.sessionContainer = new SessionContainer(this.ServiceEndpoint.Host);
            }

            this.retryPolicy = new RetryPolicy(this.globalEndpointManager, this.connectionPolicy);
            this.ResetSessionTokenRetryPolicy = this.retryPolicy;

            this.mediaClient.Timeout = this.connectionPolicy.MediaRequestTimeout;

            this.desiredConsistencyLevel = desiredConsistencyLevel;
            // Setup the proxy to be  used based on connection mode.
            // For gateway: GatewayProxy.
            // For direct: WFStoreProxy [set in OpenAsync()].
            this.initializationSyncLock = new object();

            this.eventSource = DocumentClientEventSource.Instance;

            this.initializeTask = TaskHelper.InlineIfPossibleAsync(
                () => this.GetInitializationTaskAsync(storeClientFactory: storeClientFactory),
                new ResourceThrottleRetryPolicy(
                    this.connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests,
                    this.connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds));

            // ContinueWith on the initialization task is needed for handling the UnobservedTaskException
            // if this task throws for some reason. Awaiting inside a constructor is not supported and
            // even if we had to await inside GetInitializationTask to catch the exception, that will
            // be a blocking call. In such cases, the recommended approach is to "handle" the
            // UnobservedTaskException by using ContinueWith method w/ TaskContinuationOptions.OnlyOnFaulted
            // and accessing the Exception property on the target task.
#pragma warning disable VSTHRD110 // Observe result of async calls
            this.initializeTask.ContinueWith(t =>
#pragma warning restore VSTHRD110 // Observe result of async calls
            {
                DefaultTrace.TraceWarning("initializeTask failed {0}", t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);

            this.traceId = Interlocked.Increment(ref DocumentClient.idCounter);
            DefaultTrace.TraceInformation(string.Format(
                CultureInfo.InvariantCulture,
                "DocumentClient with id {0} initialized at endpoint: {1} with ConnectionMode: {2}, connection Protocol: {3}, and consistency level: {4}",
                this.traceId,
                serviceEndpoint.ToString(),
                this.connectionPolicy.ConnectionMode.ToString(),
                this.connectionPolicy.ConnectionProtocol.ToString(),
                desiredConsistencyLevel != null ? desiredConsistencyLevel.ToString() : "null"));
        }

        // Always called from under the lock except when called from Intilialize method during construction.
        private async Task GetInitializationTaskAsync(IStoreClientFactory storeClientFactory)
        {
            await this.InitializeGatewayConfigurationReaderAsync();

            if (this.desiredConsistencyLevel.HasValue)
            {
                this.EnsureValidOverwrite(this.desiredConsistencyLevel.Value);
            }

            GatewayStoreModel gatewayStoreModel = new GatewayStoreModel(
                    this.globalEndpointManager,
                    this.sessionContainer,
                    this.connectionPolicy.RequestTimeout,
                    (global::Azure.Cosmos.ConsistencyLevel)this.accountServiceConfiguration.DefaultConsistencyLevel,
                    this.eventSource,
                    null,
                    this.connectionPolicy.UserAgentContainer,
                    this.ApiType,
                    this.httpMessageHandler);

            this.gatewayStoreModel = gatewayStoreModel;

            this.collectionCache = new ClientCollectionCache(this.sessionContainer, this.gatewayStoreModel, this, this.retryPolicy);
            this.partitionKeyRangeCache = new PartitionKeyRangeCache(this, this.gatewayStoreModel, this.collectionCache);
            this.ResetSessionTokenRetryPolicy = new ResetSessionTokenRetryPolicyFactory(this.sessionContainer, this.collectionCache, this.retryPolicy);

            if (this.connectionPolicy.ConnectionMode == ConnectionMode.Gateway)
            {
                this.storeModel = this.gatewayStoreModel;
            }
            else
            {
                this.InitializeDirectConnectivity(storeClientFactory);
            }
        }

        private async Task InitializeCachesAsync(string databaseName, DocumentCollection collection, CancellationToken cancellationToken)
        {
            if (databaseName == null)
            {
                throw new ArgumentNullException(nameof(databaseName));
            }

            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            CollectionCache collectionCache = await this.GetCollectionCacheAsync();
            using (
                DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.Query,
                    ResourceType.Document,
                    collection.SelfLink,
                    AuthorizationTokenType.PrimaryMasterKey))
            {
                ContainerProperties resolvedCollection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);
                IReadOnlyList<PartitionKeyRange> ranges = await this.partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                resolvedCollection.ResourceId,
                new Range<string>(
                    PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));

                // In Gateway mode, AddressCache is null
                if (this.AddressResolver != null)
                {
                    await this.AddressResolver.OpenAsync(databaseName, resolvedCollection, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Gets or sets the session object used for session consistency version tracking in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <value>
        /// The session object used for version tracking when the consistency level is set to Session.
        /// </value>
        /// The session object can be saved and shared between two DocumentClient instances within the same AppDomain.
        /// </remarks>
        public object Session
        {
            get
            {
                return this.sessionContainer;
            }

            set
            {
                SessionContainer container = value as SessionContainer;
                if (container == null)
                {
                    throw new ArgumentNullException("value");
                }

                if (!string.Equals(this.ServiceEndpoint.Host, container.HostName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(string.Format(
                        CultureInfo.CurrentUICulture,
                        ClientResources.BadSession,
                        container.HostName,
                        this.ServiceEndpoint.Host));
                }

                SessionContainer currentSessionContainer = this.sessionContainer as SessionContainer;
                if (currentSessionContainer == null)
                {
                    throw new ArgumentNullException(nameof(currentSessionContainer));
                }

                currentSessionContainer.ReplaceCurrrentStateWithStateOf(container);
            }
        }

        /// <summary>
        /// Gets or sets the session object used for session consistency version tracking for a specific collection in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">Collection for which session token must be retrieved.</param>
        /// <value>
        /// The session token used for version tracking when the consistency level is set to Session.
        /// </value>
        /// <remarks>
        /// The session token can be saved and supplied to a request via <see cref="Documents.Client.RequestOptions.SessionToken"/>.
        /// </remarks>
        internal string GetSessionToken(string collectionLink)
        {
            SessionContainer sessionContainerInternal = this.sessionContainer as SessionContainer;

            if (sessionContainerInternal == null)
            {
                throw new ArgumentNullException(nameof(sessionContainerInternal));
            }

            return sessionContainerInternal.GetSessionToken(collectionLink);
        }

        /// <summary>
        /// Gets the Api type
        /// </summary>
        internal ApiType ApiType
        {
            get; private set;
        }

        internal bool UseMultipleWriteLocations => this.useMultipleWriteLocations;

        /// <summary>
        /// Gets the endpoint Uri for the service endpoint from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the service endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        public Uri ServiceEndpoint
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the current write endpoint chosen based on availability and preference from the Azure Cosmos DB service.
        /// </summary>
        public Uri WriteEndpoint
        {
            get
            {
                return this.globalEndpointManager.WriteEndpoints.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets the current read endpoint chosen based on availability and preference from the Azure Cosmos DB service.
        /// </summary>
        public Uri ReadEndpoint
        {
            get
            {
                return this.globalEndpointManager.ReadEndpoints.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets the Connection policy used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Connection policy used by the client.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.ConnectionPolicy"/>
        public ConnectionPolicy ConnectionPolicy
        {
            get
            {
                return this.connectionPolicy;
            }
        }

        /// <summary>
        /// Gets a dictionary of resource tokens used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// A dictionary of resource tokens used by the client.
        /// </value>
        /// <seealso cref="System.Collections.Generic.IDictionary{TKey, TValue}"/>
        [Obsolete]
        public IDictionary<string, string> ResourceTokens
        {
            get
            {
                // NOTE: if DocumentClient was created using construction taking permission feed and there
                // are duplicate resource links, we will choose arbitrary token for it here.
                return (this.resourceTokens != null) ? this.resourceTokens.ToDictionary(pair => pair.Key, pair => pair.Value.First().ResourceToken) : null;
            }
        }

        /// <summary>
        /// Gets the AuthKey used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        /// <seealso cref="System.Security.SecureString"/>
        public SecureString AuthKey
        {
            get
            {
                if (this.authKeyHashFunction != null)
                {
                    return this.authKeyHashFunction.Key;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the configured consistency level of the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The configured <see cref="Documents.ConsistencyLevel"/> of the client.
        /// </value>
        /// <seealso cref="Documents.ConsistencyLevel"/>
        public virtual Documents.ConsistencyLevel ConsistencyLevel
        {
            get
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                TaskHelper.InlineIfPossibleAsync(() => this.EnsureValidClientAsync(), null).Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
                return this.desiredConsistencyLevel.HasValue ? this.desiredConsistencyLevel.Value :
                    this.accountServiceConfiguration.DefaultConsistencyLevel;
            }
        }

        /// <summary>
        /// Disposes the client for the Azure Cosmos DB service.
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key");
        /// if (client != null) client.Dispose();
        /// ]]>
        /// </code>
        /// </example>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.storeModel != null)
            {
                this.storeModel.Dispose();
                this.storeModel = null;
            }

            if (this.storeClientFactory != null)
            {
                // Dispose only if this store client factory was created and is owned by this instance of document client, otherwise just release the reference
                if (isStoreClientFactoryCreatedInternally)
                {
                    this.storeClientFactory.Dispose();
                }

                this.storeClientFactory = null;
            }

            if (this.AddressResolver != null)
            {
                this.AddressResolver.Dispose();
                this.AddressResolver = null;
            }

            if (this.mediaClient != null)
            {
                this.mediaClient.Dispose();
                this.mediaClient = null;
            }

            if (this.authKeyHashFunction != null)
            {
                this.authKeyHashFunction.Dispose();
                this.authKeyHashFunction = null;
            }

            if (this.globalEndpointManager != null)
            {
                this.globalEndpointManager.Dispose();
                this.globalEndpointManager = null;
            }

            DefaultTrace.TraceInformation("DocumentClient with id {0} disposed.", this.traceId);
            DefaultTrace.Flush();

            this.isDisposed = true;
        }

        /// <summary>
        /// RetryPolicy retries a request when it encounters session unavailable (see ClientRetryPolicy).
        /// Once it exhausts all write regions it clears the session container, then it uses ClientCollectionCache
        /// to resolves the request's collection name. If it differs from the session container's resource id it
        /// explains the session unavailable exception: somebody removed and recreated the collection. In this
        /// case we retry once again (with empty session token) otherwise we return the error to the client
        /// (see RenameCollectionAwareClientRetryPolicy)
        /// </summary>
        internal virtual IRetryPolicyFactory ResetSessionTokenRetryPolicy { get; private set; }

        /// <summary>
        /// Gets and sets the IStoreModel object.
        /// </summary>
        /// <remarks>
        /// Test hook to enable unit test of DocumentClient.
        /// </remarks>
        internal IStoreModel StoreModel
        {
            get { return this.storeModel; }
            set { this.storeModel = value; }
        }

        /// <summary>
        /// Gets and sets the gateway IStoreModel object.
        /// </summary>
        /// <remarks>
        /// Test hook to enable unit test of DocumentClient.
        /// </remarks>
        internal IStoreModel GatewayStoreModel
        {
            get { return this.gatewayStoreModel; }
            set { this.gatewayStoreModel = value; }
        }

        /// <summary>
        /// Gets and sets on execute scalar query callback
        /// </summary>
        /// <remarks>
        /// Test hook to enable unit test for scalar queries
        /// </remarks>
        internal Action<IQueryable> OnExecuteScalarQueryCallback
        {
            get { return this.onExecuteScalarQueryCallback; }
            set { this.onExecuteScalarQueryCallback = value; }
        }

        internal async Task<IDictionary<string, object>> GetQueryEngineConfigurationAsync()
        {
            await this.EnsureValidClientAsync();
            return this.accountServiceConfiguration.QueryEngineConfiguration;
        }

        internal virtual async Task<global::Azure.Cosmos.ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            await this.EnsureValidClientAsync();
            return (global::Azure.Cosmos.ConsistencyLevel)this.accountServiceConfiguration.DefaultConsistencyLevel;
        }

        internal Task<Documents.ConsistencyLevel?> GetDesiredConsistencyLevelAsync()
        {
            return Task.FromResult<Documents.ConsistencyLevel?>(this.desiredConsistencyLevel);
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("DocumentClient");
            }
        }

        internal virtual async Task EnsureValidClientAsync()
        {
            this.ThrowIfDisposed();

            if (this.isSuccessfullyInitialized)
            {
                return;
            }

            // If the initialization task failed, we should retry initialization.
            // We may end up throwing the same exception but this will ensure that we dont have a
            // client which is unusable and can resume working if it failed initialization once.
            // If we have to reinitialize the client, it needs to happen in thread safe manner so that
            // we dont re-initalize the task again for each incoming call.
            Task initTask = null;

            lock (this.initializationSyncLock)
            {
                initTask = this.initializeTask;
            }

            try
            {
                await initTask;
                this.isSuccessfullyInitialized = true;
                return;
            }
            catch (Exception e)
            {
                DefaultTrace.TraceWarning("initializeTask failed {0}", e.ToString());
            }

            lock (this.initializationSyncLock)
            {
                // if the task has not been updated by another caller, update it
                if (object.ReferenceEquals(this.initializeTask, initTask))
                {
                    this.initializeTask = this.GetInitializationTaskAsync(storeClientFactory: null);
                }

                initTask = this.initializeTask;
            }

            await initTask;
            this.isSuccessfullyInitialized = true;
        }

        #region IAuthorizationTokenProvider

        private bool TryGetResourceToken(string resourceAddress, PartitionKeyInternal partitionKey, out string resourceToken)
        {
            resourceToken = null;
            List<PartitionKeyAndResourceTokenPair> partitionKeyTokenPairs;
            bool isPartitionKeyAndTokenPairListAvailable = this.resourceTokens.TryGetValue(resourceAddress, out partitionKeyTokenPairs);
            if (isPartitionKeyAndTokenPairListAvailable)
            {
                PartitionKeyAndResourceTokenPair partitionKeyTokenPair = partitionKeyTokenPairs.FirstOrDefault(pair => pair.PartitionKey.Contains(partitionKey));
                if (partitionKeyTokenPair != null)
                {
                    resourceToken = partitionKeyTokenPair.ResourceToken;
                    return true;
                }
            }

            return false;
        }

        string IAuthorizationTokenProvider.GetUserAuthorizationToken(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            out string payload) // unused, use token based upon what is passed in constructor 
        {
            payload = null;

            if (this.hasAuthKeyResourceToken && this.resourceTokens == null)
            {
                // If the input auth token is a resource token, then use it as a bearer-token.
                return HttpUtility.UrlEncode(this.authKeyResourceToken);
            }

            if (this.authKeyHashFunction != null)
            {
                // this is masterkey authZ
                headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

                return AuthorizationHelper.GenerateKeyAuthorizationSignature(
                        requestVerb, resourceAddress, resourceType, headers, this.authKeyHashFunction, out payload);
            }
            else
            {
                PartitionKeyInternal partitionKey = PartitionKeyInternal.Empty;
                string partitionKeyString = headers[HttpConstants.HttpHeaders.PartitionKey];
                if (partitionKeyString != null)
                {
                    partitionKey = PartitionKeyInternal.FromJsonString(partitionKeyString);
                }

                if (PathsHelper.IsNameBased(resourceAddress))
                {
                    string resourceToken = null;
                    bool isTokenAvailable = false;

                    for (int index = 2; index < ResourceId.MaxPathFragment; index = index + 2)
                    {
                        string resourceParent = PathsHelper.GetParentByIndex(resourceAddress, index);
                        if (resourceParent == null)
                            break;

                        isTokenAvailable = this.TryGetResourceToken(resourceParent, partitionKey, out resourceToken);
                        if (isTokenAvailable)
                            break;
                    }

                    // Get or Head for collection can be done with any child token
                    if (!isTokenAvailable && PathsHelper.GetCollectionPath(resourceAddress) == resourceAddress
                        && (requestVerb == HttpConstants.HttpMethods.Get
                            || requestVerb == HttpConstants.HttpMethods.Head))
                    {
                        string resourceAddressWithSlash = resourceAddress.EndsWith("/", StringComparison.Ordinal)
                                                              ? resourceAddress
                                                              : resourceAddress + "/";
                        foreach (KeyValuePair<string, List<PartitionKeyAndResourceTokenPair>> pair in this.resourceTokens)
                        {
                            if (pair.Key.StartsWith(resourceAddressWithSlash, StringComparison.Ordinal))
                            {
                                resourceToken = pair.Value[0].ResourceToken;
                                isTokenAvailable = true;
                                break;
                            }
                        }
                    }

                    if (!isTokenAvailable)
                    {
                        throw new UnauthorizedException(string.Format(
                           CultureInfo.InvariantCulture, ClientResources.AuthTokenNotFound, resourceAddress));
                    }

                    return HttpUtility.UrlEncode(resourceToken);
                }
                else
                {
                    string resourceToken = null;

                    // In case there is no directly matching token, look for parent's token.
                    ResourceId resourceId = ResourceId.Parse(resourceAddress);

                    bool isTokenAvailable = false;
                    if (resourceId.Attachment != 0 || resourceId.Permission != 0 || resourceId.StoredProcedure != 0
                        || resourceId.Trigger != 0 || resourceId.UserDefinedFunction != 0)
                    {
                        // Use the leaf ID - attachment/permission/sproc/trigger/udf
                        isTokenAvailable = this.TryGetResourceToken(resourceAddress, partitionKey, out resourceToken);
                    }

                    if (!isTokenAvailable &&
                        (resourceId.Attachment != 0 || resourceId.Document != 0))
                    {
                        // Use DocumentID for attachment/document
                        isTokenAvailable = this.TryGetResourceToken(resourceId.DocumentId.ToString(), partitionKey, out resourceToken);
                    }

                    if (!isTokenAvailable &&
                        (resourceId.Attachment != 0 || resourceId.Document != 0 || resourceId.StoredProcedure != 0 || resourceId.Trigger != 0
                        || resourceId.UserDefinedFunction != 0 || resourceId.DocumentCollection != 0))
                    {
                        // Use CollectionID for attachment/document/sproc/trigger/udf/collection
                        isTokenAvailable = this.TryGetResourceToken(resourceId.DocumentCollectionId.ToString(), partitionKey, out resourceToken);
                    }

                    if (!isTokenAvailable &&
                        (resourceId.Permission != 0 || resourceId.User != 0))
                    {
                        // Use UserID for permission/user
                        isTokenAvailable = this.TryGetResourceToken(resourceId.UserId.ToString(), partitionKey, out resourceToken);
                    }

                    if (!isTokenAvailable)
                    {
                        // Use DatabaseId if all else fail
                        isTokenAvailable = this.TryGetResourceToken(resourceId.DatabaseId.ToString(), partitionKey, out resourceToken);
                    }

                    // Get or Head for collection can be done with any child token
                    if (!isTokenAvailable && resourceId.DocumentCollection != 0
                        && (requestVerb == HttpConstants.HttpMethods.Get
                            || requestVerb == HttpConstants.HttpMethods.Head))
                    {
                        foreach (KeyValuePair<string, List<PartitionKeyAndResourceTokenPair>> pair in this.resourceTokens)
                        {
                            ResourceId tokenRid;
                            if (!PathsHelper.IsNameBased(pair.Key) &&
                                ResourceId.TryParse(pair.Key, out tokenRid) &&
                                tokenRid.DocumentCollectionId.Equals(resourceId))
                            {
                                resourceToken = pair.Value[0].ResourceToken;
                                isTokenAvailable = true;
                                break;
                            }
                        }
                    }

                    if (!isTokenAvailable)
                    {
                        throw new UnauthorizedException(string.Format(
                            CultureInfo.InvariantCulture, ClientResources.AuthTokenNotFound, resourceAddress));
                    }

                    return HttpUtility.UrlEncode(resourceToken);
                }
            }
        }

        Task IAuthorizationTokenProvider.AddSystemAuthorizationHeaderAsync(
            DocumentServiceRequest request,
            string federationId,
            string verb,
            string resourceId)
        {
            request.Headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

            request.Headers[HttpConstants.HttpHeaders.Authorization] = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(
                resourceId ?? request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                verb,
                request.Headers,
                request.RequestAuthorizationTokenType,
                payload: out _);

            return Task.FromResult(0);
        }

        #endregion

        /// <summary>
        /// Read the <see cref="AccountProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public Task<AccountProperties> GetDatabaseAccountAsync()
        {
            return TaskHelper.InlineIfPossible(() => this.GetDatabaseAccountPrivateAsync(this.ReadEndpoint), this.ResetSessionTokenRetryPolicy.GetRequestPolicy());
        }

        /// <summary>
        /// Read the <see cref="AccountProperties"/> as an asynchronous operation
        /// given a specific reginal endpoint url.
        /// </summary>
        /// <param name="serviceEndpoint">The reginal url of the serice endpoint.</param>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        Task<AccountProperties> IDocumentClientInternal.GetDatabaseAccountInternalAsync(Uri serviceEndpoint, CancellationToken cancellationToken)
        {
            return this.GetDatabaseAccountPrivateAsync(serviceEndpoint, cancellationToken);
        }

        private async Task<AccountProperties> GetDatabaseAccountPrivateAsync(Uri serviceEndpoint, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();
            GatewayStoreModel gatewayModel = this.gatewayStoreModel as GatewayStoreModel;
            if (gatewayModel != null)
            {
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    INameValueCollection headersCollection = new DictionaryNameValueCollection();
                    string xDate = DateTime.UtcNow.ToString("r");
                    headersCollection.Add(HttpConstants.HttpHeaders.XDate, xDate);
                    request.Headers.Add(HttpConstants.HttpHeaders.XDate, xDate);

                    // Retrieve the CosmosAccountSettings from the gateway.
                    string authorizationToken;

                    if (this.hasAuthKeyResourceToken)
                    {
                        authorizationToken = HttpUtility.UrlEncode(this.authKeyResourceToken);
                    }
                    else
                    {
                        authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                            HttpConstants.HttpMethods.Get,
                            serviceEndpoint,
                            headersCollection,
                            this.authKeyHashFunction);
                    }

                    request.Headers.Add(HttpConstants.HttpHeaders.Authorization, authorizationToken);

                    request.Method = HttpMethod.Get;
                    request.RequestUri = serviceEndpoint;

                    AccountProperties databaseAccount = await gatewayModel.GetDatabaseAccountAsync(request);

                    this.useMultipleWriteLocations = this.connectionPolicy.UseMultipleWriteLocations && databaseAccount.EnableMultipleWriteLocations;

                    return databaseAccount;
                }
            }

            return null;
        }

        #region Private Impl

        /// <summary>
        /// Certain requests must be routed through gateway even when the client connectivity mode is direct.
        /// For e.g., DocumentCollection creation. This method returns the <see cref="IStoreModel"/> based
        /// on the input <paramref name="request"/>.
        /// </summary>
        /// <returns>Returns <see cref="IStoreModel"/> to which the request must be sent</returns>
        internal IStoreModel GetStoreProxy(DocumentServiceRequest request)
        {
            // If a request is configured to always use Gateway mode(in some cases when targeting .NET Core)
            // we return the Gateway store model
            if (request.UseGatewayMode)
            {
                return this.gatewayStoreModel;
            }

            ResourceType resourceType = request.ResourceType;
            OperationType operationType = request.OperationType;

            if (resourceType == ResourceType.Offer ||
                (resourceType.IsScript() && operationType != OperationType.ExecuteJavaScript) ||
                resourceType == ResourceType.PartitionKeyRange)
            {
                return this.gatewayStoreModel;
            }

            if (operationType == OperationType.Create
                || operationType == OperationType.Upsert)
            {
                if (resourceType == ResourceType.Database ||
                    resourceType == ResourceType.User ||
                    resourceType == ResourceType.Collection ||
                    resourceType == ResourceType.Permission)
                {
                    return this.gatewayStoreModel;
                }
                else
                {
                    return this.storeModel;
                }
            }
            else if (operationType == OperationType.Delete)
            {
                if (resourceType == ResourceType.Database ||
                    resourceType == ResourceType.User ||
                    resourceType == ResourceType.Collection)
                {
                    return this.gatewayStoreModel;
                }
                else
                {
                    return this.storeModel;
                }
            }
            else if (operationType == OperationType.Replace)
            {
                if (resourceType == ResourceType.Collection)
                {
                    return this.gatewayStoreModel;
                }
                else
                {
                    return this.storeModel;
                }
            }
            else if (operationType == OperationType.Read)
            {
                if (resourceType == ResourceType.Collection)
                {
                    return this.gatewayStoreModel;
                }
                else
                {
                    return this.storeModel;
                }
            }
            else
            {
                return this.storeModel;
            }
        }

        internal void EnsureValidOverwrite(Documents.ConsistencyLevel desiredConsistencyLevel)
        {
            Documents.ConsistencyLevel defaultConsistencyLevel = this.accountServiceConfiguration.DefaultConsistencyLevel;
            if (!this.IsValidConsistency(defaultConsistencyLevel, desiredConsistencyLevel))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentUICulture,
                    RMResources.InvalidConsistencyLevel,
                    desiredConsistencyLevel.ToString(),
                    defaultConsistencyLevel.ToString()));
            }
        }

        private bool IsValidConsistency(Documents.ConsistencyLevel backendConsistency, Documents.ConsistencyLevel desiredConsistency)
        {
            if (this.allowOverrideStrongerConsistency)
            {
                return true;
            }

            return ValidationHelpers.IsValidConsistencyLevelOverwrite(backendConsistency, desiredConsistency);
        }

        private void InitializeDirectConnectivity(IStoreClientFactory storeClientFactory)
        {
            // Check if we have a store client factory in input and if we do, do not initialize another store client
            // The purpose is to reuse store client factory across all document clients inside compute gateway
            if (storeClientFactory != null)
            {
                this.storeClientFactory = storeClientFactory;
                this.isStoreClientFactoryCreatedInternally = false;
            }
            else
            {
                StoreClientFactory newClientFactory = new StoreClientFactory(
                    this.connectionPolicy.ConnectionProtocol,
                    (int)this.connectionPolicy.RequestTimeout.TotalSeconds,
                    this.maxConcurrentConnectionOpenRequests,
                    this.connectionPolicy.UserAgentContainer,
                    this.eventSource,
                    null,
                    this.openConnectionTimeoutInSeconds,
                    this.idleConnectionTimeoutInSeconds,
                    this.timerPoolGranularityInSeconds,
                    this.maxRntbdChannels,
                    this.rntbdPartitionCount,
                    this.maxRequestsPerRntbdChannel,
                    receiveHangDetectionTimeSeconds: this.rntbdReceiveHangDetectionTimeSeconds,
                    sendHangDetectionTimeSeconds: this.rntbdSendHangDetectionTimeSeconds,
                    enableCpuMonitor: this.enableCpuMonitor,
                    retryWithConfiguration: this.connectionPolicy.RetryOptions?.GetRetryWithConfiguration());

                if (this.transportClientHandlerFactory != null)
                {
                    newClientFactory.WithTransportInterceptor(this.transportClientHandlerFactory);
                }

                this.storeClientFactory = newClientFactory;
                this.isStoreClientFactoryCreatedInternally = true;
            }

            this.AddressResolver = new GlobalAddressResolver(
                this.globalEndpointManager,
                this.connectionPolicy.ConnectionProtocol,
                this,
                this.collectionCache,
                this.partitionKeyRangeCache,
                this.connectionPolicy.UserAgentContainer,
                this.accountServiceConfiguration,
                this.httpMessageHandler,
                this.connectionPolicy,
                this.ApiType);

            this.CreateStoreModel(subscribeRntbdStatus: true);
        }

        private void CreateStoreModel(bool subscribeRntbdStatus)
        {
            //EnableReadRequestsFallback, if not explicity set on the connection policy,
            //is false if the account's consistency is bounded staleness,
            //and true otherwise.
            StoreClient storeClient = this.storeClientFactory.CreateStoreClient(
                this.AddressResolver,
                this.sessionContainer,
                this.accountServiceConfiguration,
                this,
                true,
                this.connectionPolicy.EnableReadRequestsFallback ?? (this.accountServiceConfiguration.DefaultConsistencyLevel != Documents.ConsistencyLevel.BoundedStaleness),
                !this.enableRntbdChannel,
                this.useMultipleWriteLocations && (this.accountServiceConfiguration.DefaultConsistencyLevel != Documents.ConsistencyLevel.Strong),
                true);

            if (subscribeRntbdStatus)
            {
                storeClient.AddDisableRntbdChannelCallback(new Action(this.DisableRntbdChannel));
            }

            //storeClient.SerializerSettings = this.serializerSettings;

            this.storeModel = new ServerStoreModel(storeClient, this.sendingRequest, this.receivedResponse);
        }

        private void DisableRntbdChannel()
        {
            Debug.Assert(this.enableRntbdChannel);
            this.enableRntbdChannel = false;
            this.CreateStoreModel(subscribeRntbdStatus: false);
        }

        private async Task InitializeGatewayConfigurationReaderAsync()
        {
            GatewayAccountReader accountReader = new GatewayAccountReader(
                    this.ServiceEndpoint,
                    this.authKeyHashFunction,
                    this.hasAuthKeyResourceToken,
                    this.authKeyResourceToken,
                    this.connectionPolicy,
                    this.ApiType,
                    this.httpMessageHandler);

            this.accountServiceConfiguration = new CosmosAccountServiceConfiguration(accountReader.InitializeReaderAsync);

            await this.accountServiceConfiguration.InitializeAsync();
            AccountProperties accountProperties = this.accountServiceConfiguration.AccountProperties;
            this.useMultipleWriteLocations = this.connectionPolicy.UseMultipleWriteLocations && accountProperties.EnableMultipleWriteLocations;

            await this.globalEndpointManager.RefreshLocationAsync(accountProperties);
        }

        internal void CaptureSessionToken(DocumentServiceRequest request, DocumentServiceResponse response)
        {
            this.sessionContainer.SetSessionToken(request, response.Headers);
        }

        internal void ValidateResource(string resourceId)
        {
            if (!string.IsNullOrEmpty(resourceId))
            {
                int match = resourceId.IndexOfAny(new char[] { '/', '\\', '?', '#' });
                if (match != -1)
                {
                    throw new ArgumentException(string.Format(
                                CultureInfo.CurrentUICulture,
                                RMResources.InvalidCharacterInResourceName,
                                resourceId[match]));
                }

                if (resourceId[resourceId.Length - 1] == ' ')
                {
                    throw new ArgumentException(RMResources.InvalidSpaceEndingInResourceName);
                }
            }
        }

        private class ResetSessionTokenRetryPolicyFactory : IRetryPolicyFactory
        {
            private readonly IRetryPolicyFactory retryPolicy;
            private readonly ISessionContainer sessionContainer;
            private readonly ClientCollectionCache collectionCache;

            public ResetSessionTokenRetryPolicyFactory(ISessionContainer sessionContainer, ClientCollectionCache collectionCache, IRetryPolicyFactory retryPolicy)
            {
                this.retryPolicy = retryPolicy;
                this.sessionContainer = sessionContainer;
                this.collectionCache = collectionCache;
            }

            public IDocumentClientRetryPolicy GetRequestPolicy()
            {
                return new RenameCollectionAwareClientRetryPolicy(this.sessionContainer, this.collectionCache, retryPolicy.GetRequestPolicy());
            }
        }

        private class HttpRequestMessageHandler : DelegatingHandler
        {
            private readonly EventHandler<SendingRequestEventArgs> sendingRequest;
            private readonly EventHandler<ReceivedResponseEventArgs> receivedResponse;

            public HttpRequestMessageHandler(EventHandler<SendingRequestEventArgs> sendingRequest, EventHandler<ReceivedResponseEventArgs> receivedResponse, HttpMessageHandler innerHandler)
            {
                this.sendingRequest = sendingRequest;
                this.receivedResponse = receivedResponse;

                InnerHandler = innerHandler ?? new HttpClientHandler();
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.sendingRequest?.Invoke(this, new SendingRequestEventArgs(request));
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                this.receivedResponse?.Invoke(this, new ReceivedResponseEventArgs(request, response));
                return response;
            }
        }

        #endregion
    }
}
