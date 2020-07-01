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
    abstract class SqlCollectionExpression : SqlObject
    {
        protected SqlCollectionExpression()
        {
        }

        public abstract void Accept(SqlCollectionExpressionVisitor visitor);

        public abstract TResult Accept<TResult>(SqlCollectionExpressionVisitor<TResult> visitor);

        public abstract TResult Accept<T, TResult>(SqlCollectionExpressionVisitor<T, TResult> visitor, T input);
    }
}
