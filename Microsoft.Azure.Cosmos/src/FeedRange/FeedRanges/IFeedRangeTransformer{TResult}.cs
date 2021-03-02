// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeTransformer<TResult>
    {
        TResult Visit(FeedRangeLogicalPartitionKey feedRange);

        TResult Visit(FeedRangePhysicalPartitionKeyRange feedRange);

        TResult Visit(FeedRangeEpkRange feedRange);
    }
}
