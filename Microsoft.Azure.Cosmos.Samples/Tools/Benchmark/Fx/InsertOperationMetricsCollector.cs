//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Represents the Benchmark insert operation metrics collector.
    /// </summary>
    internal class InsertOperationMetricsCollector : MetricsCollector
    {
        /// <summary>
        /// Initializes new instance of <see cref="InsertOperationMetricsCollector"/>.
        /// </summary>
        /// <param name="meter">OpenTelemetry meter.</param>
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
