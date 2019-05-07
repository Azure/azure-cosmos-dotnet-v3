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

        internal CosmosScriptsCore(CosmosContainerCore container)
        {
            this.container = container;
        }

        public override Task<CosmosStoredProcedureResponse> CreateStoredProcedureAsync(
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

            CosmosStoredProcedureSettings storedProcedureSettings = new CosmosStoredProcedureSettings
            {
                Id = id,
                Body = body
            };

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
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

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
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

            CosmosStoredProcedureSettings storedProcedureSettings = new CosmosStoredProcedureSettings()
            {
                Id = id,
                Body = body,
            };

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
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

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateStoredProcedureResponse(response);
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
                parametersStream = this.container.ClientContext.JsonSerializer.ToStream<TInput[]>(new TInput[1] { input });
            }
            else
            {
                parametersStream = this.container.ClientContext.JsonSerializer.ToStream<TInput>(input);
            }

            Uri LinkUri = this.container.ClientContext.CreateLink(
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

            return this.container.ClientContext.ResponseFactory.CreateStoredProcedureExecuteResponse<TOutput>(response);
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

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(triggerSettings),
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateTriggerResponse(response);
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

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateTriggerResponse(response);
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


            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: triggerSettings.Id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(triggerSettings),
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateTriggerResponse(response);
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

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateTriggerResponse(response);
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

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(userDefinedFunctionSettings),
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
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

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
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


            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: userDefinedFunctionSettings.Id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(userDefinedFunctionSettings),
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
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

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.ProcessStreamOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: null,
                cancellation: cancellation);

            return this.container.ClientContext.ResponseFactory.CreateUserDefinedFunctionResponse(response);
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

        private Task<CosmosResponseMessage> ProcessStreamOperationAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            object partitionKey,
            Stream streamPayload,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellation)
        {
            return this.container.ClientContext.ProcessResourceOperationStreamAsync(
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

        private Task<CosmosFeedResponse<T>> GetIterator<T>(
            int? maxItemCount,
            string continuationToken,
            ResourceType resourceType,
            object state,
            CosmosRequestOptions options,
            CancellationToken cancellation)
        {
            Debug.Assert(state == null);

            return this.container.ClientContext.ProcessResourceOperationAsync<CosmosFeedResponse<T>>(
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
                responseCreator: response => this.container.ClientContext.ResponseFactory.CreateResultSetQueryResponse<T>(response),
                cancellationToken: cancellation);
        }
    }
}
