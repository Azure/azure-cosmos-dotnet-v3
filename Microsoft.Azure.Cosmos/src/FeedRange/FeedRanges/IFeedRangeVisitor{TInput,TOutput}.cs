// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeVisitor<TInput, TOutput>
    {
        TOutput Visit(FeedRangeLogicalPartitionKey feedRange, TInput input);

        TOutput Visit(FeedRangePhysicalPartitionKeyRange feedRange, TInput input);

        TOutput Visit(FeedRangeEpkRange feedRange, TInput input);
    }
}
