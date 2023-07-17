//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal enum ClientQLScalarExpressionKind
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