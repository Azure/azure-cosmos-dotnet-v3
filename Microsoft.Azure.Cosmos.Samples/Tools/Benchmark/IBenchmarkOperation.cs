//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Threading.Tasks;

    /// <summary>
    /// Representes the Benchmark operation.
    /// </summary>
    internal interface IBenchmarkOperation
    {
        /// <summary>
        /// Executes Benchmark operation once asynchronously.
        /// </summary>
        /// <returns>The operation result wrapped by task.</returns>
        Task<OperationResult> ExecuteOnceAsync();

        /// <summary>
        /// Prepares Benchmark operation asynchronously.
        /// </summary>
        /// <returns>The task related to method's work.</returns>
        Task PrepareAsync();
    }
}
