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
        /// <summary>
        /// Arithmetic addition.
        /// </summary>
        Add,

        /// <summary>
        /// Logical AND.
        /// </summary>
        And,

        /// <summary>
        /// Bitwise AND.
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// Bitwise OR.
        /// </summary>
        BitwiseOr,

        /// <summary>
        /// Bitwise XOR.
        /// </summary>
        BitwiseXor,

        /// <summary>
        /// Coalesce.
        /// </summary>
        Coalesce,

        /// <summary>
        /// Division.
        /// </summary>
        Divide,

        /// <summary>
        /// Equality.
        /// </summary>
        Equal,

        /// <summary>
        /// Greater Than.
        /// </summary>
        GreaterThan,

        /// <summary>
        /// Greater Than or Equal To.
        /// </summary>
        GreaterThanOrEqual,

        /// <summary>
        /// Less Than.
        /// </summary>
        LessThan,

        /// <summary>
        /// Less Than or Equal To.
        /// </summary>
        LessThanOrEqual,

        /// <summary>
        /// Modulo.
        /// </summary>
        Modulo,

        /// <summary>
        /// Multiply.
        /// </summary>
        Multiply,

        /// <summary>
        /// Not Equals.
        /// </summary>
        NotEqual,

        /// <summary>
        /// Logical Or.
        /// </summary>
        Or,

        /// <summary>
        /// String Concat.
        /// </summary>
        StringConcat,

        /// <summary>
        /// Subtract.
        /// </summary>
        Subtract,
    }
}
