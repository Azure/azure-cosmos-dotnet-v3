//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class CosmosConflictsCore : CosmosConflicts
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClientContext clientContext;

        public CosmosConflictsCore(CosmosClientContext clientContext, CosmosContainerCore container)
        {
            this.container = container;
            this.clientContext = clientContext;
        }

        public override CosmosResultSetIterator<CosmosConflictSettings> GetConflictsIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosConflictSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ConflictsFeedRequestExecutor);
        }

        public override CosmosFeedResultSetIterator GetConflictsStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosFeedResultSetIteratorCore(
                maxItemCount,
                continuationToken,
                null,
                this.ConflictsFeedStreamRequestExecutor);
        }

        public override CosmosConflict this[string id] => new CosmosConflictCore(this.clientContext, this.container, id);

        private Task<CosmosResponseMessage> ConflictsFeedStreamRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.clientContext.ProcessResourceOperationAsync<CosmosResponseMessage>(
                resourceUri: this.container.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<CosmosQueryResponse<CosmosConflictSettings>> ConflictsFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.clientContext.ProcessResourceOperationAsync<CosmosQueryResponse<CosmosConflictSettings>>(
                resourceUri: this.container.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosConflictSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
