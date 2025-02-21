//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal abstract class GlobalPartitionEndpointManager
    {
        /// <summary>
        /// Updates the DocumentServiceRequest routing location to point
        /// new a location based if a partition level failover occurred
        /// </summary>
        public abstract bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request);

        /// <summary>
        /// Marks the current location unavailable for write. Future 
        /// requests will be routed to the next location if available
        /// </summary>
        public abstract bool TryMarkEndpointUnavailableForPartitionKeyRange(
            DocumentServiceRequest request);

        /// <summary>
        /// Increments the failure counter for the specified partition and checks if the partition can fail over.
        /// This method is used to determine if a partition should be failed over based on the number of request failures.
        /// </summary>
        public abstract bool IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(
            DocumentServiceRequest request);

        /// <summary>
        /// Sets the background connection periodic refresh task.
        /// </summary>
        public abstract void SetBackgroundConnectionPeriodicRefreshTask(
            Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task> backgroundConnectionInitTask);
    }
}
