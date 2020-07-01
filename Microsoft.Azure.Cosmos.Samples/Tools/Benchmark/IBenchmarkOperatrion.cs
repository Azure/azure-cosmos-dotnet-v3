//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Threading.Tasks;

    internal interface IBenchmarkOperatrion
    {
        Task Prepare();

        Task<OperationResult> ExecuteOnceAsync();
    }
}
