//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Metrics;

    internal class ReadOperationMetricsCollector : MetricsCollector
    {
        public ReadOperationMetricsCollector(Meter meter) : base(meter)
        {
        }

        protected override string RpsHistogramName => "ReadOperationRpsHistogram";

        protected override string LatencyInMsHistogramName => "ReadOperationLatencyInMsHistogram";

        protected override string RpsMetricName => "ReadOperationRps";

        protected override string LatencyInMsMetricName => "ReadOperationLatencyInMs";

        protected override string FailureOperationMetricName => "ReadOperationFailure";

        protected override string SuccessOperationMetricName => "ReadOperationSuccess";
    }
}
