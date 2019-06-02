//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// This is a Builder class that creates a cosmos client
    /// </summary>
    public class CosmosClientBuilder
    {
        private readonly CosmosClientOptions clientOptions = null;

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
        /// ]]>
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
        /// .WithApplicationRegion("East US 2");
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClientBuilder(
            string accountEndPoint,
            string accountKey)
        {
            this.clientOptions = new CosmosClientOptions(accountEndPoint, accountKey);
        }

        /// <summary>
        /// Extracts the account endpoint and key from the connection string.
        /// </summary>
        /// <example>"AccountEndpoint=https://mytestcosmosaccount.documents.azure.com:443/;AccountKey={SecretAccountKey};"</example>
        /// <param name="connectionString">The connection string must contain AccountEndpoint and AccountKey.</param>
        public CosmosClientBuilder(string connectionString)
        {
            this.clientOptions = new CosmosClientOptions(connectionString);
        }

        /// <summary>
        /// A method to create the cosmos client
        /// </summary>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        /// <returns>An instance of <see cref="CosmosClient"/>.</returns>
        public virtual CosmosClient Build()
        {
            DefaultTrace.TraceInformation($"CosmosClientBuilder.Build with configuration: {this.clientOptions.GetSerializedConfiguration()}");
            return new CosmosClient(this.clientOptions);
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
            return new CosmosClient(this.clientOptions, documentClient);
        }

        /// <summary>
        /// A suffix to be added to the default user-agent for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="applicationName">A string to use as suffix in the User Agent.</param>
        /// <remarks>
        /// Setting this property after sending any request won't have any effect.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        public virtual CosmosClientBuilder WithApplicationName(string applicationName)
        {
            this.clientOptions.ApplicationName = applicationName;
            return this;
        }

        /// <summary>
        /// Set the current preferred region
        /// </summary>
        /// <param name="applicationRegion"><see cref="CosmosRegions"/> for a list of valid azure regions. This list may not contain the latest azure regions.</param>
        /// <example>
        /// The example below creates a new <see cref="CosmosClientBuilder"/> with a of preferred region.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey")
        /// .WithApplicationRegion("East US 2");
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ApplicationRegion"/>
        public virtual CosmosClientBuilder WithApplicationRegion(string applicationRegion)
        {
            this.clientOptions.ApplicationRegion = applicationRegion;
            return this;
        }

        /// <summary>
        /// Sets the request timeout in seconds when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="requestTimeout">A time to use as timeout for operations.</param>
        /// <value>Default value is 60 seconds.</value>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.RequestTimeout"/>
        public virtual CosmosClientBuilder WithRequestTimeout(TimeSpan requestTimeout)
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
        public virtual CosmosClientBuilder WithConnectionModeDirect()
        {
            this.clientOptions.ConnectionMode = ConnectionMode.Direct;
            this.clientOptions.ConnectionProtocol = Protocol.Tcp;
            return this;
        }

        /// <summary>
        /// Sets the connection mode to Gateway. This is used by the client when connecting to the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxConnectionLimit">The number specifies the time to wait for response to come back from network peer. Default is 60 connections</param>
        /// <remarks>
        /// For more information, see <see href="https://docs.microsoft.com/azure/documentdb/documentdb-performance-tips#direct-connection">Connection policy: Use direct connection mode</see>.
        /// </remarks>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <seealso cref="CosmosClientOptions.ConnectionMode"/>
        /// <seealso cref="CosmosClientOptions.GatewayModeMaxConnectionLimit"/>
        public virtual CosmosClientBuilder WithConnectionModeGateway(int? maxConnectionLimit = null)
        {
            this.clientOptions.ConnectionMode = ConnectionMode.Gateway;
            this.clientOptions.ConnectionProtocol = Protocol.Https;
            if (maxConnectionLimit.HasValue)
            {
                this.clientOptions.GatewayModeMaxConnectionLimit = maxConnectionLimit.Value;
            }

            return this;
        }

        /// <summary>
        /// Sets an array of custom handlers to the request. The handlers will be chained in
        /// the order listed. The InvokerHandler.InnerHandler is required to be null to allow the
        /// pipeline to chain the handlers.
        /// </summary>
        /// <returns>The current <see cref="CosmosClientBuilder"/>.</returns>
        /// <param name="handlers">A list of <see cref="CosmosRequestHandler"/> instaces to add to the pipeline.</param>
        /// <seealso cref="CosmosClientOptions.CustomHandlers"/>
        public virtual CosmosClientBuilder AddCustomHandlers(params CosmosRequestHandler[] handlers)
        {
            if (handlers != null && handlers.Any(x => x != null))
            {
                this.clientOptions.CustomHandlers = handlers.ToList().AsReadOnly();
            }
            else
            {
                this.clientOptions.CustomHandlers = null;
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
        /// <seealso cref="CosmosClientOptions.MaxRetryWaitTimeOnThrottledRequests"/>
        /// <seealso cref="CosmosClientOptions.MaxRetryAttemptsOnThrottledRequests"/>
        public virtual CosmosClientBuilder WithThrottlingRetryOptions(TimeSpan maxRetryWaitTimeOnThrottledRequests, 
            int maxRetryAttemptsOnThrottledRequests)
        {
            this.clientOptions.MaxRetryWaitTimeOnThrottledRequests = maxRetryWaitTimeOnThrottledRequests;
            this.clientOptions.MaxRetryAttemptsOnThrottledRequests = maxRetryAttemptsOnThrottledRequests;
            return this;
        }

        /// <summary>
        /// Set a custom JSON serializer. 
        /// </summary>
        /// <param name="cosmosJsonSerializer">The custom class that implements <see cref="CosmosJsonSerializer"/> </param>
        /// <returns>The <see cref="CosmosClientBuilder"/> object</returns>
        /// <seealso cref="CosmosJsonSerializer"/>
        /// <seealso cref="CosmosClientOptions.CosmosSerializer"/>
        public virtual CosmosClientBuilder WithCustomJsonSerializer(
            CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.clientOptions.CosmosSerializer = cosmosJsonSerializer;
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
    }
}
