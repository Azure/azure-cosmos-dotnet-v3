//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Runtime.InteropServices;
    using CustomTypeExtensions = Microsoft.Azure.Documents.CustomTypeExtensions;
    using ServiceInteropWrapper = Microsoft.Azure.Documents.ServiceInteropWrapper;

    internal sealed class ServiceInteropQueryPlan : IQueryPlanInterop
    {
        private ServiceInteropQueryPlan()
        {
        }

        public static IQueryPlanInterop Instance { get; } = new ServiceInteropQueryPlan();

        public bool BypassQueryParsing()
        {
            return CustomTypeExtensions.ByPassQueryParsing();
        }

        public uint CreateServiceProvider(string configuration, out IntPtr serviceProvider)
        {
            return ServiceInteropWrapper.CreateServiceProvider(configuration, out serviceProvider);
        }

        public uint UpdateServiceProvider(IntPtr serviceProvider, string configuration)
        {
            return ServiceInteropWrapper.UpdateServiceProvider(serviceProvider, configuration);
        }

        public void ReleaseServiceProvider(IntPtr serviceProvider)
        {
            Marshal.Release(serviceProvider);
        }

        public uint GetPartitionKeyRangesFromQuery4(
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
            out uint resultLength)
        {
            return ServiceInteropWrapper.GetPartitionKeyRangesFromQuery4(
                serviceProvider,
                query,
                options,
                pathTokens,
                pathTokenLengths,
                pathCount,
                vectorPolicy,
                vectorPolicyLength,
                buffer,
                bufferLength,
                out resultLength);
        }
    }
}
