//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;

    internal class QueryMetricsAccumulator
    {
        public QueryMetricsAccumulator(
            BackendMetricsAccumulator backendMetricsAccumulator,
            IndexUtilizationInfoAccumulator indexUtilizationInfoAccumulator,
            ClientSideMetricsAccumulator clientSideMetricsAccumulator)
        {
            this.BackendMetricsAccumulator = backendMetricsAccumulator;
            this.IndexUtilizationInfoAccumulator = indexUtilizationInfoAccumulator;
            this.ClientSideMetricsAccumulator = clientSideMetricsAccumulator;
        }

        public QueryMetricsAccumulator()
        {
            this.BackendMetricsAccumulator = new BackendMetricsAccumulator();
            this.IndexUtilizationInfoAccumulator = new IndexUtilizationInfoAccumulator();
            this.ClientSideMetricsAccumulator = new ClientSideMetricsAccumulator();
        }

        private BackendMetricsAccumulator BackendMetricsAccumulator { get; }

        private IndexUtilizationInfoAccumulator IndexUtilizationInfoAccumulator { get; }

        private ClientSideMetricsAccumulator ClientSideMetricsAccumulator { get; }

        public void Accumulate(QueryMetrics queryMetrics)
        {
            if (queryMetrics == null)
            {
                throw new ArgumentNullException(nameof(queryMetrics));
            }

            this.BackendMetricsAccumulator.Accumulate(queryMetrics.BackendMetrics);
            this.IndexUtilizationInfoAccumulator.Accumulate(queryMetrics.IndexUtilizationInfo);
            this.ClientSideMetricsAccumulator.Accumulate(queryMetrics.ClientSideMetrics);
        }

        public QueryMetrics GetQueryMetrics()
        {
            return new QueryMetrics(
                this.BackendMetricsAccumulator.GetBackendMetrics(),
                this.IndexUtilizationInfoAccumulator.GetIndexUtilizationInfo(),
                this.ClientSideMetricsAccumulator.GetClientSideMetrics());
        }
    }
}
