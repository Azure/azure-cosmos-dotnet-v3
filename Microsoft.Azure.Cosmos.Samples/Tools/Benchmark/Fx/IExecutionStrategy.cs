//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Threading.Tasks;

    internal abstract class IExecutionStrategy
    {
        public static IExecutionStrategy StartNew(
            BenchmarkConfig config,
            Func<IBenchmarkOperation> benchmarkOperation)
        {
            return new ParallelExecutionStrategy(benchmarkOperation);
        }

        public abstract Task<RunSummary> ExecuteAsync(
            int serialExecutorConcurrency,
            int serialExecutorIterationCount,
            bool traceFalures,
            double warmupFraction);

    }
}
