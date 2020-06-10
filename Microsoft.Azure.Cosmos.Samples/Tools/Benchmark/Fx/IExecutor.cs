//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Threading.Tasks;

    internal interface IExecutor
    {
        public int SuccessOperationCount { get; }
        public int FailedOperationCount { get; }
        public double TotalRuCharges { get; }

        public Task ExecuteAsync(
                int iterationCount,
                bool isWarmup,
                bool traceFalures,
                Action completionCallback);
    }
}
