// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Visitor to populate RequestMessage headers and properties based on FeedRange.
    /// </summary>
    internal sealed class FeedRangeRequestMessagePopulatorVisitor : IFeedRangeVisitor<RequestMessage>
    {
        public static readonly FeedRangeRequestMessagePopulatorVisitor Singleton = new FeedRangeRequestMessagePopulatorVisitor();

        private FeedRangeRequestMessagePopulatorVisitor()
        {
        }

        public void Visit(FeedRangePartitionKey feedRange, RequestMessage requestMessage)
        {
            requestMessage.Headers.PartitionKey = feedRange.PartitionKey.ToJsonString();
        }

        public void Visit(FeedRangePartitionKeyRange feedRange, RequestMessage requestMessage)
        {
            requestMessage.PartitionKeyRangeId = new Documents.PartitionKeyRangeIdentity(feedRange.PartitionKeyRangeId);
        }

        public void Visit(FeedRangeEpk feedRange, RequestMessage requestMessage)
        {
            // In case EPK has already been set by compute
            if (!requestMessage.Properties.ContainsKey(HandlerConstants.StartEpkString))
            {
                requestMessage.Properties[HandlerConstants.StartEpkString] = feedRange.Range.Min;
                requestMessage.Properties[HandlerConstants.EndEpkString] = feedRange.Range.Max;
            }
        }
    }
}
