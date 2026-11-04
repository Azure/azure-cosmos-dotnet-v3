//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using ServiceInteropWrapper = Microsoft.Azure.Documents.ServiceInteropWrapper;

    internal interface IQueryPlanInterop
    {
        bool BypassQueryParsing();

        uint CreateServiceProvider(string configuration, out IntPtr serviceProvider);

        uint UpdateServiceProvider(IntPtr serviceProvider, string configuration);

        void ReleaseServiceProvider(IntPtr serviceProvider);

        uint GetPartitionKeyRangesFromQuery4(
            IntPtr serviceProvider,
            string query,
            ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
            string[] pathTokens,
            uint[] pathTokenLengths,
            uint pathCount,
            string vectorPolicy,
            int vectorPolicyLength,
            IntPtr buffer,
            uint bufferLength,
            out uint resultLength);
    }
}
