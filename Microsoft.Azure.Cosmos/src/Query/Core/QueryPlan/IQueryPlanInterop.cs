//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;

    internal interface IQueryPlanInterop
    {
        bool BypassQueryParsing();

        uint CreateServiceProvider(string configuration, out IntPtr serviceProvider);

        uint UpdateServiceProvider(IntPtr serviceProvider, string configuration);

        uint GetPartitionKeyRanges(
            IntPtr serviceProvider,
            string query,
            PartitionKeyRangesOptions options,
            string[] pathTokens,
            uint[] pathTokenLengths,
            uint pathCount,
            string vectorPolicy,
            IntPtr buffer,
            uint bufferLength,
            out uint resultLength);
    }
}
