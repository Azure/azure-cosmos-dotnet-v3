//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Threading.Tasks;

    internal interface IBenchmarkOperation
    {
        Task PrepareAsync();

        OperationResult ExecuteOnceAsync();
    }
}
