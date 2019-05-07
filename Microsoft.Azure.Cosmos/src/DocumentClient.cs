//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

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
        private const int MaxConcurrentConnectionOpenRequestsPerProcessor = 25;
        private const int DefaultMaxRequestsPerRntbdChannel = 30;
        private const int DefaultRntbdPartitionCount = 1;
        private const int DefaultMaxRntbdChannelsPerHost = ushort.MaxValue;
        private const int DefaultRntbdReceiveHangDetectionTimeSeconds = 65;
        private const int DefaultRntbdSendHangDetectionTimeSeconds = 10;
        private const bool DefaultEnableCpuMonitor = true;

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
        private readonly IDictionary<string, List<PartitionKeyAndResourceTokenPair>> resourceTokens;
        private IComputeHash authKeyHashFunction;

        //Consistency
        private ConsistencyLevel? desiredConsistencyLevel;

        private CosmosAccountServiceConfiguration accountServiceConfiguration;

        private ClientCollectionCache collectionCache;

        private PartitionKeyRangeCache partitionKeyRangeCache;

        private HttpMessageHandler httpMessageHandler;

        //Private state.
        private bool isSuccessfullyInitialized;
        private bool isDisposed;
        private object initializationSyncLock;  /* guards initializeTask */

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
        private ISessionContainer sessionContainer;

        private readonly bool hasAuthKeyResourceToken;
        private readonly string authKeyResourceToken = string.Empty;

        private DocumentClientEventSource eventSource;
        private GlobalEndpointManager globalEndpointManager;
        private bool useMultipleWriteLocations;

        internal Task initializeTask;

        private JsonSerializerSettings serializerSettings;
        private event EventHandler<SendingRequestEventArgs> sendingRequest;
        private event EventHandler<ReceivedResponseEventArgs> receivedResponse;
        private Func<TransportClient, TransportClient> transportClientHandlerFactory;

        //Callback for on execution of scalar LINQ queries event.
        //This callback is meant for tests only.
        private Action<IQueryable> onExecuteScalarQueryCallback;

        static DocumentClient()
        {
            StringKeyValueCollection.SetNameValueCollectionFactory(new DictionaryNameValueCollectionFactory());
        }

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
                              ConsistencyLevel? desiredConsistencyLevel = null)
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
        /// specified Azure Cosmos DB service endpoint, key, connection policy and a custom JsonSerializerSettings
        /// for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="serviceEndpoint">
        /// The service endpoint to use to create the client.
        /// </param>
        /// <param name="authKey">
        /// The list of Permission objects to use to create the client.
        /// </param>
        /// <param name="connectionPolicy">
        /// The connection policy for the client.
        /// </param>
        /// <param name="desiredConsistencyLevel">
        /// This can be used to weaken the database account consistency level for read operations.
        /// If this is not set the database account consistency level will be used for all requests.
        /// </param>
        /// <param name="serializerSettings">
        /// The custom JsonSerializer settings to be used for serialization/derialization.
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
        /// <seealso cref="JsonSerializerSettings"/>
        [Obsolete("Please use the constructor that takes JsonSerializerSettings as the third parameter.")]
        public DocumentClient(Uri serviceEndpoint,
                              SecureString authKey,
                              ConnectionPolicy connectionPolicy,
                              ConsistencyLevel? desiredConsistencyLevel,
                              JsonSerializerSettings serializerSettings)
            : this(serviceEndpoint, authKey, connectionPolicy, desiredConsistencyLevel)
        {
            this.serializerSettings = serializerSettings;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified Azure Cosmos DB service endpoint, key, connection policy and a custom JsonSerializerSettings
        /// for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="serviceEndpoint">
        /// The service endpoint to use to create the client.
        /// </param>
        /// <param name="authKey">
        /// The list of Permission objects to use to create the client.
        /// </param>
        /// <param name="serializerSettings">
        /// The custom JsonSerializer settings to be used for serialization/derialization.
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
        /// <seealso cref="JsonSerializerSettings"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        public DocumentClient(Uri serviceEndpoint,
                              SecureString authKey,
                              JsonSerializerSettings serializerSettings,
                              ConnectionPolicy connectionPolicy = null,
                              ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint, authKey, connectionPolicy, desiredConsistencyLevel)
        {
            this.serializerSettings = serializerSettings;
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
                              ConsistencyLevel? desiredConsistencyLevel = null)
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
        /// <param name="sendingRequestEventArgs"> The event handler to be invoked before the request is sent.</param>
        /// <param name="receivedResponseEventArgs"> The event handler to be invoked after a response has been received.</param>
        /// <param name="connectionPolicy">(Optional) The connection policy for the client.</param>
        /// <param name="desiredConsistencyLevel">(Optional) The default consistency policy for client operations.</param>
        /// <param name="transportClientHandlerFactory">(Optional) transport interceptor factory</param>
        /// <param name="serializerSettings">The custom JsonSerializer settings to be used for serialization/derialization.</param>
        /// <param name="apitype">Api type for the account</param>
        /// <param name="sessionContainer">The default session container with which DocumentClient is created</param>
        /// <param name="enableCpuMonitor">Flag that indicates whether client-side CPU monitoring is enabled for improved troubleshooting.</param>
        /// <param name="storeClientFactory">Factory that creates store clients sharing the same transport client to optimize network resource reuse across multiple document clients in the same process.</param>
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
        internal DocumentClient(Uri serviceEndpoint,
                              string authKeyOrResourceToken,
                              EventHandler<SendingRequestEventArgs> sendingRequestEventArgs,
                              ConnectionPolicy connectionPolicy = null,
                              ConsistencyLevel? desiredConsistencyLevel = null,
                              JsonSerializerSettings serializerSettings = null,
                              ApiType apitype = ApiType.None,
                              EventHandler<ReceivedResponseEventArgs> receivedResponseEventArgs = null,
                              ISessionContainer sessionContainer = null,
                              Func<TransportClient, TransportClient> transportClientHandlerFactory = null,
                              bool? enableCpuMonitor = null,
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

            if (serializerSettings != null)
            {
                this.serializerSettings = serializerSettings;
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
            this.Initialize(serviceEndpoint: serviceEndpoint,
                connectionPolicy: connectionPolicy,
                desiredConsistencyLevel: desiredConsistencyLevel,
                sessionContainer: sessionContainer,
                enableCpuMonitor: enableCpuMonitor, 
                storeClientFactory: storeClientFactory);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified service endpoint, an authorization key (or resource token), a connection policy
        /// and a custom JsonSerializerSettings for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="serviceEndpoint">The service endpoint to use to create the client.</param>
        /// <param name="authKeyOrResourceToken">The authorization key or resource token to use to create the client.</param>
        /// <param name="connectionPolicy">The connection policy for the client.</param>
        /// <param name="desiredConsistencyLevel">The default consistency policy for client operations.</param>
        /// <param name="serializerSettings">The custom JsonSerializer settings to be used for serialization/derialization.</param>
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
        /// <seealso cref="JsonSerializerSettings"/>
        [Obsolete("Please use the constructor that takes JsonSerializerSettings as the third parameter.")]
        public DocumentClient(Uri serviceEndpoint,
                              string authKeyOrResourceToken,
                              ConnectionPolicy connectionPolicy,
                              ConsistencyLevel? desiredConsistencyLevel,
                              JsonSerializerSettings serializerSettings)
            : this(serviceEndpoint, authKeyOrResourceToken, connectionPolicy, desiredConsistencyLevel)
        {
            this.serializerSettings = serializerSettings;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified service endpoint, an authorization key (or resource token), a connection policy
        /// and a custom JsonSerializerSettings for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="serviceEndpoint">The service endpoint to use to create the client.</param>
        /// <param name="authKeyOrResourceToken">The authorization key or resource token to use to create the client.</param>
        /// <param name="serializerSettings">The custom JsonSerializer settings to be used for serialization/derialization.</param>
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
        /// <seealso cref="JsonSerializerSettings"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>

        public DocumentClient(Uri serviceEndpoint,
                              string authKeyOrResourceToken,
                              JsonSerializerSettings serializerSettings,
                              ConnectionPolicy connectionPolicy = null,
                              ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint, authKeyOrResourceToken, connectionPolicy, desiredConsistencyLevel)
        {
            this.serializerSettings = serializerSettings;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClient"/> class using the
        /// specified Azure Cosmos DB service endpoint for the Azure Cosmos DB service, a list of permission objects and a connection policy.
        /// </summary>
        /// <param name="serviceEndpoint">The service endpoint to use to create the client.</param>
        /// <param name="permissionFeed">A list of Permission objects to use to create the client.</param>
        /// <param name="connectionPolicy">(Optional) The <see cref="Microsoft.Azure.Cosmos.ConnectionPolicy"/> to use for this connection.</param>
        /// <param name="desiredConsistencyLevel">(Optional) The default consistency policy for client operations.</param>
        /// <exception cref="System.ArgumentNullException">If <paramref name="permissionFeed"/> is not supplied.</exception>
        /// <exception cref="System.ArgumentException">If <paramref name="permissionFeed"/> is not a valid permission link.</exception>
        /// <remarks>
        /// If no <paramref name="connectionPolicy"/> is provided, then the default <see cref="Microsoft.Azure.Cosmos.ConnectionPolicy"/> will be used.
        /// Using Direct connectivity, wherever possible, is recommended.
        /// </remarks>
        /// <seealso cref="Uri"/>
        /// <seealso cref="Permission"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        public DocumentClient(
            Uri serviceEndpoint,
            IList<Permission> permissionFeed,
            ConnectionPolicy connectionPolicy = null,
            ConsistencyLevel? desiredConsistencyLevel = null)
            : this(serviceEndpoint,
                    GetResourceTokens(permissionFeed),
                    connectionPolicy,
                    desiredConsistencyLevel)
        { }

        private static List<ResourceToken> GetResourceTokens(IList<Permission> permissionFeed)
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
        /// <seealso cref="Permission"/>
        /// <seealso cref="ConnectionPolicy"/>
        /// <seealso cref="ConsistencyLevel"/>
        internal DocumentClient(Uri serviceEndpoint,
                              IList<ResourceToken> resourceTokens,
                              ConnectionPolicy connectionPolicy = null,
                              ConsistencyLevel? desiredConsistencyLevel = null)
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
            ConsistencyLevel? desiredConsistencyLevel = null)
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

        /// <summary>
        /// Open the connection to validate that the client initialization is successful in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        /// <remarks>
        /// This method is recommended to be called, after the constructor, but before calling any other methods on the DocumentClient instance.
        /// If there are any initialization exceptions, this method will throw them (set on the task).
        /// Alternately, calling any API will throw initialization exception at the first call.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     await client.OpenAsync();
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public Task OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return TaskHelper.InlineIfPossible(() => OpenPrivateInlineAsync(cancellationToken), null, cancellationToken);
        }

        private async Task OpenPrivateInlineAsync(CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();
            await TaskHelper.InlineIfPossible(() => this.OpenPrivateAsync(cancellationToken), this.ResetSessionTokenRetryPolicy.GetRequestPolicy(), cancellationToken);
        }

        private async Task OpenPrivateAsync(CancellationToken cancellationToken)
        {
            // Initialize caches for all databases and collections
            ResourceFeedReader<CosmosDatabaseSettings> databaseFeedReader = this.CreateDatabaseFeedReader(
                new FeedOptions { MaxItemCount = -1 });

            try
            {
                while (databaseFeedReader.HasMoreResults)
                {
                    foreach (CosmosDatabaseSettings database in await databaseFeedReader.ExecuteNextAsync(cancellationToken))
                    {
                        ResourceFeedReader<CosmosContainerSettings> collectionFeedReader = this.CreateDocumentCollectionFeedReader(
                            database.SelfLink,
                            new FeedOptions { MaxItemCount = -1 });
                        List<Task> tasks = new List<Task>();
                        while (collectionFeedReader.HasMoreResults)
                        {
                            tasks.AddRange((await collectionFeedReader.ExecuteNextAsync(cancellationToken)).Select(collection => this.InitializeCachesAsync(collection, cancellationToken)));
                        }

                        await Task.WhenAll(tasks);
                    }
                }
            }
            catch (DocumentClientException ex)
            {
                // Clear the caches to ensure that we don't have partial results 
                this.collectionCache = new ClientCollectionCache(this.sessionContainer, this.gatewayStoreModel, this, this.retryPolicy);
                this.partitionKeyRangeCache = new PartitionKeyRangeCache(this, this.gatewayStoreModel, this.collectionCache);

                DefaultTrace.TraceWarning("{0} occurred while OpenAsync. Exception Message: {1}", ex.ToString(), ex.Message);
            }
        }

        internal virtual void Initialize(Uri serviceEndpoint,
            ConnectionPolicy connectionPolicy = null,
            ConsistencyLevel? desiredConsistencyLevel = null,
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
#endif
            this.ServiceEndpoint = serviceEndpoint.OriginalString.EndsWith("/", StringComparison.Ordinal) ? serviceEndpoint : new Uri(serviceEndpoint.OriginalString + "/");

            this.connectionPolicy = connectionPolicy ?? ConnectionPolicy.Default;

#if !NETSTANDARD16
            ServicePoint servicePoint = ServicePointManager.FindServicePoint(this.ServiceEndpoint);
            servicePoint.ConnectionLimit = this.connectionPolicy.MaxConnectionLimit;
#endif
           
            this.globalEndpointManager = new GlobalEndpointManager(this, this.connectionPolicy);

            this.httpMessageHandler = new HttpRequestMessageHandler(this.sendingRequest, this.receivedResponse);

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

            this.eventSource = new DocumentClientEventSource();

            this.initializeTask = TaskHelper.InlineIfPossible(
                () => this.GetInitializationTask(storeClientFactory: storeClientFactory),
                new ResourceThrottleRetryPolicy(
                    this.connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests,
                    this.connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds));

            // ContinueWith on the initialization task is needed for handling the UnobservedTaskException
            // if this task throws for some reason. Awaiting inside a constructor is not supported and
            // even if we had to await inside GetInitializationTask to catch the exception, that will
            // be a blocking call. In such cases, the recommended approach is to "handle" the
            // UnobservedTaskException by using ContinueWith method w/ TaskContinuationOptions.OnlyOnFaulted
            // and accessing the Exception property on the target task.
            this.initializeTask.ContinueWith(t =>
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

            this.QueryCompatibilityMode = QueryCompatibilityMode.Default;
        }

        // Always called from under the lock except when called from Initialize method during construction.
        private async Task GetInitializationTask(IStoreClientFactory storeClientFactory)
        {
            await this.InitializeGatewayConfigurationReader();

            if (this.desiredConsistencyLevel.HasValue)
            {
                this.EnsureValidOverwrite(this.desiredConsistencyLevel.Value);
            }

            GatewayStoreModel gatewayStoreModel = new GatewayStoreModel(
                    this.globalEndpointManager,
                    this.sessionContainer,
                    this.connectionPolicy.RequestTimeout,
                    this.accountServiceConfiguration.DefaultConsistencyLevel,
                    this.eventSource,
                    this.connectionPolicy.UserAgentContainer,
                    this.ApiType,
                    this.httpMessageHandler);
            gatewayStoreModel.SerializerSettings = this.serializerSettings;
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

        private async Task InitializeCachesAsync(CosmosContainerSettings collection, CancellationToken cancellationToken)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            CollectionCache collectionCache = await this.GetCollectionCacheAsync();
            using (
                DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.Query,
                    ResourceType.Document,
                    collection.SelfLink,
                    AuthorizationTokenType.PrimaryMasterKey))
            {
                collection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);
                IReadOnlyList<PartitionKeyRange> ranges = await this.partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                collection.ResourceId,
                new Range<string>(
                    PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));
                
                // In Gateway mode, AddressCache is null
                if (this.AddressResolver != null)
                {
                    await this.AddressResolver.OpenAsync(collection, cancellationToken);
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
                this.sessionContainer = container;
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
        /// The session token can be saved and supplied to a request via <see cref="RequestOptions.SessionToken"/>.
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
        /// Gets or Sets the Api type
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
        /// The configured <see cref="Microsoft.Azure.Cosmos.ConsistencyLevel"/> of the client.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.ConsistencyLevel"/>
        public virtual ConsistencyLevel ConsistencyLevel
        {
            get
            {
                TaskHelper.InlineIfPossible(() => this.EnsureValidClientAsync(), null).Wait();
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

        //Compatibility mode:
        // Allows to specify compatibility mode used by client when making query requests.
        // should be removed when application/sql is no longer supported.
        internal QueryCompatibilityMode QueryCompatibilityMode { get; set; }

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

        internal async Task<IDictionary<string, object>> GetQueryEngineConfiguration()
        {
            await this.EnsureValidClientAsync();
            return this.accountServiceConfiguration.QueryEngineConfiguration;
        }

        internal async Task<ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            await this.EnsureValidClientAsync();
            return this.accountServiceConfiguration.DefaultConsistencyLevel;
        }
        
        internal Task<ConsistencyLevel?> GetDesiredConsistencyLevelAsync()
        {
            return Task.FromResult<ConsistencyLevel?>(this.desiredConsistencyLevel);
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
                    this.initializeTask = this.GetInitializationTask(storeClientFactory: null);
                }

                initTask = this.initializeTask;
            }

            await initTask;
            this.isSuccessfullyInitialized = true;
        }

        #region Create Impl

        /// <summary>
        /// Creates a database resource as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="database">The specification for the <see cref="CosmosDatabaseSettings"/> to create.</param>
        /// <param name="options">(Optional) The <see cref="RequestOptions"/> for the request.</param>
        /// <returns>The <see cref="CosmosDatabaseSettings"/> that was created within a task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="database"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Database are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the database object supplied. It is likely that an id was not supplied for the new Database.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosDatabaseSettings"/> with an id matching the id field of <paramref name="database"/> already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// The example below creates a new <see cref="CosmosDatabaseSettings"/> with an Id property of 'MyDatabase'
        /// This code snippet is intended to be used from within an asynchronous method as it uses the await keyword
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = await client.CreateDatabaseAsync(new Database { Id = "MyDatabase" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// If you would like to construct a <see cref="CosmosDatabaseSettings"/> from within a synchronous method then you need to use the following code
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = client.CreateDatabaseAsync(new Database { Id = "MyDatabase" }).Result;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosDatabaseSettings>> CreateDatabaseAsync(CosmosDatabaseSettings database, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.CreateDatabasePrivateAsync(database, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosDatabaseSettings>> CreateDatabasePrivateAsync(CosmosDatabaseSettings database, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            this.ValidateResource(database);

            INameValueCollection headers = this.GetRequestHeaders(options);

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                Paths.Databases_Root,
                database,
                ResourceType.Database,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosDatabaseSettings>(await this.CreateAsync(request));
            }
        }

        /// <summary>
        /// Creates(if doesn't exist) or gets(if already exists) a database resource as an asychronous operation in the Azure Cosmos DB service.
        /// You can check the status code from the response to determine whether the database was newly created(201) or existing database was returned(200)
        /// </summary>
        /// <param name="database">The specification for the <see cref="CosmosDatabaseSettings"/> to create.</param>
        /// <param name="options">(Optional) The <see cref="RequestOptions"/> for the request.</param>
        /// <returns>The <see cref="CosmosDatabaseSettings"/> that was created within a task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="database"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property.</exception>
        /// <example>
        /// The example below creates a new <see cref="CosmosDatabaseSettings"/> with an Id property of 'MyDatabase'
        /// This code snippet is intended to be used from within an asynchronous method as it uses the await keyword
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = "MyDatabase" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// If you would like to construct a <see cref="CosmosDatabaseSettings"/> from within a synchronous method then you need to use the following code
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = client.CreateDatabaseIfNotExistsAsync(new Database { Id = "MyDatabase" }).Result;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosDatabaseSettings>> CreateDatabaseIfNotExistsAsync(CosmosDatabaseSettings database, RequestOptions options = null)
        {
            return TaskHelper.InlineIfPossible(() => CreateDatabaseIfNotExistsPrivateAsync(database, options), null);
        }

        private async Task<ResourceResponse<CosmosDatabaseSettings>> CreateDatabaseIfNotExistsPrivateAsync(CosmosDatabaseSettings database,
            RequestOptions options)
        {
            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            // Doing a Read before Create will give us better latency for existing databases
            try
            {
                return await this.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(database.Id));
            }
            catch (DocumentClientException dce)
            {
                if (dce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            try
            {
                return await this.CreateDatabaseAsync(database, options);
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await this.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(database.Id));
        }

        /// <summary>
        /// Creates a Document as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentsFeedOrDatabaseLink">The link of the <see cref="CosmosContainerSettings"/> to create the document in. E.g. dbs/db_rid/colls/coll_rid/ </param>
        /// <param name="document">The document object to create.</param>
        /// <param name="options">(Optional) Any request options you wish to set. E.g. Specifying a Trigger to execute when creating the document. <see cref="RequestOptions"/></param>
        /// <param name="disableAutomaticIdGeneration">(Optional) Disables the automatic id generation, If this is True the system will throw an exception if the id property is missing from the Document.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="documentsFeedOrDatabaseLink"/> or <paramref name="document"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the document supplied. It is likely that <paramref name="disableAutomaticIdGeneration"/> was true and an id was not supplied</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This likely means the collection in to which you were trying to create the document is full.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Document"/> with an id matching the id field of <paramref name="document"/> already existed</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the <see cref="Document"/> exceeds the current max entity size. Consult documentation for limits and quotas.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// Azure Cosmos DB supports a number of different ways to work with documents. A document can extend <see cref="CosmosResource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyObject : Resource
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyObject { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// A document can be any POCO object that can be serialized to JSON, even if it doesn't extend from <see cref="CosmosResource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyPOCO
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyPOCO { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// Finally, a Document can also be a dynamic object
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync("dbs/db_rid/colls/coll_rid/", new { SomeProperty = "A Value" } );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// Create a Document and execute a Pre and Post Trigger
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync(
        ///         "dbs/db_rid/colls/coll_rid/",
        ///         new { id = "DOC123213443" },
        ///         new RequestOptions
        ///         {
        ///             PreTriggerInclude = new List<string> { "MyPreTrigger" },
        ///             PostTriggerInclude = new List<string> { "MyPostTrigger" }
        ///         });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Document>> CreateDocumentAsync(string documentsFeedOrDatabaseLink,
            object document, RequestOptions options = null, bool disableAutomaticIdGeneration = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // This call is to just run CreateDocumentInlineAsync in a SynchronizationContext aware environment
            return TaskHelper.InlineIfPossible(() => CreateDocumentInlineAsync(documentsFeedOrDatabaseLink, document, options, disableAutomaticIdGeneration, cancellationToken), null, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> CreateDocumentInlineAsync(string documentsFeedOrDatabaseLink, object document, RequestOptions options, bool disableAutomaticIdGeneration, CancellationToken cancellationToken)
        {
            IDocumentClientRetryPolicy requestRetryPolicy = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            if (options == null || options.PartitionKey == null)
            {
                requestRetryPolicy = new PartitionKeyMismatchRetryPolicy(await this.GetCollectionCacheAsync(), requestRetryPolicy);
            }

            return await TaskHelper.InlineIfPossible(() => this.CreateDocumentPrivateAsync(
                documentsFeedOrDatabaseLink,
                document,
                options,
                disableAutomaticIdGeneration,
                requestRetryPolicy,
                cancellationToken), requestRetryPolicy);
        }

        private async Task<ResourceResponse<Document>> CreateDocumentPrivateAsync(
            string documentCollectionLink,
            object document,
            RequestOptions options,
            bool disableAutomaticIdGeneration,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentCollectionLink))
            {
                throw new ArgumentNullException("documentCollectionLink");
            }

            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            Document typedDocument = Document.FromObject(document, this.GetSerializerSettingsForRequest(options));

            this.ValidateResource(typedDocument);

            if (string.IsNullOrEmpty(typedDocument.Id) && !disableAutomaticIdGeneration)
            {
                typedDocument.Id = Guid.NewGuid().ToString();
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                documentCollectionLink,
                typedDocument,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None,
                this.GetSerializerSettingsForRequest(options)))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, typedDocument, options);
                return new ResourceResponse<Document>(await this.CreateAsync(request, cancellationToken));
            }
        }

        /// <summary>
        /// Creates a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseLink">The link of the database to create the collection in. E.g. dbs/db_rid/.</param>
        /// <param name="documentCollection">The <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> object.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/> you wish to provide when creating a Collection. E.g. RequestOptions.OfferThroughput = 400. </param>
        /// <returns>The <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="databaseLink"/> or <paramref name="documentCollection"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a collection are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new collection.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for collections. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     //Create a new collection with an OfferThroughput set to 10000
        ///     //Not passing in RequestOptions.OfferThroughput will result in a collection with the default OfferThroughput set.
        ///     DocumentCollection coll = await client.CreateDocumentCollectionAsync(databaseLink,
        ///         new DocumentCollection { Id = "My Collection" },
        ///         new RequestOptions { OfferThroughput = 10000} );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.OfferV2"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosContainerSettings>> CreateDocumentCollectionAsync(string databaseLink, CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.CreateDocumentCollectionPrivateAsync(databaseLink, documentCollection, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosContainerSettings>> CreateDocumentCollectionPrivateAsync(
            string databaseLink,
            CosmosContainerSettings documentCollection,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentNullException("databaseLink");
            }

            if (documentCollection == null)
            {
                throw new ArgumentNullException("documentCollection");
            }

            this.ValidateResource(documentCollection);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                databaseLink,
                documentCollection,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                ResourceResponse<CosmosContainerSettings> collection = new ResourceResponse<CosmosContainerSettings>(await this.CreateAsync(request));
                // set the session token
                this.sessionContainer.SetSessionToken(collection.Resource.ResourceId, collection.Resource.AltLink, collection.Headers);
                return collection;
            }
        }

        /// <summary>
        /// Creates (if doesn't exist) or gets (if already exists) a collection as an asychronous operation in the Azure Cosmos DB service.
        /// You can check the status code from the response to determine whether the collection was newly created (201) or existing collection was returned (200).
        /// </summary>
        /// <param name="databaseLink">The link of the database to create the collection in. E.g. dbs/db_rid/.</param>
        /// <param name="documentCollection">The <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> object.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/> you wish to provide when creating a Collection. E.g. RequestOptions.OfferThroughput = 400. </param>
        /// <returns>The <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="databaseLink"/> or <paramref name="documentCollection"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a CosmosContainerSettings are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new collection.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for collections. Contact support to have this quota increased.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     //Create a new collection with an OfferThroughput set to 10000
        ///     //Not passing in RequestOptions.OfferThroughput will result in a collection with the default OfferThroughput set.
        ///     DocumentCollection coll = await client.CreateDocumentCollectionIfNotExistsAsync(databaseLink,
        ///         new DocumentCollection { Id = "My Collection" },
        ///         new RequestOptions { OfferThroughput = 10000} );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.OfferV2"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosContainerSettings>> CreateDocumentCollectionIfNotExistsAsync(string databaseLink, CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            return TaskHelper.InlineIfPossible(() => CreateDocumentCollectionIfNotExistsPrivateAsync(databaseLink, documentCollection, options), null);
        }

        private async Task<ResourceResponse<CosmosContainerSettings>> CreateDocumentCollectionIfNotExistsPrivateAsync(
            string databaseLink, CosmosContainerSettings documentCollection, RequestOptions options)
        {
            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentNullException("databaseLink");
            }

            if (documentCollection == null)
            {
                throw new ArgumentNullException("documentCollection");
            }

            // ReadDatabaseAsync call is needed to support this API that takes databaseLink as a parameter, to be consistent with CreateDocumentCollectionAsync. We need to construct the collectionLink to make
            // ReadDocumentCollectionAsync call, in case database selfLink got passed to this API. We cannot simply concat the database selfLink with /colls/{collectionId} to get the collectionLink.
            CosmosDatabaseSettings database = await this.ReadDatabaseAsync(databaseLink);

            // Doing a Read before Create will give us better latency for existing collections.
            // Also, in emulator case when you hit the max allowed partition count and you use this API for a collection that already exists,
            // calling Create will throw 503(max capacity reached) even though the intent of this API is to return the collection if it already exists.
            try
            {
                return await this.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(database.Id, documentCollection.Id), null);
            }
            catch (DocumentClientException dce)
            {
                if (dce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            try
            {
                return await this.CreateDocumentCollectionAsync(databaseLink, documentCollection, options);
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the collection and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await this.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(database.Id, documentCollection.Id), null);
        }

        /// <summary>
        /// Restores a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="sourceDocumentCollectionLink">The link to the source <see cref="CosmosContainerSettings"/> object.</param>
        /// <param name="targetDocumentCollection">The target <see cref="CosmosContainerSettings"/> object.</param>
        /// <param name="restoreTime">(optional)The point in time to restore. If null, use the latest restorable time. </param>
        /// <param name="options">(Optional) The <see cref="RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal Task<ResourceResponse<CosmosContainerSettings>> RestoreDocumentCollectionAsync(string sourceDocumentCollectionLink, CosmosContainerSettings targetDocumentCollection, DateTimeOffset? restoreTime = null, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.RestoreDocumentCollectionPrivateAsync(sourceDocumentCollectionLink, targetDocumentCollection, restoreTime, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosContainerSettings>> RestoreDocumentCollectionPrivateAsync(string sourceDocumentCollectionLink, CosmosContainerSettings targetDocumentCollection, DateTimeOffset? restoreTime, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(sourceDocumentCollectionLink))
            {
                throw new ArgumentNullException("sourceDocumentCollectionLink");
            }

            if (targetDocumentCollection == null)
            {
                throw new ArgumentNullException("targetDocumentCollection");
            }

            bool isFeed;
            string resourceTypeString;
            string resourceIdOrFullName;
            bool isNameBased;

            string dbsId;
            string databaseLink = PathsHelper.GetDatabasePath(sourceDocumentCollectionLink);
            if (PathsHelper.TryParsePathSegments(databaseLink, out isFeed, out resourceTypeString, out resourceIdOrFullName, out isNameBased) && isNameBased && !isFeed)
            {
                string[] segments = resourceIdOrFullName.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                dbsId = segments[segments.Length - 1];
            }
            else
            {
                throw new ArgumentNullException("sourceDocumentCollectionLink");
            }

            string sourceCollId;
            if (PathsHelper.TryParsePathSegments(sourceDocumentCollectionLink, out isFeed, out resourceTypeString, out resourceIdOrFullName, out isNameBased) && isNameBased && !isFeed)
            {
                string[] segments = resourceIdOrFullName.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                sourceCollId = segments[segments.Length - 1];
            }
            else
            {
                throw new ArgumentNullException("sourceDocumentCollectionLink");
            }

            this.ValidateResource(targetDocumentCollection);

            if (options == null)
            {
                options = new RequestOptions();
            }
            options.SourceDatabaseId = dbsId;
            options.SourceCollectionId = sourceCollId;
            if (restoreTime.HasValue)
            {
                options.RestorePointInTime = Helpers.ToUnixTime(restoreTime.Value);
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                databaseLink,
                targetDocumentCollection,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                ResourceResponse<CosmosContainerSettings> collection = new ResourceResponse<CosmosContainerSettings>(await this.CreateAsync(request));
                // set the session token
                this.sessionContainer.SetSessionToken(collection.Resource.ResourceId, collection.Resource.AltLink, collection.Headers);
                return collection;
            }
        }

        /// <summary>
        /// Get the status of a collection being restored in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="targetDocumentCollectionLink">The link of the document collection being restored.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal Task<DocumentCollectionRestoreStatus> GetDocumentCollectionRestoreStatusAsync(string targetDocumentCollectionLink)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.GetDocumentCollectionRestoreStatusPrivateAsync(targetDocumentCollectionLink, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<DocumentCollectionRestoreStatus> GetDocumentCollectionRestoreStatusPrivateAsync(string targetDocumentCollectionLink, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(targetDocumentCollectionLink))
            {
                throw new ArgumentNullException("targetDocumentCollectionLink");
            }

            ResourceResponse<CosmosContainerSettings> response = await this.ReadDocumentCollectionPrivateAsync(
                targetDocumentCollectionLink,
                new RequestOptions { PopulateRestoreStatus = true },
                retryPolicyInstance);
            string restoreState = response.ResponseHeaders.Get(WFConstants.BackendHeaders.RestoreState);
            if (restoreState == null)
            {
                restoreState = RestoreState.RestoreCompleted.ToString();
            }

            DocumentCollectionRestoreStatus ret = new DocumentCollectionRestoreStatus()
            {
                State = restoreState
            };

            return ret;
        }

        /// <summary>
        /// Creates a stored procedure as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the collection to create the stored procedure in. E.g. dbs/db_rid/colls/col_rid/</param>
        /// <param name="storedProcedure">The <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> object to create.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>for this request.</param>
        /// <returns>The <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="storedProcedure"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the stored procedure or the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of stored procedures for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Create a new stored procedure called "HelloWorldSproc" that takes in a single param called "name".
        /// StoredProcedure sproc = await client.CreateStoredProcedureAsync(collectionLink, new StoredProcedure
        /// {
        ///    Id = "HelloWorldSproc",
        ///    Body = @"function (name){
        ///                var response = getContext().getResponse();
        ///                response.setBody('Hello ' + name);
        ///             }"
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosStoredProcedureSettings>> CreateStoredProcedureAsync(string collectionLink, CosmosStoredProcedureSettings storedProcedure, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.CreateStoredProcedurePrivateAsync(collectionLink, storedProcedure, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosStoredProcedureSettings>> CreateStoredProcedurePrivateAsync(
            string collectionLink,
            CosmosStoredProcedureSettings storedProcedure,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(collectionLink))
            {
                throw new ArgumentNullException("collectionLink");
            }

            if (storedProcedure == null)
            {
                throw new ArgumentNullException("storedProcedure");
            }

            this.ValidateResource(storedProcedure);

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                collectionLink,
                storedProcedure,
                ResourceType.StoredProcedure,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosStoredProcedureSettings>(await this.CreateAsync(request));
            }
        }

        /// <summary>
        /// Creates a trigger as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> to create the trigger in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="trigger">The <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> object to create.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="trigger"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new trigger or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of triggers for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Create a trigger that validates the contents of a document as it is created and adds a 'timestamp' property if one was not found.
        /// Trigger trig = await client.CreateTriggerAsync(collectionLink, new Trigger
        /// {
        ///     Id = "ValidateDocuments",
        ///     Body = @"function validate() {
        ///                         var context = getContext();
        ///                         var request = context.getRequest();                                                             
        ///                         var documentToCreate = request.getBody();
        ///                         
        ///                         // validate properties
        ///                         if (!('timestamp' in documentToCreate)) {
        ///                             var ts = new Date();
        ///                             documentToCreate['timestamp'] = ts.getTime();
        ///                         }
        ///                         
        ///                         // update the document that will be created
        ///                         request.setBody(documentToCreate);
        ///                       }",
        ///     TriggerType = TriggerType.Pre,
        ///     TriggerOperation = TriggerOperation.Create
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosTriggerSettings>> CreateTriggerAsync(string collectionLink, CosmosTriggerSettings trigger, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.CreateTriggerPrivateAsync(collectionLink, trigger, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosTriggerSettings>> CreateTriggerPrivateAsync(string collectionLink, CosmosTriggerSettings trigger, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(collectionLink))
            {
                throw new ArgumentNullException("collectionLink");
            }

            if (trigger == null)
            {
                throw new ArgumentNullException("trigger");
            }

            this.ValidateResource(trigger);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                collectionLink,
                trigger,
                ResourceType.Trigger,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosTriggerSettings>(await this.CreateAsync(request));
            }
        }

        /// <summary>
        /// Creates a user defined function as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> to create the user defined function in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="function">The <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> object to create.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="function"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new user defined function or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined functions for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Create a user defined function that converts a string to upper case
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction
        /// {
        ///    Id = "ToUpper",
        ///    Body = @"function toUpper(input) {
        ///                        return input.toUpperCase();
        ///                     }",
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> CreateUserDefinedFunctionAsync(string collectionLink, CosmosUserDefinedFunctionSettings function, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.CreateUserDefinedFunctionPrivateAsync(collectionLink, function, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> CreateUserDefinedFunctionPrivateAsync(
            string collectionLink,
            CosmosUserDefinedFunctionSettings function,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(collectionLink))
            {
                throw new ArgumentNullException("collectionLink");
            }

            if (function == null)
            {
                throw new ArgumentNullException("function");
            }

            this.ValidateResource(function);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                collectionLink,
                function,
                ResourceType.UserDefinedFunction,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosUserDefinedFunctionSettings>(await this.CreateAsync(request));
            }
        }

        /// <summary>
        /// Creates a user defined type object as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseLink">The link of the database to create the user defined type in. E.g. dbs/db_rid/ </param>
        /// <param name="userDefinedType">The <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> object to create.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation which contains the created <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> object.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="databaseLink"/> or <paramref name="userDefinedType"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a UserDefinedType are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined type objects for this database. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Create a new user defined type in the specified database
        /// UserDefinedType userDefinedType = await client.CreateUserDefinedTypeAsync(databaseLink, new UserDefinedType { Id = "userDefinedTypeId5" });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<ResourceResponse<UserDefinedType>> CreateUserDefinedTypeAsync(string databaseLink, UserDefinedType userDefinedType, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.CreateUserDefinedTypePrivateAsync(databaseLink, userDefinedType, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<UserDefinedType>> CreateUserDefinedTypePrivateAsync(string databaseLink, UserDefinedType userDefinedType, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentNullException("databaseLink");
            }

            if (userDefinedType == null)
            {
                throw new ArgumentNullException("userDefinedType");
            }

            this.ValidateResource(userDefinedType);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Create,
                databaseLink,
                userDefinedType,
                ResourceType.UserDefinedType,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<UserDefinedType>(await this.CreateAsync(request));
            }
        }

        #endregion

        #region Delete Impl

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="databaseLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/> to delete. E.g. dbs/db_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="databaseLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a database using its selfLink property
        /// //To get the databaseLink you would have to query for the Database, using CreateDatabaseQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteDatabaseAsync(databaseLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosDatabaseSettings>> DeleteDatabaseAsync(string databaseLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.DeleteDatabasePrivateAsync(databaseLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosDatabaseSettings>> DeleteDatabasePrivateAsync(string databaseLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentNullException("databaseLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Delete,
                ResourceType.Database,
                databaseLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosDatabaseSettings>(await this.DeleteAsync(request));
            }
        }

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link of the <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> to delete. E.g. dbs/db_rid/colls/col_rid/docs/doc_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a document using its selfLink property
        /// //To get the documentLink you would have to query for the Document, using CreateDocumentQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteDocumentAsync(documentLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Document>> DeleteDocumentAsync(string documentLink, RequestOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.DeleteDocumentPrivateAsync(documentLink, options, retryPolicyInstance, cancellationToken), retryPolicyInstance, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> DeleteDocumentPrivateAsync(string documentLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentLink))
            {
                throw new ArgumentNullException("documentLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Delete,
                ResourceType.Document,
                documentLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, options);
                request.SerializerSettings = this.GetSerializerSettingsForRequest(options);
                return new ResourceResponse<Document>(await this.DeleteAsync(request, cancellationToken));
            }
        }

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentCollectionLink">The link of the <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> to delete. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentCollectionLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a collection using its selfLink property
        /// //To get the collectionLink you would have to query for the Collection, using CreateDocumentCollectionQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteDocumentCollectionAsync(collectionLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosContainerSettings>> DeleteDocumentCollectionAsync(string documentCollectionLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.DeleteDocumentCollectionPrivateAsync(documentCollectionLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosContainerSettings>> DeleteDocumentCollectionPrivateAsync(string documentCollectionLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentCollectionLink))
            {
                throw new ArgumentNullException("documentCollectionLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Delete,
                ResourceType.Collection,
                documentCollectionLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosContainerSettings>(await this.DeleteAsync(request));
            }
        }

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedureLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> to delete. E.g. dbs/db_rid/colls/col_rid/sprocs/sproc_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a stored procedure using its selfLink property.
        /// //To get the sprocLink you would have to query for the Stored Procedure, using CreateStoredProcedureQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteStoredProcedureAsync(sprocLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosStoredProcedureSettings>> DeleteStoredProcedureAsync(string storedProcedureLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.DeleteStoredProcedurePrivateAsync(storedProcedureLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosStoredProcedureSettings>> DeleteStoredProcedurePrivateAsync(string storedProcedureLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(storedProcedureLink))
            {
                throw new ArgumentNullException("storedProcedureLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Delete,
                ResourceType.StoredProcedure,
                storedProcedureLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosStoredProcedureSettings>(await this.DeleteAsync(request));
            }
        }

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> to delete. E.g. dbs/db_rid/colls/col_rid/triggers/trigger_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a trigger using its selfLink property.
        /// //To get the triggerLink you would have to query for the Trigger, using CreateTriggerQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteTriggerAsync(triggerLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosTriggerSettings>> DeleteTriggerAsync(string triggerLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.DeleteTriggerPrivateAsync(triggerLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosTriggerSettings>> DeleteTriggerPrivateAsync(string triggerLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(triggerLink))
            {
                throw new ArgumentNullException("triggerLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Delete,
                ResourceType.Trigger,
                triggerLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosTriggerSettings>(await this.DeleteAsync(request));
            }
        }

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="functionLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> to delete. E.g. dbs/db_rid/colls/col_rid/udfs/udf_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="functionLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a user defined function using its selfLink property.
        /// //To get the functionLink you would have to query for the User Defined Function, using CreateUserDefinedFunctionQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteUserDefinedFunctionAsync(functionLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> DeleteUserDefinedFunctionAsync(string functionLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.DeleteUserDefinedFunctionPrivateAsync(functionLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> DeleteUserDefinedFunctionPrivateAsync(string functionLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(functionLink))
            {
                throw new ArgumentNullException("functionLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Delete,
                ResourceType.UserDefinedFunction,
                functionLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosUserDefinedFunctionSettings>(await this.DeleteAsync(request));
            }
        }

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Cosmos.Internal.Conflict"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="conflictLink">The link of the <see cref="Microsoft.Azure.Cosmos.Internal.Conflict"/> to delete. E.g. dbs/db_rid/colls/coll_rid/conflicts/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conflictLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a conflict using its selfLink property.
        /// //To get the conflictLink you would have to query for the Conflict object, using CreateConflictQuery(), and then refer to its .SelfLink property
        /// await client.DeleteConflictAsync(conflictLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Conflict>> DeleteConflictAsync(string conflictLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.DeleteConflictPrivateAsync(conflictLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<Conflict>> DeleteConflictPrivateAsync(string conflictLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            this.ThrowIfDisposed();

            if (string.IsNullOrEmpty(conflictLink))
            {
                throw new ArgumentNullException("conflictLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Delete,
                ResourceType.Conflict,
                conflictLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, options);
                return new ResourceResponse<Conflict>(await this.DeleteAsync(request));
            }
        }
        #endregion

        #region Replace Impl

        /// <summary>
        /// Replaces a document collection in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentCollection">the updated document collection.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> containing the updated resource record.
        /// </returns>
        public Task<ResourceResponse<CosmosContainerSettings>> ReplaceDocumentCollectionAsync(CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceDocumentCollectionPrivateAsync(documentCollection, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosContainerSettings>> ReplaceDocumentCollectionPrivateAsync(
            CosmosContainerSettings documentCollection,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance,
            string altLink = null)
        {
            await this.EnsureValidClientAsync();

            if (documentCollection == null)
            {
                throw new ArgumentNullException("documentCollection");
            }

            this.ValidateResource(documentCollection);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace,
                altLink ?? this.GetLinkForRouting(documentCollection),
                documentCollection,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                ResourceResponse<CosmosContainerSettings> collection = new ResourceResponse<CosmosContainerSettings>(await this.UpdateAsync(request));
                // set the session token
                if (collection.Resource != null)
                {
                    this.sessionContainer.SetSessionToken(collection.Resource.ResourceId, collection.Resource.AltLink, collection.Headers);
                }
                return collection;
            }
        }

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link of the document to be updated. E.g. dbs/db_rid/colls/col_rid/docs/doc_rid/ </param>
        /// <param name="document">The updated <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="documentLink"/> or <paramref name="document"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// In this example, instead of using a strongly typed <see cref="Document"/>, we will work with our own POCO object and not rely on the dynamic nature of the Document class.
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyPoco
        /// {
        ///     public string Id {get; set;}
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// //Get the doc back as a Document so you have access to doc.SelfLink
        /// Document doc = client.CreateDocumentQuery<Document>(collectionLink)
        ///                        .Where(r => r.Id == "doc id")
        ///                        .AsEnumerable()
        ///                        .SingleOrDefault();
        ///
        /// //Now dynamically cast doc back to your MyPoco
        /// MyPoco poco = (dynamic)doc;
        ///
        /// //Update some properties of the poco object
        /// poco.MyProperty = "updated value";
        ///
        /// //Now persist these changes to the database using doc.SelLink and the update poco object
        /// Document updated = await client.ReplaceDocumentAsync(doc.SelfLink, poco);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Document>> ReplaceDocumentAsync(string documentLink, object document, RequestOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            // This call is to just run ReplaceDocumentInlineAsync in a SynchronizationContext aware environment
            return TaskHelper.InlineIfPossible(() => ReplaceDocumentInlineAsync(documentLink, document, options, cancellationToken), null, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> ReplaceDocumentInlineAsync(string documentLink, object document, RequestOptions options, CancellationToken cancellationToken)
        {
            IDocumentClientRetryPolicy requestRetryPolicy = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            if (options == null || options.PartitionKey == null)
            {
                requestRetryPolicy = new PartitionKeyMismatchRetryPolicy(await this.GetCollectionCacheAsync(), requestRetryPolicy);
            }

            return await TaskHelper.InlineIfPossible(() => this.ReplaceDocumentPrivateAsync(documentLink, document, options, requestRetryPolicy, cancellationToken), requestRetryPolicy, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> ReplaceDocumentPrivateAsync(string documentLink, object document, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentLink))
            {
                throw new ArgumentNullException("documentLink");
            }

            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            Document typedDocument = Document.FromObject(document, this.GetSerializerSettingsForRequest(options));
            this.ValidateResource(typedDocument);
            return await this.ReplaceDocumentPrivateAsync(documentLink, typedDocument, options, retryPolicyInstance, cancellationToken);
        }

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="document">The updated <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="document"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// This example uses <see cref="Document"/> and takes advantage of the fact that it is a dynamic object and uses SetProperty to dynamically update properties on the document
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the Document to be updated
        /// Document doc = client.CreateDocumentQuery<Document>(collectionLink)
        ///                             .Where(r => r.Id == "doc id")
        ///                             .AsEnumerable()
        ///                             .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// doc.SetPropertyValue("MyProperty", "updated value");
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// Document updated = await client.ReplaceDocumentAsync(doc);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Document>> ReplaceDocumentAsync(Document document, RequestOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceDocumentPrivateAsync(this.GetLinkForRouting(document), document, options, retryPolicyInstance, cancellationToken), retryPolicyInstance, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> ReplaceDocumentPrivateAsync(string documentLink, Document document, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            this.ValidateResource(document);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace,
                documentLink,
                document,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, document, options);
                request.SerializerSettings = this.GetSerializerSettingsForRequest(options);
                return new ResourceResponse<Document>(await this.UpdateAsync(request, cancellationToken));
            }
        }

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedure">The updated <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedure"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// StoredProcedure sproc = client.CreateStoredProcedureQuery(sprocsLink)
        ///                                  .Where(r => r.Id == "sproc id")
        ///                                  .AsEnumerable()
        ///                                  .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// sproc.Body = "function () {new javascript body for sproc}";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// StoredProcedure updated = await client.ReplaceStoredProcedureAsync(sproc);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosStoredProcedureSettings>> ReplaceStoredProcedureAsync(CosmosStoredProcedureSettings storedProcedure, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceStoredProcedurePrivateAsync(storedProcedure, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosStoredProcedureSettings>> ReplaceStoredProcedurePrivateAsync(
            CosmosStoredProcedureSettings storedProcedure,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance,
            string altLink = null)
        {
            await this.EnsureValidClientAsync();

            if (storedProcedure == null)
            {
                throw new ArgumentNullException("storedProcedure");
            }

            this.ValidateResource(storedProcedure);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace,
                altLink ?? this.GetLinkForRouting(storedProcedure),
                storedProcedure,
                ResourceType.StoredProcedure,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosStoredProcedureSettings>(await this.UpdateAsync(request));
            }
        }

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="trigger">The updated <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="trigger"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// Trigger trigger = client.CreateTriggerQuery(sprocsLink)
        ///                               .Where(r => r.Id == "trigger id")
        ///                               .AsEnumerable()
        ///                               .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// trigger.Body = "function () {new javascript body for trigger}";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// Trigger updated = await client.ReplaceTriggerAsync(sproc);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosTriggerSettings>> ReplaceTriggerAsync(CosmosTriggerSettings trigger, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceTriggerPrivateAsync(trigger, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosTriggerSettings>> ReplaceTriggerPrivateAsync(CosmosTriggerSettings trigger, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance, string altLink = null)
        {
            await this.EnsureValidClientAsync();

            if (trigger == null)
            {
                throw new ArgumentNullException("trigger");
            }

            this.ValidateResource(trigger);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace,
                altLink ?? this.GetLinkForRouting(trigger),
                trigger,
                ResourceType.Trigger,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosTriggerSettings>(await this.UpdateAsync(request));
            }
        }

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="function">The updated <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="function"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(functionsLink)
        ///                                     .Where(r => r.Id == "udf id")
        ///                                     .AsEnumerable()
        ///                                     .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// udf.Body = "function () {new javascript body for udf}";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// UserDefinedFunction updated = await client.ReplaceUserDefinedFunctionAsync(udf);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> ReplaceUserDefinedFunctionAsync(CosmosUserDefinedFunctionSettings function, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceUserDefinedFunctionPrivateAsync(function, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> ReplaceUserDefinedFunctionPrivateAsync(
            CosmosUserDefinedFunctionSettings function,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance,
            string altLink = null)
        {
            await this.EnsureValidClientAsync();

            if (function == null)
            {
                throw new ArgumentNullException("function");
            }

            this.ValidateResource(function);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace,
                altLink ?? this.GetLinkForRouting(function),
                function,
                ResourceType.UserDefinedFunction,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosUserDefinedFunctionSettings>(await this.UpdateAsync(request));
            }
        }

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Cosmos.Internal.Offer"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="offer">The updated <see cref="Microsoft.Azure.Cosmos.Internal.Offer"/> to replace the existing resource with.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Offer"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="offer"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// Offer offer = client.CreateOfferQuery()
        ///                          .Where(r => r.ResourceLink == "collection selfLink")
        ///                          .AsEnumerable()
        ///                          .SingleOrDefault();
        ///
        /// //Change the user mode to All
        /// offer.OfferType = "S3";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// Offer updated = await client.ReplaceOfferAsync(offer);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Offer>> ReplaceOfferAsync(Offer offer)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceOfferPrivateAsync(offer, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<Offer>> ReplaceOfferPrivateAsync(Offer offer, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (offer == null)
            {
                throw new ArgumentNullException("offer");
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace,
                offer.SelfLink,
                offer,
                ResourceType.Offer,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<Offer>(await this.UpdateAsync(request), OfferTypeResolver.ResponseOfferTypeResolver);
            }
        }

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedType">The updated <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedType"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// UserDefinedType userDefinedType = client.CreateUserDefinedTypeQuery(userDefinedTypesLink)
        ///                          .Where(r => r.Id == "user defined type id")
        ///                          .AsEnumerable()
        ///                          .SingleOrDefault();
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// UserDefinedType updated = await client.ReplaceUserDefinedTypeAsync(userDefinedType);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<ResourceResponse<UserDefinedType>> ReplaceUserDefinedTypeAsync(UserDefinedType userDefinedType, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceUserDefinedTypePrivateAsync(userDefinedType, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<UserDefinedType>> ReplaceUserDefinedTypePrivateAsync(UserDefinedType userDefinedType, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance, string altLink = null)
        {
            await this.EnsureValidClientAsync();

            if (userDefinedType == null)
            {
                throw new ArgumentNullException("userDefinedType");
            }

            this.ValidateResource(userDefinedType);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Replace,
                altLink ?? this.GetLinkForRouting(userDefinedType),
                userDefinedType,
                ResourceType.UserDefinedType,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<UserDefinedType>(await this.UpdateAsync(request));
            }
        }

        #endregion

        #region Read Impl
        /// <summary>
        /// Reads a <see cref="CosmosDatabaseSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="databaseLink">The link of the Database resource to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosDatabase"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="databaseLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Database resource where
        /// // - database_id is the ID property of the Database resource you wish to read.
        /// var dbLink = "/dbs/database_id";
        /// Database database = await client.ReadDatabaseAsync(dbLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the Database if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="databaseLink"/> is always "/dbs/{db identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<CosmosDatabaseSettings>> ReadDatabaseAsync(string databaseLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReadDatabasePrivateAsync(databaseLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosDatabaseSettings>> ReadDatabasePrivateAsync(string databaseLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentNullException("databaseLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Database,
                databaseLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosDatabaseSettings>(await this.ReadAsync(request));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link for the document to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //This reads a document record from a database & collection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - document_id is the ID of the document resource
        /// var docLink = "dbs/sample_database/colls/sample_collection/docs/document_id";
        /// Document doc = await client.ReadDocumentAsync(docLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the Document if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="documentLink"/> is always "dbs/{db identifier}/colls/{coll identifier}/docs/{doc identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<Document>> ReadDocumentAsync(string documentLink, RequestOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadDocumentPrivateAsync(documentLink, options, retryPolicyInstance, cancellationToken), retryPolicyInstance, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> ReadDocumentPrivateAsync(string documentLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentLink))
            {
                throw new ArgumentNullException("documentLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                documentLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, options);
                request.SerializerSettings = this.GetSerializerSettingsForRequest(options);
                return new ResourceResponse<Document>(await this.ReadAsync(request, cancellationToken));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> as a generic type T from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link for the document to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.DocumentResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //This reads a document record from a database & collection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - document_id is the ID of the document resource
        /// var docLink = "dbs/sample_database/colls/sample_collection/docs/document_id";
        /// Customer customer = await client.ReadDocumentAsync<Customer>(docLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the Document if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="documentLink"/> is always "dbs/{db identifier}/colls/{coll identifier}/docs/{doc identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.DocumentResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<DocumentResponse<T>> ReadDocumentAsync<T>(string documentLink, RequestOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadDocumentPrivateAsync<T>(documentLink, options, retryPolicyInstance, cancellationToken), retryPolicyInstance, cancellationToken);
        }

        private async Task<DocumentResponse<T>> ReadDocumentPrivateAsync<T>(string documentLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentLink))
            {
                throw new ArgumentNullException("documentLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                documentLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, options);
                request.SerializerSettings = this.GetSerializerSettingsForRequest(options);
                return new DocumentResponse<T>(await this.ReadAsync(request, cancellationToken), this.GetSerializerSettingsForRequest(options));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentCollectionLink">The link for the CosmosContainerSettings to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentCollectionLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //This reads a DocumentCollection record from a database where
        /// // - sample_database is the ID of the database
        /// // - collection_id is the ID of the collection resource to be read
        /// var collLink = "/dbs/sample_database/colls/collection_id";
        /// DocumentCollection coll = await client.ReadDocumentCollectionAsync(collLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the DocumentCollection if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="documentCollectionLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<CosmosContainerSettings>> ReadDocumentCollectionAsync(string documentCollectionLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadDocumentCollectionPrivateAsync(documentCollectionLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosContainerSettings>> ReadDocumentCollectionPrivateAsync(
            string documentCollectionLink,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentCollectionLink))
            {
                throw new ArgumentNullException("documentCollectionLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Collection,
                documentCollectionLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosContainerSettings>(await this.ReadAsync(request));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedureLink">The link of the stored procedure to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a StoredProcedure from a Database and DocumentCollection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - sproc_id is the ID of the stored procedure to be read
        /// var sprocLink = "/dbs/sample_database/colls/sample_collection/sprocs/sproc_id";
        /// StoredProcedure sproc = await client.ReadStoredProcedureAsync(sprocLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the Stored Procedure if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="storedProcedureLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}/sprocs/{sproc identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<CosmosStoredProcedureSettings>> ReadStoredProcedureAsync(string storedProcedureLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this._ReadStoredProcedureAsync(storedProcedureLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosStoredProcedureSettings>> _ReadStoredProcedureAsync(string storedProcedureLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(storedProcedureLink))
            {
                throw new ArgumentNullException("storedProcedureLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.StoredProcedure,
                storedProcedureLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosStoredProcedureSettings>(await this.ReadAsync(request));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerLink">The link to the CosmosTriggerSettings to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Trigger from a Database and DocumentCollection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - trigger_id is the ID of the trigger to be read
        /// var triggerLink = "/dbs/sample_database/colls/sample_collection/triggers/trigger_id";
        /// Trigger trigger = await client.ReadTriggerAsync(triggerLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the Trigger if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="triggerLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}/triggers/{trigger identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<CosmosTriggerSettings>> ReadTriggerAsync(string triggerLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadTriggerPrivateAsync(triggerLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosTriggerSettings>> ReadTriggerPrivateAsync(string triggerLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(triggerLink))
            {
                throw new ArgumentNullException("triggerLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Trigger,
                triggerLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosTriggerSettings>(await this.ReadAsync(request));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="functionLink">The link to the User Defined Function to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="functionLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a User Defined Function from a Database and DocumentCollection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - udf_id is the ID of the user-defined function to be read
        /// var udfLink = "/dbs/sample_database/colls/sample_collection/udfs/udf_id";
        /// UserDefinedFunction udf = await client.ReadUserDefinedFunctionAsync(udfLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the User Defined Function if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="functionLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}/udfs/{udf identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> ReadUserDefinedFunctionAsync(string functionLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadUserDefinedFunctionPrivateAsync(functionLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> ReadUserDefinedFunctionPrivateAsync(string functionLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(functionLink))
            {
                throw new ArgumentNullException("functionLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.UserDefinedFunction,
                functionLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosUserDefinedFunctionSettings>(await this.ReadAsync(request));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.Internal.Conflict"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="conflictLink">The link to the Conflict to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Conflict"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conflictLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Conflict resource from a Database
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - conflict_id is the ID of the conflict to be read
        /// var conflictLink = "/dbs/sample_database/colls/sample_collection/conflicts/conflict_id";
        /// Conflict conflict = await client.ReadConflictAsync(conflictLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the Conflict if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="conflictLink"/> is always "/dbs/{db identifier}/colls/{collectioon identifier}/conflicts/{conflict identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<Conflict>> ReadConflictAsync(string conflictLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadConflictPrivateAsync(conflictLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<Conflict>> ReadConflictPrivateAsync(string conflictLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            this.ThrowIfDisposed();

            if (string.IsNullOrEmpty(conflictLink))
            {
                throw new ArgumentNullException("conflictLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Conflict,
                conflictLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, options);
                return new ResourceResponse<Conflict>(await this.ReadAsync(request));
            }
        }

        /// <summary>
        /// Reads an <see cref="Microsoft.Azure.Cosmos.Internal.Offer"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="offerLink">The link to the Offer to be read.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Offer"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="offerLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads an Offer resource from a Database
        /// // - offer_id is the ID of the conflict to be read
        /// var offerLink = "/offers/offer_id";
        /// Offer offer = await client.ReadOfferAsync(offerLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// For an Offer, id is always generated internally by the system when the linked resource is created. id and _rid are always the same for Offer.
        /// </para>
        /// <para>
        /// The format for <paramref name="offerLink"/> is always "/offers/{offer identifier}"
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        public Task<ResourceResponse<Offer>> ReadOfferAsync(string offerLink)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadOfferPrivateAsync(offerLink, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<Offer>> ReadOfferPrivateAsync(string offerLink, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(offerLink))
            {
                throw new ArgumentNullException("offerLink");
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Offer,
                offerLink,
                null,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<Offer>(await this.ReadAsync(request), OfferTypeResolver.ResponseOfferTypeResolver);
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.Internal.Schema"/> as an asynchronous operation.
        /// </summary>
        /// <param name="documentSchemaLink">The link for the schema to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Document"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentSchemaLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when reading a Schema are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //This reads a schema record from a database & collection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - schema_id is the ID of the document resource
        /// var docLink = "/dbs/sample_database/colls/sample_collection/schemas/schemas_id";
        /// Schema schema = await client.ReadSchemaAsync(docLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the Document if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="documentSchemaLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}/schema/{schema identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Schema"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        internal Task<ResourceResponse<Schema>> ReadSchemaAsync(string documentSchemaLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadSchemaPrivateAsync(documentSchemaLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<Schema>> ReadSchemaPrivateAsync(string documentSchemaLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentSchemaLink))
            {
                throw new ArgumentNullException("documentSchemaLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Schema,
                documentSchemaLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, options);
                request.SerializerSettings = this.GetSerializerSettingsForRequest(options);
                return new ResourceResponse<Schema>(await this.ReadAsync(request));
            }
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedTypeLink">The link to the UserDefinedType resource to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedTypeLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a UserDefinedType are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a User resource from a Database
        /// // - sample_database is the ID of the database
        /// // - userDefinedType_id is the ID of the user defined type to be read
        /// var userDefinedTypeLink = "/dbs/sample_database/udts/userDefinedType_id";
        /// UserDefinedType userDefinedType = await client.ReadUserDefinedTypeAsync(userDefinedTypeLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown user defined type ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Microsoft.Azure.Cosmos.CosmosResource.SelfLink"/> property of the UserDefinedType if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="userDefinedTypeLink"/> is always "/dbs/{db identifier}/udts/{user defined type identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        internal Task<ResourceResponse<UserDefinedType>> ReadUserDefinedTypeAsync(string userDefinedTypeLink, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadUserDefinedTypePrivateAsync(userDefinedTypeLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<UserDefinedType>> ReadUserDefinedTypePrivateAsync(string userDefinedTypeLink, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(userDefinedTypeLink))
            {
                throw new ArgumentNullException("userDefinedTypeLink");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.UserDefinedType,
                userDefinedTypeLink,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<UserDefinedType>(await this.ReadAsync(request));
            }
        }

        #endregion

        #region ReadFeed Impl

        /// <summary>
        /// Reads the feed (sequence) of <see cref="CosmosDatabaseSettings"/> for a database account from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<Database> response = await client.ReadDatabaseFeedAsync(new FeedOptions
        ///                                                                 {
        ///                                                                     MaxItemCount = 10,
        ///                                                                     RequestContinuation = continuation
        ///                                                                 });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<CosmosDatabaseSettings>> ReadDatabaseFeedAsync(FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadDatabaseFeedPrivateAsync(options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<CosmosDatabaseSettings>> ReadDatabaseFeedPrivateAsync(FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            return await this.CreateDatabaseFeedReader(options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.Internal.PartitionKeyRange"/> for a database account from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="partitionKeyRangesOrCollectionLink">The link of the resources to be read, or owner collection link, SelfLink or AltLink. E.g. /dbs/db_rid/colls/coll_rid/pkranges</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedResponse<PartitionKeyRange> response = null;
        /// List<string> ids = new List<string>();
        /// do
        /// {
        ///     response = await client.ReadPartitionKeyRangeFeedAsync(collection.SelfLink, new FeedOptions { MaxItemCount = 1000 });
        ///     foreach (var item in response)
        ///     {
        ///         ids.Add(item.Id);
        ///     }
        /// }
        /// while (!string.IsNullOrEmpty(response.ResponseContinuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.PartitionKeyRange"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.FeedOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.FeedResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<PartitionKeyRange>> ReadPartitionKeyRangeFeedAsync(string partitionKeyRangesOrCollectionLink, FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadPartitionKeyRangeFeedPrivateAsync(partitionKeyRangesOrCollectionLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<PartitionKeyRange>> ReadPartitionKeyRangeFeedPrivateAsync(string partitionKeyRangesLink, FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(partitionKeyRangesLink))
            {
                throw new ArgumentNullException("partitionKeyRangesLink");
            }

            return await this.CreatePartitionKeyRangeFeedReader(partitionKeyRangesLink, options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> for a database from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="collectionsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="collectionsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<DocumentCollection> response = await client.ReadDocumentCollectionFeedAsync("/dbs/db_rid/colls/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<CosmosContainerSettings>> ReadDocumentCollectionFeedAsync(string collectionsLink, FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadDocumentCollectionFeedPrivateAsync(collectionsLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<CosmosContainerSettings>> ReadDocumentCollectionFeedPrivateAsync(string collectionsLink, FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(collectionsLink))
            {
                throw new ArgumentNullException("collectionsLink");
            }

            return await this.CreateDocumentCollectionFeedReader(collectionsLink, options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProceduresLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/col_rid/sprocs/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProceduresLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<StoredProcedure> response = await client.ReadStoredProcedureFeedAsync("/dbs/db_rid/colls/col_rid/sprocs/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<CosmosStoredProcedureSettings>> ReadStoredProcedureFeedAsync(string storedProceduresLink, FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadStoredProcedureFeedPrivateAsync(storedProceduresLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<CosmosStoredProcedureSettings>> ReadStoredProcedureFeedPrivateAsync(string storedProceduresLink, FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(storedProceduresLink))
            {
                throw new ArgumentNullException("storedProceduresLink");
            }

            return await this.CreateStoredProcedureFeedReader(storedProceduresLink, options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="triggersLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/col_rid/triggers/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggersLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<Trigger> response = await client.ReadTriggerFeedAsync("/dbs/db_rid/colls/col_rid/triggers/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>

        public Task<FeedResponse<CosmosTriggerSettings>> ReadTriggerFeedAsync(string triggersLink, FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadTriggerFeedPrivateAsync(triggersLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<CosmosTriggerSettings>> ReadTriggerFeedPrivateAsync(string triggersLink, FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(triggersLink))
            {
                throw new ArgumentNullException("triggersLink");
            }

            return await this.CreateTriggerFeedReader(triggersLink, options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedFunctionsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/col_rid/udfs/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedFunctionsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<UserDefinedFunction> response = await client.ReadUserDefinedFunctionFeedAsync("/dbs/db_rid/colls/col_rid/udfs/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<CosmosUserDefinedFunctionSettings>> ReadUserDefinedFunctionFeedAsync(string userDefinedFunctionsLink, FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadUserDefinedFunctionFeedPrivateAsync(userDefinedFunctionsLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<CosmosUserDefinedFunctionSettings>> ReadUserDefinedFunctionFeedPrivateAsync(string userDefinedFunctionsLink, FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(userDefinedFunctionsLink))
            {
                throw new ArgumentNullException("userDefinedFunctionsLink");
            }

            return await this.CreateUserDefinedFunctionFeedReader(userDefinedFunctionsLink, options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of documents for a specified collection from the Azure Cosmos DB service.
        /// This takes returns a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which will contain an enumerable list of dynamic objects.
        /// </summary>
        /// <param name="documentsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/coll_rid/docs/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> containing dynamic objects representing the items in the feed.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<dynamic> response = await client.ReadDocumentFeedAsync("/dbs/db_rid/colls/coll_rid/docs/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Instead of FeedResponse{Document} this method takes advantage of dynamic objects in .NET. This way a single feed result can contain any kind of Document, or POCO object.
        /// This is important becuse a DocumentCollection can contain different kinds of documents.
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<dynamic>> ReadDocumentFeedAsync(string documentsLink, FeedOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return TaskHelper.InlineIfPossible(() => ReadDocumentFeedInlineAsync(documentsLink, options, cancellationToken), null, cancellationToken);
        }

        private async Task<FeedResponse<dynamic>> ReadDocumentFeedInlineAsync(string documentsLink, FeedOptions options, CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentsLink))
            {
                throw new ArgumentNullException("documentsLink");
            }

            FeedResponse<Document> response = await this.CreateDocumentFeedReader(documentsLink, options).ExecuteNextAsync(cancellationToken);
            return new FeedResponse<dynamic>(response.Cast<dynamic>(), response.Count, response.Headers, response.UseETagAsContinuation, response.QueryMetrics, response.RequestStatistics, responseLengthBytes: response.ResponseLengthBytes);
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.Internal.Conflict"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="conflictsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/coll_rid/conflicts/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Conflict"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conflictsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<Conflict> response = await client.ReadConflictAsync("/dbs/db_rid/colls/coll_rid/conflicts/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<Conflict>> ReadConflictFeedAsync(string conflictsLink, FeedOptions options = null)
        {
            return TaskHelper.InlineIfPossible(() => ReadConflictFeedInlineAsync(conflictsLink, options), null);
        }

        private async Task<FeedResponse<Conflict>> ReadConflictFeedInlineAsync(string conflictsLink, FeedOptions options)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(conflictsLink))
            {
                throw new ArgumentNullException("conflictsLink");
            }

            return await this.CreateConflictFeedReader(conflictsLink, options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.Internal.Offer"/> for a database account from the Azure Cosmos DB service
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Offer"/> containing the read resource record.
        /// </returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<Offer> response = await client.ReadOfferAsync(new FeedOptions
        ///                                                                 {
        ///                                                                     MaxItemCount = 10,
        ///                                                                     RequestContinuation = continuation
        ///                                                                 });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<FeedResponse<Offer>> ReadOffersFeedAsync(FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadOfferFeedPrivateAsync(options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<Offer>> ReadOfferFeedPrivateAsync(FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            return await this.CreateOfferFeedReader(options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.Internal.Schema"/> for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="documentCollectionSchemaLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/coll_rid/schemas </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.Schema"/> containing the read resource record.
        /// </returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<User> response = await client.ReadUserFeedAsync("/dbs/db_rid/colls/coll_rid/schemas",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Schema"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<FeedResponse<Schema>> ReadSchemaFeedAsync(string documentCollectionSchemaLink, FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReadSchemaFeedPrivateAsync(documentCollectionSchemaLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<Schema>> ReadSchemaFeedPrivateAsync(string documentCollectionSchemaLink, FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentCollectionSchemaLink))
            {
                throw new ArgumentNullException("documentCollectionSchemaLink");
            }

            return await this.CreateSchemaFeedReader(documentCollectionSchemaLink, options).ExecuteNextAsync();
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> for a database from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedTypesLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/udts/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedTypesLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a UserDefinedType are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     FeedResponse<UserDefinedType> response = await client.ReadUserDefinedTypeFeedAsync("/dbs/db_rid/udts/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<FeedResponse<UserDefinedType>> ReadUserDefinedTypeFeedAsync(string userDefinedTypesLink, FeedOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(
                () => this.ReadUserDefinedTypeFeedPrivateAsync(userDefinedTypesLink, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<FeedResponse<UserDefinedType>> ReadUserDefinedTypeFeedPrivateAsync(string userDefinedTypesLink, FeedOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(userDefinedTypesLink))
            {
                throw new ArgumentNullException("userDefinedTypesLink");
            }

            return await this.CreateUserDefinedTypeFeedReader(userDefinedTypesLink, options).ExecuteNextAsync();
        }

        #endregion

        #region Stored procs
        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureLink">The link to the stored procedure to execute.</param>
        /// <param name="procedureParams">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Execute a StoredProcedure with ResourceId of "sproc_rid" that takes two "Player" documents, does some stuff, and returns a bool
        /// StoredProcedureResponse<bool> sprocResponse = await client.ExecuteStoredProcedureAsync<bool>(
        ///                                                         "/dbs/db_rid/colls/col_rid/sprocs/sproc_rid/",
        ///                                                         new Player { id="1", name="joe" } ,
        ///                                                         new Player { id="2", name="john" }
        ///                                                     );
        ///
        /// if (sprocResponse.Response) Console.WriteLine("Congrats, the stored procedure did some stuff");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.StoredProcedureResponse{TValue}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(string storedProcedureLink, params dynamic[] procedureParams)
        {
            return this.ExecuteStoredProcedureAsync<TValue>(storedProcedureLink, null, default(CancellationToken), procedureParams);
        }

        /// <summary>
        /// Executes a stored procedure against a partitioned collection in the Azure Cosmos DB service as an asynchronous operation, specifiying a target partition.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureLink">The link to the stored procedure to execute.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="procedureParams">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Execute a StoredProcedure with ResourceId of "sproc_rid" that takes two "Player" documents, does some stuff, and returns a bool
        /// StoredProcedureResponse<bool> sprocResponse = await client.ExecuteStoredProcedureAsync<bool>(
        ///                                                         "/dbs/db_rid/colls/col_rid/sprocs/sproc_rid/",
        ///                                                         new RequestOptions { PartitionKey = new PartitionKey(1) },
        ///                                                         new Player { id="1", name="joe" } ,
        ///                                                         new Player { id="2", name="john" }
        ///                                                     );
        ///
        /// if (sprocResponse.Response) Console.WriteLine("Congrats, the stored procedure did some stuff");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.StoredProcedureResponse{TValue}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(string storedProcedureLink, RequestOptions options, params dynamic[] procedureParams)
        {
            return TaskHelper.InlineIfPossible(() => this.ExecuteStoredProcedurePrivateAsync<TValue>(storedProcedureLink, options, default(CancellationToken), procedureParams), this.ResetSessionTokenRetryPolicy.GetRequestPolicy());
        }

        /// <summary>
        /// Executes a stored procedure against a partitioned collection in the Azure Cosmos DB service as an asynchronous operation, specifiying a target partition.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureLink">The link to the stored procedure to execute.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="procedureParams">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Execute a StoredProcedure with ResourceId of "sproc_rid" that takes two "Player" documents, does some stuff, and returns a bool
        /// StoredProcedureResponse<bool> sprocResponse = await client.ExecuteStoredProcedureAsync<bool>(
        ///                                                         "/dbs/db_rid/colls/col_rid/sprocs/sproc_rid/",
        ///                                                         new RequestOptions { PartitionKey = new PartitionKey(1) },
        ///                                                         new Player { id="1", name="joe" } ,
        ///                                                         new Player { id="2", name="john" }
        ///                                                     );
        ///
        /// if (sprocResponse.Response) Console.WriteLine("Congrats, the stored procedure did some stuff");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.StoredProcedureResponse{TValue}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(string storedProcedureLink, RequestOptions options, CancellationToken cancellationToken, params dynamic[] procedureParams)
        {
            return TaskHelper.InlineIfPossible(() => this.ExecuteStoredProcedurePrivateAsync<TValue>(storedProcedureLink, options, cancellationToken, procedureParams), this.ResetSessionTokenRetryPolicy.GetRequestPolicy(), cancellationToken);
        }

        private async Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedurePrivateAsync<TValue>(string storedProcedureLink, RequestOptions options, CancellationToken cancellationToken, params dynamic[] procedureParams)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(storedProcedureLink))
            {
                throw new ArgumentNullException("storedProcedureLink");
            }

            JsonSerializerSettings serializerSettings = this.GetSerializerSettingsForRequest(options);
            string storedProcedureInput = serializerSettings == null ?
                JsonConvert.SerializeObject(procedureParams) :
                JsonConvert.SerializeObject(procedureParams, serializerSettings);
            using (MemoryStream storedProcedureInputStream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(storedProcedureInputStream))
                {
                    writer.Write(storedProcedureInput);
                    writer.Flush();
                    storedProcedureInputStream.Position = 0;

                    INameValueCollection headers = this.GetRequestHeaders(options);
                    using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                        OperationType.ExecuteJavaScript,
                        ResourceType.StoredProcedure,
                        storedProcedureLink,
                        storedProcedureInputStream,
                        AuthorizationTokenType.PrimaryMasterKey,
                        headers))
                    {
                        request.Headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r");
                        if (options == null || options.PartitionKeyRangeId == null)
                        {
                            await this.AddPartitionKeyInformationAsync(
                                request,
                                options);
                        }

                        request.SerializerSettings = this.GetSerializerSettingsForRequest(options);
                        return new StoredProcedureResponse<TValue>(await this.ExecuteProcedureAsync(request, cancellationToken), this.GetSerializerSettingsForRequest(options));
                    }
                }
            }
        }

        #endregion

        #region Upsert Impl

        /// <summary>
        /// Upserts a database resource as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="database">The specification for the <see cref="CosmosDatabaseSettings"/> to upsert.</param>
        /// <param name="options">(Optional) The <see cref="RequestOptions"/> for the request.</param>
        /// <returns>The <see cref="CosmosDatabaseSettings"/> that was upserted within a task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="database"/> is not set</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Database are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the database object supplied. It is likely that an id was not supplied for the new Database.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosDatabaseSettings"/> with an id matching the id field of <paramref name="database"/> already existed</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// The example below upserts a new <see cref="CosmosDatabaseSettings"/> with an Id property of 'MyDatabase'
        /// This code snippet is intended to be used from within an Asynchronous method as it uses the await keyword
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = await client.UpsertDatabaseAsync(new Database { Id = "MyDatabase" });
        /// }
        /// ]]>
        /// </code>
        ///
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosDatabaseSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<ResourceResponse<CosmosDatabaseSettings>> UpsertDatabaseAsync(CosmosDatabaseSettings database, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.UpsertDatabasePrivateAsync(database, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosDatabaseSettings>> UpsertDatabasePrivateAsync(CosmosDatabaseSettings database, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            this.ValidateResource(database);

            INameValueCollection headers = this.GetRequestHeaders(options);

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Upsert,
                Paths.Databases_Root,
                database,
                ResourceType.Database,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosDatabaseSettings>(await this.UpsertAsync(request));
            }
        }

        /// <summary>
        /// Upserts a Document as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentsFeedOrDatabaseLink">The link of the <see cref="CosmosContainerSettings"/> to upsert the document in. E.g. dbs/db_rid/colls/coll_rid/ </param>
        /// <param name="document">The document object to upsert.</param>
        /// <param name="options">(Optional) Any request options you wish to set. E.g. Specifying a Trigger to execute when creating the document. <see cref="RequestOptions"/></param>
        /// <param name="disableAutomaticIdGeneration">(Optional) Disables the automatic id generation, If this is True the system will throw an exception if the id property is missing from the Document.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The <see cref="Document"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="documentsFeedOrDatabaseLink"/> or <paramref name="document"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the document supplied. It is likely that <paramref name="disableAutomaticIdGeneration"/> was true and an id was not supplied</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This likely means the collection in to which you were trying to upsert the document is full.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Document"/> with an id matching the id field of <paramref name="document"/> already existed</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the <see cref="Document"/> exceeds the current max entity size. Consult documentation for limits and quotas.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// Azure Cosmos DB supports a number of different ways to work with documents. A document can extend <see cref="CosmosResource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyObject : Resource
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyObject { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// A document can be any POCO object that can be serialized to JSON, even if it doesn't extend from <see cref="CosmosResource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyPOCO
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyPOCO { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// A Document can also be a dynamic object
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync("dbs/db_rid/colls/coll_rid/", new { SomeProperty = "A Value" } );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// Upsert a Document and execute a Pre and Post Trigger
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync(
        ///         "dbs/db_rid/colls/coll_rid/",
        ///         new { id = "DOC123213443" },
        ///         new RequestOptions
        ///         {
        ///             PreTriggerInclude = new List<string> { "MyPreTrigger" },
        ///             PostTriggerInclude = new List<string> { "MyPostTrigger" }
        ///         });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Document>> UpsertDocumentAsync(string documentsFeedOrDatabaseLink, object document, RequestOptions options = null, bool disableAutomaticIdGeneration = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            // This call is to just run UpsertDocumentInlineAsync in a SynchronizationContext aware environment
            return TaskHelper.InlineIfPossible(() => UpsertDocumentInlineAsync(documentsFeedOrDatabaseLink, document, options, disableAutomaticIdGeneration, cancellationToken), null, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> UpsertDocumentInlineAsync(string documentsFeedOrDatabaseLink, object document, RequestOptions options, bool disableAutomaticIdGeneration, CancellationToken cancellationToken)
        {
            IDocumentClientRetryPolicy requestRetryPolicy = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            if (options == null || options.PartitionKey == null)
            {
                requestRetryPolicy = new PartitionKeyMismatchRetryPolicy(await this.GetCollectionCacheAsync(), requestRetryPolicy);
            }

            return await TaskHelper.InlineIfPossible(() => this.UpsertDocumentPrivateAsync(
                documentsFeedOrDatabaseLink,
                document,
                options,
                disableAutomaticIdGeneration,
                requestRetryPolicy,
                cancellationToken), requestRetryPolicy, cancellationToken);
        }

        private async Task<ResourceResponse<Document>> UpsertDocumentPrivateAsync(
            string documentCollectionLink,
            object document,
            RequestOptions options,
            bool disableAutomaticIdGeneration,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(documentCollectionLink))
            {
                throw new ArgumentNullException("documentCollectionLink");
            }

            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            INameValueCollection headers = this.GetRequestHeaders(options);
            Document typedDocument = Document.FromObject(document, this.GetSerializerSettingsForRequest(options));
            this.ValidateResource(typedDocument);

            if (string.IsNullOrEmpty(typedDocument.Id) && !disableAutomaticIdGeneration)
            {
                typedDocument.Id = Guid.NewGuid().ToString();
            }

            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Upsert,
                documentCollectionLink,
                typedDocument,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                await this.AddPartitionKeyInformationAsync(request, typedDocument, options);
                request.SerializerSettings = this.GetSerializerSettingsForRequest(options);

                return new ResourceResponse<Document>(await this.UpsertAsync(request, cancellationToken));
            }
        }

        /// <summary>
        /// Upserts a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseLink">The link of the database to upsert the collection in. E.g. dbs/db_rid/</param>
        /// <param name="documentCollection">The <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> object.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/> you wish to provide when creating a Collection. E.g. RequestOptions.OfferThroughput = 400. </param>
        /// <returns>The <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="databaseLink"/> or <paramref name="documentCollection"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new collection.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for collections. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     //Upsert a new collection with an OfferThroughput set to 10000
        ///     //Not passing in RequestOptions.OfferThroughput will result in a collection with the default OfferThroughput set.
        ///     DocumentCollection coll = await client.UpsertDocumentCollectionAsync(databaseLink,
        ///         new DocumentCollection { Id = "My Collection" },
        ///         new RequestOptions { OfferThroughput = 10000} );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<ResourceResponse<CosmosContainerSettings>> UpsertDocumentCollectionAsync(string databaseLink, CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            // To be implemented.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Upserts a stored procedure as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the collection to upsert the stored procedure in. E.g. dbs/db_rid/colls/col_rid/</param>
        /// <param name="storedProcedure">The <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> object to upsert.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>for this request.</param>
        /// <returns>The <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="storedProcedure"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the stored procedure or the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of stored procedures for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/> you tried to upsert was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Upsert a new stored procedure called "HelloWorldSproc" that takes in a single param called "name".
        /// StoredProcedure sproc = await client.UpsertStoredProcedureAsync(collectionLink, new StoredProcedure
        /// {
        ///    Id = "HelloWorldSproc",
        ///    Body = @"function (name){
        ///                var response = getContext().getResponse();
        ///                response.setBody('Hello ' + name);
        ///             }"
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosStoredProcedureSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosStoredProcedureSettings>> UpsertStoredProcedureAsync(string collectionLink, CosmosStoredProcedureSettings storedProcedure, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.UpsertStoredProcedurePrivateAsync(collectionLink, storedProcedure, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosStoredProcedureSettings>> UpsertStoredProcedurePrivateAsync(
            string collectionLink,
            CosmosStoredProcedureSettings storedProcedure,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(collectionLink))
            {
                throw new ArgumentNullException("collectionLink");
            }

            if (storedProcedure == null)
            {
                throw new ArgumentNullException("storedProcedure");
            }

            this.ValidateResource(storedProcedure);

            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Upsert,
                collectionLink,
                storedProcedure,
                ResourceType.StoredProcedure,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosStoredProcedureSettings>(await this.UpsertAsync(request));
            }
        }

        /// <summary>
        /// Upserts a trigger as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> to upsert the trigger in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="trigger">The <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> object to upsert.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="trigger"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new trigger or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of triggers for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/> you tried to upsert was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Upsert a trigger that validates the contents of a document as it is created and adds a 'timestamp' property if one was not found.
        /// Trigger trig = await client.UpsertTriggerAsync(collectionLink, new Trigger
        /// {
        ///     Id = "ValidateDocuments",
        ///     Body = @"function validate() {
        ///                         var context = getContext();
        ///                         var request = context.getRequest();                                                             
        ///                         var documentToCreate = request.getBody();
        ///                         
        ///                         // validate properties
        ///                         if (!('timestamp' in documentToCreate)) {
        ///                             var ts = new Date();
        ///                             documentToCreate['timestamp'] = ts.getTime();
        ///                         }
        ///                         
        ///                         // update the document that will be created
        ///                         request.setBody(documentToCreate);
        ///                       }",
        ///     TriggerType = TriggerType.Pre,
        ///     TriggerOperation = TriggerOperation.Create
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosTriggerSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosTriggerSettings>> UpsertTriggerAsync(string collectionLink, CosmosTriggerSettings trigger, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.UpsertTriggerPrivateAsync(collectionLink, trigger, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosTriggerSettings>> UpsertTriggerPrivateAsync(string collectionLink, CosmosTriggerSettings trigger, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(collectionLink))
            {
                throw new ArgumentNullException("collectionLink");
            }

            if (trigger == null)
            {
                throw new ArgumentNullException("trigger");
            }

            this.ValidateResource(trigger);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Upsert,
                collectionLink,
                trigger,
                ResourceType.Trigger,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosTriggerSettings>(await this.UpsertAsync(request));
            }
        }

        /// <summary>
        /// Upserts a user defined function as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="Microsoft.Azure.Cosmos.CosmosContainerSettings"/> to upsert the user defined function in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="function">The <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> object to upsert.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Cosmos.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="function"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new user defined function or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined functions for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/> you tried to upsert was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Upsert a user defined function that converts a string to upper case
        /// UserDefinedFunction udf = client.UpsertUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction
        /// {
        ///    Id = "ToUpper",
        ///    Body = @"function toUpper(input) {
        ///                        return input.toUpperCase();
        ///                     }",
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosUserDefinedFunctionSettings"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> UpsertUserDefinedFunctionAsync(string collectionLink, CosmosUserDefinedFunctionSettings function, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.UpsertUserDefinedFunctionPrivateAsync(collectionLink, function, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> UpsertUserDefinedFunctionPrivateAsync(
            string collectionLink,
            CosmosUserDefinedFunctionSettings function,
            RequestOptions options,
            IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(collectionLink))
            {
                throw new ArgumentNullException("collectionLink");
            }

            if (function == null)
            {
                throw new ArgumentNullException("function");
            }

            this.ValidateResource(function);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Upsert,
                collectionLink,
                function,
                ResourceType.UserDefinedFunction,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<CosmosUserDefinedFunctionSettings>(await this.UpsertAsync(request));
            }
        }

        /// <summary>
        /// Upserts a user defined type object in the Azure Cosmos DB service as an asychronous operation.
        /// </summary>
        /// <param name="databaseLink">The link of the database to upsert the user defined type in. E.g. dbs/db_rid/ </param>
        /// <param name="userDefinedType">The <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> object to upsert.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation which contains the upserted <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> object.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="databaseLink"/> or <paramref name="userDefinedType"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined type objects for this database. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Upsert a new user defined type in the specified database
        /// UserDefinedType userDefinedType = await client.UpsertUserDefinedTypeAsync(databaseLink, new UserDefinedType { Id = "userDefinedTypeId5" });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Internal.UserDefinedType"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<ResourceResponse<UserDefinedType>> UpsertUserDefinedTypeAsync(string databaseLink, UserDefinedType userDefinedType, RequestOptions options = null)
        {
            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.UpsertUserDefinedTypePrivateAsync(databaseLink, userDefinedType, options, retryPolicyInstance), retryPolicyInstance);
        }

        private async Task<ResourceResponse<UserDefinedType>> UpsertUserDefinedTypePrivateAsync(string databaseLink, UserDefinedType userDefinedType, RequestOptions options, IDocumentClientRetryPolicy retryPolicyInstance)
        {
            await this.EnsureValidClientAsync();

            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentNullException("databaseLink");
            }

            if (userDefinedType == null)
            {
                throw new ArgumentNullException("userDefinedType");
            }

            this.ValidateResource(userDefinedType);
            INameValueCollection headers = this.GetRequestHeaders(options);
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Upsert,
                databaseLink,
                userDefinedType,
                ResourceType.UserDefinedType,
                AuthorizationTokenType.PrimaryMasterKey,
                headers,
                SerializationFormattingPolicy.None))
            {
                if (retryPolicyInstance != null)
                {
                    retryPolicyInstance.OnBeforeSendRequest(request);
                }

                return new ResourceResponse<UserDefinedType>(await this.UpsertAsync(request));
            }
        }
        #endregion

        #region IAuthorizationTokenProvider

        private bool TryGetResourceToken(string resourceAddress, PartitionKeyInternal partitionKey, out string resourceToken)
        {
            resourceToken = null;
            List<PartitionKeyAndResourceTokenPair> partitionKeyTokenPairs;
            bool isPartitionKeyAndTokenPairListAvailable = this.resourceTokens.TryGetValue(resourceAddress, out partitionKeyTokenPairs);
            if (isPartitionKeyAndTokenPairListAvailable)
            {
                var partitionKeyTokenPair = partitionKeyTokenPairs.FirstOrDefault(pair => pair.PartitionKey.Contains(partitionKey));
                if (partitionKeyTokenPair != null)
                {
                    resourceToken = partitionKeyTokenPair.ResourceToken;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="resourceAddress"></param>
        /// <param name="resourceType"></param>
        /// <param name="requestVerb"></param>
        /// <param name="headers"></param>
        /// <param name="tokenType">unused, use token based upon what is passed in constructor</param>
        /// <returns></returns>
        string IAuthorizationTokenProvider.GetUserAuthorizationToken(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType) /* unused, use token based upon what is passed in constructor */
        {
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
                        requestVerb, resourceAddress, resourceType, headers, this.authKeyHashFunction);
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

        Task<string> IAuthorizationTokenProvider.GetSystemAuthorizationTokenAsync(
            string federationName,
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType)
        {
            return Task.FromResult(
                ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(resourceAddress, resourceType, requestVerb, headers, tokenType));
        }

        #endregion

        #region Core Implementation
        internal async Task<DocumentServiceResponse> CreateAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string authorization = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                HttpConstants.HttpMethods.Post, request.Headers, AuthorizationTokenType.PrimaryMasterKey);
            request.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;
            request.Headers.AuthorizationToken = authorization;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                return await storeProxy.ProcessMessageAsync(request, cancellationToken);
            }
        }
        internal async Task<DocumentServiceResponse> UpdateAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string authorization = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                HttpConstants.HttpMethods.Put,
                request.Headers,
                AuthorizationTokenType.PrimaryMasterKey);

            request.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;
            request.Headers.AuthorizationToken = authorization;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                return await storeProxy.ProcessMessageAsync(request, cancellationToken);
            }
        }

        internal async Task<DocumentServiceResponse> ReadAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string authorizationToken = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                HttpConstants.HttpMethods.Get, request.Headers,
                AuthorizationTokenType.PrimaryMasterKey);

            request.Headers[HttpConstants.HttpHeaders.Authorization] = authorizationToken;
            request.Headers.AuthorizationToken = authorizationToken;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                return await storeProxy.ProcessMessageAsync(request, cancellationToken);
            }
        }

        internal async Task<DocumentServiceResponse> ReadFeedAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string authorizationToken = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                HttpConstants.HttpMethods.Get, request.Headers, AuthorizationTokenType.PrimaryMasterKey);
            request.Headers[HttpConstants.HttpHeaders.Authorization] = authorizationToken;
            request.Headers.AuthorizationToken = authorizationToken;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                return await storeProxy.ProcessMessageAsync(request, cancellationToken);
            }
        }

        internal async Task<DocumentServiceResponse> DeleteAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string authorizationToken = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(request.ResourceAddress,
                    PathsHelper.GetResourcePath(request.ResourceType),
                    HttpConstants.HttpMethods.Delete,
                    request.Headers,
                    AuthorizationTokenType.PrimaryMasterKey);

            request.Headers[HttpConstants.HttpHeaders.Authorization] = authorizationToken;
            request.Headers.AuthorizationToken = authorizationToken;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                return await storeProxy.ProcessMessageAsync(request, cancellationToken);
            }
        }

        internal async Task<DocumentServiceResponse> ExecuteProcedureAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Headers[HttpConstants.HttpHeaders.ContentType] = RuntimeConstants.MediaTypes.Json;
            string userAuthorizationToken = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType), HttpConstants.HttpMethods.Post, request.Headers, AuthorizationTokenType.PrimaryMasterKey);
            request.Headers[HttpConstants.HttpHeaders.Authorization] =
                            userAuthorizationToken;
            request.Headers.AuthorizationToken = userAuthorizationToken;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                DocumentServiceResponse response = await storeProxy.ProcessMessageAsync(request, cancellationToken);

                this.CaptureSessionToken(request, response);
                return response;
            }
        }

        internal async Task<DocumentServiceResponse> ExecuteQueryAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string authorizationToken = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(
                request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                HttpConstants.HttpMethods.Post, request.Headers, AuthorizationTokenType.PrimaryMasterKey);
            request.Headers[HttpConstants.HttpHeaders.Authorization] = authorizationToken;
            request.Headers.AuthorizationToken = authorizationToken;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                DocumentServiceResponse response = await storeProxy.ProcessMessageAsync(request, cancellationToken);
                this.CaptureSessionToken(request, response);
                return response;
            }
        }

        internal async Task<DocumentServiceResponse> UpsertAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string authorization = ((IAuthorizationTokenProvider)this).GetUserAuthorizationToken(request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                HttpConstants.HttpMethods.Post, request.Headers, AuthorizationTokenType.PrimaryMasterKey);
            request.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;
            request.Headers.AuthorizationToken = authorization;

            request.Headers[HttpConstants.HttpHeaders.IsUpsert] = bool.TrueString;

            using (new ActivityScope(Guid.NewGuid()))
            {
                IStoreModel storeProxy = this.GetStoreProxy(request);
                DocumentServiceResponse response = await storeProxy.ProcessMessageAsync(request, cancellationToken);

                this.CaptureSessionToken(request, response);
                return response;
            }
        }
        #endregion

        /// <summary>
        /// Read the <see cref="Microsoft.Azure.Cosmos.CosmosAccountSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// A <see cref="CosmosAccountSettings"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public Task<CosmosAccountSettings> GetDatabaseAccountAsync()
        {
            return TaskHelper.InlineIfPossible(() => this.GetDatabaseAccountPrivateAsync(this.ReadEndpoint), this.ResetSessionTokenRetryPolicy.GetRequestPolicy());
        }

        /// <summary>
        /// Read the <see cref="Microsoft.Azure.Cosmos.CosmosAccountSettings"/> as an asynchronous operation
        /// given a specific reginal endpoint url.
        /// </summary>
        /// <param name="serviceEndpoint">The reginal url of the serice endpoint.</param>
        /// <returns>
        /// A <see cref="CosmosAccountSettings"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        Task<CosmosAccountSettings> IDocumentClientInternal.GetDatabaseAccountInternalAsync(Uri serviceEndpoint)
        {
            return this.GetDatabaseAccountPrivateAsync(serviceEndpoint);
        }

        private async Task<CosmosAccountSettings> GetDatabaseAccountPrivateAsync(Uri serviceEndpoint)
        {
            await this.EnsureValidClientAsync();
            GatewayStoreModel gatewayModel = this.gatewayStoreModel as GatewayStoreModel;
            if (gatewayModel != null)
            {
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    INameValueCollection headersCollection = new StringKeyValueCollection();
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

                    CosmosAccountSettings databaseAccount = await gatewayModel.GetDatabaseAccountAsync(request);

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
        /// <param name="request"></param>
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
                resourceType.IsScript() && operationType != OperationType.ExecuteJavaScript ||
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
        
        /// <summary>
        /// The preferred link used in replace operation in SDK.
        /// </summary>
        /// <returns></returns>
        private string GetLinkForRouting(CosmosResource resource)
        {
            // we currently prefer the selflink
            return resource.SelfLink ?? resource.AltLink;
        }

        internal void EnsureValidOverwrite(ConsistencyLevel desiredConsistencyLevel)
        {
            ConsistencyLevel defaultConsistencyLevel = this.accountServiceConfiguration.DefaultConsistencyLevel;
            if (!this.IsValidConsistency(defaultConsistencyLevel, desiredConsistencyLevel))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentUICulture,
                    RMResources.InvalidConsistencyLevel,
                    desiredConsistencyLevel.ToString(),
                    defaultConsistencyLevel.ToString()));
            }
        }

        private bool IsValidConsistency(ConsistencyLevel backendConsistency, ConsistencyLevel desiredConsistency)
        {
            if (this.allowOverrideStrongerConsistency)
            {
                return true;
            }

            return ValidationHelpers.ValidateConsistencyLevel(backendConsistency, desiredConsistency);
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
                this.storeClientFactory = new StoreClientFactory(
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
                    this.rntbdReceiveHangDetectionTimeSeconds,
                    this.rntbdSendHangDetectionTimeSeconds,
                    this.transportClientHandlerFactory,
                    this.enableCpuMonitor);
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
                this.connectionPolicy.EnableReadRequestsFallback ??
                    (this.accountServiceConfiguration.DefaultConsistencyLevel !=
                     ConsistencyLevel.BoundedStaleness),
                !this.enableRntbdChannel,
                this.useMultipleWriteLocations && this.accountServiceConfiguration.DefaultConsistencyLevel != ConsistencyLevel.Strong);

            if (subscribeRntbdStatus)
            {
                storeClient.AddDisableRntbdChannelCallback(new Action(this.DisableRntbdChannel));
            }

            storeClient.SerializerSettings = this.serializerSettings;

            this.storeModel = new ServerStoreModel(storeClient, this.sendingRequest, this.receivedResponse);
        }

        private void DisableRntbdChannel()
        {
            Debug.Assert(this.enableRntbdChannel);
            this.enableRntbdChannel = false;
            this.CreateStoreModel(subscribeRntbdStatus: false);
        }

        private async Task InitializeGatewayConfigurationReader()
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
            CosmosAccountSettings accountSettings = this.accountServiceConfiguration.AccountSettings;
            this.useMultipleWriteLocations = this.connectionPolicy.UseMultipleWriteLocations && accountSettings.EnableMultipleWriteLocations;

            await this.globalEndpointManager.RefreshLocationAsync(accountSettings);
        }

        internal void CaptureSessionToken(DocumentServiceRequest request, DocumentServiceResponse response)
        {
            this.sessionContainer.SetSessionToken(request, response.Headers);
        }

        internal DocumentServiceRequest CreateDocumentServiceRequest(
            OperationType operationType,
            string resourceLink,
            ResourceType resourceType,
            INameValueCollection headers)
        {
            if (resourceType == ResourceType.Database || resourceType == ResourceType.Offer)
            {
                return DocumentServiceRequest.Create(
                    operationType,
                    null,
                    resourceType,
                    AuthorizationTokenType.PrimaryMasterKey,
                    headers);
            }
            else
            {
                return DocumentServiceRequest.Create(
                    operationType,
                    resourceType,
                    resourceLink,
                    AuthorizationTokenType.PrimaryMasterKey,
                    headers);
            }
        }

        internal void ValidateResource(CosmosResource resource)
        {
            if (!string.IsNullOrEmpty(resource.Id))
            {
                int match = resource.Id.IndexOfAny(new char[] { '/', '\\', '?', '#' });
                if (match != -1)
                {
                    throw new ArgumentException(string.Format(
                                CultureInfo.CurrentUICulture,
                                RMResources.InvalidCharacterInResourceName,
                                resource.Id[match]
                                ));
                }

                if (resource.Id[resource.Id.Length - 1] == ' ')
                {
                    throw new ArgumentException(
                                RMResources.InvalidSpaceEndingInResourceName
                                );
                }
            }
        }

        private async Task AddPartitionKeyInformationAsync(DocumentServiceRequest request, Document document, RequestOptions options)
        {
            CollectionCache collectionCache = await this.GetCollectionCacheAsync();
            CosmosContainerSettings collection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);
            PartitionKeyDefinition partitionKeyDefinition = collection.PartitionKey;

            PartitionKeyInternal partitionKey;
            if (options != null && options.PartitionKey != null)
            {
                partitionKey = options.PartitionKey.InternalKey;
            }
            else
            {
                partitionKey = DocumentAnalyzer.ExtractPartitionKeyValue(document, partitionKeyDefinition);
            }

            request.Headers.Set(HttpConstants.HttpHeaders.PartitionKey, partitionKey.ToJsonString());
        }

        internal async Task AddPartitionKeyInformationAsync(DocumentServiceRequest request, RequestOptions options)
        {
            CollectionCache collectionCache = await this.GetCollectionCacheAsync();
            CosmosContainerSettings collection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);
            PartitionKeyDefinition partitionKeyDefinition = collection.PartitionKey;

            // For backward compatibility, if collection doesn't have partition key defined, we assume all documents
            // have empty value for it and user doesn't need to specify it explicitly.
            PartitionKeyInternal partitionKey;
            if (options == null || options.PartitionKey == null)
            {
                if (partitionKeyDefinition == null || partitionKeyDefinition.Paths.Count == 0)
                {
                    partitionKey = PartitionKeyInternal.Empty;
                }
                else
                {
                    throw new InvalidOperationException(RMResources.MissingPartitionKeyValue);
                }
            }
            else
            {
                partitionKey = options.PartitionKey.InternalKey;
            }

            request.Headers.Set(HttpConstants.HttpHeaders.PartitionKey, partitionKey.ToJsonString());
        }

        private JsonSerializerSettings GetSerializerSettingsForRequest(RequestOptions requestOptions)
        {
            return requestOptions?.JsonSerializerSettings ?? this.serializerSettings;
        }

        private INameValueCollection GetRequestHeaders(RequestOptions options)
        {
            Debug.Assert(
                this.initializeTask.IsCompleted,
                "GetRequestHeaders should be called after initialization task has been awaited to avoid blocking while accessing ConsistencyLevel property");

            INameValueCollection headers = new StringKeyValueCollection();

            if (this.useMultipleWriteLocations)
            {
                headers.Set(HttpConstants.HttpHeaders.AllowTentativeWrites, bool.TrueString);
            }

            if (this.desiredConsistencyLevel.HasValue)
            {
                // check anyways since default consistency level might have been refreshed.
                if (!this.IsValidConsistency(this.accountServiceConfiguration.DefaultConsistencyLevel, this.desiredConsistencyLevel.Value))
                {
                    throw new ArgumentException(string.Format(
                            CultureInfo.CurrentUICulture,
                            RMResources.InvalidConsistencyLevel,
                            options.ConsistencyLevel.Value.ToString(),
                            this.accountServiceConfiguration.DefaultConsistencyLevel));
                }

                headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, this.desiredConsistencyLevel.Value.ToString());
            }

            if (options == null)
            {
                return headers;
            }

            if (options.AccessCondition != null)
            {
                if (options.AccessCondition.Type == AccessConditionType.IfMatch)
                {
                    headers.Set(HttpConstants.HttpHeaders.IfMatch, options.AccessCondition.Condition);
                }
                else
                {
                    headers.Set(HttpConstants.HttpHeaders.IfNoneMatch, options.AccessCondition.Condition);
                }
            }

            if (options.ConsistencyLevel.HasValue)
            {
                if (!this.IsValidConsistency(this.accountServiceConfiguration.DefaultConsistencyLevel, options.ConsistencyLevel.Value))
                {
                    throw new ArgumentException(string.Format(
                            CultureInfo.CurrentUICulture,
                            RMResources.InvalidConsistencyLevel,
                            options.ConsistencyLevel.Value.ToString(),
                            this.accountServiceConfiguration.DefaultConsistencyLevel));
                }

                headers.Set(HttpConstants.HttpHeaders.ConsistencyLevel, options.ConsistencyLevel.ToString());
            }

            if (options.IndexingDirective.HasValue)
            {
                headers.Set(HttpConstants.HttpHeaders.IndexingDirective, options.IndexingDirective.ToString());
            }

            if (options.PostTriggerInclude != null && options.PostTriggerInclude.Count > 0)
            {
                string postTriggerInclude = string.Join(",", options.PostTriggerInclude.AsEnumerable());
                headers.Set(HttpConstants.HttpHeaders.PostTriggerInclude, postTriggerInclude);
            }

            if (options.PreTriggerInclude != null && options.PreTriggerInclude.Count > 0)
            {
                string preTriggerInclude = string.Join(",", options.PreTriggerInclude.AsEnumerable());
                headers.Set(HttpConstants.HttpHeaders.PreTriggerInclude, preTriggerInclude);
            }

            if (!string.IsNullOrEmpty(options.SessionToken))
            {
                headers[HttpConstants.HttpHeaders.SessionToken] = options.SessionToken;
            }

            if (options.ResourceTokenExpirySeconds.HasValue)
            {
                headers.Set(HttpConstants.HttpHeaders.ResourceTokenExpiry, options.ResourceTokenExpirySeconds.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (options.OfferType != null)
            {
                headers.Set(HttpConstants.HttpHeaders.OfferType, options.OfferType);
            }

            if (options.OfferThroughput.HasValue)
            {
                headers.Set(HttpConstants.HttpHeaders.OfferThroughput, options.OfferThroughput.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (options.EnableScriptLogging)
            {
                headers.Set(HttpConstants.HttpHeaders.EnableLogging, bool.TrueString);
            }

            if (options.PopulateQuotaInfo)
            {
                headers.Set(HttpConstants.HttpHeaders.PopulateQuotaInfo, bool.TrueString);
            }

            if (options.PopulateRestoreStatus)
            {
                headers.Set(HttpConstants.HttpHeaders.PopulateRestoreStatus, bool.TrueString);
            }

            if (options.PopulatePartitionKeyRangeStatistics)
            {
                headers.Set(HttpConstants.HttpHeaders.PopulatePartitionStatistics, bool.TrueString);
            }

            if (options.PartitionKeyRangeId != null)
            {
                headers.Set(WFConstants.BackendHeaders.PartitionKeyRangeId, options.PartitionKeyRangeId);
            }

            if (options.SourceDatabaseId != null)
            {
                headers.Set(HttpConstants.HttpHeaders.SourceDatabaseId, options.SourceDatabaseId);
            }

            if (options.SourceCollectionId != null)
            {
                headers.Set(HttpConstants.HttpHeaders.SourceCollectionId, options.SourceCollectionId);
            }

            if (options.RestorePointInTime.HasValue)
            {
                headers.Set(HttpConstants.HttpHeaders.RestorePointInTime, options.RestorePointInTime.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (options.ExcludeSystemProperties.HasValue)
            {
                headers.Set(WFConstants.BackendHeaders.ExcludeSystemProperties, options.ExcludeSystemProperties.Value.ToString());
            }

            return headers;
        }

        private string GetAttachmentId(string mediaId)
        {
            string attachmentId = null;
            byte storageIndex = 0;
            if (!MediaIdHelper.TryParseMediaId(mediaId, out attachmentId, out storageIndex))
            {
                throw new ArgumentException(ClientResources.MediaLinkInvalid);
            }

            return attachmentId;
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

            public HttpRequestMessageHandler(EventHandler<SendingRequestEventArgs> sendingRequest, EventHandler<ReceivedResponseEventArgs> receivedResponse)
            {
                this.sendingRequest = sendingRequest;
                this.receivedResponse = receivedResponse;
                InnerHandler = new HttpClientHandler();
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
