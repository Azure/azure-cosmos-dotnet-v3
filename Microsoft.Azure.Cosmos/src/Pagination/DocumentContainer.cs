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

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicGetChildRangeAsync(
                feedRange,
                cancellationToken);

        public Task<List<FeedRangeEpk>> GetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => TryCatch<List<FeedRangeInternal>>.UnsafeGetResultAsync(
                this.MonadicGetChildRangeAsync(
                    feedRange,
                    cancellationToken),
                cancellationToken);

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicGetFeedRangesAsync(
                cancellationToken);

        public Task<List<FeedRangeEpk>> GetFeedRangesAsync(
            CancellationToken cancellationToken) => TryCatch<List<FeedRangeEpk>>.UnsafeGetResultAsync(
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
            CancellationToken cancellationToken) => TryCatch<Record>.UnsafeGetResultAsync(
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
            FeedRangeInternal feedRange,
            ResourceId resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicReadFeedAsync(
                feedRange,
                resourceIdentifer,
                pageSize,
                cancellationToken);

        public Task<DocumentContainerPage> ReadFeedAsync(
            FeedRangeInternal feedRange,
            ResourceId resourceIdentifier,
            int pageSize,
            CancellationToken cancellationToken) => TryCatch<DocumentContainerPage>.UnsafeGetResultAsync(
                this.MonadicReadFeedAsync(
                    feedRange,
                    resourceIdentifier,
                    pageSize,
                    cancellationToken),
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
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicSplitAsync(
                feedRange,
                cancellationToken);

        public Task SplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken) => TryCatch.UnsafeWaitAsync(
                this.MonadicSplitAsync(
                    feedRange,
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

        public Task<string> GetResourceIdentifierAsync(
            CancellationToken cancellationToken) => TryCatch<string>.UnsafeGetResultAsync(
                this.MonadicGetResourceIdentifierAsync(cancellationToken),
                cancellationToken);

        public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(
            CancellationToken cancellationToken) => this.monadicDocumentContainer.MonadicGetResourceIdentifierAsync(cancellationToken);
    }
}
