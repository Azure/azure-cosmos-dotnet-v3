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

    internal sealed class CosmosDatabasesImpl : CosmosDatabases
    {
        private readonly CosmosClient client;
        private readonly ConcurrentDictionary<string, CosmosDatabase> databasesCache;

        internal CosmosDatabasesImpl(
            CosmosClient client)
        {
            this.client = client;
            this.databasesCache = new ConcurrentDictionary<string, CosmosDatabase>();
        }

        public override Task<CosmosDatabaseResponse> CreateDatabaseAsync(
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
        
        public override async Task<CosmosDatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Doing a Read before Create will give us better latency for existing databases
            CosmosDatabase database = this[id];
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

        public override CosmosResultSetIterator<CosmosDatabaseSettings> GetDatabaseIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosDatabaseSettings>(
                maxItemCount,
                continuationToken,
                options: null,
                nextDelegate: this.DatabaseFeedRequestExecutor);
        }

        public override CosmosDatabase this[string id] =>
                // TODO: Argument check and singleton database
                this.databasesCache.GetOrAdd(
                    id,
                    keyName => new CosmosDatabaseImpl(this.client, keyName));

        public override Task<CosmosResponseMessage> CreateDatabaseStreamAsync(
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

            CosmosIdentifier.ValidateResource(databaseSettings);
            return databaseSettings;
        }

        internal Task<CosmosDatabaseResponse> CreateDatabaseAsync(
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
