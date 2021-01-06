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
                (diagnostics, trace) => base.CreateContainerAsync(diagnostics, containerProperties, throughput, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.CreateContainerAsync(diagnostics, id, partitionKeyPath, throughput, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.CreateContainerIfNotExistsAsync(diagnostics, containerProperties, throughput, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.CreateContainerIfNotExistsAsync(diagnostics, id, partitionKeyPath, throughput, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.CreateContainerStreamAsync(diagnostics, containerProperties, throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<UserResponse> CreateUserAsync(string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateUserAsync),
                requestOptions,
                (diagnostics, trace) => base.CreateUserAsync(diagnostics, id, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.DeleteAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.DeleteStreamAsync(diagnostics, requestOptions, trace, cancellationToken));
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
                requestOptions));
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetContainerQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetContainerQueryStreamIterator(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetContainerQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
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
                requestOptions));
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetUserQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override Task<DatabaseResponse> ReadAsync(RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadStreamAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadThroughputAsync),
                null,
                (diagnostics, trace) => base.ReadThroughputAsync(diagnostics, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadThroughputAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadThroughputAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceThroughputAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceThroughputAsync(diagnostics, throughput, requestOptions, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceThroughputAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceThroughputAsync(diagnostics, throughputProperties, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.CreateContainerAsync(diagnostics, containerProperties, throughputProperties, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.CreateContainerIfNotExistsAsync(diagnostics, containerProperties, throughputProperties, requestOptions, trace, cancellationToken));
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
                (diagnostics, trace) => base.CreateContainerStreamAsync(diagnostics, containerProperties, throughputProperties, requestOptions, trace, cancellationToken));
        }

        public override Task<UserResponse> UpsertUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(UpsertUserAsync),
                requestOptions,
                (diagnostics, trace) => base.UpsertUserAsync(diagnostics, id, requestOptions, trace, cancellationToken));
        }

#if PREVIEW
        public
#else
        internal
#endif
            override ClientEncryptionKey GetClientEncryptionKey(string id)
        {
            return base.GetClientEncryptionKey(id);
        }

#if PREVIEW
        public
#else
        internal
#endif
            override FeedIterator<ClientEncryptionKeyProperties> GetClientEncryptionKeyQueryIterator(
                QueryDefinition queryDefinition,
                string continuationToken = null,
                QueryRequestOptions requestOptions = null)  
        {
            return base.GetClientEncryptionKeyQueryIterator(queryDefinition, continuationToken, requestOptions);
        }

#if PREVIEW
        public
#else
        internal
#endif
            override Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(
                ClientEncryptionKeyProperties clientEncryptionKeyProperties,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateClientEncryptionKeyAsync),
                requestOptions,
                (diagnostics, trace) => base.CreateClientEncryptionKeyAsync(clientEncryptionKeyProperties, requestOptions, cancellationToken));
        }
    }
}
