//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.FaultInjection;
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
        private readonly AzureKeyCredential azureKeyCredential;
        private readonly TokenCredential tokenCredential;

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
            if (string.IsNullOrEmpty(accountEndpoint))
            {
                throw new ArgumentNullException(nameof(CosmosClientBuilder.accountEndpoint));
            }

            if (string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
            }

            this.accountEndpoint = accountEndpoint;
            this.accountKey = authKeyOrResourceToken;
        }

        /// <summary>
        /// Initialize a new CosmosConfiguration class that holds all the properties the CosmosClient requires with the account endpoint URI string and AzureKeyCredential.
        /// AzureKeyCredential enables changing/updating master-key/ResourceToken while CosmosClient is still in use. 
        /// 
        /// </summary>
        /// <param name="accountEndpoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="authKeyOrResourceTokenCredential">AzureKeyCredential with master-key or resource token.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
        ///     authKeyOrResourceTokenCredential: new AzureKeyCredential("SuperSecretKey"));
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
        ///     authKeyOrResourceTokenCredential: new AzureKeyCredential("SuperSecretKey"))
        /// .WithConsistencyLevel(ConsistencyLevel.Strong)
        /// .WithApplicationRegion("East US 2");
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>AzureKeyCredential enables changing/updating master-key/ResourceToken whle CosmosClient is still in use.</remarks> 
        public CosmosClientBuilder(
            string accountEndpoint,
            AzureKeyCredential authKeyOrResourceTokenCredential)
        {
            if (string.IsNullOrEmpty(accountEndpoint))
            {
                throw new ArgumentNullException(nameof(CosmosClientBuilder.accountEndpoint));
            }

            this.accountEndpoint = accountEndpoint;
            this.azureKeyCredential = authKeyOrResourceTokenCredential ?? throw new ArgumentNullException(nameof(authKeyOrResourceTokenCredential));
        }

        /// <summary>
        /// Extracts the account endpoint and key from the connection string.
        /// </summary>
        /// <example>"AccountEndpoint=https://mytestcosmosaccount.documents.azure.com:443/;AccountKey={SecretAccountKey};"</example>
        /// <param name="connectionString">The connection string must contain AccountEndpoint and AccountKey or ResourceToken.</param>
        /// <remarks>
        /// Emulator: To ignore SSL Certificate please suffix connectionstring with "DisableServerCertificateValidation=True;". 
        /// When CosmosClientOptions.HttpClientFactory is used, SSL certificate needs to be handled appropriately.
        /// NOTE: DO NOT use this flag in production (only for emulator)
        /// </remarks>
        public CosmosClientBuilder(string connectionString)
        {
            if (connectionString == null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            this.accountEndpoint = CosmosClientOptions.GetAccountEndpoint(connectionString);
            this.accountKey = CosmosClientOptions.GetAccountKey(connectionString);
            
            this.clientOptions = CosmosClientOptions.GetCosmosClientOptionsWithCertificateFlag(connectionString, this.clientOptions);
        }

        /// <summary>
        /// Initializes a new <see cref="CosmosClientBuilder"/> with a <see cref="TokenCredential"/> instance.
        /// </summary>
        /// <param name="accountEndpoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="tokenCredential">An instance of <see cref="TokenCredential"/></param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> using a <see cref="TokenCredential"/>.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
        ///     tokenCredential: new DefaultAzureCredential());
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClientBuilder(
            string accountEndpoint, 
            TokenCredential tokenCredential)
        {
            if (string.IsNullOrEmpty(accountEndpoint))
            {
                throw new ArgumentNullException(nameof(CosmosClientBuilder.accountEndpoint));
            }

            this.accountEndpoint = accountEndpoint;
            this.tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(CosmosClientBuilder.tokenCredential));
        }

        /// <summary>
        /// A method to create the cosmos client
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3">performance guide</see>.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// The returned reference doesn't guarantee credentials or connectivity validations because creation doesn't make any network calls.
        /// </remarks>
        /// <returns>An instance of <see cref="CosmosClient"/>.</returns>
        public CosmosClient Build()
        {
            DefaultTrace.TraceInformation($"CosmosClientBuilder.Build with configuration: {this.clientOptions.GetSerializedConfiguration()}");

            if (this.tokenCredential != null)
            {
                return new CosmosClient(this.accountEndpoint, this.tokenCredential, this.clientOptions);
            }

            if (this.azureKeyCredential != null)
            {
                return new CosmosClient(this.accountEndpoint, this.azureKeyCredential, this.clientOptions);
            }

            return new CosmosClient(this.accountEndpoint, this.accountKey, this.clientOptions);
        }

        /// <summary>
        /// A method to create the cosmos client and initialize the provided containers.
        /// In addition to that it initializes the client with containers provided i.e The SDK warms up the caches and 
        /// connections before the first call to the service is made. Use this to obtain lower latency while startup of your application.
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3">performance guide</see>.
        /// </summary>
        /// <param name="containers">Containers to be initialized identified by it's database name and container name.</param>
        /// <param name="cancellationToken">(Optional) Cancellation Token</param>
        /// <returns>
        /// A CosmosClient object.
        /// </returns>
        public Task<CosmosClient> BuildAndInitializeAsync(IReadOnlyList<(string databaseId, string containerId)> containers, CancellationToken cancellationToken = default)
        {
            if (this.tokenCredential != null)
            {
                return CosmosClient.CreateAndInitializeAsync(this.accountEndpoint, this.tokenCredential, containers, this.clientOptions, cancellationToken);
            }

            if (this.azureKeyCredential != null)
            {
                return CosmosClient.CreateAndInitializeAsync(this.accountEndpoint, this.azureKeyCredential, containers, this.clientOptions, cancellationToken);
            }

            return CosmosClient.CreateAndInitializeAsync(this.accountEndpoint, this.accountKey, containers, this.clientOptions, cancellationToken);
        }

        /// <summary>
        /// A method to create the cosmos client
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3">performance guide</see>.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// The returned reference doesn't guarantee credentials or connectivity validations because creation doesn't make any network calls.
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
        /// Set the preferred regions for geo-replicated database accounts in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="applicationPreferredRegions">A list of preferred Azure regions used for SDK to define failover order.</param>
        /// <remarks>
        ///  This function is an alternative to <see cref="WithApplicationRegion"/>, either one can be set but not both.
        /// </remarks>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> with a of preferred regions.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
        ///     authKeyOrResourceToken: "SuperSecretKey")
        /// .WithApplicationPreferredRegions(new[] {Regions.EastUS, Regions.EastUS2});
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ApplicationPreferredRegions"/>
        public CosmosClientBuilder WithApplicationPreferredRegions(IReadOnlyList<string> applicationPreferredRegions)
        {
            this.clientOptions.ApplicationPreferredRegions = applicationPreferredRegions;
            return this;
        }

        /// <summary>
        /// Sets the custom endpoints to use for account initialization for geo-replicated database accounts in the Azure Cosmos DB service. 
        /// During the CosmosClient initialization the account information, including the available regions, is obtained from the <see cref="CosmosClient.Endpoint"/>.
        /// Should the global endpoint become inaccessible, the CosmosClient will attempt to obtain the account information issuing requests to the custom endpoints
        /// provided in the customAccountEndpoints list.
        /// </summary>
        /// <param name="customAccountEndpoints">An instance of <see cref="IEnumerable{T}"/> of Uri containing the custom private endpoints for the cosmos db account.</param>
        /// <remarks>
        ///  This function is optional and is recommended for implementation when a customer has configured one or more endpoints with a custom DNS
        ///  hostname (instead of accountname-region.documents.azure.com) etc. for their Cosmos DB account.
        /// </remarks>
        /// <example>
        /// The example below creates a new instance of <see cref="CosmosClientBuilder"/> with the regional endpoints.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
        ///     authKeyOrResourceToken: "SuperSecretKey")
        /// .WithCustomAccountEndpoints(new HashSet<Uri>()
        ///     { 
        ///         new Uri("https://region-1.documents-test.windows-int.net:443/"),
        ///         new Uri("https://region-2.documents-test.windows-int.net:443/") 
        ///     });
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.AccountInitializationCustomEndpoints"/>
        public CosmosClientBuilder WithCustomAccountEndpoints(IEnumerable<Uri> customAccountEndpoints)
        {
            this.clientOptions.AccountInitializationCustomEndpoints = customAccountEndpoints;
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/high-availability">High availability</seealso>
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
        /// For more information, see <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3#direct-connection">Connection policy: Use direct connection mode</see>.
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
        /// <param name="portReuseMode">
        /// (Direct/TCP) Controls the client port reuse policy used by the transport stack.
        /// The default value is PortReuseMode.ReuseUnicastPort.
        /// </param>
        /// /// <param name="enableTcpConnectionEndpointRediscovery">
        /// (Direct/TCP) Controls the address cache refresh on TCP connection reset notification.
        /// The default value is false.
        /// </param>
        /// <remarks>
        /// For more information, see <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ConnectionMode"/>
        public CosmosClientBuilder WithConnectionModeDirect(TimeSpan? idleTcpConnectionTimeout = null,
            TimeSpan? openTcpConnectionTimeout = null,
            int? maxRequestsPerTcpConnection = null,
            int? maxTcpConnectionsPerEndpoint = null,
            Cosmos.PortReuseMode? portReuseMode = null,
            bool? enableTcpConnectionEndpointRediscovery = null)
        {
            this.clientOptions.IdleTcpConnectionTimeout = idleTcpConnectionTimeout;
            this.clientOptions.OpenTcpConnectionTimeout = openTcpConnectionTimeout;
            this.clientOptions.MaxRequestsPerTcpConnection = maxRequestsPerTcpConnection;
            this.clientOptions.MaxTcpConnectionsPerEndpoint = maxTcpConnectionsPerEndpoint;
            this.clientOptions.PortReuseMode = portReuseMode;
            if (enableTcpConnectionEndpointRediscovery.HasValue)
            {
                this.clientOptions.EnableTcpConnectionEndpointRediscovery = enableTcpConnectionEndpointRediscovery.Value;
            }

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
        /// Sets the priority level for requests created using cosmos client.
        /// </summary>
        /// <remarks>
        /// If priority level is also set at request level in <see cref="RequestOptions.PriorityLevel"/>, that priority is used.
        /// If <see cref="WithBulkExecution(bool)"/> is set to true, priority level set on the CosmosClient is used.
        /// </remarks>
        /// <param name="priorityLevel">The desired priority level for the client.</param>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso href="https://aka.ms/CosmosDB/PriorityBasedExecution"/>
        public CosmosClientBuilder WithPriorityLevel(Cosmos.PriorityLevel priorityLevel)
        {
            this.clientOptions.PriorityLevel = priorityLevel;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Gateway. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxConnectionLimit">The number specifies the number of connections that may be opened simultaneously. Default is 50 connections</param>
        /// <param name="webProxy">Get or set the proxy information used for web requests.</param>
        /// <remarks>
        /// For more information, see <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ConnectionMode"/>
        /// <seealso cref="CosmosClientOptions.GatewayModeMaxConnectionLimit"/>
        public CosmosClientBuilder WithConnectionModeGateway(
            int? maxConnectionLimit = null,
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
        /// Sets the maximum time to wait between retry and the max number of times to retry on throttled requests.
        /// </summary>
        /// <param name="maxRetryWaitTimeOnThrottledRequests">The maximum retry timespan for the Azure Cosmos DB service. Any interval that is smaller than a second will be ignored.</param>
        /// <param name="maxRetryAttemptsOnThrottledRequests">The number specifies the times retry requests for throttled requests.</param>
        /// <para>
        /// When a request fails due to a rate limiting error, the service sends back a response that
        /// contains a value indicating the client should not retry before the time period has
        /// elapsed. This property allows the application to set a maximum wait time for all retry attempts.
        /// If the cumulative wait time exceeds the this value, the client will stop retrying and return the error to the application.
        /// </para>
        /// <para>
        /// For more information, see <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3#429">Handle rate limiting/request rate too large</see>.
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
        public CosmosClientBuilder WithBulkExecution(bool enabled)
        {
            this.clientOptions.AllowBulkExecution = enabled;
            return this;
        }

        /// <summary>
        /// Sets a delegate to use to obtain an HttpClient instance to be used for HTTPS communication.
        /// </summary>
        /// <param name="httpClientFactory">A delegate function to generate instances of HttpClient.</param>
        /// <remarks>
        /// <para>
        /// HTTPS communication is used when <see cref="ConnectionMode"/> is set to <see cref="ConnectionMode.Gateway"/> for all operations and when <see cref="ConnectionMode"/> is <see cref="ConnectionMode.Direct"/> (default) for metadata operations.
        /// </para>
        /// <para>
        /// Useful in scenarios where the application is using a pool of HttpClient instances to be shared, like ASP.NET Core applications with IHttpClientFactory or Blazor WebAssembly applications.
        /// </para>
        /// </remarks>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        /// <seealso cref="CosmosClientOptions.HttpClientFactory"/>
        public CosmosClientBuilder WithHttpClientFactory(Func<HttpClient> httpClientFactory)
        {
            this.clientOptions.HttpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            return this;
        }

        /// <summary>
        /// Gets or sets the boolean to only return the headers and status code in
        /// the Cosmos DB response for write item operation like Create, Upsert, Patch and Replace.
        /// Setting the option to false will cause the response to have a null resource. This reduces networking and CPU load by not sending
        /// the resource back over the network and serializing it on the client.
        /// </summary>
        /// <param name="contentResponseOnWrite">a boolean indicating whether payload will be included in the response or not.</param>
        /// <remarks>
        /// <para>
        /// This option can be overriden by similar property in ItemRequestOptions and TransactionalBatchItemRequestOptions
        /// </para>
        /// </remarks>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        /// <seealso cref="ItemRequestOptions.EnableContentResponseOnWrite"/>
        /// <seealso cref="TransactionalBatchItemRequestOptions.EnableContentResponseOnWrite"/>
        public CosmosClientBuilder WithContentResponseOnWrite(bool contentResponseOnWrite)
        {
            this.clientOptions.EnableContentResponseOnWrite = contentResponseOnWrite;
            return this;
        }

        /// <summary>
        /// Configures the <see cref="CosmosClientBuilder"/> to use System.Text.Json for serialization.
        /// Use <see cref="System.Text.Json.JsonSerializerOptions" /> to use System.Text.Json with a default configuration.
        /// If no options are specified, Newtonsoft.Json will be used for serialization instead.
        /// </summary>
        /// <param name="serializerOptions">An instance of <see cref="System.Text.Json.JsonSerializerOptions"/>
        /// containing the system text json serializer options.</param>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        public CosmosClientBuilder WithSystemTextJsonSerializerOptions(
            System.Text.Json.JsonSerializerOptions serializerOptions)
        {
            this.clientOptions.UseSystemTextJsonSerializerWithOptions = serializerOptions;
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
        /// Sets the ambient Session Container to use for this CosmosClient.
        /// This is used to track session tokens per client for requests made to the store.
        /// </summary>
        internal CosmosClientBuilder WithSessionContainer(ISessionContainer sessionContainer)
        {
            this.clientOptions.SessionContainer = sessionContainer;
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
        /// Availability Stragey to be used for periods of high latency
        /// </summary>
        /// <param name="strategy"></param>
        /// <returns>The CosmosClientBuilder</returns>
#if PREVIEW
        public
#else
        internal
#endif
        CosmosClientBuilder WithAvailabilityStrategy(AvailabilityStrategy strategy)
        {
            this.clientOptions.AvailabilityStrategy = strategy;
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

        /// <summary>
        /// Enabled partition level failover in the SDK
        /// </summary>
        internal CosmosClientBuilder WithPartitionLevelFailoverEnabled()
        {
            this.clientOptions.EnablePartitionLevelFailover = true;
            return this;
        }

        /// <summary>
        /// Enables SDK to inject fault. Used for testing applications.  
        /// </summary>
        /// <param name="chaosInterceptorFactory"></param>
        internal CosmosClientBuilder WithFaultInjection(IChaosInterceptorFactory chaosInterceptorFactory)
        {
            this.clientOptions.ChaosInterceptorFactory = chaosInterceptorFactory;
            return this;
        }

        /// <summary>
        /// Enables SDK to inject fault. Used for testing applications.  
        /// </summary>
        /// <param name="faultInjector"></param>
        /// <returns>>The <see cref="CosmosClientBuilder"/> object</returns>
        public CosmosClientBuilder WithFaultInjection(IFaultInjector faultInjector)
        {
            this.clientOptions.ChaosInterceptorFactory = faultInjector.GetChaosInterceptorFactory();
            return this;
        }

        /// <summary>
        /// To enable LocalQuorum Consistency, i.e. Allows Quorum read with Eventual Consistency Account or with Consistent Prefix Account.
        /// Use By Compute Only
        /// </summary>
        internal CosmosClientBuilder AllowUpgradeConsistencyToLocalQuorum()
        {
            this.clientOptions.EnableUpgradeConsistencyToLocalQuorum = true;
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

        /// <summary>
        /// To enable Telemetry features with corresponding options
        /// </summary>
        /// <param name="options"></param>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        public CosmosClientBuilder WithClientTelemetryOptions(CosmosClientTelemetryOptions options)
        {
            this.clientOptions.CosmosClientTelemetryOptions = options;
            return this;
        }

        /// <summary>
        /// Sets the throughput bucket for requests created using cosmos client.
        /// </summary>
        /// <remarks>
        /// If throughput bucket is also set at request level in <see cref="RequestOptions.ThroughputBucket"/>, that throughput bucket is used.
        /// If <see cref="WithBulkExecution(bool)"/> is set to true, throughput bucket can only be set at client level.
        /// </remarks>
        /// <param name="throughputBucket">The desired throughput bucket for the client.</param>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso href="https://aka.ms/cosmsodb-bucketing"/>
        internal CosmosClientBuilder WithThroughputBucket(int throughputBucket)
        {
            this.clientOptions.ThroughputBucket = throughputBucket;
            return this;
        }
    }
}
