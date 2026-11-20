//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Runtime.InteropServices;
    using PartitionKind = Documents.PartitionKind;

    // Layout must match the native PartitionKeyRangesApiOptions struct shared by ServiceInterop and QueryPlanInterop.
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct PartitionKeyRangesOptions
    {
        private readonly int requireFormattableOrderByQuery;
        private readonly int isContinuationExpected;
        private readonly int allowNonValueAggregateQuery;
        private readonly int hasLogicalPartitionKey;
        private readonly int allowDCount;
        private readonly int useSystemPrefix;
        private readonly int partitionKind;
        private readonly int geospatialType;
        private readonly int hybridSearchSkipOrderByRewrite;
        private readonly int reserved1;
        private readonly long reserved2;
        private readonly long reserved3;
        private readonly long reserved4;

        public PartitionKeyRangesOptions(
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            bool allowDCount,
            bool useSystemPrefix,
            bool hybridSearchSkipOrderByRewrite,
            PartitionKind partitionKind,
            GeospatialType geospatialType)
        {
            this.requireFormattableOrderByQuery = requireFormattableOrderByQuery ? 1 : 0;
            this.isContinuationExpected = isContinuationExpected ? 1 : 0;
            this.allowNonValueAggregateQuery = allowNonValueAggregateQuery ? 1 : 0;
            this.hasLogicalPartitionKey = hasLogicalPartitionKey ? 1 : 0;
            this.allowDCount = allowDCount ? 1 : 0;
            this.useSystemPrefix = useSystemPrefix ? 1 : 0;
            this.partitionKind = (int)partitionKind;
            this.geospatialType = (int)geospatialType;
            this.hybridSearchSkipOrderByRewrite = hybridSearchSkipOrderByRewrite ? 1 : 0;
            this.reserved1 = 0;
            this.reserved2 = 0;
            this.reserved3 = 0;
            this.reserved4 = 0;
        }

        public bool RequireFormattableOrderByQuery => this.requireFormattableOrderByQuery != 0;

        public bool IsContinuationExpected => this.isContinuationExpected != 0;

        public bool AllowNonValueAggregateQuery => this.allowNonValueAggregateQuery != 0;

        public bool HasLogicalPartitionKey => this.hasLogicalPartitionKey != 0;

        public bool AllowDCount => this.allowDCount != 0;

        public bool UseSystemPrefix => this.useSystemPrefix != 0;

        public bool HybridSearchSkipOrderByRewrite => this.hybridSearchSkipOrderByRewrite != 0;

        public PartitionKind PartitionKind => (PartitionKind)this.partitionKind;

        public GeospatialType GeospatialType => (GeospatialType)this.geospatialType;
    }
}
