// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeVisitor
    {
        public abstract void Visit(FeedRangeLogicalPartitionKey feedRange);

        public abstract void Visit(FeedRangePhysicalPartitionKeyRange feedRange);

        public abstract void Visit(FeedRangeEpkRange feedRange);
    }
}
