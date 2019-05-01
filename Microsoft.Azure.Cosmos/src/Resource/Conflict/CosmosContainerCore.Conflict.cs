//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    internal partial class CosmosContainerCore
    {
        public override Task<CosmosResponseMessage> DeleteConflictAsync(
            object partitionKey,
            string id,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Uri conflictLink = this.ClientContext.CreateLink(
                 parentLink: this.LinkUri.OriginalString,
                 uriPathSegment: Paths.ConflictsPathSegment,
                 id: id);

            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: conflictLink,
                resourceType: ResourceType.Conflict,
                operationType: OperationType.Delete,
                requestOptions: null,
                cosmosContainerCore: this,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        public override async Task<CosmosResponseMessage> ReadConflictSourceItemAsync(
            object partitionKey,
            CosmosConflict cosmosConflict,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // SourceResourceId is RID based on Conflicts, so we need to obtain the db and container rid
            CosmosDatabaseCore databaseCore = (CosmosDatabaseCore) this.Database;
            string databaseResourceId = await databaseCore.GetRID(cancellationToken);
            string containerResourceId = await this.GetRID(cancellationToken);

            Uri dbLink = this.ClientContext.CreateLink(
                parentLink: string.Empty,
                uriPathSegment: Paths.DatabasesPathSegment,
                id: databaseResourceId);

            Uri containerLink = this.ClientContext.CreateLink(
                parentLink: dbLink.OriginalString,
                uriPathSegment: Paths.CollectionsPathSegment,
                id: containerResourceId);
            
            Uri itemLink = this.ClientContext.CreateLink(
                parentLink: containerLink.OriginalString,
                uriPathSegment: Paths.DocumentsPathSegment,
                id: cosmosConflict.SourceResourceId);

            return await this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: itemLink,
                resourceType: ResourceType.Document,
                operationType: OperationType.Read,
                requestOptions: null,
                cosmosContainerCore: this,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        public override CosmosResultSetIterator<CosmosConflict> GetConflictsIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosConflict>(
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

        private Task<CosmosResponseMessage> ConflictsFeedStreamRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync<CosmosResponseMessage>(
                resourceUri: this.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                cosmosContainerCore: this,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<CosmosQueryResponse<CosmosConflict>> ConflictsFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync<CosmosQueryResponse<CosmosConflict>>(
                resourceUri: this.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: this,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosConflict>(response),
                cancellationToken: cancellationToken);
        }
    }
}
