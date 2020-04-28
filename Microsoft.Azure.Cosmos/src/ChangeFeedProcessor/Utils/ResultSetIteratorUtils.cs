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
            bool startFromBeginning,
            CosmosStreamTransformer streamTransformer = null)
        {
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions();
            requestOptions.CosmosStreamTransformer = streamTransformer;
            if (startTime.HasValue)
            {
                requestOptions.StartTime = startTime.Value;
            }
            else if (startFromBeginning)
            {
                requestOptions.StartTime = ChangeFeedRequestOptions.DateTimeStartFromBeginning;
            }

            return new ChangeFeedPartitionKeyResultSetIteratorCore(
                partitionKeyRangeId: partitionKeyRangeId,
                continuationToken: continuationToken,
                maxItemCount: maxItemCount,
                clientContext: container.ClientContext,
                container: container,
                options: requestOptions);
        }
    }
}
