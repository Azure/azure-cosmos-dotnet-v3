//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Threading.Tasks;

    internal abstract class BenchmarkOperation : IBenchmarkOperation
    {
        public abstract Task<OperationResult> ExecuteOnceAsync();

        public abstract Task PrepareAsync();
    }
}
