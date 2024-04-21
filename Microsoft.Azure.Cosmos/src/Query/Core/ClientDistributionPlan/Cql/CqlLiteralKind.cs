//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal enum CqlLiteralKind
    {
        Undefined,
        Array,
        Boolean,
        Null,
        Number,
        Object,
        String,
    }
}