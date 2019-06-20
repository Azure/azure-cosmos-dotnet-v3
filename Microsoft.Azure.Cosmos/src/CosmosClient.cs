//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Provides a client-side logical representation of the Azure Cosmos DB database account.
    /// This client can be used to configure and execute requests in the Azure Cosmos DB database service.
    /// 
    /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
    /// of the application which enables efficient connection management and performance.
    /// </summary>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient uses the <see cref="Cosmos.CosmosClientOptions"/> to get all the configuration values.
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
    ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
    ///     accountKey: "SuperSecretKey")
    ///     .WithApplicationRegion(LocationNames.EastUS2);
    /// 
    /// using (CosmosClient cosmosClient = cosmosClientBuilder.Build())
    /// {
    ///     Database db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString())
    ///     Container container = await db.CreateContainerAsync(Guid.NewGuid().ToString());
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// 
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created with the AccountEndpoint and AccountKey.
    /// <code language="c#">
    /// <![CDATA[
    /// using (CosmosClient cosmosClient = new CosmosClient(
    ///     accountEndpoint: "https://testcosmos.documents.azure.com:443/",
    ///     accountKey: "SuperSecretKey"))
    /// {
    ///     Database db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString())
    ///     Container container = await db.Containers.CreateContainerAsync(Guid.NewGuid().ToString());
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// 
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created with the connection string.
    /// <code language="c#">
    /// <![CDATA[
    /// using (CosmosClient cosmosClient = new CosmosClient(
    ///     connectionString: "AccountEndpoint=https://testcosmos.documents.azure.com:443/;AccountKey=SuperSecretKey;"))
    /// {
    ///     Database db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString())
    ///     Container container = await db.Containers.CreateContainerAsync(Guid.NewGuid().ToString());
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// <remarks>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/distribute-data-globally" />
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/partitioning-overview" />
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units" />
    /// </remarks>
    public class CosmosClient : IDisposable
    {
        private readonly Uri DatabaseRootUri = new Uri(Paths.Databases_Root, UriKind.Relative);
        private Lazy<CosmosOffers> offerSet;

        static CosmosClient()
        {
            HttpConstants.Versions.CurrentVersion = HttpConstants.Versions.v2018_12_31;
            HttpConstants.Versions.CurrentVersionUTF8 = Encoding.UTF8.GetBytes(HttpConstants.Versions.CurrentVersion);

            // V3 always assumes assemblies exists
            // Shall revisit on feedback
            ServiceInteropWrapper.AssembliesExist = new Lazy<bool>(() => true);
        }

        /// <summary>
        /// Create a new CosmosClient used for mock testing
        /// </summary>
        protected CosmosClient()
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the connection
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. Example: https://mycosmosaccount.documents.azure.com:443/;AccountKey=SuperSecretKey;</param>
        /// <param name="clientOptions">(Optional) client options</param>
        /// <example>
        /// This example creates a CosmosClient
        /// <code language="c#">
        /// <![CDATA[
        /// using (CosmosClient cosmosClient = new CosmosClient(
        ///     connectionString: "https://testcosmos.documents.azure.com:443/;AccountKey=SuperSecretKey;"))
        /// {
        ///     // Create a database and other CosmosClient operations
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClient(string connectionString, CosmosClientOptions clientOptions = null)
            : this(
                  CosmosClientOptions.GetAccountEndpoint(connectionString),
                  CosmosClientOptions.GetAccountKey(connectionString),
                  clientOptions)
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the account endpoint URI string and account key
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use to create the client.</param>
        /// <param name="accountKey">The cosmos account key to use to create the client.</param>
        /// <param name="clientOptions">(Optional) client options</param>
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
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClient(
            string accountEndpoint,
            string accountKey,
            CosmosClientOptions clientOptions = null)
        {
            if (accountEndpoint == null)
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            if (accountKey == null)
            {
                throw new ArgumentNullException(nameof(accountKey));
            }

            if (clientOptions == null)
            {
                clientOptions = new CosmosClientOptions();
            }

            this.Endpoint = new Uri(accountEndpoint);
            this.AccountKey = accountKey;
            CosmosClientOptions clientOptionsClone = clientOptions.Clone();

            DocumentClient documentClient = new DocumentClient(
                this.Endpoint,
                this.AccountKey,
                apitype: clientOptionsClone.ApiType,
                sendingRequestEventArgs: clientOptionsClone.SendingRequestEventArgs,
                transportClientHandlerFactory: clientOptionsClone.TransportClientHandlerFactory,
                connectionPolicy: clientOptionsClone.GetConnectionPolicy(),
                enableCpuMonitor: clientOptionsClone.EnableCpuMonitor,
                storeClientFactory: clientOptionsClone.StoreClientFactory);

            this.Init(
                clientOptionsClone,
                documentClient);
        }

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        internal CosmosClient(
            string accountEndpoint,
            string accountKey,
            CosmosClientOptions cosmosClientOptions,
            DocumentClient documentClient)
        {
            if (accountEndpoint == null)
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            if (accountKey == null)
            {
                throw new ArgumentNullException(nameof(accountKey));
            }

            if (cosmosClientOptions == null)
            {
                throw new ArgumentNullException(nameof(cosmosClientOptions));
            }

            if (documentClient == null)
            {
                throw new ArgumentNullException(nameof(documentClient));
            }

            this.Endpoint = new Uri(accountEndpoint);
            this.AccountKey = accountKey;

            this.Init(cosmosClientOptions, documentClient);
        }

        /// <summary>
        /// The <see cref="Cosmos.CosmosClientOptions"/> used initialize CosmosClient
        /// </summary>
        public virtual CosmosClientOptions ClientOptions { get; private set; }

        /// <summary>
        /// Gets the endpoint Uri for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the account endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        public virtual Uri Endpoint { get; }

        /// <summary>
        /// Gets the AuthKey used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        internal string AccountKey { get; }

        internal CosmosOffers Offers => this.offerSet.Value;
        internal DocumentClient DocumentClient { get; set; }
        internal RequestInvokerHandler RequestHandler { get; private set; }
        internal ConsistencyLevel AccountConsistencyLevel { get; private set; }
        internal CosmosResponseFactory ResponseFactory { get; private set; }
        internal CosmosClientContext ClientContext { get; private set; }

        /// <summary>
        /// Read the <see cref="Microsoft.Azure.Cosmos.AccountProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public virtual Task<AccountProperties> ReadAccountAsync()
        {
            return ((IDocumentClientInternal)this.DocumentClient).GetDatabaseAccountInternalAsync(this.Endpoint);
        }

        /// <summary>
        /// Returns a reference to a database object. 
        /// </summary>
        /// <param name="id">The cosmos database id</param>
        /// <remarks>
        /// Note that the database must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Database db = this.cosmosClient.GetDatabase("myDatabaseId"];
        /// DatabaseResponse response = await db.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>Cosmos database proxy</returns>
        public virtual Database GetDatabase(string id)
        {
            return new DatabaseCore(this.ClientContext, id);
        }

        /// <summary>
        /// Get cosmos container proxy. 
        /// </summary>
        /// <remarks>Proxy existence doesn't guarantee either database or container existence.</remarks>
        /// <param name="databaseId">cosmos database name</param>
        /// <param name="containerId">cosmos container name</param>
        /// <returns>Cosmos container proxy</returns>
        public virtual Container GetContainer(string databaseId, string containerId)
        {
            if (string.IsNullOrEmpty(databaseId))
            {
                throw new ArgumentNullException(nameof(databaseId));
            }

            if (string.IsNullOrEmpty(containerId))
            {
                throw new ArgumentNullException(nameof(containerId));
            }

            return this.GetDatabase(databaseId).GetContainer(containerId);
        }

        /// <summary>
        /// Send a request for creating a database.
        ///
        /// A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.
        ///
        /// Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.
        /// </summary>
        /// <param name="id">The database id.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual Task<DatabaseResponse> CreateDatabaseAsync(
                string id,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            DatabaseProperties databaseProperties = this.PrepareDatabaseProperties(id);
            return this.CreateDatabaseAsync(
                databaseProperties: databaseProperties,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Check if a database exists, and if it doesn't, create it.
        /// This will make a read operation, and if the database is not found it will do a create operation.
        ///
        /// A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.
        ///
        /// Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.
        /// </summary>
        /// <param name="id">The database id.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of additional options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            // Doing a Read before Create will give us better latency for existing databases
            Database database = this.GetDatabase(id);
            DatabaseResponse cosmosDatabaseResponse = await database.ReadAsync(cancellationToken: cancellationToken);
            if (cosmosDatabaseResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return cosmosDatabaseResponse;
            }

            cosmosDatabaseResponse = await this.CreateDatabaseAsync(id, throughput, requestOptions, cancellationToken: cancellationToken);
            if (cosmosDatabaseResponse.StatusCode != HttpStatusCode.Conflict)
            {
                return cosmosDatabaseResponse;
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await database.ReadAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets an iterator to go through all the databases for the account
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <example>
        /// Get an iterator for all the database under the cosmos account
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<DatabaseProperties> feedIterator = this.cosmosClient.GetDatabasesIterator();
        /// {
        ///     foreach (DatabaseProperties databaseProperties in  await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(databaseProperties.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the databases.</returns>
        public virtual FeedIterator<DatabaseProperties> GetDatabaseQueryIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            FeedIterator databaseStreamIterator = this.GetDatabaseQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            return new FeedStatelessIteratorCore<DatabaseProperties>(
                databaseStreamIterator,
                this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<DatabaseProperties>);
        }

        /// <summary>
        /// Gets an iterator to go through all the containers for the database
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the containers</returns>
        public virtual FeedIterator GetDatabaseQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedStatelessIteratorCore(
               this.ClientContext,
               this.DatabaseRootUri,
               ResourceType.Collection,
               queryDefinition,
               continuationToken,
               requestOptions);
        }

        /// <summary>
        /// Send a request for creating a database.
        ///
        /// A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.
        ///
        /// Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.
        /// </summary>
        /// <param name="databaseProperties">The database properties</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual Task<ResponseMessage> CreateDatabaseStreamAsync(
                DatabaseProperties databaseProperties,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            if (databaseProperties == null)
            {
                throw new ArgumentNullException(nameof(databaseProperties));
            }

            this.ClientContext.ValidateResource(databaseProperties.Id);
            Stream streamPayload = this.ClientContext.PropertiesSerializer.ToStream<DatabaseProperties>(databaseProperties);

            return this.CreateDatabaseStreamInternalAsync(streamPayload, throughput, requestOptions, cancellationToken);
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void Init(
            CosmosClientOptions clientOptions,
            DocumentClient documentClient)
        {
            this.ClientOptions = clientOptions;
            this.DocumentClient = documentClient;

            //Request pipeline 
            ClientPipelineBuilder clientPipelineBuilder = new ClientPipelineBuilder(
                this,
                this.DocumentClient.ResetSessionTokenRetryPolicy,
                this.ClientOptions.CustomHandlers);

            // DocumentClient is not initialized with any consistency overrides so default is backend consistency
            this.AccountConsistencyLevel = (ConsistencyLevel)this.DocumentClient.ConsistencyLevel;

            this.RequestHandler = clientPipelineBuilder.Build();

            this.ResponseFactory = new CosmosResponseFactory(
                defaultJsonSerializer: this.ClientOptions.PropertiesSerializer,
                userJsonSerializer: this.ClientOptions.CosmosSerializerWithWrapperOrDefault);

            this.ClientContext = new ClientContextCore(
                client: this,
                clientOptions: this.ClientOptions,
                userJsonSerializer: this.ClientOptions.CosmosSerializerWithWrapperOrDefault,
                defaultJsonSerializer: this.ClientOptions.PropertiesSerializer,
                cosmosResponseFactory: this.ResponseFactory,
                requestHandler: this.RequestHandler,
                documentClient: this.DocumentClient,
                documentQueryClient: new DocumentQueryClient(this.DocumentClient));

            this.offerSet = new Lazy<CosmosOffers>(() => new CosmosOffers(this.DocumentClient), LazyThreadSafetyMode.PublicationOnly);
        }

        internal DatabaseProperties PrepareDatabaseProperties(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            DatabaseProperties databaseProperties = new DatabaseProperties()
            {
                Id = id
            };

            this.ClientContext.ValidateResource(databaseProperties.Id);
            return databaseProperties;
        }

        internal Task<DatabaseResponse> CreateDatabaseAsync(
                    DatabaseProperties databaseProperties,
                    int? throughput = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.CreateDatabaseStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream<DatabaseProperties>(databaseProperties),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this.GetDatabase(databaseProperties.Id), response);
        }

        private Task<ResponseMessage> CreateDatabaseStreamInternalAsync(
                Stream streamPayload,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.DatabaseRootUri,
                resourceType: ResourceType.Database,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: streamPayload,
                requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputHeader(throughput),
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<DatabaseProperties>> DatabaseFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.ClientContext.ProcessResourceOperationAsync<FeedResponse<DatabaseProperties>>(
                resourceUri: this.DatabaseRootUri,
                resourceType: ResourceType.Database,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<DatabaseProperties>(response),
                cancellationToken: cancellationToken);
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
    }
}
