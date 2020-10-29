// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Documents;

    internal interface IDocumentContainer : IMonadicDocumentContainer, IFeedRangeProvider, IQueryDataSource, IChangeFeedDataSource
    {
        Task<Record> CreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken);

        Task<Record> ReadItemAsync(
            CosmosElement partitionKey,
            string identifier,
            CancellationToken cancellationToken);

        Task<DocumentContainerPage> ReadFeedAsync(
            FeedRangeInternal feedRange,
            ResourceId resourceIdentifier,
            int pageSize,
            CancellationToken cancellationToken);

        Task SplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken);

        Task<string> GetResourceIdentifierAsync(CancellationToken cancellationToken);
    }
}
