// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    internal abstract class ChangeFeedStartFromVisitor<TResult>
    {
        public abstract TResult Visit(ChangeFeedStartFromNow startFromNow);
        public abstract TResult Visit(ChangeFeedStartFromTime startFromTime);
        public abstract TResult Visit(ChangeFeedStartFromContinuation startFromContinuation);
        public abstract TResult Visit(ChangeFeedStartFromBeginning startFromBeginning);
        public abstract TResult Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange);
    }
}
