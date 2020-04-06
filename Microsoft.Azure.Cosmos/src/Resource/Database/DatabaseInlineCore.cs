//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal class DatabaseInlineCore : Database
    {
        public override string Id => this.DatabaseCore.Id;

        internal virtual Uri LinkUri => this.DatabaseCore.LinkUri;

        internal CosmosClientContext ClientContext => this.DatabaseCore.ClientContext;
        internal DatabaseCore DatabaseCore { get; }

        /// <summary>
        /// Used for unit testing
        /// </summary>
        internal DatabaseInlineCore()
        {
        }

        internal DatabaseInlineCore(
            CosmosClientContext clientContext,
            string databaseId)
        {
            if (clientContext == null)
            {
                throw new ArgumentNullException(nameof(clientContext));
            }

            this.DatabaseCore = new DatabaseCore(this, clientContext, databaseId);
        }

        public override Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.CreateContainerAsync(containerProperties, throughput, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerAsync(string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.CreateContainerAsync(id, partitionKeyPath, throughput, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.CreateContainerIfNotExistsAsync(containerProperties, throughput, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.CreateContainerIfNotExistsAsync(id, partitionKeyPath, throughput, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.CreateContainerStreamAsync(containerProperties, throughput, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<UserResponse> CreateUserAsync(string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.CreateUserAsync(id, requestOptions, diagnostics, cancellationToken));
        }

        public override ContainerBuilder DefineContainer(
            string name,
            string partitionKeyPath)
        {
            return this.DatabaseCore.DefineContainer(name, partitionKeyPath);
        }

        public override Task<DatabaseResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.DeleteAsync(requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.DeleteStreamAsync(requestOptions, diagnostics, cancellationToken));
        }

        public override Container GetContainer(string id)
        {
            return this.DatabaseCore.GetContainer(id);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.DatabaseCore.GetContainerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.DatabaseCore.GetContainerQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetContainerQueryStreamIterator(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.DatabaseCore.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.DatabaseCore.GetContainerQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override User GetUser(string id)
        {
            return this.DatabaseCore.GetUser(id);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.DatabaseCore.GetUserQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.DatabaseCore.GetUserQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override Task<DatabaseResponse> ReadAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.ReadAsync(requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.ReadStreamAsync(requestOptions, diagnostics, cancellationToken));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: null,
                (diagnostics) => this.DatabaseCore.ReadThroughputAsync(diagnostics, cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.ReadThroughputAsync(requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.ReplaceThroughputAsync(throughput, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<UserResponse> UpsertUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.UpsertUserAsync(id, requestOptions, diagnostics, cancellationToken));
        }

#if PREVIEW
        public override
#else
        internal virtual
#endif
        DataEncryptionKey GetDataEncryptionKey(string id)
        {
            return this.DatabaseCore.GetDataEncryptionKey(id);
        }

#if PREVIEW
        public override
#else
        internal virtual
#endif
        FeedIterator<DataEncryptionKeyProperties> GetDataEncryptionKeyIterator(
            string startId = null,
            string endId = null,
            bool isDescending = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.DatabaseCore.GetDataEncryptionKeyIterator(startId, endId, isDescending, continuationToken, requestOptions);
        }

#if PREVIEW
        public override
#else
        internal virtual
#endif
        Task<DataEncryptionKeyResponse> CreateDataEncryptionKeyAsync(
            string id,
            CosmosEncryptionAlgorithm encryptionAlgorithm,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.DatabaseCore.CreateDataEncryptionKeyAsync(id, encryptionAlgorithm, encryptionKeyWrapMetadata, requestOptions, diagnostics, cancellationToken));
        }

        internal virtual Task<string> GetRIDAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.DatabaseCore.GetRIDAsync(diagnosticsContext, cancellationToken);
        }

        internal FeedIterator GetUserQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return this.DatabaseCore.GetUserQueryStreamIterator(queryDefinition, continuationToken, requestOptions);
        }

        internal FeedIterator GetUserQueryStreamIterator(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return this.DatabaseCore.GetUserQueryStreamIterator(queryText, continuationToken, requestOptions);
        }
    }
}
