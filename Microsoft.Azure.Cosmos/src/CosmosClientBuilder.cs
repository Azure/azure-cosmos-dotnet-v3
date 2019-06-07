//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// This is a Builder class that creates a cosmos client
    /// </summary>
    public class CosmosClientBuilder
    {
        private readonly CosmosClientConfiguration cosmosClientConfiguration = null;

        /// <summary>
        /// Initialize a new CosmosConfiguration class that holds all the properties the CosmosClient requires.
        /// </summary>
        /// <param name="accountEndPoint">The Uri to the Cosmos Account. Example: https://{Cosmos Account Name}.documents.azure.com:443/ </param>
        /// <param name="accountKey">The key to the account.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey");
        /// CosmosClient client = cosmosClientBuilder.Build();
        ///]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> with a ConsistencyLevel and a list of preferred locations.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey")
        /// .UseConsistencyLevel(ConsistencyLevel.Strong)
        /// .UseCurrentRegion(Region.USEast2);
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClientBuilder(
            string accountEndPoint,
            string accountKey)
        {
            this.cosmosClientConfiguration = new CosmosClientConfiguration(accountEndPoint, accountKey);
        }

        /// <summary>
        /// Extracts the account endpoint and key from the connection string.
        /// </summary>
        /// <example>"AccountEndpoint=https://mytestcosmosaccount.documents.azure.com:443/;AccountKey={SecretAccountKey};"</example>
        /// <param name="connectionString">The connection string must contain AccountEndpoint and AccountKey.</param>
        public CosmosClientBuilder(string connectionString)
        {
            this.cosmosClientConfiguration = new CosmosClientConfiguration(connectionString);
        }

        /// <summary>
        /// A method to create the cosmos client
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public virtual CosmosClient Build()
        {
            CosmosClientConfiguration copyOfConfig = this.cosmosClientConfiguration.Clone();
            DefaultTrace.TraceInformation($"CosmosClientBuilder.Build with configuration: {copyOfConfig.GetSerializedConfiguration()}");
            return new CosmosClient(copyOfConfig);
        }

        /// <summary>
        /// A method to create the cosmos client
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        internal virtual CosmosClient Build(DocumentClient documentClient)
        {
            CosmosClientConfiguration copyOfConfig = this.cosmosClientConfiguration.Clone();
            DefaultTrace.TraceInformation($"CosmosClientBuilder.Build(DocumentClient) with configuration: {copyOfConfig.GetSerializedConfiguration()}");
            return new CosmosClient(copyOfConfig, documentClient);
        }

        /// <summary>
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        public virtual CosmosClientBuilder UseUserAgentSuffix(string userAgentSuffix)
        {
            this.cosmosClientConfiguration.UserAgentSuffix = userAgentSuffix;
            return this;
        }

        /// <summary>
        /// Set the current preferred region
        /// </summary>
        /// <param name="cosmosRegion"><see cref="CosmosRegions"/> for a list of valid azure regions. This list may not contain the latest azure regions.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> with a of preferred region.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey")
        /// .UseCurrentRegion(CosmosRegion.USEast2);
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="CosmosClientConfiguration.CurrentRegion"/>
        public virtual CosmosClientBuilder UseCurrentRegion(string cosmosRegion)
        {
            this.cosmosClientConfiguration.CurrentRegion = cosmosRegion;
            return this;
        }

        /// <summary>
        /// Sets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <value>Default value is 60 seconds.</value>
        /// <seealso cref="CosmosClientConfiguration.RequestTimeout"/>
        public virtual CosmosClientBuilder UseRequestTimeout(TimeSpan requestTimeout)
        {
            this.cosmosClientConfiguration.RequestTimeout = requestTimeout;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Direct. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosClientConfiguration.ConnectionMode"/>
        public virtual CosmosClientBuilder UseConnectionModeDirect()
        {
            this.cosmosClientConfiguration.ConnectionMode = ConnectionMode.Direct;
            this.cosmosClientConfiguration.ConnectionProtocol = Protocol.Tcp;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Gateway. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxConnectionLimit">The number specifies the time to wait for response to come back from network peer. Default is 60 connections</param>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/en-us/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <seealso cref="CosmosClientConfiguration.ConnectionMode"/>
        /// <seealso cref="CosmosClientConfiguration.MaxConnectionLimit"/>
        public virtual CosmosClientBuilder UseConnectionModeGateway(int? maxConnectionLimit = null)
        {
            this.cosmosClientConfiguration.ConnectionMode = ConnectionMode.Gateway;
            this.cosmosClientConfiguration.ConnectionProtocol = Protocol.Https;
            if (maxConnectionLimit.HasValue)
            {
                this.cosmosClientConfiguration.MaxConnectionLimit = maxConnectionLimit.Value;
            }

            return this;
        }

        /// <summary>
        /// Sets an array of custom handlers to the request. The handlers will be chained in
        /// the order listed. The InvokerHandler.InnerHandler is required to be null to allow the
        /// pipeline to chain the handlers.
        /// </summary>
        /// <seealso cref="CosmosClientConfiguration.CustomHandlers"/>
        public virtual CosmosClientBuilder AddCustomHandlers(params CosmosRequestHandler[] handlers)
        {
            if (handlers != null && handlers.Any(x => x != null))
            {
                this.cosmosClientConfiguration.CustomHandlers = handlers.ToList().AsReadOnly();
            }
            else
            {
                this.cosmosClientConfiguration.CustomHandlers = null;
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
        /// <seealso cref="CosmosClientConfiguration.MaxRetryWaitTimeOnThrottledRequests"/>
        /// <seealso cref="CosmosClientConfiguration.MaxRetryAttemptsOnThrottledRequests"/>
        public virtual CosmosClientBuilder UseThrottlingRetryOptions(TimeSpan maxRetryWaitTimeOnThrottledRequests, int maxRetryAttemptsOnThrottledRequests)
        {
            this.cosmosClientConfiguration.MaxRetryWaitTimeOnThrottledRequests = maxRetryWaitTimeOnThrottledRequests;
            this.cosmosClientConfiguration.MaxRetryAttemptsOnThrottledRequests = maxRetryAttemptsOnThrottledRequests;
            return this;
        }

        /// <summary>
        /// Set a custom JSON serializer. 
        /// </summary>
        /// <param name="cosmosJsonSerializer">The custom class that implements <see cref="CosmosJsonSerializer"/> </param>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        /// <seealso cref="CosmosJsonSerializer"/>
        /// <seealso cref="CosmosClientConfiguration.CosmosJsonSerializer"/>
        public virtual CosmosClientBuilder UseCustomJsonSerializer(
            CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.cosmosClientConfiguration.CosmosJsonSerializer = cosmosJsonSerializer;
            return this;
        }

        /// <summary>
        /// The event handler to be invoked before the request is sent.
        /// </summary>
        internal CosmosClientBuilder UseSendingRequestEventArgs(EventHandler<SendingRequestEventArgs> sendingRequestEventArgs)
        {
            this.cosmosClientConfiguration.SendingRequestEventArgs = sendingRequestEventArgs;
            return this;
        }

        /// <summary>
        /// (Optional) transport interceptor factory
        /// </summary>
        internal CosmosClientBuilder UseTransportClientHandlerFactory(Func<TransportClient, TransportClient> transportClientHandlerFactory)
        {
            this.cosmosClientConfiguration.TransportClientHandlerFactory = transportClientHandlerFactory;
            return this;
        }

        /// <summary>
        /// ApiType for the account
        /// </summary>
        internal CosmosClientBuilder UseApiType(ApiType apiType)
        {
            this.cosmosClientConfiguration.ApiType = apiType;
            return this;
        }

        /// <summary>
        /// Specify a store client factory to use for all transport requests for cosmos client.
        /// </summary>
        /// <remarks>
        /// This method enables transport client sharing among multiple cosmos client instances inside a single process.
        /// </remarks>
        /// <param name="storeClientFactory">Instance of store client factory to use to create transport client for an instance of cosmos client.</param>
        internal CosmosClientBuilder UseStoreClientFactory(IStoreClientFactory storeClientFactory)
        {
            this.cosmosClientConfiguration.StoreClientFactory = storeClientFactory;
            return this;
        }

        /// <summary>
        /// Disables CPU monitoring for transport client which will inhibit troubleshooting of timeout exceptions.
        /// </summary>
        internal CosmosClientBuilder DisableCpuMonitor()
        {
            this.cosmosClientConfiguration.EnableCpuMonitor = false;
            return this;
        }

        internal CosmosClientBuilder WithRetryWithOptions(
            int? initialRetryForRetryWithMilliseconds,
            int? maximumRetryForRetryWithMilliseconds,
            int? randomSaltForRetryWithMilliseconds,
            int? totalWaitTimeForRetryWithMilliseconds)
        {
            this.cosmosClientConfiguration.InitialRetryForRetryWithMilliseconds = initialRetryForRetryWithMilliseconds;
            this.cosmosClientConfiguration.MaximumRetryForRetryWithMilliseconds = maximumRetryForRetryWithMilliseconds;
            this.cosmosClientConfiguration.RandomSaltForRetryWithMilliseconds = randomSaltForRetryWithMilliseconds;
            this.cosmosClientConfiguration.TotalWaitTimeForRetryWithMilliseconds = totalWaitTimeForRetryWithMilliseconds;
            return this;
        }
    }
}
