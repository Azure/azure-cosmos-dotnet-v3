//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    /// <summary>
    /// Metrics received for queries from the backend.
    /// </summary>
    public sealed class ServerSideAccumulatedMetrics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideAccumulatedMetrics"/> class.
        /// </summary>
        /// <param name="cumulativeServerSideMetrics"></param>
        /// <param name="serverSideMetricsList"></param>
        internal ServerSideAccumulatedMetrics(ServerSideMetrics cumulativeServerSideMetrics, List<PartitionedServerSideMetrics> serverSideMetricsList)
        {
            this.CumulativeServerSideMetrics = cumulativeServerSideMetrics;
            this.PartitionedServerSideMetrics = serverSideMetricsList;
        }

        /// <summary>
        /// Gets the ServerSideMetrics accumulated for a single round trip.
        /// </summary>
        public ServerSideMetrics CumulativeServerSideMetrics { get; }

        /// <summary>
        /// Gets the list of ServerSideMetrics, one for for each partition.
        /// </summary>
        public List<PartitionedServerSideMetrics> PartitionedServerSideMetrics { get; }
    }
}
