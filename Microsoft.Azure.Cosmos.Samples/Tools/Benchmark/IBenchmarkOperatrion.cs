//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Threading.Tasks;

    internal interface IBenchmarkOperatrion
    {
        void Prepare();

        Task<OperationResult> ExecuteOnceAsync();
    }
}
