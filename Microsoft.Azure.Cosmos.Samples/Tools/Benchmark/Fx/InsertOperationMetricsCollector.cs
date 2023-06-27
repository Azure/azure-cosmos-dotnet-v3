//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using Microsoft.ApplicationInsights;
    using System.Diagnostics.Metrics;

    internal class InsertOperationMetricsCollector : MetricsCollector
    {
        public InsertOperationMetricsCollector(Meter meter) : base(meter)
        {
        }

        protected override string RpsHistogramName => "InsertOperationRpsHistogram";

        protected override string LatencyInMsHistogramName => "InsertOperationLatencyInMsHistogram";

        protected override string RpsMetricName => "InsertOperationRps";

        protected override string LatencyInMsMetricName => "InsertOperationLatencyInMs";

        protected override string FailureOperationMetricName => "InsertOperationFailure";

        protected override string SuccessOperationMetricName => "InsertOperationSuccess";
    }
}
