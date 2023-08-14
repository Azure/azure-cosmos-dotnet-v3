//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Represents server side metrics specific for a single partition.
    /// </summary>
    public sealed class ServerSidePartitionedMetrics
    {
        internal ServerSidePartitionedMetrics(ServerSideMetricsInternal serverSideMetricsInternal)
            : this(new ServerSideMetrics(serverSideMetricsInternal), serverSideMetricsInternal.FeedRange, serverSideMetricsInternal.PartitionKeyRangeId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSidePartitionedMetrics"/> class.
        /// </summary>
        /// <param name="serverSideMetrics"></param>
        /// <param name="feedRange"></param>
        /// <param name="partitionKeyRangeId"></param>
        public ServerSidePartitionedMetrics(ServerSideMetrics serverSideMetrics, string feedRange, string partitionKeyRangeId)
        {
            this.ServerSideMetrics = serverSideMetrics;
            this.FeedRange = feedRange;
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        /// <summary>
        /// Gets the backend metrics for the request.
        /// </summary>
        public ServerSideMetrics ServerSideMetrics { get; }

        /// <summary>
        /// Gets the FeedRange for the partition.
        /// </summary>
        public string FeedRange { get; }

        /// <summary>
        /// Gets the partition key range id for the partition.
        /// </summary>
        public string PartitionKeyRangeId { get; }
    }
}
