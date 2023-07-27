//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class ClientSideMetricsAccumulator
    {
        public ClientSideMetricsAccumulator(long retries, double requestCharge, IEnumerable<FetchExecutionRange> fetchExecutionRanges)
        {
            this.Retries = retries;
            this.RequestCharge = requestCharge;
            this.FetchExecutionRanges = fetchExecutionRanges;
        }

        public ClientSideMetricsAccumulator()
        {
            this.Retries = default;
            this.RequestCharge = default;
            this.FetchExecutionRanges = default;
        }

        private long Retries { get; set; }

        private double RequestCharge { get; set; }

        private IEnumerable<FetchExecutionRange> FetchExecutionRanges { get; set; }

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

        public ClientSideMetrics GetClientSideMetrics()
        {
            return new ClientSideMetrics(
                retries: this.Retries,
                requestCharge: this.RequestCharge,
                fetchExecutionRanges: this.FetchExecutionRanges);
        }
    }
}
