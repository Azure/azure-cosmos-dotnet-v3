//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    internal enum QLEnumerableExpressionKind
    {
        Aggregate,
        Distinct,
        GroupBy,
        Flatten,
        Input,
        OrderBy,
        ScalarAsEnumerable,
        Select,
        SelectMany,
        Take,
        Where,
    }
}