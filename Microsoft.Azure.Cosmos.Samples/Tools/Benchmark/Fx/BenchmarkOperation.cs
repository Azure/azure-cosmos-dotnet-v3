//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the Benchmark operation.
    /// </summary>
    internal abstract class BenchmarkOperation : IBenchmarkOperation
    {
        /// <summary>
        /// Executes the Benchmark operation once asynchronously.
        /// </summary>
        /// <returns><see cref="OperationResult"/></returns>
        public abstract Task<OperationResult> ExecuteOnceAsync();

        /// <summary>
        /// Prepares the Benchmark operation asynchronously.
        /// </summary>
        /// <returns><see cref="Task"/> representing the preparation.</returns>
        public abstract Task PrepareAsync();
    }
}
