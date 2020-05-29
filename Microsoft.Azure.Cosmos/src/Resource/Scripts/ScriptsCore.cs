//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal abstract class ScriptsCore : Scripts
    {
        private readonly ContainerInternal container;

        internal ScriptsCore(
            ContainerInternal container,
            CosmosClientContext clientContext)
        {
            this.container = container;
            this.ClientContext = clientContext;
        }

        internal CosmosClientContext ClientContext { get; }

        public Task<StoredProcedureResponse> CreateStoredProcedureAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessStoredProcedureOperationAsync(
                diagnosticsContext: diagnosticsContext,
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: this.ClientContext.SerializerCore.ToStream(storedProcedureProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public FeedIteratorBase GetStoredProcedureQueryStreamIteratorHelper(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetStoredProcedureQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIteratorBase<T> GetStoredProcedureQueryIteratorHelper<T>(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetStoredProcedureQueryIteratorHelper<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIteratorBase GetStoredProcedureQueryStreamIteratorHelper(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               this.container.LinkUri,
               resourceType: ResourceType.StoredProcedure,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        public FeedIteratorBase<T> GetStoredProcedureQueryIteratorHelper<T>(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            FeedIteratorBase streamIterator = this.GetStoredProcedureQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions);

            return new FeedIteratorCore<T>(
                streamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.StoredProcedure));
        }

        public Task<StoredProcedureResponse> ReadStoredProcedureAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessStoredProcedureOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<StoredProcedureResponse> ReplaceStoredProcedureAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessStoredProcedureOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: storedProcedureProperties.Id,
                operationType: OperationType.Replace,
                streamPayload: this.ClientContext.SerializerCore.ToStream(storedProcedureProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<StoredProcedureResponse> DeleteStoredProcedureAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessStoredProcedureOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public async Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TOutput>(
            CosmosDiagnosticsContext diagnosticsContext,
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ExecuteStoredProcedureStreamAsync(
                diagnosticsContext: diagnosticsContext,
                storedProcedureId: storedProcedureId,
                partitionKey: partitionKey,
                parameters: parameters,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureExecuteResponse<TOutput>(response);
        }

        public Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Stream streamPayload = null;
            if (parameters != null)
            {
                streamPayload = this.ClientContext.SerializerCore.ToStream<dynamic[]>(parameters);
            }

            return this.ExecuteStoredProcedureStreamAsync(
                diagnosticsContext: diagnosticsContext,
                storedProcedureId: storedProcedureId,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string storedProcedureId,
            Stream streamPayload,
            Cosmos.PartitionKey partitionKey,
            StoredProcedureRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(storedProcedureId))
            {
                throw new ArgumentNullException(nameof(storedProcedureId));
            }

            ContainerInternal.ValidatePartitionKey(partitionKey, requestOptions);

            Uri linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: storedProcedureId);

            return this.ProcessStreamOperationAsync(
                diagnosticsContext: diagnosticsContext,
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.ExecuteJavaScript,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<TriggerResponse> CreateTriggerAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            TriggerProperties triggerProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (triggerProperties == null)
            {
                throw new ArgumentNullException(nameof(triggerProperties));
            }

            if (string.IsNullOrEmpty(triggerProperties.Id))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Id));
            }

            if (string.IsNullOrEmpty(triggerProperties.Body))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Body));
            }

            return this.ProcessTriggerOperationAsync(
                diagnosticsContext: diagnosticsContext,
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: this.ClientContext.SerializerCore.ToStream(triggerProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public FeedIteratorBase GetTriggerQueryStreamIteratorHelper(
           string queryText,
           string continuationToken,
           QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetTriggerQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIteratorBase<T> GetTriggerQueryIteratorHelper<T>(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetTriggerQueryIteratorHelper<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIteratorBase GetTriggerQueryStreamIteratorHelper(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               this.container.LinkUri,
               resourceType: ResourceType.Trigger,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        public FeedIteratorBase<T> GetTriggerQueryIteratorHelper<T>(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            FeedIteratorBase streamIterator = this.GetTriggerQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions);

            return new FeedIteratorCore<T>(
                streamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.Trigger));
        }

        public Task<TriggerResponse> ReadTriggerAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessTriggerOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<TriggerResponse> ReplaceTriggerAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            TriggerProperties triggerProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (triggerProperties == null)
            {
                throw new ArgumentNullException(nameof(triggerProperties));
            }

            if (string.IsNullOrEmpty(triggerProperties.Id))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Id));
            }

            if (string.IsNullOrEmpty(triggerProperties.Body))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Body));
            }

            return this.ProcessTriggerOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: triggerProperties.Id,
                operationType: OperationType.Replace,
                streamPayload: this.ClientContext.SerializerCore.ToStream(triggerProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<TriggerResponse> DeleteTriggerAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessTriggerOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (userDefinedFunctionProperties == null)
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Id))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Id));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Body))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Body));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                diagnosticsContext: diagnosticsContext,
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: this.ClientContext.SerializerCore.ToStream(userDefinedFunctionProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public FeedIteratorBase GetUserDefinedFunctionQueryStreamIteratorHelper(
           string queryText,
           string continuationToken,
           QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserDefinedFunctionQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIteratorBase<T> GetUserDefinedFunctionQueryIteratorHelper<T>(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserDefinedFunctionQueryIteratorHelper<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIteratorBase GetUserDefinedFunctionQueryStreamIteratorHelper(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               this.container.LinkUri,
               resourceType: ResourceType.UserDefinedFunction,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        public FeedIteratorBase<T> GetUserDefinedFunctionQueryIteratorHelper<T>(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            FeedIteratorBase streamIterator = this.GetUserDefinedFunctionQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions);

            return new FeedIteratorCore<T>(
                streamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.UserDefinedFunction));
        }

        public Task<UserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<UserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (userDefinedFunctionProperties == null)
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Id))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Id));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Body))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Body));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: userDefinedFunctionProperties.Id,
                operationType: OperationType.Replace,
                streamPayload: this.ClientContext.SerializerCore.ToStream(userDefinedFunctionProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<UserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                diagnosticsContext: diagnosticsContext,
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<StoredProcedureResponse> ProcessStoredProcedureOperationAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            return this.ProcessStoredProcedureOperationAsync(
                diagnosticsContext: diagnosticsContext,
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private async Task<StoredProcedureResponse> ProcessStoredProcedureOperationAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ProcessStreamOperationAsync(
                diagnosticsContext: diagnosticsContext,
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
        }

        private Task<TriggerResponse> ProcessTriggerOperationAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.ProcessTriggerOperationAsync(
                diagnosticsContext: diagnosticsContext,
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private async Task<TriggerResponse> ProcessTriggerOperationAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ProcessStreamOperationAsync(
                diagnosticsContext: diagnosticsContext,
                resourceUri: linkUri,
                resourceType: ResourceType.Trigger,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateTriggerResponse(response);
        }

        private Task<ResponseMessage> ProcessStreamOperationAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            Cosmos.PartitionKey? partitionKey,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: null,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        private Task<UserDefinedFunctionResponse> ProcessUserDefinedFunctionOperationAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: id);

            return this.ProcessUserDefinedFunctionOperationAsync(
                diagnosticsContext: diagnosticsContext,
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private async Task<UserDefinedFunctionResponse> ProcessUserDefinedFunctionOperationAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ProcessStreamOperationAsync(
                diagnosticsContext: diagnosticsContext,
                resourceUri: linkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
        }
    }
}
