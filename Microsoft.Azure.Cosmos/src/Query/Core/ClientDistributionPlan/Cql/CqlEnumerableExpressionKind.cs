//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal enum CqlEnumerableExpressionKind
    {
        Aggregate,
        Distinct,
        GroupBy,
        Input,
        OrderBy,
        ScalarAsEnumerable,
        Select,
        SelectMany,
        Take,
        Where,
    }
}