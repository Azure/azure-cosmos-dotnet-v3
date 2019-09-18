//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Net;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// This is a Builder class that creates a cosmos client
    /// </summary>
    public class CosmosClientBuilder
    {
        private readonly CosmosClientOptions clientOptions = new CosmosClientOptions();
        private readonly string accountEndpoint;
        private readonly string accountKey;

        /// <summary>
        /// Initialize a new CosmosConfiguration class that holds all the properties the CosmosClient requires.
        /// </summary>
        /// <param name="accountEndpoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="authKeyOrResourceToken">The key to the account or resource token.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
        ///     authKeyOrResourceToken: "SuperSecretKey");
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> with a ConsistencyLevel and a list of preferred locations.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
        ///     authKeyOrResourceToken: "SuperSecretKey")
        /// .WithConsistencyLevel(ConsistencyLevel.Strong)
        /// .WithApplicationRegion("East US 2");
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClientBuilder(
            string accountEndpoint,
            string authKeyOrResourceToken)
        {
            if (accountEndpoint == null)
            {
                throw new ArgumentNullException(nameof(CosmosClientBuilder.accountEndpoint));
            }

            if (authKeyOrResourceToken == null)
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
            }

            this.accountEndpoint = accountEndpoint;
            this.accountKey = authKeyOrResourceToken;
        }

        /// <summary>
        /// Extracts the account endpoint and key from the connection string.
        /// </summary>
        /// <example>"AccountEndpoint=https://mytestcosmosaccount.documents.azure.com:443/;AccountKey={SecretAccountKey};"</example>
        /// <param name="connectionString">The connection string must contain AccountEndpoint and AccountKey or ResourceToken.</param>
        public CosmosClientBuilder(string connectionString)
        {
            if (connectionString == null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            this.accountEndpoint = CosmosClientOptions.GetAccountEndpoint(connectionString);
            this.accountKey = CosmosClientOptions.GetAccountKey(connectionString);
        }

        /// <summary>
        /// A method to create the cosmos client
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        /// <returns>An instance of <see cref="CosmosClient"/>.</returns>
        public CosmosClient Build()
        {
            DefaultTrace.TraceInformation($"CosmosClientBuilder.Build with configuration: {this.clientOptions.GetSerializedConfiguration()}");
            return new CosmosClient(this.accountEndpoint, this.accountKey, this.clientOptions);
        }

        /// <summary>
        /// A method to create the cosmos client
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        internal virtual CosmosClient Build(DocumentClient documentClient)
        {
            DefaultTrace.TraceInformation($"CosmosClientBuilder.Build(DocumentClient) with configuration: {this.clientOptions.GetSerializedConfiguration()}");
            return new CosmosClient(this.accountEndpoint, this.accountKey, this.clientOptions, documentClient);
        }

        /// <summary>
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="applicationName">A string to use as suffix in the User Agent.</param>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        public CosmosClientBuilder WithApplicationName(string applicationName)
        {
            this.clientOptions.ApplicationName = applicationName;
            return this;
        }

        /// <summary>
        /// Set the preferred geo-replicated region to be used in the Azure Cosmos DB service. 
        /// </summary>
        /// <param name="applicationRegion">Azure region where application is running. <see cref="Regions"/> lists valid Cosmos DB regions.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> with a of preferred region.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
        ///     authKeyOrResourceToken: "SuperSecretKey")
        /// .WithApplicationRegion("East US 2");
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ApplicationRegion"/>
        public CosmosClientBuilder WithApplicationRegion(string applicationRegion)
        {
            this.clientOptions.ApplicationRegion = applicationRegion;
            return this;
        }

        /// <summary>
        /// Limits the operations to the provided endpoint on the CosmosClientBuilder constructor.
        /// </summary>
        /// <param name="limitToEndpoint">Whether operations are limited to the endpoint or not.</param>
        /// <value>Default value is false.</value>
        /// <remarks>
        /// When the value of <paramref name="limitToEndpoint"/> is false, the SDK will automatically discover all account write and read regions, and use them when the configured application region is not available.
        /// When set to true, availability is limited to the endpoint specified on the CosmosClientBuilder constructor.
        /// Using <see cref="WithApplicationRegion(string)"/> is not allowed when the value is true. </remarks>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> to limit the endpoint to East US.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos-eastus.documents.azure.com:443/",
        ///     authKeyOrResourceToken: "SuperSecretKey")
        /// .WithLimitToEndpoint(true);
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/high-availability"/>
        /// <seealso cref="CosmosClientOptions.LimitToEndpoint"/>
        public CosmosClientBuilder WithLimitToEndpoint(bool limitToEndpoint)
        {
            this.clientOptions.LimitToEndpoint = limitToEndpoint;
            return this;
        }

        /// <summary>
        /// Sets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="requestTimeout">A time to use as timeout for operations.</param>
        /// <value>Default value is 60 seconds.</value>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.RequestTimeout"/>
        public CosmosClientBuilder WithRequestTimeout(TimeSpan requestTimeout)
        {
            this.clientOptions.RequestTimeout = requestTimeout;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Direct. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ConnectionMode"/>
        public CosmosClientBuilder WithConnectionModeDirect()
        {
            this.clientOptions.ConnectionMode = ConnectionMode.Direct;
            this.clientOptions.ConnectionProtocol = Protocol.Tcp;

            return this;
        }

        /// <summary>
        /// Sets the connection mode to Direct. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="idleTcpConnectionTimeout">
        /// Controls the amount of idle time after which unused connections are closed.
        /// By default, idle connections are kept open indefinitely. Value must be greater than or equal to 10 minutes. Recommended values are between 20 minutes and 24 hours.
        /// Mainly useful for sparse infrequent access to a large database account.
        /// </param>
        /// <param name="openTcpConnectionTimeout">
        /// Controls the amount of time allowed for trying to establish a connection.
        /// The default timeout is 5 seconds. Recommended values are greater than or equal to 5 seconds.
        /// When the time elapses, the attempt is cancelled and an error is returned. Longer timeouts will delay retries and failures.
        /// </param>
        /// <param name="maxRequestsPerTcpConnection">
        /// Controls the number of requests allowed simultaneously over a single TCP connection. When more requests are in flight simultaneously, the direct/TCP client will open additional connections.
        /// The default settings allow 30 simultaneous requests per connection.
        /// Do not set this value lower than 4 requests per connection or higher than 50-100 requests per connection.       
        /// The former can lead to a large number of connections to be created. 
        /// The latter can lead to head of line blocking, high latency and timeouts.
        /// Applications with a very high degree of parallelism per connection, with large requests or responses, or with very tight latency requirements might get better performance with 8-16 requests per connection.
        /// </param>
        /// <param name="maxTcpConnectionsPerEndpoint">
        /// Controls the maximum number of TCP connections that may be opened to each Cosmos DB back-end.
        /// Together with MaxRequestsPerTcpConnection, this setting limits the number of requests that are simultaneously sent to a single Cosmos DB back-end(MaxRequestsPerTcpConnection x MaxTcpConnectionPerEndpoint).
        /// The default value is 65,535. Value must be greater than or equal to 16.
        /// </param>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ConnectionMode"/>
        internal CosmosClientBuilder WithConnectionModeDirect(TimeSpan? idleTcpConnectionTimeout = null,
            TimeSpan? openTcpConnectionTimeout = null,
            int? maxRequestsPerTcpConnection = null,
            int? maxTcpConnectionsPerEndpoint = null)
        {
            this.clientOptions.IdleTcpConnectionTimeout = idleTcpConnectionTimeout;
            this.clientOptions.OpenTcpConnectionTimeout = openTcpConnectionTimeout;
            this.clientOptions.MaxRequestsPerTcpConnection = maxRequestsPerTcpConnection;
            this.clientOptions.MaxTcpConnectionsPerEndpoint = maxTcpConnectionsPerEndpoint;

            this.clientOptions.ConnectionMode = ConnectionMode.Direct;
            this.clientOptions.ConnectionProtocol = Protocol.Tcp;

            return this;
        }

        /// <summary>
        /// This can be used to weaken the database account consistency level for read operations.
        /// If this is not set the database account consistency level will be used for all requests.
        /// </summary>
        /// <param name="consistencyLevel">The desired consistency level for the client.</param>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        public CosmosClientBuilder WithConsistencyLevel(Cosmos.ConsistencyLevel consistencyLevel)
        {
            this.clientOptions.ConsistencyLevel = consistencyLevel;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Gateway. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxConnectionLimit">The number specifies the time to wait for response to come back from network peer. Default is 60 connections</param>
        /// <param name="webProxy">Get or set the proxy information used for web requests.</param>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ConnectionMode"/>
        /// <seealso cref="CosmosClientOptions.GatewayModeMaxConnectionLimit"/>
        public CosmosClientBuilder WithConnectionModeGateway(int? maxConnectionLimit = null,
            IWebProxy webProxy = null)
        {
            this.clientOptions.ConnectionMode = ConnectionMode.Gateway;
            this.clientOptions.ConnectionProtocol = Protocol.Https;

            if (maxConnectionLimit.HasValue)
            {
                this.clientOptions.GatewayModeMaxConnectionLimit = maxConnectionLimit.Value;
            }

            this.clientOptions.WebProxy = webProxy;

            return this;
        }

        /// <summary>
        /// Sets an array of custom handlers to the request. The handlers will be chained in
        /// the order listed. The InvokerHandler.InnerHandler is required to be null to allow the
        /// pipeline to chain the handlers.
        /// </summary>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <param name="customHandlers">A list of <see cref="RequestHandler"/> instaces to add to the pipeline.</param>
        /// <seealso cref="CosmosClientOptions.CustomHandlers"/>
        public CosmosClientBuilder AddCustomHandlers(params RequestHandler[] customHandlers)
        {
            foreach (RequestHandler handler in customHandlers)
            {
                if (handler.InnerHandler != null)
                {
                    throw new ArgumentException(nameof(customHandlers) + " requires all DelegatingHandler.InnerHandler to be null. The CosmosClient uses the inner handler in building the pipeline.");
                }

                this.clientOptions.CustomHandlers.Add(handler);
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
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#429">Handle rate limiting/request rate too large</see>.
        /// </para>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.MaxRetryWaitTimeOnRateLimitedRequests"/>
        /// <seealso cref="CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests"/>
        public CosmosClientBuilder WithThrottlingRetryOptions(TimeSpan maxRetryWaitTimeOnThrottledRequests,
            int maxRetryAttemptsOnThrottledRequests)
        {
            this.clientOptions.MaxRetryWaitTimeOnRateLimitedRequests = maxRetryWaitTimeOnThrottledRequests;
            this.clientOptions.MaxRetryAttemptsOnRateLimitedRequests = maxRetryAttemptsOnThrottledRequests;
            return this;
        }

        /// <summary>
        /// Set a custom serializer option. 
        /// </summary>
        /// <param name="cosmosSerializerOptions">The custom class that implements <see cref="CosmosSerializer"/> </param>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        /// <seealso cref="CosmosSerializer"/>
        /// <seealso cref="CosmosClientOptions.SerializerOptions"/>
        public CosmosClientBuilder WithSerializerOptions(CosmosSerializationOptions cosmosSerializerOptions)
        {
            this.clientOptions.SerializerOptions = cosmosSerializerOptions;
            return this;
        }

        /// <summary>
        /// Set a custom JSON serializer. 
        /// </summary>
        /// <param name="cosmosJsonSerializer">The custom class that implements <see cref="CosmosSerializer"/> </param>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        /// <seealso cref="CosmosSerializer"/>
        /// <seealso cref="CosmosClientOptions.Serializer"/>
        public CosmosClientBuilder WithCustomSerializer(CosmosSerializer cosmosJsonSerializer)
        {
            this.clientOptions.Serializer = cosmosJsonSerializer;
            return this;
        }

        /// <summary>
        /// Allows optimistic batching of requests to service. Setting this option might impact the latency of the operations. Hence this option is recommended for non-latency sensitive scenarios only.
        /// </summary>
        /// <param name="enabled">Whether <see cref="CosmosClientOptions.AllowBulkExecution"/> is enabled.</param>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        /// <seealso cref="CosmosClientOptions.AllowBulkExecution"/>
#if PREVIEW
        public
#else
        internal
#endif
        CosmosClientBuilder WithBulkexecution(bool enabled)
        {
            this.clientOptions.AllowBulkExecution = enabled;
            return this;
        }

        /// <summary>
        /// The event handler to be invoked before the request is sent.
        /// </summary>
        internal CosmosClientBuilder WithSendingRequestEventArgs(EventHandler<SendingRequestEventArgs> sendingRequestEventArgs)
        {
            this.clientOptions.SendingRequestEventArgs = sendingRequestEventArgs;
            return this;
        }

        /// <summary>
        /// (Optional) transport interceptor factory
        /// </summary>
        internal CosmosClientBuilder WithTransportClientHandlerFactory(Func<TransportClient, TransportClient> transportClientHandlerFactory)
        {
            this.clientOptions.TransportClientHandlerFactory = transportClientHandlerFactory;
            return this;
        }

        /// <summary>
        /// ApiType for the account
        /// </summary>
        internal CosmosClientBuilder WithApiType(ApiType apiType)
        {
            this.clientOptions.ApiType = apiType;
            return this;
        }

        /// <summary>
        /// Specify a store client factory to use for all transport requests for cosmos client.
        /// </summary>
        /// <remarks>
        /// This method enables transport client sharing among multiple cosmos client instances inside a single process.
        /// </remarks>
        /// <param name="storeClientFactory">Instance of store client factory to use to create transport client for an instance of cosmos client.</param>
        internal CosmosClientBuilder WithStoreClientFactory(IStoreClientFactory storeClientFactory)
        {
            this.clientOptions.StoreClientFactory = storeClientFactory;
            return this;
        }

        /// <summary>
        /// Disables CPU monitoring for transport client which will inhibit troubleshooting of timeout exceptions.
        /// </summary>
        internal CosmosClientBuilder WithCpuMonitorDisabled()
        {
            this.clientOptions.EnableCpuMonitor = false;
            return this;
        }

        internal CosmosClientBuilder WithRetryWithOptions(
            int? initialRetryForRetryWithMilliseconds,
            int? maximumRetryForRetryWithMilliseconds,
            int? randomSaltForRetryWithMilliseconds,
            int? totalWaitTimeForRetryWithMilliseconds)
        {
            this.clientOptions.InitialRetryForRetryWithMilliseconds = initialRetryForRetryWithMilliseconds;
            this.clientOptions.MaximumRetryForRetryWithMilliseconds = maximumRetryForRetryWithMilliseconds;
            this.clientOptions.RandomSaltForRetryWithMilliseconds = randomSaltForRetryWithMilliseconds;
            this.clientOptions.TotalWaitTimeForRetryWithMilliseconds = totalWaitTimeForRetryWithMilliseconds;
            return this;
        }
    }
}
