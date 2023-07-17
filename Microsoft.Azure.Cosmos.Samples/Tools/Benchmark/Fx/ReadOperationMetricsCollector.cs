//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Represents the Benchmark read operation metrics collector.
    /// </summary>
    internal class ReadOperationMetricsCollector : MetricsCollector
    {
        /// <summary>
        /// Initializes new instance of <see cref="ReadOperationMetricsCollector"/>.
        /// </summary>
        /// <param name="meter">OpenTelemetry meter.</param>
        public ReadOperationMetricsCollector(Meter meter) : base(meter)
        {
        }

        /// <summary>
        /// Gets the name of the histogram for requests per second (RPS) metric.
        /// </summary>
        protected override string RpsHistogramName => "ReadOperationRpsHistogram";

        /// <summary>
        /// Gets the name of the histogram for operation latency in milliseconds.
        /// </summary>
        protected override string LatencyInMsHistogramName => "ReadOperationLatencyInMsHistogram";

        /// <summary>
        /// Gets the name of the observable gauge for requests per second (RPS) metric.
        /// </summary>
        protected override string RpsMetricName => "ReadOperationRps";

        /// <summary>
        /// Gets the name of the observable gauge for operation latency in milliseconds.
        /// </summary>
        protected override string LatencyInMsMetricName => "ReadOperationLatencyInMs";

        /// <summary>
        /// Gets the name of the counter for failed operations.
        /// </summary>
        protected override string FailureOperationMetricName => "ReadOperationFailure";

        /// <summary>
        /// Gets the name of the counter for successful operations.
        /// </summary>
        protected override string SuccessOperationMetricName => "ReadOperationSuccess";
    }
}
