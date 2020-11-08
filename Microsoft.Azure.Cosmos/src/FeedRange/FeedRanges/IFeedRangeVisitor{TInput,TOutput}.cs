// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeVisitor<TInput, TOutput>
    {
        TOutput Visit(FeedRangePartitionKey feedRange, TInput input);

        TOutput Visit(FeedRangePartitionKeyRange feedRange, TInput input);

        TOutput Visit(FeedRangeEpk feedRange, TInput input);
    }
}
