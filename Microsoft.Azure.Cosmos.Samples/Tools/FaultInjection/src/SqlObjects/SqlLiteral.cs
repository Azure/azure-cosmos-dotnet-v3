//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class SqlLiteral : SqlObject
    {
        protected SqlLiteral()
        {
        }

        public abstract void Accept(SqlLiteralVisitor visitor);

        public abstract TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor);
    }
}
