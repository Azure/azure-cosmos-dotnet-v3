//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;

    [Flags]
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
    public
#else
    internal
#endif
    enum QueryFeatures : ulong
    {
        None = 0,
        Aggregate = 1 << 0,
        CompositeAggregate = 1 << 1,
        Distinct = 1 << 2,
        GroupBy = 1 << 3,
        MultipleAggregates = 1 << 4,
        MultipleOrderBy = 1 << 5,
        OffsetAndLimit = 1 << 6,
        OrderBy = 1 << 7,
        Top = 1 << 8,
        NonValueAggregate = 1 << 9,
        DCount = 1 << 10,
        NonStreamingOrderBy = 1 << 11,
        ListAndSetAggregate = 1 << 12,
        HybridSearch = 1 << 13,
    }
}