// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Documents;
    using ChangeFeedPagination = Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using CosmosPagination = Microsoft.Azure.Cosmos.Pagination;

    internal sealed class ChangeFeedModeFullFidelity : ChangeFeedMode
    {
        public static readonly string FullFidelityHeader = "Full-Fidelity Feed"; // HttpConstants.A_IMHeaderValues.FullFidelityFeed

        public static ChangeFeedMode Instance { get; } = new ChangeFeedModeFullFidelity();

        internal override void Accept(RequestMessage requestMessage)
        {
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.A_IM, ChangeFeedModeFullFidelity.FullFidelityHeader);
        }

        internal override CosmosPagination.ISplitStrategy<ChangeFeedPagination.ChangeFeedPage, ChangeFeedPagination.ChangeFeedState>
            CreateSplitStrategy(
                CosmosPagination.IFeedRangeProvider feedRangeProvider,
                CosmosPagination.CreatePartitionRangePageAsyncEnumerator<
                    ChangeFeedPagination.ChangeFeedPage, ChangeFeedPagination.ChangeFeedState> partitionRangeEnumeratorCreator)
        {
            return new FullFidelityChangeFeedSplitStrategy(feedRangeProvider, partitionRangeEnumeratorCreator);
        }
    }
}
