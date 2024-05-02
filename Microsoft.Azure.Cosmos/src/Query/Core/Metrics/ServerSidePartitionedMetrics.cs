//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Represents server side metrics specific for a single partition.
    /// </summary>
    public abstract class ServerSidePartitionedMetrics
    {
        /// <summary>
        /// Gets the backend metrics for the request.
        /// </summary>
        public abstract ServerSideMetrics ServerSideMetrics { get; }

        /// <summary>
        /// Gets the FeedRange for the partition.
        /// </summary>
        public abstract string FeedRange { get; }

        /// <summary>
        /// Gets the partition key range id for the partition.
        /// </summary>
        /// <remarks>
        /// Only has a value in direct mode. When using gateway mode, this is null.
        /// </remarks>
        public abstract int? PartitionKeyRangeId { get; }

        /// <summary>
        /// Gets the request charge for the operation on this partition.
        /// </summary>
        public abstract double RequestCharge { get; }
    }
}
