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
            FeedRange feedRange = new FeedRangePartitionKeyRange(partitionKeyRangeId);

            ChangeFeedRequestOptions.StartFrom startFrom;
            if (continuationToken != null)
            {
                // For continuation based feed range we need to manufactor a new continuation token that has the partition key range id in it.
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuationToken);
                throw new NotImplementedException("Need to implement after I see what the continuation token looks like.");
            }
            else if (startTime.HasValue)
            {
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromTimeWithRange(startTime.Value, feedRange);
            }
            else if (startFromBeginning)
            {
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromBeginningWithRange(feedRange);
            }
            else
            {
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromNowWithRange(feedRange);
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
