//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Metrics;

    internal class QueryOperationMetricsCollector : MetricsCollector
    {
        public QueryOperationMetricsCollector(Meter meter) : base(meter)
        {
        }

        protected override string RpsHistogramName => "QueryOperationRpsHistogram";

        protected override string LatencyInMsHistogramName => "QueryOperationLatencyInMsHistogram";

        protected override string RpsMetricName => "QueryOperationRps";

        protected override string LatencyInMsMetricName => "QueryOperationLatencyInMs";

        protected override string FailureOperationMetricName => "QueryOperationFailure";

        protected override string SuccessOperationMetricName => "QueryOperationSuccess";
    }
}
