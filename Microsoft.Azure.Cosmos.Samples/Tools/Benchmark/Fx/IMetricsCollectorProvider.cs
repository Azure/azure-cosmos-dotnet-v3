//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using OpenTelemetry.Metrics;

    public interface IMetricsCollectorProvider
    {
        public static InsertOperationMetricsCollector InsertOperationMetricsCollector { get; }

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.QueryOperationMetricsCollector"/>.
        /// </summary>
        QueryOperationMetricsCollector QueryOperationMetricsCollector { get; }

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.ReadOperationMetricsCollector"/>.
        /// </summary>
        ReadOperationMetricsCollector ReadOperationMetricsCollector { get; }

        IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation);
    }
}
