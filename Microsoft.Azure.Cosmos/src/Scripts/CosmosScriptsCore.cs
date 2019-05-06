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

    internal sealed class CosmosScriptsCore : CosmosScripts
    {
        private readonly CosmosContainerCore container;

        internal CosmosScriptsCore(CosmosContainerCore container)
        {
            this.container = container;
        }

        public override Task<CosmosStoredProcedure> CreateStoredProcedureAsync(
                    string id,
                    string body,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentNullException(nameof(body));
            }

            CosmosStoredProcedure storedProcedureSettings = new CosmosStoredProcedure
            {
                Id = id,
                Body = body
            };

            return this.container.ClientContext.ProcessResourceOperationAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestEnricher: null,
                responseCreator: response => this.container.ClientContext.ResponseFactory.ToObjectInternal<CosmosStoredProcedure>(response),
                cancellationToken: cancellationToken);
        }

        public override CosmosFeedIterator<CosmosStoredProcedure> GetStoredProcedureIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosStoredProcedure>(
                maxItemCount,
                continuationToken,
                null,
                this.StoredProcedureFeedRequestExecutor);
        }

        public override Task<CosmosStoredProcedure> ReadStoredProcedureAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.container.ClientContext.ProcessResourceOperationAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: null,
                responseCreator: response => this.container.ClientContext.ResponseFactory.ToObjectInternal<CosmosStoredProcedure>(response),
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosResponseMessage> ReplaceStoredProcedureAsync(
            string id,
            string body,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentNullException(nameof(body));
            }

            CosmosStoredProcedure storedProcedureSettings = new CosmosStoredProcedure()
            {
                Id = id,
                Body = body,
            };

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.container.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosResponseMessage> DeleteStoredProcedureAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            Uri LinkUri = this.container.ClientContext.CreateLink(
                parentLink: container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.container.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosItemResponse<TOutput>> ExecuteStoredProcedureAsync<TInput, TOutput>(
            object partitionKey,
            string id,
            TInput input,
            CosmosStoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
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
                parentLink: container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            Task<CosmosResponseMessage> response = this.container.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.ExecuteJavaScript,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: parametersStream,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.container.ClientContext.ResponseFactory.CreateItemResponse<TOutput>(response);
        }

        private Task<CosmosFeedResponse<CosmosStoredProcedure>> StoredProcedureFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.container.ClientContext.ProcessResourceOperationAsync<CosmosFeedResponse<CosmosStoredProcedure>>(
                resourceUri: resourceUri,
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
                responseCreator: response => this.container.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosStoredProcedure>(response),
                cancellationToken: cancellationToken);
        }
    }
}
