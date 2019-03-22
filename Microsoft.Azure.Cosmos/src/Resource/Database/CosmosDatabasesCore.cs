//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// Operations for creating new databases, and reading/querying all databases
    ///
    /// <see cref="CosmosDatabaseCore"/>for reading, replacing, or deleting an existing container;
    /// </summary>
    /// <remarks>
    /// All these operations make calls against a fixed budget.
    /// You should design your system such that these calls scale sub-linearly with your application.
    /// For instance, do not call `databases.GetIterator` before every single `item.read` call, to ensure the database exists;
    /// do this once on application start up.
    /// </remarks>
    /// <example>
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosDatabaseResponse response = await this.cosmosClient.Databases.CreateDatabaseAsync(Guid.NewGuid().ToString());
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// This example shows how to access an existing database. This does not do a network call or verify that the database exists in Cosmos.
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosDatabase database = this.cosmosClient.Databases["MyDatabaseId"];
    /// ]]>
    /// </code>
    /// </example>
    public class CosmosDatabasesCore
    {
        private readonly CosmosClient client;
        private readonly ConcurrentDictionary<string, CosmosDatabaseCore> databasesCache;

        /// <summary>
        /// Use the Cosmos client reference to create <see cref="CosmosDatabasesCore"/>
        /// </summary>
        /// <param name="client">The <see cref="CosmosClient"/></param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosDatabaseResponse response = await this.cosmosClient.Databases.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
        /// ]]>
        /// </code>
        /// </example>
        protected internal CosmosDatabasesCore(CosmosClient client)
        {
            this.client = client;
            this.databasesCache = new ConcurrentDictionary<string, CosmosDatabaseCore>();
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
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosDatabaseResponse"/> which wraps a <see cref="CosmosDatabaseSettings"/> containing the resource record.</returns>
        public virtual Task<CosmosDatabaseResponse> CreateDatabaseAsync(
                string id,
                int? throughput = null,
                CosmosRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosDatabaseSettings databaseSettings = this.PrepareCosmosDatabaseSettings(id);
            return this.CreateDatabaseAsync(
                databaseSettings: databaseSettings,
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
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of additional options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosDatabaseResponse"/> which wraps a <see cref="CosmosDatabaseSettings"/> containing the resource record.</returns>
        public virtual async Task<CosmosDatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Doing a Read before Create will give us better latency for existing databases
            CosmosDatabaseCore database = this[id];
            CosmosDatabaseResponse cosmosDatabaseResponse = await database.ReadAsync(cancellationToken: cancellationToken);
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
        /// <param name="maxItemCount">The max item count to return as part of the query</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the database under the cosmos account
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosResultSetIterator<CosmosDatabaseSettings> setIterator = this.cosmosClient.Databases.GetDatabaseIterator();
        /// {
        ///     foreach (CosmosDatabaseSettings databaseSettings in  await setIterator.FetchNextSetAsync())
        ///     {
        ///         Console.WriteLine(setting.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator<CosmosDatabaseSettings> GetDatabaseIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosDatabaseSettings>(
                maxItemCount,
                continuationToken,
                options: null,
                nextDelegate: this.DatabaseFeedRequestExecutor);
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
        /// CosmosDatabase db = this.cosmosClient.Databases["myDatabaseId"];
        /// CosmosDatabaseResponse response = await db.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosDatabaseCore this[string id] =>
                // TODO: Argument check and singleton database
                this.databasesCache.GetOrAdd(
                    id,
                    keyName => new CosmosDatabaseCore(this.client, keyName));

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
        /// <param name="streamPayload">The database id.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) A set of options that can be set.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosDatabaseResponse"/> which wraps a <see cref="CosmosDatabaseSettings"/> containing the resource record.</returns>
        internal virtual Task<CosmosResponseMessage> CreateDatabaseStreamAsync(
                Stream streamPayload,
                int? throughput = null,
                CosmosRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            Uri resourceUri = new Uri(Paths.Databases_Root, UriKind.Relative);
            return ExecUtils.ProcessResourceOperationStreamAsync(
                this.client,
                resourceUri,
                ResourceType.Database,
                OperationType.Create,
                requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputHeader(throughput),
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

            this.client.DocumentClient.ValidateResource(databaseSettings);
            return databaseSettings;
        }

        internal virtual Task<CosmosDatabaseResponse> CreateDatabaseAsync(
                    CosmosDatabaseSettings databaseSettings,
                    int? throughput = null,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.CreateDatabaseStreamAsync(
                streamPayload: databaseSettings.GetResourceStream(),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateDatabaseResponse(this[databaseSettings.Id], response);
        }

        private Task<CosmosQueryResponse<CosmosDatabaseSettings>> DatabaseFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            Uri resourceUri = new Uri(Paths.Databases_Root, UriKind.Relative);
            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<CosmosDatabaseSettings>>(
                this.client,
                resourceUri,
                ResourceType.Database,
                OperationType.ReadFeed,
                options,
                request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                response => this.client.ResponseFactory.CreateResultSetQueryResponse<CosmosDatabaseSettings>(response),
                cancellationToken);
        }
    }
}
