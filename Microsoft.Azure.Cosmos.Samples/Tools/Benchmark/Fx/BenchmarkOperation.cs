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
        /// Executes Benchmark operation once asynchronously.
        /// </summary>
        /// <returns>The operation result wrapped by task.</returns>
        public abstract Task<OperationResult> ExecuteOnceAsync();

        /// <summary>
        /// Prepares Benchmark operation asynchronously.
        /// </summary>
        /// <returns>The task related to method's work.</returns>
        public abstract Task PrepareAsync();
    }
}
