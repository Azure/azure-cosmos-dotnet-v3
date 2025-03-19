//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.ClientModel.Primitives;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using RequestOptions = Microsoft.Azure.Cosmos.RequestOptions;

    internal abstract class ScriptsCore : Scripts
    {
        protected readonly ContainerInternal container;

        internal ScriptsCore(
            ContainerInternal container,
            CosmosClientContext clientContext)
        {
            this.container = container;
            this.ClientContext = clientContext;
        }

        protected CosmosClientContext ClientContext { get; }

        public Task<ResponseMessage> CreateStoredProcedureStreamAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessStreamOperationAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: this.ClientContext.SerializerCore.ToStream(storedProcedureProperties),
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<StoredProcedureResponse> CreateStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.CreateStoredProcedureStreamAsync(
                storedProcedureProperties: storedProcedureProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
        }

        public override FeedIterator GetStoredProcedureQueryStreamIterator(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetStoredProcedureQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetStoredProcedureQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetStoredProcedureQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetStoredProcedureQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               resourceLink: this.container.LinkUri,
               resourceType: ResourceType.StoredProcedure,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions,
               container: this.container);
        }

        public override FeedIterator<T> GetStoredProcedureQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetStoredProcedureQueryStreamIterator(
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
                    resourceType: ResourceType.StoredProcedure));
        }

        public Task<ResponseMessage> ReadStoredProcedureStreamAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<StoredProcedureResponse> ReadStoredProcedureAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReadStoredProcedureStreamAsync(
                id: id,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
        }

        public Task<ResponseMessage> ReplaceStoredProcedureStreamAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            string linkUri = this.ClientContext.CreateLink(
                   parentLink: this.container.LinkUri,
                   uriPathSegment: Paths.StoredProceduresPathSegment,
                   id: storedProcedureProperties.Id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: this.ClientContext.SerializerCore.ToStream(storedProcedureProperties),
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<StoredProcedureResponse> ReplaceStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReplaceStoredProcedureStreamAsync(
                storedProcedureProperties: storedProcedureProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
        }

        public Task<ResponseMessage> DeleteStoredProcedureStreamAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<StoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.DeleteStoredProcedureStreamAsync(
                id: id,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
        }

        public async Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TOutput>(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ExecuteStoredProcedureStreamAsync(
                storedProcedureId: storedProcedureId,
                partitionKey: partitionKey,
                parameters: parameters,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureExecuteResponse<TOutput>(response);
        }

        public Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            Stream streamPayload = null;
            if (parameters != null)
            {
                streamPayload = this.ClientContext.SerializerCore.ToStream<dynamic[]>(parameters);
            }

            return this.ExecuteStoredProcedureStreamAsync(
                storedProcedureId: storedProcedureId,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Stream streamPayload,
            Cosmos.PartitionKey partitionKey,
            StoredProcedureRequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(storedProcedureId))
            {
                throw new ArgumentNullException(nameof(storedProcedureId));
            }

            ContainerInternal.ValidatePartitionKey(partitionKey, requestOptions);

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: storedProcedureId);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.ExecuteJavaScript,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> CreateTriggerStreamAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions,
            ITrace trace,
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

            return this.ProcessStreamOperationAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: this.ClientContext.SerializerCore.ToStream(triggerProperties),
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<TriggerResponse> CreateTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.CreateTriggerStreamAsync(
                triggerProperties: triggerProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateTriggerResponse(response);
        }

        public override FeedIterator GetTriggerQueryStreamIterator(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetTriggerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetTriggerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetTriggerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetTriggerQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               resourceLink: this.container.LinkUri,
               resourceType: ResourceType.Trigger,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions,
               container: this.container);
        }

        public override FeedIterator<T> GetTriggerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetTriggerQueryStreamIterator(
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
                    resourceType: ResourceType.Trigger));
        }

        public Task<ResponseMessage> ReadTriggerStreamAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<TriggerResponse> ReadTriggerAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReadTriggerStreamAsync(
                id: id,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateTriggerResponse(response);
        }

        public Task<ResponseMessage> ReplaceTriggerStreamAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions,
            ITrace trace,
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

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.TriggersPathSegment,
                id: triggerProperties.Id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: this.ClientContext.SerializerCore.ToStream(triggerProperties),
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<TriggerResponse> ReplaceTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReplaceTriggerStreamAsync(
                triggerProperties: triggerProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateTriggerResponse(response);
        }

        public Task<ResponseMessage> DeleteTriggerStreamAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<TriggerResponse> DeleteTriggerAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.DeleteTriggerStreamAsync(
                id: id,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateTriggerResponse(response);
        }

        public Task<ResponseMessage> CreateUserDefinedFunctionStreamAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions,
            ITrace trace,
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

            return this.ProcessStreamOperationAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: this.ClientContext.SerializerCore.ToStream(userDefinedFunctionProperties),
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.CreateUserDefinedFunctionStreamAsync(
                userDefinedFunctionProperties: userDefinedFunctionProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
        }

        public override FeedIterator GetUserDefinedFunctionQueryStreamIterator(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserDefinedFunctionQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserDefinedFunctionQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetUserDefinedFunctionQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               resourceLink: this.container.LinkUri,
               resourceType: ResourceType.UserDefinedFunction,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions,
               container: this.container);
        }

        public override FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetUserDefinedFunctionQueryStreamIterator(
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
                    resourceType: ResourceType.UserDefinedFunction));
        }

        public Task<ResponseMessage> ReadUserDefinedFunctionStreamAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<UserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReadUserDefinedFunctionStreamAsync(
                id: id,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
        }

        public Task<ResponseMessage> ReplaceUserDefinedFunctionStreamAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions,
            ITrace trace,
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

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: userDefinedFunctionProperties.Id);

            return this.ProcessStreamOperationAsync(
               resourceUri: linkUri,
               resourceType: ResourceType.UserDefinedFunction,
               operationType: OperationType.Replace,
               requestOptions: requestOptions,
               partitionKey: null,
               streamPayload: this.ClientContext.SerializerCore.ToStream(userDefinedFunctionProperties),
               trace: trace,
               cancellationToken: cancellationToken);
        }

        public async Task<UserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReplaceUserDefinedFunctionStreamAsync(
                userDefinedFunctionProperties: userDefinedFunctionProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
        }

        public Task<ResponseMessage> DeleteUserDefinedFunctionStreamAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: id);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<UserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.DeleteUserDefinedFunctionStreamAsync(
                id: id,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
        }

        private async Task<StoredProcedureResponse> ProcessStoredProcedureOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            string linkUri = this.ClientContext.CreateLink(
                parentLink: this.container.LinkUri,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            ResponseMessage response = await this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
        }

        private Task<ResponseMessage> ProcessStreamOperationAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            Cosmos.PartitionKey? partitionKey,
            Stream streamPayload,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                feedRange: partitionKey.HasValue ? new FeedRangePartitionKey(partitionKey.Value) : null,
                streamPayload: streamPayload,
                requestEnricher: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }
    }
}
