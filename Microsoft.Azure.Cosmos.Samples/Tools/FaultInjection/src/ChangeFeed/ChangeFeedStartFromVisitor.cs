// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    internal abstract class ChangeFeedStartFromVisitor
    {
        public abstract void Visit(ChangeFeedStartFromNow startFromNow);
        public abstract void Visit(ChangeFeedStartFromTime startFromTime);
        public abstract void Visit(ChangeFeedStartFromContinuation startFromContinuation);
        public abstract void Visit(ChangeFeedStartFromBeginning startFromBeginning);
        public abstract void Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange);
    }
}
