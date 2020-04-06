//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Documents;

    internal sealed class CosmosClientCore : IDisposable
    {
        private readonly Uri DatabaseRootUri = new Uri(Paths.Databases_Root, UriKind.Relative);
        private ConsistencyLevel? accountConsistencyLevel;
        private bool isDisposed = false;

        static CosmosClientCore()
        {
            HttpConstants.Versions.CurrentVersion = HttpConstants.Versions.v2018_12_31;
            HttpConstants.Versions.CurrentVersionUTF8 = Encoding.UTF8.GetBytes(HttpConstants.Versions.CurrentVersion);

            // V3 always assumes assemblies exists
            // Shall revisit on feedback
            // NOTE: Native ServiceInteropWrapper.AssembliesExist has appsettings dependency which are proofed for CTL (native dll entry) scenarios.
            // Revert of this depends on handling such in direct assembly
            ServiceInteropWrapper.AssembliesExist = new Lazy<bool>(() => true);
        }

        public CosmosClientCore(
            string accountEndpoint,
            string authKeyOrResourceToken,
            CosmosClientOptions clientOptions)
        {
            if (accountEndpoint == null)
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            if (authKeyOrResourceToken == null)
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
            }

            this.Endpoint = new Uri(accountEndpoint);
            this.AccountKey = authKeyOrResourceToken;

            this.ClientContext = ClientContextCore.Create(
                this,
                clientOptions);
        }

        internal CosmosClientCore(
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

            this.ClientContext = ClientContextCore.Create(
                 this,
                 documentClient,
                 cosmosClientOptions);
        }

        public CosmosClientOptions ClientOptions => this.ClientContext.ClientOptions;
        public Uri Endpoint { get; }
        internal string AccountKey { get; }
        internal DocumentClient DocumentClient => this.ClientContext.DocumentClient;
        internal RequestInvokerHandler RequestHandler => this.ClientContext.RequestHandler;
        internal CosmosClientContext ClientContext { get; }

        public Task<AccountProperties> ReadAccountAsync()
        {
            return ((IDocumentClientInternal)this.DocumentClient).GetDatabaseAccountInternalAsync(this.Endpoint);
        }

        internal DatabaseInlineCore GetDatabase(string id)
        {
            return new DatabaseInlineCore(this.ClientContext, id);
        }

        public Container GetContainer(string databaseId, string containerId)
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

        public Task<DatabaseResponse> CreateDatabaseAsync(
                string id,
                int? throughput,
                RequestOptions requestOptions,
                CosmosDiagnosticsContext diagnostics,
                CancellationToken cancellationToken)
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
                diagnostics: diagnostics,
                cancellationToken: cancellationToken);
        }

        public async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            // Doing a Read before Create will give us better latency for existing databases
            DatabaseProperties databaseProperties = this.PrepareDatabaseProperties(id);
            DatabaseInlineCore databaseInlineCore = this.GetDatabase(id);
            DatabaseCore databaseCore = databaseInlineCore.DatabaseCore;
            ResponseMessage readResponse = await databaseCore.ReadStreamAsync(
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            if (readResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return await this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(databaseInlineCore, Task.FromResult(readResponse));
            }

            ResponseMessage createResponse = await this.CreateDatabaseStreamAsync(
                databaseProperties,
                throughput,
                requestOptions,
                diagnosticsContext,
                cancellationToken);

            // Merge the diagnostics with the first read request.
            createResponse.DiagnosticsContext.AddDiagnosticsInternal(readResponse.DiagnosticsContext);
            if (createResponse.StatusCode != HttpStatusCode.Conflict)
            {
                return await this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(databaseInlineCore, Task.FromResult(createResponse));
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            ResponseMessage readResponseAfterConflict = await databaseCore.ReadStreamAsync(
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return await this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(databaseInlineCore, Task.FromResult(readResponseAfterConflict));
        }

        public FeedIterator<T> GetDatabaseQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return this.GetDatabaseQueryIteratorHelper<T>(
                    queryDefinition,
                    continuationToken,
                    requestOptions);
        }

        public FeedIterator GetDatabaseQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return this.GetDatabaseQueryStreamIteratorHelper(
                    queryDefinition,
                    continuationToken,
                    requestOptions);
        }

        public FeedIterator<T> GetDatabaseQueryIterator<T>(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetDatabaseQueryIteratorHelper<T>(
                    queryDefinition,
                    continuationToken,
                    requestOptions);
        }

        public FeedIterator GetDatabaseQueryStreamIterator(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetDatabaseQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions);
        }

        public Task<ResponseMessage> CreateDatabaseStreamAsync(
            DatabaseProperties databaseProperties,
            int? throughput,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (databaseProperties == null)
            {
                throw new ArgumentNullException(nameof(databaseProperties));
            }

            this.ClientContext.ValidateResource(databaseProperties.Id);
            Stream streamPayload = this.ClientContext.SerializerCore.ToStream<DatabaseProperties>(databaseProperties);

            return this.CreateDatabaseStreamInternalAsync(
                    streamPayload,
                    throughput,
                    requestOptions,
                    diagnosticsContext,
                    cancellationToken);
        }

        internal async Task<ConsistencyLevel> GetAccountConsistencyLevelAsync()
        {
            if (!this.accountConsistencyLevel.HasValue)
            {
                this.accountConsistencyLevel = await this.DocumentClient.GetDefaultConsistencyLevelAsync();
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
                    int? throughput,
                    RequestOptions requestOptions,
                    CosmosDiagnosticsContext diagnostics,
                    CancellationToken cancellationToken)
        {
            Task<ResponseMessage> response = this.CreateDatabaseStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream<DatabaseProperties>(databaseProperties),
                throughput: throughput,
                requestOptions: requestOptions,
                diagnostics: diagnostics,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this.GetDatabase(databaseProperties.Id), response);
        }

        private Task<ResponseMessage> CreateDatabaseStreamInternalAsync(
                Stream streamPayload,
                int? throughput,
                RequestOptions requestOptions,
                CosmosDiagnosticsContext diagnostics,
                CancellationToken cancellationToken)
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
                diagnosticsContext: diagnostics,
                cancellationToken: cancellationToken);
        }

        private FeedIteratorInternal<T> GetDatabaseQueryIteratorHelper<T>(
           QueryDefinition queryDefinition,
           string continuationToken,
           QueryRequestOptions requestOptions)
        {
            if (!(this.GetDatabaseQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal databaseStreamIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                    databaseStreamIterator,
                    (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                        responseMessage: response,
                        resourceType: ResourceType.Database));
        }

        private FeedIteratorInternal GetDatabaseQueryStreamIteratorHelper(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return FeedIteratorCore.CreateForNonPartitionedResource(
               clientContext: this.ClientContext,
               resourceLink: this.DatabaseRootUri,
               resourceType: ResourceType.Database,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
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
        private void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.ClientContext.Dispose();
                }

                this.isDisposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException($"Accessing {nameof(CosmosClient)} after it is disposed is invalid.");
            }
        }
    }
}
