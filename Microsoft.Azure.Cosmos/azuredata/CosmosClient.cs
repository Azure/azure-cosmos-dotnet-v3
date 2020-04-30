//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Provides a client-side logical representation of the Azure Cosmos DB account.
    /// This client can be used to configure and execute requests in the Azure Cosmos DB database service.
    /// 
    /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
    /// of the application which enables efficient connection management and performance. Please refer to 
    /// performance guide at <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>.
    /// </summary>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient is created with the connection string and configured to use "East US 2" region.
    /// <code language="c#">
    /// <![CDATA[
    /// using Azure.Cosmos;
    /// 
    /// CosmosClient cosmosClient = new CosmosClient(
    ///             "connection-string-from-portal", 
    ///             new CosmosClientOptions()
    ///             {
    ///                 ApplicationRegion = Regions.EastUS2,
    ///             });
    /// 
    /// Database db = await client.CreateDatabaseAsync("database-id");
    /// Container container = await db.CreateContainerAsync("container-id");
    /// 
    /// // Dispose cosmosClient at application exit
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and configured to use "East US 2" region.
    /// <code language="c#">
    /// <![CDATA[
    /// using Azure.Cosmos;
    /// 
    /// CosmosClient cosmosClient = new CosmosClient(
    ///             "account-endpoint-from-portal", 
    ///             "account-key-from-portal", 
    ///             new CosmosClientOptions()
    ///             {
    ///                 ApplicationRegion = Regions.EastUS2,
    ///             });
    /// 
    /// Database db = await client.CreateDatabaseAsync("database-id");
    /// Container container = await db.CreateContainerAsync("container-id");
    /// 
    /// // Dispose cosmosClient at application exit
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient is created through builder pattern <see cref="Fluent.CosmosClientBuilder"/>.
    /// <code language="c#">
    /// <![CDATA[
    /// using Azure.Cosmos;
    /// using Azure.Cosmos.Fluent;
    /// 
    /// CosmosClient cosmosClient = new CosmosClientBuilder("connection-string-from-portal")
    ///     .WithApplicationRegion("East US 2")
    ///     .Build();
    /// 
    /// Database db = await client.CreateDatabaseAsync("database-id")
    /// Container container = await db.CreateContainerAsync("container-id");
    /// 
    /// // Dispose cosmosClient at application exit
    /// ]]>
    /// </code>
    /// </example>
    /// <remarks>
    /// <seealso cref="CosmosClientOptions"/>
    /// <seealso cref="Fluent.CosmosClientBuilder"/>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk"/>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/distribute-data-globally" />
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/partitioning-overview" />
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units" />
    /// </remarks>
    public class CosmosClient : IDisposable
    {
        private readonly Uri DatabaseRootUri = new Uri(Paths.Databases_Root, UriKind.Relative);
        private ConsistencyLevel? accountConsistencyLevel;

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
        /// Create a new CosmosClient with the connection string
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to 
        /// performance guide at <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>.
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. ex: https://mycosmosaccount.documents.azure.com:443/;AccountKey=SuperSecretKey; </param>
        /// <example>
        /// The CosmosClient is created with the connection string and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Azure.Cosmos;
        /// 
        /// CosmosClient cosmosClient = new CosmosClient(
        ///             "connection-string-from-azure-portal");
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk"/>
        /// </remarks>
        public CosmosClient(string connectionString)
            : this(
                  CosmosClientOptions.GetAccountEndpoint(connectionString),
                  CosmosClientOptions.GetAccountKey(connectionString))
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the connection string
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to 
        /// performance guide at <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>.
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. ex: https://mycosmosaccount.documents.azure.com:443/;AccountKey=SuperSecretKey; </param>
        /// <param name="clientOptions">Client options</param>
        /// <example>
        /// The CosmosClient is created with the connection string and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Azure.Cosmos;
        /// 
        /// CosmosClient cosmosClient = new CosmosClient(
        ///             "connection-string-from-azure-portal",
        ///             new CosmosClientOptions()
        ///             {
        ///                 ApplicationRegion = Regions.EastUS2,
        ///             });
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk"/>
        /// </remarks>
        public CosmosClient(
            string connectionString,
            CosmosClientOptions clientOptions)
            : this(
                  CosmosClientOptions.GetAccountEndpoint(connectionString),
                  CosmosClientOptions.GetAccountKey(connectionString),
                  clientOptions)
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the account endpoint URI string and account key
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to 
        /// performance guide at <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use</param>
        /// <param name="authKeyOrResourceToken">The cosmos account key or resource token to use to create the client.</param>
        /// <param name="clientOptions">(Optional) client options</param>
        /// <example>
        /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Azure.Cosmos;
        /// 
        /// CosmosClient cosmosClient = new CosmosClient(
        ///             "account-endpoint-from-portal", 
        ///             "account-key-from-portal", 
        ///             new CosmosClientOptions()
        ///             {
        ///                 ApplicationRegion = Regions.EastUS2,
        ///             });
        /// 
        /// // Dispose cosmosClient at application exit
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk"/>
        /// </remarks>
        public CosmosClient(
            string accountEndpoint,
            string authKeyOrResourceToken,
            CosmosClientOptions clientOptions = null)
        {
            if (accountEndpoint == null)
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            if (authKeyOrResourceToken == null)
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
            }

            if (clientOptions == null)
            {
                clientOptions = new CosmosClientOptions();
            }

            this.Endpoint = new Uri(accountEndpoint);
            this.AccountKey = authKeyOrResourceToken;
            CosmosClientOptions clientOptionsClone = clientOptions.Clone();

            DocumentClient documentClient = new DocumentClient(
                this.Endpoint,
                this.AccountKey,
                apitype: clientOptionsClone.ApiType,
                sendingRequestEventArgs: clientOptionsClone.SendingRequestEventArgs,
                transportClientHandlerFactory: clientOptionsClone.TransportClientHandlerFactory,
                connectionPolicy: clientOptionsClone.GetConnectionPolicy(),
                enableCpuMonitor: clientOptionsClone.EnableCpuMonitor,
                storeClientFactory: clientOptionsClone.StoreClientFactory,
                desiredConsistencyLevel: clientOptionsClone.GetDocumentsConsistencyLevel(),
                handler: this.CreateHttpClientHandler(clientOptions));

            this.Init(
                clientOptionsClone,
                documentClient);
        }

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        internal CosmosClient(
            string accountEndpoint,
            string authKeyOrResourceToken,
            CosmosClientOptions cosmosClientOptions,
            DocumentClient documentClient)
        {
            if (accountEndpoint == null)
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            if (authKeyOrResourceToken == null)
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
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
            this.AccountKey = authKeyOrResourceToken;

            this.Init(cosmosClientOptions, documentClient);
        }

        /// <summary>
        /// The <see cref="Cosmos.CosmosClientOptions"/> used initialize CosmosClient
        /// </summary>
        internal CosmosClientOptions ClientOptions { get; private set; }

        /// <summary>
        /// Gets the endpoint Uri for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the account endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        public virtual Uri Endpoint { get; }

        /// <summary>
        /// Gets the AuthKey or resource token used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        internal string AccountKey { get; }

        internal DocumentClient DocumentClient { get; set; }
        internal RequestInvokerHandler RequestHandler { get; private set; }
        internal CosmosResponseFactory ResponseFactory { get; private set; }
        internal CosmosClientContext ClientContext { get; private set; }

        /// <summary>
        /// Read Azure Cosmos DB account properties <see cref="AccountProperties"/>
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public virtual Task<AccountProperties> ReadAccountAsync()
        {
            return ((IDocumentClientInternal)this.DocumentClient).GetDatabaseAccountInternalAsync(this.Endpoint);
        }

        /// <summary>
        /// Returns a proxy reference to a database. 
        /// </summary>
        /// <param name="id">The cosmos database id</param>
        /// <remarks>
        /// <see cref="CosmosDatabase"/> proxy reference doesn't guarantee existence.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Database db = cosmosClient.GetDatabase("myDatabaseId"];
        /// DatabaseResponse response = await db.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>Cosmos database proxy</returns>
        public virtual CosmosDatabase GetDatabase(string id)
        {
            return new DatabaseCore(this.ClientContext, id);
        }

        /// <summary>
        /// Returns a proxy reference to a container. 
        /// </summary>
        /// <remarks>
        /// <see cref="CosmosContainer"/> proxy reference doesn't guarantee existence.
        /// Please ensure container exists, before
        /// operating on it.
        /// </remarks>
        /// <param name="databaseId">cosmos database name</param>
        /// <param name="containerId">cosmos container name</param>
        /// <returns>Cosmos container proxy</returns>
        public virtual CosmosContainer GetContainer(string databaseId, string containerId)
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
        /// <returns>A <see cref="Task"/> containing a <see cref="Response"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
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
        /// <para>Check if a database exists, and if it doesn't, create it.
        /// Only the database id is used to verify if there is an existing database. Other database properties 
        /// such as throughput are not validated and can be different then the passed properties.</para>
        /// 
        /// <para>A database manages users, permissions and a set of containers.
        /// Each Azure Cosmos DB Database Account is able to support multiple independent named databases,
        /// with the database being the logical container for data.</para>
        ///
        /// <para>Each Database consists of one or more containers, each of which in turn contain one or more
        /// documents. Since databases are an administrative resource, the Service Master Key will be
        /// required in order to access and successfully complete any action using the User APIs.</para>
        /// </summary>
        /// <param name="id">The database id.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of additional options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="Response"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <list>
        ///     <listheader>
        ///         <term>StatusCode</term><description>Common success StatusCodes for the CreateDatabaseIfNotExistsAsync operation</description>
        ///     </listheader>
        ///     <item>
        ///         <term>201</term><description>Created - New database is created.</description>
        ///     </item>
        ///     <item>
        ///         <term>200</term><description>Accepted - This means the database already exists.</description>
        ///     </item>
        /// </list>
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
            DatabaseProperties databaseProperties = this.PrepareDatabaseProperties(id);
            CosmosDatabase database = this.GetDatabase(id);
            Response response = await database.ReadStreamAsync(requestOptions: requestOptions, cancellationToken: cancellationToken);
            if (response.Status != (int)HttpStatusCode.NotFound)
            {
                return await this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this.GetDatabase(databaseProperties.Id), Task.FromResult(response), cancellationToken);
            }

            response = await this.CreateDatabaseStreamAsync(databaseProperties, throughput, requestOptions, cancellationToken);
            if (response.Status != (int)HttpStatusCode.Conflict)
            {
                return await this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this.GetDatabase(databaseProperties.Id), Task.FromResult(response), cancellationToken);
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await database.ReadAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement with parameterized values. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the databases.</returns>
        public virtual AsyncPageable<T> GetDatabaseQueryResultsAsync<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator feedIterator = this.GetDatabaseFeedIterator(
               queryDefinition,
               continuationToken,
               requestOptions);

            PageIteratorCore<T> pageIterator = new PageIteratorCore<T>(
                feedIterator: feedIterator,
                responseCreator: this.ClientContext.ResponseFactory.CreateQueryFeedResponseWithPropertySerializer<T>);

            return PageResponseEnumerator.CreateAsyncPageable(continuation => pageIterator.GetPageAsync(continuation, cancellationToken));
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the databases.</returns>
        public virtual AsyncPageable<T> GetDatabaseQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetDatabaseQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement with parameterized values. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the databases</returns>
        public virtual async IAsyncEnumerable<Response> GetDatabaseStreamQueryResultsAsync(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator feedIterator = this.GetDatabaseFeedIterator(
               queryDefinition,
               continuationToken,
               requestOptions);

            while (feedIterator.HasMoreResults)
            {
                yield return await feedIterator.ReadNextAsync(cancellationToken);
            }
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the databases</returns>
        public virtual IAsyncEnumerable<Response> GetDatabaseQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetDatabaseQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        private FeedIterator GetDatabaseFeedIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
              this.ClientContext,
              this.DatabaseRootUri,
              ResourceType.Database,
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
        /// <returns>A <see cref="Task"/> containing a <see cref="Response"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual Task<Response> CreateDatabaseStreamAsync(
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
                new CosmosClientDriverContext(this),
                this.ClientOptions.CustomHandlers);

            this.RequestHandler = clientPipelineBuilder.Build();

            CosmosSerializer userSerializer = this.ClientOptions.GetCosmosSerializerWithWrapperOrDefault();
            this.ResponseFactory = new CosmosResponseFactory(
                defaultJsonSerializer: this.ClientOptions.PropertiesSerializer,
                userJsonSerializer: userSerializer);

            CosmosSerializer sqlQuerySpecSerializer = TextJsonCosmosSqlQuerySpecConverter.CreateSqlQuerySpecSerializer(
                cosmosSerializer: userSerializer,
                propertiesSerializer: this.ClientOptions.PropertiesSerializer);

            this.ClientContext = new ClientContextCore(
                client: this,
                clientOptions: this.ClientOptions,
                userJsonSerializer: userSerializer,
                defaultJsonSerializer: this.ClientOptions.PropertiesSerializer,
                sqlQuerySpecSerializer: sqlQuerySpecSerializer,
                cosmosResponseFactory: this.ResponseFactory,
                requestHandler: this.RequestHandler,
                documentClient: this.DocumentClient);
        }

        internal virtual async Task<ConsistencyLevel> GetAccountConsistencyLevelAsync()
        {
            if (!this.accountConsistencyLevel.HasValue)
            {
                this.accountConsistencyLevel = (ConsistencyLevel)await this.DocumentClient.GetDefaultConsistencyLevelAsync();
            }

            return this.accountConsistencyLevel.Value;
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
            Task<Response> response = this.CreateDatabaseStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream<DatabaseProperties>(databaseProperties),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this.GetDatabase(databaseProperties.Id), response, cancellationToken);
        }

        private Task<Response> CreateDatabaseStreamInternalAsync(
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

        private HttpClientHandler CreateHttpClientHandler(CosmosClientOptions clientOptions)
        {
            if (clientOptions == null || (clientOptions.WebProxy == null))
            {
                return null;
            }

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.Proxy = clientOptions.WebProxy;

            return httpClientHandler;
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
