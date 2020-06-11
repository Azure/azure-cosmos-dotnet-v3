//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Threading.Tasks;

    internal interface IExecutionStrategy
    {
        public static IExecutionStrategy StartNew(
            BenchmarkConfig config,
            IBenchmarkOperatrion benchmarkOperation)
        {
            return new ParallelExecutionStrategy(benchmarkOperation);
        }

        public Task ExecuteAsync(
            int serialExecutorConcurrency,
            int serialExecutorIterationCount,
            double warmupFraction);

    }
}
