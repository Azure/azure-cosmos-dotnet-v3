//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Threading.Tasks;

    internal interface IExecutor
    {
        int SuccessOperationCount { get; }
        int FailedOperationCount { get; }
        double TotalRuCharges { get; }

        Task ExecuteAsync(
                int iterationCount,
                bool isWarmup,
                Action completionCallback);
    }
}
