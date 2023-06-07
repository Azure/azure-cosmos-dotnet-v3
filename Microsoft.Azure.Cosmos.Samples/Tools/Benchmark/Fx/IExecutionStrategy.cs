//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Threading.Tasks;
    using App.Metrics;
    using Microsoft.Extensions.Logging;

    internal interface IExecutionStrategy
    {
        public static IExecutionStrategy StartNew(
            Func<IBenchmarkOperation> benchmarkOperation, IMetricsCollector metricsCollector)
        {
            return new ParallelExecutionStrategy(benchmarkOperation, metricsCollector);
        }

        public Task<RunSummary> ExecuteAsync(
            BenchmarkConfig benchmarkConfig,
            int serialExecutorConcurrency,
            int serialExecutorIterationCount,
            double warmupFraction,
            ILogger logger,
            IMetrics metrics);
    }
}
