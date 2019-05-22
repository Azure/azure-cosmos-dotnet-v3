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
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for creating new databases, and reading/querying all databases
    ///
    /// <see cref="CosmosDatabase"/>for reading, replacing, or deleting an existing container;
    /// </summary>
    internal class CosmosDatabasesCore : CosmosDatabases
    {
        private readonly ConcurrentDictionary<string, CosmosDatabase> databasesCache;

        protected internal CosmosDatabasesCore(CosmosClientContext clientContext)
        {
            this.ClientContext = clientContext;
            this.databasesCache = new ConcurrentDictionary<string, CosmosDatabase>();
        }

        internal CosmosClientContext ClientContext { get; }

        public override Task<DatabaseResponse> CreateDatabaseAsync(
                string id,
                int? throughput = null,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosDatabaseSettings databaseSettings = this.PrepareCosmosDatabaseSettings(id);
            return this.CreateDatabaseAsync(
                databaseSettings: databaseSettings,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Doing a Read before Create will give us better latency for existing databases
            CosmosDatabase database = this[id];
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

        public override FeedIterator<CosmosDatabaseSettings> GetDatabasesIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosDatabaseSettings>(
                maxItemCount,
                continuationToken,
                options: null,
                nextDelegate: this.DatabaseFeedRequestExecutor);
        }

        public override CosmosDatabase this[string id] =>
                // TODO: Argument check and singleton database
                this.databasesCache.GetOrAdd(
                    id,
                    keyName => new CosmosDatabaseCore(this.ClientContext, keyName));

        public override Task<CosmosResponseMessage> CreateDatabaseStreamAsync(
                Stream streamPayload,
                int? throughput = null,
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

            this.ClientContext.ValidateResource(databaseSettings.Id);
            return databaseSettings;
        }

        internal Task<DatabaseResponse> CreateDatabaseAsync(
                    CosmosDatabaseSettings databaseSettings,
                    int? throughput = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.CreateDatabaseStreamAsync(
                streamPayload: this.ClientContext.DefaultJsonSerializer.ToStream<CosmosDatabaseSettings>(databaseSettings),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this[databaseSettings.Id], response);
        }

        private Task<FeedResponse<CosmosDatabaseSettings>> DatabaseFeedRequestExecutor(
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
