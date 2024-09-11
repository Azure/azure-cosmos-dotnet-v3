//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Internal implementation of metrics received for queries from the backend.
    /// </summary>
    internal class ServerSideCumulativeMetricsInternal : ServerSideCumulativeMetrics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideCumulativeMetrics"/> class.
        /// </summary>
        /// <param name="serverSideMetricsList"></param>
        internal ServerSideCumulativeMetricsInternal(IEnumerable<ServerSidePartitionedMetricsInternal> serverSideMetricsList)
        {
            this.PartitionedMetrics = serverSideMetricsList.ToList();
            this.CumulativeMetrics = ServerSideMetricsInternal.Create(serverSideMetricsList.Select(partitionedMetrics => partitionedMetrics.ServerSideMetricsInternal));
            this.TotalRequestCharge = serverSideMetricsList.Sum(partitionedMetrics => partitionedMetrics.RequestCharge);
        }

        public override ServerSideMetrics CumulativeMetrics { get; }

        public override IReadOnlyList<ServerSidePartitionedMetrics> PartitionedMetrics { get; }

        public override double TotalRequestCharge { get; }
    }
}
