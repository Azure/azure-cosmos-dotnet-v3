//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Threading.Tasks;

    internal interface IExecutionStrategy
    {
        public static IExecutionStrategy StartNew(
            BenchmarkConfig config,
            Func<IBenchmarkOperatrion> benchmarkOperation)
        {
            return new ParallelExecutionStrategy(benchmarkOperation);
        }

        public Task ExecuteAsync(
            int serialExecutorConcurrency,
            int serialExecutorIterationCount,
            bool traceFalures,
            double warmupFraction);

    }
}
