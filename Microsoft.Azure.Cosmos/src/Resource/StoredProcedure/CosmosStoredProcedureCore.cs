//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing stored procedures by id.
    /// 
    /// <see cref="CosmosStoredProcedures"/> for creating new stored procedures, and reading/querying all stored procedures;
    /// </summary>
    internal class CosmosStoredProcedureCore : CosmosStoredProcedure
    {
        internal CosmosStoredProcedureCore(
            CosmosContainerCore container,
            string storedProcedureId)
        {
            this.Id = storedProcedureId;
            this.container = container;
            base.Initialize(
                client: container.Client,
                parentLink: container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment);
        }

        public override string Id { get; }

        public override Task<CosmosStoredProcedureResponse> ReadAsync(
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessAsync(
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosStoredProcedureResponse> ReplaceAsync(
                    string body,
                    CosmosRequestOptions requestOptions = null,
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

            return ProcessAsync(
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosStoredProcedureResponse> DeleteAsync(
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessAsync(
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosItemResponse<TOutput>> ExecuteAsync<TInput, TOutput>(
            object partitionKey,
            TInput input,
            CosmosStoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosItemsCore.ValidatePartitionKey(partitionKey, requestOptions);

            Stream parametersStream;
            if (input != null && !input.GetType().IsArray)
            {
                parametersStream = this.Client.CosmosJsonSerializer.ToStream<TInput[]>(new TInput[1] { input });
            }
            else
            {
                parametersStream = this.Client.CosmosJsonSerializer.ToStream<TInput>(input);
            }

            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.Client,
                this.LinkUri,
                ResourceType.StoredProcedure,
                OperationType.ExecuteJavaScript,
                requestOptions,
                this.container,
                partitionKey,
                parametersStream,
                null,
                cancellationToken);

            return this.Client.ResponseFactory.CreateItemResponse<TOutput>(response);
        }

        internal Task<CosmosStoredProcedureResponse> ProcessAsync(
            object partitionKey,
            Stream streamPayload,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.Client,
                this.LinkUri,
                ResourceType.StoredProcedure,
                operationType,
                requestOptions,
                this.container,
                partitionKey,
                streamPayload,
                null,
                cancellationToken);

            return this.Client.ResponseFactory.CreateStoredProcedureResponse(this, response);
        }
        internal CosmosContainerCore container { get; }
    }
}
