﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal enum CqlScalarExpressionKind
    {
        ArrayCreate,
        ArrayIndexer,
        BinaryOperator,
        IsOperator,
        Let,
        Literal,
        Mux,
        ObjectCreate,
        PropertyRef,
        SystemFunctionCall,
        TupleCreate,
        TupleItemRef,
        UnaryOperator,
        UserDefinedFunctionCall,
        VariableRef,
    }
}