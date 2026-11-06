//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Runtime.InteropServices;

    internal static class QueryPlanInterop
    {
        public static IQueryPlanInterop Instance { get; } = Create();

        private static IQueryPlanInterop Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && (RuntimeInformation.ProcessArchitecture == Architecture.X64
                    || RuntimeInformation.ProcessArchitecture == Architecture.Arm64))
            {
                return LinuxQueryPlanInterop.Instance;
            }

            return WindowsX64QueryPlanInterop.Instance;
        }
    }
}
