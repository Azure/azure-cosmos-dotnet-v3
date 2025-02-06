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
        /// Can Partition fail over on request timeouts.
        /// </summary>
        public abstract bool IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(
            DocumentServiceRequest request);

        /// <summary>
        /// Can Partition fail over on request timeouts.
        /// </summary>
        public abstract void SetBackgroundConnectionInitTask(
            Func<List<Tuple<PartitionKeyRange, Uri>>, Task<bool>> backgroundConnectionInitTask);

        /// <summary>
        /// Can Partition fail over on request timeouts.
        /// </summary>
        public abstract List<Tuple<PartitionKeyRange, Uri>> GetTuples();
    }
}
