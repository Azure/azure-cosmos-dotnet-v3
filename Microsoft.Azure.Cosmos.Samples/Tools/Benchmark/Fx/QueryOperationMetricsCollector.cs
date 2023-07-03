//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Represents the Benchmark query operation metrics collector.
    /// </summary>
    public class QueryOperationMetricsCollector : MetricsCollector
    {
        /// <summary>
        /// Initializes new instance of <see cref="QueryOperationMetricsCollector"/>.
        /// </summary>
        /// <param name="meter">OpenTelemetry meter.</param>
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
