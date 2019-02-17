//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// This is a configuration class that holds all the properties the CosmosClient requires.
    /// </summary>
    public class CosmosConfiguration
    {
        /// <summary>
        /// Default max connection limit
        /// </summary>
        private const int DefaultMaxConcurrentConnectionLimit = 50;

        /// <summary>
        /// Default request timeout
        /// </summary>
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Default connection mode
        /// </summary>
        private const ConnectionMode DefaultConnectionMode = ConnectionMode.Direct;

        /// <summary>
        /// Default Protocol mode
        /// </summary>
        private const Protocol DefaultProtocol = Protocol.Tcp;

        private const string ConnectionStringAccountEndpoint = "AccountEndpoint";
        private const string ConnectionStringAccountKey = "AccountKey";

        private const ApiType DefaultApiType = ApiType.None;

        private CosmosRequestHandler[] customHandlers = null;
        private int maxConnectionLimit = CosmosConfiguration.DefaultMaxConcurrentConnectionLimit;
        private CosmosJsonSerializer cosmosJsonSerializer = null;

        /// <summary>
        /// Initialize a new CosmosConfiguration class that holds all the properties the CosmosClient requires.
        /// </summary>
        /// <param name="accountEndPoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="accountKey">The key to the account.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosConfiguration"/>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosConfiguration cosmosConfiguration = new CosmosConfiguration(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey");
        ///]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below creates a new <see cref="CosmosConfiguration"/> with a ConsistencyLevel and a list of preferred locations.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosConfiguration cosmosConfiguration = new CosmosConfiguration(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey")
        /// .UseConsistencyLevel(ConsistencyLevel.Strong)
        /// .UseCurrentRegion(Region.USEast2, Region.USWest2);
        /// ]]>
        /// </code>
        /// </example>
        public CosmosConfiguration(
            string accountEndPoint,
            string accountKey)
        {
            if (string.IsNullOrWhiteSpace(accountEndPoint))
            {
                throw new ArgumentNullException(nameof(accountEndPoint));
            }

            if (string.IsNullOrWhiteSpace(accountKey))
            {
                throw new ArgumentNullException(nameof(accountKey));
            }

            this.AccountEndPoint = new Uri(accountEndPoint);
            this.AccountKey = accountKey;
            Initialize();
        }

        /// <summary>
        /// Extracts the account endpoint and key from the connection string.
        /// </summary>
        /// <example>"AccountEndpoint=https://mytestcosmosaccount.documents.azure.com:443/;AccountKey={SecretAccountKey};"</example>
        /// <param name="connectionString">The connection string must contain AccountEndpoint and AccountKey.</param>
        public CosmosConfiguration(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            DbConnectionStringBuilder builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            this.AccountEndPoint = new Uri(CosmosConfiguration.GetValueFromSqlConnectionString(builder,
                CosmosConfiguration.ConnectionStringAccountEndpoint));
            this.AccountKey = CosmosConfiguration.GetValueFromSqlConnectionString(builder, CosmosConfiguration.ConnectionStringAccountKey);
            Initialize();
        }

        /// <summary>
        /// Gets the endpoint Uri for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the account endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        public virtual Uri AccountEndPoint { get; }

        /// <summary>
        /// Gets the AuthKey used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        internal string AccountKey { get; }

        /// <summary>
        /// Gets or sets the flag to determine whether SSL verification will be disabled when connecting to Cosmos DB over HTTPS.
        /// </summary>
        /// <remarks>
        /// When the value of this property is true, the SDK will bypass the normal SSL certificate verification
        /// process. This is useful when connecting the client to a Cosmos DB emulator across the network as
        /// some Linux clients do not honor any self-signed certificates that are installed into ca-certificates.
        /// Do not set this property when targeting Production environments.
        /// <value>Default value is false.</value>
        /// </remarks>
        public virtual bool DisableSslVerification { get; set; }

        /// <summary>
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public virtual string UserAgentSuffix
        {
            get => this.UserAgentContainer.Suffix;
            set => this.UserAgentContainer.Suffix = value;
        }

        /// <summary>
        /// Gets the current region. <see cref="CosmosRegions"/> to get a list of regions that
        /// are currently supported. Please update to a latest SDK version if a preferred Azure region is not listed.
        /// </summary>
        /// <seealso cref="CosmosConfiguration.UseCurrentRegion(string)"/>
        public virtual string CurrentRegion { get; set; }

        /// <summary>
        /// Gets the maximum number of concurrent connections allowed for the target
        /// service endpoint in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This setting is only applicable in Gateway mode.
        /// </remarks>
        /// <value>Default value is 50.</value>
        /// <seealso cref="CosmosConfiguration.UseConnectionModeGateway(int)"/>
        public virtual int MaxConnectionLimit
        {
            get => this.maxConnectionLimit;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                if (this.ConnectionMode != ConnectionMode.Gateway)
                {
                    throw new ArgumentException("Max connection limit is only valid for ConnectionMode.Gateway.");
                }

                this.maxConnectionLimit = value;
            }
        }

        /// <summary>
        /// Gets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// The number specifies the time to wait for response to come back from network peer.
        /// </summary>
        /// <value>Default value is 1 minute.</value>
        /// <seealso cref="CosmosConfiguration.UseRequestTimeout(TimeSpan)"/>
        public virtual TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Gets the handlers run before the process
        /// </summary>
        /// <seealso cref="CosmosConfiguration.AddCustomHandlers(CosmosRequestHandler[])"/>
        public virtual CosmosRequestHandler[] CustomHandlers
        {
            get => this.customHandlers;
            set
            {
                if (value != null && value.Any(x => x == null))
                {
                    throw new ArgumentNullException(nameof(this.CustomHandlers) + "requires all positions in the array to not be null.");
                }

                if (value != null && value.Any(x => x?.InnerHandler != null))
                {
                    throw new ArgumentException(nameof(this.CustomHandlers) + " requires all DelegatingHandler.InnerHandler to be null. The CosmosClient uses the inner handler in building the pipeline.");
                }

                this.customHandlers = value;
            }
        }

        /// <summary>
        /// Gets the connection mode used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Cosmos.ConnectionMode.Direct"/>
        /// </value>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosConfiguration.UseConnectionModeDirect"/>
        /// <seealso cref="CosmosConfiguration.UseConnectionModeGateway(int)"/>
        public virtual ConnectionMode ConnectionMode { get; set; }

        /// <summary>
        /// The number of times to retry on throttled requests.
        /// </summary>
        /// <seealso cref="CosmosConfiguration.UseThrottlingRetryOptions(TimeSpan, int)"/>
        public virtual int? MaxRetryAttemptsOnThrottledRequests { get; set; }

        /// <summary>
        /// The max time to wait for retry requests. 
        /// </summary>
        /// <remarks>
        /// The minimum interval is seconds. Any interval that is smaller will be ignored.
        /// </remarks>
        /// <seealso cref="CosmosConfiguration.UseThrottlingRetryOptions(TimeSpan, int)"/>
        public virtual TimeSpan? MaxRetryWaitTimeOnThrottledRequests { get; set; }

        /// <summary>
        /// Gets or sets the connection protocol when connecting to the Azure Cosmos service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Protocol.Tcp"/>.
        /// </value>
        /// <remarks>
        /// This setting is not used when <see cref="ConnectionMode"/> is set to <see cref="Cosmos.ConnectionMode.Gateway"/>.
        /// Gateway mode only supports HTTPS.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#use-tcp">Connection policy: Use the TCP protocol</see>.
        /// </remarks>
        internal Protocol ConnectionProtocol { get; private set; }

        /// <summary>
        /// A JSON serializer used by the CosmosClient to serialize or de-serialize cosmos request/responses.
        /// If no custom JSON converter was set it uses the default <see cref="CosmosDefaultJsonSerializer"/>
        /// </summary>
        public virtual CosmosJsonSerializer CosmosJsonSerializer
        {
            get => this.cosmosJsonSerializer ?? (this.cosmosJsonSerializer = new CosmosJsonSerializerWrapper(new CosmosDefaultJsonSerializer()));
            set => this.cosmosJsonSerializer = new CosmosJsonSerializerWrapper(value) ?? throw new NullReferenceException(nameof(this.CosmosJsonSerializer));
        }

        internal UserAgentContainer UserAgentContainer { get; private set; }

        /// <summary>
        /// The event handler to be invoked before the request is sent.
        /// </summary>
        internal EventHandler<SendingRequestEventArgs> SendingRequestEventArgs { get; private set; }

        /// <summary>
        /// (Optional) transport interceptor factory
        /// </summary>
        internal Func<TransportClient, TransportClient> TransportClientHandlerFactory { get; private set; }

        /// <summary>
        /// API type for the account
        /// </summary>
        internal ApiType ApiType { get; private set; }

        /// <summary>
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public virtual CosmosConfiguration UseUserAgentSuffix(string userAgentSuffix)
        {
            this.UserAgentContainer.Suffix = userAgentSuffix;
            return this;
        }

        /// <summary>
        /// Set the current preferred region
        /// </summary>
        /// <param name="cosmosRegion"><see cref="CosmosRegions"/> for a list of valid azure regions. This list may not contain the latest azure regions.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosConfiguration"/> with a of preferred region.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosConfiguration cosmosConfiguration = new CosmosConfiguration(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey")
        /// .UseCurrentRegion(CosmosRegion.USEast2);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosConfiguration.CurrentRegion"/>
        public virtual CosmosConfiguration UseCurrentRegion(string cosmosRegion)
        {
            this.CurrentRegion = cosmosRegion;
            return this;
        }

        /// <summary>
        /// Sets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>Default value is 60 seconds.</value>
        /// <seealso cref="CosmosConfiguration.RequestTimeout"/>
        public virtual CosmosConfiguration UseRequestTimeout(TimeSpan requestTimeout)
        {
            this.RequestTimeout = requestTimeout;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Direct. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosConfiguration.ConnectionMode"/>
        public virtual CosmosConfiguration UseConnectionModeDirect()
        {
            this.ConnectionMode = ConnectionMode.Direct;
            this.ConnectionProtocol = Protocol.Tcp;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Gateway. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxConnectionLimit">The number specifies the time to wait for response to come back from network peer. Default is 60 connections</param>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosConfiguration.ConnectionMode"/>
        /// <seealso cref="CosmosConfiguration.MaxConnectionLimit"/>
        public virtual CosmosConfiguration UseConnectionModeGateway(int maxConnectionLimit = CosmosConfiguration.DefaultMaxConcurrentConnectionLimit)
        {
            this.ConnectionMode = ConnectionMode.Gateway;
            this.ConnectionProtocol = Protocol.Https;
            this.MaxConnectionLimit = maxConnectionLimit;
            return this;
        }

        /// <summary>
        /// Sets an array of custom handlers to the request. The handlers will be chained in
        /// the order listed. The InvokerHandler.InnerHandler is required to be null to allow the
        /// pipeline to chain the handlers.
        /// </summary>
        /// <seealso cref="CosmosConfiguration.CustomHandlers"/>
        public virtual CosmosConfiguration AddCustomHandlers(params CosmosRequestHandler[] handlers)
        {
            if (handlers != null && handlers.Any(x => x != null))
            {
                this.CustomHandlers = handlers;
            }
            else
            {
                this.CustomHandlers = null;
            }

            return this;
        }

        /// <summary>
        /// Sets the minimum time to wait between retry and the max number of times to retry on throttled requests.
        /// </summary>
        /// <param name="maxRetryWaitTimeOnThrottledRequests">The maximum retry time in seconds for the Azure Cosmos DB service. Any interval that is smaller than a second will be ignored.</param>
        /// <param name="maxRetryAttemptsOnThrottledRequests">The number specifies the times retry requests for throttled requests.</param>
        /// <para>
        /// When a request fails due to a rate limiting error, the service sends back a response that
        /// contains a value indicating the client should not retry before the time period has
        /// elapsed. This property allows the application to set a maximum wait time for all retry attempts.
        /// If the cumulative wait time exceeds the this value, the client will stop retrying and return the error to the application.
        /// </para>
        /// <para>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#429">Handle rate limiting/request rate too large</see>.
        /// </para>
        /// <seealso cref="CosmosConfiguration.MaxRetryWaitTimeOnThrottledRequests"/>
        /// <seealso cref="CosmosConfiguration.MaxRetryAttemptsOnThrottledRequests"/>
        public virtual CosmosConfiguration UseThrottlingRetryOptions(TimeSpan maxRetryWaitTimeOnThrottledRequests, int maxRetryAttemptsOnThrottledRequests)
        {
            this.MaxRetryWaitTimeOnThrottledRequests = maxRetryWaitTimeOnThrottledRequests;
            this.MaxRetryAttemptsOnThrottledRequests = maxRetryAttemptsOnThrottledRequests;
            return this;
        }

        /// <summary>
        /// Set a custom JSON serializer. 
        /// </summary>
        /// <param name="cosmosJsonSerializer">The custom class that implements <see cref="CosmosJsonSerializer"/> </param>
        /// <returns>The <see cref="CosmosConfiguration"/> object</returns>
        /// <seealso cref="CosmosJsonSerializer"/>
        /// <seealso cref="CosmosConfiguration.CosmosJsonSerializer"/>
        public virtual CosmosConfiguration UseCustomJsonSerializer(
            CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.CosmosJsonSerializer = cosmosJsonSerializer;
            return this;
        }

        /// <summary>
        /// The event handler to be invoked before the request is sent.
        /// </summary>
        internal CosmosConfiguration UseSendingRequestEventArgs(EventHandler<SendingRequestEventArgs> sendingRequestEventArgs)
        {
            this.SendingRequestEventArgs = sendingRequestEventArgs;
            return this;
        }

        /// <summary>
        /// (Optional) transport interceptor factory
        /// </summary>
        internal CosmosConfiguration UseTransportClientHandlerFactory(Func<TransportClient, TransportClient> transportClientHandlerFactory)
        {
            this.TransportClientHandlerFactory = transportClientHandlerFactory;
            return this;
        }

        /// <summary>
        /// ApiType for the account
        /// </summary>
        internal CosmosConfiguration UseApiType(ApiType apiType)
        {
            this.ApiType = apiType;
            return this;
        }

        internal ConnectionPolicy GetConnectionPolicy()
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                MaxConnectionLimit = this.MaxConnectionLimit,
                RequestTimeout = this.RequestTimeout,
                ConnectionMode = this.ConnectionMode,
                ConnectionProtocol = this.ConnectionProtocol,
                UserAgentContainer = this.UserAgentContainer,
                UseMultipleWriteLocations = true,
                DisableSslVerification = this.DisableSslVerification
            };

            if (this.CurrentRegion != null)
            {
                connectionPolicy.SetCurrentLocation(this.CurrentRegion);
            }

            if (this.MaxRetryAttemptsOnThrottledRequests != null && this.MaxRetryAttemptsOnThrottledRequests.HasValue)
            {
                connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = this.MaxRetryAttemptsOnThrottledRequests.Value;
            }

            if (this.MaxRetryWaitTimeOnThrottledRequests != null && this.MaxRetryWaitTimeOnThrottledRequests.HasValue)
            {
                connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = (int)this.MaxRetryWaitTimeOnThrottledRequests.Value.TotalSeconds;
            }

            return connectionPolicy;
        }

        private void Initialize()
        {
            this.UserAgentContainer = new UserAgentContainer();
            this.MaxConnectionLimit = CosmosConfiguration.DefaultMaxConcurrentConnectionLimit;
            this.RequestTimeout = CosmosConfiguration.DefaultRequestTimeout;
            this.ConnectionMode = CosmosConfiguration.DefaultConnectionMode;
            this.ConnectionProtocol = CosmosConfiguration.DefaultProtocol;
            this.ApiType = CosmosConfiguration.DefaultApiType;
            this.DisableSslVerification = false;
        }

        private static string GetValueFromSqlConnectionString(DbConnectionStringBuilder builder, string keyName)
        {
            if (builder.TryGetValue(keyName, out object value))
            {
                string keyNameValue = value as string;
                if (!string.IsNullOrEmpty(keyNameValue))
                {
                    return keyNameValue;
                }
            }

            throw new ArgumentException("The connection string is missing a required property: " + keyName);
        }
    }
}
