//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data.Common;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines all the configurable options that the CosmosClient requires.
    /// </summary>
    /// <example>
    /// An example on how to configure the serialization option to ignore null values.
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosClientOptions clientOptions = new CosmosClientOptions()
    /// {
    ///     SerializerOptions = new CosmosSerializationOptions(){
    ///         IgnoreNullValues = true
    ///     },
    ///     ConnectionMode = ConnectionMode.Gateway,
    /// };
    /// 
    /// CosmosClient client = new CosmosClient("endpoint", "key", clientOptions);
    /// ]]>
    /// </code>
    /// </example>
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
        private const string ConnectionStringDisableServerCertificateValidation = "DisableServerCertificateValidation";

        private const ApiType DefaultApiType = ApiType.None;

        /// <summary>
        /// Default request timeout
        /// </summary>
        private int gatewayModeMaxConnectionLimit;
        private CosmosSerializationOptions serializerOptions;
        private CosmosSerializer serializerInternal;
        private System.Text.Json.JsonSerializerOptions stjSerializerOptions;

        private ConnectionMode connectionMode;
        private Protocol connectionProtocol;
        private TimeSpan? idleTcpConnectionTimeout;
        private TimeSpan? openTcpConnectionTimeout;
        private int? maxRequestsPerTcpConnection;
        private int? maxTcpConnectionsPerEndpoint;
        private PortReuseMode? portReuseMode;
        private IWebProxy webProxy;
        private Func<HttpClient> httpClientFactory;
        private string applicationName;
        private IFaultInjector faultInjector;

        /// <summary>
        /// Creates a new CosmosClientOptions
        /// </summary>
        public CosmosClientOptions()
        {
            this.GatewayModeMaxConnectionLimit = ConnectionPolicy.Default.MaxConnectionLimit;
            this.RequestTimeout = ConnectionPolicy.Default.RequestTimeout;
            this.TokenCredentialBackgroundRefreshInterval = null;
            this.ConnectionMode = CosmosClientOptions.DefaultConnectionMode;
            this.ConnectionProtocol = CosmosClientOptions.DefaultProtocol;
            this.ApiType = CosmosClientOptions.DefaultApiType;
            this.CustomHandlers = new Collection<RequestHandler>();
            this.CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions();
        }

        /// <summary>
        /// Get or set user-agent suffix to include with every Azure Cosmos DB service interaction.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public string ApplicationName
        {
            get => this.applicationName;
            set
            {
                try
                {
                    HttpRequestMessage dummyMessage = new HttpRequestMessage();
                    dummyMessage.Headers.Add(HttpConstants.HttpHeaders.UserAgent, value);
                }
                catch (FormatException fme)
                {
                    throw new ArgumentException($"Application name '{value}' is invalid.", fme);
                }

                this.applicationName = value;
            }
        }

        /// <summary>
        /// Get or set session container for the client
        /// </summary>
        internal ISessionContainer SessionContainer { get; set; }

        /// <summary>
        /// Gets or sets the location where the application is running. This will influence the SDK's choice for the Azure Cosmos DB service interaction.
        /// </summary>
        /// <remarks>
        /// <para>
        /// During the CosmosClient initialization the account information, including the available regions, is obtained from the <see cref="CosmosClient.Endpoint"/>.
        /// The CosmosClient will use the value of <see cref="ApplicationRegion"/> to populate the preferred list with the account available regions ordered by geographical proximity to the indicated region.
        /// If the value of <see cref="ApplicationRegion"/> is not an available region in the account, the preferred list is still populated following the same mechanism but would not include the indicated region.
        /// </para>
        /// <para>
        /// If during CosmosClient initialization, the <see cref="CosmosClient.Endpoint"/> is not reachable, the CosmosClient will attempt to recover and obtain the account information issuing requests to all <see cref="Regions"/> ordered by proximity to the <see cref="ApplicationRegion"/>.
        /// For more granular control over the selected regions or to define a list based on a custom criteria, use <see cref="ApplicationPreferredRegions"/> instead of <see cref="ApplicationRegion"/>.
        /// </para>
        /// <para>
        /// See also <seealso href="https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-sdk-availability">Diagnose
        /// and troubleshoot the availability of Cosmos SDKs</seealso> for more details.
        /// </para>
        /// <para>
        /// This configuration is an alternative to <see cref="ApplicationPreferredRegions"/>, either one can be set but not both.
        /// </para>
        /// </remarks>
        /// <example>
        /// If an account is configured with multiple regions including West US, East US, and West Europe, configuring a client like the below example would result in the CosmosClient generating a sorted preferred regions based on proximity to East US.
        /// The CosmosClient will send requests to East US, if that region becomes unavailable, it will fallback to West US (second in proximity), and finally to West Europe if West US becomes unavailable.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions()
        /// {
        ///     ApplicationRegion = Regions.EastUS
        /// };
        /// 
        /// CosmosClient client = new CosmosClient("endpoint", "key", clientOptions);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosClientBuilder.WithApplicationRegion(string)"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/high-availability#high-availability-with-cosmos-db-in-the-event-of-regional-outages">High availability on regional outages</seealso>
        public string ApplicationRegion { get; set; }

        /// <summary>
        /// Gets and sets the preferred regions for geo-replicated database accounts in the Azure Cosmos DB service. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// During the CosmosClient initialization the account information, including the available regions, is obtained from the <see cref="CosmosClient.Endpoint"/>.
        /// The CosmosClient will use the value of <see cref="ApplicationPreferredRegions"/> to populate the preferred list with the account available regions that intersect with its value.
        /// If the value of <see cref="ApplicationPreferredRegions"/> contains regions that are not an available region in the account, the values will be ignored. If the these invalid regions are added later to the account, the CosmosClient will use them if they are higher in the preference order.
        /// </para>
        /// <para>
        /// If during CosmosClient initialization, the <see cref="CosmosClient.Endpoint"/> is not reachable, the CosmosClient will attempt to recover and obtain the account information issuing requests to the regions in <see cref="ApplicationPreferredRegions"/> in the order that they are listed.
        /// </para>
        /// <para>
        /// See also <seealso href="https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-sdk-availability">Diagnose
        /// and troubleshoot the availability of Cosmos SDKs</seealso> for more details.
        /// </para>
        /// <para>
        /// This configuration is an alternative to <see cref="ApplicationRegion"/>, either one can be set but not both.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions()
        /// {
        ///     ApplicationPreferredRegions = new List<string>(){ Regions.EastUS, Regions.WestUS }
        /// };
        /// 
        /// CosmosClient client = new CosmosClient("endpoint", "key", clientOptions);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/high-availability#high-availability-with-cosmos-db-in-the-event-of-regional-outages">High availability on regional outages</seealso>
        public IReadOnlyList<string> ApplicationPreferredRegions { get; set; }

        /// <summary>
        /// Gets and sets the custom endpoints to use for account initialization for geo-replicated database accounts in the Azure Cosmos DB service. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// During the CosmosClient initialization the account information, including the available regions, is obtained from the <see cref="CosmosClient.Endpoint"/>.
        /// Should the global endpoint become inaccessible, the CosmosClient will attempt to obtain the account information issuing requests to the custom endpoints provided in <see cref="AccountInitializationCustomEndpoints"/>.
        /// </para>
        /// <para>
        /// Nevertheless, this parameter remains optional and is recommended for implementation when a customer has configured an endpoint with a custom DNS hostname
        /// (instead of accountname-region.documents.azure.com) etc. for their Cosmos DB account.
        /// </para>
        /// <para>
        /// See also <seealso href="https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-sdk-availability">Diagnose
        /// and troubleshoot the availability of Cosmos SDKs</seealso> for more details.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions()
        /// {
        ///     AccountInitializationCustomEndpoints = new HashSet<Uri>()
        ///     { 
        ///         new Uri("custom.p-1.documents.azure.com"),
        ///         new Uri("custom.p-2.documents.azure.com") 
        ///     }
        /// };
        /// 
        /// CosmosClient client = new CosmosClient("endpoint", "key", clientOptions);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/high-availability#high-availability-with-cosmos-db-in-the-event-of-regional-outages">High availability on regional outages</seealso>
        public IEnumerable<Uri> AccountInitializationCustomEndpoints { get; set; }

        /// <summary>
        /// Get or set the maximum number of concurrent connections allowed for the target
        /// service endpoint in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This setting is only applicable in Gateway mode.
        /// </remarks>
        /// <value>Default value is 50.</value>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeGateway(int?, IWebProxy)"/>
        public int GatewayModeMaxConnectionLimit
        {
            get => this.gatewayModeMaxConnectionLimit;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                if (this.HttpClientFactory != null && value != ConnectionPolicy.Default.MaxConnectionLimit)
                {
                    throw new ArgumentException($"{nameof(this.httpClientFactory)} can not be set along with {nameof(this.GatewayModeMaxConnectionLimit)}. This must be set on the HttpClientHandler.MaxConnectionsPerServer property.");
                }

                this.gatewayModeMaxConnectionLimit = value;
            }
        }

        /// <summary>
        /// Gets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// The number specifies the time to wait for response to come back from network peer.
        /// </summary>
        /// <value>Default value is 6 seconds.</value>
        /// <seealso cref="CosmosClientBuilder.WithRequestTimeout(TimeSpan)"/>
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// The SDK does a background refresh based on the time interval set to refresh the token credentials.
        /// This avoids latency issues because the old token is used until the new token is retrieved.
        /// </summary>
        /// <remarks>
        /// The recommended minimum value is 5 minutes. The default value is 50% of the token expire time.
        /// </remarks>
        public TimeSpan? TokenCredentialBackgroundRefreshInterval { get; set; }

        /// <summary>
        /// Gets the handlers run before the process
        /// </summary>
        /// <seealso cref="CosmosClientBuilder.AddCustomHandlers(RequestHandler[])"/>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        public Collection<RequestHandler> CustomHandlers { get; }

        /// <summary>
        /// Get or set the connection mode used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Cosmos.ConnectionMode.Direct"/>
        /// </value>
        /// <remarks>
        /// For more information, see <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeDirect()"/>
        /// <seealso cref="CosmosClientBuilder.WithConnectionModeGateway(int?, IWebProxy)"/>
        public ConnectionMode ConnectionMode
        {
            get => this.connectionMode;
            set
            {
                if (value == ConnectionMode.Gateway)
                {
                    this.ConnectionProtocol = Protocol.Https;
                }
                else if (value == ConnectionMode.Direct)
                {
                    this.connectionProtocol = Protocol.Tcp;
                }

                this.ValidateDirectTCPSettings();
                this.connectionMode = value;
            }
        }

        /// <summary>
        /// This can be used to weaken the database account consistency level for read operations.
        /// If this is not set the database account consistency level will be used for all requests.
        /// </summary>
        public ConsistencyLevel? ConsistencyLevel { get; set; }

        /// <summary>
        /// Sets the priority level for requests created using cosmos client.
        /// </summary>
        /// <remarks>
        /// If priority level is also set at request level in <see cref="RequestOptions.PriorityLevel"/>, that priority is used.
        /// If <see cref="AllowBulkExecution"/> is set to true in CosmosClientOptions, priority level set on the CosmosClient is used.
        /// </remarks>
        /// <seealso href="https://aka.ms/CosmosDB/PriorityBasedExecution"/>
        public PriorityLevel? PriorityLevel { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retries in the case where the request fails
        /// because the Azure Cosmos DB service has applied rate limiting on the client.
        /// </summary>
        /// <value>
        /// The default value is 9. This means in the case where the request is rate limited,
        /// the same request will be issued for a maximum of 10 times to the server before
        /// an error is returned to the application.
        ///
        /// If the value of this property is set to 0, there will be no automatic retry on rate
        /// limiting requests from the client and the exception needs to be handled at the
        /// application level.
        /// </value>
        /// <remarks>
        /// <para>
        /// When a client is sending requests faster than the allowed rate,
        /// the service will return HttpStatusCode 429 (Too Many Requests) to rate limit the client. The current
        /// implementation in the SDK will then wait for the amount of time the service tells it to wait and
        /// retry after the time has elapsed.
        /// </para>
        /// <para>
        /// For more information, see <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips#throughput">Handle rate limiting/request rate too large</see>.
        /// </para>
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithThrottlingRetryOptions(TimeSpan, int)"/>
        public int? MaxRetryAttemptsOnRateLimitedRequests { get; set; }

        /// <summary>
        /// Gets or sets the maximum retry time in seconds for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The default value is 30 seconds. 
        /// </value>
        /// <remarks>
        /// <para>
        /// The minimum interval is seconds. Any interval that is smaller will be ignored.
        /// </para>
        /// <para>
        /// When a request fails due to a rate limiting error, the service sends back a response that
        /// contains a value indicating the client should not retry before the <see cref="Microsoft.Azure.Cosmos.CosmosException.RetryAfter"/> time period has
        /// elapsed.
        ///
        /// This property allows the application to set a maximum wait time for all retry attempts.
        /// If the cumulative wait time exceeds the this value, the client will stop retrying and return the error to the application.
        /// </para>
        /// <para>
        /// For more information, see <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips#throughput">Handle rate limiting/request rate too large</see>.
        /// </para>
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithThrottlingRetryOptions(TimeSpan, int)"/>
        public TimeSpan? MaxRetryWaitTimeOnRateLimitedRequests { get; set; }

        /// <summary>
        /// Gets or sets the boolean to only return the headers and status code in
        /// the Cosmos DB response for write item operation like Create, Upsert, Patch and Replace.
        /// Setting the option to false will cause the response to have a null resource. This reduces networking and CPU load by not sending
        /// the resource back over the network and serializing it on the client.
        /// </summary>
        /// <remarks>
        /// <para>This is optimal for workloads where the returned resource is not used.</para>
        /// <para>This option can be overriden by similar property in ItemRequestOptions and TransactionalBatchItemRequestOptions</para>
        /// </remarks>
        /// <seealso cref="CosmosClientBuilder.WithContentResponseOnWrite(bool)"/>
        /// <seealso cref="ItemRequestOptions.EnableContentResponseOnWrite"/>
        /// <seealso cref="TransactionalBatchItemRequestOptions.EnableContentResponseOnWrite"/>
        public bool? EnableContentResponseOnWrite { get; set; }

        /// <summary>
        /// Sets the <see cref="System.Text.Json.JsonSerializerOptions"/> for the System.Text.Json serializer.
        /// Note that if this option is provided, then the SDK will use the System.Text.Json as the default serializer and set
        /// the serializer options as the constructor args.
        /// </summary>
        /// <example>
        /// An example on how to configure the System.Text.Json serializer options to ignore null values
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions()
        /// {
        ///     UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions()
        ///     {
        ///         DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        ///     }
        /// };
        /// 
        /// CosmosClient client = new CosmosClient("endpoint", "key", clientOptions);
        /// ]]>
        /// </code>
        /// </example>
        public System.Text.Json.JsonSerializerOptions UseSystemTextJsonSerializerWithOptions
        {
            get => this.stjSerializerOptions;
            set
            {
                if (this.Serializer != null || this.SerializerOptions != null)
                {
                    throw new ArgumentException(
                        $"{nameof(this.UseSystemTextJsonSerializerWithOptions)} is not compatible with {nameof(this.Serializer)} or {nameof(this.SerializerOptions)}. Only one can be set.  ");
                }

                this.stjSerializerOptions = value;
                this.serializerInternal = new CosmosSystemTextJsonSerializer(
                    this.stjSerializerOptions);
            }
        }

        /// <summary>
        /// Gets or sets the advanced replica selection flag. The advanced replica selection logic keeps track of the replica connection
        /// status, and based on status, it prioritizes the replicas which show healthy stable connections, so that the requests can be sent
        /// confidently to the particular replica. This helps the cosmos client to become more resilient and effective to any connectivity issues.
        /// The default value for this parameter is 'false'.
        /// </summary>
        /// <remarks>
        /// <para>This is optimal for latency-sensitive workloads. Does not apply if <see cref="ConnectionMode.Gateway"/> is used.</para>
        /// </remarks>
        internal bool? EnableAdvancedReplicaSelectionForTcp { get; set; }

        /// <summary>
        /// (Direct/TCP) Controls the amount of idle time after which unused connections are closed.
        /// </summary>
        /// <value>
        /// By default, idle connections are kept open indefinitely. Value must be greater than or equal to 10 minutes. Recommended values are between 20 minutes and 24 hours.
        /// </value>
        /// <remarks>
        /// Mainly useful for sparse infrequent access to a large database account.
        /// </remarks>
        public TimeSpan? IdleTcpConnectionTimeout
        {
            get => this.idleTcpConnectionTimeout;
            set
            {
                this.idleTcpConnectionTimeout = value;
                this.ValidateDirectTCPSettings();
            }
        }

        /// <summary>
        /// (Direct/TCP) Controls the amount of time allowed for trying to establish a connection.
        /// </summary>
        /// <value>
        /// The default timeout is 5 seconds. For latency sensitive applications that prefer to retry faster, a recommended value of 1 second can be used.
        /// </value>
        /// <remarks>
        /// When the time elapses, the attempt is cancelled and an error is returned. Longer timeouts will delay retries and failures.
        /// </remarks>
        public TimeSpan? OpenTcpConnectionTimeout
        {
            get => this.openTcpConnectionTimeout;
            set
            {
                this.openTcpConnectionTimeout = value;
                this.ValidateDirectTCPSettings();
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
        public int? MaxRequestsPerTcpConnection
        {
            get => this.maxRequestsPerTcpConnection;
            set
            {
                this.maxRequestsPerTcpConnection = value;
                this.ValidateDirectTCPSettings();
            }
        }

        /// <summary>
        /// (Direct/TCP) Controls the maximum number of TCP connections that may be opened to each Cosmos DB back-end.
        /// Together with MaxRequestsPerTcpConnection, this setting limits the number of requests that are simultaneously sent to a single Cosmos DB back-end(MaxRequestsPerTcpConnection x MaxTcpConnectionPerEndpoint).
        /// </summary>
        /// <value>
        /// The default value is 65,535. Value must be greater than or equal to 16.
        /// </value>
        public int? MaxTcpConnectionsPerEndpoint
        {
            get => this.maxTcpConnectionsPerEndpoint;
            set
            {
                this.maxTcpConnectionsPerEndpoint = value;
                this.ValidateDirectTCPSettings();
            }
        }

        /// <summary>
        /// (Direct/TCP) Controls the client port reuse policy used by the transport stack.
        /// </summary>
        /// <value>
        /// The default value is PortReuseMode.ReuseUnicastPort.
        /// </value>
        /// <remarks>
        /// ReuseUnicastPort and PrivatePortPool are not mutually exclusive.
        /// When PrivatePortPool is enabled, the client first tries to reuse a port it already has.
        /// It falls back to allocating a new port if the initial attempts failed. If this fails, too, the client then falls back to ReuseUnicastPort.
        /// </remarks>
        public PortReuseMode? PortReuseMode
        {
            get => this.portReuseMode;
            set
            {
                this.portReuseMode = value;
                this.ValidateDirectTCPSettings();
            }
        }

        /// <summary>
        /// (Gateway/Https) Get or set the proxy information used for web requests.
        /// </summary>
        [JsonIgnore]
        public IWebProxy WebProxy
        {
            get => this.webProxy;
            set
            {
                if (value != null && this.HttpClientFactory != null)
                {
                    throw new ArgumentException($"{nameof(this.WebProxy)} cannot be set along {nameof(this.HttpClientFactory)}");
                }

                this.webProxy = value;
            }
        }

        /// <summary>
        /// Get to set optional serializer options.
        /// </summary>
        /// <example>
        /// An example on how to configure the serialization option to ignore null values
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions()
        /// {
        ///     SerializerOptions = new CosmosSerializationOptions(){
        ///         IgnoreNullValues = true
        ///     }
        /// };
        /// 
        /// CosmosClient client = new CosmosClient("endpoint", "key", clientOptions);
        /// ]]>
        /// </code>
        /// </example>
        public CosmosSerializationOptions SerializerOptions
        {
            get => this.serializerOptions;
            set
            {
                if (this.Serializer != null || this.UseSystemTextJsonSerializerWithOptions != null)
                {
                    throw new ArgumentException(
                        $"{nameof(this.SerializerOptions)} is not compatible with {nameof(this.Serializer)} or {nameof(this.UseSystemTextJsonSerializerWithOptions)}. Only one can be set.  ");
                }

                this.serializerOptions = value;
            }
        }

        /// <summary>
        /// Get to set an optional JSON serializer. The client will use it to serialize or de-serialize user's cosmos request/responses.
        /// SDK owned types such as DatabaseProperties and ContainerProperties will always use the SDK default serializer.
        /// </summary>
        /// <example>
        /// An example on how to set a custom serializer. For basic serializer options look at CosmosSerializationOptions
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosSerializer ignoreNullSerializer = new MyCustomIgnoreNullSerializer();
        ///         
        /// CosmosClientOptions clientOptions = new CosmosClientOptions()
        /// {
        ///     Serializer = ignoreNullSerializer
        /// };
        /// 
        /// CosmosClient client = new CosmosClient("endpoint", "key", clientOptions);
        /// ]]>
        /// </code>
        /// </example>
        [JsonConverter(typeof(ClientOptionJsonConverter))]
        public CosmosSerializer Serializer
        {
            get => this.serializerInternal;
            set
            {
                if (this.SerializerOptions != null || this.UseSystemTextJsonSerializerWithOptions != null)
                {
                    throw new ArgumentException(
                        $"{nameof(this.Serializer)} is not compatible with {nameof(this.SerializerOptions)} or {nameof(this.UseSystemTextJsonSerializerWithOptions)}. Only one can be set.  ");
                }

                this.serializerInternal = value;
            }
        }

        /// <summary>
        /// Limits the operations to the provided endpoint on the CosmosClient.
        /// </summary>
        /// <value>
        /// Default value is false.
        /// </value>
        /// <remarks>
        /// When the value of this property is false, the SDK will automatically discover write and read regions, and use them when the configured application region is not available.
        /// When set to true, availability is limited to the endpoint specified on the CosmosClient constructor.
        /// Defining the <see cref="ApplicationRegion"/> or <see cref="ApplicationPreferredRegions"/>  is not allowed when setting the value to true.
        /// </remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/high-availability">High availability</seealso>
        public bool LimitToEndpoint { get; set; } = false;

        /// <summary>
        /// Allows optimistic batching of requests to service. Setting this option might impact the latency of the operations. Hence this option is recommended for non-latency sensitive scenarios only.
        /// <remarks>
        /// The use of Resource Tokens scoped to a Partition Key as an authentication mechanism when Bulk is enabled is not recommended as it reduces the potential throughput benefit
        /// </remarks>
        /// </summary>
        public bool AllowBulkExecution { get; set; }

        /// <summary>
        /// Gets or sets the flag to enable address cache refresh on TCP connection reset notification.
        /// </summary>
        /// <remarks>
        /// Does not apply if <see cref="ConnectionMode.Gateway"/> is used.
        /// </remarks>
        /// <value>
        /// The default value is true
        /// </value>
        public bool EnableTcpConnectionEndpointRediscovery { get; set; } = true;

        /// <summary>
        /// Gets or sets a delegate to use to obtain an HttpClient instance to be used for HTTPS communication.
        /// </summary>
        /// <remarks>
        /// <para>
        /// HTTPS communication is used when <see cref="ConnectionMode"/> is set to <see cref="ConnectionMode.Gateway"/> for all operations and when <see cref="ConnectionMode"/> is <see cref="ConnectionMode.Direct"/> (default) for metadata operations.
        /// </para>
        /// <para>
        /// Useful in scenarios where the application is using a pool of HttpClient instances to be shared, like ASP.NET Core applications with IHttpClientFactory or Blazor WebAssembly applications.
        /// </para>
        /// <para>
        /// For .NET core applications the default GatewayConnectionLimit will be ignored. It must be set on the HttpClientHandler.MaxConnectionsPerServer to limit the number of connections
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public Func<HttpClient> HttpClientFactory
        {
            get => this.httpClientFactory;
            set
            {
                if (value != null && this.WebProxy != null)
                {
                    throw new ArgumentException($"{nameof(this.HttpClientFactory)} cannot be set along {nameof(this.WebProxy)}");
                }

                if (this.GatewayModeMaxConnectionLimit != ConnectionPolicy.Default.MaxConnectionLimit)
                {
                    throw new ArgumentException($"{nameof(this.httpClientFactory)} can not be set along with {nameof(this.GatewayModeMaxConnectionLimit)}. This must be set on the HttpClientHandler.MaxConnectionsPerServer property.");
                }

                this.httpClientFactory = value;
            }
        }

        /// <summary>
        /// Availability Strategy to be used for periods of high latency
        /// </summary>
        /// /// <example>
        /// An example on how to set an availability strategy custom serializer.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClient client = new CosmosClientBuilder("connection string")
        /// .WithApplicationPreferredRegions(
        ///    new List<string> { "East US", "Central US", "West US" } )
        /// .WithAvailabilityStrategy(
        ///    AvailabilityStrategy.CrossRegionHedgingStrategy(
        ///    threshold: TimeSpan.FromMilliseconds(500),
        ///    thresholdStep: TimeSpan.FromMilliseconds(100)
        /// ))
        /// .Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks> 
        /// The availability strategy in the example is a Cross Region Hedging Strategy.
        /// These strategies take two values, a threshold and a threshold step.When a request that is sent 
        /// out takes longer than the threshold time, the SDK will hedge to the second region in the application preferred regions list.
        /// If a response from either the primary request or the first hedged request is not received 
        /// after the threshold step time, the SDK will hedge to the third region and so on.
        /// </remarks>
        public AvailabilityStrategy AvailabilityStrategy { get; set; }

        /// <summary>
        /// Enable partition key level failover
        /// </summary>
        internal bool EnablePartitionLevelFailover { get; set; } = ConfigurationManager.IsPartitionLevelFailoverEnabled(defaultValue: false);

        /// <summary>
        /// Quorum Read allowed with eventual consistency account or consistent prefix account.
        /// </summary>
        internal bool EnableUpgradeConsistencyToLocalQuorum { get; set; } = false;

        /// <summary>
        /// Gets or sets the connection protocol when connecting to the Azure Cosmos service.
        /// </summary>
        /// <value>
        /// Default value is <see cref="Protocol.Tcp"/>.
        /// </value>
        /// <remarks>
        /// This setting is not used when <see cref="ConnectionMode"/> is set to <see cref="Cosmos.ConnectionMode.Gateway"/>.
        /// Gateway mode only supports HTTPS.
        /// For more information, see <see href="https://learn.microsoft.com/azure/cosmos-db/nosql/performance-tips-dotnet-sdk-v3#networking">Connection policy: Use the HTTPS protocol</see>.
        /// </remarks>
        internal Protocol ConnectionProtocol
        {
            get => this.connectionProtocol;
            set
            {
                this.ValidateDirectTCPSettings();
                this.connectionProtocol = value;
            }
        }

        /// <summary>
        /// The event handler to be invoked before the request is sent.
        /// </summary>
        internal EventHandler<SendingRequestEventArgs> SendingRequestEventArgs { get; set; }

        /// <summary>
        /// (Optional) transport interceptor factory
        /// </summary>
        internal Func<TransportClient, TransportClient> TransportClientHandlerFactory { get; set; }

        /// <summary>
        /// A callback delegate to do custom certificate validation for both HTTP and TCP.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Emulator: To ignore SSL Certificate please suffix connectionstring with "DisableServerCertificateValidation=True;". 
        /// When CosmosClientOptions.HttpClientFactory is used, SSL certificate needs to be handled appropriately.
        /// NOTE: DO NOT use the `DisableServerCertificateValidation` flag in production (only for emulator)
        /// </para>
        /// </remarks>
        public Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> ServerCertificateCustomValidationCallback { get; set; }

        /// <summary>
        /// Real call back that will be hooked down-stream to the transport clients (both http and tcp).
        /// NOTE: All down stream real-usage should come through this API only and not through the public API.
        /// 
        /// Test hook DisableServerCertificateValidationInvocationCallback 
        /// - When configured will invoke it when ever custom validation is done
        /// </summary>
        internal Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> GetServerCertificateCustomValidationCallback()
        {
            if (this.DisableServerCertificateValidation)
            {
                if (this.DisableServerCertificateValidationInvocationCallback == null)
                {
                    return this.ServerCertificateCustomValidationCallback ?? ((_, _, _) => true);
                }
                else
                {
                    return (X509Certificate2 cert, X509Chain chain, SslPolicyErrors policyErrors) =>
                    {
                        bool bValidationResult = true;
                        if (this.ServerCertificateCustomValidationCallback != null)
                        {
                            bValidationResult = this.ServerCertificateCustomValidationCallback(cert, chain, policyErrors);
                        }
                        this.DisableServerCertificateValidationInvocationCallback?.Invoke();
                        return bValidationResult;
                    };
                }
            }

            return this.ServerCertificateCustomValidationCallback;
        }

        internal Action DisableServerCertificateValidationInvocationCallback { get; set; }

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

        /// <summary>
        /// Flag indicates the value of DisableServerCertificateValidation flag set at connection string level.Default it is false.
        /// </summary>
        internal bool DisableServerCertificateValidation { get; set; }

        /// <summary>
        /// Gets or sets Client Telemetry Options like feature flags and corresponding options
        /// </summary>
        public CosmosClientTelemetryOptions CosmosClientTelemetryOptions { get; set; }

        /// <summary>
        /// Create a client with Fault Injection capabilities using the Cosmos DB Fault Injection Library.
        /// </summary>
        /// <example>
        /// How to create a CosmosClient with Fault Injection capabilities.
        /// <code language="c#">
        /// <![CDATA[
        /// FaultInjectionRule rule = new FaultInjectionRuleBuilder(
        ///     id: "ruleId",
        ///     condition: new FaultInjectionConditionBuilder()
        ///         .WithRegion("East US")
        ///         .Build(),
        ///     result: new FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
        ///         .Build())
        ///     .Build();
        ///     
        /// FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule>() { rule });
        /// 
        /// CosmosClientOptions clientOptions = new CosmosClientOptions()
        /// {
        ///     FaultInjector = faultInjector
        /// };
        /// 
        /// CosmosClient client = new CosmosClient("connection string", clientOptions);
        /// ]]>
        /// </code>
        /// </example> 
        public IFaultInjector FaultInjector
        {
            get => this.faultInjector;
            set
            {
                this.faultInjector = value;
                if (this.faultInjector != null)
                {
                    this.ChaosInterceptorFactory = this.faultInjector.GetChaosInterceptorFactory();
                }
            }
        }

        /// <summary>
        /// Sets the throughput bucket for requests created using cosmos client.
        /// </summary>
        /// <remarks>
        /// If throughput bucket is also set at request level in <see cref="RequestOptions.ThroughputBucket"/>, that throughput bucket is used.
        /// If <see cref="AllowBulkExecution"/> is set to true in CosmosClientOptions, throughput bucket can only be set at client level.
        /// </remarks>
        /// <seealso href="https://aka.ms/cosmsodb-bucketing"/>
        internal int? ThroughputBucket { get; set; }

        internal IChaosInterceptorFactory ChaosInterceptorFactory { get; set; }

        internal void SetSerializerIfNotConfigured(CosmosSerializer serializer)
        {
            if (this.serializerInternal == null)
            {
                this.serializerInternal = serializer ?? throw new ArgumentNullException(nameof(serializer));
            }
        }

        internal CosmosClientOptions Clone()
        {
            CosmosClientOptions cloneConfiguration = (CosmosClientOptions)this.MemberwiseClone();
            return cloneConfiguration;
        }

        internal virtual ConnectionPolicy GetConnectionPolicy(int clientId)
        {
            this.ValidateDirectTCPSettings();
            this.ValidateLimitToEndpointSettings();
            this.ValidatePartitionLevelFailoverSettings();

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                MaxConnectionLimit = this.GatewayModeMaxConnectionLimit,
                RequestTimeout = this.RequestTimeout,
                ConnectionMode = this.ConnectionMode,
                ConnectionProtocol = this.ConnectionProtocol,
                UserAgentContainer = this.CreateUserAgentContainerWithFeatures(clientId),
                UseMultipleWriteLocations = true,
                IdleTcpConnectionTimeout = this.IdleTcpConnectionTimeout,
                OpenTcpConnectionTimeout = this.OpenTcpConnectionTimeout,
                MaxRequestsPerTcpConnection = this.MaxRequestsPerTcpConnection,
                MaxTcpConnectionsPerEndpoint = this.MaxTcpConnectionsPerEndpoint,
                EnableEndpointDiscovery = !this.LimitToEndpoint,
                EnablePartitionLevelFailover = this.EnablePartitionLevelFailover,
                PortReuseMode = this.portReuseMode,
                EnableTcpConnectionEndpointRediscovery = this.EnableTcpConnectionEndpointRediscovery,
                EnableAdvancedReplicaSelectionForTcp = this.EnableAdvancedReplicaSelectionForTcp,
                HttpClientFactory = this.httpClientFactory,
                ServerCertificateCustomValidationCallback = this.ServerCertificateCustomValidationCallback,
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions()
            };

            if (this.CosmosClientTelemetryOptions != null)
            {
                connectionPolicy.CosmosClientTelemetryOptions = this.CosmosClientTelemetryOptions;
            }

            RegionNameMapper mapper = new RegionNameMapper();
            if (!string.IsNullOrEmpty(this.ApplicationRegion))
            {
                connectionPolicy.SetCurrentLocation(mapper.GetCosmosDBRegionName(this.ApplicationRegion));
            }

            if (this.ApplicationPreferredRegions != null)
            {
                List<string> mappedRegions = this.ApplicationPreferredRegions.Select(s => mapper.GetCosmosDBRegionName(s)).ToList();

                connectionPolicy.SetPreferredLocations(mappedRegions);
            }

            if (this.AccountInitializationCustomEndpoints != null)
            {
                connectionPolicy.SetAccountInitializationCustomEndpoints(this.AccountInitializationCustomEndpoints);
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

            return (Documents.ConsistencyLevel)this.ConsistencyLevel.Value;
        }

        internal static string GetAccountEndpoint(string connectionString)
        {
            return CosmosClientOptions.GetValueFromConnectionString<string>(connectionString, CosmosClientOptions.ConnectionStringAccountEndpoint, null);
        }

        internal static string GetAccountKey(string connectionString)
        {
            return CosmosClientOptions.GetValueFromConnectionString<string>(connectionString, CosmosClientOptions.ConnectionStringAccountKey, null);
        }

        internal static bool IsConnectionStringDisableServerCertificateValidationFlag(string connectionString)
        {
            return Convert.ToBoolean(CosmosClientOptions.GetValueFromConnectionString<bool>(connectionString, CosmosClientOptions.ConnectionStringDisableServerCertificateValidation, false));
        }

        internal static CosmosClientOptions GetCosmosClientOptionsWithCertificateFlag(string connectionString, CosmosClientOptions clientOptions)
        {
            clientOptions ??= new CosmosClientOptions();
            if (CosmosClientOptions.IsConnectionStringDisableServerCertificateValidationFlag(connectionString))
            {
                clientOptions.DisableServerCertificateValidation = true;
            }

            return clientOptions;
        }

        private static T GetValueFromConnectionString<T>(string connectionString, string keyName, T defaultValue)
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
                    try
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch (InvalidCastException)
                    {
                        throw new ArgumentException("The connection string contains invalid property: " + keyName);
                    }
                }
            }

            if (defaultValue != null)
            {
                return defaultValue;
            }

            throw new ArgumentException("The connection string is missing a required property: " + keyName);
        }

        private void ValidateLimitToEndpointSettings()
        {
            if (!string.IsNullOrEmpty(this.ApplicationRegion) && this.LimitToEndpoint)
            {
                throw new ArgumentException($"Cannot specify {nameof(this.ApplicationRegion)} and enable {nameof(this.LimitToEndpoint)}. Only one can be set.");
            }

            if (this.ApplicationPreferredRegions?.Count > 0 && this.LimitToEndpoint)
            {
                throw new ArgumentException($"Cannot specify {nameof(this.ApplicationPreferredRegions)} and enable {nameof(this.LimitToEndpoint)}. Only one can be set.");
            }

            if (!string.IsNullOrEmpty(this.ApplicationRegion) && this.ApplicationPreferredRegions?.Count > 0)
            {
                throw new ArgumentException($"Cannot specify {nameof(this.ApplicationPreferredRegions)} and {nameof(this.ApplicationRegion)}. Only one can be set.");
            }
        }

        private void ValidatePartitionLevelFailoverSettings()
        {
            if (this.EnablePartitionLevelFailover
                && string.IsNullOrEmpty(this.ApplicationRegion)
                && (this.ApplicationPreferredRegions is null || this.ApplicationPreferredRegions.Count == 0))
            {
                throw new ArgumentException($"{nameof(this.ApplicationPreferredRegions)} or {nameof(this.ApplicationRegion)} is required when {nameof(this.EnablePartitionLevelFailover)} is enabled.");
            }
        }

        private void ValidateDirectTCPSettings()
        {
            string settingName = string.Empty;
            if (this.ConnectionMode != ConnectionMode.Direct)
            {
                if (this.IdleTcpConnectionTimeout.HasValue)
                {
                    settingName = nameof(this.IdleTcpConnectionTimeout);
                }
                else if (this.OpenTcpConnectionTimeout.HasValue)
                {
                    settingName = nameof(this.OpenTcpConnectionTimeout);
                }
                else if (this.MaxRequestsPerTcpConnection.HasValue)
                {
                    settingName = nameof(this.MaxRequestsPerTcpConnection);
                }
                else if (this.MaxTcpConnectionsPerEndpoint.HasValue)
                {
                    settingName = nameof(this.MaxTcpConnectionsPerEndpoint);
                }
                else if (this.PortReuseMode.HasValue)
                {
                    settingName = nameof(this.PortReuseMode);
                }
            }

            if (!string.IsNullOrEmpty(settingName))
            {
                throw new ArgumentException($"{settingName} requires {nameof(this.ConnectionMode)} to be set to {nameof(ConnectionMode.Direct)}");
            }
        }

        internal UserAgentContainer CreateUserAgentContainerWithFeatures(int clientId)
        {
            CosmosClientOptionsFeatures features = CosmosClientOptionsFeatures.NoFeatures;
            if (this.AllowBulkExecution)
            {
                features |= CosmosClientOptionsFeatures.AllowBulkExecution;
            }

            if (this.HttpClientFactory != null)
            {
                features |= CosmosClientOptionsFeatures.HttpClientFactory;
            }

            string featureString = null;
            if (features != CosmosClientOptionsFeatures.NoFeatures)
            {
                featureString = Convert.ToString((int)features, 2).PadLeft(8, '0');
            }

            string regionConfiguration = this.GetRegionConfiguration();

            return new UserAgentContainer(
                        clientId: clientId,
                        features: featureString,
                        regionConfiguration: regionConfiguration,
                        suffix: this.ApplicationName);
        }

        /// <summary>
        /// This generates a key that added to the user agent to make it 
        /// possible to determine if the SDK has region failover enabled.
        /// </summary>
        /// <returns>Format Reg-{D (Disabled discovery)}-S(application region)|L(List of preferred regions)|N(None, user did not configure it)</returns>
        private string GetRegionConfiguration()
        {
            string regionConfig = this.LimitToEndpoint ? "D" : string.Empty;
            if (!string.IsNullOrEmpty(this.ApplicationRegion))
            {
                return regionConfig + "S";
            }

            if (this.ApplicationPreferredRegions != null)
            {
                return regionConfig + "L";
            }

            return regionConfig + "N";
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
                if (value is Collection<RequestHandler> handlers)
                {
                    writer.WriteValue(string.Join(":", handlers.Select(x => x.GetType())));
                    return;
                }

                CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper = value as CosmosJsonSerializerWrapper;
                if (value is CosmosJsonSerializerWrapper)
                {
                    writer.WriteValue(cosmosJsonSerializerWrapper.InternalJsonSerializer.GetType().ToString());
                }

                CosmosSerializer cosmosSerializer = value as CosmosSerializer;
                if (cosmosSerializer is CosmosSerializer)
                {
                    writer.WriteValue(cosmosSerializer.GetType().ToString());
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