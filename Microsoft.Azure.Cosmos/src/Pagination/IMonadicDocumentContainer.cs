// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IMonadicDocumentContainer :
        IMonadicFeedRangeProvider,  
        IMonadicQueryDataSource, 
        IMonadicReadFeedDataSource
#if !COSMOS_GW_AOT
        , 
        IMonadicChangeFeedDataSource
#endif
    {
        Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            JsonTypeInfo jsonTypeInfo,
            CancellationToken cancellationToken);

        Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            string identifer,
            CancellationToken cancellationToken);

        Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken);

        Task<TryCatch> MonadicMergeAsync(
            FeedRangeInternal feedRange1,
            FeedRangeInternal feedRange2,
            CancellationToken cancellationToken);

        Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(
            ITrace trace,
            CancellationToken cancellationToken);
    }
}
