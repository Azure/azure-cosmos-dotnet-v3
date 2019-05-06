//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for creating new stored procedures, and reading/querying all stored procedures
    ///
    /// <see cref="CosmosStoredProcedure"/> for reading, replacing, or deleting an existing stored procedures.
    /// </summary>
    internal class CosmosStoredProceduresCore : CosmosStoredProcedures
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClientContext clientContext;

        internal CosmosStoredProceduresCore(
            CosmosClientContext clientContext,
            CosmosContainerCore container)
        {
            this.container = container;
            this.clientContext = clientContext;
        }

        public override Task<CosmosStoredProcedureResponse> CreateStoredProcedureAsync(
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

            CosmosStoredProcedureSettings storedProcedureSettings = new CosmosStoredProcedureSettings
            {
                Id = id,
                Body = body
            };

            Task<CosmosResponseMessage> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateStoredProcedureResponse(this[id], response);
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

        public override CosmosStoredProcedure this[string id] => new CosmosStoredProcedureCore(
            this.clientContext,
            this.container,
            id);

        private Task<CosmosFeedResponse<CosmosStoredProcedureSettings>> StoredProcedureFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.clientContext.ProcessResourceOperationAsync<CosmosFeedResponse<CosmosStoredProcedureSettings>>(
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
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosStoredProcedureSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
