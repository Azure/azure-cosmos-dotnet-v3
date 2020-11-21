// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeTransformer<TResult>
    {
        TResult Visit(FeedRangePartitionKey feedRange);

        TResult Visit(FeedRangePartitionKeyRange feedRange);

        TResult Visit(FeedRangeEpk feedRange);
    }
}
