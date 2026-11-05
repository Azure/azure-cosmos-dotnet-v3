//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using CustomTypeExtensions = Microsoft.Azure.Documents.CustomTypeExtensions;
    using ServiceInteropWrapper = Microsoft.Azure.Documents.ServiceInteropWrapper;

    internal sealed class WindowsX64QueryPlanInterop : IQueryPlanInterop
    {
        private WindowsX64QueryPlanInterop()
        {
        }

        public static IQueryPlanInterop Instance { get; } = new WindowsX64QueryPlanInterop();

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

        public uint GetPartitionKeyRanges(
            IntPtr serviceProvider,
            string query,
            PartitionKeyRangesOptions options,
            string[] pathTokens,
            uint[] pathTokenLengths,
            uint pathCount,
            string vectorPolicy,
            IntPtr buffer,
            uint bufferLength,
            out uint resultLength)
        {
            ServiceInteropWrapper.PartitionKeyRangesApiOptions nativeOptions = new ServiceInteropWrapper.PartitionKeyRangesApiOptions()
            {
                bRequireFormattableOrderByQuery = Convert.ToInt32(options.RequireFormattableOrderByQuery),
                bIsContinuationExpected = Convert.ToInt32(options.IsContinuationExpected),
                bAllowNonValueAggregateQuery = Convert.ToInt32(options.AllowNonValueAggregateQuery),
                bHasLogicalPartitionKey = Convert.ToInt32(options.HasLogicalPartitionKey),
                bAllowDCount = Convert.ToInt32(options.AllowDCount),
                bUseSystemPrefix = Convert.ToInt32(options.UseSystemPrefix),
                ePartitionKind = Convert.ToInt32(options.PartitionKind),
                eGeospatialType = Convert.ToInt32(options.GeospatialType),
                bHybridSearchSkipOrderByRewrite = Convert.ToInt32(options.HybridSearchSkipOrderByRewrite),
            };

            return ServiceInteropWrapper.GetPartitionKeyRangesFromQuery4(
                serviceProvider,
                query,
                nativeOptions,
                pathTokens,
                pathTokenLengths,
                pathCount,
                vectorPolicy,
                vectorPolicy?.Length ?? 0,
                buffer,
                bufferLength,
                out resultLength);
        }
    }
}
