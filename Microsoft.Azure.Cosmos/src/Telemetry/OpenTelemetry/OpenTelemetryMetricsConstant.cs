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
        /// OperationMetrics
        /// </summary>
        public static class OperationMetrics
        {
            /// <summary>
            /// OperationMetricName
            /// </summary>
            public const string MeterName = "Azure.Cosmos.Client.Operation";

            /// <summary>
            /// MetricVersion100
            /// </summary>
            public const string Version = "1.0.0";

            /// <summary>
            /// Metric Names
            /// </summary>
            public static class Name
            {
                /// <summary>
                /// Total request units per operation (sum of RUs for all requested needed when processing an operation)
                /// </summary>
                public const string RequestCharge = "db.cosmosdb.operation.request_charge";

                /// <summary>
                /// Total end-to-end duration of the operation
                /// </summary>
                public const string Latency = "db.client.operation.duration";

                /// <summary>
                /// For feed operations (query, readAll, readMany, change feed) batch operations this meter capture the actual item count in responses from the service. 
                /// </summary>
                public const string RowCount = "db.client.response.row_count";

                /// <summary>
                /// Number of active SDK client instances. 
                /// </summary>
                public const string ActiveInstances = "db.cosmosdb.client.active_instances";

            }

            /// <summary>
            /// Unit for metrics
            /// </summary>
            public static class Unit
            {
                /// <summary>
                /// Count
                /// </summary>
                public const string Count = "#";

                /// <summary>
                /// Milliseconds
                /// </summary>
                public const string Sec = "s";

                /// <summary>
                /// RUUnit
                /// </summary>
                public const string RequestUnit = "# RU";

            }

            /// <summary>
            /// Metric Descriptions
            /// </summary>
            public static class Description
            {
                /// <summary>
                /// LatencyDesc
                /// </summary>
                public const string Latency = "Total end-to-end duration of the operation";

                /// <summary>
                /// RUDesc
                /// </summary>
                public const string RequestCharge = "Total request units per operation (sum of RUs for all requested needed when processing an operation)";

                /// <summary>
                /// ActualItemDesc
                /// </summary>
                public const string RowCount = "For feed operations (query, readAll, readMany, change feed) batch operations this meter capture the actual item count in responses from the service";

                /// <summary>
                /// InstanceMetricDesc
                /// </summary>
                public const string ActiveInstances = "Number of active SDK client instances.";
            }
        }

        /// <summary>
        /// Buckets
        /// </summary>
        public static class HistogramBuckets
        {
            /// <summary>
            /// ExplicitBucketBoundaries for "db.cosmosdb.operation.request_charge" Metrics
            /// </summary>
            /// <remarks>
            /// <b>1, 5, 10</b>: Low Usage Levels, These smaller buckets allow for precise tracking of operations that consume a minimal number of Request Units. This is important for lightweight operations, such as basic read requests or small queries, where resource utilization should be optimized. Monitoring these low usage levels can help ensure that the application is not inadvertently using more resources than necessary.<br></br>
            /// <b>25, 50</b>: Moderate Usage Levels, These ranges capture more moderate operations, which are typical in many applications. For example, queries that return a reasonable amount of data or perform standard CRUD operations may fall within these limits. Identifying usage patterns in these buckets can help detect efficiency issues in routine operations.<br></br>
            /// <b>100, 250</b>: Higher Usage Levels, These boundaries represent operations that may require significant resources, such as complex queries or larger transactions. Monitoring RUs in these ranges can help identify performance bottlenecks or costly queries that might lead to throttling.<br></br>
            /// <b>500, 1000</b>: Very High Usage Levels, These buckets capture operations that consume a substantial number of Request Units, which may indicate potentially expensive queries or batch processes. Understanding the frequency and patterns of such high RU usage can be critical in optimizing performance and ensuring the application remains within provisioned throughput limits.
            /// </remarks>
            public static readonly double[] RequestUnitBuckets = new double[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000};

            /// <summary>
            /// ExplicitBucketBoundaries for "db.client.operation.duration" Metrics.
            /// </summary>
            /// <remarks>
            /// <b>0.001, 0.005, 0.010</b> seconds: Higher Precision at Sub-Millisecond Levels, For high-performance workloads, especially when dealing with microservices or low-latency queries. <br></br>
            /// <b>0.050, 0.100, 0.200</b> seconds: Granularity for Standard Web Applications, These values allow detailed tracking for latencies between 50ms and 200ms, which are common in web applications. Fine-grained buckets in this range help in identifying performance issues before they grow critical, while covering the typical response times expected in Cosmos DB.<br></br>
            /// <b>0.500, 1.000</b> seconds: Wider Range for Intermediate Latencies, Operations that take longer, in the range of 500ms to 1 second, are still important for performance monitoring. By capturing these values, you maintain awareness of potential bottlenecks or slower requests that may need optimization.<br></br>
            /// <b>2.000, 5.000</b> seconds: Capturing Outliers and Slow Queries, It’s important to track outliers that might go beyond 1 second. Having buckets for 2 and 5 seconds enables identification of rare, long-running operations that may require further investigation.
            /// </remarks>
            public static readonly double[] RequestLatencyBuckets = new double[] { 0.001, 0.005, 0.010, 0.050, 0.100, 0.200, 0.500, 1.000, 2.000, 5.000 };

            /// <summary>
            /// ExplicitBucketBoundaries for "db.client.response.row_count" Metrics
            /// </summary>
            /// <remarks>
            /// <b>10, 50, 100</b>: Small Response Sizes, These buckets are useful for capturing scenarios where only a small number of items are returned. Such small queries are common in real-time or interactive applications where performance and quick responses are critical. They also help in tracking the efficiency of operations that should return minimal data, minimizing resource usage.<br></br>
            /// <b>250, 500, 1000</b>: Moderate Response Sizes, These values represent typical workloads where moderate amounts of data are returned in each query. This is useful for applications that need to return more information, such as data analytics or reporting systems. Tracking these ranges helps identify whether the system is optimized for these relatively larger data sets and if they lead to any performance degradation.<br></br>
            /// <b>2000, 5000</b>: Larger Response Sizes, These boundaries capture scenarios where the query returns large datasets, often used in batch processing or in-depth analytical queries. These larger page sizes can potentially increase memory or CPU usage and may lead to longer query execution times, making it important to track performance in these ranges.<br></br>
            /// <b>10000</b>: Very Large Response Sizes (Outliers), This boundary is included to capture rare, very large response sizes. Such queries can put significant strain on system resources, including memory, CPU, and network bandwidth, and can often lead to performance issues such as high latency or even network drops.
            /// </remarks>
            public static readonly double[] RowCountBuckets = new double[] { 10, 50, 100, 250, 500, 1000, 2000, 5000, 10000 };

            /// <summary>
            /// ExplicitBucketBoundaries for "db.cosmosdb.client.active_instances" Metrics
            /// </summary>
            /// <remarks>
            /// <b>1</b>: Single Instance, This bucket represents the ideal scenario where there is only one active instance of the SDK client per process. Monitoring this level helps confirm that best practices are being followed.<br></br>
            /// <b>2, 3, 5</b>: Low Multi-Instance Usage, These buckets capture scenarios where there are a small number of instances (2 to 5). Monitoring these levels is important as having a couple of instances may be necessary for certain applications, but it can also start to introduce resource contention. Identifying any performance degradation at this level can help detect early signs of potential issues.<br></br>
            /// <b>10, 20</b>: Moderate Multi-Instance Usage, These boundaries capture situations where there are more than a few instances of the SDK client. As the number of active instances increases, the risk of CPU and memory-related issues also rises. Tracking these ranges can provide insight into whether the application is scaling appropriately or if optimizations are required.<br></br>
            /// <b>50, 100</b>: These buckets account for scenarios where the number of active instances is significant. Monitoring these higher values is critical, as having many instances may lead to severe performance impacts, including increased latency and resource exhaustion. This can help identify applications that may be misconfigured or operating beyond their intended limits.
            /// </remarks>
            public static readonly double[] ActiveInstancesBuckets = new double[] { 1, 2, 3, 5, 10, 50, 100 };
        }
    }
}
