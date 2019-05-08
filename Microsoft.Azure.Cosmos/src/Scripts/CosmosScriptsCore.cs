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

        public override Task<CosmosStoredProcedureResponse> CreateStoredProcedureAsync(
                    string id,
                    string body,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellation = default(CancellationToken))
        {
            CosmosStoredProcedureSettings storedProcedureSettings = new CosmosStoredProcedureSettings(id, body);

            return this.ProcessStoredProcedureOperationAsync(
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestOptions: requestOptions,
                cancellation: cancellation);
        }

        public override CosmosFeedIterator<CosmosStoredProcedureSettings> GetStoredProcedureIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosStoredProcedureSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.StoredProcedureFeedRequestExecutor);
        }

        public override Task<CosmosStoredProcedureResponse> ReadStoredProcedureAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override Task<CosmosStoredProcedureResponse> ReplaceStoredProcedureAsync(
            string id,
            string body,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentNullException(nameof(body));
            }

            CosmosStoredProcedureSettings storedProcedureSettings = new CosmosStoredProcedureSettings(id, body);

            return this.ProcessStoredProcedureOperationAsync(
                id: id,
                operationType: OperationType.Replace,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestOptions: requestOptions,
                cancellation: cancellation);
        }

        public override Task<CosmosStoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override Task<CosmosStoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TInput, TOutput>(
            object partitionKey,
            string id,
            TInput input,
            CosmosStoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            CosmosItemsCore.ValidatePartitionKey(partitionKey, requestOptions);

            Stream parametersStream;
            if (input != null && !input.GetType().IsArray)
            {
                parametersStream = this.clientContext.JsonSerializer.ToStream<TInput[]>(new TInput[1] { input });
            }
            else
            {
                parametersStream = this.clientContext.JsonSerializer.ToStream<TInput>(input);
            }

            Uri LinkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.ExecuteJavaScript,
                partitionKey: partitionKey,
                streamPayload: parametersStream,
                requestOptions: requestOptions,
                cancellation: cancellation);

            return this.clientContext.ResponseFactory.CreateStoredProcedureExecuteResponse<TOutput>(response);
        }

        public override Task<CosmosTriggerResponse> CreateTriggerAsync(
            CosmosTriggerSettings triggerSettings, 
            CosmosRequestOptions requestOptions = null, 
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override CosmosFeedIterator<CosmosTriggerSettings> GetTriggerIterator(
            int? maxItemCount = null, 
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosTriggerSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ContainerFeedRequestExecutor);
        }

        public override Task<CosmosTriggerResponse> ReadTriggerAsync(
            string id,
            CosmosRequestOptions requestOptions = null, 
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override Task<CosmosTriggerResponse> ReplaceTriggerAsync(
            CosmosTriggerSettings triggerSettings, 
            CosmosRequestOptions requestOptions = null, 
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override Task<CosmosTriggerResponse> DeleteTriggerAsync(
            string id,
            CosmosRequestOptions requestOptions = null, 
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override Task<CosmosUserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings, 
            CosmosRequestOptions requestOptions = null, 
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override CosmosFeedIterator<CosmosUserDefinedFunctionSettings> GetUserDefinedFunctionIterator(
            int? maxItemCount = null, 
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosUserDefinedFunctionSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.UserDefinedFunctionFeedRequestExecutor);
        }

        public override Task<CosmosUserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            string id, 
            CosmosRequestOptions requestOptions = null, 
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override Task<CosmosUserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings, 
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        public override Task<CosmosUserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id, 
            CosmosRequestOptions requestOptions = null, 
            CancellationToken cancellation = default(CancellationToken))
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
                cancellation: cancellation);
        }

        private Task<CosmosFeedResponse<CosmosTriggerSettings>> ContainerFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellation)
        {
            return this.GetIterator<CosmosTriggerSettings>(
                maxItemCount: maxItemCount,
                continuationToken: continuationToken,
                state: state,
                resourceType: ResourceType.Trigger,
                options: options,
                cancellation: cancellation);
        }

        private Task<CosmosFeedResponse<CosmosStoredProcedureSettings>> StoredProcedureFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellation)
        {
            return this.GetIterator<CosmosStoredProcedureSettings>(
                maxItemCount: maxItemCount,
                continuationToken: continuationToken,
                state: state,
                resourceType: ResourceType.StoredProcedure,
                options: options,
                cancellation: cancellation);
        }

        private Task<CosmosFeedResponse<CosmosUserDefinedFunctionSettings>> UserDefinedFunctionFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellation)
        {
            return this.GetIterator<CosmosUserDefinedFunctionSettings>(
                maxItemCount: maxItemCount,
                continuationToken: continuationToken,
                state: state,
                resourceType: ResourceType.UserDefinedFunction,
                options: options,
                cancellation: cancellation);
        }

        private Task<CosmosStoredProcedureResponse> ProcessStoredProcedureOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
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
                cancellation: cancellation);
        }

        private Task<CosmosStoredProcedureResponse> ProcessStoredProcedureOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
        {
            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellation: cancellation);

            return this.clientContext.ResponseFactory.CreateStoredProcedureResponse(response);
        }

        private Task<CosmosTriggerResponse> ProcessTriggerOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
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
                cancellation: cancellation);
        }

        private Task<CosmosTriggerResponse> ProcessTriggerOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
        {
            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.Trigger,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellation: cancellation);

            return this.clientContext.ResponseFactory.CreateTriggerResponse(response);
        }

        private Task<CosmosResponseMessage> ProcessStreamOperationAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            object partitionKey,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
        {
            return this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: null,
                cancellationToken: cancellation);
        }

        private Task<CosmosUserDefinedFunctionResponse> ProcessUserDefinedFunctionOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
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
                cancellation: cancellation);
        }

        private Task<CosmosUserDefinedFunctionResponse> ProcessUserDefinedFunctionOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
        {
            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellation: cancellation);

            return this.clientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
        }

        private Task<CosmosFeedResponse<T>> GetIterator<T>(
            int? maxItemCount,
            string continuationToken,
            ResourceType resourceType,
            object state,
            CosmosRequestOptions options,
            CancellationToken cancellation)
        {
            Debug.Assert(state == null);

            return this.clientContext.ProcessResourceOperationAsync<CosmosFeedResponse<T>>(
                resourceUri: this.container.LinkUri,
                resourceType: resourceType,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<T>(response),
                cancellationToken: cancellation);
        }
    }
}
