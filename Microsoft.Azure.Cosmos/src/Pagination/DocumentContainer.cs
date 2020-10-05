// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Composes a <see cref="IMonadicDocumentContainer"/> and creates an <see cref="IDocumentContainer"/>.
    /// </summary>
    internal sealed class DocumentContainer : IDocumentContainer
    {
        private readonly IMonadicDocumentContainer monadicDocumentContainer;

        public DocumentContainer(IMonadicDocumentContainer monadicDocumentContainer)
        {
            this.monadicDocumentContainer = monadicDocumentContainer ?? throw new ArgumentNullException(nameof(monadicDocumentContainer));
        }

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicGetChildRangeAsync(
                partitionKeyRange,
                cancellationToken);

        public Task<List<PartitionKeyRange>> GetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicGetChildRangeAsync(
                    partitionKeyRange,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicGetFeedRangesAsync(
                cancellationToken);

        public Task<List<PartitionKeyRange>> GetFeedRangesAsync(
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicGetFeedRangesAsync(
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicCreateItemAsync(
                payload,
                cancellationToken);

        public Task<Record> CreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken) => TryCatch<List<PartitionKeyRange>>.UnsafeGetResultAsync(
                this.MonadicCreateItemAsync(
                    payload,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            string identifer,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicReadItemAsync(
                partitionKey,
                identifer,
                cancellationToken);

        public Task<Record> ReadItemAsync(
            CosmosElement partitionKey,
            string identifier,
            CancellationToken cancellationToken) => TryCatch<Record>.UnsafeGetResultAsync(
                this.MonadicReadItemAsync(
                    partitionKey,
                    identifier,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<DocumentContainerPage>> MonadicReadFeedAsync(
            int partitionKeyRangeId,
            ResourceId resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicReadFeedAsync(
                partitionKeyRangeId,
                resourceIdentifer,
                pageSize,
                cancellationToken);

        public Task<DocumentContainerPage> ReadFeedAsync(
            int partitionKeyRangeId,
            ResourceId resourceIdentifier,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<DocumentContainerPage>.UnsafeGetResultAsync(
                this.MonadicReadFeedAsync(
                    partitionKeyRangeId,
                    resourceIdentifier,
                    pageSize,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            Cosmos.PartitionKey partitionKey,
            int pageSize,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicQueryAsync(
                sqlQuerySpec,
                continuationToken,
                partitionKey,
                pageSize,
                cancellationToken);

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicQueryAsync(
                sqlQuerySpec,
                continuationToken,
                partitionKeyRangeId,
                pageSize,
                cancellationToken);

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicQueryAsync(
                sqlQuerySpec,
                continuationToken,
                feedRange,
                pageSize,
                cancellationToken);

        public Task<QueryPage> QueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            Cosmos.PartitionKey partitionKey,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<QueryPage>.UnsafeGetResultAsync(
                this.MonadicQueryAsync(
                    sqlQuerySpec,
                    continuationToken,
                    partitionKey,
                    pageSize,
                    cancellationToken),
                cancellationToken);

        public Task<QueryPage> QueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<QueryPage>.UnsafeGetResultAsync(
                this.MonadicQueryAsync(
                    sqlQuerySpec,
                    continuationToken,
                    partitionKeyRangeId,
                    pageSize,
                    cancellationToken),
                cancellationToken);

        public Task<QueryPage> QueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<QueryPage>.UnsafeGetResultAsync(
                this.MonadicQueryAsync(
                    sqlQuerySpec,
                    continuationToken,
                    feedRange,
                    pageSize,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch> MonadicSplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicSplitAsync(
                partitionKeyRangeId,
                cancellationToken);

        public Task SplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken) => TryCatch.UnsafeWaitAsync(
                this.MonadicSplitAsync(
                    partitionKeyRangeId,
                    cancellationToken),
                cancellationToken);

        public Task<ChangeFeedPage> ChangeFeedAsync(
            ChangeFeedState state,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<ChangeFeedPage>.UnsafeGetResultAsync(
                this.MonadicChangeFeedAsync(
                    state,
                    feedRange,
                    pageSize,
                    cancellationToken), 
                cancellationToken);

        public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            ChangeFeedState state,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicChangeFeedAsync(
                state,
                feedRange,
                pageSize,
                cancellationToken);
    }
}
