// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IDocumentContainer : IMonadicDocumentContainer, IFeedRangeProvider, IQueryDataSource, IReadFeedDataSource
#if !COSMOS_GW_AOT
        , IChangeFeedDataSource 
#endif
    {
        Task<Record> CreateItemAsync(
            CosmosObject payload,
            JsonTypeInfo jsonTypeInfo,
            CancellationToken cancellationToken);

        Task<Record> ReadItemAsync(
            CosmosElement partitionKey,
            string identifier,
            CancellationToken cancellationToken);

        Task SplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken);

        Task MergeAsync(
            FeedRangeInternal feedRange1,
            FeedRangeInternal feedRange2,
            CancellationToken cancellationToken);

        Task<string> GetResourceIdentifierAsync(
            ITrace trace,
            CancellationToken cancellationToken);
    }
}
