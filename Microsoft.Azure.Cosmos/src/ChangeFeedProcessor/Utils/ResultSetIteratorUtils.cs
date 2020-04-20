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
            ContainerCore container,
            DateTime? startTime,
            bool startFromBeginning)
        {
            ChangeFeedRequestOptions.StartFrom startFrom;
            if (continuationToken != null)
            {
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuationToken);
            }
            else if (startTime.HasValue)
            {
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromTime(startTime.Value);
            }
            else if (!startFromBeginning)
            {
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromNow();
            }
            else
            {
                startFrom = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning();
            }

            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
            {
                MaxItemCount = maxItemCount,
                PartitionKeyRangeId = partitionKeyRangeId,
                From = startFrom,
            };

            return new ChangeFeedPartitionKeyResultSetIteratorCore(
                clientContext: container.ClientContext,
                container: container,
                options: requestOptions);
        }
    }
}
