//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Metrics received for queries from the backend.
    /// </summary>
    public class ServerSideCumulativeMetrics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideCumulativeMetrics"/> class.
        /// </summary>
        /// <param name="serverSideMetricsList"></param>
        internal ServerSideCumulativeMetrics(IEnumerable<ServerSidePartitionedMetrics> serverSideMetricsList)
        {
            this.PartitionedMetrics = serverSideMetricsList.ToList();
            this.CumulativeMetrics = ServerSideMetrics.Create(serverSideMetricsList.Select(partitionedMetrics => partitionedMetrics.ServerSideMetrics));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSideCumulativeMetrics"/> class.
        /// </summary>
        public ServerSideCumulativeMetrics()
        {
        }

        /// <summary>
        /// Gets the ServerSideMetrics accumulated for a single round trip.
        /// </summary>
        public virtual ServerSideMetrics CumulativeMetrics { get; }

        /// <summary>
        /// Gets the list of ServerSideMetrics, one for for each partition.
        /// </summary>
        public virtual IReadOnlyList<ServerSidePartitionedMetrics> PartitionedMetrics { get; }
    }
}
