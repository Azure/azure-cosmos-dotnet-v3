// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IFeedRangeContinuationVisitor
    {
        public void Visit(FeedRangeCompositeContinuation feedRangeCompositeContinuation);
    }
}
