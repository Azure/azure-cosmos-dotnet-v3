﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal enum ClientQLAggregateOperatorKind
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