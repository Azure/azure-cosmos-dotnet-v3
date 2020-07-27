// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Visitor to populate RequestMessage headers and properties based on FeedRange.
    /// </summary>
    internal sealed class FeedRangeContinuationRequestMessagePopulatorVisitor : IFeedRangeContinuationVisitor
    {
        private readonly RequestMessage request;
        private readonly Action<RequestMessage, string> fillContinuation;

        public FeedRangeContinuationRequestMessagePopulatorVisitor(RequestMessage request, Action<RequestMessage, string> fillContinuation)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.fillContinuation = fillContinuation ?? throw new ArgumentNullException(nameof(fillContinuation));
        }

        public void Visit(FeedRangeCompositeContinuation feedRangeCompositeContinuation)
        {
            // In case EPK has already been set by compute
            if (!this.request.Properties.ContainsKey(HandlerConstants.StartEpkString))
            {
                this.request.Properties[HandlerConstants.StartEpkString] = feedRangeCompositeContinuation.CurrentToken.Range.Min;
                this.request.Properties[HandlerConstants.EndEpkString] = feedRangeCompositeContinuation.CurrentToken.Range.Max;
            }

            this.fillContinuation(this.request, feedRangeCompositeContinuation.GetContinuation());
        }
    }
}
