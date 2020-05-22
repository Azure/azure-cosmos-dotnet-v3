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

    // TODO: This class should inherit from ConflictsInternal to avoid the downcasting hacks.
    internal abstract class ConflictsCore : Conflicts
    {
        private readonly ContainerInternal container;

        public ConflictsCore(
            CosmosClientContext clientContext,
            ContainerInternal container)
        {
            if (clientContext == null)
            {
                throw new ArgumentNullException(nameof(clientContext));
            }

            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            this.container = container;
            this.ClientContext = clientContext;
        }

        internal CosmosClientContext ClientContext { get; }

        public Task<ResponseMessage> DeleteAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            ConflictProperties conflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (conflict == null)
            {
                throw new ArgumentNullException(nameof(conflict));
            }

            Uri conflictLink = this.ClientContext.CreateLink(
                 parentLink: this.container.LinkUri.OriginalString,
                 uriPathSegment: Paths.ConflictsPathSegment,
                 id: conflict.Id);

            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: conflictLink,
                resourceType: ResourceType.Conflict,
                operationType: OperationType.Delete,
                requestOptions: null,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public override FeedIterator GetConflictQueryStreamIterator(
           string queryText,
           string continuationToken,
           QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetConflictQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            string queryText,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetConflictQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetConflictQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               this.container.LinkUri,
               resourceType: ResourceType.Conflict,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);

        }

        public override FeedIterator<T> GetConflictQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions)
        {
            if (!(this.GetConflictQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal databaseStreamIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                databaseStreamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.Conflict));
        }

        public async Task<ItemResponse<T>> ReadCurrentAsync<T>(
            CosmosDiagnosticsContext diagnosticsContext,
            ConflictProperties cosmosConflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cosmosConflict == null)
            {
                throw new ArgumentNullException(nameof(cosmosConflict));
            }

            // SourceResourceId is RID based on Conflicts, so we need to obtain the db and container rid
            DatabaseInternal databaseCore = (DatabaseInternal)this.container.Database;
            string databaseResourceId = await databaseCore.GetRIDAsync(cancellationToken);
            string containerResourceId = await this.container.GetRIDAsync(cancellationToken);

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

            ResponseMessage response = await this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: itemLink,
                resourceType: ResourceType.Document,
                operationType: OperationType.Read,
                requestOptions: null,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override T ReadConflictContent<T>(ConflictProperties cosmosConflict)
        {
            if (cosmosConflict == null)
            {
                throw new ArgumentNullException(nameof(cosmosConflict));
            }

            // cosmosConflict.Content is string and converted to stream on demand for de-serialization
            if (!string.IsNullOrEmpty(cosmosConflict.Content))
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.Write(cosmosConflict.Content);
                        writer.Flush();
                        stream.Position = 0;
                        return this.ClientContext.SerializerCore.FromStream<T>(stream);
                    }
                }
            }

            return default(T);
        }
    }
}
