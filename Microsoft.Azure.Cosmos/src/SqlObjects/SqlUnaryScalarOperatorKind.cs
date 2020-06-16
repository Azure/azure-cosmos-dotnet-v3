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
    enum SqlUnaryScalarOperatorKind
    {
        /// <summary>
        /// Bitwise Not.
        /// </summary>
        BitwiseNot,

        /// <summary>
        /// Not.
        /// </summary>
        Not,

        /// <summary>
        /// Minus.
        /// </summary>
        Minus,

        /// <summary>
        /// Plus.
        /// </summary>
        Plus,
    }
}
