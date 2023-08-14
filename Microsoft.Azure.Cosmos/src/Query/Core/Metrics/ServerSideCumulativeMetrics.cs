//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    /// <summary>
    /// Metrics received for queries from the backend.
    /// </summary>
    public sealed class ServerSideCumulativeMetrics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideCumulativeMetrics"/> class.
        /// </summary>
        /// <param name="cumulativeMetrics"></param>
        /// <param name="serverSideMetricsList"></param>
        public ServerSideCumulativeMetrics(ServerSideMetrics cumulativeMetrics, List<ServerSidePartitionedMetrics> serverSideMetricsList)
        {
            this.CumulativeMetrics = cumulativeMetrics;
            this.PartitionedMetrics = serverSideMetricsList;
        }

        /// <summary>
        /// Gets the ServerSideMetrics accumulated for a single round trip.
        /// </summary>
        public ServerSideMetrics CumulativeMetrics { get; }

        /// <summary>
        /// Gets the list of ServerSideMetrics, one for for each partition.
        /// </summary>
        public List<ServerSidePartitionedMetrics> PartitionedMetrics { get; }
    }
}
