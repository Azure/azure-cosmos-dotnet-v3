//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    internal static class ChangeFeedHelper
    {
        internal static bool IsChangeFeedWithQueryRequest(
           OperationType operationType,
           bool hasPayload)
        {
            // ChangeFeed with payload is a CF with query support and will
            // be a query POST request.
            return operationType == OperationType.ReadFeed && hasPayload;
        }

        /// <summary>
        /// Returns a boolean flag to indicate if a change feed operation can safely handle missing primes. This is
        /// supported for both incremental and full-fidelity change feed operations.
        /// </summary>
        /// <param name="resourceType">An instance of <see cref="ResourceType"/> containing the resource type</param>
        /// <param name="operationType">An instance of <see cref="OperationType"/> containing the operation type</param>
        /// <param name="consistencyLevel">An instance of <see cref="ConsistencyLevel"/> containing the client consistency level</param>
        /// <param name="isPartitionLevelFailoverEnabled">A boolean flag indicating if partition level failover is enabled</param>
        /// <returns>A boolean flag indicating is the change feed operation can support missing primes.</returns>
        internal static bool IsChangeFeedSupportedToHandleMissingPrimes(
            ResourceType resourceType,
            OperationType operationType,
            ConsistencyLevel? consistencyLevel,
            bool isPartitionLevelFailoverEnabled)
        {
            if (resourceType == ResourceType.Document
                && operationType == OperationType.ReadFeed
                && consistencyLevel.HasValue
                && isPartitionLevelFailoverEnabled)
            {
                return consistencyLevel.Value != ConsistencyLevel.Strong;
            }

            return false;
        }
    }
}
