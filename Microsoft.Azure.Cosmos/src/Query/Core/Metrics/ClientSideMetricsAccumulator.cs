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
        private readonly List<ClientSideMetrics> clientSideMetricsList;

        public ClientSideMetricsAccumulator()
        {
            this.clientSideMetricsList = new List<ClientSideMetrics>();
        }

        public void Accumulate(ClientSideMetrics clientSideMetrics)
        {
            if (clientSideMetrics == null)
            {
                throw new ArgumentNullException(nameof(clientSideMetrics));
            }

            this.clientSideMetricsList.Add(clientSideMetrics);
        }

        public ClientSideMetrics GetClientSideMetrics()
        {
            long retries = 0;
            double requestCharge = 0;
            List<FetchExecutionRange> fetchExecutionRanges = new List<FetchExecutionRange>();

            foreach (ClientSideMetrics clientSideMetrics in this.clientSideMetricsList)
            {
                retries += clientSideMetrics.Retries;
                requestCharge += clientSideMetrics.RequestCharge;
                fetchExecutionRanges.AddRange(clientSideMetrics.FetchExecutionRanges);
            }

            return new ClientSideMetrics(
                retries: retries,
                requestCharge: requestCharge,
                fetchExecutionRanges: fetchExecutionRanges);
        }
    }
}
