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

        private Task<CosmosFeedResponse<CosmosTriggerSettings>> ContainerFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.container.ClientContext.ProcessResourceOperationAsync<CosmosFeedResponse<CosmosTriggerSettings>>(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Trigger,
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
                responseCreator: response => this.container.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosTriggerSettings>(response),
                cancellationToken: cancellationToken);
        }

        private Task<CosmosFeedResponse<CosmosStoredProcedureSettings>> StoredProcedureFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellation)
        {
            Debug.Assert(state == null);

            return this.container.ClientContext.ProcessResourceOperationAsync<CosmosFeedResponse<CosmosStoredProcedureSettings>>(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.StoredProcedure,
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
                responseCreator: response => this.container.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosStoredProcedureSettings>(response),
                cancellationToken: cancellation);
        }


    }
}
