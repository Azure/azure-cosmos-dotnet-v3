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

        /// <summary>
        /// Gets the name of the histogram for requests per second (RPS) metric.
        /// </summary>
        protected override string RpsHistogramName => "InsertOperationRpsHistogram";

        /// <summary>
        /// Gets the name of the histogram for operation latency in milliseconds.
        /// </summary>
        protected override string LatencyInMsHistogramName => "InsertOperationLatencyInMsHistogram";

        /// <summary>
        /// Gets the name of the observable gauge for requests per second (RPS) metric.
        /// </summary>
        protected override string RpsMetricName => "InsertOperationRps";

        /// <summary>
        /// Gets the name of the observable gauge for operation latency in milliseconds.
        /// </summary>
        protected override string LatencyInMsMetricName => "InsertOperationLatencyInMs";

        /// <summary>
        /// Gets the name of the counter for failed operations.
        /// </summary>
        protected override string FailureOperationMetricName => "InsertOperationFailure";

        /// <summary>
        /// Gets the name of the counter for successful operations.
        /// </summary>
        protected override string SuccessOperationMetricName => "InsertOperationSuccess";
    }
}
