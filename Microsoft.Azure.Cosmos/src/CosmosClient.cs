//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Provides a client-side logical representation of the Azure Cosmos DB account.
    /// This client can be used to configure and execute requests in the Azure Cosmos DB database service.
    /// 
    /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
    /// of the application which enables efficient connection management and performance. Please refer to the
    /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
    /// </summary>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created with the connection string and configured to use "East US 2" region.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Cosmos;
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
    /// This example creates a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and configured to use "East US 2" region.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Cosmos;
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
    /// This example creates a <see cref="CosmosClient"/>, <see cref="Database"/>, and a <see cref="Container"/>.
    /// The CosmosClient is created through builder pattern using <see cref="Fluent.CosmosClientBuilder"/>.
    /// <code language="c#">
    /// <![CDATA[
    /// using Microsoft.Azure.Cosmos;
    /// using Microsoft.Azure.Cosmos.Fluent;
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
    /// <seealso cref="CosmosClientOptions"/>
    /// <seealso cref="Fluent.CosmosClientBuilder"/>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">Performance Tips</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk">Diagnose and troubleshoot issues</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/distribute-data-globally">Global data distribution</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/partitioning-overview">Partitioning and horizontal scaling</seealso>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
    public class CosmosClient : IDisposable
    {
        private readonly CosmosClientCore cosmosClientCore;

        static CosmosClient()
        {
            HttpConstants.Versions.CurrentVersion = HttpConstants.Versions.v2018_12_31;
            HttpConstants.Versions.CurrentVersionUTF8 = Encoding.UTF8.GetBytes(HttpConstants.Versions.CurrentVersion);

            // V3 always assumes assemblies exists
            // Shall revisit on feedback
            // NOTE: Native ServiceInteropWrapper.AssembliesExist has appsettings dependency which are proofed for CTL (native dll entry) scenarios.
            // Revert of this depends on handling such in direct assembly
            ServiceInteropWrapper.AssembliesExist = new Lazy<bool>(() => true);
        }

        /// <summary>
        /// Create a new CosmosClient used for mock testing
        /// </summary>
        protected CosmosClient()
        {
        }

        /// <summary>
        /// Creates a new CosmosClient with the connection string.
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. ex: https://mycosmosaccount.documents.azure.com:443/;AccountKey=SuperSecretKey; </param>
        /// <param name="clientOptions">(Optional) client options</param>
        /// <example>
        /// The CosmosClient is created with the connection string and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
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
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">Performance Tips</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk">Diagnose and troubleshoot issues</seealso>
        public CosmosClient(
            string connectionString,
            CosmosClientOptions clientOptions = null)
            : this(
                  CosmosClientOptions.GetAccountEndpoint(connectionString),
                  CosmosClientOptions.GetAccountKey(connectionString),
                  clientOptions)
        {
        }

        /// <summary>
        /// Creates a new CosmosClient with the account endpoint URI string and account key.
        /// 
        /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
        /// of the application which enables efficient connection management and performance. Please refer to the
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">performance guide</see>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use</param>
        /// <param name="authKeyOrResourceToken">The cosmos account key or resource token to use to create the client.</param>
        /// <param name="clientOptions">(Optional) client options</param>
        /// <example>
        /// The CosmosClient is created with the AccountEndpoint, AccountKey or ResourceToken and configured to use "East US 2" region.
        /// <code language="c#">
        /// <![CDATA[
        /// using Microsoft.Azure.Cosmos;
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
        /// <seealso cref="CosmosClientOptions"/>
        /// <seealso cref="Fluent.CosmosClientBuilder"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/performance-tips">Performance Tips</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk">Diagnose and troubleshoot issues</seealso>
        public CosmosClient(
            string accountEndpoint,
            string authKeyOrResourceToken,
            CosmosClientOptions clientOptions = null)
        {
            this.cosmosClientCore = new CosmosClientCore(
                accountEndpoint,
                authKeyOrResourceToken,
                clientOptions);
        }

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        /// <remarks>This constructor should be removed at some point. The mocking should happen in a derivied class.</remarks>
        internal CosmosClient(
            string accountEndpoint,
            string authKeyOrResourceToken,
            CosmosClientOptions cosmosClientOptions,
            DocumentClient documentClient)
        {
            this.cosmosClientCore = new CosmosClientCore(
                accountEndpoint,
                authKeyOrResourceToken,
                cosmosClientOptions,
                documentClient);
        }

        /// <summary>
        /// The <see cref="Cosmos.CosmosClientOptions"/> used initialize CosmosClient.
        /// </summary>
        public virtual CosmosClientOptions ClientOptions => this.cosmosClientCore.ClientOptions;

        /// <summary>
        /// Gets the endpoint Uri for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the account endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        public virtual Uri Endpoint => this.cosmosClientCore.Endpoint;

        /// <summary>
        /// Gets the AuthKey or resource token used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        internal string AccountKey => this.cosmosClientCore.AccountKey;

        internal DocumentClient DocumentClient => this.cosmosClientCore.DocumentClient;
        internal RequestInvokerHandler RequestHandler => this.cosmosClientCore.RequestHandler;
        internal CosmosClientContext ClientContext => this.cosmosClientCore.ClientContext;

        /// <summary>
        /// Reads the <see cref="Microsoft.Azure.Cosmos.AccountProperties"/> for the Azure Cosmos DB account.
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public virtual Task<AccountProperties> ReadAccountAsync()
        {
            return CosmosClientContext.ProcessHelperAsync(
               requestOptions: null,
               (diagnostics) => this.cosmosClientCore.ReadAccountAsync());
        }

        /// <summary>
        /// Returns a proxy reference to a database. 
        /// </summary>
        /// <param name="id">The Cosmos database id</param>
        /// <remarks>
        /// <see cref="Database"/> proxy reference doesn't guarantee existence.
        /// Please ensure database exists through <see cref="CosmosClient.CreateDatabaseAsync(string, int?, RequestOptions, CancellationToken)"/> 
        /// or <see cref="CosmosClient.CreateDatabaseIfNotExistsAsync(string, int?, RequestOptions, CancellationToken)"/>, before
        /// operating on it.
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
        public virtual Database GetDatabase(string id)
        {
            return this.cosmosClientCore.GetDatabase(id);
        }

        /// <summary>
        /// Returns a proxy reference to a container. 
        /// </summary>
        /// <remarks>
        /// <see cref="Container"/> proxy reference doesn't guarantee existence.
        /// Please ensure container exists through <see cref="Database.CreateContainerAsync(ContainerProperties, int?, RequestOptions, CancellationToken)"/> 
        /// or <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, int?, RequestOptions, CancellationToken)"/>, before
        /// operating on it.
        /// </remarks>
        /// <param name="databaseId">Cosmos database name</param>
        /// <param name="containerId">Cosmos container name</param>
        /// <returns>Cosmos container proxy</returns>
        public virtual Container GetContainer(string databaseId, string containerId)
        {
            return this.cosmosClientCore.GetContainer(databaseId, containerId);
        }

        /// <summary>
        /// Sends a request for creating a database.
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<DatabaseResponse> CreateDatabaseAsync(
                string id,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.cosmosClientCore.CreateDatabaseAsync(
                    id: id,
                    throughput: throughput,
                    requestOptions: requestOptions,
                    diagnostics: diagnostics,
                    cancellationToken: cancellationToken));
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
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the resource record.
        /// <list type="table">
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
        /// </returns>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return CosmosClientContext.ProcessHelperAsync(
                 requestOptions: requestOptions,
                 (diagnostics) => this.cosmosClientCore.CreateDatabaseIfNotExistsAsync(
                     id,
                     throughput,
                     requestOptions,
                     diagnostics,
                     cancellationToken));
         }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement with parameterized values. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the databases.</returns>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// This create the type feed iterator for database with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c where c.status like @status")
        ///     .WithParameter("@status", "start%");
        /// FeedIterator<DatabaseProperties> feedIterator = this.users.GetDatabaseQueryIterator<DatabaseProperties>(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<DatabaseProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var database in response)
        ///     {
        ///         Console.WriteLine(database);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator<T> GetDatabaseQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(
                this.cosmosClientCore.GetDatabaseQueryIterator<T>(
                    queryDefinition,
                    continuationToken,
                    requestOptions));
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement with parameterized values. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the query request.</param>
        /// <returns>An iterator to go through the databases</returns>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadStreamAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// Example on how to fully drain the query results.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("select * From c where c._rid = @rid")
        ///               .WithParameter("@rid", "TheRidValue");
        /// FeedIterator feedIterator = this.CosmosClient.GetDatabaseQueryStreamIterator(
        ///     queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status for errors
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator GetDatabaseQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(
                this.cosmosClientCore.GetDatabaseQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions));
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the databases.</returns>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// This create the type feed iterator for database with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.status like 'start%'";
        /// FeedIterator<DatabaseProperties> feedIterator = this.users.GetDatabaseQueryIterator<DatabaseProperties>(queryText);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<DatabaseProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var database in response)
        ///     {
        ///         Console.WriteLine(database);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator<T> GetDatabaseQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return new FeedIteratorInlineCore<T>(
                this.cosmosClientCore.GetDatabaseQueryIterator<T>(
                    queryDefinition,
                    continuationToken,
                    requestOptions));
        }

        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the query request.</param>
        /// <returns>An iterator to go through the databases</returns>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadStreamAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// Example on how to fully drain the query results.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator feedIterator = this.CosmosClient.GetDatabaseQueryStreamIterator(
        ///     ("select * From c where c._rid = 'TheRidValue'");
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status for errors
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator GetDatabaseQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(
                this.cosmosClientCore.GetDatabaseQueryStreamIterator(
                    queryText,
                    continuationToken,
                    requestOptions));
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public virtual Task<ResponseMessage> CreateDatabaseStreamAsync(
                DatabaseProperties databaseProperties,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.cosmosClientCore.CreateDatabaseStreamAsync(
                    databaseProperties,
                    throughput,
                    requestOptions,
                    diagnostics,
                    cancellationToken));
        }

        internal virtual Task<ConsistencyLevel> GetAccountConsistencyLevelAsync()
        {
            return this.cosmosClientCore.GetAccountConsistencyLevelAsync();
        }
        
        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        /// <param name="disposing">True if disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            this.cosmosClientCore.Dispose();
        }
    }
}
