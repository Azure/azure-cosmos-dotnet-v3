﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;

    internal sealed class QueryMetricsAccumulator
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
            ServerSideMetricsInternalAccumulator serverSideMetricsAccumulator = new ServerSideMetricsInternalAccumulator();
            IndexUtilizationInfoAccumulator indexUtilizationInfoAccumulator = new IndexUtilizationInfoAccumulator();
            ClientSideMetricsAccumulator clientSideMetricsAccumulator = new ClientSideMetricsAccumulator();

            foreach (QueryMetrics queryMetrics in this.queryMetricsList)
            {
                serverSideMetricsAccumulator.Accumulate(queryMetrics.ServerSideMetrics);
                indexUtilizationInfoAccumulator.Accumulate(queryMetrics.IndexUtilizationInfo);
                clientSideMetricsAccumulator.Accumulate(queryMetrics.ClientSideMetrics);
            }

            return new QueryMetrics(
                serverSideMetricsAccumulator.GetServerSideMetrics(),
                indexUtilizationInfoAccumulator.GetIndexUtilizationInfo(),
                clientSideMetricsAccumulator.GetClientSideMetrics());
        }
    }
}
