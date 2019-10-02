//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    [Flags]
    internal enum QueryFeatures : ulong
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
    }
}