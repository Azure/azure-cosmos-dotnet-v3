//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Data.Common;
    using System.Linq;
    using System.Runtime.ConstrainedExecution;
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

        /// <summary>
        /// Default request timeout
        /// </summary>
        private static readonly CosmosSerializer propertiesSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());
        private readonly Collection<RequestHandler> customHandlers;

        private int gatewayModeMaxConnectionLimit;

        /// <summary>
        /// Creates a new CosmosClientOptions
        /// </summary>
        public CosmosClientOptions()
        {
            this.UserAgentContainer = new UserAgentContainer();
            this.GatewayModeMaxConnectionLimit = ConnectionPolicy.Default.MaxConnectionLimit;
            this.RequestTimeout = ConnectionPolicy.Default.RequestTimeout;
            this.ConnectionMode = CosmosClientOptions.DefaultConnectionMode;
            this.ConnectionProtocol = CosmosClientOptions.DefaultProtocol;
            this.ApiType = CosmosClientOptions.DefaultApiType;
            this.customHandlers = new Collection<RequestHandler>();
        }

        /// <summary>
        /// Get or set user-agent suffix to include with every Azure Cosmos DB service interaction.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public string ApplicationName
        {
            get => this.UserAgentContainer.Suffix;
            set => this.UserAgentContainer.Suffix = value;
        }

        /// <summary>
        /// Get or set the preferred geo-replicated region to be used for Azure Cosmos DB service interaction.
        /// </summary>
        /// <remarks>
        /// When this property is specified, the SDK prefers the region to perform operations. Also SDK auto-selects 
        /// fallback geo-replicated regions for high availability. 
        /// When this property is not specified, the SDK uses the write region as the preferred region for all operations.
        /// 
        /// <seealso cref="CosmosClientBuilder.WithApplicationRegion(string)"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/how-to-multi-master"/>
        /// </remarks>
        public string ApplicationRegion { get; set; }

        /// <summary>
        /// Get or set the maximum number of concurrent connections allowed for the target
        /// service endpoint in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This setting is only applicable in Gateway mode.
        /// </remarks>
        /// <value>Default value is 50.</value>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeGateway(int?)"/>
        public int GatewayModeMaxConnectionLimit
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
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Gets the handlers run before the process
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.AddCustomHandlers(RequestHandler[])"/>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        public Collection<RequestHandler> CustomHandlers
        {
            get => this.customHandlers;
        }

        /// <summary>
        /// Get or set the connection mode used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Cosmos.ConnectionMode.Direct"/>
        /// </value>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeDirect"/>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeGateway(int?)"/>
        public ConnectionMode ConnectionMode { get; set; }

        /// <summary>
        /// This can be used to weaken the database account consistency level for read operations.
        /// If this is not set the database account consistency level will be used for all requests.
        /// </summary>
        public ConsistencyLevel? ConsistencyLevel { get; set; }

        /// <summary>
        /// Get ot set the number of times client should retry on rate throttled requests.
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.WithThrottlingRetryOptions(TimeSpan, int)"/>
        public int? MaxRetryAttemptsOnRateLimitedRequests { get; set; }

        /// <summary>
        /// Get or set the max time to client is allowed to retry on rate throttled requests. 
        /// </summary>
        /// <remarks>
        /// The minimum interval is seconds. Any interval that is smaller will be ignored.
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithThrottlingRetryOptions(TimeSpan, int)"/>
        public TimeSpan? MaxRetryWaitTimeOnRateLimitedRequests { get; set; }

        /// <summary>
        /// Get ot set an optional serializer client should use to serialize or de-serialize cosmos request/responses.
        /// </summary>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        public CosmosSerializer Serializer { get; set; }

        /// <summary>
        /// A JSON serializer used by the CosmosClient to serialize or de-serialize cosmos request/responses.
        /// The default serializer is always used for all system owned types like DatabaseProperties.
        /// The default serializer is used for user types if no UserJsonSerializer is specified
        /// </summary>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        internal CosmosSerializer PropertiesSerializer => CosmosClientOptions.propertiesSerializer;

        /// <summary>
        /// Gets the user json serializer with the CosmosJsonSerializerWrapper or the default
        /// </summary>
        [JsonIgnore]
        internal CosmosSerializer CosmosSerializerWithWrapperOrDefault => this.Serializer == null ? this.PropertiesSerializer : new CosmosJsonSerializerWrapper(this.Serializer);

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
        /// Gets or sets the interval to salt retry with value. This will spread the retry values from 1..n from the exponential back-off
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
        /// Gets or sets the total time to wait before failing the request for retry with failures.
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
            };

            if (this.ApplicationRegion != null)
            {
                connectionPolicy.SetCurrentLocation(this.ApplicationRegion);
            }

            if (this.MaxRetryAttemptsOnRateLimitedRequests != null)
            {
                connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = this.MaxRetryAttemptsOnRateLimitedRequests.Value;
            }

            if (this.MaxRetryWaitTimeOnRateLimitedRequests != null)
            {
                connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = (int)this.MaxRetryWaitTimeOnRateLimitedRequests.Value.TotalSeconds;
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

        internal Documents.ConsistencyLevel? GetDocumentsConsistencyLevel()
        {
            if (!this.ConsistencyLevel.HasValue)
            {
                return null;
            }

            switch (this.ConsistencyLevel.Value)
            {
                case Cosmos.ConsistencyLevel.BoundedStaleness:
                    return Documents.ConsistencyLevel.BoundedStaleness;
                case Cosmos.ConsistencyLevel.ConsistentPrefix:
                    return Documents.ConsistencyLevel.BoundedStaleness;
                case Cosmos.ConsistencyLevel.Eventual:
                    return Documents.ConsistencyLevel.Eventual;
                case Cosmos.ConsistencyLevel.Session:
                    return Documents.ConsistencyLevel.Session;
                case Cosmos.ConsistencyLevel.Strong:
                    return Documents.ConsistencyLevel.Strong;
                default:
                    throw new ArgumentException($"Unsupported ConsistencyLevel {this.ConsistencyLevel.Value}");
            }
        }

        internal static string GetAccountEndpoint(string connectionString)
        {
            return CosmosClientOptions.GetValueFromConnectionString(connectionString, CosmosClientOptions.ConnectionStringAccountEndpoint);
        }

        internal static string GetAccountKey(string connectionString)
        {
            return CosmosClientOptions.GetValueFromConnectionString(connectionString, CosmosClientOptions.ConnectionStringAccountKey);
        }

        private static string GetValueFromConnectionString(string connectionString, string keyName)
        {
            if (connectionString == null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            DbConnectionStringBuilder builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
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
                Collection<RequestHandler> handlers = value as Collection<RequestHandler>;
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
