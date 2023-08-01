//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal class QueryMetricsAccumulator
    {
        private readonly List<QueryMetrics> queryMetricsList;

        public QueryMetricsAccumulator()
        {
            this.queryMetricsList = new List<QueryMetrics>();
        }

        public void Accumulate(QueryMetrics queryMetrics)
        {
            if (queryMetrics == null)
            {
                throw new ArgumentNullException(nameof(queryMetrics));
            }

            this.queryMetricsList.Add(queryMetrics);
        }

        public QueryMetrics GetQueryMetrics()
        {
            BackendMetricsAccumulator backendMetricsAccumulator = new BackendMetricsAccumulator();
            IndexUtilizationInfoAccumulator indexUtilizationInfoAccumulator = new IndexUtilizationInfoAccumulator();
            ClientSideMetricsAccumulator clientSideMetricsAccumulator = new ClientSideMetricsAccumulator();

            foreach (QueryMetrics queryMetrics in this.queryMetricsList)
            {
                backendMetricsAccumulator.Accumulate(queryMetrics.BackendMetrics);
                indexUtilizationInfoAccumulator.Accumulate(queryMetrics.IndexUtilizationInfo);
                clientSideMetricsAccumulator.Accumulate(queryMetrics.ClientSideMetrics);
            }

            return new QueryMetrics(
                backendMetricsAccumulator.GetBackendMetrics(),
                indexUtilizationInfoAccumulator.GetIndexUtilizationInfo(),
                clientSideMetricsAccumulator.GetClientSideMetrics());
        }
    }
}
