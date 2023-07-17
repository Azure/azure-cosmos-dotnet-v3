//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal enum ClientQLEnumerableExpressionKind
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
        Unwind,
        Where,
    }
}