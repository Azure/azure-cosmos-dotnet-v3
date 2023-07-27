//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal struct ClientSideMetricsAccumulator
    {
        public ClientSideMetricsAccumulator(long retries, double requestCharge, IEnumerable<FetchExecutionRange> fetchExecutionRanges)
        {
            this.Retries = retries;
            this.RequestCharge = requestCharge;
            this.FetchExecutionRanges = fetchExecutionRanges;
        }

        public long Retries { get; set; }

        public double RequestCharge { get; set; }

        public IEnumerable<FetchExecutionRange> FetchExecutionRanges { get; set; }

        public void Accumulate(ClientSideMetrics clientSideMetrics)
        {
            if (clientSideMetrics == null)
            {
                throw new ArgumentNullException(nameof(clientSideMetrics));
            }

            this.Retries += clientSideMetrics.Retries;
            this.RequestCharge += clientSideMetrics.RequestCharge;
            this.FetchExecutionRanges = (this.FetchExecutionRanges ?? Enumerable.Empty<FetchExecutionRange>()).Concat(clientSideMetrics.FetchExecutionRanges);

            return;
        }

        public static ClientSideMetrics ToClientSideMetrics(ClientSideMetricsAccumulator accumulator)
        {
            return new ClientSideMetrics(
                retries: accumulator.Retries,
                requestCharge: accumulator.RequestCharge,
                fetchExecutionRanges: accumulator.FetchExecutionRanges);
        }
    }
}
