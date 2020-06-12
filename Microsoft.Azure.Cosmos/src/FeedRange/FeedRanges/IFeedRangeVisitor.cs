// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeVisitor
    {
        public abstract void Visit(FeedRangePartitionKey feedRange);

        public abstract void Visit(FeedRangePartitionKeyRange feedRange);

        public abstract void Visit(FeedRangeEPK feedRange);
    }
}
