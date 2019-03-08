//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a client-side logical representation of the Azure Cosmos DB database account.
    /// This client can be used to configure and execute requests in the Azure Cosmos DB database service.
    /// 
    /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
    /// of the application which enables efficient connection management and performance.
    /// </summary>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient uses the <see cref="CosmosClientConfiguration"/> to get all the configuration values.
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
    ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
    ///     accountKey: "SuperSecretKey")
    ///     .UseCurrentRegion(LocationNames.EastUS2);
    /// 
    /// using (CosmosClient cosmosClient = cosmosClientBuilder.Build())
    /// {
    ///     CosmosDatabase db = await client.Databases.CreateAsync(Guid.NewGuid().ToString())
    ///     CosmosContainer container = await db.Containers.CreateAsync(Guid.NewGuid().ToString());
    /// }
    ///]]>
    /// </code>
    /// </example>
    /// 
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient is created with the AccountEndpoint and AccountKey.
    /// <code language="c#">
    /// <![CDATA[
    /// using (CosmosClient cosmosClient = new CosmosClient(
    ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
    ///     accountKey: "SuperSecretKey"))
    /// {
    ///     CosmosDatabase db = await client.Databases.CreateAsync(Guid.NewGuid().ToString())
    ///     CosmosContainer container = await db.Containers.CreateAsync(Guid.NewGuid().ToString());
    /// }
    ///]]>
    /// </code>
    /// </example>
    /// 
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient is created with the connection string.
    /// <code language="c#">
    /// <![CDATA[
    /// using (CosmosClient cosmosClient = new CosmosClient(
    ///     connectionString: "AccountEndpoint=https://testcosmos.documents.azure.com:443/;AccountKey=SuperSecretKey;"))
    /// {
    ///     CosmosDatabase db = await client.Databases.CreateAsync(Guid.NewGuid().ToString())
    ///     CosmosContainer container = await db.Containers.CreateAsync(Guid.NewGuid().ToString());
    /// }
    ///]]>
    /// </code>
    /// </example>
    public class CosmosClient : IDisposable
    {
        private Lazy<CosmosOffers> offerSet;

        /// <summary>
        /// Create a new CosmosClient with the connection
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. Example: https://mycosmosaccount.documents.azure.com:443/;AccountKey=SuperSecretKey;</param>
        /// <example>
        /// This example creates a CosmosClient
        /// <code language="c#">
        /// <![CDATA[
        /// using (CosmosClient cosmosClient = new CosmosClient(
        ///     connectionString: "https://testcosmos.documents.azure.com:443/;AccountKey=SuperSecretKey;"))
        /// {
        ///     // Create a database and other CosmosClient operations
        /// }
        ///]]>
        /// </code>
        /// </example>
        public CosmosClient(string connectionString) :
            this(new CosmosClientConfiguration(connectionString))
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the account endpoint URI string and account key
        /// </summary>
        /// <param name="accountEndPoint">The cosmos service endpoint to use to create the client.</param>
        /// <param name="accountKey">The cosmos account key to use to create the client.</param>
        /// <example>
        /// This example creates a CosmosClient
        /// <code language="c#">
        /// <![CDATA[
        /// using (CosmosClient cosmosClient = new CosmosClient(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey"))
        /// {
        ///     // Create a database and other CosmosClient operations
        /// }
        ///]]>
        /// </code>
        /// </example>
        public CosmosClient(
            string accountEndPoint,
            string accountKey) :
            this(new CosmosClientConfiguration(accountEndPoint, accountKey))
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the cosmosClientConfiguration
        /// </summary>
        /// <param name="cosmosClientConfiguration">The <see cref="CosmosClientConfiguration"/> used to initialize the cosmos client.</param>
        /// <example>
        /// This example creates a CosmosClient
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("accountEndpoint", "accountkey");
        /// using (CosmosClient cosmosClient = cosmosClientBuilder.Build())
        /// {
        ///     // Create a database and other CosmosClient operations
        /// }
        ///]]>
        /// </code>
        /// </example>
        internal CosmosClient(CosmosClientConfiguration cosmosClientConfiguration)
        {
            if (cosmosClientConfiguration == null)
            {
                throw new ArgumentNullException(nameof(cosmosClientConfiguration));
            }

            DocumentClient documentClient = new DocumentClient(
                cosmosClientConfiguration.AccountEndPoint,
                cosmosClientConfiguration.AccountKey,
                apitype: cosmosClientConfiguration.ApiType,
                sendingRequestEventArgs: cosmosClientConfiguration.SendingRequestEventArgs,
                transportClientHandlerFactory: cosmosClientConfiguration.TransportClientHandlerFactory,
                connectionPolicy: cosmosClientConfiguration.GetConnectionPolicy(),
                enableCpuMonitor: cosmosClientConfiguration.EnableCpuMonitor,
                storeClientFactory: cosmosClientConfiguration.StoreClientFactory);

            Init(
                cosmosClientConfiguration,
                documentClient);
        }

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        internal CosmosClient(
            CosmosClientConfiguration cosmosClientConfiguration,
            DocumentClient documentClient)
        {
            if (cosmosClientConfiguration == null)
            {
                throw new ArgumentNullException(nameof(cosmosClientConfiguration));
            }

            if (documentClient == null)
            {
                throw new ArgumentNullException(nameof(documentClient));
            }

            Init(cosmosClientConfiguration, documentClient);
        }

        /// <summary>
        /// Used for creating new databases, or querying/reading all databases.
        /// </summary>
        /// <example>
        /// This example creates a cosmos database and container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosDatabase database = this.cosmosClient.Databases.CreateDatabaseAsync(Guid.NewGuid().ToString());
        /// CosmosContainerResponse container = database.Containers.CreateContainerAsync(Guid.NewGuid().ToString());
        ///]]>
        /// </code>
        /// </example>
        public virtual CosmosDatabases Databases { get; private set; }

        /// <summary>
        /// The <see cref="Cosmos.CosmosClientConfiguration"/> used initialize CosmosClient
        /// </summary>
        public virtual CosmosClientConfiguration Configuration { get; private set; }

        internal CosmosOffers Offers => this.offerSet.Value;
        internal DocumentClient DocumentClient { get; set; }
        internal CosmosRequestHandler RequestHandler { get; private set; }
        internal ConsistencyLevel AccountConsistencyLevel { get; private set; }

        internal CosmosResponseFactory ResponseFactory =>
            new CosmosResponseFactory(this.CosmosJsonSerializer);
        internal CosmosJsonSerializer CosmosJsonSerializer { get; private set; }

        /// <summary>
        /// Read the <see cref="Microsoft.Azure.Cosmos.CosmosAccountSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// A <see cref="CosmosAccountSettings"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public virtual Task<CosmosAccountSettings> GetAccountSettingsAsync()
        {
            return ((IDocumentClientInternal)this.DocumentClient).GetDatabaseAccountInternalAsync(this.Configuration.AccountEndPoint);
        }

        internal void Init(
            CosmosClientConfiguration configuration,
            DocumentClient documentClient)
        {
            this.Configuration = configuration;
            this.DocumentClient = documentClient;
            this.CosmosJsonSerializer = new CosmosJsonSerializerWrapper(this.Configuration.CosmosJsonSerializer);

            //Request pipeline 
            ClientPipelineBuilder clientPipelineBuilder = new ClientPipelineBuilder(
                this,
                this.DocumentClient.ResetSessionTokenRetryPolicy,
                this.Configuration.CustomHandlers
            );

            // DocumentClient is not initialized with any consistency overrides so default is backend consistency
            this.AccountConsistencyLevel = this.DocumentClient.ConsistencyLevel;

            this.RequestHandler = clientPipelineBuilder.Build();
            this.Databases = new CosmosDatabasesCore(this);
            this.offerSet = new Lazy<CosmosOffers>(() => new CosmosOffers(this.DocumentClient), LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        /// <param name="disposing">True if disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.DocumentClient != null)
            {
                this.DocumentClient.Dispose();
                this.DocumentClient = null;
            }
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
