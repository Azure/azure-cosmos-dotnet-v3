//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

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
                operationName: nameof(CreateContainerAsync),
                containerName: containerProperties.Id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerAsync(containerProperties, throughput, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainer, (response) => new OpenTelemetryResponse<ContainerProperties>(responseMessage: response)));
        }

        public override Task<ContainerResponse> CreateContainerAsync(string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateContainerAsync),
                containerName: id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerAsync(id, partitionKeyPath, throughput, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainer, (response) => new OpenTelemetryResponse<ContainerProperties>(responseMessage: response)));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateContainerIfNotExistsAsync),
                containerName: containerProperties.Id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerIfNotExistsAsync(containerProperties, throughput, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainerIfNotExists, (response) => new OpenTelemetryResponse<ContainerProperties>(responseMessage: response)));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateContainerIfNotExistsAsync),
                containerName: id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerIfNotExistsAsync(id, partitionKeyPath, throughput, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainerIfNotExists, (response) => new OpenTelemetryResponse<ContainerProperties>(responseMessage: response)));
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateContainerStreamAsync),
                containerName: containerProperties.Id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerStreamAsync(containerProperties, throughput, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainer, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<UserResponse> CreateUserAsync(string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateUserAsync),
                containerName: id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateUserAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateUser, (response) => new OpenTelemetryResponse<UserProperties>(response)));
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
                operationName: nameof(DeleteAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteDatabase, (response) => new OpenTelemetryResponse<DatabaseProperties>(responseMessage: response)));
        }

        public override Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteStreamAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteStreamAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteDatabase, (response) => new OpenTelemetryResponse(response)));
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
                operationName: nameof(ReadAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadDatabase, (response) => new OpenTelemetryResponse<DatabaseProperties>(responseMessage: response)));
        }

        public override Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadStreamAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadStreamAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadDatabase, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadThroughputAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: null,
                task: (trace) => base.ReadThroughputAsync(trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadThroughputAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadThroughputAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadThroughput, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceThroughputAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceThroughputAsync(throughput, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceThroughput, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceThroughputAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceThroughputAsync(throughputProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceThroughput, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateContainerAsync),
                containerName: containerProperties.Id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerAsync(containerProperties, throughputProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainer, (response) => new OpenTelemetryResponse<ContainerProperties>(responseMessage: response)));
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateContainerIfNotExistsAsync),
                containerName: containerProperties.Id,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerIfNotExistsAsync(containerProperties, throughputProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainerIfNotExists, (response) => new OpenTelemetryResponse<ContainerProperties>(response)));
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateContainerStreamAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateContainerStreamAsync(containerProperties, throughputProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateContainer, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<UserResponse> UpsertUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(UpsertUserAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Upsert,
                requestOptions: requestOptions,
                task: (trace) => base.UpsertUserAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.UpsertUser, (response) => new OpenTelemetryResponse<UserProperties>(response)));
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
                operationName: nameof(CreateClientEncryptionKeyAsync),
                containerName: null,
                databaseName: this.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateClientEncryptionKeyAsync(trace, clientEncryptionKeyProperties, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateClientEncryptionKey, (response) => new OpenTelemetryResponse<ClientEncryptionKeyProperties>(response)));
        }
    }
}
