//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System;

    internal static class ResultSetIteratorUtils
    {
        public static ChangeFeedPartitionKeyResultSetIteratorCore BuildResultSetIterator(
            string partitionKeyRangeId,
            string continuationToken,
            int? maxItemCount,
            ContainerInternal container,
            DateTime? startTime,
            bool startFromBeginning)
        {
            FeedRangeInternal feedRange = new FeedRangePartitionKeyRange(partitionKeyRangeId);

            ChangeFeedStartFrom startFrom;
            if (continuationToken != null)
            {
                // For continuation based feed range we need to manufactor a new continuation token that has the partition key range id in it.
                startFrom = new ChangeFeedStartFromContinuationAndFeedRange(continuationToken, feedRange);
            }
            else if (startTime.HasValue)
            {
                startFrom = ChangeFeedStartFrom.CreateFromTimeWithRange(startTime.Value, feedRange);
            }
            else if (startFromBeginning)
            {
                startFrom = ChangeFeedStartFrom.CreateFromBeginningWithRange(feedRange);
            }
            else
            {
                startFrom = ChangeFeedStartFrom.CreateFromNowWithRange(feedRange);
            }

            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
            {
                MaxItemCount = maxItemCount,
                From = startFrom,
            };

            return new ChangeFeedPartitionKeyResultSetIteratorCore(
                clientContext: container.ClientContext,
                container: container,
                options: requestOptions);
        }
    }
}
