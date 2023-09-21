//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    internal enum QLAggregateOperatorKind
    {
        All,
        Any,
        Array,
        CMax,
        CMin,
        Count,
        CSum,
        First,
        Last,
        Max,
        Min,
        Sum,
    }
}