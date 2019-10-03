//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Stores client side QueryMetrics.
    /// </summary>
    internal sealed class ClientSideMetrics
    {
        public static readonly ClientSideMetrics Zero = new ClientSideMetrics(
            retries: 0,
            requestCharge: 0,
            fetchExecutionRanges: new List<FetchExecutionRange>(),
            partitionSchedulingTimeSpans: new List<Tuple<string, SchedulingTimeSpan>>());

        private readonly long retries;
        private readonly double requestCharge;
        private readonly IEnumerable<FetchExecutionRange> fetchExecutionRanges;
        private readonly IEnumerable<Tuple<string, SchedulingTimeSpan>> partitionSchedulingTimeSpans;

        /// <summary>
        /// Initializes a new instance of the ClientSideMetrics class.
        /// </summary>
        /// <param name="retries">The number of retries required to execute the query.</param>
        /// <param name="requestCharge">The request charge incurred from executing the query.</param>
        /// <param name="fetchExecutionRanges">The fetch execution ranges from executing the query.</param>
        /// <param name="partitionSchedulingTimeSpans">The partition scheduling timespans from the query.</param>
        [JsonConstructor]
        public ClientSideMetrics(
            long retries,
            double requestCharge,
            IEnumerable<FetchExecutionRange> fetchExecutionRanges,
            IEnumerable<Tuple<string, SchedulingTimeSpan>> partitionSchedulingTimeSpans)
        {
            if (fetchExecutionRanges == null)
            {
                throw new ArgumentNullException("fetchExecutionRanges");
            }

            if (partitionSchedulingTimeSpans == null)
            {
                throw new ArgumentNullException("partitionSchedulingTimeSpans");
            }

            this.retries = retries;
            this.requestCharge = requestCharge;
            this.fetchExecutionRanges = fetchExecutionRanges;
            this.partitionSchedulingTimeSpans = partitionSchedulingTimeSpans;
        }

        /// <summary>
        /// Gets number of retries in the Azure DocumentDB database service (see IRetryPolicy.cs).
        /// </summary>
        public long Retries
        {
            get
            {
                return this.retries;
            }
        }

        /// <summary>
        /// Gets the request charge for this continuation of the query.
        /// </summary>
        public double RequestCharge
        {
            get
            {
                return this.requestCharge;
            }
        }

        /// <summary>
        /// Gets the Fetch Execution Ranges for this continuation of the query.
        /// </summary>
        public IEnumerable<FetchExecutionRange> FetchExecutionRanges
        {
            get
            {
                return this.fetchExecutionRanges;
            }
        }

        /// <summary>
        /// Gets the Partition Scheduling TimeSpans for this query.
        /// </summary>
        public IEnumerable<Tuple<string, SchedulingTimeSpan>> PartitionSchedulingTimeSpans
        {
            get
            {
                return this.partitionSchedulingTimeSpans;
            }
        }

        /// <summary>
        /// Creates a new ClientSideMetrics that is the sum of all elements in an IEnumerable.
        /// </summary>
        /// <param name="clientSideMetricsList">The IEnumerable to aggregate.</param>
        /// <returns>A new ClientSideMetrics that is the sum of all elements in an IEnumerable.</returns>
        public static ClientSideMetrics CreateFromIEnumerable(IEnumerable<ClientSideMetrics> clientSideMetricsList)
        {
            long retries = 0;
            double requestCharge = 0;
            IEnumerable<FetchExecutionRange> fetchExecutionRanges = new List<FetchExecutionRange>();
            IEnumerable<Tuple<string, SchedulingTimeSpan>> schedulingTimeSpans = new List<Tuple<string, SchedulingTimeSpan>>();

            if (clientSideMetricsList == null)
            {
                throw new ArgumentNullException("clientSideQueryMetricsList");
            }

            foreach (ClientSideMetrics clientSideQueryMetrics in clientSideMetricsList)
            {
                retries += clientSideQueryMetrics.retries;
                requestCharge += clientSideQueryMetrics.requestCharge;
                fetchExecutionRanges = fetchExecutionRanges.Concat(clientSideQueryMetrics.fetchExecutionRanges);
                schedulingTimeSpans = schedulingTimeSpans.Concat(clientSideQueryMetrics.partitionSchedulingTimeSpans);
            }

            return new ClientSideMetrics(retries, requestCharge, fetchExecutionRanges, schedulingTimeSpans);
        }
    }
}
