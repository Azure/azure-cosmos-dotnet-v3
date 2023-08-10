//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Threading.Tasks;
    using OpenTelemetry.Metrics;

    internal interface IExecutionStrategy
    {
        public static IExecutionStrategy StartNew(
            Func<IBenchmarkOperation> benchmarkOperation)
        {
            return new ParallelExecutionStrategy(benchmarkOperation);
        }

        public Task<RunSummary> ExecuteAsync(
            BenchmarkConfig benchmarkConfig,
            int serialExecutorConcurrency,
            int serialExecutorIterationCount,
            double warmupFraction,
            MeterProvider meterProvider, 
            MetricsCollectorProvider metricsCollectorProvider);
    }
}
