//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class ScriptsInlineCore : ScriptsCore
    {
        internal ScriptsInlineCore(
            ContainerInternal container,
            CosmosClientContext clientContext)
            : base(
                  container,
                  clientContext)
        {
        }

        public override Task<StoredProcedureResponse> CreateStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateStoredProcedureAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateStoredProcedureAsync(storedProcedureProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateStoredProcedure, (response) => new OpenTelemetryResponse<StoredProcedureProperties>(response)));
        }

        public override FeedIterator<T> GetStoredProcedureQueryIterator<T>(
           QueryDefinition queryDefinition,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetStoredProcedureQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetStoredProcedureQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetStoredProcedureQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetStoredProcedureQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetStoredProcedureQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetStoredProcedureQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetStoredProcedureQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override Task<StoredProcedureResponse> ReadStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadStoredProcedureAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadStoredProcedureAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadStoredProcedure, (response) => new OpenTelemetryResponse<StoredProcedureProperties>(response)));
        }

        public override Task<StoredProcedureResponse> ReplaceStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceStoredProcedureAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions,
                task: (trace) => base.ReplaceStoredProcedureAsync(storedProcedureProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceStoredProcedure, (response) => new OpenTelemetryResponse<StoredProcedureProperties>(response)));
        }

        public override Task<StoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteStoredProcedureAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteStoredProcedureAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteStoreProcedure, (response) => new OpenTelemetryResponse<StoredProcedureProperties>(response)));
        }

        public override Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TOutput>(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ExecuteStoredProcedureAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Execute,
                requestOptions: requestOptions,
                task: (trace) => base.ExecuteStoredProcedureAsync<TOutput>(storedProcedureId, partitionKey, parameters, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ExecuteStoredProcedure, (response) => new OpenTelemetryResponse<TOutput>(response)));
        }

        public override Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ExecuteStoredProcedureStreamAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Execute,
                requestOptions: requestOptions,
                task: (trace) => base.ExecuteStoredProcedureStreamAsync(storedProcedureId, partitionKey, parameters, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ExecuteStoredProcedure, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Stream streamPayload,
            Cosmos.PartitionKey partitionKey,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ExecuteStoredProcedureStreamAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Execute,
                requestOptions: requestOptions,
                task: (trace) => base.ExecuteStoredProcedureStreamAsync(storedProcedureId, streamPayload, partitionKey, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ExecuteStoredProcedure, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<TriggerResponse> CreateTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateTriggerAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateTriggerAsync(triggerProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateTrigger, (response) => new OpenTelemetryResponse<TriggerProperties>(response)));
        }

        public override FeedIterator<T> GetTriggerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetTriggerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetTriggerQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetTriggerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetTriggerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetTriggerQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetTriggerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetTriggerQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override Task<TriggerResponse> ReadTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadTriggerAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadTriggerAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadTrigger, (response) => new OpenTelemetryResponse<TriggerProperties>(response)));
        }

        public override Task<TriggerResponse> ReplaceTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceTriggerAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceTriggerAsync(triggerProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceTrigger, (response) => new OpenTelemetryResponse<TriggerProperties>(response)));
        }

        public override Task<TriggerResponse> DeleteTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteTriggerAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteTriggerAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteTrigger, (response) => new OpenTelemetryResponse<TriggerProperties>(response)));
        }

        public override Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateUserDefinedFunctionAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateUserDefinedFunctionAsync(userDefinedFunctionProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateUserDefinedFunction, (response) => new OpenTelemetryResponse<UserDefinedFunctionProperties>(response)));
        }

        public override FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetUserDefinedFunctionQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetUserDefinedFunctionQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetUserDefinedFunctionQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetUserDefinedFunctionQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetUserDefinedFunctionQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetUserDefinedFunctionQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override Task<UserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadUserDefinedFunctionAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadUserDefinedFunctionAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadUserDefinedFunction, (response) => new OpenTelemetryResponse<UserDefinedFunctionProperties>(response)));
        }

        public override Task<UserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceUserDefinedFunctionAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceUserDefinedFunctionAsync(userDefinedFunctionProperties, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceUserDefinedFunctions, (response) => new OpenTelemetryResponse<UserDefinedFunctionProperties>(response)));
        }

        public override Task<UserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteUserDefinedFunctionAsync),
                containerName: this.container.Id,
                databaseName: this.container.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteUserDefinedFunctionAsync(id, requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteUserDefinedFunctions, (response) => new OpenTelemetryResponse<UserDefinedFunctionProperties>(response)));
        }
    }
}
