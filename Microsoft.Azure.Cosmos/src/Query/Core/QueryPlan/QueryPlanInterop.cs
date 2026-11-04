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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                        return LinuxQueryPlanInterop.X64.Instance;
                    case Architecture.Arm64:
                        return LinuxQueryPlanInterop.Arm64.Instance;
                }
            }

            return ServiceInteropQueryPlan.Instance;
        }
    }
}
