//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for creating new databases, and reading/querying all databases
    ///
    /// <see cref="CosmosDatabase"/>for reading, replacing, or deleting an existing container;
    /// </summary>
    public partial class CosmosClient
    {
        internal CosmosClientContext ClientContext { get; private set; }

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
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="CosmosDatabaseSettings"/> containing the resource record.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual Task<DatabaseResponse> CreateDatabaseAsync(
                string id,
                int? requestUnitsPerSecond = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            CosmosDatabaseSettings databaseSettings = this.PrepareCosmosDatabaseSettings(id);
            return this.CreateDatabaseAsync(
                databaseSettings: databaseSettings,
                requestUnitsPerSecond: requestUnitsPerSecond,
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
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of additional options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="CosmosDatabaseSettings"/> containing the resource record.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? requestUnitsPerSecond = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            // Doing a Read before Create will give us better latency for existing databases
            CosmosDatabase database = this.GetDatabase(id);
            DatabaseResponse cosmosDatabaseResponse = await database.ReadAsync(cancellationToken: cancellationToken);
            if (cosmosDatabaseResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return cosmosDatabaseResponse;
            }

            cosmosDatabaseResponse = await this.CreateDatabaseAsync(id, requestUnitsPerSecond, requestOptions, cancellationToken: cancellationToken);
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
        /// <param name="maxItemCount">The max item count to return as part of the query</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the database under the cosmos account
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<CosmosDatabaseSettings> feedIterator = this.cosmosClient.GetDatabasesIterator();
        /// {
        ///     foreach (CosmosDatabaseSettings databaseSettings in  await feedIterator.FetchNextSetAsync())
        ///     {
        ///         Console.WriteLine(setting.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the databases.</returns>
        public virtual FeedIterator<CosmosDatabaseSettings> GetDatabasesIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosDatabaseSettings>(
                maxItemCount,
                continuationToken,
                options: null,
                nextDelegate: this.DatabaseFeedRequestExecutorAsync);
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
        /// CosmosDatabase db = this.cosmosClient.GetDatabase("myDatabaseId"];
        /// DatabaseResponse response = await db.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>Cosmos database proxy</returns>
        public virtual CosmosDatabase GetDatabase(string id)
        {
            return new CosmosDatabaseCore(this.ClientContext, id);
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
        /// <param name="databaseSettings">The database settings</param>
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a database in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="CosmosDatabaseSettings"/> containing the resource record.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual Task<CosmosResponseMessage> CreateDatabaseStreamAsync(
                CosmosDatabaseSettings databaseSettings,
                int? requestUnitsPerSecond = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            if (databaseSettings == null)
            {
                throw new ArgumentNullException(nameof(databaseSettings));
            }

            this.ClientContext.ValidateResource(databaseSettings.Id);
            Stream streamPayload = this.ClientContext.SettingsSerializer.ToStream<CosmosDatabaseSettings>(databaseSettings);

            return this.CreateDatabaseStreamInternalAsync(streamPayload, requestUnitsPerSecond, requestOptions, cancellationToken);
        }

        private Task<CosmosResponseMessage> CreateDatabaseStreamInternalAsync(
                Stream streamPayload,
                int? requestUnitsPerSecond = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            Uri resourceUri = new Uri(Paths.Databases_Root, UriKind.Relative);
            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: ResourceType.Database,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: streamPayload,
                requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputHeader(requestUnitsPerSecond),
                cancellationToken: cancellationToken);
        }

        internal CosmosDatabaseSettings PrepareCosmosDatabaseSettings(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            CosmosDatabaseSettings databaseSettings = new CosmosDatabaseSettings()
            {
                Id = id
            };

            this.ClientContext.ValidateResource(databaseSettings.Id);
            return databaseSettings;
        }

        internal Task<DatabaseResponse> CreateDatabaseAsync(
                    CosmosDatabaseSettings databaseSettings,
                    int? requestUnitsPerSecond = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.CreateDatabaseStreamInternalAsync(
                streamPayload: this.ClientContext.SettingsSerializer.ToStream<CosmosDatabaseSettings>(databaseSettings),
                requestUnitsPerSecond: requestUnitsPerSecond,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this.GetDatabase(databaseSettings.Id), response);
        }

        private Task<FeedResponse<CosmosDatabaseSettings>> DatabaseFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            Uri resourceUri = new Uri(Paths.Databases_Root, UriKind.Relative);
            return this.ClientContext.ProcessResourceOperationAsync<FeedResponse<CosmosDatabaseSettings>>(
                resourceUri: resourceUri,
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
                responseCreator: response => this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosDatabaseSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
