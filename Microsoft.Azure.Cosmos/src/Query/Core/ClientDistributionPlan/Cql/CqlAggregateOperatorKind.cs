//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal enum CqlAggregateOperatorKind
    {
        All,
        Any,
        Array,
        Count,
        First,
        Last,
        Max,
        Min,
        Sum,
    }
}