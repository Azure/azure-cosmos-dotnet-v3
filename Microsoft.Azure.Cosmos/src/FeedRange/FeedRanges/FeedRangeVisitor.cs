// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Visitor to populate RequestMessage headers and properties based on FeedRange and Continuation.
    /// </summary>
    internal sealed class FeedRangeVisitor
    {
        private readonly RequestMessage request;
        public FeedRangeVisitor(RequestMessage request)
        {
            this.request = request;
        }

        public void Visit(FeedRangePartitionKey feedRange)
        {
            this.request.Headers.PartitionKey = feedRange.PartitionKey.ToJsonString();
        }

        public void Visit(FeedRangePartitionKeyRange feedRange)
        {
            ChangeFeedRequestOptions.FillPartitionKeyRangeId(this.request, feedRange.PartitionKeyRangeId);
        }

        public void Visit(FeedRangeEpk feedRange)
        {
            // No-op since the range is defined by the composite continuation token
        }

        public void Visit(
            FeedRangeCompositeContinuation continuation,
            Action<RequestMessage, string> fillContinuation)
        {
            // In case EPK has already been set by compute
            if (!this.request.Properties.ContainsKey(HandlerConstants.StartEpkString))
            {
                this.request.Properties[HandlerConstants.StartEpkString] = continuation.CurrentToken.Range.Min;
                this.request.Properties[HandlerConstants.EndEpkString] = continuation.CurrentToken.Range.Max;
            }

            fillContinuation(this.request, continuation.GetContinuation());
        }
    }
}
