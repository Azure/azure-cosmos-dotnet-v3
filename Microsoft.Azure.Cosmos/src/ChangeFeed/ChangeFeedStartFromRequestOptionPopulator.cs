// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedStartFromRequestOptionPopulator : ChangeFeedStartFromVisitor
    {
        private const string IfNoneMatchAllHeaderValue = "*";
        private static readonly DateTime StartFromBeginningTime = DateTime.MinValue.ToUniversalTime();

        private readonly RequestMessage requestMessage;

        public ChangeFeedStartFromRequestOptionPopulator(RequestMessage requestMessage)
        {
            this.requestMessage = requestMessage ?? throw new ArgumentNullException(nameof(requestMessage));
        }

        public override void Visit(ChangeFeedStartFromNow startFromNow)
        {
            this.requestMessage.Headers.IfNoneMatch = ChangeFeedStartFromRequestOptionPopulator.IfNoneMatchAllHeaderValue;

            if (startFromNow.FeedRange != null)
            {
                startFromNow.FeedRange.Accept(FeedRangeRequestMessagePopulatorVisitor.Singleton, this.requestMessage);
            }
        }

        public override void Visit(ChangeFeedStartFromTime startFromTime)
        {
            // Our current public contract for ChangeFeedProcessor uses DateTime.MinValue.ToUniversalTime as beginning.
            // We need to add a special case here, otherwise it would send it as normal StartTime.
            // The problem is Multi master accounts do not support StartTime header on ReadFeed, and thus,
            // it would break multi master Change Feed Processor users using Start From Beginning semantics.
            // It's also an optimization, since the backend won't have to binary search for the value.
            if (startFromTime.StartTime != ChangeFeedStartFromRequestOptionPopulator.StartFromBeginningTime)
            {
                this.requestMessage.Headers.Add(
                    HttpConstants.HttpHeaders.IfModifiedSince,
                    startFromTime.StartTime.ToString("r", CultureInfo.InvariantCulture));
            }

            startFromTime.FeedRange.Accept(FeedRangeRequestMessagePopulatorVisitor.Singleton, this.requestMessage);
        }

        public override void Visit(ChangeFeedStartFromContinuation startFromContinuation)
        {
            // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
            this.requestMessage.Headers.IfNoneMatch = startFromContinuation.Continuation;
        }

        public override void Visit(ChangeFeedStartFromBeginning startFromBeginning)
        {
            // We don't need to set any headers to start from the beginning

            // Except for the feed range.
            startFromBeginning.FeedRange.Accept(FeedRangeRequestMessagePopulatorVisitor.Singleton, this.requestMessage);
        }

        public override void Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange)
        {
            // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
            if (startFromContinuationAndFeedRange.Etag != null)
            {
                this.requestMessage.Headers.IfNoneMatch = startFromContinuationAndFeedRange.Etag;
            }

            startFromContinuationAndFeedRange.FeedRange.Accept(FeedRangeRequestMessagePopulatorVisitor.Singleton, this.requestMessage);
        }
    }
}
