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

    internal class CosmosConflictsCore : CosmosConflicts
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClientContext clientContext;

        public CosmosConflictsCore(
            CosmosClientContext clientContext,
            CosmosContainerCore container)
        {
            this.container = container;
            this.clientContext = clientContext;
        }

        public override Task<CosmosResponseMessage> DeleteConflictAsync(
            object partitionKey,
            CosmosConflictSettings conflict, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (conflict == null)
            {
                throw new ArgumentNullException(nameof(conflict));
            }

            Uri conflictLink = this.clientContext.CreateLink(
                 parentLink: this.container.LinkUri.OriginalString,
                 uriPathSegment: Paths.ConflictsPathSegment,
                 id: conflict.Id);

            return this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: conflictLink,
                resourceType: ResourceType.Conflict,
                operationType: OperationType.Delete,
                requestOptions: null,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        public override FeedIterator<CosmosConflictSettings> GetConflictsIterator(
            int? maxItemCount = null, 
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosConflictSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ConflictsFeedRequestExecutor);
        }

        public override FeedIterator GetConflictsStreamIterator(
            int? maxItemCount = null, 
            string continuationToken = null)
        {
            return new FeedIteratorCore(
                maxItemCount,
                continuationToken,
                null,
                this.ConflictsFeedStreamRequestExecutor);
        }

        public override async Task<ItemResponse<T>> ReadCurrentAsync<T>(
            object partitionKey, 
            CosmosConflictSettings cosmosConflict, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (cosmosConflict == null)
            {
                throw new ArgumentNullException(nameof(cosmosConflict));
            }

            // SourceResourceId is RID based on Conflicts, so we need to obtain the db and container rid
            CosmosDatabaseCore databaseCore = (CosmosDatabaseCore)this.container.Database;
            string databaseResourceId = await databaseCore.GetRID(cancellationToken);
            string containerResourceId = await this.container.GetRID(cancellationToken);

            Uri dbLink = this.clientContext.CreateLink(
                parentLink: string.Empty,
                uriPathSegment: Paths.DatabasesPathSegment,
                id: databaseResourceId);

            Uri containerLink = this.clientContext.CreateLink(
                parentLink: dbLink.OriginalString,
                uriPathSegment: Paths.CollectionsPathSegment,
                id: containerResourceId);

            Uri itemLink = this.clientContext.CreateLink(
                parentLink: containerLink.OriginalString,
                uriPathSegment: Paths.DocumentsPathSegment,
                id: cosmosConflict.SourceResourceId);

            Task<CosmosResponseMessage> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: itemLink,
                resourceType: ResourceType.Document,
                operationType: OperationType.Read,
                requestOptions: null,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return await this.clientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override T ReadConflictContent<T>(CosmosConflictSettings cosmosConflict)
        {
            if (!string.IsNullOrEmpty(cosmosConflict.Content))
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.Write(cosmosConflict.Content);
                        writer.Flush();
                        stream.Position = 0;
                        return this.clientContext.JsonSerializer.FromStream<T>(stream);
                    }
                }
            }

            return default(T);
        }

        private Task<CosmosResponseMessage> ConflictsFeedStreamRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.clientContext.ProcessResourceOperationAsync<CosmosResponseMessage>(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Conflict,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<CosmosConflictSettings>> ConflictsFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.clientContext.ProcessResourceOperationAsync<FeedResponse<CosmosConflictSettings>>(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Conflict,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosConflictSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
