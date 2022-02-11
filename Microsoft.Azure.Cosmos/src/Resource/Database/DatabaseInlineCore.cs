//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class DatabaseInlineCore : DatabaseCore
    {
        internal DatabaseInlineCore(
           CosmosClientContext clientContext,
           string databaseId)
            : base(
               clientContext,
               databaseId)
        {
        }

        public override Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerAsync(containerProperties, throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerAsync(string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerAsync(id, partitionKeyPath, throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerIfNotExistsAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerIfNotExistsAsync(containerProperties, throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerIfNotExistsAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerIfNotExistsAsync(id, partitionKeyPath, throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerStreamAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerStreamAsync(containerProperties, throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<UserResponse> CreateUserAsync(string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateUserAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateUserAsync(id, requestOptions, trace, cancellationToken));
        }

        public override ContainerBuilder DefineContainer(
            string name,
            string partitionKeyPath)
        {
            return base.DefineContainer(name, partitionKeyPath);
        }

        public override Task<DatabaseResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.DeleteAsync(requestOptions, trace, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteStreamAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.DeleteStreamAsync(requestOptions, trace, cancellationToken));
        }

        public override Container GetContainer(string id)
        {
            return base.GetContainer(id);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
                   QueryDefinition queryDefinition,
                   string continuationToken = null,
                   QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetContainerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetContainerQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetContainerQueryStreamIterator(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetContainerQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override User GetUser(string id)
        {
            return base.GetUser(id);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetUserQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetUserQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override Task<DatabaseResponse> ReadAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.ReadAsync(requestOptions, trace, cancellationToken));
        }

        public override Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadStreamAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.ReadStreamAsync(requestOptions, trace, cancellationToken));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadThroughputAsync),
                null,
                (trace, diagnosticAttributes) => base.ReadThroughputAsync(trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadThroughputAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.ReadThroughputAsync(requestOptions, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceThroughputAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.ReplaceThroughputAsync(throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceThroughputAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.ReplaceThroughputAsync(throughputProperties, requestOptions, trace, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerAsync(containerProperties, throughputProperties, requestOptions, trace, cancellationToken));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerIfNotExistsAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerIfNotExistsAsync(containerProperties, throughputProperties, requestOptions, trace, cancellationToken));
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateContainerStreamAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateContainerStreamAsync(containerProperties, throughputProperties, requestOptions, trace, cancellationToken));
        }

        public override Task<UserResponse> UpsertUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(UpsertUserAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.UpsertUserAsync(id, requestOptions, trace, cancellationToken));
        }

        public override ClientEncryptionKey GetClientEncryptionKey(string id)
        {
            return base.GetClientEncryptionKey(id);
        }

        public override FeedIterator<ClientEncryptionKeyProperties> GetClientEncryptionKeyQueryIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)  
        {
            return base.GetClientEncryptionKeyQueryIterator(queryDefinition, continuationToken, requestOptions);
        }

        public override Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateClientEncryptionKeyAsync),
                requestOptions,
                (trace, diagnosticAttributes) => base.CreateClientEncryptionKeyAsync(trace, clientEncryptionKeyProperties, requestOptions, cancellationToken));
        }
    }
}
