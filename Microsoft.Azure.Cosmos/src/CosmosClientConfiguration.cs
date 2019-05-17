//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Data.Common;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json;

    /// <summary>
    /// This is a configuration class that holds all the properties the CosmosClient requires.
    /// </summary>
    public class CosmosClientConfiguration
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

        private ReadOnlyCollection<CosmosRequestHandler> customHandlers = null;
        private int maxConnectionLimit = CosmosClientConfiguration.DefaultMaxConcurrentConnectionLimit;
        private CosmosJsonSerializer cosmosJsonSerializer = null;

        /// <summary>
        /// Initialize a new CosmosConfiguration class that holds all the properties the CosmosClient requires.
        /// </summary>
        /// <param name="accountEndPoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="accountKey">The key to the account.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientConfiguration"/>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosConfiguration cosmosConfiguration = new CosmosConfiguration(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey");
        ///]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientConfiguration"/> with a ConsistencyLevel and a list of preferred locations.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosConfiguration cosmosConfiguration = new CosmosConfiguration(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey")
        /// .UseConsistencyLevel(ConsistencyLevel.Strong)
        /// .UseCurrentRegion(Region.USEast2);
        /// ]]>
        /// </code>
        /// </example>
        internal CosmosClientConfiguration(
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
        internal CosmosClientConfiguration(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            DbConnectionStringBuilder builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            this.AccountEndPoint = new Uri(CosmosClientConfiguration.GetValueFromSqlConnectionString(builder,
                CosmosClientConfiguration.ConnectionStringAccountEndpoint));
            this.AccountKey = CosmosClientConfiguration.GetValueFromSqlConnectionString(builder, CosmosClientConfiguration.ConnectionStringAccountKey);
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
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public virtual string UserAgentSuffix
        {
            get => this.UserAgentContainer.Suffix;
            internal set => this.UserAgentContainer.Suffix = value;
        }

        /// <summary>
        /// Gets the current region. <see cref="CosmosRegions"/> to get a list of regions that
        /// are currently supported. Please update to a latest SDK version if a preferred Azure region is not listed.
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.UseCurrentRegion(string)"/>
        public virtual string CurrentRegion { get; internal set; }

        /// <summary>
        /// Gets the maximum number of concurrent connections allowed for the target
        /// service endpoint in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This setting is only applicable in Gateway mode.
        /// </remarks>
        /// <value>Default value is 50.</value>
        /// <seealso cref="CosmosClientBuilder.UseConnectionModeGateway(int?)"/>
        public virtual int MaxConnectionLimit
        {
            get => this.maxConnectionLimit;
            internal set
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
        /// <seealso cref="CosmosClientBuilder.UseRequestTimeout(TimeSpan)"/>
        public virtual TimeSpan RequestTimeout { get; internal set; }

        /// <summary>
        /// Gets the handlers run before the process
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.AddCustomHandlers(CosmosRequestHandler[])"/>
        [JsonConverter(typeof(ClientConfigurationJsonConverter))]
        public virtual ReadOnlyCollection<CosmosRequestHandler> CustomHandlers
        {
            get => this.customHandlers;
            internal set
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
        /// <seealso cref="CosmosClientBuilder.UseConnectionModeDirect"/>
        /// <seealso cref="CosmosClientBuilder.UseConnectionModeGateway(int?)"/>
        public virtual ConnectionMode ConnectionMode { get; internal set; }

        /// <summary>
        /// The number of times to retry on throttled requests.
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.UseThrottlingRetryOptions(TimeSpan, int)"/>
        public virtual int? MaxRetryAttemptsOnThrottledRequests { get; internal set; }

        /// <summary>
        /// The max time to wait for retry requests. 
        /// </summary>
        /// <remarks>
        /// The minimum interval is seconds. Any interval that is smaller will be ignored.
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.UseThrottlingRetryOptions(TimeSpan, int)"/>
        public virtual TimeSpan? MaxRetryWaitTimeOnThrottledRequests { get; internal set; }

        /// <summary>
        /// A JSON serializer used by the CosmosClient to serialize or de-serialize cosmos request/responses.
        /// If no custom JSON converter was set it uses the default <see cref="CosmosJsonSerializerCore"/>
        /// </summary>
        [JsonConverter(typeof(ClientConfigurationJsonConverter))]
        public virtual CosmosJsonSerializer CosmosJsonSerializer
        {
            get => this.cosmosJsonSerializer ?? (this.cosmosJsonSerializer = new CosmosJsonSerializerCore());
            internal set => this.cosmosJsonSerializer = value ?? throw new NullReferenceException(nameof(this.CosmosJsonSerializer));
        }

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
        /// Flag that controls whether CPU monitoring thread is created to enrich timeout exceptions with additional diagnostic. Default value is true.
        /// </summary>
        internal bool? EnableCpuMonitor { get; set; }

        internal CosmosClientConfiguration Clone()
        {
            CosmosClientConfiguration cloneConfiguration = (CosmosClientConfiguration)MemberwiseClone();
            return cloneConfiguration;
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
            this.MaxConnectionLimit = CosmosClientConfiguration.DefaultMaxConcurrentConnectionLimit;
            this.RequestTimeout = CosmosClientConfiguration.DefaultRequestTimeout;
            this.ConnectionMode = CosmosClientConfiguration.DefaultConnectionMode;
            this.ConnectionProtocol = CosmosClientConfiguration.DefaultProtocol;
            this.ApiType = CosmosClientConfiguration.DefaultApiType;
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
        private class ClientConfigurationJsonConverter : JsonConverter
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
