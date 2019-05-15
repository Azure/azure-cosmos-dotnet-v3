//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing stored procedures by id.
    /// 
    /// <see cref="CosmosStoredProcedures"/> for creating new stored procedures, and reading/querying all stored procedures;
    /// </summary>
    internal class CosmosStoredProcedureCore : CosmosStoredProcedure
    {
        private readonly CosmosClientContext clientContext;

        internal CosmosStoredProcedureCore(
            CosmosClientContext clientContext,
            CosmosContainerCore container,
            string storedProcedureId)
        {
            this.clientContext = clientContext;
            this.Id = storedProcedureId;
            this.LinkUri = clientContext.CreateLink(
                 parentLink: container.LinkUri.OriginalString,
                 uriPathSegment: Paths.StoredProceduresPathSegment,
                 id: storedProcedureId);
        }

        public override string Id { get; }

        internal Uri LinkUri { get; }

        public override Task<StoredProcedureResponse> ReadAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<StoredProcedureResponse> ReplaceAsync(
                    string body,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentNullException(nameof(body));
            }

            CosmosStoredProcedureSettings storedProcedureSettings = new CosmosStoredProcedureSettings()
            {
                Id = this.Id,
                Body = body,
            };

            return this.ProcessAsync(
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<StoredProcedureResponse> DeleteAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<ItemResponse<TOutput>> ExecuteAsync<TInput, TOutput>(
            object partitionKey,
            TInput input,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
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

            Task<CosmosResponseMessage> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.ExecuteJavaScript,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: parametersStream,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateItemResponse<TOutput>(response);
        }

        internal Task<StoredProcedureResponse> ProcessAsync(
            object partitionKey,
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<CosmosResponseMessage> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateStoredProcedureResponse(this, response);
        }
        internal CosmosContainerCore container { get; }
    }
}
