//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
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

            Task<CosmosResponseMessage> response = this.container.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(storedProcedureSettings),
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.container.ClientContext.ResponseFactory.CreateStoredProcedureResponse(this[id], response);
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

        private Task<CosmosFeedResponse<CosmosStoredProcedureSettings>> StoredProcedureFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.container.ClientContext.ProcessResourceOperationAsync<CosmosFeedResponse<CosmosStoredProcedureSettings>>(
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
                responseCreator: response => this.container.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosStoredProcedureSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
