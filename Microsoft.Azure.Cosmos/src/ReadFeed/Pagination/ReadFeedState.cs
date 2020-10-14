namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class ReadFeedState : State
    {
        public ReadFeedState(CosmosElement continuationToken)
        {
            this.ContinuationToken = continuationToken ?? throw new ArgumentNullException(nameof(continuationToken));
        }

        public CosmosElement ContinuationToken { get; }
    }
}
