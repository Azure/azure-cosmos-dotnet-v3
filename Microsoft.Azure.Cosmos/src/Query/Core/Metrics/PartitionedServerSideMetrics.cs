//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Per-partition metrics for queries from the backend.
    /// </summary>
    public sealed class PartitionedServerSideMetrics
    {
        internal PartitionedServerSideMetrics(ServerSideMetricsInternal serverSideMetricsInternal)
        {
            this.ServerSideMetrics = new ServerSideMetrics(serverSideMetricsInternal);
            this.FeedRange = serverSideMetricsInternal.FeedRange;
            this.PartitionKeyRangeId = serverSideMetricsInternal.PartitionKeyRangeId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionedServerSideMetrics"/> class.
        /// </summary>
        /// <param name="serverSideMetrics"></param>
        /// <param name="feedRange"></param>
        /// <param name="partitionKeyRangeId"></param>
        public PartitionedServerSideMetrics(ServerSideMetrics serverSideMetrics, string feedRange, string partitionKeyRangeId)
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
        /// Gets the FeedRange for a single backend call.
        /// </summary>
        public string FeedRange { get; }

        /// <summary>
        /// Gets the partition key range id for a single backend call.
        /// </summary>
        public string PartitionKeyRangeId { get; }
    }
}
