//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class ConflictsCore : Conflicts
    {
        private readonly ContainerCore container;
        private readonly CosmosClientContext clientContext;

        public ConflictsCore(
            CosmosClientContext clientContext,
            ContainerCore container)
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
            this.clientContext = clientContext;
        }

        public override Task<Response> DeleteAsync(
            ConflictProperties conflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken))
        {
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

        public override IAsyncEnumerable<Response> GetConflictQueryStreamIterator(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetConflictQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override AsyncPageable<T> GetConflictQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetConflictQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override async IAsyncEnumerable<Response> GetConflictQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator feedIterator = new FeedIteratorCore(
               this.clientContext,
               this.container.LinkUri,
               ResourceType.Conflict,
               queryDefinition,
               continuationToken,
               requestOptions);

            while (feedIterator.HasMoreResults)
            {
                yield return await feedIterator.ReadNextAsync(cancellationToken);
            }
        }

        public override AsyncPageable<T> GetConflictQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator feedIterator = this.GetConflictQueryIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            PageIteratorCore<T> pageIterator = new PageIteratorCore<T>(
                feedIterator: feedIterator,
                responseCreator: this.clientContext.ResponseFactory.CreateQueryFeedResponseWithPropertySerializer<T>);

            return PageResponseEnumerator.CreateAsyncPageable(continuation => pageIterator.GetPageAsync(continuation, cancellationToken));
        }

        public override async Task<ItemResponse<T>> ReadCurrentAsync<T>(
            ConflictProperties cosmosConflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cosmosConflict == null)
            {
                throw new ArgumentNullException(nameof(cosmosConflict));
            }

            // SourceResourceId is RID based on Conflicts, so we need to obtain the db and container rid
            DatabaseCore databaseCore = (DatabaseCore)this.container.Database;
            string databaseResourceId = await databaseCore.GetRIDAsync(cancellationToken);
            string containerResourceId = await this.container.GetRIDAsync(cancellationToken);

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

            Task<Response> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: itemLink,
                resourceType: ResourceType.Document,
                operationType: OperationType.Read,
                requestOptions: null,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return await this.clientContext.ResponseFactory.CreateItemResponseAsync<T>(response, cancellationToken);
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
                        return this.clientContext.CosmosSerializer.FromStream<T>(stream);
                    }
                }
            }

            return default(T);
        }

        private FeedIterator GetConflictQueryIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.clientContext,
               this.container.LinkUri,
               ResourceType.Conflict,
               queryDefinition,
               continuationToken,
               requestOptions);
        }
    }
}
