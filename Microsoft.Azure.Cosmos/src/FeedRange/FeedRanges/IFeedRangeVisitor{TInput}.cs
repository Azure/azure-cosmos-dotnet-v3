// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeVisitor<TInput>
    {
        void Visit(FeedRangeLogicalPartitionKey feedRange, TInput input);

        void Visit(FeedRangePhysicalPartitionKeyRange feedRange, TInput input);

        void Visit(FeedRangeEpkRange feedRange, TInput input);
    }
}
