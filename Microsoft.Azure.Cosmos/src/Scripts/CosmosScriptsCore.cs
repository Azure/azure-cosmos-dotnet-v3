//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class CosmosScriptsCore : CosmosScripts
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClientContext clientContext;

        internal CosmosScriptsCore(
            CosmosContainerCore container, 
            CosmosClientContext clientContext)
        {
            this.container = container;
            this.clientContext = clientContext;
        }

        public override Task<StoredProcedureResponse> CreateStoredProcedureAsync(
                    CosmosStoredProcedureSettings storedProcedureSettings,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStoredProcedureOperationAsync(
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override FeedIterator<CosmosStoredProcedureSettings> GetStoredProceduresIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosStoredProcedureSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.StoredProcedureFeedRequestExecutorAsync);
        }

        public override Task<StoredProcedureResponse> ReadStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessStoredProcedureOperationAsync(
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<StoredProcedureResponse> ReplaceStoredProcedureAsync(
            CosmosStoredProcedureSettings storedProcedureSettings,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStoredProcedureOperationAsync(
                id: storedProcedureSettings.Id,
                operationType: OperationType.Replace,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<StoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessStoredProcedureOperationAsync(
                id: id, 
                operationType: OperationType.Delete, 
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TInput, TOutput>(
            object partitionKey,
            string id,
            TInput input,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            CosmosContainerCore.ValidatePartitionKey(partitionKey, requestOptions);

            Stream parametersStream;
            if (input != null && !input.GetType().IsArray)
            {
                parametersStream = this.clientContext.CosmosSerializer.ToStream<TInput[]>(new TInput[1] { input });
            }
            else
            {
                parametersStream = this.clientContext.CosmosSerializer.ToStream<TInput>(input);
            }

            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.ExecuteJavaScript,
                partitionKey: partitionKey,
                streamPayload: parametersStream,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateStoredProcedureExecuteResponseAsync<TOutput>(response);
        }

        public override Task<TriggerResponse> CreateTriggerAsync(
            CosmosTriggerSettings triggerSettings, 
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (triggerSettings == null)
            {
                throw new ArgumentNullException(nameof(triggerSettings));
            }

            if (string.IsNullOrEmpty(triggerSettings.Id))
            {
                throw new ArgumentNullException(nameof(triggerSettings.Id));
            }

            if (string.IsNullOrEmpty(triggerSettings.Body))
            {
                throw new ArgumentNullException(nameof(triggerSettings.Body));
            }

            return this.ProcessTriggerOperationAsync(
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: CosmosResource.ToStream(triggerSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override FeedIterator<CosmosTriggerSettings> GetTriggersIterator(
            int? maxItemCount = null, 
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosTriggerSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ContainerFeedRequestExecutorAsync);
        }

        public override Task<TriggerResponse> ReadTriggerAsync(
            string id,
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessTriggerOperationAsync(
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<TriggerResponse> ReplaceTriggerAsync(
            CosmosTriggerSettings triggerSettings, 
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (triggerSettings == null)
            {
                throw new ArgumentNullException(nameof(triggerSettings));
            }

            if (string.IsNullOrEmpty(triggerSettings.Id))
            {
                throw new ArgumentNullException(nameof(triggerSettings.Id));
            }

            if (string.IsNullOrEmpty(triggerSettings.Body))
            {
                throw new ArgumentNullException(nameof(triggerSettings.Body));
            }

            return this.ProcessTriggerOperationAsync(
                id: triggerSettings.Id,
                operationType: OperationType.Replace,
                streamPayload: CosmosResource.ToStream(triggerSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<TriggerResponse> DeleteTriggerAsync(
            string id,
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessTriggerOperationAsync(
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings, 
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userDefinedFunctionSettings == null)
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionSettings));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionSettings.Id))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionSettings.Id));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionSettings.Body))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionSettings.Body));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: CosmosResource.ToStream(userDefinedFunctionSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override FeedIterator<CosmosUserDefinedFunctionSettings> GetUserDefinedFunctionsIterator(
            int? maxItemCount = null, 
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosUserDefinedFunctionSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.UserDefinedFunctionFeedRequestExecutorAsync);
        }

        public override Task<UserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            string id, 
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<UserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings, 
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userDefinedFunctionSettings == null)
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionSettings));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionSettings.Id))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionSettings.Id));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionSettings.Body))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionSettings.Body));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                id: userDefinedFunctionSettings.Id,
                operationType: OperationType.Replace,
                streamPayload: CosmosResource.ToStream(userDefinedFunctionSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<UserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id, 
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<CosmosTriggerSettings>> ContainerFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.GetIteratorAsync<CosmosTriggerSettings>(
                maxItemCount: maxItemCount,
                continuationToken: continuationToken,
                state: state,
                resourceType: ResourceType.Trigger,
                options: options,
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<CosmosStoredProcedureSettings>> StoredProcedureFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.GetIteratorAsync<CosmosStoredProcedureSettings>(
                maxItemCount: maxItemCount,
                continuationToken: continuationToken,
                state: state,
                resourceType: ResourceType.StoredProcedure,
                options: options,
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<CosmosUserDefinedFunctionSettings>> UserDefinedFunctionFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.GetIteratorAsync<CosmosUserDefinedFunctionSettings>(
                maxItemCount: maxItemCount,
                continuationToken: continuationToken,
                state: state,
                resourceType: ResourceType.UserDefinedFunction,
                options: options,
                cancellationToken: cancellationToken);
        }

        private Task<StoredProcedureResponse> ProcessStoredProcedureOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            return this.ProcessStoredProcedureOperationAsync(
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<StoredProcedureResponse> ProcessStoredProcedureOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateStoredProcedureResponseAsync(response);
        }

        private Task<TriggerResponse> ProcessTriggerOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.ProcessTriggerOperationAsync(
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<TriggerResponse> ProcessTriggerOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.Trigger,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateTriggerResponseAsync(response);
        }

        private Task<CosmosResponseMessage> ProcessStreamOperationAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            object partitionKey,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.clientContext.ProcessResourceOperationAsStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        private Task<UserDefinedFunctionResponse> ProcessUserDefinedFunctionOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: id);

            return this.ProcessUserDefinedFunctionOperationAsync(
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<UserDefinedFunctionResponse> ProcessUserDefinedFunctionOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateUserDefinedFunctionResponseAsync(response);
        }

        private Task<FeedResponse<T>> GetIteratorAsync<T>(
            int? maxItemCount,
            string continuationToken,
            ResourceType resourceType,
            object state,
            RequestOptions options,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.clientContext.ProcessResourceOperationAsync<FeedResponse<T>>(
                resourceUri: this.container.LinkUri,
                resourceType: resourceType,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<T>(response),
                cancellationToken: cancellationToken);
        }
    }
}
