//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// The internal implementation for server side metrics specific for a single partition.
    /// </summary>
    internal class ServerSidePartitionedMetricsInternal : ServerSidePartitionedMetrics
    {
        internal ServerSidePartitionedMetricsInternal(ServerSideMetricsInternal serverSideMetricsInternal)
            : this(serverSideMetricsInternal, serverSideMetricsInternal.FeedRange, serverSideMetricsInternal.PartitionKeyRangeId, serverSideMetricsInternal.RequestCharge)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSidePartitionedMetricsInternal"/> class.
        /// </summary>
        /// <param name="serverSideMetricsInternal"></param>
        /// <param name="feedRange"></param>
        /// <param name="partitionKeyRangeId"></param>
        /// <param name="requestCharge"></param>
        internal ServerSidePartitionedMetricsInternal(ServerSideMetricsInternal serverSideMetricsInternal, string feedRange, int? partitionKeyRangeId, double? requestCharge)
        {
            this.ServerSideMetricsInternal = serverSideMetricsInternal;
            this.FeedRange = feedRange;
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.RequestCharge = requestCharge;
        }

        public ServerSideMetricsInternal ServerSideMetricsInternal { get; }

        public override ServerSideMetrics ServerSideMetrics => this.ServerSideMetricsInternal;

        public override string FeedRange { get; }

        public override int? PartitionKeyRangeId { get; }

        public override double? RequestCharge { get; }
    }
}
