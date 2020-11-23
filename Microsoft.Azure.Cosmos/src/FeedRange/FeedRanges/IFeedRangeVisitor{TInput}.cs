// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeVisitor<TInput>
    {
        void Visit(FeedRangePartitionKey feedRange, TInput input);

        void Visit(FeedRangePartitionKeyRange feedRange, TInput input);

        void Visit(FeedRangeEpk feedRange, TInput input);
    }
}
