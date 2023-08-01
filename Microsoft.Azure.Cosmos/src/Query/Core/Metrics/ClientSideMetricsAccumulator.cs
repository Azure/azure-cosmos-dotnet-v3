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
        public ClientSideMetricsAccumulator()
        {
            this.ClientSideMetricsList = new List<ClientSideMetrics>();
        }

        private readonly List<ClientSideMetrics> ClientSideMetricsList;

        public void Accumulate(ClientSideMetrics clientSideMetrics)
        {
            if (clientSideMetrics == null)
            {
                throw new ArgumentNullException(nameof(clientSideMetrics));
            }

            this.ClientSideMetricsList.Add(clientSideMetrics);
        }

        public ClientSideMetrics GetClientSideMetrics()
        {
            long retries = default;
            double requestCharge = default;
            IEnumerable<FetchExecutionRange> fetchExecutionRanges = default;

            foreach (ClientSideMetrics clientSideMetrics in this.ClientSideMetricsList)
            {
                retries += clientSideMetrics.Retries;
                requestCharge += clientSideMetrics.RequestCharge;
                fetchExecutionRanges = (fetchExecutionRanges ?? Enumerable.Empty<FetchExecutionRange>()).Concat(clientSideMetrics.FetchExecutionRanges);
            }

            return new ClientSideMetrics(
                retries: retries,
                requestCharge: requestCharge,
                fetchExecutionRanges: fetchExecutionRanges);
        }
    }
}
