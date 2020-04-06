//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class ScriptsInlineCore : Scripts
    {
        private readonly ScriptsCore scripts;

        internal ScriptsInlineCore(ScriptsCore scripts)
        {
            if (scripts == null)
            {
                throw new ArgumentNullException(nameof(scripts));
            }

            this.scripts = scripts;
        }

        public override Task<StoredProcedureResponse> CreateStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.CreateStoredProcedureAsync(storedProcedureProperties, requestOptions, diagnostics, cancellationToken));
        }

        public override FeedIterator<T> GetStoredProcedureQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.scripts.GetStoredProcedureQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetStoredProcedureQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.scripts.GetStoredProcedureQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetStoredProcedureQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.scripts.GetStoredProcedureQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetStoredProcedureQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.scripts.GetStoredProcedureQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override Task<StoredProcedureResponse> ReadStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ReadStoredProcedureAsync(id, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<StoredProcedureResponse> ReplaceStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ReplaceStoredProcedureAsync(storedProcedureProperties, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<StoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.DeleteStoredProcedureAsync(id, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TOutput>(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ExecuteStoredProcedureAsync<TOutput>(storedProcedureId, partitionKey, parameters, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ExecuteStoredProcedureStreamAsync(storedProcedureId, partitionKey, parameters, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Stream streamPayload,
            Cosmos.PartitionKey partitionKey,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ExecuteStoredProcedureStreamAsync(storedProcedureId, streamPayload, partitionKey, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<TriggerResponse> CreateTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.CreateTriggerAsync(triggerProperties, requestOptions, diagnostics, cancellationToken));
        }

        public override FeedIterator<T> GetTriggerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.scripts.GetTriggerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetTriggerQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.scripts.GetTriggerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetTriggerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.scripts.GetTriggerQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetTriggerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.scripts.GetTriggerQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override Task<TriggerResponse> ReadTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ReadTriggerAsync(id, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<TriggerResponse> ReplaceTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ReplaceTriggerAsync(triggerProperties, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<TriggerResponse> DeleteTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.DeleteTriggerAsync(id, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.CreateUserDefinedFunctionAsync(userDefinedFunctionProperties, requestOptions, diagnostics, cancellationToken));
        }

        public override FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.scripts.GetUserDefinedFunctionQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetUserDefinedFunctionQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.scripts.GetUserDefinedFunctionQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(this.scripts.GetUserDefinedFunctionQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetUserDefinedFunctionQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(this.scripts.GetUserDefinedFunctionQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override Task<UserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ReadUserDefinedFunctionAsync(id, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<UserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.ReplaceUserDefinedFunctionAsync(userDefinedFunctionProperties, requestOptions, diagnostics, cancellationToken));
        }

        public override Task<UserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return CosmosClientContext.ProcessHelperAsync(
                requestOptions: requestOptions,
                (diagnostics) => this.scripts.DeleteUserDefinedFunctionAsync(id, requestOptions, diagnostics, cancellationToken));
        }
    }
}
