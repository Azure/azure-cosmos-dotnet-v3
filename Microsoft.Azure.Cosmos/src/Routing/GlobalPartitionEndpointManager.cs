//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
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
    }
}
