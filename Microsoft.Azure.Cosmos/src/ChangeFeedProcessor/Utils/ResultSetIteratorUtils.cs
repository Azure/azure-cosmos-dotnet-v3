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
            CosmosContainerCore cosmosContainer,
            DateTime? startTime,
            bool startFromBeginning)
        {
            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions();
            if (startTime.HasValue)
            {
                requestOptions.StartTime = startTime.Value;
            }
            else if (startFromBeginning)
            {
                requestOptions.StartTime = DateTime.MinValue;
            }

            return new ChangeFeedPartitionKeyResultSetIteratorCore(
                partitionKeyRangeId: partitionKeyRangeId,
                continuationToken: continuationToken,
                maxItemCount: maxItemCount,
                clientContext: cosmosContainer.ClientContext,
                cosmosContainer: cosmosContainer,
                options: requestOptions);
        }
    }
}
