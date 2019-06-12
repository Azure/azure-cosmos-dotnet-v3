//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Data.Common;
    using System.Linq;
    using System.Security;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines all the configurable options that the CosmosClient requires.
    /// </summary>
    public class CosmosClientOptions
    {
        /// <summary>
        /// Default max connection limit
        /// </summary>
        private const int DefaultMaxConcurrentConnectionLimit = 50;

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
        private const string RequiresDiretMode = "requires Direct Mode";

        private const ApiType DefaultApiType = ApiType.None;

        /// <summary>
        /// Default request timeout
        /// </summary>
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(1);

        private static readonly CosmosJsonSerializer settingsSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonSerializerCore());
        private CosmosJsonSerializer userJsonSerializer;

        private ReadOnlyCollection<CosmosRequestHandler> customHandlers;
        private int gatewayModeMaxConnectionLimit;

        private TimeSpan? idleTcpConnectionTimeout;
        private TimeSpan? openTcpConnectionTimeout;
        private int? maxRequestsPerTcpConnection;
        private int? maxTcpConnectionsPerEndpoint;

        /// <summary>
        /// Initialize a new CosmosClientOptions class that holds all the properties the CosmosClient requires.
        /// </summary>
        /// <param name="endPoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="accountKey">The key to the account.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientOptions"/>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions(
        ///     endPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey");
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClientOptions(
            string endPoint,
            string accountKey)
        {
            if (string.IsNullOrWhiteSpace(endPoint))
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            if (string.IsNullOrWhiteSpace(accountKey))
            {
                throw new ArgumentNullException(nameof(accountKey));
            }

            this.EndPoint = new Uri(endPoint);
            this.AccountKey = new StringHMACSHA256Hash(accountKey);

            this.Initialize();
        }

        /// <summary>
        /// Initialize a new CosmosClientOptions class that holds all the properties the CosmosClient requires.
        /// </summary>
        /// <param name="endPoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="accountKey">The key to the account.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientOptions"/>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions(
        ///     endPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey");
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClientOptions(
            string endPoint,
            SecureString accountKey)
        {
            if (string.IsNullOrWhiteSpace(endPoint))
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            if (accountKey == null || accountKey.Length == 0)
            {
                throw new ArgumentNullException(nameof(accountKey));
            }

            this.EndPoint = new Uri(endPoint);
            this.AccountKey = new SecureStringHMACSHA256Helper(accountKey);

            this.Initialize();
        }

        /// <summary>
        /// Extracts the account endpoint and key from the connection string.
        /// </summary>
        /// <example>"AccountEndpoint=https://mytestcosmosaccount.documents.azure.com:443/;AccountKey={SecretAccountKey};"</example>
        /// <param name="connectionString">The connection string must contain AccountEndpoint and AccountKey.</param>
        public CosmosClientOptions(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            DbConnectionStringBuilder builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            this.EndPoint = new Uri(CosmosClientOptions.GetValueFromSqlConnectionString(builder,
                CosmosClientOptions.ConnectionStringAccountEndpoint));

            string key = CosmosClientOptions.GetValueFromSqlConnectionString(builder, CosmosClientOptions.ConnectionStringAccountKey);
            this.AccountKey = new StringHMACSHA256Hash(key); 

            this.Initialize();
        }

        /// <summary>
        /// Gets the endpoint Uri for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the account endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        public virtual Uri EndPoint { get; }

        /// <summary>
        /// Gets the AuthKey used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        internal IComputeHash AccountKey { get; }

        /// <summary>
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public virtual string ApplicationName
        {
            get => this.UserAgentContainer.Suffix;
            set => this.UserAgentContainer.Suffix = value;
        }

        /// <summary>
        /// Gets the current region. <see cref="CosmosRegions"/> to get a list of regions that
        /// are currently supported. Please update to a latest SDK version if a preferred Azure region is not listed.
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.WithApplicationRegion(string)"/>
        public virtual string ApplicationRegion { get; set; }

        /// <summary>
        /// Gets the maximum number of concurrent connections allowed for the target
        /// service endpoint in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This setting is only applicable in Gateway mode.
        /// </remarks>
        /// <value>Default value is 50.</value>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeGateway(int?)"/>
        public virtual int GatewayModeMaxConnectionLimit
        {
            get => this.gatewayModeMaxConnectionLimit;
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

                this.gatewayModeMaxConnectionLimit = value;
            }
        }

        /// <summary>
        /// Gets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// The number specifies the time to wait for response to come back from network peer.
        /// </summary>
        /// <value>Default value is 1 minute.</value>
        /// <seealso cref="CosmosClientBuilder.WithRequestTimeout(TimeSpan)"/>
        public virtual TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Gets the handlers run before the process
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.AddCustomHandlers(CosmosRequestHandler[])"/>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        public virtual ReadOnlyCollection<CosmosRequestHandler> CustomHandlers
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
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeDirect"/>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeGateway(int?)"/>
        public virtual ConnectionMode ConnectionMode { get; set; }

        /// <summary>
        /// The number of times to retry on throttled requests.
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.WithThrottlingRetryOptions(TimeSpan, int)"/>
        public virtual int? MaxRetryAttemptsOnThrottledRequests { get; set; }

        /// <summary>
        /// The max time to wait for retry requests. 
        /// </summary>
        /// <remarks>
        /// The minimum interval is seconds. Any interval that is smaller will be ignored.
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithThrottlingRetryOptions(TimeSpan, int)"/>
        public virtual TimeSpan? MaxRetryWaitTimeOnThrottledRequests { get; set; }
          
        /// <summary>
        /// (Direct/TCP) Controls the amount of idle time after which unused connections are closed.
        /// </summary>
        /// <value>
        /// By default, idle connections are kept open indefinitely. Value must be greater than or equal to 10 minutes. Recommended values are between 20 minutes and 24 hours.
        /// </value>
        /// <remarks>
        /// Mainly useful for sparse infrequent access to a large database account.
        /// </remarks>
        public virtual TimeSpan? IdleTcpConnectionTimeout
        {
            get => this.idleTcpConnectionTimeout;
            set
            {
                if (this.ConnectionMode == ConnectionMode.Gateway)
                {
                    throw new ArgumentException($"{nameof(this.MaxTcpConnectionsPerEndpoint)} {RequiresDiretMode}");
                }
                this.idleTcpConnectionTimeout = value;
            }
        }      

        /// <summary>
        /// (Direct/TCP) Controls the amount of time allowed for trying to establish a connection.
        /// </summary>
        /// <value>
        /// The default timeout is 5 seconds. Recommended values are greater than or equal to 5 seconds.
        /// </value>
        /// <remarks>
        /// When the time elapses, the attempt is cancelled and an error is returned. Longer timeouts will delay retries and failures.
        /// </remarks>
        public virtual TimeSpan? OpenTcpConnectionTimeout
        {
            get => this.openTcpConnectionTimeout;
            set
            {
                if (this.ConnectionMode == ConnectionMode.Gateway)
                {
                    throw new ArgumentException($"{nameof(this.MaxTcpConnectionsPerEndpoint)} {RequiresDiretMode}");
                }
                this.openTcpConnectionTimeout = value;
            }
        }

        /// <summary>
        /// (Direct/TCP) Controls the number of requests allowed simultaneously over a single TCP connection. When more requests are in flight simultaneously, the direct/TCP client will open additional connections.
        /// </summary>
        /// <value>
        /// The default settings allow 30 simultaneous requests per connection.
        /// Do not set this value lower than 4 requests per connection or higher than 50-100 requests per connection.       
        /// The former can lead to a large number of connections to be created. 
        /// The latter can lead to head of line blocking, high latency and timeouts.
        /// </value>
        /// <remarks>
        /// Applications with a very high degree of parallelism per connection, with large requests or responses, or with very tight latency requirements might get better performance with 8-16 requests per connection.
        /// </remarks>
        public virtual int? MaxRequestsPerTcpConnection
        {
            get => this.maxRequestsPerTcpConnection;
            set
            {
                if (this.ConnectionMode == ConnectionMode.Gateway)
                {
                    throw new ArgumentException($"{nameof(this.MaxTcpConnectionsPerEndpoint)} {RequiresDiretMode}");
                }
                this.maxRequestsPerTcpConnection = value;
            }
        }

        /// <summary>
        /// (Direct/TCP) Controls the maximum number of TCP connections that may be opened to each Cosmos DB back-end.
        /// Together with MaxRequestsPerTcpConnection, this setting limits the number of requests that are simultaneously sent to a single Cosmos DB back-end(MaxRequestsPerTcpConnection x MaxTcpConnectionPerEndpoint).
        /// </summary>
        /// <value>
        /// The default value is 65,535. Value must be greater than or equal to 16.
        /// </value>
        public virtual int? MaxTcpConnectionsPerEndpoint
        {           
            get => this.maxTcpConnectionsPerEndpoint;
            set
            {
                if (this.ConnectionMode == ConnectionMode.Gateway)
                {
                    throw new ArgumentException($"{nameof(this.MaxTcpConnectionsPerEndpoint)} {RequiresDiretMode}");
                }
                this.maxTcpConnectionsPerEndpoint = value;
            }
        }

        /// <summary>
        /// A JSON serializer used by the CosmosClient to serialize or de-serialize cosmos request/responses.
        /// If no custom JSON converter was set it uses the default <see cref="CosmosJsonSerializerCore"/>
        /// </summary>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        public virtual CosmosJsonSerializer CosmosSerializer
        {
            get => this.userJsonSerializer;
            set => this.userJsonSerializer = value ?? throw new NullReferenceException(nameof(this.CosmosSerializer));
        }

        /// <summary>
        /// A JSON serializer used by the CosmosClient to serialize or de-serialize cosmos request/responses.
        /// The default serializer is always used for all system owned types like CosmosDatabaseSettings.
        /// The default serializer is used for user types if no UserJsonSerializer is specified
        /// </summary>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        internal virtual CosmosJsonSerializer SettingsSerializer => CosmosClientOptions.settingsSerializer;

        /// <summary>
        /// Gets the user json serializer with the CosmosJsonSerializerWrapper or the default
        /// </summary>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        internal virtual CosmosJsonSerializer CosmosSerializerWithWrapperOrDefault => this.userJsonSerializer == null ? this.SettingsSerializer : new CosmosJsonSerializerWrapper(this.userJsonSerializer);

        /// <summary>
        /// Gets or sets the connection protocol when connecting to the Azure Cosmos service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Protocol.Tcp"/>.
        /// </value>
        /// <remarks>
        /// This setting is not used when <see cref="ConnectionMode"/> is set to <see cref="Cosmos.ConnectionMode.Gateway"/>.
        /// Gateway mode only supports HTTPS.
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#use-tcp">Connection policy: Use the TCP protocol</see>.
        /// </remarks>
        internal Protocol ConnectionProtocol { get; set; }

        internal UserAgentContainer UserAgentContainer { get; private set; }

        /// <summary>
        /// The event handler to be invoked before the request is sent.
        /// </summary>
        internal EventHandler<SendingRequestEventArgs> SendingRequestEventArgs { get; set; }

        /// <summary>
        /// (Optional) transport interceptor factory
        /// </summary>
        internal Func<TransportClient, TransportClient> TransportClientHandlerFactory { get; set; }

        /// <summary>
        /// API type for the account
        /// </summary>
        internal ApiType ApiType { get; set; }

        /// <summary>
        /// Optional store client factory instance to use for all transport requests.
        /// </summary>
        internal IStoreClientFactory StoreClientFactory { get; set; }

        /// <summary>
        /// Gets or sets the initial delay retry time in milliseconds for the Azure Cosmos DB service
        /// for requests that hit RetryWithExceptions. This covers errors that occur due to concurrency errors in the store.
        /// </summary>
        /// <value>
        /// The default value is 1 second. For an example on how to set this value, please refer to <see cref="ConnectionPolicy.RetryOptions"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures the client
        /// to delay the time specified before retrying the request.
        /// </para>
        /// </remarks>
        internal int? InitialRetryForRetryWithMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the maximum delay retry time in milliseconds for the Azure Cosmos DB service
        /// for requests that hit RetryWithExceptions. This covers errors that occur due to concurrency errors in the store.
        /// </summary>
        /// <value>
        /// The default value is 30 seconds. For an example on how to set this value, please refer to <see cref="ConnectionPolicy.RetryOptions"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures the maximum time
        /// the client should delay before failing the request.
        /// </para>
        /// </remarks>
        internal int? MaximumRetryForRetryWithMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the interval to salt retrywith retries with. This will spread the retry values from 1..n from the exponential backoff
        /// subscribed.
        /// </summary>
        /// <value>
        /// The default value is to not salt.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures the jitter on the retry attempted.
        /// </para>
        /// </remarks>
        internal int? RandomSaltForRetryWithMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the total time to wait before failing the request for retrywith failures.
        /// subscribed.
        /// </summary>
        /// <value>
        /// The default value 30 seconds.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a request fails due to a RetryWith error, the client delays and retries the request. This configures total time spent waiting on the request.
        /// </para>
        /// </remarks>
        internal int? TotalWaitTimeForRetryWithMilliseconds { get; set; }

        /// <summary>
        /// Flag that controls whether CPU monitoring thread is created to enrich timeout exceptions with additional diagnostic. Default value is true.
        /// </summary>
        internal bool? EnableCpuMonitor { get; set; }

        internal CosmosClientOptions Clone()
        {
            CosmosClientOptions cloneConfiguration = (CosmosClientOptions)this.MemberwiseClone();
            return cloneConfiguration;
        }

        internal ConnectionPolicy GetConnectionPolicy()
        {            
            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                MaxConnectionLimit = this.GatewayModeMaxConnectionLimit,
                RequestTimeout = this.RequestTimeout,
                ConnectionMode = this.ConnectionMode,
                ConnectionProtocol = this.ConnectionProtocol,
                UserAgentContainer = this.UserAgentContainer,
                UseMultipleWriteLocations = true,
                IdleTcpConnectionTimeout = this.IdleTcpConnectionTimeout,
                OpenTcpConnectionTimeout = this.OpenTcpConnectionTimeout,
                MaxRequestsPerTcpConnection = this.MaxRequestsPerTcpConnection,
                MaxTcpConnectionsPerEndpoint = this.MaxTcpConnectionsPerEndpoint
            };

            if (this.ApplicationRegion != null)
            {
                connectionPolicy.SetCurrentLocation(this.ApplicationRegion);
            }

            if (this.MaxRetryAttemptsOnThrottledRequests != null)
            {
                connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = this.MaxRetryAttemptsOnThrottledRequests.Value;
            }

            if (this.MaxRetryWaitTimeOnThrottledRequests != null)
            {
                connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = (int)this.MaxRetryWaitTimeOnThrottledRequests.Value.TotalSeconds;
            }

            if (this.InitialRetryForRetryWithMilliseconds != null)
            {
                connectionPolicy.RetryOptions.InitialRetryForRetryWithMilliseconds =
                    this.InitialRetryForRetryWithMilliseconds;
            }

            if (this.MaximumRetryForRetryWithMilliseconds != null)
            {
                connectionPolicy.RetryOptions.MaximumRetryForRetryWithMilliseconds =
                    this.MaximumRetryForRetryWithMilliseconds;
            }

            if (this.RandomSaltForRetryWithMilliseconds != null)
            {
                connectionPolicy.RetryOptions.RandomSaltForRetryWithMilliseconds
                    = this.RandomSaltForRetryWithMilliseconds;
            }

            if (this.TotalWaitTimeForRetryWithMilliseconds != null)
            {
                connectionPolicy.RetryOptions.TotalWaitTimeForRetryWithMilliseconds
                    = this.TotalWaitTimeForRetryWithMilliseconds;
            }

            return connectionPolicy;
        }

        private void Initialize()
        {
            this.UserAgentContainer = new UserAgentContainer();
            this.GatewayModeMaxConnectionLimit = CosmosClientOptions.DefaultMaxConcurrentConnectionLimit;
            this.RequestTimeout = CosmosClientOptions.DefaultRequestTimeout;
            this.ConnectionMode = CosmosClientOptions.DefaultConnectionMode;
            this.ConnectionProtocol = CosmosClientOptions.DefaultProtocol;
            this.ApiType = CosmosClientOptions.DefaultApiType;
            this.customHandlers = null;
            this.userJsonSerializer = null;
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

        /// <summary>
        /// Serialize the current configuration into a JSON string
        /// </summary>
        /// <returns>Returns a JSON string of the current configuration.</returns>
        internal string GetSerializedConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// The complex object passed in by the user can contain objects that can not be serialized. Instead just log the types.
        /// </summary>
        private class ClientOptionJsonConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                ReadOnlyCollection<CosmosRequestHandler> handlers = value as ReadOnlyCollection<CosmosRequestHandler>;
                if (handlers != null)
                {
                    writer.WriteValue(string.Join(":", handlers.Select(x => x.GetType())));
                    return;
                }

                CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper = value as CosmosJsonSerializerWrapper;
                if (value is CosmosJsonSerializerWrapper)
                {
                    writer.WriteValue(cosmosJsonSerializerWrapper.InternalJsonSerializer.GetType().ToString());
                }
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
            }

            public override bool CanRead => false;

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(DateTime);
            }
        }
    }
}
