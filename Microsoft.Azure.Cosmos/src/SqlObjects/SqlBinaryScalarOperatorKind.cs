//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlBinaryScalarOperatorKind.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal enum SqlBinaryScalarOperatorKind
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
