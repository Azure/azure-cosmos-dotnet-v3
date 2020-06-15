//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    enum SqlBinaryScalarOperatorKind
    {
        Add,
        And,
        BitwiseAnd,
        BitwiseOr,
        BitwiseXor,
        Coalesce,
        Divide,
        Equal,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Modulo,
        Multiply,
        NotEqual,
        Or,
        StringConcat,
        Subtract,
    }
}
