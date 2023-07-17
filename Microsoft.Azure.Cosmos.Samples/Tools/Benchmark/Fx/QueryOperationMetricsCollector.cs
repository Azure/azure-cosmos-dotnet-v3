//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Represents the Benchmark query operation metrics collector.
    /// </summary>
    internal class QueryOperationMetricsCollector : MetricsCollector
    {
        /// <summary>
        /// Initializes new instance of <see cref="QueryOperationMetricsCollector"/>.
        /// </summary>
        /// <param name="meter">OpenTelemetry meter.</param>
        public QueryOperationMetricsCollector(Meter meter) : base(meter)
        {
        }

        /// <summary>
        /// Gets the name of the histogram for requests per second (RPS) metric.
        /// </summary>
        protected override string RpsHistogramName => "QueryOperationRpsHistogram";

        /// <summary>
        /// Gets the name of the histogram for operation latency in milliseconds.
        /// </summary>
        protected override string LatencyInMsHistogramName => "QueryOperationLatencyInMsHistogram";

        /// <summary>
        /// Gets the name of the observable gauge for requests per second (RPS) metric.
        /// </summary>
        protected override string RpsMetricName => "QueryOperationRps";

        /// <summary>
        /// Gets the name of the observable gauge for operation latency in milliseconds.
        /// </summary>
        protected override string LatencyInMsMetricName => "QueryOperationLatencyInMs";

        /// <summary>
        /// Gets the name of the counter for failed operations.
        /// </summary>
        protected override string FailureOperationMetricName => "QueryOperationFailure";

        /// <summary>
        /// Gets the name of the counter for successful operations.
        /// </summary>
        protected override string SuccessOperationMetricName => "QueryOperationSuccess";
    }
}
