// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
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
            /// RUDesc
            /// </summary>
            public const string RUDesc = "Total request units per operation (sum of RUs for all requested needed when processing an operation)";

            /// <summary>
            /// RUName
            /// </summary>
            public const string MaxItemName = "cosmos.client.op.maxItemCount";

            /// <summary>
            /// RUDesc
            /// </summary>
            public const string MaxItemDesc = "For feed operations (query, readAll, readMany, change feed) and batch operations this meter capture the requested maxItemCount per page/request";

            /// <summary>
            /// ActualItemName
            /// </summary>
            public const string ActualItemName = "cosmos.client.op.actualItemCount";

            /// <summary>
            /// ActualItemDesc
            /// </summary>
            public const string ActualItemDesc = "For feed operations (query, readAll, readMany, change feed) batch operations this meter capture the actual item count in responses from the service";

            /// <summary>
            /// RegionContactedName
            /// </summary>
            public const string RegionContactedName = "cosmos.client.op.regionsContacted";

            /// <summary>
            /// RegionContactedDesc
            /// </summary>
            public const string RegionContactedDesc = "Number of regions contacted when executing an operation";

            /// <summary>
            /// Count
            /// </summary>
            public const string Count = "#";

            /// <summary>
            /// Milliseconds
            /// </summary>
            public const string Ms = "ms";

            /// <summary>
            /// RUUnit
            /// </summary>
            public const string RUUnit = "# RU";
            
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
