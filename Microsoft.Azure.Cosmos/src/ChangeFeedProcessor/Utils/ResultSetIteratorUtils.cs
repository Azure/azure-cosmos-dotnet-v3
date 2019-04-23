//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

using System;

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    internal static class ResultSetIteratorUtils
    {
        public static CosmosChangeFeedPartitionKeyResultSetIteratorCore BuildResultSetIterator(
            string partitionKeyRangeId,
            string continuationToken,
            int? maxItemCount,
            CosmosContainerCore cosmosContainer,
            DateTime? startTime,
            bool startFromBeginning
            )
        {
            CosmosChangeFeedRequestOptions requestOptions = new CosmosChangeFeedRequestOptions();
            if (startTime.HasValue)
            {
                requestOptions.StartTime = startTime.Value;
            }
            else if (startFromBeginning)
            {
                requestOptions.StartTime = DateTime.MinValue;
            }

            return new CosmosChangeFeedPartitionKeyResultSetIteratorCore(
                partitionKeyRangeId: partitionKeyRangeId,
                continuationToken: continuationToken,
                maxItemCount: maxItemCount,
                cosmosContainer: cosmosContainer,
                options: requestOptions);
        }
    }
}
