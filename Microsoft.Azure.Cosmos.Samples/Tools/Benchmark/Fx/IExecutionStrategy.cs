//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Threading.Tasks;

    internal interface IExecutionStrategy
    {
        Task ExecuteAsync(
            int serialExecutorConcurrency,
            int serialExecutorIterationCount,
            double warmupFraction);

    }
}
