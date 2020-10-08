// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    internal sealed class ChangeFeedRangeExtractor : ChangeFeedStartFromVisitor<FeedRange>
    {
        public static readonly ChangeFeedRangeExtractor Singleton = new ChangeFeedRangeExtractor();

        private ChangeFeedRangeExtractor()
        {
        }

        public override FeedRange Visit(ChangeFeedStartFromNow startFromNow)
        {
            return startFromNow.FeedRange;
        }

        public override FeedRange Visit(ChangeFeedStartFromTime startFromTime)
        {
            return startFromTime.FeedRange;
        }

        public override FeedRange Visit(ChangeFeedStartFromContinuation startFromContinuation)
        {
            throw new NotSupportedException($"{nameof(ChangeFeedStartFromContinuation)} does not have a feed range.");
        }

        public override FeedRange Visit(ChangeFeedStartFromBeginning startFromBeginning)
        {
            return startFromBeginning.FeedRange;
        }

        public override FeedRange Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange)
        {
            return startFromContinuationAndFeedRange.FeedRange;
        }
    }
}
