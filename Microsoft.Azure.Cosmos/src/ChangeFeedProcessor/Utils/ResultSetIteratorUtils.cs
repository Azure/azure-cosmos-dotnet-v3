//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    internal static class ResultSetIteratorUtils
    {
        public static ChangeFeedPartitionKeyResultSetIteratorCore BuildResultSetIterator(
            DocumentServiceLease lease,
            string continuationToken,
            int? maxItemCount,
            ContainerInternal container,
            DateTime? startTime,
            bool startFromBeginning)
        {
            // Back compat with Partition based leases
            FeedRangeInternal feedRange = lease is DocumentServiceLeaseCoreEpk ? lease.FeedRange : new FeedRangePartitionKeyRange(lease.CurrentLeaseToken);

            ChangeFeedStartFrom startFrom;
            if (continuationToken != null)
            {
                // For continuation based feed range we need to manufactor a new continuation token that has the partition key range id in it.
                startFrom = new ChangeFeedStartFromContinuationAndFeedRange(continuationToken, feedRange);
            }
            else if (startTime.HasValue)
            {
                startFrom = ChangeFeedStartFrom.Time(startTime.Value, feedRange);
            }
            else if (startFromBeginning)
            {
                startFrom = ChangeFeedStartFrom.Beginning(feedRange);
            }
            else
            {
                startFrom = ChangeFeedStartFrom.Now(feedRange);
            }

            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
            {
                PageSizeHint = maxItemCount,
            };

            return new ChangeFeedPartitionKeyResultSetIteratorCore(
                clientContext: container.ClientContext,
                container: container,
                changeFeedStartFrom: startFrom,
                options: requestOptions);
        }
    }
}
