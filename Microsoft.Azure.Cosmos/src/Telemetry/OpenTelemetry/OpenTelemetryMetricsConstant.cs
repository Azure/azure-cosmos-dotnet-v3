// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetryMetricsConstant
    /// </summary>
    public sealed class OpenTelemetryMetricsConstant
    {
        /// <summary>
        /// OperationMetricName
        /// </summary>
        public const string OperationMetricName = "Azure.Cosmos.Client.Operation";

        /// <summary>
        /// MetricVersion100
        /// </summary>
        public const string MetricVersion100 = "1.0.0";

        /// <summary>
        /// OperationMetrics
        /// </summary>
        public static class OperationMetrics
        {
            /// <summary>
            /// NumberOfCalls
            /// </summary>
            public const string NumberOfCallsName = "cosmos.client.op.calls";

            /// <summary>
            /// Count
            /// </summary>
            public const string Count = "#";

            /// <summary>
            /// NumberOfCallsDesc
            /// </summary>
            public const string NumberOfCallsDesc = "Number of operation calls";

            /// <summary>
            /// LatencyName
            /// </summary>
            public const string LatencyName = "cosmos.client.op.latency";

            /// <summary>
            /// LatencyDesc
            /// </summary>
            public const string LatencyDesc = "Total end-to-end duration of the operation";

            /// <summary>
            /// RUName
            /// </summary>
            public const string RUName = "cosmos.client.op.RUs";

            /// <summary>
            /// RUUnit
            /// </summary>
            public const string RUUnit = "# RU";

            /// <summary>
            /// RUDesc
            /// </summary>
            public const string RUDesc = "Total request units per operation (sum of RUs for all requested needed when processing an operation)";
        }

        /// <summary>
        /// Buckets
        /// </summary>
        public static class HistogramBuckets
        {
            /// <summary>
            /// / RequestUnitBuckets
            /// </summary>
            public static readonly double[] RequestUnitBuckets = new double[] { 3, 6, 9, 10, 50, 100 };

            /// <summary>
            /// / RequestLatencyBuckets
            /// </summary>
            public static readonly double[] RequestLatencyBuckets = new double[] { 10, 50, 100, 200, 300, 400, 500, 1000};
        }
    }
}
